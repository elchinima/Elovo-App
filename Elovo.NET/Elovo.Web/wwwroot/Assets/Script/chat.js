(() => {
    const {
        navigateWithLoader,
        getAntiForgeryToken,
        setAvatarElement,
        readResponseText,
        showPageLoader,
        purgeExpiredChatMessages
    } = window.Elovo;
const logoutButton = document.querySelector("#logoutButton");
const settingsButton = document.querySelector("#settingsButton");
const restoreHiddenButton = document.querySelector("#restoreHiddenButton");
const messengerView = document.querySelector("#messengerView");
const chatList = document.querySelector("#chatList");
const searchInput = document.querySelector("#searchInput");
const chatSearchButton = document.querySelector("#chatSearchButton");
const messageStream = document.querySelector("#messageStream");
const messageForm = document.querySelector("#messageForm");
const messageInput = document.querySelector("#messageInput");
const messageSubmitIcon = document.querySelector("#messageSubmitIcon");
const attachImageButton = document.querySelector("#attachImageButton");
const imageInput = document.querySelector("#imageInput");
const activeName = document.querySelector("#activeName");
const activeStatus = document.querySelector("#activeStatus");
const activeAvatar = document.querySelector("#activeAvatar");
const backButton = document.querySelector("#backButton");
const callButton = document.querySelector("#callButton");
const callModal = document.querySelector("#callModal");
const callAvatar = document.querySelector("#callAvatar");
const callUserName = document.querySelector("#callUserName");
const callDuration = document.querySelector("#callDuration");
const endCallButton = document.querySelector("#endCallButton");
const muteCallButton = document.querySelector("#muteCallButton");
const muteCallLabel = document.querySelector("#muteCallLabel");
const speakerCallButton = document.querySelector("#speakerCallButton");
const callSpeakerLabel = document.querySelector("#callSpeakerLabel");
if (callDuration) {
    callDuration.style.color = "var(--muted)";
}
const allFriendsButton = document.querySelector("#allFriendsButton");
const addFriendButton = document.querySelector("#addFriendButton");
const friendRequestsButton = document.querySelector("#friendRequestsButton");
const allFriendsModal = document.querySelector("#allFriendsModal");
const addFriendModal = document.querySelector("#addFriendModal");
const friendRequestsModal = document.querySelector("#friendRequestsModal");
const restoreHiddenModal = document.querySelector("#restoreHiddenModal");
const deleteFriendModal = document.querySelector("#deleteFriendModal");
const confirmRestoreHiddenButton = document.querySelector("#confirmRestoreHiddenButton");
const confirmDeleteFriendButton = document.querySelector("#confirmDeleteFriendButton");
const deleteFriendCopy = document.querySelector("#deleteFriendCopy");
const allFriendsList = document.querySelector("#allFriendsList");
const userSearchInput = document.querySelector("#userSearchInput");
const userSearchButton = document.querySelector("#userSearchButton");
const userSearchResults = document.querySelector("#userSearchResults");
const friendRequestsList = document.querySelector("#friendRequestsList");
let conversations = [];
let hiddenConversationIds = new Set();
let activeConversation = null;
let connection = null;
let typingTimer = null;
let latestMessageId = "";
let isSending = false;
let messageActionTimer = null;
let activeMessageActions = null;
let imageTransferStatus = null;
let voiceTransferStatus = null;
let avatarCropState = null;
let conversationSearchTerm = "";
let friendPendingDeletion = null;
let isLeavingChatPage = false;
let voiceRecorder = null;
let voiceStream = null;
let voiceChunks = [];
let voiceRecordStartedAt = 0;
let voiceRecordTimer = null;
let voiceAutoStopTimer = null;
let isPreparingVoice = false;
let isRecordingVoice = false;
let shouldStopVoiceWhenReady = false;
let activeVoiceAudio = null;
let activeCall = null;
let callTimer = null;
let incomingCall = null;
let incomingCallBanner = null;
let remoteCallAudio = null;
let browserSpeakerDeviceIndex = 0;
const allowedImageTypes = ["image/png", "image/jpeg", "image/jpg", "image/gif"];
const maxImageSize = 10 * 1024 * 1024;
const maxVoiceDurationMs = 60 * 1000;
const voiceAudioBitRate = 64000;
const minVoiceDurationMs = 450;
const allowedVoiceTypes = ["audio/webm", "audio/ogg", "audio/mp4"];
const imageCacheDbName = "elovo-image-cache";
const imageCacheStoreName = "images";
const voiceCacheDbName = "elovo-voice-cache";
const voiceCacheStoreName = "voices";
let imageCacheDb = null;
let imageCacheDbPromise = null;
let voiceCacheDb = null;
let voiceCacheDbPromise = null;
const pendingUploadedImages = new Map();
const pendingUploadedVoices = new Map();
function getCurrentUserId() {
    return (window.elovoCurrentUserId || "").toLowerCase();
}

function normalizeId(value) {
    return (value || "").toLowerCase();
}

function sameId(first, second) {
    return normalizeId(first) === normalizeId(second);
}

function getConversationStorageKey(userId) {
    const currentUserId = getCurrentUserId();
    const otherUserId = normalizeId(userId);
    const pair = [currentUserId, otherUserId].sort().join(":");
    return `elovo:messages:${pair}`;
}

function getConversationStorageUserId(key) {
    const prefix = "elovo:messages:";
    if (!key || !key.startsWith(prefix)) {
        return "";
    }

    const ids = key.slice(prefix.length).split(":");
    const currentUserId = getCurrentUserId();
    return ids.includes(currentUserId)
        ? ids.find((id) => id !== currentUserId) || ""
        : "";
}

function getHiddenConversationStorageKey() {
    return `elovo:hidden-conversations:${getCurrentUserId()}`;
}

function getImageCacheKey(messageId) {
    return `${getCurrentUserId()}:${messageId}`;
}

function getImageStorageKey(path) {
    return (path || "").trim().toLowerCase();
}

function getVoiceCacheKey(messageId) {
    return `${getCurrentUserId()}:voice:${messageId}`;
}

function getVoiceStorageKey(path) {
    return (path || "").trim().toLowerCase();
}

function openImageCacheDb() {
    if (!window.indexedDB) {
        return Promise.reject(new Error("IndexedDB is unavailable."));
    }

    if (imageCacheDb) {
        return Promise.resolve(imageCacheDb);
    }

    if (imageCacheDbPromise) {
        return imageCacheDbPromise;
    }

    imageCacheDbPromise = new Promise((resolve, reject) => {
        const request = window.indexedDB.open(imageCacheDbName, 1);

        request.addEventListener("upgradeneeded", () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(imageCacheStoreName)) {
                db.createObjectStore(imageCacheStoreName);
            }
        });

        request.addEventListener("success", () => {
            imageCacheDb = request.result;
            imageCacheDb.addEventListener("close", () => {
                imageCacheDb = null;
                imageCacheDbPromise = null;
            });
            resolve(imageCacheDb);
        });

        request.addEventListener("error", () => reject(request.error || new Error("Image cache failed.")));
    });

    return imageCacheDbPromise;
}

function openVoiceCacheDb() {
    if (!window.indexedDB) {
        return Promise.reject(new Error("IndexedDB is unavailable."));
    }

    if (voiceCacheDb) {
        return Promise.resolve(voiceCacheDb);
    }

    if (voiceCacheDbPromise) {
        return voiceCacheDbPromise;
    }

    voiceCacheDbPromise = new Promise((resolve, reject) => {
        const request = window.indexedDB.open(voiceCacheDbName, 1);

        request.addEventListener("upgradeneeded", () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(voiceCacheStoreName)) {
                db.createObjectStore(voiceCacheStoreName);
            }
        });

        request.addEventListener("success", () => {
            voiceCacheDb = request.result;
            voiceCacheDb.addEventListener("close", () => {
                voiceCacheDb = null;
                voiceCacheDbPromise = null;
            });
            resolve(voiceCacheDb);
        });

        request.addEventListener("error", () => reject(request.error || new Error("Voice cache failed.")));
    });

    return voiceCacheDbPromise;
}

function runImageCacheRequest(mode, action) {
    return openImageCacheDb().then((db) => new Promise((resolve, reject) => {
        const transaction = db.transaction(imageCacheStoreName, mode);
        const store = transaction.objectStore(imageCacheStoreName);
        const request = action(store);

        request.addEventListener("success", () => resolve(request.result));
        request.addEventListener("error", () => reject(request.error || new Error("Image cache request failed.")));
    }));
}

function runVoiceCacheRequest(mode, action) {
    return openVoiceCacheDb().then((db) => new Promise((resolve, reject) => {
        const transaction = db.transaction(voiceCacheStoreName, mode);
        const store = transaction.objectStore(voiceCacheStoreName);
        const request = action(store);

        request.addEventListener("success", () => resolve(request.result));
        request.addEventListener("error", () => reject(request.error || new Error("Voice cache request failed.")));
    }));
}

async function readCachedImageBlob(messageId) {
    try {
        const item = await runImageCacheRequest("readonly", (store) => store.get(getImageCacheKey(messageId)));
        return item && item.blob ? item.blob : null;
    } catch {
        return null;
    }
}

async function readCachedVoiceBlob(messageId) {
    try {
        const item = await runVoiceCacheRequest("readonly", (store) => store.get(getVoiceCacheKey(messageId)));
        return item && item.blob ? item.blob : null;
    } catch {
        return null;
    }
}

async function writeCachedImageBlob(message, blob) {
    if (!message || !message.id || !blob) {
        return false;
    }

    try {
        await runImageCacheRequest("readwrite", (store) => store.put({
            blob,
            fileName: message.imageFileName || "",
            updatedAt: new Date().toISOString()
        }, getImageCacheKey(message.id)));
        return true;
    } catch {
        return false;
    }
}

async function writeCachedVoiceBlob(message, blob) {
    if (!message || !message.id || !blob) {
        return false;
    }

    try {
        await runVoiceCacheRequest("readwrite", (store) => store.put({
            blob,
            durationSeconds: message.voiceDurationSeconds || null,
            updatedAt: new Date().toISOString()
        }, getVoiceCacheKey(message.id)));
        return true;
    } catch {
        return false;
    }
}

async function removeCachedImage(messageId) {
    try {
        await runImageCacheRequest("readwrite", (store) => store.delete(getImageCacheKey(messageId)));
    } catch {
        // Local image cache cleanup is best-effort.
    }
}

async function removeCachedVoice(messageId) {
    try {
        await runVoiceCacheRequest("readwrite", (store) => store.delete(getVoiceCacheKey(messageId)));
    } catch {
        // Local voice cache cleanup is best-effort.
    }
}

async function cacheImageForMessage(message) {
    if (!message || !message.isImage || !message.id) {
        return false;
    }

    if (await readCachedImageBlob(message.id)) {
        return true;
    }

    const storageKey = getImageStorageKey(message.imageStoragePath || message.content);
    const uploadedBlob = storageKey ? pendingUploadedImages.get(storageKey) : null;
    if (uploadedBlob) {
        pendingUploadedImages.delete(storageKey);
        return writeCachedImageBlob(message, uploadedBlob);
    }

    if (!message.imagePath) {
        return false;
    }

    try {
        const response = await fetch(message.imagePath);
        if (!response.ok) {
            return false;
        }

        const blob = await response.blob();
        return writeCachedImageBlob(message, blob);
    } catch {
        return false;
    }
}

async function cacheVoiceForMessage(message) {
    if (!message || !message.isVoice || !message.id) {
        return false;
    }

    if (await readCachedVoiceBlob(message.id)) {
        return true;
    }

    const storageKey = getVoiceStorageKey(message.voiceUrl);
    const uploadedBlob = storageKey ? pendingUploadedVoices.get(storageKey) : null;
    if (uploadedBlob) {
        pendingUploadedVoices.delete(storageKey);
        return writeCachedVoiceBlob(message, uploadedBlob);
    }

    if (!message.voiceUrl) {
        return false;
    }

    try {
        const response = await fetch(message.voiceUrl);
        if (!response.ok) {
            return false;
        }

        const blob = await response.blob();
        return writeCachedVoiceBlob(message, blob);
    } catch {
        return false;
    }
}

async function requestPersistentStorage() {
    if (!navigator.storage || !navigator.storage.persist) {
        return false;
    }

    try {
        return await navigator.storage.persist();
    } catch {
        return false;
    }
}

function readHiddenConversationIds() {
    try {
        const raw = window.localStorage.getItem(getHiddenConversationStorageKey());
        const ids = raw ? JSON.parse(raw) : [];
        return new Set(Array.isArray(ids) ? ids.map(normalizeId).filter(Boolean) : []);
    } catch {
        return new Set();
    }
}

function writeHiddenConversationIds() {
    window.localStorage.setItem(getHiddenConversationStorageKey(), JSON.stringify([...hiddenConversationIds]));
}

