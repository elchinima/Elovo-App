(() => {
    const storageKey = "elovo:language";
    const supportedLanguages = ["en", "ru", "az"];
    const languageOptions = [
        { code: "en", flag: "/Assets/Images/Flags/en.svg", label: "English" },
        { code: "ru", flag: "/Assets/Images/Flags/ru.svg", label: "Русский" },
        { code: "az", flag: "/Assets/Images/Flags/az.svg", label: "Azərbaycan dili" }
    ];
    const dictionaries = window.ElovoTranslations || {};

    function normalizeLanguage(value) {
        const language = String(value || "").toLowerCase().split("-")[0];
        return supportedLanguages.includes(language) ? language : "en";
    }

    function getSystemLanguage() {
        return normalizeLanguage(navigator.language);
    }

    function getProfileLanguage() {
        return document.querySelector('meta[name="elovo-preferred-language"]')?.content;
    }

    function getLanguage() {
        const stored = window.localStorage.getItem(storageKey);
        return stored ? normalizeLanguage(stored) : normalizeLanguage(getProfileLanguage() || getSystemLanguage());
    }

    function interpolate(value, params) {
        return Object.entries(params || {}).reduce(
            (result, [key, replacement]) => result.replaceAll(`{${key}}`, replacement),
            value
        );
    }

    function t(source, params = {}) {
        const dictionary = dictionaries[getLanguage()] || {};
        const key = String(source).trim().replace(/\s+/g, " ");
        return interpolate(dictionary[key] || source, params);
    }

    function translateElement(element) {
        ["aria-label", "title", "placeholder"].forEach((attribute) => {
            const value = element.getAttribute(attribute);
            if (value) {
                element.setAttribute(attribute, t(value));
            }
        });
    }

    function translatePage(root = document) {
        document.documentElement.lang = getLanguage();
        document.title = t(document.title);
        root.querySelectorAll("*").forEach((element) => {
            if (["SCRIPT", "STYLE", "TEMPLATE"].includes(element.tagName)) {
                return;
            }

            translateElement(element);
            element.childNodes.forEach((node) => {
                if (node.nodeType !== Node.TEXT_NODE || !node.nodeValue.trim()) {
                    return;
                }

                const leading = node.nodeValue.match(/^\s*/)?.[0] || "";
                const trailing = node.nodeValue.match(/\s*$/)?.[0] || "";
                node.nodeValue = `${leading}${t(node.nodeValue.trim())}${trailing}`;
            });
        });
    }

    function setLanguage(language) {
        const normalizedLanguage = normalizeLanguage(language);
        window.localStorage.setItem(storageKey, normalizedLanguage);
        fetch("/api/account/language", {
            method: "PATCH",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ language: normalizedLanguage }),
            keepalive: true
        }).catch(() => { });
        window.location.reload();
    }

    function syncSystemLanguage() {
        if (window.localStorage.getItem(storageKey) || getProfileLanguage()) {
            return;
        }

        fetch("/api/account/language", {
            method: "PATCH",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ language: getLanguage() }),
            keepalive: true
        }).catch(() => { });
    }

    function createLanguageSelector() {
        if (!document.querySelector(".login-view")) {
            return;
        }

        const selector = document.createElement("div");
        const current = languageOptions.find((item) => item.code === getLanguage()) || languageOptions[0];
        const toggle = document.createElement("button");
        const menu = document.createElement("div");
        const createFlag = (item) => {
            const image = document.createElement("img");
            image.src = item.flag;
            image.alt = "";
            image.setAttribute("aria-hidden", "true");
            return image;
        };

        selector.className = "language-selector";
        toggle.type = "button";
        toggle.className = "language-selector-toggle";
        toggle.setAttribute("aria-label", t("Choose language"));
        toggle.setAttribute("title", t("Choose language"));
        toggle.setAttribute("aria-expanded", "false");
        toggle.appendChild(createFlag(current));
        menu.className = "language-selector-menu";
        menu.hidden = true;

        languageOptions
            .filter((item) => item.code !== current.code)
            .forEach((item) => {
                const button = document.createElement("button");
                button.type = "button";
                button.className = "language-selector-option";
                button.setAttribute("aria-label", item.label);
                button.setAttribute("title", item.label);
                button.appendChild(createFlag(item));
                button.addEventListener("click", () => setLanguage(item.code));
                menu.appendChild(button);
            });

        toggle.addEventListener("click", () => {
            menu.hidden = !menu.hidden;
            toggle.setAttribute("aria-expanded", menu.hidden ? "false" : "true");
        });
        document.addEventListener("click", (event) => {
            if (!selector.contains(event.target)) {
                menu.hidden = true;
                toggle.setAttribute("aria-expanded", "false");
            }
        });

        selector.append(toggle, menu);
        document.body.appendChild(selector);
    }

    syncSystemLanguage();
    translatePage();
    createLanguageSelector();

    window.ElovoI18n = {
        getLanguage,
        setLanguage,
        t,
        translatePage
    };
})();
