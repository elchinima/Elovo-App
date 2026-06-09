(() => {
    const { showPageLoader, hidePageLoader } = window.Elovo;
    const twoFactorForm = document.querySelector("#twoFactorForm");
    const twoFactorEmailTimer = document.querySelector("#twoFactorEmailTimer");

    if (!twoFactorForm) {
        return;
    }

    window.localStorage.removeItem("elovoCurrentUser");

    function formatRemaining(milliseconds) {
        const totalSeconds = Math.max(0, Math.ceil(milliseconds / 1000));
        const minutes = Math.floor(totalSeconds / 60).toString().padStart(2, "0");
        const seconds = (totalSeconds % 60).toString().padStart(2, "0");
        return `${minutes}:${seconds}`;
    }

    function updateEmailTimer() {
        if (!twoFactorEmailTimer) {
            return;
        }

        const endsAt = new Date(twoFactorEmailTimer.dataset.cooldownEndsAt || "").getTime();
        if (Number.isNaN(endsAt)) {
            twoFactorEmailTimer.textContent = "";
            return;
        }

        const remaining = endsAt - Date.now();
        if (remaining <= 0) {
            twoFactorEmailTimer.textContent = "";
            return;
        }

        twoFactorEmailTimer.textContent = window.Elovo.t("Next email available in {time}", {
            time: formatRemaining(remaining)
        });
    }

    updateEmailTimer();
    window.setInterval(updateEmailTimer, 1000);

    twoFactorForm.addEventListener("submit", () => {
        showPageLoader();
        twoFactorForm.querySelectorAll("button").forEach((button) => {
            button.disabled = true;
        });
        window.setTimeout(() => {
            hidePageLoader();
            twoFactorForm.querySelectorAll("button").forEach((button) => {
                button.disabled = false;
            });
        }, 20000);
    });
})();