function updateRestoreHiddenButton() {
    if (!restoreHiddenButton) {
        return;
    }

    restoreHiddenButton.hidden = hiddenConversationIds.size === 0;
}

function readStoredMessages(userId) {
    try {
        const raw = window.localStorage.getItem(getConversationStorageKey(userId));
        const messages = raw ? JSON.parse(raw) : [];
        return Array.isArray(messages) ? messages : [];
    } catch {
        return [];
    }
}

function writeStoredMessages(userId, messages) {
    try {
        window.localStorage.setItem(getConversationStorageKey(userId), JSON.stringify(messages));
        return true;
    } catch {
        return false;
    }
}

function deleteStoredMessages(userId) {
    try {
        window.localStorage.removeItem(getConversationStorageKey(userId));
    } catch {
        // Local cleanup is best-effort.
    }
}

async function deleteLocalConversationHistory(userId) {
    const messages = readStoredMessages(userId);
    for (const message of messages) {
        if (!message || !message.id) {
            continue;
        }

        await removeCachedImage(message.id);
        await removeCachedVoice(message.id);
    }

    deleteStoredMessages(userId);
}

function updateStoredMessage(otherUserId, messageId, update) {
    const messages = readStoredMessages(otherUserId);
    let changed = false;
    const updatedMessages = messages.map((message) => {
        if (message.id !== messageId) {
            return message;
        }

        changed = true;
        return { ...message, ...update };
    });

    if (changed) {
        return writeStoredMessages(otherUserId, updatedMessages);
    }

    return false;
}

function removeStoredMessage(otherUserId, messageId) {
    const messages = readStoredMessages(otherUserId);
    const updatedMessages = messages.filter((message) => message.id !== messageId);
    if (updatedMessages.length === messages.length) {
        return false;
    }

    return writeStoredMessages(otherUserId, updatedMessages);
}

function getMessageParticipantId(message) {
    const currentUserId = getCurrentUserId();
    const senderId = normalizeId(message.senderId);
    const receiverId = normalizeId(message.receiverId);
    return senderId === currentUserId ? receiverId : senderId;
}

function storeMessage(message) {
    const otherUserId = getMessageParticipantId(message);
    if (!otherUserId) {
        return false;
    }

    const messages = readStoredMessages(otherUserId);
    if (messages.some((storedMessage) => storedMessage.id === message.id)) {
        return true;
    }

    messages.push(message);
    messages.sort((first, second) => new Date(first.sentAt) - new Date(second.sentAt));
    return writeStoredMessages(otherUserId, messages);
}

function acknowledgePendingMessage(message) {
    if (!connection ||
        !message ||
        !message.isPending ||
        !sameId(message.receiverId, getCurrentUserId())) {
        return;
    }

    connection.invoke("AcknowledgePendingMessages", [message.id]).catch(() => { });
}

function markOutgoingMessagesRead(userId, readAt) {
    const otherUserId = normalizeId(userId);
    if (!otherUserId) {
        return false;
    }

    const currentUserId = getCurrentUserId();
    const messages = readStoredMessages(otherUserId);
    let changed = false;

    const updatedMessages = messages.map((message) => {
        if (sameId(message.senderId, currentUserId) &&
            sameId(message.receiverId, otherUserId) &&
            (!message.readAt || message.isPending)) {
            changed = true;
            return { ...message, isPending: false, deliveredAt: message.deliveredAt || readAt, readAt: message.readAt || readAt };
        }

        return message;
    });

    if (changed) {
        writeStoredMessages(otherUserId, updatedMessages);
    }

    return changed;
}

function markOutgoingMessagesReadBefore(userId, readAt) {
    const otherUserId = normalizeId(userId);
    if (!otherUserId || !readAt) {
        return false;
    }

    const readTime = new Date(readAt).getTime();
    if (Number.isNaN(readTime)) {
        return false;
    }

    const currentUserId = getCurrentUserId();
    const messages = readStoredMessages(otherUserId);
    let changed = false;

    const updatedMessages = messages.map((message) => {
        const sentTime = new Date(message.sentAt).getTime();
        if (sameId(message.senderId, currentUserId) &&
            sameId(message.receiverId, otherUserId) &&
            (!message.readAt || message.isPending) &&
            !Number.isNaN(sentTime) &&
            sentTime <= readTime) {
            changed = true;
            return { ...message, isPending: false, deliveredAt: message.deliveredAt || readAt, readAt: message.readAt || readAt };
        }

        return message;
    });

    if (changed) {
        writeStoredMessages(otherUserId, updatedMessages);
    }

    return changed;
}

function markOutgoingMessagesDelivered(userId, messageIds, deliveredAt) {
    const otherUserId = normalizeId(userId);
    const ids = Array.isArray(messageIds) ? messageIds : [];
    if (!otherUserId || ids.length === 0) {
        return false;
    }

    const idSet = new Set(ids.map((id) => `${id}`));
    const currentUserId = getCurrentUserId();
    const messages = readStoredMessages(otherUserId);
    let changed = false;

    const updatedMessages = messages.map((message) => {
        if (sameId(message.senderId, currentUserId) && idSet.has(`${message.id}`)) {
            changed = true;
            return { ...message, isPending: false, deliveredAt };
        }

        return message;
    });

    if (changed) {
        writeStoredMessages(otherUserId, updatedMessages);
    }

    return changed;
}

async function refreshConversationAfterMessageChange(userId) {
    if (activeConversation && sameId(activeConversation.userId, userId)) {
        await loadMessages(activeConversation.userId);
    }

    await loadConversations();
}

function notifyActiveConversationRead() {
    if (!connection || !activeConversation) {
        return;
    }

    connection.invoke("MarkMessagesRead", activeConversation.userId).catch(() => { });
}

function getStoredConversationSummary(userId) {
    const messages = readStoredMessages(userId);
    const lastMessage = messages.at(-1);

    return {
        lastMessage: lastMessage ? getMessagePreview(lastMessage) : "Start a conversation.",
        lastMessageAt: lastMessage ? lastMessage.sentAt : null
    };
}

function getMessagePreview(message) {
    if (message.isVoice) {
        return "Voice message";
    }

    return message.isImage ? "Image" : message.content;
}

function applyLocalConversationHistory(items) {
    return items
        .map((chat) => {
            markOutgoingMessagesReadBefore(chat.userId, chat.otherUserReadAt);
            const summary = getStoredConversationSummary(chat.userId);
            return {
                ...chat,
                lastMessage: summary.lastMessage,
                lastMessageAt: summary.lastMessageAt,
                unreadCount: 0
            };
        })
        .sort((first, second) => {
            const firstTime = first.lastMessageAt ? new Date(first.lastMessageAt).getTime() : 0;
            const secondTime = second.lastMessageAt ? new Date(second.lastMessageAt).getTime() : 0;
            return secondTime - firstTime || first.username.localeCompare(second.username);
        });
}

async function purgeRemovedLocalConversations(validUserIds) {
    const validIds = new Set(validUserIds.map(normalizeId).filter(Boolean));
    const keys = [];

    for (let index = 0; index < window.localStorage.length; index += 1) {
        const key = window.localStorage.key(index);
        const otherUserId = getConversationStorageUserId(key);
        if (otherUserId && !validIds.has(otherUserId)) {
            keys.push({ key, otherUserId });
        }
    }

    for (const item of keys) {
        await deleteLocalConversationHistory(item.otherUserId);
        window.localStorage.removeItem(item.key);
        hiddenConversationIds.delete(item.otherUserId);
    }

    if (keys.length > 0) {
        writeHiddenConversationIds();
        updateRestoreHiddenButton();
    }
}

function formatTime(value) {
    if (!value) {
        return "";
    }

    const date = new Date(value);
    const now = new Date();

    if (Number.isNaN(date.getTime())) {
        return "";
    }

    if (date.toDateString() === now.toDateString()) {
        return new Intl.DateTimeFormat("en", {
            hour: "2-digit",
            minute: "2-digit",
            hour12: false
        }).format(date);
    }

    return new Intl.DateTimeFormat("en", {
        month: "short",
        day: "numeric"
    }).format(date);
}

function formatStatus(chat) {
    if (chat.isOnline) {
        return "Online now";
    }

    if (!chat.lastSeenAt) {
        return "Offline";
    }

    return `Last seen ${formatTime(chat.lastSeenAt)}`;
}

function openModal(modal) {
    if (!modal) {
        return;
    }

    modal.classList.add("is-open");
    modal.setAttribute("aria-hidden", "false");
}

function closeModal(modal) {
    if (!modal) {
        return;
    }

    modal.classList.remove("is-open");
    modal.setAttribute("aria-hidden", "true");
}

function formatCallDuration(totalSeconds) {
    const safeSeconds = Math.max(0, Math.floor(totalSeconds));
    const hours = Math.floor(safeSeconds / 3600);
    const minutes = Math.floor((safeSeconds % 3600) / 60);
    const seconds = safeSeconds % 60;
    const pad = (value) => value.toString().padStart(2, "0");

    if (hours > 0) {
        return `${hours}:${pad(minutes)}:${pad(seconds)}`;
    }

    return `${pad(minutes)}:${pad(seconds)}`;
}

function updateCallDuration() {
    if (!activeCall || !activeCall.startedAt || !callDuration) {
        return;
    }

    callDuration.textContent = formatCallDuration((Date.now() - activeCall.startedAt) / 1000);
}

function updateCallControls() {
    if (!activeCall) {
        return;
    }

    if (muteCallButton) {
        muteCallButton.classList.toggle("is-active", activeCall.isMuted);
        muteCallButton.setAttribute("aria-pressed", activeCall.isMuted ? "true" : "false");
        muteCallButton.setAttribute("aria-label", activeCall.isMuted ? "Unmute microphone" : "Mute microphone");
        muteCallButton.title = activeCall.isMuted ? "Unmute microphone" : "Mute microphone";
        const muteIcon = muteCallButton.querySelector("img");
        if (muteIcon) {
            muteIcon.src = activeCall.isMuted
                ? "/Assets/Images/Icons/microphone-off.svg"
                : "/Assets/Images/Icons/microphone.svg";
        }
    }

    if (muteCallLabel) {
        muteCallLabel.textContent = activeCall.isMuted ? "Muted" : "Mute";
    }

    updateSpeakerButtonState(activeCall.speakerLabel, activeCall.speakerPressed);
}

function updateCallStatus(status) {
    if (callDuration) {
        callDuration.textContent = status;
    }
}

function startCallTimer() {
    if (!activeCall || activeCall.startedAt) {
        return;
    }

    activeCall.startedAt = Date.now();
    updateCallDuration();
    callTimer = window.setInterval(updateCallDuration, 1000);
}

function stopCallTimer() {
    if (callTimer) {
        window.clearInterval(callTimer);
        callTimer = null;
    }
}

function getRemoteCallAudio() {
    if (remoteCallAudio) {
        return remoteCallAudio;
    }

    remoteCallAudio = document.createElement("audio");
    remoteCallAudio.autoplay = true;
    remoteCallAudio.playsInline = true;
    remoteCallAudio.hidden = true;
    document.body.appendChild(remoteCallAudio);
    return remoteCallAudio;
}

function resetCallControls() {
    if (callButton) {
        callButton.classList.remove("is-active");
        callButton.setAttribute("aria-pressed", "false");
    }

    if (muteCallButton) {
        muteCallButton.classList.remove("is-active");
        muteCallButton.setAttribute("aria-pressed", "false");
        muteCallButton.setAttribute("aria-label", "Mute microphone");
        muteCallButton.title = "Mute microphone";
        const muteIcon = muteCallButton.querySelector("img");
        if (muteIcon) {
            muteIcon.src = "/Assets/Images/Icons/microphone.svg";
        }
    }

    if (muteCallLabel) {
        muteCallLabel.textContent = "Mute";
    }

    if (speakerCallButton) {
        speakerCallButton.classList.remove("is-active");
        speakerCallButton.setAttribute("aria-label", "Change speaker. Current: Speaker");
        speakerCallButton.title = "Change speaker: Speaker";
    }

    if (callSpeakerLabel) {
        callSpeakerLabel.textContent = "Speaker";
    }

    if (callDuration) {
        callDuration.textContent = "00:00";
    }
}

function setCallModalUser(user) {
    if (callUserName) {
        callUserName.textContent = user.username || "Audio call";
    }

    if (callAvatar) {
        setAvatarElement(callAvatar, user, user.initial || "?");
    }
}

async function fetchTurnIceServers() {
    const response = await fetch("/api/turn-credentials", {
        headers: { "Accept": "application/json" }
    });

    if (!response.ok) {
        throw new Error(await readResponseText(response));
    }

    const credentials = await response.json();
    if (!credentials || !Array.isArray(credentials.iceServers)) {
        throw new Error("TURN credentials response is invalid.");
    }

    return credentials.iceServers;
}

