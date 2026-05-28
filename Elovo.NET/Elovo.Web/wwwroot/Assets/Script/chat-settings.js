(() => {
    const {
        openModal,
        closeModal,
        chatRetentionAllowedDays,
        getChatRetentionDays,
        setChatRetentionDays,
        purgeExpiredChatMessages
    } = window.Elovo;

    const options = document.querySelector("#chatRetentionOptions");
    const status = document.querySelector("#chatRetentionStatus");
    const confirmModal = document.querySelector("#chatRetentionConfirmModal");
    const confirmMessage = document.querySelector("#chatRetentionConfirmMessage");
    const confirmAccept = document.querySelector("#chatRetentionConfirmAccept");
    const confirmCancel = document.querySelector("#chatRetentionConfirmCancel");
    const confirmClose = document.querySelector("#chatRetentionConfirmClose");
    let pendingDays = null;

    function setStatus(message, kind = "") {
        if (!status) {
            return;
        }

        status.textContent = message;
        status.classList.toggle("is-error", kind === "error");
        status.classList.toggle("is-success", kind === "success");
    }

    function renderOptions(selectedDays = getChatRetentionDays()) {
        if (!options) {
            return;
        }

        options.innerHTML = "";
        chatRetentionAllowedDays.forEach((days) => {
            const button = document.createElement("button");
            const icon = document.createElement("img");
            const label = document.createElement("span");
            const detail = document.createElement("small");

            button.type = "button";
            button.className = `chat-retention-option${days === selectedDays ? " is-active" : ""}`;
            button.setAttribute("role", "radio");
            button.setAttribute("aria-checked", days === selectedDays ? "true" : "false");
            icon.src = "/Assets/Images/Icons/chat-retention.svg";
            icon.alt = "";
            label.textContent = `${days} days`;
            detail.textContent = days === 90 ? "Default" : "Auto delete";
            button.append(icon, label, detail);
            button.addEventListener("click", () => requestRetentionChange(days));
            options.appendChild(button);
        });
    }

    function requestRetentionChange(days) {
        const currentDays = getChatRetentionDays();
        if (days === currentDays) {
            return;
        }

        const retentionKey = `elovo:chat-retention-days:${(window.elovoCurrentUserId || "").toLowerCase()}`;
        if (days === 90 && !window.localStorage.getItem(retentionKey)) {
            applyRetentionChange(days);
            return;
        }

        pendingDays = days;
        if (confirmMessage) {
            confirmMessage.textContent = `Local messages older than ${days} days will be deleted from this browser or mobile app.`;
        }
        openModal(confirmModal);
    }

    function closeConfirm() {
        pendingDays = null;
        closeModal(confirmModal);
    }

    async function acceptRetentionChange() {
        if (!pendingDays) {
            return;
        }

        const days = pendingDays;
        pendingDays = null;
        closeModal(confirmModal);
        await applyRetentionChange(days);
    }

    async function applyRetentionChange(days) {
        setChatRetentionDays(days);
        renderOptions(days);
        setStatus("Cleaning old local messages...");

        try {
            const result = await purgeExpiredChatMessages(days);
            setStatus(`Auto delete is set to ${days} days. ${result.removed} old local message${result.removed === 1 ? "" : "s"} removed.`, "success");
        } catch {
            setStatus("Auto delete period was saved, but cleanup could not finish now.", "error");
        }
    }

    renderOptions();
    purgeExpiredChatMessages().catch(() => { });

    if (confirmAccept) {
        confirmAccept.addEventListener("click", acceptRetentionChange);
    }

    if (confirmCancel) {
        confirmCancel.addEventListener("click", closeConfirm);
    }

    if (confirmClose) {
        confirmClose.addEventListener("click", closeConfirm);
    }

    if (confirmModal) {
        confirmModal.addEventListener("click", (event) => {
            if (event.target === confirmModal) {
                closeConfirm();
            }
        });
    }
})();
