(() => {
    const pageLoader = document.querySelector("#pageLoader");
    const keepAliveIntervalMs = 14 * 60 * 1000;

    function keepAlive() {
        fetch("/health", { cache: "no-store" }).catch(() => { });
    }

    window.setInterval(keepAlive, keepAliveIntervalMs);

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

    window.Elovo = {
        showPageLoader,
        hidePageLoader,
        navigateWithLoader,
        getAntiForgeryToken,
        setAvatarElement,
        readResponseText,
        openModal,
        closeModal
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