function createCallState(remoteUserId, remoteUser) {
    activeCall = {
        remoteUserId,
        remoteUser,
        peerConnection: null,
        localStream: null,
        remoteStream: new MediaStream(),
        pendingIceCandidates: [],
        startedAt: 0,
        isMuted: false,
        speakerLabel: "Speaker",
        speakerPressed: true
    };

    setCallModalUser(remoteUser);
    updateCallStatus("00:00");

    if (callButton) {
        callButton.classList.add("is-active");
        callButton.setAttribute("aria-pressed", "true");
    }

    updateCallControls();
    openModal(callModal);
}

async function createPeerConnection(remoteUserId) {
    if (!activeCall) {
        throw new Error("Call state is unavailable.");
    }

    const iceServers = await fetchTurnIceServers();
    const peerConnection = new RTCPeerConnection({ iceServers });
    const localStream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, bitrate: 32000 }
    });

    activeCall.peerConnection = peerConnection;
    activeCall.localStream = localStream;

    localStream.getTracks().forEach((track) => peerConnection.addTrack(track, localStream));

    peerConnection.addEventListener("track", (event) => {
        const audio = getRemoteCallAudio();
        const [remoteStream] = event.streams;
        audio.srcObject = remoteStream || activeCall.remoteStream;

        if (!remoteStream) {
            activeCall.remoteStream.addTrack(event.track);
        }

        audio.play().catch(() => { });
    });

    peerConnection.addEventListener("icecandidate", (event) => {
        if (!event.candidate || !connection) {
            return;
        }

        const candidate = typeof event.candidate.toJSON === "function"
            ? event.candidate.toJSON()
            : event.candidate;
        connection.invoke("IceCandidate", remoteUserId, JSON.stringify(candidate)).catch(() => { });
    });

    peerConnection.addEventListener("iceconnectionstatechange", () => {
        if (!activeCall || activeCall.peerConnection !== peerConnection) {
            return;
        }

        if (peerConnection.iceConnectionState === "connected" || peerConnection.iceConnectionState === "completed") {
            startCallTimer();
        }

        if (peerConnection.iceConnectionState === "connected" && window.AndroidBridge) {
            window.AndroidBridge.enableProximitySensor();
            updateSpeakerButtonUI(window.AndroidBridge.getCurrentAudioDevice());
        }

        if (peerConnection.iceConnectionState === "failed") {
            endActiveCall(true);
        }
    });

    return peerConnection;
}

async function addPendingIceCandidates() {
    if (!activeCall || !activeCall.peerConnection || !activeCall.peerConnection.remoteDescription) {
        return;
    }

    const candidates = activeCall.pendingIceCandidates.splice(0);
    for (const candidate of candidates) {
        await activeCall.peerConnection.addIceCandidate(candidate).catch(() => { });
    }
}

function parseIceCandidate(candidate) {
    if (!candidate) {
        return null;
    }

    try {
        return new RTCIceCandidate(JSON.parse(candidate));
    } catch {
        try {
            return new RTCIceCandidate({ candidate });
        } catch {
            return null;
        }
    }
}

async function startOutgoingCall() {
    if (!activeConversation || !callModal || !connection || connection.state !== signalR.HubConnectionState.Connected) {
        return;
    }

    if (!navigator.mediaDevices || !window.RTCPeerConnection) {
        updateCallStatus("Calls unavailable");
        return;
    }

    if (activeCall) {
        openModal(callModal);
        return;
    }

    createCallState(activeConversation.userId, activeConversation);
    if (window.AndroidBridge) {
        window.AndroidBridge.onCallStarted();
        // Ждём пока WebView поставит speaker, потом переключаем на earpiece
        setTimeout(() => {
            window.AndroidBridge.setAudioDevice('earpiece');
            updateSpeakerButtonUI('earpiece');
            if (speakerCallButton) speakerCallButton.disabled = false;
        }, 350);
        // Блокируем кнопку динамика на 350мс
        if (speakerCallButton) speakerCallButton.disabled = true;
    }

    try {
        const peerConnection = await createPeerConnection(activeConversation.userId);
        await connection.invoke("CallUser", activeConversation.userId);
        const offer = await peerConnection.createOffer({ offerToReceiveAudio: true });
        await peerConnection.setLocalDescription(offer);
        await connection.invoke("CallOffer", activeConversation.userId, offer.sdp);
    } catch {
        endActiveCall(false);
    }
}

async function acceptIncomingCall() {
    if (!incomingCall || !callModal || !connection || connection.state !== signalR.HubConnectionState.Connected) {
        return;
    }

    const call = incomingCall;
    dismissIncomingCallBanner();
    createCallState(call.callerId, {
        userId: call.callerId,
        username: call.callerName,
        profileImageUrl: call.callerAvatar,
        initial: call.callerName ? call.callerName.slice(0, 1).toUpperCase() : "?"
    });
    if (window.AndroidBridge) {
        window.AndroidBridge.onCallStarted();
        // Ждём пока WebView поставит speaker, потом переключаем на earpiece
        setTimeout(() => {
            window.AndroidBridge.setAudioDevice('earpiece');
            updateSpeakerButtonUI('earpiece');
            if (speakerCallButton) speakerCallButton.disabled = false;
        }, 350);
        // Блокируем кнопку динамика на 350мс
        if (speakerCallButton) speakerCallButton.disabled = true;
    }

    try {
        const peerConnection = await createPeerConnection(call.callerId);
        activeCall.pendingIceCandidates = call.pendingIceCandidates || [];
        if (!call.offer) {
            return;
        }

        await peerConnection.setRemoteDescription({ type: "offer", sdp: call.offer });
        await addPendingIceCandidates();
        const answer = await peerConnection.createAnswer();
        await peerConnection.setLocalDescription(answer);
        await connection.invoke("CallAnswer", call.callerId, answer.sdp);
    } catch {
        endActiveCall(true);
    }
}

window.acceptIncomingCall = function(callerId, callerName) {
    incomingCall = {
        callerId: callerId,
        callerName: callerName,
        callerAvatar: "",
        offer: null,
        pendingIceCandidates: []
    };
    acceptIncomingCall();
};

function rejectIncomingCall() {
    if (incomingCall && connection && connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("CallReject", incomingCall.callerId).catch(() => { });
    }

    dismissIncomingCallBanner();
}

function endActiveCall(notifyRemote = true) {
    const call = activeCall;
    stopCallTimer();
    activeCall = null;

    if (call) {
        if (notifyRemote && connection && connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke("CallEnd", call.remoteUserId).catch(() => { });
        }

        if (window.AndroidBridge) {
            window.AndroidBridge.onCallEnded();
            window.AndroidBridge.disableProximitySensor();
        }

        if (call.peerConnection) {
            call.peerConnection.onicecandidate = null;
            call.peerConnection.ontrack = null;
            call.peerConnection.close();
        }

        if (call.localStream) {
            call.localStream.getTracks().forEach((track) => track.stop());
        }
    }

    if (remoteCallAudio) {
        remoteCallAudio.srcObject = null;
    }

    closeModal(callModal);
    resetCallControls();
}

function toggleCallMute() {
    if (!activeCall || !activeCall.localStream) {
        return;
    }

    activeCall.isMuted = !activeCall.isMuted;
    activeCall.localStream.getAudioTracks().forEach((track) => {
        track.enabled = !activeCall.isMuted;
    });
    updateCallControls();
}

function updateSpeakerButtonState(label, pressed) {
    if (speakerCallButton) {
        speakerCallButton.classList.toggle("is-active", pressed);
        speakerCallButton.setAttribute("aria-pressed", pressed ? "true" : "false");
        speakerCallButton.setAttribute("aria-label", `Change speaker. Current: ${label}`);
        speakerCallButton.title = `Change speaker: ${label}`;
    }

    if (callSpeakerLabel) {
        callSpeakerLabel.textContent = label;
    }

    if (activeCall) {
        activeCall.speakerLabel = label;
        activeCall.speakerPressed = pressed;
    }
}

function updateSpeakerButtonUI(mode) {
    const speakerMode = String(mode || "speaker").toLowerCase();

    if (speakerMode === "earpiece") {
        updateSpeakerButtonState("Earpiece", false);
        return;
    }

    if (speakerMode === "bluetooth") {
        updateSpeakerButtonState("BT", true);
        return;
    }

    updateSpeakerButtonState("Speaker", true);
}

async function cycleBrowserSpeaker() {
    if (!activeCall) {
        return;
    }

    const audioElement = getRemoteCallAudio();

    await navigator.mediaDevices.getUserMedia({ audio: true })
        .then((stream) => { stream.getTracks().forEach((track) => track.stop()); })
        .catch(() => { });

    if (typeof audioElement.setSinkId !== "function") {
        updateSpeakerButtonState("Speaker", true);
        return;
    }

    try {
        const devices = await navigator.mediaDevices.enumerateDevices();
        const audioOutputs = devices.filter((device) => device.kind === "audiooutput");
        const speakerOptions = [
            { deviceId: "", label: "Speaker" },
            ...audioOutputs.map((device, index) => ({
                deviceId: device.deviceId,
                label: device.label || `Speaker ${index + 1}`
            }))
        ];

        browserSpeakerDeviceIndex = (browserSpeakerDeviceIndex + 1) % speakerOptions.length;
        const speaker = speakerOptions[browserSpeakerDeviceIndex];
        await audioElement.setSinkId(speaker.deviceId);
        updateSpeakerButtonState(speaker.label, true);
    } catch {
        updateSpeakerButtonState("Speaker", true);
    }
}

function showAndroidSpeakerPicker() {
    const existing = document.getElementById("speakerPickerModal");
    if (existing) { existing.remove(); return; }

    let devices = [];
    try {
        devices = JSON.parse(window.AndroidBridge.getAvailableAudioDevices());
    } catch { return; }

    const overlay = document.createElement("div");
    overlay.id = "speakerPickerModal";
    overlay.style.cssText = `
        position: fixed; inset: 0; z-index: 9999;
        display: flex; align-items: flex-end; justify-content: center;
        background: rgba(0,0,0,0.5);
    `;

    const sheet = document.createElement("div");
    sheet.style.cssText = `
        background: var(--color-surface, #1e1e2e);
        border-radius: 16px 16px 0 0;
        padding: 16px;
        width: 100%;
        max-width: 480px;
    `;

    devices.forEach(device => {
        const btn = document.createElement("button");
        btn.textContent = device.label;
        btn.style.cssText = `
            display: block; width: 100%;
            padding: 14px 16px; margin-bottom: 8px;
            background: var(--color-surface-variant, #2a2a3e);
            color: var(--color-on-surface, #fff);
            border: none; border-radius: 10px;
            font-size: 15px; text-align: left; cursor: pointer;
        `;
        btn.addEventListener("click", () => {
            window.AndroidBridge.setAudioDevice(device.id);
            updateSpeakerButtonUI(device.id);
            overlay.remove();
        });
        sheet.appendChild(btn);
    });

    overlay.appendChild(sheet);
    overlay.addEventListener("click", (e) => {
        if (e.target === overlay) overlay.remove();
    });
    document.body.appendChild(overlay);
}

function dismissIncomingCallBanner() {
    if (incomingCallBanner) {
        incomingCallBanner.remove();
        incomingCallBanner = null;
    }

    incomingCall = null;
}

function positionIncomingCallBanner() {
    if (!incomingCallBanner) {
        return;
    }

    if (window.matchMedia("(max-width: 640px)").matches) {
        incomingCallBanner.style.inset = "calc(env(safe-area-inset-top, 0px) + 12px) 12px auto 12px";
        incomingCallBanner.style.width = "auto";
        incomingCallBanner.style.transform = "";
        return;
    }

    incomingCallBanner.style.inset = "18px 18px auto auto";
    incomingCallBanner.style.width = "min(360px, calc(100vw - 36px))";
    incomingCallBanner.style.transform = "";
}

