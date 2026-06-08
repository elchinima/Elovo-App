(() => {
    const { showPageLoader, hidePageLoader } = window.Elovo;
    const loginForm = document.querySelector("#loginForm");
    const preferredLanguageInput = document.querySelector("#loginPreferredLanguage");

    if (!loginForm) {
        return;
    }

    window.localStorage.removeItem("elovoCurrentUser");

    loginForm.addEventListener("submit", () => {
        if (preferredLanguageInput && window.ElovoI18n) {
            preferredLanguageInput.value = window.ElovoI18n.getLanguage();
        }

        showPageLoader();
        loginForm.querySelectorAll("button").forEach((button) => {
            button.disabled = true;
        });
        window.setTimeout(() => {
            hidePageLoader();
            loginForm.querySelectorAll("button").forEach((button) => {
                button.disabled = false;
            });
        }, 20000);
    });
})();
