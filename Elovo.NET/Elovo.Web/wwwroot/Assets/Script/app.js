const loginForm = document.querySelector("#loginForm");
const registerForm = document.querySelector("#registerForm");
const logoutButton = document.querySelector("#logoutButton");
const messengerView = document.querySelector("#messengerView");
const chatList = document.querySelector("#chatList");
const searchInput = document.querySelector("#searchInput");
const messageStream = document.querySelector("#messageStream");
const messageForm = document.querySelector("#messageForm");
const messageInput = document.querySelector("#messageInput");
const attachImageButton = document.querySelector("#attachImageButton");
const imageInput = document.querySelector("#imageInput");
const activeName = document.querySelector("#activeName");
const activeStatus = document.querySelector("#activeStatus");
const activeAvatar = document.querySelector("#activeAvatar");
const backButton = document.querySelector("#backButton");
const allFriendsButton = document.querySelector("#allFriendsButton");
const addFriendButton = document.querySelector("#addFriendButton");
const friendRequestsButton = document.querySelector("#friendRequestsButton");
const allFriendsModal = document.querySelector("#allFriendsModal");
const addFriendModal = document.querySelector("#addFriendModal");
const friendRequestsModal = document.querySelector("#friendRequestsModal");
const allFriendsList = document.querySelector("#allFriendsList");
const userSearchInput = document.querySelector("#userSearchInput");
const userSearchResults = document.querySelector("#userSearchResults");
const friendRequestsList = document.querySelector("#friendRequestsList");
const pageLoader = document.querySelector("#pageLoader");

let conversations = [];
let activeConversation = null;
let connection = null;
let typingTimer = null;
let userSearchTimer = null;
let latestMessageId = "";
let isSending = false;
const allowedImageTypes = ["image/png", "image/jpeg", "image/jpg", "image/gif"];
const maxImageSize = 10 * 1024 * 1024;

function showPageLoader() {
    if (!pageLoader) {
        return;
    }

    pageLoader.setAttribute("aria-hidden", "false");
    document.body.classList.add("is-page-loading");
}

function hidePageLoader() {
    if (!pageLoader) {
        return;
    }

    pageLoader.setAttribute("aria-hidden", "true");
    document.body.classList.remove("is-page-loading");
}

function navigateWithLoader(url) {
    showPageLoader();
    window.location.href = url;
}

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
    window.localStorage.setItem(getConversationStorageKey(userId), JSON.stringify(messages));
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
        return;
    }

    const messages = readStoredMessages(otherUserId);
    if (messages.some((storedMessage) => storedMessage.id === message.id)) {
        return;
    }

    messages.push(message);
    messages.sort((first, second) => new Date(first.sentAt) - new Date(second.sentAt));
    writeStoredMessages(otherUserId, messages);
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
            !message.readAt) {
            changed = true;
            return { ...message, readAt };
        }

        return message;
    });

    if (changed) {
        writeStoredMessages(otherUserId, updatedMessages);
    }

    return changed;
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
    return message.isImage ? "Image" : message.content;
}