function showIncomingCallBanner(callerId, callerName, callerAvatar) {
    const existingCall = incomingCall && sameId(incomingCall.callerId, callerId) ? incomingCall : null;
    dismissIncomingCallBanner();

    incomingCall = {
        callerId,
        callerName,
        callerAvatar,
        offer: existingCall ? existingCall.offer : null,
        pendingIceCandidates: existingCall ? existingCall.pendingIceCandidates : []
    };

    const banner = document.createElement("div");
    banner.setAttribute("role", "dialog");
    banner.setAttribute("aria-label", `Incoming call from ${callerName}`);
    banner.style.position = "fixed";
    banner.style.zIndex = "90";
    banner.style.width = "min(360px, calc(100vw - 36px))";
    banner.style.boxSizing = "border-box";
    banner.style.display = "grid";
    banner.style.gridTemplateColumns = "48px minmax(0, 1fr)";
    banner.style.gap = "12px";
    banner.style.padding = "14px";
    banner.style.border = "1px solid rgba(255, 255, 255, 0.14)";
    banner.style.borderRadius = "12px";
    banner.style.background = "rgba(12, 18, 36, 0.96)";
    banner.style.boxShadow = "0 18px 48px rgba(0, 0, 0, 0.35)";
    banner.style.color = "#fff";

    const avatar = document.createElement("span");
    avatar.className = "avatar";
    setAvatarElement(avatar, {
        profileImageUrl: callerAvatar,
        initial: callerName ? callerName.slice(0, 1).toUpperCase() : "?"
    });

    const content = document.createElement("div");
    content.style.minWidth = "0";

    const title = document.createElement("strong");
    title.textContent = callerName || "Incoming call";
    title.style.display = "block";
    title.style.overflow = "hidden";
    title.style.textOverflow = "ellipsis";
    title.style.whiteSpace = "nowrap";

    const subtitle = document.createElement("span");
    subtitle.textContent = "Incoming audio call";
    subtitle.style.display = "block";
    subtitle.style.marginTop = "2px";
    subtitle.style.color = "rgba(255, 255, 255, 0.72)";
    subtitle.style.fontSize = "0.88rem";

    const actions = document.createElement("div");
    actions.style.display = "flex";
    actions.style.gap = "8px";
    actions.style.marginTop = "12px";

    const acceptButton = document.createElement("button");
    acceptButton.type = "button";
    acceptButton.style.display = "inline-flex";
    acceptButton.style.alignItems = "center";
    acceptButton.style.justifyContent = "center";
    acceptButton.style.gap = "8px";
    acceptButton.style.flex = "1";
    acceptButton.style.border = "0";
    acceptButton.style.borderRadius = "8px";
    acceptButton.style.padding = "10px 12px";
    acceptButton.style.background = "#22c55e";
    acceptButton.style.color = "#04120a";
    acceptButton.style.fontWeight = "700";
    const acceptIcon = document.createElement("img");
    acceptIcon.src = "/Assets/Images/Icons/call.svg";
    acceptIcon.alt = "";
    acceptIcon.style.width = "18px";
    acceptIcon.style.height = "18px";
    acceptIcon.style.filter = "brightness(0)";
    const acceptLabel = document.createElement("span");
    acceptLabel.textContent = "Accept";
    acceptButton.append(acceptIcon, acceptLabel);
    acceptButton.addEventListener("click", acceptIncomingCall);

    const rejectButton = document.createElement("button");
    rejectButton.type = "button";
    rejectButton.style.display = "inline-flex";
    rejectButton.style.alignItems = "center";
    rejectButton.style.justifyContent = "center";
    rejectButton.style.gap = "8px";
    rejectButton.style.flex = "1";
    rejectButton.style.border = "0";
    rejectButton.style.borderRadius = "8px";
    rejectButton.style.padding = "10px 12px";
    rejectButton.style.background = "#ef4444";
    rejectButton.style.color = "#111";
    rejectButton.style.fontWeight = "700";
    const rejectIcon = document.createElement("img");
    rejectIcon.src = "/Assets/Images/Icons/call-end.svg";
    rejectIcon.alt = "";
    rejectIcon.style.width = "18px";
    rejectIcon.style.height = "18px";
    rejectIcon.style.filter = "brightness(0)";
    const rejectLabel = document.createElement("span");
    rejectLabel.textContent = "Reject";
    rejectButton.append(rejectIcon, rejectLabel);
    rejectButton.addEventListener("click", rejectIncomingCall);

    actions.append(acceptButton, rejectButton);
    content.append(title, subtitle, actions);
    banner.append(avatar, content);
    document.body.appendChild(banner);
    incomingCallBanner = banner;
    positionIncomingCallBanner();
}

async function handleCallOffer(sdpOffer, callerId) {
    if (activeCall && sameId(activeCall.remoteUserId, callerId) && activeCall.peerConnection) {
        try {
            await activeCall.peerConnection.setRemoteDescription({ type: "offer", sdp: sdpOffer });
            await addPendingIceCandidates();
            const answer = await activeCall.peerConnection.createAnswer();
            await activeCall.peerConnection.setLocalDescription(answer);
            await connection.invoke("CallAnswer", callerId, answer.sdp);
        } catch {
            endActiveCall(true);
        }
        return;
    }

    if (!incomingCall || !sameId(incomingCall.callerId, callerId)) {
        showIncomingCallBanner(callerId, "Incoming call", null);
    }

    if (incomingCall) {
        incomingCall.offer = sdpOffer;
    }
}

async function handleCallAnswered(sdpAnswer) {
    if (!activeCall || !activeCall.peerConnection) {
        return;
    }

    try {
        await activeCall.peerConnection.setRemoteDescription({ type: "answer", sdp: sdpAnswer });
        await addPendingIceCandidates();
    } catch {
        endActiveCall(true);
    }
}

async function handleIceCandidate(candidate) {
    const iceCandidate = parseIceCandidate(candidate);
    if (!iceCandidate) {
        return;
    }

    if (activeCall && activeCall.peerConnection) {
        if (!activeCall.peerConnection.remoteDescription) {
            activeCall.pendingIceCandidates.push(iceCandidate);
            return;
        }

        await activeCall.peerConnection.addIceCandidate(iceCandidate).catch(() => { });
        return;
    }

    if (incomingCall) {
        incomingCall.pendingIceCandidates.push(iceCandidate);
    }
}

function getFilteredConversations() {
    const term = conversationSearchTerm.toLowerCase();
    const visible = conversations.filter((chat) => !hiddenConversationIds.has(normalizeId(chat.userId)));

    if (!term) {
        return visible;
    }

    return visible.filter((chat) => {
        return chat.username.toLowerCase().includes(term);
    });
}

function applyConversationSearch() {
    conversationSearchTerm = searchInput ? searchInput.value.trim() : "";
    renderChatList(getFilteredConversations());
}

function validateSearchInput(input, message) {
    if (!input) {
        return false;
    }

    if (input.value.trim()) {
        input.setCustomValidity("");
        input.removeAttribute("aria-invalid");
        return true;
    }

    input.setCustomValidity(message);
    input.setAttribute("aria-invalid", "true");
    input.reportValidity();
    input.focus();
    return false;
}

function clearSearchValidation(input) {
    if (!input) {
        return;
    }

    input.setCustomValidity("");
    input.removeAttribute("aria-invalid");
}

function submitConversationSearch() {
    if (!validateSearchInput(searchInput, "Enter a chat name to search.")) {
        return;
    }

    applyConversationSearch();
}

function createChatSwipeShell(chat, button) {
    const shell = document.createElement("div");
    const action = document.createElement("button");
    const actionIcon = document.createElement("img");
    let startX = 0;
    let currentX = 0;
    let dragging = false;
    let moved = false;
    let suppressClick = false;

    shell.className = "chat-swipe-shell";
    action.type = "button";
    action.className = "chat-hide-action";
    action.setAttribute("aria-label", "Remove conversation from list");
    actionIcon.src = "/Assets/Images/Icons/hidden-chats.svg";
    actionIcon.alt = "";
    actionIcon.className = "chat-hide-icon";
    action.appendChild(actionIcon);
    action.addEventListener("click", () => hideConversation(chat.userId));

    const reset = () => {
        shell.classList.remove("is-revealed");
        shell.classList.remove("is-dragging");
        shell.style.removeProperty("--swipe-progress");
        button.style.transform = "";
    };

    button.addEventListener("pointerdown", (event) => {
        if (event.pointerType === "mouse" && event.button !== 0) {
            return;
        }

        startX = event.clientX;
        currentX = 0;
        dragging = true;
        moved = false;
        button.setPointerCapture(event.pointerId);
        button.classList.add("is-swiping");
        shell.classList.add("is-dragging");
    });

    button.addEventListener("pointermove", (event) => {
        if (!dragging) {
            return;
        }

        currentX = Math.min(0, Math.max(-116, event.clientX - startX));
        moved = Math.abs(currentX) > 8;
        if (moved) {
            event.preventDefault();
            shell.style.setProperty("--swipe-progress", Math.min(1, Math.abs(currentX) / 92).toString());
            button.style.transform = `translateX(${currentX}px)`;
        }
    });

    const finish = () => {
        if (!dragging) {
            return;
        }

        dragging = false;
        button.classList.remove("is-swiping");
        shell.classList.remove("is-dragging");

        if (currentX <= -92) {
            hideConversation(chat.userId, shell, button);
            return;
        }

        if (currentX <= -46) {
            shell.classList.add("is-revealed");
            shell.style.setProperty("--swipe-progress", "1");
            button.style.transform = "";
        } else {
            reset();
        }

        if (moved) {
            suppressClick = true;
            window.setTimeout(() => {
                suppressClick = false;
            });
        }
    };

    button.addEventListener("pointerup", finish);
    button.addEventListener("pointercancel", finish);
    button.addEventListener("lostpointercapture", finish);
    button.addEventListener("click", (event) => {
        if (suppressClick) {
            event.preventDefault();
            event.stopImmediatePropagation();
        }
    }, true);

    shell.append(action, button);
    return shell;
}

function hideConversation(userId, shell = null, button = null) {
    const id = normalizeId(userId);
    if (!id) {
        return;
    }

    const completeHide = () => {
        hiddenConversationIds.add(id);
        writeHiddenConversationIds();
        updateRestoreHiddenButton();

        if (activeConversation && sameId(activeConversation.userId, id)) {
            activeConversation = getFilteredConversations()[0] || null;
            if (activeConversation) {
                renderConversationHeader(activeConversation);
                loadMessages(activeConversation.userId);
            } else {
                renderEmptyConversationHeader();
            }
        }

        renderChatList(getFilteredConversations());
    };

    if (shell && button) {
        shell.classList.add("is-hiding");
        shell.style.setProperty("--swipe-progress", "1");
        button.style.transform = "translateX(-118%)";
        window.setTimeout(completeHide, 260);
        return;
    }

    const currentShell = chatList ? [...chatList.querySelectorAll(".chat-swipe-shell")].find((item) => {
        return item.querySelector(".chat-item") && item.querySelector(".chat-item").dataset.userId === id;
    }) : null;

    if (currentShell) {
        const currentButton = currentShell.querySelector(".chat-item");
        if (currentButton) {
            hideConversation(id, currentShell, currentButton);
            return;
        } else {
            currentShell.classList.add("is-hiding");
            window.setTimeout(completeHide, 260);
            return;
        }
    }

    completeHide();
}

function restoreHiddenConversations() {
    hiddenConversationIds.clear();
    writeHiddenConversationIds();
    updateRestoreHiddenButton();
    closeModal(restoreHiddenModal);
    if (!activeConversation && conversations.length > 0) {
        activeConversation = conversations[0];
        renderConversationHeader(activeConversation);
        loadMessages(activeConversation.userId);
    }
    renderChatList(getFilteredConversations());
    renderAllFriends(conversations);
}

function promptDeleteFriend(friend) {
    if (!friend || !deleteFriendModal) {
        return;
    }

    friendPendingDeletion = friend;
    if (deleteFriendCopy) {
        deleteFriendCopy.textContent = `${friend.username} will be removed from your friends and the entire chat history will be permanently deleted.`;
    }

    closeModal(allFriendsModal);
    window.setTimeout(() => openModal(deleteFriendModal), 120);
}

async function removeLocalFriendConversation(friendId) {
    const id = normalizeId(friendId);
    if (!id) {
        return;
    }

    await deleteLocalConversationHistory(id);
    hiddenConversationIds.delete(id);
    writeHiddenConversationIds();
    updateRestoreHiddenButton();
    conversations = conversations.filter((chat) => !sameId(chat.userId, id));

    if (activeCall && sameId(activeCall.remoteUserId, id)) {
        endActiveCall(false);
    }

    if (incomingCall && sameId(incomingCall.callerId, id)) {
        dismissIncomingCallBanner();
    }

    if (activeConversation && sameId(activeConversation.userId, id)) {
        activeConversation = getFilteredConversations()[0] || null;
        if (activeConversation) {
            renderConversationHeader(activeConversation);
            await loadMessages(activeConversation.userId);
        } else {
            renderEmptyConversationHeader();
        }
    }

    renderChatList(getFilteredConversations());
    renderAllFriends(conversations);
}

