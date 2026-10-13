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

const loadPrograms = ["Morning cohort", "Evening cohort", "Weekend cohort"];
const loadSlots = Array.from({ length: 10 }, () => ({ on: false }));
const loadThreads = document.querySelector("#load-threads");
const loadThreadsValue = document.querySelector("#load-threads-value");
const loadStart = document.querySelector("#load-start");
const loadStop = document.querySelector("#load-stop");
const loadStats = document.querySelector("#load-stats");
let loadOn = false;
let loadAccepted = 0;
let loadFailed = 0;
let loadInFlight = 0;
let loadNext = 1;
const loadPage = crypto.randomUUID().slice(0, 8);
const ticksAtUnixEpoch = 621355968000000000;

function nextStudentEmail() {
  const n = loadNext++;
  const ticks = ticksAtUnixEpoch + Date.now() * 10000 + n;
  return `student-${ticks}-${loadPage}@example.com`;
}

function paintLoad() {
  const running = loadOn ? `${loadInFlight} in flight. ` : "Stopped. ";
  loadStats.textContent = `${running}${loadAccepted} accepted. ${loadFailed} failed.`;
  loadStats.classList.remove("idle", "ok", "busy", "error");
  loadStats.classList.add(loadOn ? "busy" : "idle");
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function postGenerated() {
  const n = loadNext;
  const body = {
    name: `Student ${n}`,
    email: nextStudentEmail(),
    program: loadPrograms[(n - 1) % loadPrograms.length],
    processing: document.querySelector("#enroll-processing").value
  };
  loadInFlight += 1;
  paintLoad();
  try {
    const response = await fetch("/api/enrollments", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    if (response.ok) {
      loadAccepted += 1;
    } else {
      loadFailed += 1;
      await delay(250);
    }
  } catch {
    loadFailed += 1;
    await delay(250);
  } finally {
    loadInFlight -= 1;
    paintLoad();
  }
}

function loadPump(slot) {
  async function step() {
    while (slot.on) {
      await postGenerated();
    }
  }

  step();
}

function applyLoadThreads() {
  const wanted = Number(loadThreads.value);
  loadThreadsValue.textContent = String(wanted);
  for (let i = 0; i < loadSlots.length; i += 1) {
    const should = loadOn && i < wanted;
    if (should && !loadSlots[i].on) {
      loadSlots[i].on = true;
      loadPump(loadSlots[i]);
    } else if (!should) {
      loadSlots[i].on = false;
    }
  }

  loadStart.disabled = loadOn;
  loadStop.disabled = !loadOn;
  paintLoad();
}

loadThreads.addEventListener("input", applyLoadThreads);
loadStart.addEventListener("click", () => {
  loadOn = true;
  applyLoadThreads();
});
loadStop.addEventListener("click", () => {
  loadOn = false;
  applyLoadThreads();
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

const ordersList = document.querySelector("#orders-list");
const ordersEmpty = document.querySelector("#orders-empty");
const ordersRefresh = document.querySelector("#orders-refresh");
const gateList = document.querySelector("#gate-list");
const processingNote = document.querySelector("#processing-note");

const gateLabels = {
  running: "Running",
  pause: "Pause processing",
  error: "Route to error"
};

function renderOrders(items) {
  ordersList.replaceChildren();
  ordersEmpty.hidden = items.length > 0;
  for (const item of items) {
    const li = document.createElement("li");
    const who = document.createElement("strong");
    who.textContent = item.name;
    const state = document.createElement("span");
    state.textContent = item.summary;
    const detail = document.createElement("span");
    detail.textContent = `${item.email} · ${item.trackingId}`;
    li.append(who, state, detail);
    ordersList.append(li);
  }
}

async function loadOrders() {
  const items = await readJson(await fetch("/api/orders"));
  renderOrders(items);
}

ordersRefresh.addEventListener("click", () => {
  loadOrders().catch((error) => showError(error.message));
});

function allowsPause(message, handlers) {
  if (message) return message.endsWith("Command");
  return handlers.some((handler) => handler.message.endsWith("Command"));
}

function gateSelect(mode, disabled, allowPause) {
  const select = document.createElement("select");
  select.disabled = disabled;
  for (const [value, label] of Object.entries(gateLabels)) {
    if (value === "pause" && !allowPause) continue;
    const option = document.createElement("option");
    option.value = value;
    option.textContent = label;
    option.selected = value === mode;
    select.append(option);
  }
  return select;
}

function renderGates(catalog) {
  gateList.replaceChildren();
  processingNote.textContent = catalog.canUpdate
    ? "Endpoints notice a change within about 5 seconds."
    : "App Configuration is not connected, so these gates cannot be changed from the page.";

  for (const endpoint of catalog.endpoints) {
    const section = document.createElement("section");
    section.className = "gate";
    const heading = document.createElement("h3");
    heading.textContent = endpoint.label;
    section.append(heading);
    section.append(gateRow(catalog, endpoint.name, null, "Whole endpoint", endpoint.mode, endpoint.handlers));
    for (const handler of endpoint.handlers) {
      section.append(gateRow(catalog, endpoint.name, handler.message, handler.label, handler.mode, endpoint.handlers));
    }
    gateList.append(section);
  }
}

function gateRow(catalog, endpoint, message, label, mode, handlers) {
  const row = document.createElement("div");
  row.className = "gate-row";
  const caption = document.createElement("label");
  caption.textContent = label;
  const select = gateSelect(mode, !catalog.canUpdate, allowsPause(message, handlers));
  row.append(caption, select);
  select.addEventListener("change", () => {
    saveGate(endpoint, message, select).catch((error) => showError(error.message));
  });
  return row;
}

async function saveGate(endpoint, message, select) {
  const mode = select.value;
  select.disabled = true;
  processingNote.textContent = "Saving…";
  try {
    const response = await fetch("/api/processing", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ endpoint, message, mode })
    });
    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      const detail = problem?.errors
        ? Object.values(problem.errors).flat().join(" ")
        : problem?.detail;
      throw new Error(detail || `Could not update the gate (${response.status}).`);
    }
    renderGates(await response.json());
  } catch (error) {
    processingNote.textContent = error.message;
    showError(error.message);
    select.disabled = false;
  }
}

async function loadGates() {
  const response = await fetch("/api/processing");
  if (!response.ok) {
    processingNote.textContent = `Could not read the gates (${response.status}).`;
    return;
  }
  renderGates(await response.json());
}

loadOrders().catch((error) => showError(error.message));
loadGates().catch((error) => showError(error.message));

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
