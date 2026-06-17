(() => {
    const { showPageLoader } = window.Elovo;
    const registerForm = document.querySelector("#registerForm");
    const preferredLanguageInput = document.querySelector("#registerPreferredLanguage");

    if (!registerForm) {
        return;
    }

    window.localStorage.removeItem("elovoCurrentUser");

    registerForm.addEventListener("submit", () => {
        if (preferredLanguageInput && window.ElovoI18n) {
            preferredLanguageInput.value = window.ElovoI18n.getLanguage();
        }

        showPageLoader();
        registerForm.querySelectorAll("button").forEach((button) => {
            button.disabled = true;
        });
    });
})();