async function confirmDeleteFriend() {
    if (!friendPendingDeletion || !confirmDeleteFriendButton) {
        return;
    }

    const friend = friendPendingDeletion;
    confirmDeleteFriendButton.disabled = true;
    confirmDeleteFriendButton.classList.add("is-loading");

    const response = await fetch(`/api/friends/${encodeURIComponent(friend.userId)}`, {
        method: "DELETE",
        headers: {
            "RequestVerificationToken": getAntiForgeryToken()
        }
    });

    confirmDeleteFriendButton.disabled = false;
    confirmDeleteFriendButton.classList.remove("is-loading");

    if (!response.ok) {
        const error = await readResponseText(response);
        if (deleteFriendCopy) {
            deleteFriendCopy.textContent = error || "Friend could not be deleted. Try again.";
        }
        return;
    }

    friendPendingDeletion = null;
    closeModal(deleteFriendModal);
    closeModal(allFriendsModal);
    await removeLocalFriendConversation(friend.userId);
}

function updateComposerAction() {
    const submitButton = messageForm ? messageForm.querySelector(".send-button") : null;
    if (!messageInput || !messageSubmitIcon || !submitButton) {
        return;
    }

    const hasText = messageInput.value.trim().length > 0;
    messageSubmitIcon.src = hasText
        ? "/Assets/Images/Icons/send.svg"
        : "/Assets/Images/Icons/voice-message.svg";
    submitButton.setAttribute("aria-label", hasText ? "Send message" : "Send voice message");
    submitButton.setAttribute("title", hasText ? "Send message" : "Send voice message");
}

function renderChatList(items = conversations) {
    if (!chatList) {
        return;
    }

    chatList.innerHTML = "";

    if (items.length === 0) {
        const empty = document.createElement("span");
        empty.className = "chat-copy";
        empty.textContent = "No conversations yet.";
        chatList.appendChild(empty);
        return;
    }

    items.forEach((chat, index) => {
        const button = document.createElement("button");
        const avatar = document.createElement("span");
        const avatarRing = document.createElement("span");
        const copy = document.createElement("span");
        const nameRow = document.createElement("span");
        const name = document.createElement("strong");
        const preview = document.createElement("span");
        const meta = document.createElement("span");
        const time = document.createElement("span");
        const status = document.createElement("span");

        button.type = "button";
        button.className = `chat-item${activeConversation && sameId(chat.userId, activeConversation.userId) ? " is-active" : ""}`;
        button.dataset.userId = normalizeId(chat.userId);
        button.style.animationDelay = `${index * 45}ms`;
        avatar.className = "avatar";
        avatarRing.className = "chat-avatar-ring";
        copy.className = "chat-copy";
        nameRow.className = "chat-name-row";
        meta.className = "chat-meta";
        time.className = "time";
        status.className = `chat-status-dot${chat.isOnline ? " is-online" : ""}`;
        setAvatarElement(avatar, chat, chat.initial);
        name.textContent = chat.username;
        preview.textContent = chat.lastMessage || "Start a conversation.";
        time.textContent = formatTime(chat.lastMessageAt);
        avatarRing.appendChild(avatar);
        nameRow.append(name, status);
        meta.append(time);
        copy.append(nameRow, preview);
        button.append(avatarRing, copy, meta);
        button.addEventListener("click", () => selectConversation(chat.userId));
        chatList.appendChild(createChatSwipeShell(chat, button));
    });
}

function renderConversationHeader(chat) {
    if (!chat || !activeName || !activeStatus || !activeAvatar) {
        return;
    }

    activeName.textContent = chat.username;
    activeStatus.textContent = formatStatus(chat);
    setAvatarElement(activeAvatar, chat, chat.initial);

    if (callButton) {
        callButton.disabled = false;
        callButton.removeAttribute("aria-disabled");
    }
}

function renderEmptyConversationHeader() {
    if (activeName) {
        activeName.textContent = "Elovo";
    }

    if (activeStatus) {
        activeStatus.textContent = "Choose a conversation";
    }

    if (activeAvatar) {
        activeAvatar.innerHTML = "";
        activeAvatar.textContent = "E";
    }

    if (messageStream) {
        messageStream.innerHTML = "";
    }

    if (callButton) {
        callButton.disabled = true;
        callButton.setAttribute("aria-disabled", "true");
        callButton.classList.remove("is-active");
        callButton.setAttribute("aria-pressed", "false");
    }
}

function renderMessages(messages) {
    if (!messageStream) {
        return;
    }

    messageStream.innerHTML = "";

    messages.forEach((message) => {
        appendMessage(message);
    });

    messageStream.scrollTo({
        top: messageStream.scrollHeight,
        behavior: "smooth"
    });
}

function appendMessage(message) {
    const bubble = document.createElement("article");
    const meta = document.createElement("span");
    const time = document.createElement("small");
    const currentUserId = getCurrentUserId();
    const isMine = (message.senderId || "").toLowerCase() === currentUserId;

    bubble.className = `message ${isMine ? "mine" : "them"}${message.id === latestMessageId ? " is-new" : ""}${message.isImage ? " has-image" : ""}${message.isVoice ? " has-voice" : ""}`;
    bubble.dataset.messageId = message.id;
    meta.className = "message-meta";
    time.textContent = formatTime(message.sentAt);
    meta.appendChild(time);

    if (message.isVoice && message.voiceUrl) {
        bubble.appendChild(createVoiceMessage(message));
    } else if (message.isImage && message.imagePath) {
        bubble.appendChild(createImageMessage(message));
    } else {
        bubble.appendChild(document.createTextNode(message.content));
    }

    if (isMine) {
        const status = document.createElement("img");
        const statusText = message.readAt ? "Read" : message.isPending ? "Pending" : "Delivered";
        status.className = `message-status-icon ${message.readAt ? "is-read" : message.isPending ? "is-pending" : "is-delivered"}`;
        status.src = message.readAt
            ? "/Assets/Images/Icons/message-read.svg"
            : "/Assets/Images/Icons/sent-action.svg";
        status.alt = statusText;
        status.title = statusText;
        meta.appendChild(status);
    }

    bubble.appendChild(meta);
    if (isMine) {
        attachMessageActions(bubble, message);
    }
    messageStream.appendChild(bubble);
}

function attachMessageActions(bubble, message) {
    const open = (event) => {
        event.preventDefault();
        showMessageActions(message, bubble);
    };

    bubble.addEventListener("contextmenu", open);
    bubble.addEventListener("pointerdown", (event) => {
        if (event.pointerType === "mouse" && event.button !== 0) {
            return;
        }

        window.clearTimeout(messageActionTimer);
        messageActionTimer = window.setTimeout(() => open(event), 520);
    });
    ["pointerup", "pointercancel", "pointerleave"].forEach((name) => {
        bubble.addEventListener(name, () => window.clearTimeout(messageActionTimer));
    });
}

function closeMessageActions() {
    if (activeMessageActions) {
        activeMessageActions.remove();
        activeMessageActions = null;
    }
}

function createActionButton(icon, text, action) {
    const button = document.createElement("button");
    const image = document.createElement("img");
    const label = document.createElement("span");

    button.type = "button";
    button.className = "message-action-button";
    image.src = icon;
    image.alt = "";
    label.textContent = text;
    button.append(image, label);
    button.addEventListener("click", action);
    return button;
}

function showMessageActions(message, bubble) {
    closeMessageActions();

    const menu = document.createElement("div");
    const canModifyPending = message.isPending === true;
    const rect = bubble.getBoundingClientRect();
    const top = Math.max(12, Math.min(window.innerHeight - 170, Math.max(12, rect.top - 8)));
    const left = Math.min(window.innerWidth - 212, Math.max(12, rect.left));

    menu.className = "message-actions-popover";
    menu.style.top = `${top}px`;
    menu.style.left = `${left}px`;
    menu.addEventListener("pointerdown", (event) => event.stopPropagation());
    menu.appendChild(createActionButton("/Assets/Images/Icons/delete-local.svg", "Delete for me", async () => {
        closeMessageActions();
        await deleteMessageForMe(message);
    }));

    if (canModifyPending) {
        menu.appendChild(createActionButton("/Assets/Images/Icons/delete-all.svg", "Delete for everyone", async () => {
            closeMessageActions();
            await deleteMessageForEveryone(message);
        }));

        if (!message.isImage && !message.isVoice) {
            menu.appendChild(createActionButton("/Assets/Images/Icons/edit-message.svg", "Edit", () => {
                closeMessageActions();
                startPendingMessageEdit(message);
            }));
        }
    }

    document.body.appendChild(menu);
    activeMessageActions = menu;
    window.setTimeout(() => {
        document.addEventListener("pointerdown", closeMessageActions, { once: true });
        document.addEventListener("keydown", closeMessageActions, { once: true });
    });
}

async function deleteMessageForMe(message) {
    if (!activeConversation) {
        return;
    }

    removeStoredMessage(activeConversation.userId, message.id);
    if (message.isImage) {
        await removeCachedImage(message.id);
    }
    if (message.isVoice) {
        await removeCachedVoice(message.id);
    }
    await loadMessages(activeConversation.userId);
    await loadConversations();
}

async function deleteMessageForEveryone(message) {
    if (connection && message.isPending) {
        const deleted = await connection.invoke("DeletePendingMessage", message.id).catch(() => false);
        if (!deleted) {
            updateStoredMessage(activeConversation.userId, message.id, { isPending: false });
        }
    }

    await deleteMessageForMe(message);
}

function startPendingMessageEdit(message) {
    if (!messageInput || !messageForm || !activeConversation || message.isImage || message.isVoice) {
        return;
    }

    messageInput.value = message.content || "";
    messageInput.focus();
    messageForm.dataset.editingMessageId = message.id;
}

function createImageMessage(message) {
    const frame = document.createElement("button");
    const image = document.createElement("img");
    const loader = document.createElement("span");
    let previewPath = message.imagePath;

    frame.type = "button";
    frame.className = "message-image-frame is-loading";
    frame.title = message.imageFileName || "Open image";
    image.src = previewPath;
    image.alt = message.imageFileName || "Sent image";
    image.loading = "lazy";
    loader.className = "image-transfer-loader";

    image.addEventListener("load", () => {
        frame.classList.remove("is-loading");
        frame.classList.remove("is-error");
    });

    image.addEventListener("error", () => {
        frame.classList.remove("is-loading");
        frame.classList.add("is-error");
    });

    readCachedImageBlob(message.id).then((blob) => {
        if (!blob) {
            return;
        }

        previewPath = URL.createObjectURL(blob);
        image.src = previewPath;
    });

    frame.addEventListener("click", () => openImagePreview(previewPath, message.imageFileName));
    frame.append(image, loader);
    return frame;
}

function createVoiceMessage(message) {
    const player = document.createElement("div");
    const playButton = document.createElement("button");
    const icon = document.createElement("img");
    const wave = document.createElement("span");
    const duration = document.createElement("span");
    const audio = document.createElement("audio");
    let objectUrl = null;

    player.className = "message-voice";
    player.style.setProperty("--voice-progress", "0%");
    playButton.type = "button";
    playButton.className = "voice-play-button";
    playButton.setAttribute("aria-label", "Play voice message");
    playButton.setAttribute("title", "Play voice message");
    icon.src = "/Assets/Images/Icons/play-voice.svg";
    icon.alt = "";
    wave.className = "voice-wave";
    duration.className = "voice-duration";
    duration.textContent = formatVoiceDuration(message.voiceDurationSeconds || 0);
    audio.preload = "metadata";
    audio.src = message.voiceUrl;

    createVoiceBars(message.id).forEach((bar) => wave.appendChild(bar));

    readCachedVoiceBlob(message.id).then((blob) => {
        if (!blob) {
            return;
        }

        objectUrl = URL.createObjectURL(blob);
        audio.src = objectUrl;
    });

    audio.addEventListener("loadedmetadata", () => {
        if (!message.voiceDurationSeconds && Number.isFinite(audio.duration)) {
            duration.textContent = formatVoiceDuration(audio.duration);
        }
    });

    audio.addEventListener("play", () => {
        if (activeVoiceAudio && activeVoiceAudio !== audio) {
            activeVoiceAudio.pause();
        }

        activeVoiceAudio = audio;
        player.classList.add("is-playing");
        icon.src = "/Assets/Images/Icons/pause-voice.svg";
        playButton.setAttribute("aria-label", "Pause voice message");
        playButton.setAttribute("title", "Pause voice message");
    });

    const resetPlayState = () => {
        player.classList.remove("is-playing");
        icon.src = "/Assets/Images/Icons/play-voice.svg";
        playButton.setAttribute("aria-label", "Play voice message");
        playButton.setAttribute("title", "Play voice message");
        if (activeVoiceAudio === audio) {
            activeVoiceAudio = null;
        }
    };

    audio.addEventListener("pause", resetPlayState);
    audio.addEventListener("ended", () => {
        audio.currentTime = 0;
        player.style.setProperty("--voice-progress", "0%");
        resetPlayState();
    });

    audio.addEventListener("timeupdate", () => {
        if (!Number.isFinite(audio.duration) || audio.duration <= 0) {
            return;
        }

        player.style.setProperty("--voice-progress", `${Math.min(100, (audio.currentTime / audio.duration) * 100)}%`);
        duration.textContent = formatVoiceDuration(Math.max(0, audio.duration - audio.currentTime));
    });

    player.addEventListener("DOMNodeRemoved", () => {
        if (objectUrl) {
            URL.revokeObjectURL(objectUrl);
        }
    }, { once: true });

    playButton.appendChild(icon);
    playButton.addEventListener("click", () => {
        if (audio.paused) {
            audio.play().catch(() => { });
        } else {
            audio.pause();
        }
    });

    player.append(playButton, wave, duration, audio);
    return player;
}

