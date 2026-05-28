(() => {
    const pageLoader = document.querySelector("#pageLoader");
    const keepAliveIntervalMs = 10 * 60 * 1000;

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

    function setAvatarElement(element, item, initialFallback = "?") {
        if (!element) {
            return;
        }

        element.innerHTML = "";
        const imageUrl = item && item.profileImageUrl ? item.profileImageUrl : "";
        if (imageUrl) {
            const image = document.createElement("img");
            image.src = imageUrl;
            image.alt = "";
            element.appendChild(image);
            return;
        }

        element.textContent = item && item.initial ? item.initial : initialFallback;
    }

    async function readResponseText(response) {
        const text = await response.text();
        return text || "Request failed.";
    }

    function openModal(modal) {
        if (!modal) {
            return;
        }

        modal.classList.add("is-open");
        modal.setAttribute("aria-hidden", "false");
    }

    function closeModal(modal) {
        if (!modal) {
            return;
        }

        modal.classList.remove("is-open");
        modal.setAttribute("aria-hidden", "true");
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
        readResponseText,
        openModal,
        closeModal,
        chatRetentionAllowedDays,
        chatRetentionDefaultDays,
        getChatRetentionDays,
        setChatRetentionDays,
        purgeExpiredChatMessages
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
