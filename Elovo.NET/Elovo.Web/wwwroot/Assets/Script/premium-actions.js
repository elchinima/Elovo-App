(() => {
    const { getAntiForgeryToken, readResponseText, t } = window.Elovo;
    const extendedVoiceToggle = document.querySelector("#extendedVoiceMessagesToggle");
    const extendedVoiceStatus = document.querySelector("#extendedVoiceMessagesStatus");
    const premiumBadgeToggle = document.querySelector("#premiumBadgeToggle");
    const premiumBadgeStatus = document.querySelector("#premiumBadgeStatus");
    const rawImageUploadsToggle = document.querySelector("#rawImageUploadsToggle");
    const rawImageUploadsStatus = document.querySelector("#rawImageUploadsStatus");

    function setStatus(element, message, type = "") {
        const status = element;
        if (!status) {
            return;
        }

        status.textContent = message || "";
        status.classList.toggle("is-error", type === "error");
        status.classList.toggle("is-success", type === "success");
    }

    async function savePremiumSetting(options) {
        const { toggle, status, endpoint, enabled, profileKey, successEnabled, successDisabled } = options;
        if (!toggle) {
            return;
        }

        toggle.disabled = true;
        setStatus(status, t("Saving setting..."));

        const response = await fetch(endpoint, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": getAntiForgeryToken()
            },
            body: JSON.stringify({ enabled })
        });

        if (response.ok) {
            const profile = await response.json();
            window.elovoPremiumActions = window.elovoPremiumActions || {};
            window.elovoPremiumActions[profileKey] = Boolean(profile[profileKey]);
            toggle.checked = window.elovoPremiumActions[profileKey];
            toggle.disabled = false;
            setStatus(status, t(enabled ? successEnabled : successDisabled), "success");
            return;
        }

        toggle.checked = !enabled;
        toggle.disabled = false;
        setStatus(status, await readResponseText(response), "error");
    }

    if (extendedVoiceToggle) {
        extendedVoiceToggle.addEventListener("change", () => savePremiumSetting({
            toggle: extendedVoiceToggle,
            status: extendedVoiceStatus,
            endpoint: "/api/profile/extended-voice-messages",
            enabled: extendedVoiceToggle.checked,
            profileKey: "isExtendedVoiceMessagesEnabled",
            successEnabled: "3-minute voice messages enabled.",
            successDisabled: "3-minute voice messages disabled."
        }));
    }

    if (premiumBadgeToggle) {
        premiumBadgeToggle.addEventListener("change", () => savePremiumSetting({
            toggle: premiumBadgeToggle,
            status: premiumBadgeStatus,
            endpoint: "/api/profile/premium-badge",
            enabled: premiumBadgeToggle.checked,
            profileKey: "isPremiumBadgeVisible",
            successEnabled: "Premium badge enabled.",
            successDisabled: "Premium badge hidden."
        }));
    }

    if (rawImageUploadsToggle) {
        rawImageUploadsToggle.addEventListener("change", () => savePremiumSetting({
            toggle: rawImageUploadsToggle,
            status: rawImageUploadsStatus,
            endpoint: "/api/profile/raw-image-uploads",
            enabled: rawImageUploadsToggle.checked,
            profileKey: "isRawImageUploadsEnabled",
            successEnabled: "Raw image uploads enabled.",
            successDisabled: "Raw image uploads disabled."
        }));
    }
})();
