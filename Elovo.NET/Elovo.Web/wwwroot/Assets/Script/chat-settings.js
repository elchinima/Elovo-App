(() => {
    const {
        openModal,
        closeModal,
        chatRetentionAllowedDays,
        getChatRetentionDays,
        setChatRetentionDays,
        purgeExpiredChatMessages,
        t
    } = window.Elovo;

    const options = document.querySelector("#chatRetentionOptions");
    const status = document.querySelector("#chatRetentionStatus");
    const confirmModal = document.querySelector("#chatRetentionConfirmModal");
    const confirmMessage = document.querySelector("#chatRetentionConfirmMessage");
    const confirmAccept = document.querySelector("#chatRetentionConfirmAccept");
    const confirmCancel = document.querySelector("#chatRetentionConfirmCancel");
    const confirmClose = document.querySelector("#chatRetentionConfirmClose");
    const languageOptions = document.querySelector("#settingsLanguageOptions");
    const themeOptions = document.querySelector("#settingsThemeOptions");
    const languages = [
        { code: "system", flag: "/Assets/Images/Icons/settings.svg", label: "System language" },
        { code: "en", flag: "/Assets/Images/Flags/en.svg", label: "English" },
        { code: "ru", flag: "/Assets/Images/Flags/ru.svg", label: "Русский" },
        { code: "az", flag: "/Assets/Images/Flags/az.svg", label: "Azərbaycan dili" }
    ];
    const themes = [
        { code: "dark", label: "Dark" },
        { code: "default", label: "Ordinary" },
        { code: "light", label: "Light" }
    ];
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
            label.textContent = t("{days} days", { days });
            detail.textContent = days === 90 ? t("Default") : t("Auto delete");
            button.append(icon, label, detail);
            button.addEventListener("click", () => requestRetentionChange(days));
            options.appendChild(button);
        });
    }

    function renderLanguageOptions() {
        if (!languageOptions || !window.ElovoI18n) {
            return;
        }

        const selectedLanguage = window.ElovoI18n.getLanguagePreference();
        languageOptions.innerHTML = "";
        languages.forEach((language) => {
            const button = document.createElement("button");
            const flag = document.createElement("img");
            const copy = document.createElement("span");
            const label = document.createElement("strong");
            const detail = document.createElement("small");
            const isActive = language.code === selectedLanguage;

            button.type = "button";
            button.className = `settings-language-option${isActive ? " active" : ""}`;
            button.setAttribute("role", "radio");
            button.setAttribute("aria-checked", isActive ? "true" : "false");
            button.setAttribute("aria-label", t(language.label));
            flag.src = language.flag;
            flag.alt = "";
            label.textContent = t(language.label);
            detail.textContent = isActive ? t("Selected") : t("Choose");
            copy.append(label, detail);
            button.append(flag, copy);
            button.addEventListener("click", () => {
                if (button.classList.contains("active")) {
                    return;
                }

                window.ElovoI18n.setLanguage(language.code);
            });
            languageOptions.appendChild(button);
        });
    }

    function renderThemeOptions() {
        if (!themeOptions || !window.ElovoTheme) {
            return;
        }

        const selectedTheme = window.ElovoTheme.getTheme();
        themeOptions.innerHTML = "";
        themes.forEach((theme) => {
            const button = document.createElement("button");
            const preview = document.createElement("span");
            const copy = document.createElement("span");
            const label = document.createElement("strong");
            const detail = document.createElement("small");
            const isActive = theme.code === selectedTheme;

            button.type = "button";
            button.className = `settings-theme-option${isActive ? " active" : ""}`;
            button.setAttribute("role", "radio");
            button.setAttribute("aria-checked", isActive ? "true" : "false");
            button.setAttribute("aria-label", t(theme.label));
            preview.className = `settings-theme-preview settings-theme-preview-${theme.code}`;
            preview.setAttribute("aria-hidden", "true");
            preview.append(document.createElement("i"), document.createElement("i"), document.createElement("i"));
            label.textContent = t(theme.label);
            detail.textContent = isActive ? t("Selected") : t("Choose");
            copy.append(label, detail);
            button.append(preview, copy);
            button.addEventListener("click", () => {
                if (button.classList.contains("active")) {
                    return;
                }

                window.ElovoTheme.setTheme(theme.code);
                renderThemeOptions();
            });
            themeOptions.appendChild(button);
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
            confirmMessage.textContent = t("Local messages older than {days} days will be deleted from this browser or mobile app.", { days });
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
        setStatus(t("Cleaning old local messages..."));

        try {
            const result = await purgeExpiredChatMessages(days);
            setStatus(t("Auto delete is set to {days} days. {count} old local messages removed.", { days, count: result.removed }), "success");
        } catch {
            setStatus(t("Auto delete period was saved, but cleanup could not finish now."), "error");
        }
    }

    renderThemeOptions();
    renderLanguageOptions();
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
