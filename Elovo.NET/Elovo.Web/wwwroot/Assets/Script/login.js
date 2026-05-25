(() => {
    const { showPageLoader, hidePageLoader } = window.Elovo;
    const loginForm = document.querySelector("#loginForm");

    if (!loginForm) {
        return;
    }

    window.localStorage.removeItem("elovoCurrentUser");

    loginForm.addEventListener("submit", () => {
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
