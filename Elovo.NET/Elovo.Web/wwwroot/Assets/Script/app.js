(() => {
    const pageLoader = document.querySelector("#pageLoader");
    const keepAliveIntervalMs = 10 * 60 * 1000;
    const { t } = window.ElovoI18n;

    function keepAlive() {
        fetch(`/health?ts=${Date.now()}`, {
            cache: "no-store",
            credentials: "same-origin"
        }).catch(() => { });
    }

    window.setInterval(keepAlive, keepAliveIntervalMs);
    keepAlive();
    document.addEventListener("visibilitychange", () => {
        if (!document.hidden) {
            keepAlive();
        }
    });

    function showPageLoader() {
        if (!pageLoader) {
            return;
        }

        pageLoader.setAttribute("aria-hidden", "false");
        document.body.classList.add("is-page-loading");
    }

    function hidePageLoader() {
        if (!pageLoader) {
            return;
        }

        pageLoader.setAttribute("aria-hidden", "true");
        document.body.classList.remove("is-page-loading");
    }

    function navigateWithLoader(url) {
        showPageLoader();
        window.location.href = url;
    }

    function getAntiForgeryToken() {
        const token = document.querySelector("input[name='__RequestVerificationToken']");
        return token ? token.value : "";
    }

    const avatarCacheDbName = "elovo-avatar-cache";
    const avatarCacheStoreName = "avatars";
    let avatarCacheDb = null;
    let avatarCacheDbPromise = null;

    function openAvatarCacheDb() {
        if (!window.indexedDB) {
            return Promise.reject(new Error("IndexedDB is unavailable."));
        }
        if (avatarCacheDb) return Promise.resolve(avatarCacheDb);
        if (avatarCacheDbPromise) return avatarCacheDbPromise;

        avatarCacheDbPromise = new Promise((resolve, reject) => {
            const request = window.indexedDB.open(avatarCacheDbName, 1);
            request.addEventListener("upgradeneeded", () => {
                const db = request.result;
                if (!db.objectStoreNames.contains(avatarCacheStoreName)) {
                    db.createObjectStore(avatarCacheStoreName);
                }
            });
            request.addEventListener("success", () => {
                avatarCacheDb = request.result;
                avatarCacheDb.addEventListener("close", () => {
                    avatarCacheDb = null;
                    avatarCacheDbPromise = null;
                });
                resolve(avatarCacheDb);
            });
            request.addEventListener("error", () => reject(request.error || new Error("Avatar cache failed.")));
        });
        return avatarCacheDbPromise;
    }

    async function runAvatarCacheRequest(mode, transactionCallback) {
        const db = await openAvatarCacheDb();
        return new Promise((resolve, reject) => {
            const transaction = db.transaction(avatarCacheStoreName, mode);
            const store = transaction.objectStore(avatarCacheStoreName);
            const request = transactionCallback(store);

            transaction.addEventListener("complete", () => resolve(request ? request.result : undefined));
            transaction.addEventListener("error", () => reject(transaction.error || new Error("Avatar cache transaction failed.")));
        });
    }

    const pendingAvatarFetches = new Map();

    async function getOrFetchAvatar(userId, url) {
        if (!url || url.startsWith("blob:") || url.startsWith("data:")) return url;
        if (!userId) {
            const match = url.match(/\/([a-f0-9\-]{36})\/[^/]+\.(webp|png|jpg|jpeg)$/i);
            if (match) userId = match[1];
        }
        if (!userId) return url;

        const isSmall = url.includes("_small");
        const urlProperty = isSmall ? "smallUrl" : "originalUrl";
        const blobProperty = isSmall ? "smallBlob" : "originalBlob";

        let cached = null;
        try {
            cached = await runAvatarCacheRequest("readonly", store => store.get(userId));
        } catch (e) {
            console.error("Avatar cache read error", e);
        }

        if (cached && cached[urlProperty] === url && cached[blobProperty]) {
            return URL.createObjectURL(cached[blobProperty]);
        }

        if (pendingAvatarFetches.has(url)) {
            return pendingAvatarFetches.get(url);
        }

        const fetchPromise = (async () => {
            try {
                const response = await fetch(url, { cache: "no-store" });
                if (!response.ok) throw new Error("Fetch failed");
                const blob = await response.blob();
                
                await runAvatarCacheRequest("readwrite", async store => {
                    return new Promise((resolve, reject) => {
                        const getReq = store.get(userId);
                        getReq.onsuccess = () => {
                            const latestCached = getReq.result || {};
                            latestCached[urlProperty] = url;
                            latestCached[blobProperty] = blob;
                            const putReq = store.put(latestCached, userId);
                            putReq.onsuccess = () => resolve();
                            putReq.onerror = () => reject(putReq.error);
                        };
                        getReq.onerror = () => reject(getReq.error);
                    });
                });

                return URL.createObjectURL(blob);
            } catch (e) {
                console.error("Avatar fetch/cache error", e);
                return url;
            } finally {
                pendingAvatarFetches.delete(url);
            }
        })();

        pendingAvatarFetches.set(url, fetchPromise);
        return fetchPromise;
    }

    async function preloadUserAvatars(user) {
        if (!user) return;
        const userId = user.id || user.userId || user.otherUserId || user.senderId || user.callerId;
        const originalUrl = user.profileImageUrl;
        const smallUrl = user.profileImageSmallUrl || originalUrl;

        if (smallUrl) await getOrFetchAvatar(userId, smallUrl);
        if (originalUrl && originalUrl !== smallUrl) await getOrFetchAvatar(userId, originalUrl);
    }

    function setAvatarElement(element, item, initialFallback = "?") {
        if (!element) {
            return;
        }

        element.innerHTML = "";
        
        const originalUrl = item && item.profileImageUrl ? item.profileImageUrl : "";
        const smallUrl = item && item.profileImageSmallUrl ? item.profileImageSmallUrl : originalUrl;
        
        if (originalUrl) {
            element.dataset.originalUrl = originalUrl;
            element.dataset.userId = item.id || item.userId || item.otherUserId || item.senderId || item.callerId || "";
        } else {
            delete element.dataset.originalUrl;
            delete element.dataset.userId;
        }

        if (smallUrl) {
            const fetchId = Math.random().toString();
            element.dataset.avatarFetchId = fetchId;
            element.textContent = item && item.initial ? item.initial : initialFallback;

            const userId = element.dataset.userId;

            getOrFetchAvatar(userId, smallUrl).then(localUrl => {
                if (element.dataset.avatarFetchId !== fetchId) return;
                element.innerHTML = "";
                const image = document.createElement("img");
                image.src = localUrl;
                image.alt = "";
                element.appendChild(image);
            });
            return;
        }

        element.textContent = item && item.initial ? item.initial : initialFallback;
    }

    async function readResponseText(response) {
        const text = await response.text();
        const cooldownMatch = text.match(/^You can request another email in ([0-9]{2,}:[0-9]{2})\.$/);
        if (cooldownMatch) {
            return t("Next email available in {time}", { time: cooldownMatch[1] });
        }

        return t(text || "Request failed.");
    }

    function syncModalScrollLock() {
        const hasOpenModal = document.querySelector(".modal-backdrop.is-open, .image-preview-backdrop.is-open");
        document.documentElement.classList.toggle("has-open-modal", !!hasOpenModal);
        document.body.classList.toggle("has-open-modal", !!hasOpenModal);
    }

    function openModal(modal) {
        if (!modal) {
            return;
        }

        modal.classList.add("is-open");
        modal.setAttribute("aria-hidden", "false");
        syncModalScrollLock();
    }

    function closeModal(modal) {
        if (!modal) {
            return;
        }

        modal.classList.remove("is-open");
        modal.setAttribute("aria-hidden", "true");
        syncModalScrollLock();
    }

    const chatRetentionDefaultDays = 90;
    const chatRetentionAllowedDays = [7, 14, 30, 45, 90];
    const imageCacheDbName = "elovo-image-cache";
    const imageCacheStoreName = "images";

    function normalizeUserId(value) {
        return (value || "").toLowerCase();
    }

    function getCurrentUserId() {
        return normalizeUserId(window.elovoCurrentUserId);
    }

    function getChatRetentionStorageKey(userId = getCurrentUserId()) {
        return `elovo:chat-retention-days:${normalizeUserId(userId)}`;
    }

    function getChatRetentionDays(userId = getCurrentUserId()) {
        const stored = Number.parseInt(window.localStorage.getItem(getChatRetentionStorageKey(userId)), 10);
        return chatRetentionAllowedDays.includes(stored) ? stored : chatRetentionDefaultDays;
    }

    function setChatRetentionDays(days, userId = getCurrentUserId()) {
        const value = chatRetentionAllowedDays.includes(days) ? days : chatRetentionDefaultDays;
        window.localStorage.setItem(getChatRetentionStorageKey(userId), value.toString());
        return value;
    }

    function isCurrentUserMessageKey(key, userId = getCurrentUserId()) {
        return key.startsWith("elovo:messages:") && key.slice("elovo:messages:".length).split(":").includes(userId);
    }

    function readStoredChatMessages(key) {
        try {
            const messages = JSON.parse(window.localStorage.getItem(key) || "[]");
            return Array.isArray(messages) ? messages : [];
        } catch {
            return [];
        }
    }

    function getMessageTime(message) {
        const time = new Date(message && message.sentAt ? message.sentAt : "").getTime();
        return Number.isNaN(time) ? null : time;
    }

    function getLocalStorageKeys() {
        const keys = [];
        for (let index = 0; index < window.localStorage.length; index += 1) {
            keys.push(window.localStorage.key(index));
        }
        return keys.filter(Boolean);
    }

    function deleteCachedMessageImages(messageIds, userId = getCurrentUserId()) {
        if (!window.indexedDB || !messageIds.length) {
            return Promise.resolve();
        }

        return new Promise((resolve) => {
            const request = window.indexedDB.open(imageCacheDbName, 1);
            request.addEventListener("upgradeneeded", () => {
                const db = request.result;
                if (!db.objectStoreNames.contains(imageCacheStoreName)) {
                    db.createObjectStore(imageCacheStoreName);
                }
            });
            request.addEventListener("success", () => {
                const db = request.result;
                if (!db.objectStoreNames.contains(imageCacheStoreName)) {
                    db.close();
                    resolve();
                    return;
                }

                const transaction = db.transaction(imageCacheStoreName, "readwrite");
                const store = transaction.objectStore(imageCacheStoreName);
                messageIds.forEach((id) => store.delete(`${userId}:${id}`));
                transaction.addEventListener("complete", () => {
                    db.close();
                    resolve();
                });
                transaction.addEventListener("error", () => {
                    db.close();
                    resolve();
                });
            });
            request.addEventListener("error", () => resolve());
        });
    }

    async function purgeExpiredChatMessages(days = getChatRetentionDays()) {
        const userId = getCurrentUserId();
        if (!userId) {
            return { removed: 0 };
        }

        const cutoff = Date.now() - (days * 24 * 60 * 60 * 1000);
        const removedImageIds = [];
        let removed = 0;

        getLocalStorageKeys()
            .filter((key) => isCurrentUserMessageKey(key, userId))
            .forEach((key) => {
                const messages = readStoredChatMessages(key);
                const keptMessages = messages.filter((message) => {
                    const time = getMessageTime(message);
                    const keep = time === null || time >= cutoff;
                    if (!keep) {
                        removed += 1;
                        if (message.isImage && message.id) {
                            removedImageIds.push(message.id);
                        }
                    }
                    return keep;
                });

                if (keptMessages.length === messages.length) {
                    return;
                }

                if (keptMessages.length) {
                    window.localStorage.setItem(key, JSON.stringify(keptMessages));
                } else {
                    window.localStorage.removeItem(key);
                }
            });

        await deleteCachedMessageImages(removedImageIds, userId);
        return { removed };
    }

    window.Elovo = {
        showPageLoader,
        hidePageLoader,
        navigateWithLoader,
        getAntiForgeryToken,
        setAvatarElement,
        preloadUserAvatars,
        getOrFetchAvatar,
        readResponseText,
        syncModalScrollLock,
        openModal,
        closeModal,
        chatRetentionAllowedDays,
        chatRetentionDefaultDays,
        getChatRetentionDays,
        setChatRetentionDays,
        purgeExpiredChatMessages,
        t
    };

    document.querySelectorAll("[data-close-modal]").forEach((button) => {
        button.addEventListener("click", () => {
            closeModal(button.closest(".modal-backdrop"));
        });
    });

    document.querySelectorAll(".modal-backdrop").forEach((modal) => {
        modal.addEventListener("click", (event) => {
            if (event.target !== modal) {
                return;
            }

            if (modal.id === "avatarCropModal" && window.ElovoProfilePage) {
                window.ElovoProfilePage.closeAvatarCrop();
                return;
            }

            closeModal(modal);
        });
    });

    document.querySelectorAll("a[href]").forEach((link) => {
        link.addEventListener("click", (event) => {
            const url = new URL(link.href, window.location.href);
            const isSamePageHash = url.origin === window.location.origin &&
                url.pathname === window.location.pathname &&
                url.search === window.location.search &&
                url.hash;
            const closesEmbeddedSettings = window.parent !== window &&
                window.location.pathname.startsWith("/settings/") &&
                url.origin === window.location.origin &&
                url.pathname === "/chat";

            if (event.defaultPrevented ||
                event.button !== 0 ||
                event.metaKey ||
                event.ctrlKey ||
                event.shiftKey ||
                event.altKey ||
                link.target ||
                link.hasAttribute("download") ||
                url.origin !== window.location.origin ||
                isSamePageHash) {
                return;
            }

            if (closesEmbeddedSettings) {
                event.preventDefault();
                window.parent.postMessage({ type: "elovo:close-settings" }, window.location.origin);
                return;
            }

            showPageLoader();
        });
    });

    if (window.matchMedia("(max-width: 820px), (pointer: coarse)").matches) {
        ["copy", "cut", "contextmenu", "selectstart"].forEach((eventName) => {
            document.addEventListener(eventName, (event) => {
                if (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement) {
                    return;
                }

                event.preventDefault();
            }, { capture: true });
        });
    }

    window.addEventListener("pageshow", hidePageLoader);
})();
