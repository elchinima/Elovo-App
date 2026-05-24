(() => {
    const {
        getAntiForgeryToken,
        setAvatarElement,
        openModal,
        closeModal
    } = window.Elovo;
const profileAvatar = document.querySelector("#profileAvatar");
const profileImageInput = document.querySelector("#profileImageInput");
const profileImageButton = document.querySelector("#profileImageButton");
const deleteProfileImageButton = document.querySelector("#deleteProfileImageButton");
const profileImageStatus = document.querySelector("#profileImageStatus");
const profileEmailForm = document.querySelector("#profileEmailForm");
const profileEmail = document.querySelector("#profileEmail");
const profileEmailStatus = document.querySelector("#profileEmailStatus");
const profilePasswordForm = document.querySelector("#profilePasswordForm");
const currentPassword = document.querySelector("#currentPassword");
const newPassword = document.querySelector("#newPassword");
const profilePasswordStatus = document.querySelector("#profilePasswordStatus");
const twoFactorToggle = document.querySelector("#twoFactorToggle");
const twoFactorStatus = document.querySelector("#twoFactorStatus");
const avatarCropModal = document.querySelector("#avatarCropModal");
const avatarCropStage = document.querySelector("#avatarCropStage");
const avatarCropImage = document.querySelector("#avatarCropImage");
const avatarZoom = document.querySelector("#avatarZoom");
const avatarCropSave = document.querySelector("#avatarCropSave");
const avatarCropCancel = document.querySelector("#avatarCropCancel");
let avatarCropState = null;
const allowedProfileImageTypes = ["image/png", "image/jpeg", "image/jpg"];
const maxImageSize = 10 * 1024 * 1024;
function setProfileStatus(element, message, kind = "") {
    if (!element) {
        return;
    }

    element.textContent = message;
    element.classList.toggle("is-error", kind === "error");
    element.classList.toggle("is-success", kind === "success");
}

async function readResponseText(response) {
    const text = await response.text();
    return text || "Request failed.";
}

function renderProfile(profile) {
    if (!profile) {
        return;
    }

    window.elovoProfile = {
        ...window.elovoProfile,
        ...profile,
        hasEmail: !!profile.email
    };

    setAvatarElement(profileAvatar, profile, profile.initial);
    if (profileEmail) {
        profileEmail.value = profile.email || "";
    }

    if (deleteProfileImageButton) {
        deleteProfileImageButton.disabled = !profile.profileImageUrl;
    }

    if (twoFactorToggle) {
        twoFactorToggle.checked = !!profile.isTwoFactorEnabled;
        twoFactorToggle.disabled = !profile.email;
    }
}
function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
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
        const stageSize = rect.width || 320;
        avatarCropState = {
            file,
            url,
            width: avatarCropImage.naturalWidth,
            height: avatarCropImage.naturalHeight,
            baseScale: Math.max(stageSize / avatarCropImage.naturalWidth, stageSize / avatarCropImage.naturalHeight),
            zoom: 1,
            offsetX: 0,
            offsetY: 0,
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
            reject(new Error("Image is not ready."));
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
        canvas.width = 512;
        canvas.height = 512;
        const context = canvas.getContext("2d");

        if (!context) {
            reject(new Error("Canvas is unavailable."));
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

            reject(new Error("Image crop failed."));
        }, "image/png", 0.92);
    });
}

