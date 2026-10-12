const helloMessage = document.querySelector("#hello-message");
const helloFlag = document.querySelector("#hello-flag");
const helloEnabled = document.querySelector("#hello-enabled");
const helloTiming = document.querySelector("#hello-timing");
const helloLog = document.querySelector("#hello-log");
const helloCall = document.querySelector("#hello-call");
const pingStatus = document.querySelector("#ping-status");
const pingTiming = document.querySelector("#ping-timing");
const pingLog = document.querySelector("#ping-log");
const pingCall = document.querySelector("#ping-call");
const todoList = document.querySelector("#todo-list");
const todoEmpty = document.querySelector("#todo-empty");
const todoForm = document.querySelector("#todo-form");
const todoTitle = document.querySelector("#todo-title");
const todosError = document.querySelector("#todos-error");
const appError = document.querySelector("#app-error");

async function readJson(response) {
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`${response.status} ${response.statusText}${text ? `: ${text}` : ""}`);
  }
  if (response.status === 204) {
    return null;
  }
  return response.json();
}

function showError(message) {
  appError.hidden = !message;
  appError.textContent = message ?? "";
}

async function runCall({ path, button, log, onStart, onSuccess }) {
  showError("");
  button.disabled = true;
  onStart();
  log.textContent = `Calling GET ${path}…`;
  const started = performance.now();
  try {
    const response = await fetch(path);
    const elapsed = Math.round(performance.now() - started);
    const data = await readJson(response);
    log.textContent = `${response.status} ${response.statusText} in ${elapsed} ms`;
    onSuccess(data, elapsed, response);
  } catch (error) {
    log.textContent = `Failed: ${error.message}`;
    throw error;
  } finally {
    button.disabled = false;
  }
}

function resetHello() {
  helloMessage.textContent = "Not called yet";
  helloMessage.classList.add("idle");
  helloFlag.textContent = "—";
  helloEnabled.textContent = "—";
  helloTiming.textContent = "—";
  helloLog.textContent = "Press Call to GET /api/hello";
}

function resetPing() {
  pingStatus.textContent = "Not called yet";
  pingStatus.classList.add("idle", "status");
  pingStatus.classList.remove("ok", "busy");
  pingTiming.textContent = "Last call: —";
  pingLog.textContent = "Press Call to GET /api/ping";
}

async function callHello() {
  await runCall({
    path: "/api/hello",
    button: helloCall,
    log: helloLog,
    onStart() {
      helloMessage.textContent = "Calling…";
      helloMessage.classList.remove("idle");
      helloFlag.textContent = "—";
      helloEnabled.textContent = "—";
      helloTiming.textContent = "—";
    },
    onSuccess(data, elapsed) {
      helloMessage.textContent = data.message;
      helloFlag.textContent = data.featureFlag;
      helloEnabled.textContent = data.featureEnabled ? "true" : "false";
      helloTiming.textContent = `${elapsed} ms`;
    }
  });
}

async function callPing() {
  await runCall({
    path: "/api/ping",
    button: pingCall,
    log: pingLog,
    onStart() {
      pingStatus.textContent = "Calling…";
      pingStatus.classList.add("busy");
      pingStatus.classList.remove("idle", "ok");
      pingTiming.textContent = "Last call: —";
    },
    onSuccess(data, elapsed) {
      pingStatus.textContent = data.status;
      pingStatus.classList.remove("busy", "idle");
      pingStatus.classList.toggle("ok", data.status === "pong");
      pingTiming.textContent = `Last call: ${elapsed} ms`;
    }
  });
}

function renderTodos(items) {
  todoList.replaceChildren();
  todoEmpty.hidden = items.length > 0;
  if (!items.length) {
    return;
  }

  for (const item of items) {
    const li = document.createElement("li");
    if (item.isComplete) {
      li.classList.add("done");
    }

    const checkbox = document.createElement("input");
    checkbox.type = "checkbox";
    checkbox.checked = item.isComplete;
    checkbox.setAttribute("aria-label", `Complete ${item.title}`);
    checkbox.addEventListener("change", () => updateTodo(item, checkbox.checked));

    const title = document.createElement("span");
    title.textContent = item.title;

    const remove = document.createElement("button");
    remove.type = "button";
    remove.className = "ghost";
    remove.textContent = "Delete";
    remove.addEventListener("click", () => deleteTodo(item.id));

    li.append(checkbox, title, remove);
    todoList.append(li);
  }
}

async function loadTodos() {
  todosError.hidden = true;
  const items = await readJson(await fetch("/api/todos"));
  renderTodos(items);
}

