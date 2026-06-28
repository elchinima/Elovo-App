(() => {
    const { getAntiForgeryToken, readResponseText, t, setAvatarElement, openModal, closeModal } = window.Elovo;
    
    // Toggles
    const extendedVoiceToggle = document.querySelector("#extendedVoiceMessagesToggle");
    const extendedVoiceStatus = document.querySelector("#extendedVoiceMessagesStatus");
    const premiumBadgeToggle = document.querySelector("#premiumBadgeToggle");
    const premiumBadgeStatus = document.querySelector("#premiumBadgeStatus");
    const rawImageUploadsToggle = document.querySelector("#rawImageUploadsToggle");
    const rawImageUploadsStatus = document.querySelector("#rawImageUploadsStatus");
    const videoUploadsToggle = document.querySelector("#videoUploadsToggle");
    const videoUploadsStatus = document.querySelector("#videoUploadsStatus");

    // Premium Avatar Elements
    const premiumProfileAvatar = document.querySelector("#premiumProfileAvatar");
    const premiumProfileImageInput = document.querySelector("#premiumProfileImageInput");
    const premiumProfileImageButton = document.querySelector("#premiumProfileImageButton");
    const deletePremiumProfileImageButton = document.querySelector("#deletePremiumProfileImageButton");
    const premiumProfileImageStatus = document.querySelector("#premiumProfileImageStatus");

    // Cropper Elements
    const avatarCropModal = document.querySelector("#avatarCropModal");
    const avatarCropStage = document.querySelector("#avatarCropStage");
    const avatarCropImage = document.querySelector("#avatarCropImage");
    const avatarZoom = document.querySelector("#avatarZoom");
    const avatarCropSave = document.querySelector("#avatarCropSave");
    const avatarCropCancel = document.querySelector("#avatarCropCancel");

    // Confirm Modal Elements
    const profileConfirmModal = document.querySelector("#profileConfirmModal");
    const profileConfirmTitle = document.querySelector("#profileConfirmTitle");
    const profileConfirmMessage = document.querySelector("#profileConfirmMessage");
    const profileConfirmAccept = document.querySelector("#profileConfirmAccept");
    const profileConfirmCancel = document.querySelector("#profileConfirmCancel");
    const profileConfirmClose = document.querySelector("#profileConfirmClose");
    const profileConfirmIcon = document.querySelector("#profileConfirmIcon");

    const profileConfirmIcons = {
        delete: "/Assets/Images/Icons/confirm-delete.svg",
        email: "/Assets/Images/Icons/confirm-email.svg",
        password: "/Assets/Images/Icons/confirm-password.svg",
        security: "/Assets/Images/Icons/confirm-security.svg",
        logout: "/Assets/Images/Icons/logout.svg",
        default: "/Assets/Images/Icons/confirm-profile.svg"
    };

    const maxImageSize = 10 * 1024 * 1024;
    const allowedProfileImageTypes = ["image/png", "image/jpeg", "image/jpg"];
    let avatarCropState = null;
    let profileConfirmResolve = null;

    function setStatus(element, message, type = "") {
        const status = element;
        if (!status) {
            return;
        }

        status.textContent = message || "";
        status.classList.toggle("is-error", type === "error");
        status.classList.toggle("is-success", type === "success");
    }

    function setProfileStatus(element, message, kind = "") {
        setStatus(element, message, kind);
    }

    function closeProfileConfirm(result) {
        closeModal(profileConfirmModal);
        if (profileConfirmResolve) {
            profileConfirmResolve(result);
            profileConfirmResolve = null;
        }
    }

    if (profileConfirmAccept) {
        profileConfirmAccept.addEventListener("click", () => closeProfileConfirm(true));
    }
    if (profileConfirmCancel) {
        profileConfirmCancel.addEventListener("click", () => closeProfileConfirm(false));
    }
    if (profileConfirmClose) {
        profileConfirmClose.addEventListener("click", () => closeProfileConfirm(false));
    }
    if (profileConfirmModal) {
        profileConfirmModal.addEventListener("click", (event) => {
            if (event.target === profileConfirmModal) {
                closeProfileConfirm(false);
            }
        });
    }

    function confirmProfileAction(title, message, confirmText = t("Confirm"), icon = "default") {
        if (!profileConfirmModal || !profileConfirmTitle || !profileConfirmMessage || !profileConfirmAccept) {
            return Promise.resolve(true);
        }

        if (profileConfirmResolve) {
            closeProfileConfirm(false);
        }

        profileConfirmTitle.textContent = title;
        profileConfirmMessage.textContent = message;
        if (profileConfirmIcon) {
            profileConfirmIcon.src = profileConfirmIcons[icon] || profileConfirmIcons.default;
        }
        const text = profileConfirmAccept.querySelector("span");
        if (text) {
            text.textContent = confirmText;
        }
        openModal(profileConfirmModal);

        return new Promise((resolve) => {
            profileConfirmResolve = resolve;
        });
    }

    function renderProfile(profile) {
        if (!profile) {
            return;
        }
        window.elovoProfile = window.elovoProfile || {};
        window.elovoProfile.profileImageUrl = profile.profileImageUrl;
        setAvatarElement(premiumProfileAvatar, profile, profile.initial || "?");
        if (deletePremiumProfileImageButton) {
            deletePremiumProfileImageButton.disabled = !profile.profileImageUrl;
        }

        if (window.parent !== window && window.location.pathname.startsWith("/settings/")) {
            window.parent.postMessage({ type: "elovo:profile-updated", profile }, window.location.origin);
        }
    }

    // Modal helpers
    function clamp(val, min, max) {
        return Math.min(Math.max(val, min), max);
    }

    function closeAvatarCrop() {
        closeModal(avatarCropModal);
        if (avatarCropState && avatarCropState.url) {
            URL.revokeObjectURL(avatarCropState.url);
        }
        avatarCropState = null;
        if (avatarCropImage) {
            avatarCropImage.removeAttribute("src");
        }
    }

    function renderAvatarCrop() {
        if (!avatarCropState || !avatarCropStage || !avatarCropImage) {
            return;
        }

        const rect = avatarCropStage.getBoundingClientRect();
        const stageSize = rect.width;
        const scale = avatarCropState.baseScale * avatarCropState.zoom;
        const width = avatarCropState.width * scale;
        const height = avatarCropState.height * scale;

        const maxX = Math.max(0, (width - stageSize) / 2);
        const maxY = Math.max(0, (height - stageSize) / 2);
        avatarCropState.offsetX = clamp(avatarCropState.offsetX, -maxX, maxX);
        avatarCropState.offsetY = clamp(avatarCropState.offsetY, -maxY, maxY);

        avatarCropImage.style.width = `${width}px`;
        avatarCropImage.style.height = `${height}px`;
        avatarCropImage.style.left = `calc(50% + ${avatarCropState.offsetX}px)`;
        avatarCropImage.style.top = `calc(50% + ${avatarCropState.offsetY}px)`;
        avatarCropImage.style.transform = "translate(-50%, -50%)";
    }

    function openAvatarCrop(file) {
        if (!avatarCropModal || !avatarCropStage || !avatarCropImage || !avatarZoom) {
            return;
        }

        const url = URL.createObjectURL(file);
        avatarCropImage.onload = () => {
            const rect = avatarCropStage.getBoundingClientRect();
            const stageSize = rect.width;
            avatarCropState = {
                url,
                zoom: 1,
                offsetX: 0,
                offsetY: 0,
                width: avatarCropImage.naturalWidth,
                height: avatarCropImage.naturalHeight,
                baseScale: Math.max(stageSize / avatarCropImage.naturalWidth, stageSize / avatarCropImage.naturalHeight),
                dragging: false,
                dragX: 0,
                dragY: 0
            };

            avatarZoom.value = "1";
            renderAvatarCrop();
            openModal(avatarCropModal);
        };
        avatarCropImage.src = url;
    }

    function getCroppedAvatarBlob() {
        return new Promise((resolve, reject) => {
            if (!avatarCropState || !avatarCropStage || !avatarCropImage) {
                reject(new Error(t("Image is not ready.")));
                return;
            }

            const rect = avatarCropStage.getBoundingClientRect();
            const stageSize = rect.width;
            const scale = avatarCropState.baseScale * avatarCropState.zoom;
            const width = avatarCropState.width * scale;
            const height = avatarCropState.height * scale;
            const left = stageSize / 2 + avatarCropState.offsetX - width / 2;
            const top = stageSize / 2 + avatarCropState.offsetY - height / 2;
            const canvas = document.createElement("canvas");
            // Premium quality 512x512
            canvas.width = 512;
            canvas.height = 512;
            const context = canvas.getContext("2d");

            if (!context) {
                reject(new Error(t("Canvas is unavailable.")));
                return;
            }

            const ratio = 512 / stageSize;
            context.fillStyle = "#070914";
            context.fillRect(0, 0, 512, 512);
            context.drawImage(avatarCropImage, left * ratio, top * ratio, width * ratio, height * ratio);
            canvas.toBlob((blob) => {
                if (blob) {
                    resolve(blob);
                    return;
                }

                reject(new Error(t("Image crop failed.")));
            }, "image/png", 0.92);
        });
    }

    async function uploadProfileAvatar(blob) {
        const data = new FormData();
        data.append("image", blob, "profile.png");

        const response = await fetch("/api/profile/image?isPremiumImage=true", {
            method: "POST",
            headers: {
                "RequestVerificationToken": getAntiForgeryToken()
            },
            body: data
        });

        if (!response.ok) {
            throw new Error(await readResponseText(response));
        }

        return response.json();
    }

    async function saveAvatarCrop() {
        if (!avatarCropSave) {
            return;
        }

        avatarCropSave.disabled = true;
        setProfileStatus(premiumProfileImageStatus, t("Uploading image..."));

        try {
            const blob = await getCroppedAvatarBlob();
            const profile = await uploadProfileAvatar(blob);
            renderProfile(profile);
            closeAvatarCrop();
            setProfileStatus(premiumProfileImageStatus, t("Profile image updated."), "success");
        } catch (error) {
            setProfileStatus(premiumProfileImageStatus, t(error.message || "Image upload failed."), "error");
        } finally {
            avatarCropSave.disabled = false;
        }
    }

    async function deleteProfileAvatar() {
        if (!deletePremiumProfileImageButton) {
            return;
        }

        const confirmed = await confirmProfileAction(
            t("Delete profile image?"),
            t("Your current profile image will be removed from the account."),
            t("Delete"),
            "delete"
        );
        if (!confirmed) {
            return;
        }

        deletePremiumProfileImageButton.disabled = true;
        setProfileStatus(premiumProfileImageStatus, t("Deleting image..."));

        const response = await fetch("/api/profile/image", {
            method: "DELETE",
            headers: {
                "RequestVerificationToken": getAntiForgeryToken()
            }
        });

        if (response.ok) {
            renderProfile(await response.json());
            setProfileStatus(premiumProfileImageStatus, t("Profile image deleted."), "success");
            return;
        }

        setProfileStatus(premiumProfileImageStatus, await readResponseText(response), "error");
        deletePremiumProfileImageButton.disabled = false;
    }

    // Toggle Premium Settings Endpoint
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

    // Toggle Event Listeners
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

    if (videoUploadsToggle) {
        videoUploadsToggle.addEventListener("change", () => savePremiumSetting({
            toggle: videoUploadsToggle,
            status: videoUploadsStatus,
            endpoint: "/api/profile/video-uploads",
            enabled: videoUploadsToggle.checked,
            profileKey: "isVideoUploadsEnabled",
            successEnabled: "Video uploads enabled.",
            successDisabled: "Video uploads disabled."
        }));
    }

    // Premium Avatar Event Listeners
    if (premiumProfileImageButton && premiumProfileImageInput) {
        premiumProfileImageButton.addEventListener("click", () => premiumProfileImageInput.click());
        premiumProfileImageInput.addEventListener("change", () => {
            const file = premiumProfileImageInput.files && premiumProfileImageInput.files[0];
            premiumProfileImageInput.value = "";
            if (!file) {
                return;
            }

            if (!allowedProfileImageTypes.includes(file.type)) {
                setProfileStatus(premiumProfileImageStatus, t("Only PNG, JPEG and JPG images up to 10 MB are allowed."), "error");
                return;
            }

            if (file.size > maxImageSize) {
                const fileLimitModal = document.querySelector("#fileLimitModal");
                const fileLimitCopy = document.querySelector("#fileLimitCopy");
                if (fileLimitModal && fileLimitCopy) {
                    fileLimitCopy.textContent = "The selected image exceeds the maximum allowed size of 10 MB.";
                    openModal(fileLimitModal);
                }
                return;
            }

            openAvatarCrop(file);
        });
    }

    if (deletePremiumProfileImageButton) {
        deletePremiumProfileImageButton.addEventListener("click", deleteProfileAvatar);
    }

    if (avatarCropCancel) {
        avatarCropCancel.addEventListener("click", closeAvatarCrop);
    }

    if (avatarCropSave) {
        avatarCropSave.addEventListener("click", saveAvatarCrop);
    }

    if (avatarZoom) {
        avatarZoom.addEventListener("input", () => {
            if (avatarCropState) {
                avatarCropState.zoom = Number.parseFloat(avatarZoom.value) || 1;
                renderAvatarCrop();
            }
        });
    }

    if (avatarCropStage) {
        avatarCropStage.addEventListener("pointerdown", (event) => {
            if (!avatarCropState) {
                return;
            }

            avatarCropStage.setPointerCapture(event.pointerId);
            avatarCropState.dragging = true;
            avatarCropState.dragX = event.clientX;
            avatarCropState.dragY = event.clientY;
        });

        avatarCropStage.addEventListener("pointermove", (event) => {
            if (!avatarCropState || !avatarCropState.dragging) {
                return;
            }

            avatarCropState.offsetX += event.clientX - avatarCropState.dragX;
            avatarCropState.offsetY += event.clientY - avatarCropState.dragY;
            avatarCropState.dragX = event.clientX;
            avatarCropState.dragY = event.clientY;
            renderAvatarCrop();
        });

        ["pointerup", "pointercancel", "lostpointercapture"].forEach((name) => {
            avatarCropStage.addEventListener(name, () => {
                if (avatarCropState) {
                    avatarCropState.dragging = false;
                }
            });
        });
    }

    window.ElovoProfilePage = {
        closeAvatarCrop
    };
})();