function applyLocalConversationHistory(items) {
    return items
        .map((chat) => {
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

function getAntiForgeryToken() {
    const token = document.querySelector("input[name='__RequestVerificationToken']");
    return token ? token.value : "";
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

function getFilteredConversations() {
    const term = searchInput ? searchInput.value.trim().toLowerCase() : "";

    if (!term) {
        return conversations;
    }

    return conversations.filter((chat) => {
        return chat.username.toLowerCase().includes(term);
    });
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
        const copy = document.createElement("span");
        const name = document.createElement("strong");
        const preview = document.createElement("span");
        const time = document.createElement("span");

        button.type = "button";
        button.className = `chat-item${activeConversation && sameId(chat.userId, activeConversation.userId) ? " is-active" : ""}`;
        button.style.animationDelay = `${index * 45}ms`;
        avatar.className = "avatar";
        copy.className = "chat-copy";
        time.className = "time";
        avatar.textContent = chat.initial;
        name.textContent = chat.username;
        preview.textContent = chat.lastMessage || "Start a conversation.";
        time.textContent = formatTime(chat.lastMessageAt);
        copy.append(name, preview);
        button.append(avatar, copy, time);
        button.addEventListener("click", () => selectConversation(chat.userId));
        chatList.appendChild(button);
    });
}

function renderConversationHeader(chat) {
    if (!chat || !activeName || !activeStatus || !activeAvatar) {
        return;
    }

    activeName.textContent = chat.username;
    activeStatus.textContent = formatStatus(chat);
    activeAvatar.textContent = chat.initial;
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

    bubble.className = `message ${isMine ? "mine" : "them"}${message.id === latestMessageId ? " is-new" : ""}${message.isImage ? " has-image" : ""}`;
    meta.className = "message-meta";
    time.textContent = formatTime(message.sentAt);
    meta.appendChild(time);

    if (message.isImage && message.imagePath) {
        bubble.appendChild(createImageMessage(message));
    } else {
        bubble.appendChild(document.createTextNode(message.content));
    }

    if (isMine) {
        const status = document.createElement("img");
        status.className = "message-status-icon";
        status.src = message.readAt
            ? "/Assets/Images/Icons/message-read.svg"
            : "/Assets/Images/Icons/sent-action.svg";
        status.alt = message.readAt ? "Read" : "Sent";
        status.title = message.readAt ? "Read" : "Sent";
        meta.appendChild(status);
    }

    bubble.appendChild(meta);
    messageStream.appendChild(bubble);
}

function createImageMessage(message) {
    const frame = document.createElement("button");
    const image = document.createElement("img");
    const loader = document.createElement("span");

    frame.type = "button";
    frame.className = "message-image-frame is-loading";
    frame.title = message.imageFileName || "Open image";
    image.src = message.imagePath;
    image.alt = message.imageFileName || "Sent image";
    image.loading = "lazy";
    loader.className = "image-transfer-loader";

    image.addEventListener("load", () => {
        frame.classList.remove("is-loading");
    });

    image.addEventListener("error", () => {
        frame.classList.remove("is-loading");
        frame.classList.add("is-error");
    });

    frame.addEventListener("click", () => openImagePreview(message.imagePath, message.imageFileName));
    frame.append(image, loader);
    return frame;
}

function openImagePreview(path, fileName) {
    const backdrop = document.createElement("div");
    const image = document.createElement("img");
    const close = document.createElement("button");

    backdrop.className = "image-preview-backdrop is-open";
    backdrop.setAttribute("role", "dialog");
    backdrop.setAttribute("aria-modal", "true");
    image.src = path;
    image.alt = fileName || "Image preview";
    close.type = "button";
    close.className = "image-preview-close";
    close.setAttribute("aria-label", "Close");
    close.textContent = "×";

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

    if (!activeConversation && conversations.length > 0) {
        activeConversation = conversations[0];
    } else if (activeConversation) {
        activeConversation = conversations.find(x => sameId(x.userId, activeConversation.userId)) || activeConversation;
    }

    renderChatList(getFilteredConversations());

    if (activeConversation) {
        renderConversationHeader(activeConversation);
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

        avatar.textContent = user.initial;
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
        const button = document.createElement("button");

        row.className = "user-row";
        avatar.className = "avatar";
        copy.className = "chat-copy";
        button.type = "button";
        button.className = "row-action primary";

        avatar.textContent = friend.initial;
        name.textContent = friend.username;
        status.textContent = formatStatus(friend);
        button.textContent = "Open";
        button.addEventListener("click", () => {
            closeModal(allFriendsModal);
            selectConversation(friend.userId);
        });

        copy.append(name, status);
        row.append(avatar, copy, button);
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

async function searchUsers() {
    if (!userSearchInput || !userSearchResults) {
        return;
    }

    const term = userSearchInput.value.trim();
    if (!term) {
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

        avatar.textContent = request.initial;
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
        .build();

    connection.on("ReceiveMessage", async (message) => {
        latestMessageId = message.id;
        storeMessage(message);

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
    });

    connection.on("UserOnline", (userId) => {
        updateUserStatus(userId, true);
    });

    connection.on("UserOffline", (userId) => {
        updateUserStatus(userId, false);
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

        if (activeConversation && sameId(activeConversation.userId, readerId)) {
            await loadMessages(activeConversation.userId);
        }
    });

    await connection.start();
    notifyActiveConversationRead();
}

function messageBelongsToActiveConversation(message) {
    return activeConversation &&
        (sameId(message.senderId, activeConversation.userId) ||
            sameId(message.receiverId, activeConversation.userId));
}

function updateUserStatus(userId, isOnline) {
    const chat = conversations.find(x => sameId(x.userId, userId));
    if (!chat) {
        return;
    }

    chat.isOnline = isOnline;
    chat.lastSeenAt = isOnline ? null : new Date().toISOString();

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

    isSending = true;
    messageInput.value = "";
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

    if (attachImageButton) {
        attachImageButton.disabled = active;
    }

    if (messageInput) {
        messageInput.disabled = active || isSending;
    }
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
                setImageTransferState(true, Math.round((event.loaded / event.total) * 100));
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
        setImageTransferState(true, 100);
        await connection.invoke("SendImageMessage", activeConversation.userId, image.path, image.fileName || file.name);
    } finally {
        isSending = false;
        setImageTransferState(false);
        if (messageInput) {
            messageInput.focus();
        }
    }
}

async function logout() {
    showPageLoader();

    await fetch("/auth/logout", {
        method: "POST",
        headers: {
            "RequestVerificationToken": getAntiForgeryToken()
        }
    });

    navigateWithLoader("/auth/login");
}

if (messengerView && chatList && messageStream) {
    loadConversations()
        .then(() => activeConversation ? loadMessages(activeConversation.userId) : null)
        .then(startSignalR)
        .catch(() => {
            navigateWithLoader("/auth/login");
        });
}

if (logoutButton) {
    logoutButton.addEventListener("click", logout);
}

if (searchInput) {
    searchInput.addEventListener("input", () => {
        renderChatList(getFilteredConversations());
    });
}

if (messageForm) {
    messageForm.addEventListener("submit", sendCurrentMessage);
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
    messageInput.addEventListener("input", () => {
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
        window.clearTimeout(userSearchTimer);
        userSearchTimer = window.setTimeout(searchUsers, 250);
    });
}

document.querySelectorAll("[data-close-modal]").forEach((button) => {
    button.addEventListener("click", () => {
        closeModal(button.closest(".modal-backdrop"));
    });
});

document.querySelectorAll(".modal-backdrop").forEach((modal) => {
    modal.addEventListener("click", (event) => {
        if (event.target === modal) {
            closeModal(modal);
        }
    });
});

if (loginForm || registerForm) {
    window.localStorage.removeItem("elovoCurrentUser");
}

document.querySelectorAll("a[href]").forEach((link) => {
    link.addEventListener("click", (event) => {
        const url = new URL(link.href, window.location.href);
        const isSamePageHash = url.origin === window.location.origin &&
            url.pathname === window.location.pathname &&
            url.search === window.location.search &&
            url.hash;

        if (event.defaultPrevented ||
            event.button !== 0 ||
            event.metaKey ||
            event.ctrlKey ||
            event.shiftKey ||
            event.altKey ||
            link.target ||
            link.hasAttribute("download") ||
            url.origin !== window.location.origin ||
            isSamePageHash) {
            return;
        }

        showPageLoader();
    });
});

[loginForm, registerForm].forEach((form) => {
    if (!form) {
        return;
    }

    form.addEventListener("submit", () => {
        showPageLoader();
        form.querySelectorAll("button").forEach((button) => {
            button.disabled = true;
        });
    });
});

if (window.matchMedia("(max-width: 820px), (pointer: coarse)").matches) {
    ["copy", "cut", "contextmenu", "selectstart"].forEach((eventName) => {
        document.addEventListener(eventName, (event) => {
            if (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement) {
                return;
            }

            event.preventDefault();
        }, { capture: true });
    });
}

window.addEventListener("pageshow", hidePageLoader);