async function updateTodo(item, isComplete) {
  await readJson(await fetch(`/api/todos/${item.id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ title: item.title, isComplete })
  }));
  await loadTodos();
}

async function deleteTodo(id) {
  const response = await fetch(`/api/todos/${id}`, { method: "DELETE" });
  if (!response.ok && response.status !== 204) {
    throw new Error(`Delete failed (${response.status})`);
  }
  await loadTodos();
}

todoForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  todosError.hidden = true;
  try {
    const response = await fetch("/api/todos", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ title: todoTitle.value })
    });
    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      const detail = problem?.errors?.title?.[0] ?? `Could not create todo (${response.status})`;
      todosError.hidden = false;
      todosError.textContent = detail;
      return;
    }
    todoTitle.value = "";
    await loadTodos();
  } catch (error) {
    todosError.hidden = false;
    todosError.textContent = error.message;
  }
});

document.querySelector("#hello-call").addEventListener("click", () => callHello().catch((error) => showError(error.message)));
document.querySelector("#hello-reset").addEventListener("click", resetHello);
document.querySelector("#ping-call").addEventListener("click", () => callPing().catch((error) => showError(error.message)));
document.querySelector("#ping-reset").addEventListener("click", resetPing);

loadTodos().catch((error) => showError(error.message));

const enrollForm = document.querySelector("#enroll-form");
const enrollStatus = document.querySelector("#enroll-status");
const enrollLog = document.querySelector("#enroll-log");
const enrollSubmit = document.querySelector("#enroll-submit");
const enrollRestart = document.querySelector("#enroll-restart");
const toasts = document.querySelector("#toasts");

const pollEveryMs = 2000;
const jokeAfterMs = 10000;
const giveUpAfterMs = 120000;
const jokes = [
  "A queue walks into a bar and asks for a table. The bartender says, “You’re already in one.”",
  "Why was the message calm? It knew the handler would get to it.",
  "I told my database a joke. It said it would commit to laughing later.",
  "Delayed delivery is just a message that took the scenic route."
];

let pollTimer = null;
let pollStartedAt = 0;
let activeTrackingId = null;
let jokeShown = false;

function showToast(message) {
  const toast = document.createElement("p");
  toast.className = "toast";
  toast.textContent = message;
  toasts.append(toast);
  window.setTimeout(() => toast.remove(), 6000);
}

function problemMessages(problem) {
  if (!problem || !problem.errors) {
    return [];
  }

  return Object.values(problem.errors).flat().filter(Boolean);
}

function setEnrollStatus(text, kind) {
  enrollStatus.textContent = text;
  enrollStatus.classList.remove("idle", "ok", "busy", "error");
  if (kind) {
    enrollStatus.classList.add(kind);
  }
}

function stopPolling() {
  if (pollTimer !== null) {
    window.clearInterval(pollTimer);
    pollTimer = null;
  }
}

async function readEnrollment(trackingId) {
  const response = await fetch(`/api/enrollments/${trackingId}`);
  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`${response.status} ${response.statusText}${text ? `: ${text}` : ""}`);
  }

  return response.json();
}

async function tickEnrollment() {
  const elapsed = performance.now() - pollStartedAt;
  const row = await readEnrollment(activeTrackingId);

  if (row && row.status === "completed") {
    stopPolling();
    enrollRestart.hidden = true;
    setEnrollStatus("transaction is completed, thank you", "ok");
    enrollLog.textContent = `${row.name} is enrolled in ${row.program}. Tracking ${activeTrackingId}.`;
    loadEnrollments(document.querySelector("#lookup-email").value).catch((error) => showError(error.message));
    return;
  }

  if (row && row.status === "rejected") {
    stopPolling();
    enrollRestart.hidden = true;
    setEnrollStatus(row.message || "This enrollment was rejected.", "error");
    enrollLog.textContent = `Rejected ${activeTrackingId}.`;
    loadEnrollments(document.querySelector("#lookup-email").value).catch((error) => showError(error.message));
    return;
  }

  if (elapsed >= giveUpAfterMs) {
    stopPolling();
    setEnrollStatus("No result yet. The handler may still be working.", "busy");
    enrollLog.textContent = `Stopped checking ${activeTrackingId} after two minutes.`;
    enrollRestart.hidden = false;
    return;
  }

  if (elapsed >= jokeAfterMs && !jokeShown) {
    jokeShown = true;
    const joke = jokes[Math.floor(Math.random() * jokes.length)];
    setEnrollStatus(`This is taking longer than expected, let me tell you a joke in the meantime. ${joke}`, "busy");
  }
}

function startPolling(trackingId) {
  stopPolling();
  activeTrackingId = trackingId;
  pollStartedAt = performance.now();
  jokeShown = false;
  enrollRestart.hidden = true;
  setEnrollStatus("Thank you, please hold while processing…", "busy");
  enrollLog.textContent = `Checking ${trackingId} every 2 seconds.`;
  pollTimer = window.setInterval(() => {
    tickEnrollment().catch((error) => {
      stopPolling();
      setEnrollStatus("Could not check the enrollment.", "busy");
      enrollLog.textContent = error.message;
      enrollRestart.hidden = false;
    });
  }, pollEveryMs);
  tickEnrollment().catch((error) => showError(error.message));
}

enrollForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  stopPolling();
  enrollRestart.hidden = true;
  enrollSubmit.disabled = true;
  setEnrollStatus("Sending…", "busy");
  enrollLog.textContent = "";

  const body = {
    name: document.querySelector("#enroll-name").value,
    email: document.querySelector("#enroll-email").value,
    program: document.querySelector("#enroll-program").value,
    processing: document.querySelector("#enroll-processing").value
  };

  try {
    const response = await fetch("/api/enrollments", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });

    if (response.status === 400) {
      const problem = await response.json().catch(() => null);
      const messages = problemMessages(problem);
      if (messages.length === 0) {
        showToast("The enrollment was not accepted.");
      } else {
        for (const message of messages) {
          showToast(message);
        }
      }
      setEnrollStatus("Fix the form and try again.", "idle");
      return;
    }

    if (!response.ok) {
      const text = await response.text();
      throw new Error(`${response.status} ${response.statusText}${text ? `: ${text}` : ""}`);
    }

    const accepted = await response.json();
    startPolling(accepted.trackingId);
  } catch (error) {
    setEnrollStatus("The enrollment could not be sent.", "idle");
    enrollLog.textContent = error.message;
    showToast(error.message);
  } finally {
    enrollSubmit.disabled = false;
  }
});

enrollRestart.addEventListener("click", () => {
  if (activeTrackingId) {
    startPolling(activeTrackingId);
  }
});

const lookupForm = document.querySelector("#lookup-form");
const lookupEmail = document.querySelector("#lookup-email");
const lookupList = document.querySelector("#lookup-list");
const lookupEmpty = document.querySelector("#lookup-empty");

function renderEnrollments(items) {
  lookupList.replaceChildren();
  lookupEmpty.hidden = items.length > 0;
  for (const item of items) {
    const li = document.createElement("li");
    const who = document.createElement("p");
    who.className = "who";
    who.textContent = `${item.name} · ${item.email}`;
    const detail = document.createElement("p");
    detail.className = "detail";
    detail.textContent = item.message
      ? `${item.program}. ${item.message}`
      : item.program;
    const state = document.createElement("p");
    state.className = `state ${item.status}`;
    state.textContent = item.status;
    li.append(who, state, detail);
    lookupList.append(li);
  }
}

async function loadEnrollments(email) {
  const query = email && email.trim() ? `?email=${encodeURIComponent(email.trim())}` : "";
  const items = await readJson(await fetch(`/api/enrollments${query}`));
  renderEnrollments(items);
}

lookupForm.addEventListener("submit", (event) => {
  event.preventDefault();
  loadEnrollments(lookupEmail.value).catch((error) => showError(error.message));
});

loadEnrollments("").catch((error) => showError(error.message));

const tabs = [...document.querySelectorAll("[role='tab']")];

function showTab(name, { updateHash = true } = {}) {
  const match = tabs.find((tab) => tab.id === `tab-${name}`) ?? tabs[0];
  for (const tab of tabs) {
    const selected = tab === match;
    tab.setAttribute("aria-selected", selected ? "true" : "false");
    tab.tabIndex = selected ? 0 : -1;
    document.getElementById(tab.getAttribute("aria-controls")).hidden = !selected;
  }
  if (updateHash && location.hash !== `#${match.id.slice(4)}`) {
    history.replaceState(null, "", `#${match.id.slice(4)}`);
  }
}

for (const tab of tabs) {
  tab.addEventListener("click", () => showTab(tab.id.slice(4)));
}

document.querySelector(".tabs").addEventListener("keydown", (event) => {
  const current = tabs.indexOf(document.activeElement);
  if (current < 0) {
    return;
  }
  const next = event.key === "ArrowRight" || event.key === "ArrowDown"
    ? (current + 1) % tabs.length
    : event.key === "ArrowLeft" || event.key === "ArrowUp"
      ? (current - 1 + tabs.length) % tabs.length
      : -1;
  if (next < 0) {
    return;
  }
  event.preventDefault();
  tabs[next].focus();
  showTab(tabs[next].id.slice(4));
});

window.addEventListener("hashchange", () => showTab(location.hash.slice(1), { updateHash: false }));
showTab(location.hash.slice(1) || "hello", { updateHash: false });
