const conversations = [
    {
        id: "maya",
        name: "Maya Sterling",
        status: "Online now",
        time: "09:42",
        accent: "M",
        preview: "The product mockup is ready for review.",
        messages: [
            { from: "them", text: "Morning Alex. The product mockup is ready for review.", time: "09:32" },
            { from: "mine", text: "Great. Send me the main screen first.", time: "09:34" },
            { from: "them", text: "Done. I kept the Elovo gradient and made the chat area cleaner.", time: "09:42" }
        ]
    },
    {
        id: "team",
        name: "Design Team",
        status: "4 members active",
        time: "10:15",
        accent: "D",
        preview: "Login, inbox, and conversation flow are connected.",
        messages: [
            { from: "them", text: "Login, inbox, and conversation flow are connected.", time: "10:08" },
            { from: "mine", text: "Nice. Keep every visible label in English for this build.", time: "10:10" },
            { from: "them", text: "Already done. This is still a simulation, so everything stays local.", time: "10:15" }
        ]
    },
    {
        id: "noah",
        name: "Noah Reed",
        status: "Last seen 12 min ago",
        time: "Yesterday",
        accent: "N",
        preview: "Can we add file sharing after the prototype?",
        messages: [
            { from: "them", text: "Can we add file sharing after the prototype?", time: "18:20" },
            { from: "mine", text: "Yes. For now I want the simulation to feel complete.", time: "18:24" }
        ]
    },
    {
        id: "olivia",
        name: "Olivia Hart",
        status: "Online now",
        time: "Mon",
        accent: "O",
        preview: "The visual direction matches the logo nicely.",
        messages: [
            { from: "them", text: "The visual direction matches the logo nicely.", time: "14:02" },
            { from: "mine", text: "Good. I want it to feel like Elovo before people read the name.", time: "14:05" }
        ]
    }
];

const loginView = document.querySelector("#loginView");
const messengerView = document.querySelector("#messengerView");
const loginForm = document.querySelector("#loginForm");
const registerForm = document.querySelector("#registerForm");
const showRegister = document.querySelector("#showRegister");
const showLogin = document.querySelector("#showLogin");
const loginUsernameInput = document.querySelector("#loginUsername");
const loginPasswordInput = document.querySelector("#loginPassword");
const loginError = document.querySelector("#loginError");
const regUsernameInput = document.querySelector("#regUsername");
const regPasswordInput = document.querySelector("#regPassword");
const regError = document.querySelector("#regError");
const logoutButton = document.querySelector("#logoutButton");
const chatList = document.querySelector("#chatList");
const searchInput = document.querySelector("#searchInput");
const messageStream = document.querySelector("#messageStream");
const messageForm = document.querySelector("#messageForm");
const messageInput = document.querySelector("#messageInput");
const activeName = document.querySelector("#activeName");
const activeStatus = document.querySelector("#activeStatus");
const activeAvatar = document.querySelector("#activeAvatar");
const backButton = document.querySelector("#backButton");

let activeConversation = conversations[0];
let latestMessageId = "";
let isSending = false;

function renderChatList(items = conversations) {
    chatList.innerHTML = "";

    items.forEach((chat, index) => {
        const button = document.createElement("button");
        const avatar = document.createElement("span");
        const copy = document.createElement("span");
        const name = document.createElement("strong");
        const preview = document.createElement("span");
        const time = document.createElement("span");

        button.type = "button";
        button.className = `chat-item${chat.id === activeConversation.id ? " is-active" : ""}`;
        button.style.animationDelay = `${index * 45}ms`;
        avatar.className = "avatar";
        copy.className = "chat-copy";
        time.className = "time";
        avatar.textContent = chat.accent;
        name.textContent = chat.name;
        preview.textContent = chat.preview;
        time.textContent = chat.time;
        copy.append(name, preview);
        button.append(avatar, copy, time);
        button.addEventListener("click", () => {
            activeConversation = chat;
            latestMessageId = "";
            renderChatList(getFilteredConversations());
            renderConversation();
            messengerView.classList.add("chat-open");
        });
        chatList.appendChild(button);
    });
}

function renderConversation() {
    activeName.textContent = activeConversation.name;
    activeStatus.textContent = activeConversation.status;
    activeAvatar.textContent = activeConversation.accent;
    messageStream.innerHTML = "";

    activeConversation.messages.forEach((message) => {
        const bubble = document.createElement("article");
        const time = document.createElement("small");

        bubble.className = `message ${message.from}${message.id === latestMessageId ? " is-new" : ""}`;
        bubble.textContent = message.text;
        time.textContent = message.time;
        bubble.appendChild(time);
        messageStream.appendChild(bubble);
    });

    messageStream.scrollTo({
        top: messageStream.scrollHeight,
        behavior: "smooth"
    });
}

