(() => {
    const { showPageLoader } = window.Elovo;
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
    });
})();