function createVoiceBars(seed) {
    const value = `${seed || ""}`;
    return Array.from({ length: 18 }, (_, index) => {
        const bar = document.createElement("span");
        const code = value.charCodeAt(index % Math.max(1, value.length)) || 42;
        const height = 8 + ((code + index * 11) % 20);
        bar.style.setProperty("--bar-height", `${height}px`);
        bar.style.animationDelay = `${index * 42}ms`;
        return bar;
    });
}

function formatVoiceDuration(seconds) {
    const value = Math.max(0, Math.round(Number(seconds) || 0));
    const minutes = Math.floor(value / 60);
    const remainder = `${value % 60}`.padStart(2, "0");
    return `${minutes}:${remainder}`;
}

function openImagePreview(path, fileName) {
    const backdrop = document.createElement("div");
    const image = document.createElement("img");
    const close = document.createElement("button");
    const pointers = new Map();
    let previewScale = 1;
    let translateX = 0;
    let translateY = 0;
    let startScale = 1;
    let startTranslateX = 0;
    let startTranslateY = 0;
    let startX = 0;
    let startY = 0;
    let startDistance = 0;
    let moved = false;

    backdrop.className = "image-preview-backdrop is-open";
    backdrop.setAttribute("role", "dialog");
    backdrop.setAttribute("aria-modal", "true");
    image.src = path;
    image.alt = fileName || "Image preview";
    image.draggable = false;
    close.type = "button";
    close.className = "image-preview-close";
    close.setAttribute("aria-label", "Close");
    close.textContent = "Г—";

    close.title = "Close";

    const closeIcon = document.createElement("img");
    closeIcon.src = "/Assets/Images/Icons/close.svg";
    closeIcon.alt = "";
    close.replaceChildren(closeIcon);

    const closePreview = () => {
        document.removeEventListener("keydown", onKeyDown);
        backdrop.remove();
    };

    const onKeyDown = (event) => {
        if (event.key === "Escape") {
            closePreview();
        }
    };

    close.addEventListener("click", closePreview);

    const clamp = (value, min, max) => Math.min(max, Math.max(min, value));
    const getDistance = () => {
        const values = [...pointers.values()];
        if (values.length < 2) {
            return 0;
        }

        return Math.hypot(values[0].clientX - values[1].clientX, values[0].clientY - values[1].clientY);
    };
    const getCenter = () => {
        const values = [...pointers.values()];
        const total = values.reduce((point, item) => {
            point.x += item.clientX;
            point.y += item.clientY;
            return point;
        }, { x: 0, y: 0 });

        return {
            x: total.x / values.length,
            y: total.y / values.length
        };
    };
    const clampTranslate = () => {
        const maxX = Math.max(0, ((previewScale - 1) * image.offsetWidth) / 2);
        const maxY = Math.max(0, ((previewScale - 1) * image.offsetHeight) / 2);
        translateX = clamp(translateX, -maxX, maxX);
        translateY = clamp(translateY, -maxY, maxY);
    };
    const applyTransform = () => {
        previewScale = clamp(previewScale, 1, 3);
        if (previewScale === 1) {
            translateX = 0;
            translateY = 0;
            image.classList.remove("is-zoomed");
            image.style.transform = "";
            return;
        }

        clampTranslate();
        image.classList.add("is-zoomed");
        image.style.transform = `translate(${translateX}px, ${translateY}px) scale(${previewScale})`;
    };
    const beginPointerGesture = () => {
        startScale = previewScale;
        startTranslateX = translateX;
        startTranslateY = translateY;

        if (pointers.size === 1) {
            const point = [...pointers.values()][0];
            startX = point.clientX;
            startY = point.clientY;
            return;
        }

        const center = getCenter();
        startX = center.x;
        startY = center.y;
        startDistance = getDistance();
    };

    image.addEventListener("click", (event) => {
        event.stopPropagation();
        if (moved) {
            moved = false;
            return;
        }

        const rect = image.getBoundingClientRect();
        image.style.transformOrigin = `${clamp(((event.clientX - rect.left) / rect.width) * 100, 0, 100)}% ${clamp(((event.clientY - rect.top) / rect.height) * 100, 0, 100)}%`;
        previewScale = previewScale > 1 ? 1 : 2;
        applyTransform();
    });
    image.addEventListener("pointerdown", (event) => {
        pointers.set(event.pointerId, event);
        image.setPointerCapture(event.pointerId);
        beginPointerGesture();
    });
    image.addEventListener("pointermove", (event) => {
        if (!pointers.has(event.pointerId)) {
            return;
        }

        pointers.set(event.pointerId, event);

        if (pointers.size === 1 && previewScale > 1) {
            translateX = startTranslateX + event.clientX - startX;
            translateY = startTranslateY + event.clientY - startY;
            moved = Math.abs(event.clientX - startX) > 4 || Math.abs(event.clientY - startY) > 4;
            applyTransform();
            return;
        }

        if (pointers.size >= 2 && startDistance > 0) {
            const center = getCenter();
            previewScale = startScale * (getDistance() / startDistance);
            translateX = startTranslateX + center.x - startX;
            translateY = startTranslateY + center.y - startY;
            moved = true;
            applyTransform();
        }
    });
    const endPointerGesture = (event) => {
        pointers.delete(event.pointerId);
        if (pointers.size > 0) {
            beginPointerGesture();
        }
    };

    image.addEventListener("pointerup", endPointerGesture);
    image.addEventListener("pointercancel", endPointerGesture);
    image.addEventListener("lostpointercapture", endPointerGesture);
    image.addEventListener("wheel", (event) => {
        event.preventDefault();
        previewScale += event.deltaY < 0 ? 0.18 : -0.18;
        applyTransform();
    });
    backdrop.addEventListener("click", (event) => {
        if (event.target === backdrop) {
            closePreview();
        }
    });
    document.addEventListener("keydown", onKeyDown);

    backdrop.append(image, close);
    document.body.appendChild(backdrop);
}

async function loadConversations() {
    const response = await fetch("/api/conversations", {
        headers: { "Accept": "application/json" }
    });

    if (!response.ok) {
        navigateWithLoader("/auth/login");
        return;
    }

    conversations = applyLocalConversationHistory(await response.json());
    hiddenConversationIds = readHiddenConversationIds();
    await purgeRemovedLocalConversations(conversations.map((chat) => chat.userId));
    updateRestoreHiddenButton();
    const visibleConversations = getFilteredConversations();

    if (activeConversation && hiddenConversationIds.has(normalizeId(activeConversation.userId))) {
        activeConversation = null;
    }

    if (activeConversation) {
        activeConversation = conversations.find(x => sameId(x.userId, activeConversation.userId)) || null;
    }

    if (!activeConversation && visibleConversations.length > 0) {
        activeConversation = visibleConversations[0];
    }

    renderChatList(visibleConversations);

    if (activeConversation) {
        renderConversationHeader(activeConversation);
    } else {
        renderEmptyConversationHeader();
    }
}

function renderUsers(items) {
    if (!userSearchResults) {
        return;
    }

    userSearchResults.innerHTML = "";

    if (items.length === 0) {
        const empty = document.createElement("span");
        empty.className = "modal-empty";
        empty.textContent = "No users found.";
        userSearchResults.appendChild(empty);
        return;
    }

    items.forEach((user) => {
        const row = document.createElement("div");
        const avatar = document.createElement("span");
        const copy = document.createElement("span");
        const name = document.createElement("strong");
        const status = document.createElement("span");
        const button = document.createElement("button");

        row.className = "user-row";
        avatar.className = "avatar";
        copy.className = "chat-copy";
        button.type = "button";
        button.className = `row-action${user.status === "none" ? " primary" : ""}`;

        setAvatarElement(avatar, user, user.initial);
        name.textContent = user.username;
        status.textContent = formatCandidateStatus(user.status);
        const icon = document.createElement("img");
        const label = document.createElement("span");

        icon.className = "action-icon";
        icon.src = user.status === "none"
            ? "/Assets/Images/Icons/add-action.svg"
            : "/Assets/Images/Icons/sent-action.svg";
        icon.alt = "";
        label.textContent = formatCandidateAction(user.status);
        button.append(icon, label);
        button.disabled = user.status !== "none";

        if (user.status === "none") {
            button.addEventListener("click", () => sendFriendRequest(user.id, button, status));
        }

        copy.append(name, status);
        row.append(avatar, copy, button);
        userSearchResults.appendChild(row);
    });
}

function renderAllFriends(items) {
    if (!allFriendsList) {
        return;
    }

    allFriendsList.innerHTML = "";

    if (items.length === 0) {
        const empty = document.createElement("span");
        empty.className = "modal-empty";
        empty.textContent = "No friends yet.";
        allFriendsList.appendChild(empty);
        return;
    }

    items.forEach((friend) => {
        const row = document.createElement("div");
        const avatar = document.createElement("span");
        const copy = document.createElement("span");
        const name = document.createElement("strong");
        const status = document.createElement("span");
        const actions = document.createElement("span");
        const button = document.createElement("button");
        const deleteButton = document.createElement("button");
        const deleteIcon = document.createElement("img");

        row.className = "user-row";
        avatar.className = "avatar";
        copy.className = "chat-copy";
        actions.className = "user-row-actions";
        button.type = "button";
        button.className = "row-action primary";
        deleteButton.type = "button";
        deleteButton.className = "row-action danger";
        deleteButton.setAttribute("aria-label", `Delete ${friend.username}`);
        deleteButton.title = `Delete ${friend.username}`;
        deleteIcon.className = "action-icon";
        deleteIcon.src = "/Assets/Images/Icons/remove-friend.svg";
        deleteIcon.alt = "";

        setAvatarElement(avatar, friend, friend.initial);
        name.textContent = friend.username;
        status.textContent = formatStatus(friend);
        button.textContent = "Open";
        button.addEventListener("click", () => {
            closeModal(allFriendsModal);
            selectConversation(friend.userId);
        });
        deleteButton.appendChild(deleteIcon);
        deleteButton.addEventListener("click", () => promptDeleteFriend(friend));

        copy.append(name, status);
        actions.append(button, deleteButton);
        row.append(avatar, copy, actions);
        allFriendsList.appendChild(row);
    });
}

async function openAllFriends() {
    openModal(allFriendsModal);
    if (conversations.length === 0) {
        await loadConversations();
    }
    renderAllFriends(conversations);
}

function formatCandidateStatus(status) {
    if (status === "friend") {
        return "Already friends";
    }

    if (status === "sent") {
        return "Request sent";
    }

    if (status === "incoming") {
        return "Sent you a request";
    }

    return "Not in friends";
}

function formatCandidateAction(status) {
    if (status === "friend") {
        return "Friend";
    }

    if (status === "sent") {
        return "Sent";
    }

    if (status === "incoming") {
        return "Open requests";
    }

    return "Add";
}

async function searchUsers(validateEmpty = false) {
    if (!userSearchInput || !userSearchResults) {
        return;
    }

    const term = userSearchInput.value.trim();
    if (!term) {
        if (validateEmpty) {
            validateSearchInput(userSearchInput, "Enter a username to search.");
        }

        userSearchResults.innerHTML = "";
        const empty = document.createElement("span");
        empty.className = "modal-empty";
        empty.textContent = "Enter a username.";
        userSearchResults.appendChild(empty);
        return;
    }

    const response = await fetch(`/api/users?query=${encodeURIComponent(term)}`, {
        headers: { "Accept": "application/json" }
    });

    if (response.ok) {
        renderUsers(await response.json());
    }
}

async function sendFriendRequest(receiverId, button, status) {
    button.disabled = true;
    setActionButtonLabel(button, "Sending", "/Assets/Images/Icons/sent-action.svg");

    const response = await fetch("/api/friend-requests", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": getAntiForgeryToken()
        },
        body: JSON.stringify({ receiverId })
    });

    if (response.ok) {
        setActionButtonLabel(button, "Sent", "/Assets/Images/Icons/sent-action.svg");
        button.classList.remove("primary");
        status.textContent = "Request sent";
    } else {
        button.disabled = false;
        setActionButtonLabel(button, "Add", "/Assets/Images/Icons/add-action.svg");
    }
}