function appendPendingMessage(text, reserveSpace = false) {
    const bubble = document.createElement("article");
    const time = document.createElement("small");

    bubble.className = "message mine is-pending";
    bubble.textContent = text;
    time.textContent = "Sending...";
    bubble.appendChild(time);

    if (reserveSpace) {
        bubble.style.opacity = "0";
        bubble.style.animation = "none";
        bubble.style.transform = "none";
    }

    messageStream.appendChild(bubble);
    return bubble;
}

function getFilteredConversations() {
    const term = searchInput.value.trim().toLowerCase();

    if (!term) {
        return conversations;
    }

    return conversations.filter((chat) => {
        return chat.name.toLowerCase().includes(term) || chat.preview.toLowerCase().includes(term);
    });
}

function getCurrentTime() {
    return new Intl.DateTimeFormat("en", {
        hour: "2-digit",
        minute: "2-digit",
        hour12: false
    }).format(new Date());
}

function isScrolledToBottom() {
    return messageStream.scrollHeight - messageStream.scrollTop - messageStream.clientHeight < 4;
}

if (registerForm) {
    registerForm.addEventListener("submit", (event) => {
        event.preventDefault();
        const username = regUsernameInput.value.trim();
        const password = regPasswordInput.value.trim();
        
        let users = JSON.parse(localStorage.getItem("elovoUsers") || "{}");
        if (users[username]) {
            regError.style.display = "block";
        } else {
            users[username] = password;
            localStorage.setItem("elovoUsers", JSON.stringify(users));
            regError.style.display = "none";
            alert("Registration successful! You can now log in.");
            window.location.href = "login.html";
        }
    });
}

if (loginForm) {
    loginForm.addEventListener("submit", (event) => {
        event.preventDefault();
        const username = loginUsernameInput.value.trim();
        const password = loginPasswordInput.value.trim();
        
        let users = JSON.parse(localStorage.getItem("elovoUsers") || "{}");
        if (users[username] && users[username] === password) {
            loginError.style.display = "none";
            localStorage.setItem("elovoCurrentUser", username);
            window.location.href = "index.html";
        } else {
            loginError.style.display = "block";
        }
    });
}

if (messengerView && chatList && messageStream) {
    renderChatList();
    renderConversation();
}

if (logoutButton) {
    logoutButton.addEventListener("click", () => {
        window.location.href = "login.html";
    });
}

if (searchInput) {
    searchInput.addEventListener("input", () => {
        renderChatList(getFilteredConversations());
    });
}

if (messageForm) {
    messageForm.addEventListener("submit", (event) => {
    event.preventDefault();
    const text = messageInput.value.trim();

    if (!text || isSending) {
        return;
    }

    const messageId = `${activeConversation.id}-${Date.now()}`;
    const wasAtBottom = isScrolledToBottom();
    const previousScrollHeight = messageStream.scrollHeight;

    isSending = true;
    messageInput.value = "";
    messageInput.disabled = true;
    messageForm.classList.add("is-sending");

    const bubble = appendPendingMessage(text, true);
    const addedHeight = messageStream.scrollHeight - previousScrollHeight;

    if (wasAtBottom) {
        messageStream.scrollTop += addedHeight;
    }

    window.requestAnimationFrame(() => {
        bubble.style.transition = "opacity 220ms ease";
        bubble.style.opacity = "0.86";

        setTimeout(() => {
            bubble.style.transition = "";
            bubble.style.animation = "";
            bubble.style.opacity = "";
            bubble.style.transform = "";
        }, 220);
    });

    window.setTimeout(() => {
        const time = getCurrentTime();

        activeConversation.messages.push({
            id: messageId,
            from: "mine",
            text,
            time
        });
        activeConversation.preview = text;
        activeConversation.time = "Now";
        latestMessageId = messageId;
        bubble.classList.remove("is-pending");
        bubble.querySelector("small").textContent = time;
        messageStream.scrollTo({
            top: messageStream.scrollHeight,
            behavior: "smooth"
        });
        renderChatList(getFilteredConversations());
        messageForm.classList.remove("is-sending");
        messageInput.disabled = false;
        messageInput.focus();
        isSending = false;
    }, 560);
    });
}

if (backButton) {
    backButton.addEventListener("click", () => {
        if (messengerView) {
            messengerView.classList.remove("chat-open");
        }
    });
}