async function uploadProfileAvatar(blob) {
    const data = new FormData();
    data.append("image", blob, "profile.png");

    const response = await fetch("/api/profile/image", {
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
    setProfileStatus(profileImageStatus, "Uploading image...");

    try {
        const blob = await getCroppedAvatarBlob();
        const profile = await uploadProfileAvatar(blob);
        renderProfile(profile);
        closeAvatarCrop();
        setProfileStatus(profileImageStatus, "Profile image updated.", "success");
    } catch (error) {
        setProfileStatus(profileImageStatus, error.message || "Image upload failed.", "error");
    } finally {
        avatarCropSave.disabled = false;
    }
}

async function deleteProfileAvatar() {
    if (!deleteProfileImageButton) {
        return;
    }

    deleteProfileImageButton.disabled = true;
    setProfileStatus(profileImageStatus, "Deleting image...");

    const response = await fetch("/api/profile/image", {
        method: "DELETE",
        headers: {
            "RequestVerificationToken": getAntiForgeryToken()
        }
    });

    if (response.ok) {
        renderProfile(await response.json());
        setProfileStatus(profileImageStatus, "Profile image deleted.", "success");
        return;
    }

    setProfileStatus(profileImageStatus, await readResponseText(response), "error");
    deleteProfileImageButton.disabled = false;
}

async function saveProfileEmail(event) {
    event.preventDefault();
    setProfileStatus(profileEmailStatus, "Saving email...");

    const response = await fetch("/api/profile/email", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": getAntiForgeryToken()
        },
        body: JSON.stringify({ email: profileEmail ? profileEmail.value : "" })
    });

    if (response.ok) {
        renderProfile(await response.json());
        setProfileStatus(profileEmailStatus, "Email saved.", "success");
        setProfileStatus(twoFactorStatus, "");
        return;
    }

    setProfileStatus(profileEmailStatus, await readResponseText(response), "error");
}

async function saveProfilePassword(event) {
    event.preventDefault();
    setProfileStatus(profilePasswordStatus, "Changing password...");

    const response = await fetch("/api/profile/password", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": getAntiForgeryToken()
        },
        body: JSON.stringify({
            currentPassword: currentPassword ? currentPassword.value : "",
            newPassword: newPassword ? newPassword.value : ""
        })
    });

    if (response.ok) {
        if (profilePasswordForm) {
            profilePasswordForm.reset();
        }

        setProfileStatus(profilePasswordStatus, "Password changed.", "success");
        return;
    }

    setProfileStatus(profilePasswordStatus, await readResponseText(response), "error");
}

async function setTwoFactorEnabled() {
    if (!twoFactorToggle) {
        return;
    }

    twoFactorToggle.disabled = true;
    setProfileStatus(twoFactorStatus, "Saving setting...");

    const response = await fetch("/api/profile/two-factor", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": getAntiForgeryToken()
        },
        body: JSON.stringify({ enabled: twoFactorToggle.checked })
    });

    if (response.ok) {
        renderProfile(await response.json());
        setProfileStatus(twoFactorStatus, twoFactorToggle.checked ? "Two-factor authentication enabled." : "Two-factor authentication disabled.", "success");
        return;
    }

    twoFactorToggle.checked = !twoFactorToggle.checked;
    twoFactorToggle.disabled = false;
    setProfileStatus(twoFactorStatus, await readResponseText(response), "error");
}

if (profileImageButton && profileImageInput) {
    profileImageButton.addEventListener("click", () => profileImageInput.click());
    profileImageInput.addEventListener("change", () => {
        const file = profileImageInput.files && profileImageInput.files[0];
        profileImageInput.value = "";

        if (!file) {
            return;
        }

        if (!allowedProfileImageTypes.includes(file.type) || file.size > maxImageSize) {
            setProfileStatus(profileImageStatus, "Only PNG, JPEG and JPG images up to 10 MB are allowed.", "error");
            return;
        }

        openAvatarCrop(file);
    });
}

if (deleteProfileImageButton) {
    deleteProfileImageButton.addEventListener("click", deleteProfileAvatar);
}

if (profileEmailForm) {
    profileEmailForm.addEventListener("submit", saveProfileEmail);
}

if (profilePasswordForm) {
    profilePasswordForm.addEventListener("submit", saveProfilePassword);
}

if (twoFactorToggle) {
    twoFactorToggle.addEventListener("change", setTwoFactorEnabled);
}

if (avatarZoom) {
    avatarZoom.addEventListener("input", () => {
        if (!avatarCropState) {
            return;
        }

        avatarCropState.zoom = Number.parseFloat(avatarZoom.value) || 1;
        renderAvatarCrop();
    });
}

if (avatarCropSave) {
    avatarCropSave.addEventListener("click", saveAvatarCrop);
}

if (avatarCropCancel) {
    avatarCropCancel.addEventListener("click", closeAvatarCrop);
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