function setActionButtonLabel(button, text, iconSrc) {
    const icon = button.querySelector(".action-icon");
    const label = button.querySelector("span");

    if (icon) {
        icon.src = iconSrc;
    }

    if (label) {
        label.textContent = text;
        return;
    }

    button.textContent = text;
}

function renderFriendRequests(items) {
    if (!friendRequestsList) {
        return;
    }

    friendRequestsList.innerHTML = "";

    if (items.length === 0) {
        const empty = document.createElement("span");
        empty.className = "modal-empty";
        empty.textContent = "No friend requests.";
        friendRequestsList.appendChild(empty);
        return;
    }

    items.forEach((request) => {
        const row = document.createElement("div");
        const avatar = document.createElement("span");
        const copy = document.createElement("span");
        const name = document.createElement("strong");
        const date = document.createElement("span");
        const button = document.createElement("button");

        row.className = "user-row";
        avatar.className = "avatar";
        copy.className = "chat-copy";
        button.type = "button";
        button.className = "row-action primary";

        setAvatarElement(avatar, request, request.initial);
        name.textContent = request.senderUsername;
        date.textContent = formatTime(request.createdAt);
        button.textContent = "Accept";
        button.addEventListener("click", () => acceptFriendRequest(request.id, button));

        copy.append(name, date);
        row.append(avatar, copy, button);
        friendRequestsList.appendChild(row);
    });
}

async function loadFriendRequests() {
    const response = await fetch("/api/friend-requests", {
        headers: { "Accept": "application/json" }
    });

    if (response.ok) {
        renderFriendRequests(await response.json());
    }
}

async function acceptFriendRequest(requestId, button) {
    button.disabled = true;
    button.textContent = "Adding";

    const response = await fetch(`/api/friend-requests/${requestId}/accept`, {
        method: "POST",
        headers: {
            "RequestVerificationToken": getAntiForgeryToken()
        }
    });

    if (response.ok) {
        await loadFriendRequests();
        await loadConversations();
    } else {
        button.disabled = false;
        button.textContent = "Accept";
    }
}

async function loadMessages(userId) {
    renderMessages(readStoredMessages(userId));
}

async function selectConversation(userId) {
    activeConversation = conversations.find(x => sameId(x.userId, userId));
    latestMessageId = "";

    if (!activeConversation) {
        return;
    }

    renderChatList(getFilteredConversations());
    renderConversationHeader(activeConversation);
    await loadMessages(activeConversation.userId);

    if (messengerView) {
        messengerView.classList.add("chat-open");
    }

    notifyActiveConversationRead();
}

async function startSignalR() {
    if (!messengerView || !window.signalR) {
        if (activeStatus) {
            activeStatus.textContent = "Realtime client is unavailable";
        }
        return;
    }

    connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect()
        .withServerTimeout(25000)
        .withKeepAliveInterval(10000)
        .build();

    connection.on("ReceiveMessage", async (message) => {
        latestMessageId = message.id;
        const messageStored = storeMessage(message);
        if (messageStored && !message.isImage && !message.isVoice) {
            acknowledgePendingMessage(message);
        }
        const mediaCachePromise = message.isVoice
            ? cacheVoiceForMessage(message)
            : cacheImageForMessage(message);

        if (messageBelongsToActiveConversation(message)) {
            await loadMessages(activeConversation.userId);
            if (!sameId(message.senderId, getCurrentUserId())) {
                notifyActiveConversationRead();
            }
            messageStream.scrollTo({
                top: messageStream.scrollHeight,
                behavior: "smooth"
            });
        }

        await loadConversations();

        const mediaCached = await mediaCachePromise;
        if (messageStored && (message.isImage || message.isVoice) && mediaCached) {
            acknowledgePendingMessage(message);
        }

        if (mediaCached && messageBelongsToActiveConversation(message)) {
            await loadMessages(activeConversation.userId);
        }
    });

    connection.on("UserOnline", (userId, lastSeenAt) => {
        updateUserStatus(userId, true, lastSeenAt);
    });

    connection.on("UserOffline", (userId, lastSeenAt) => {
        updateUserStatus(userId, false, lastSeenAt);
    });

    connection.on("UserTyping", (userId) => {
        if (activeConversation && sameId(userId, activeConversation.userId)) {
            activeStatus.textContent = "Typing...";
        }
    });

    connection.on("UserStopTyping", (userId) => {
        if (activeConversation && sameId(userId, activeConversation.userId)) {
            activeStatus.textContent = formatStatus(activeConversation);
        }
    });

    connection.on("MessagesRead", async (readerId, readAt) => {
        if (!markOutgoingMessagesRead(readerId, readAt)) {
            return;
        }

        await refreshConversationAfterMessageChange(readerId);
    });

    connection.on("MessagesDelivered", async (receiverId, messageIds, deliveredAt) => {
        if (!markOutgoingMessagesDelivered(receiverId, messageIds, deliveredAt)) {
            return;
        }

        await refreshConversationAfterMessageChange(receiverId);
    });

    connection.on("PendingMessageDeleted", async (receiverId, messageId) => {
        if (!removeStoredMessage(receiverId, messageId)) {
            return;
        }

        await refreshConversationAfterMessageChange(receiverId);
    });

    connection.on("PendingMessageEdited", async (receiverId, messageId, content) => {
        if (!updateStoredMessage(receiverId, messageId, { content })) {
            return;
        }

        await refreshConversationAfterMessageChange(receiverId);
    });

    connection.on("IncomingCall", (callerId, callerName, callerAvatar) => {
        if (activeCall) {
            connection.invoke("CallReject", callerId).catch(() => { });
            return;
        }

        showIncomingCallBanner(callerId, callerName, callerAvatar);
    });

    connection.on("CallOffer", async (sdpOffer, callerId) => {
        await handleCallOffer(sdpOffer, callerId);
    });

    connection.on("CallAnswered", async (sdpAnswer) => {
        await handleCallAnswered(sdpAnswer);
    });

    connection.on("IceCandidate", async (candidate) => {
        await handleIceCandidate(candidate);
    });

    connection.on("CallRejected", () => {
        dismissIncomingCallBanner();
        endActiveCall(false);
    });

    connection.on("CallEnded", () => {
        dismissIncomingCallBanner();
        endActiveCall(false);
    });

    connection.on("FriendRemoved", async (friendId) => {
        await removeLocalFriendConversation(friendId);
    });

    await connection.start();
    notifyActiveConversationRead();
}

async function stopSignalRForExit() {
    isLeavingChatPage = true;

    if (!connection || connection.state === signalR.HubConnectionState.Disconnected) {
        return;
    }

    await connection.stop().catch(() => { });
}

function messageBelongsToActiveConversation(message) {
    return activeConversation &&
        (sameId(message.senderId, activeConversation.userId) ||
            sameId(message.receiverId, activeConversation.userId));
}

function updateUserStatus(userId, isOnline, lastSeenAt = null) {
    const chat = conversations.find(x => sameId(x.userId, userId));
    if (!chat) {
        return;
    }

    chat.isOnline = isOnline;
    chat.lastSeenAt = isOnline ? null : lastSeenAt;

    if (activeConversation && sameId(activeConversation.userId, userId)) {
        activeConversation = chat;
        renderConversationHeader(chat);
    }

    renderChatList(getFilteredConversations());
}

async function sendCurrentMessage(event) {
    event.preventDefault();

    if (!activeConversation || !connection || isSending) {
        return;
    }

    const text = messageInput.value.trim();
    if (!text) {
        return;
    }

    const editingMessageId = messageForm.dataset.editingMessageId;
    if (editingMessageId) {
        isSending = true;
        messageInput.disabled = true;
        messageForm.classList.add("is-sending");

        try {
            const edited = await connection.invoke("EditPendingMessage", editingMessageId, text).catch(() => false);
            if (edited) {
                updateStoredMessage(activeConversation.userId, editingMessageId, { content: text });
            } else {
                updateStoredMessage(activeConversation.userId, editingMessageId, { isPending: false });
            }
            messageInput.value = "";
            updateComposerAction();
            delete messageForm.dataset.editingMessageId;
            await loadMessages(activeConversation.userId);
            await loadConversations();
        } finally {
            messageForm.classList.remove("is-sending");
            messageInput.disabled = false;
            messageInput.focus();
            isSending = false;
        }

        return;
    }

    isSending = true;
    messageInput.value = "";
    updateComposerAction();
    messageInput.disabled = true;
    messageForm.classList.add("is-sending");

    try {
        await connection.invoke("SendMessage", activeConversation.userId, text);
    } finally {
        messageForm.classList.remove("is-sending");
        messageInput.disabled = false;
        messageInput.focus();
        isSending = false;
    }
}

function setImageTransferState(active, progress = 0) {
    if (!messageForm) {
        return;
    }

    messageForm.classList.toggle("is-image-transfer", active);
    messageForm.style.setProperty("--image-progress", `${Math.max(0, Math.min(100, progress))}%`);
    updateImageTransferStatus(active, progress);

    if (attachImageButton) {
        attachImageButton.disabled = active;
    }

    if (messageInput) {
        messageInput.disabled = active || isSending;
    }
}

function updateImageTransferStatus(active, progress = 0, loaded = 0, total = 0) {
    if (!messageForm) {
        return;
    }

    if (!imageTransferStatus) {
        imageTransferStatus = document.createElement("span");
        imageTransferStatus.className = "image-upload-status";
        messageForm.appendChild(imageTransferStatus);
    }

    if (!active) {
        imageTransferStatus.remove();
        imageTransferStatus = null;
        return;
    }

    const percent = Math.max(0, Math.min(100, Math.round(progress)));
    const remaining = total > 0 ? Math.max(0, total - loaded) : 0;
    const remainingText = total > 0 ? `, ${formatBytes(remaining)} left` : "";
    imageTransferStatus.innerHTML = `<img src="/Assets/Images/Icons/image-upload-loader.svg" alt=""> <span>${percent}%${remainingText}</span>`;
}

function formatBytes(value) {
    if (!value) {
        return "0 KB";
    }

    const units = ["B", "KB", "MB"];
    let size = value;
    let unit = 0;
    while (size >= 1024 && unit < units.length - 1) {
        size /= 1024;
        unit += 1;
    }

    return `${size >= 10 || unit === 0 ? Math.round(size) : size.toFixed(1)} ${units[unit]}`;
}

function uploadImage(file) {
    return new Promise((resolve, reject) => {
        const data = new FormData();
        const request = new XMLHttpRequest();

        data.append("image", file);
        request.open("POST", "/api/messages/images");
        request.setRequestHeader("RequestVerificationToken", getAntiForgeryToken());
        request.responseType = "json";

        request.upload.addEventListener("progress", (event) => {
            if (event.lengthComputable) {
                const progress = Math.round((event.loaded / event.total) * 100);
                setImageTransferState(true, progress);
                updateImageTransferStatus(true, progress, event.loaded, event.total);
            }
        });

        request.addEventListener("load", () => {
            if (request.status >= 200 && request.status < 300) {
                resolve(request.response);
                return;
            }

            reject(new Error("Image upload failed."));
        });

        request.addEventListener("error", () => reject(new Error("Image upload failed.")));
        request.addEventListener("abort", () => reject(new Error("Image upload was cancelled.")));
        request.send(data);
    });
}

async function sendSelectedImage() {
    if (!activeConversation || !connection || !imageInput || isSending) {
        return;
    }

    const file = imageInput.files && imageInput.files[0];
    imageInput.value = "";

    if (!file) {
        return;
    }

    if (!allowedImageTypes.includes(file.type) || file.size > maxImageSize) {
        setImageTransferState(false);
        return;
    }

    isSending = true;
    setImageTransferState(true, 0);

    try {
        const image = await uploadImage(file);
        const imageStorageKey = getImageStorageKey(image.path);
        if (imageStorageKey) {
            pendingUploadedImages.set(imageStorageKey, file);
        }

        setImageTransferState(true, 100);
        try {
            await connection.invoke("SendImageMessage", activeConversation.userId, image.path, image.fileName || file.name);
        } catch (error) {
            if (imageStorageKey) {
                pendingUploadedImages.delete(imageStorageKey);
            }
            throw error;
        }
    } finally {
        isSending = false;
        setImageTransferState(false);
        if (messageInput) {
            messageInput.focus();
        }
    }
}

function getSupportedVoiceMimeType() {
    const candidates = [
        "audio/webm;codecs=opus",
        "audio/webm",
        "audio/ogg;codecs=opus",
        "audio/mp4"
    ];

    if (!window.MediaRecorder || !MediaRecorder.isTypeSupported) {
        return "";
    }

    return candidates.find((type) => MediaRecorder.isTypeSupported(type)) || "";
}

