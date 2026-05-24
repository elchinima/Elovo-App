(() => {
    const { showPageLoader } = window.Elovo;
    const twoFactorForm = document.querySelector("#twoFactorForm");

    if (!twoFactorForm) {
        return;
    }

    window.localStorage.removeItem("elovoCurrentUser");

    twoFactorForm.addEventListener("submit", () => {
        showPageLoader();
        twoFactorForm.querySelectorAll("button").forEach((button) => {
            button.disabled = true;
        });
    });
})();
