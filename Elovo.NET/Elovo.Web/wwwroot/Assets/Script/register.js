(() => {
    const { showPageLoader } = window.Elovo;
    const registerForm = document.querySelector("#registerForm");

    if (!registerForm) {
        return;
    }

    window.localStorage.removeItem("elovoCurrentUser");

    registerForm.addEventListener("submit", () => {
        showPageLoader();
        registerForm.querySelectorAll("button").forEach((button) => {
            button.disabled = true;
        });
    });
})();