function setVoiceRecordingState(active) {
    if (!messageForm) {
        return;
    }

    messageForm.classList.toggle("is-voice-recording", active);
    if (messageInput) {
        messageInput.disabled = active || isSending;
        messageInput.placeholder = active ? "Recording voice message" : "Write a message";
    }

    if (!active) {
        window.clearInterval(voiceRecordTimer);
        window.clearTimeout(voiceAutoStopTimer);
        if (voiceTransferStatus) {
            voiceTransferStatus.remove();
            voiceTransferStatus = null;
        }
        return;
    }

    if (!voiceTransferStatus) {
        voiceTransferStatus = document.createElement("span");
        voiceTransferStatus.className = "voice-recording-status";
        voiceTransferStatus.innerHTML = `
            <span class="voice-recording-dot"></span>
            <span class="voice-recording-copy">0:00</span>
            <span class="voice-recording-meter">${createRecordingMeterMarkup()}</span>`;
        messageForm.appendChild(voiceTransferStatus);
    }

    updateVoiceRecordingTimer();
    voiceRecordTimer = window.setInterval(updateVoiceRecordingTimer, 200);
}

function createRecordingMeterMarkup() {
    return Array.from({ length: 12 }, (_, index) => `<i style="--meter-delay:${index * 54}ms"></i>`).join("");
}

function updateVoiceRecordingTimer() {
    if (!voiceTransferStatus || !voiceRecordStartedAt) {
        return;
    }

    const elapsed = Math.min(maxVoiceDurationMs, Date.now() - voiceRecordStartedAt);
    const timer = voiceTransferStatus.querySelector(".voice-recording-copy");
    if (timer) {
        timer.textContent = `${formatVoiceDuration(elapsed / 1000)} / 1:00`;
    }

    messageForm.style.setProperty("--voice-record-progress", `${(elapsed / maxVoiceDurationMs) * 100}%`);
}

function canStartVoiceRecording() {
    return activeConversation &&
        connection &&
        !isSending &&
        !isRecordingVoice &&
        !isPreparingVoice &&
        messageInput &&
        messageInput.value.trim().length === 0 &&
        navigator.mediaDevices &&
        navigator.mediaDevices.getUserMedia &&
        window.MediaRecorder;
}

async function startVoiceRecording(event) {
    if (!canStartVoiceRecording()) {
        return;
    }

    event.preventDefault();
    shouldStopVoiceWhenReady = false;
    isPreparingVoice = true;

    try {
        const mimeType = getSupportedVoiceMimeType();
        voiceStream = await navigator.mediaDevices.getUserMedia({
            audio: {
                echoCancellation: true,
                noiseSuppression: true,
                autoGainControl: true
            }
        });

        const options = {
            audioBitsPerSecond: voiceAudioBitRate
        };
        if (mimeType) {
            options.mimeType = mimeType;
        }

        voiceChunks = [];
        voiceRecorder = new MediaRecorder(voiceStream, options);
        voiceRecorder.addEventListener("dataavailable", (dataEvent) => {
            if (dataEvent.data && dataEvent.data.size > 0) {
                voiceChunks.push(dataEvent.data);
            }
        });
        voiceRecorder.addEventListener("stop", sendRecordedVoice);
        voiceRecordStartedAt = Date.now();
        voiceRecorder.start();
        isRecordingVoice = true;
        setVoiceRecordingState(true);
        voiceAutoStopTimer = window.setTimeout(() => stopVoiceRecording(true), maxVoiceDurationMs);

        if (shouldStopVoiceWhenReady) {
            stopVoiceRecording(true);
        }
    } catch {
        resetVoiceRecording();
    } finally {
        isPreparingVoice = false;
    }
}

function stopVoiceRecording(shouldSend) {
    shouldStopVoiceWhenReady = isPreparingVoice && shouldSend;
    if (!voiceRecorder || voiceRecorder.state === "inactive") {
        return;
    }

    voiceRecorder.datasetShouldSend = shouldSend ? "true" : "false";
    voiceRecorder.stop();
}

function resetVoiceRecording() {
    setVoiceRecordingState(false);
    if (voiceStream) {
        voiceStream.getTracks().forEach((track) => track.stop());
    }

    voiceRecorder = null;
    voiceStream = null;
    voiceChunks = [];
    voiceRecordStartedAt = 0;
    isRecordingVoice = false;
    isPreparingVoice = false;
    shouldStopVoiceWhenReady = false;
    if (messageInput) {
        messageInput.disabled = isSending;
        messageInput.placeholder = "Write a message";
    }
    if (messageForm) {
        messageForm.style.removeProperty("--voice-record-progress");
    }
}

async function sendRecordedVoice() {
    const recorder = voiceRecorder;
    const shouldSend = recorder && recorder.datasetShouldSend !== "false";
    const durationMs = voiceRecordStartedAt ? Date.now() - voiceRecordStartedAt : 0;
    const type = recorder && recorder.mimeType ? recorder.mimeType : getSupportedVoiceMimeType() || "audio/webm";
    const chunks = voiceChunks.slice();

    resetVoiceRecording();

    if (!shouldSend || durationMs < minVoiceDurationMs || chunks.length === 0) {
        return;
    }

    const blob = new Blob(chunks, { type });
    await sendVoiceBlob(blob, Math.min(durationMs / 1000, 60));
}

function uploadVoice(blob) {
    return new Promise((resolve, reject) => {
        const data = new FormData();
        const request = new XMLHttpRequest();

        data.append("voice", blob, "voice-message");
        request.open("POST", "/api/messages/voice");
        request.setRequestHeader("RequestVerificationToken", getAntiForgeryToken());
        request.responseType = "json";

        request.upload.addEventListener("progress", (event) => {
            if (event.lengthComputable) {
                const progress = Math.round((event.loaded / event.total) * 100);
                setVoiceSendingState(true, progress);
            }
        });

        request.addEventListener("load", () => {
            if (request.status >= 200 && request.status < 300) {
                resolve(request.response);
                return;
            }

            reject(new Error("Voice upload failed."));
        });

        request.addEventListener("error", () => reject(new Error("Voice upload failed.")));
        request.addEventListener("abort", () => reject(new Error("Voice upload was cancelled.")));
        request.send(data);
    });
}

function setVoiceSendingState(active, progress = 0) {
    if (!messageForm) {
        return;
    }

    messageForm.classList.toggle("is-voice-sending", active);
    messageForm.classList.toggle("is-image-transfer", active);
    messageForm.style.setProperty("--voice-progress", `${Math.max(0, Math.min(100, progress))}%`);
    messageForm.style.setProperty("--image-progress", `${Math.max(0, Math.min(100, progress))}%`);

    if (!voiceTransferStatus && active) {
        voiceTransferStatus = document.createElement("span");
        voiceTransferStatus.className = "image-upload-status voice-upload-status";
        messageForm.appendChild(voiceTransferStatus);
    }

    if (!active) {
        if (voiceTransferStatus) {
            voiceTransferStatus.remove();
            voiceTransferStatus = null;
        }
        return;
    }

    voiceTransferStatus.innerHTML = `<img src="/Assets/Images/Icons/image-upload-loader.svg" alt=""> <span>${Math.round(progress)}%</span>`;
}

async function sendVoiceBlob(blob, durationSeconds) {
    if (!activeConversation || !connection || isSending) {
        return;
    }

    if (!allowedVoiceTypes.some((type) => blob.type.startsWith(type))) {
        return;
    }

    isSending = true;
    setVoiceSendingState(true, 0);

    try {
        const voice = await uploadVoice(blob);
        const voiceStorageKey = getVoiceStorageKey(voice.url || voice.path);
        if (voiceStorageKey) {
            pendingUploadedVoices.set(voiceStorageKey, blob);
        }

        setVoiceSendingState(true, 100);
        try {
            await connection.invoke("SendVoiceMessage", activeConversation.userId, voice.path, durationSeconds);
        } catch (error) {
            if (voiceStorageKey) {
                pendingUploadedVoices.delete(voiceStorageKey);
            }
            throw error;
        }
    } finally {
        isSending = false;
        setVoiceSendingState(false);
        if (messageInput) {
            messageInput.focus();
        }
    }
}
async function logout() {
    showPageLoader();
    await stopSignalRForExit();

    await fetch("/auth/logout", {
        method: "POST",
        headers: {
            "RequestVerificationToken": getAntiForgeryToken()
        }
    });

    navigateWithLoader("/auth/login");
}

window.addEventListener("pagehide", () => {
    if (!connection || isLeavingChatPage) {
        return;
    }

    stopSignalRForExit();
});

window.addEventListener("resize", positionIncomingCallBanner);

if (messengerView && chatList && messageStream) {
    requestPersistentStorage();
    purgeExpiredChatMessages()
        .then(loadConversations)
        .then(() => activeConversation ? loadMessages(activeConversation.userId) : null)
        .then(startSignalR)
        .catch(() => {
            navigateWithLoader("/auth/login");
        });
}

if (logoutButton) {
    logoutButton.addEventListener("click", logout);
}

if (settingsButton) {
    settingsButton.addEventListener("click", () => navigateWithLoader("/settings/profile"));
}

if (callButton) {
    callButton.addEventListener("click", startOutgoingCall);
}

if (endCallButton) {
    endCallButton.addEventListener("click", () => endActiveCall(true));
}

if (muteCallButton) {
    muteCallButton.addEventListener("click", toggleCallMute);
}

if (speakerCallButton) {
    speakerCallButton.addEventListener("click", () => {
        if (window.AndroidBridge) {
            showAndroidSpeakerPicker();
        } else {
            cycleBrowserSpeaker();
        }
    });
}

if (searchInput) {
    searchInput.addEventListener("input", () => {
        clearSearchValidation(searchInput);

        if (!searchInput.value.trim()) {
            applyConversationSearch();
        }
    });

    searchInput.addEventListener("keydown", (event) => {
        if (event.key === "Enter") {
            event.preventDefault();
            submitConversationSearch();
        }
    });
}

if (chatSearchButton) {
    chatSearchButton.addEventListener("click", submitConversationSearch);
}

if (restoreHiddenButton) {
    restoreHiddenButton.addEventListener("click", () => openModal(restoreHiddenModal));
}

if (confirmRestoreHiddenButton) {
    confirmRestoreHiddenButton.addEventListener("click", restoreHiddenConversations);
}

if (confirmDeleteFriendButton) {
    confirmDeleteFriendButton.addEventListener("click", confirmDeleteFriend);
}

if (messageForm) {
    messageForm.addEventListener("submit", sendCurrentMessage);

    const submitButton = messageForm.querySelector(".send-button");
    if (submitButton) {
        submitButton.addEventListener("pointerdown", (event) => {
            if (!messageInput || messageInput.value.trim()) {
                return;
            }

            submitButton.setPointerCapture(event.pointerId);
            startVoiceRecording(event);
        });

        submitButton.addEventListener("pointerup", (event) => {
            if (!isRecordingVoice && !isPreparingVoice) {
                return;
            }

            event.preventDefault();
            stopVoiceRecording(true);
        });

        submitButton.addEventListener("pointercancel", () => stopVoiceRecording(false));
        submitButton.addEventListener("lostpointercapture", () => {
            if (isRecordingVoice || isPreparingVoice) {
                stopVoiceRecording(true);
            }
        });
    }
}

if (attachImageButton && imageInput) {
    attachImageButton.addEventListener("click", () => {
        if (!activeConversation || isSending) {
            return;
        }

        imageInput.click();
    });

    imageInput.addEventListener("change", sendSelectedImage);
}

if (messageInput) {
    updateComposerAction();
    messageInput.addEventListener("input", () => {
        updateComposerAction();

        if (!connection || !activeConversation) {
            return;
        }

        connection.invoke("StartTyping", activeConversation.userId);
        window.clearTimeout(typingTimer);
        typingTimer = window.setTimeout(() => {
            connection.invoke("StopTyping", activeConversation.userId);
        }, 700);
    });
}

if (backButton) {
    backButton.addEventListener("click", () => {
        if (messengerView) {
            messengerView.classList.remove("chat-open");
        }
    });
}

if (allFriendsButton) {
    allFriendsButton.addEventListener("click", openAllFriends);
}

if (addFriendButton) {
    addFriendButton.addEventListener("click", () => {
        openModal(addFriendModal);
        searchUsers();
    });
}

if (friendRequestsButton) {
    friendRequestsButton.addEventListener("click", () => {
        openModal(friendRequestsModal);
        loadFriendRequests();
    });
}

if (userSearchInput) {
    userSearchInput.addEventListener("input", () => {
        clearSearchValidation(userSearchInput);

        if (!userSearchInput.value.trim() && userSearchResults) {
            userSearchResults.innerHTML = "";
        }
    });

    userSearchInput.addEventListener("keydown", (event) => {
        if (event.key === "Enter") {
            event.preventDefault();
            searchUsers(true);
        }
    });
}

if (userSearchButton) {
    userSearchButton.addEventListener("click", () => searchUsers(true));
}
})();

