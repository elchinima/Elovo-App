(() => {
    const storageKey = "elovo:theme";
    const supportedThemes = ["dark", "default", "light"];

    function normalizeTheme(value) {
        return supportedThemes.includes(value) ? value : "default";
    }

    function getTheme() {
        return normalizeTheme(window.localStorage.getItem(storageKey));
    }

    function applyTheme(theme) {
        const normalizedTheme = normalizeTheme(theme);
        document.documentElement.dataset.theme = normalizedTheme;
        return normalizedTheme;
    }

    function setTheme(theme) {
        const normalizedTheme = applyTheme(theme);
        if (normalizedTheme === "default") {
            window.localStorage.removeItem(storageKey);
        } else {
            window.localStorage.setItem(storageKey, normalizedTheme);
        }
        return normalizedTheme;
    }

    applyTheme(getTheme());

    window.ElovoTheme = {
        getTheme,
        setTheme
    };
})();
