(() => {
    const { showPageLoader, hidePageLoader, t } = window.Elovo;
    const loginForm = document.querySelector("#loginForm");
    const preferredLanguageInput = document.querySelector("#loginPreferredLanguage");
    const loginError = document.querySelector("#loginError");

    if (!loginForm) {
        return;
    }

    window.localStorage.removeItem("elovoCurrentUser");

    if (loginError) {
        const cooldownMatch = loginError.textContent.trim().match(/^You can request another email in ([0-9]{2,}:[0-9]{2})\.$/);
        if (cooldownMatch) {
            loginError.textContent = t("Next email available in {time}", { time: cooldownMatch[1] });
        }
    }

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
