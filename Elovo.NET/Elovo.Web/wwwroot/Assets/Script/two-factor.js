(() => {
    const { showPageLoader, hidePageLoader } = window.Elovo;
    const twoFactorForm = document.querySelector("#twoFactorForm");
    const twoFactorEmailTimer = document.querySelector("#twoFactorEmailTimer");
    const twoFactorCode = document.querySelector("#twoFactorCode");
    const twoFactorCodeInput = document.querySelector("#twoFactorCodeInput");
    const codeCells = Array.from(document.querySelectorAll("[data-code-cell]"));

    if (!twoFactorForm) {
        return;
    }

    window.localStorage.removeItem("elovoCurrentUser");

    function syncCodeCells() {
        if (!twoFactorCode || codeCells.length === 0) {
            return;
        }

        const value = twoFactorCode.value.replace(/\D/g, "").slice(0, 7);
        if (twoFactorCode.value !== value) {
            twoFactorCode.value = value;
        }

        codeCells.forEach((cell, index) => {
            cell.textContent = value[index] || "";
            cell.classList.toggle("is-filled", index < value.length);
            cell.classList.toggle("is-active", index === Math.min(value.length, codeCells.length - 1));
        });
    }

    if (twoFactorCode) {
        syncCodeCells();
        twoFactorCode.addEventListener("input", syncCodeCells);
        twoFactorCode.addEventListener("focus", () => {
            twoFactorCodeInput?.classList.add("is-focused");
            syncCodeCells();
        });
        twoFactorCode.addEventListener("blur", () => {
            twoFactorCodeInput?.classList.remove("is-focused");
            syncCodeCells();
        });
    }

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
        syncCodeCells();
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
