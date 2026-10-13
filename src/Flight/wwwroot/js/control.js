const clock = document.querySelector("#clock");
const banner = document.querySelector("#banner");
const views = document.querySelectorAll(".view");
const classifier = document.querySelector("#classifier");
const failureStatus = document.querySelector("#failure-status");
const trafficEndpoint = document.querySelector("#traffic-endpoint");

let snapshot = null;
let failures = [];
let groups = [];
let traffic = null;
let selectedId = null;
let detail = null;
let view = "board";
let busy = false;
let windowPeriod = 1;
let activityRows = [];
let activityRequest = 0;

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function clockTick() {
  clock.textContent = new Date().toISOString().slice(11, 19);
}

function shortType(value) {
  const text = value || "";
  const slash = text.lastIndexOf(".");
  return slash >= 0 ? text.slice(slash + 1) : text;
}

function stamp(value) {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toISOString().slice(11, 19);
}

function formatCount(value) {
  const number = Number(value) || 0;
  return Number.isInteger(number) ? String(number) : number.toFixed(2);
}

function formatRate(value) {
  const number = Number(value) || 0;
  return `${number < 10 ? number.toFixed(2) : Math.round(number)} /s`;
}

function formatDuration(milliseconds) {
  const number = Number(milliseconds) || 0;
  if (number >= 1000) return `${(number / 1000).toFixed(2)} s`;
  if (number >= 10) return `${Math.round(number)} ms`;
  return `${number.toFixed(1)} ms`;
}

function niceMax(peak, minimum) {
  const value = Number(peak) || 0;
  if (value <= 0) return minimum;
  let upper = 10;
  while (value > upper) upper *= 10;
  upper /= 10;
  return Math.max(Math.floor(value / upper) * upper + upper, minimum);
}

function plotPaths(trace, minimum) {
  const values = trace?.points?.length ? trace.points.map(value => Number(value) || 0) : [0];
  const average = Number(trace?.average) || 0;
  const maxY = niceMax(Math.max(...values, average * 1.5), minimum);
  const step = values.length === 1 ? 0 : 100 / (values.length - 1);
  const line = values.map((value, index) => `${index ? "L" : "M"}${index * step} ${value}`).join(" ");
  const area = `M0 0 ${values.map((value, index) => `L${index * step} ${value}`).join(" ")} L100 0 Z`;
  return { maxY, line, area, averageLine: `M0 ${average} L100 ${average}`, values, average };
}

function plotSvg(paths) {
  return `<div class="plot-face" data-values="${escapeHtml(JSON.stringify(paths.values))}">
    <svg viewBox="0 0 100 ${paths.maxY}" preserveAspectRatio="none" aria-hidden="true">
      <path class="plot-fill" d="${paths.area}"></path>
      <path class="plot-line" d="${paths.line}" vector-effect="non-scaling-stroke"></path>
      <path class="plot-avg" d="${paths.averageLine}" vector-effect="non-scaling-stroke"></path>
    </svg>
    <div class="plot-readout" hidden>
      <span class="plot-cursor"></span>
      <span class="plot-tip"></span>
    </div>
  </div>`;
}

function exactCount(value) {
  const number = Number(value) || 0;
  return Number.isInteger(number) ? String(number) : number.toFixed(2);
}

function traceText(face, value) {
  const cell = face.closest(".metric-cell");
  if (cell?.classList.contains("throughput") || cell?.classList.contains("retries")) return `${exactCount(value)} /s`;
  if (cell?.classList.contains("time")) return formatDuration(value);
  return exactCount(value);
}

let plotPointer = null;

function hidePlotReadouts() {
  document.querySelectorAll(".plot-readout").forEach(node => {
    node.hidden = true;
  });
}

function showPlotReadout(face, clientX) {
  let values = [];
  try {
    values = JSON.parse(face.dataset.values || "[]");
  } catch {
    return;
  }
  if (!values.length) return;

  const width = face.getBoundingClientRect().width;
  const ratio = width <= 0 ? 0 : Math.min(1, Math.max(0, (clientX - face.getBoundingClientRect().left) / width));
  const index = values.length === 1 ? 0 : Math.round(ratio * (values.length - 1));
  const at = values.length === 1 ? 0 : index / (values.length - 1);
  const readout = face.querySelector(".plot-readout");
  hidePlotReadouts();
  readout.hidden = false;
  readout.querySelector(".plot-cursor").style.left = `${at * 100}%`;
  const tip = readout.querySelector(".plot-tip");
  tip.style.left = `${at * 100}%`;
  tip.style.transform = at < 0.15 ? "translateX(0)" : at > 0.85 ? "translateX(-100%)" : "translateX(-50%)";
  tip.textContent = traceText(face, values[index]);
}

function refreshPlotUnderPointer() {
  if (!plotPointer) return;
  const face = document.elementFromPoint(plotPointer.x, plotPointer.y)?.closest(".plot-face");
  if (!face) {
    hidePlotReadouts();
    return;
  }
  showPlotReadout(face, plotPointer.x);
}

function span(value) {
  if (!value) return "—";
  const match = String(value).match(/(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?/);
  if (!match) return value;
  const seconds = Number(match[1]) * 3600 + Number(match[2]) * 60 + Number(match[3]);
  const fraction = match[4] ? Number(`0.${match[4]}`) : 0;
  const total = seconds + fraction;
  return total < 1 ? `${Math.round(total * 1000)} ms` : `${total.toFixed(2)} s`;
}

async function getJson(path) {
  const response = await fetch(path, { headers: { accept: "application/json" } });
  if (!response.ok) {
    const error = new Error(`${response.status} ${path}`);
    error.status = response.status;
    throw error;
  }
  return response.json();
}

function showBanner(message) {
  banner.hidden = !message;
  banner.textContent = message || "";
}

function renderBoard() {
  const live = snapshot?.link === "live";
  document.querySelector("#license").textContent = live
    ? `License ${snapshot.license || "unknown"}`
    : snapshot?.link === "down"
      ? "Link down"
      : "Link dark";
  const quiet = (snapshot?.endpoints || []).filter(row => row.status === "dead" || row.status === "down").length;
  document.querySelector("#meter-beats").textContent = live ? String(snapshot.heartbeatsActive) : "—";
  document.querySelector("#meter-beats-note").textContent = live
    ? `${quiet} silent`
    : "waiting";
  document.querySelector("#meter-faults").textContent = live ? String(snapshot.unresolved) : "—";
  document.querySelector("#meter-checks").textContent = live
    ? `${snapshot.checksPassing}/${snapshot.checksPassing + snapshot.checksFailing}`
    : "—";
  document.querySelector("#meter-checks-note").textContent = live
    ? `${snapshot.checksFailing} failing`
    : "custom checks";
  document.querySelector("#meter-beats").closest(".meter").classList.toggle("ok", live && quiet === 0 && snapshot.heartbeatsActive > 0);
  document.querySelector("#meter-beats").closest(".meter").classList.toggle("fault", live && quiet > 0);
  document.querySelector("#meter-faults").closest(".meter").classList.toggle("ok", live && snapshot.unresolved === 0);
  document.querySelector("#meter-faults").closest(".meter").classList.toggle("fault", live && snapshot.unresolved > 0);
  document.querySelector("#meter-checks").closest(".meter").classList.toggle("ok", live && snapshot.checksFailing === 0);
  document.querySelector("#meter-checks").closest(".meter").classList.toggle("warn", live && snapshot.checksFailing > 0);
  const queues = document.querySelector("#meter-queues");
  const monitoringLive = live && snapshot.monitoring === "live";
  queues.classList.toggle("meter-dark", !monitoringLive);
  queues.classList.toggle("ok", monitoringLive);
  document.querySelector("#meter-queues-value").textContent = monitoringLive
    ? String(snapshot.queueLength ?? 0)
    : snapshot?.monitoring === "down"
      ? "DOWN"
      : "DARK";
  document.querySelector("#meter-queues-note").textContent = monitoringLive
    ? "open activity"
    : snapshot?.monitoring === "down"
      ? "no answer"
      : "monitoring off";
  document.querySelector("#check-count").textContent = live ? `${snapshot.checksFailing} fail` : "";

  const endpoints = document.querySelector("#endpoints");
  endpoints.innerHTML = (snapshot?.endpoints || []).map(row => `
    <div class="row">
      <span class="lamp ${escapeHtml(row.status)}"></span>
      <span>${escapeHtml(row.name)}<small>${escapeHtml(row.host)} · ${escapeHtml(stamp(row.lastReportAt))}</small></span>
      <span>${escapeHtml(row.status)}</span>
    </div>`).join("") || `<p class="empty">No endpoints reported.</p>`;

  const checks = document.querySelector("#checks");
  checks.innerHTML = (snapshot?.checks || []).map(row => `
    <div class="row">
      <span class="lamp ${escapeHtml(row.status)}"></span>
      <span>${escapeHtml(row.name)}<small>${escapeHtml(row.category)}${row.reason ? ` · ${escapeHtml(row.reason)}` : ""}</small></span>
      <span>${escapeHtml(row.status)}</span>
    </div>`).join("") || `<p class="empty">No checks reported.</p>`;

  renderLog();
}

function renderGroups() {
  const host = document.querySelector("#groups");
  host.innerHTML = groups.map(row => `
    <div class="group">
      <span>${escapeHtml(String(row.count))}</span>
      <span>${escapeHtml(row.title || row.type)}<small>${escapeHtml(!row.operationStatus || row.operationStatus === "None" ? "idle" : row.operationStatus)} · ${escapeHtml(row.type)}</small></span>
      <span class="actions">
        <button type="button" class="action retry" data-group-retry="${escapeHtml(row.id)}">Retry</button>
        <button type="button" class="action archive" data-group-archive="${escapeHtml(row.id)}">Archive</button>
      </span>
    </div>`).join("") || `<p class="empty">No open groups.</p>`;
}

function renderFailures() {
  const host = document.querySelector("#failures");
  host.innerHTML = failures.map(row => `
    <button type="button" class="row" data-failure="${escapeHtml(row.id)}" aria-current="${row.id === selectedId}">
      <span class="lamp ${row.status === "archived" ? "warn" : "fail"}"></span>
      <span>${escapeHtml(shortType(row.messageType))}<small>${escapeHtml(shortType(row.exceptionType))} · ${escapeHtml(row.receivingEndpoint || "")}</small></span>
      <span>${escapeHtml(stamp(row.failedAt))}</span>
    </button>`).join("") || `<p class="empty">No messages in this list.</p>`;
}

function renderDetail() {
  const host = document.querySelector("#detail");
  document.querySelector("#detail-id").textContent = selectedId || "none";
  if (!detail) {
    host.innerHTML = "Select a message.";
    return;
  }

  host.innerHTML = `
    <p>${escapeHtml(detail.messageType || "")}</p>
    <small>${escapeHtml(detail.status)} · ${escapeHtml(detail.receivingEndpoint || "")} · ${escapeHtml(stamp(detail.failedAt))}</small>
    <p>${escapeHtml(detail.exceptionType || "")}</p>
    <p>${escapeHtml(detail.exceptionMessage || "")}</p>
    <div class="actions">
      <button type="button" class="action retry" id="retry-one">Retry</button>
      <button type="button" class="action archive" id="archive-one">Archive</button>
    </div>
    <pre>${escapeHtml(detail.stackTrace || "No stack trace.")}</pre>
    <pre>${escapeHtml(detail.body || "No body.")}</pre>`;
}

function renderTraffic() {
  const note = document.querySelector("#traffic-note");
  note.hidden = !traffic?.note;
  note.textContent = traffic?.note || "";
  const body = document.querySelector("#traffic");
  const rows = traffic?.messages || [];
  body.innerHTML = rows.map(row => `
    <tr>
      <td>${escapeHtml(shortType(row.messageType))}</td>
      <td>${escapeHtml(row.from || "—")}</td>
      <td>${escapeHtml(row.to || "—")}</td>
      <td>${escapeHtml(span(row.criticalTime))}</td>
      <td>${escapeHtml(span(row.processingTime))}</td>
      <td>${escapeHtml(stamp(row.sentAt))}</td>
    </tr>`).join("") || `<tr><td colspan="6" class="empty">No audited messages.</td></tr>`;
}

function renderLog() {
  const host = document.querySelector("#events");
  host.innerHTML = (snapshot?.events || []).map(row => `
    <div class="row">
      <span class="lamp ${escapeHtml(row.severity)}"></span>
      <span>${escapeHtml(row.description)}<small>${escapeHtml(row.category)} · ${escapeHtml(row.eventType)}</small></span>
      <span>${escapeHtml(stamp(row.raisedAt))}</span>
    </div>`).join("") || `<p class="empty">No events.</p>`;
}

function metricValue(trace, format, stale) {
  if (stale) return "?";
  return format(trace?.average);
}

function metricCell(label, kind, trace, format, minimum, stale) {
  const paths = plotPaths(trace, minimum);
  return `<div class="metric-cell ${kind}">
    <p>${escapeHtml(label)}</p>
    <strong>${escapeHtml(metricValue(trace, format, stale))}</strong>
    ${plotSvg(paths)}
  </div>`;
}

function renderActivity() {
  const live = snapshot?.monitoring === "live" || activityRows.length > 0;
  document.querySelector("#activity-note").textContent = live
    ? "Average for the window."
    : snapshot?.monitoringNote || "Queue length is dark.";
  document.querySelectorAll(".period").forEach(button => {
    button.setAttribute("aria-pressed", String(Number(button.dataset.history) === windowPeriod));
  });
  const host = document.querySelector("#activity-list");
  if (!activityRows.length) {
    host.innerHTML = `<p class="empty">${live ? "No endpoint metrics yet." : "Monitoring is dark."}</p>`;
    return;
  }

  host.innerHTML = activityRows.map(row => `<section class="endpoint-block">
      <h3 class="endpoint-name">${escapeHtml(row.name)}${row.stale ? "<small>stale</small>" : ""}</h3>
      <div class="metric-grid">
        ${metricCell("Queue length", "queue", row.queueLength, value => formatCount(Math.round(value || 0)), 10, row.stale)}
        ${metricCell("Throughput", "throughput", row.throughput, formatRate, 1, row.stale)}
        ${metricCell("Retries", "retries", row.retries, formatRate, 1, row.stale)}
        ${metricCell("Processing", "time", row.processingTime, formatDuration, 10, row.stale)}
        ${metricCell("Critical", "time", row.criticalTime, formatDuration, 10, row.stale)}
      </div>
    </section>`).join("");
  refreshPlotUnderPointer();
}

async function loadActivity() {
  const request = ++activityRequest;
  if (windowPeriod === 1 && snapshot?.activity) {
    activityRows = snapshot.activity;
    renderActivity();
    return;
  }

  const page = await getJson(`/api/control/activity?history=${windowPeriod}`);
  if (request !== activityRequest) return;
  activityRows = page.endpoints || [];
  renderActivity();
}

function showView(next) {
  view = next;
  views.forEach(button => button.setAttribute("aria-selected", String(button.dataset.view === next)));
  for (const name of ["board", "activity", "faults", "traffic", "log"]) {
    document.querySelector(`#view-${name}`).hidden = name !== next;
  }
  const hash = next === "board" ? "" : next;
  window.history.replaceState(null, "", hash ? `#${hash}` : location.pathname);
  if (next === "activity") loadActivity();
  if (next === "faults") loadFaults();
  if (next === "traffic") loadTraffic();
}

async function loadBoard() {
  snapshot = await getJson("/api/control");
  showBanner(snapshot.link === "live" ? "" : snapshot.note || "ServiceControl is not on this link.");
  renderBoard();
}

async function loadFaults() {
  const [groupPage, failurePage] = await Promise.all([
    getJson(`/api/control/groups?classifier=${encodeURIComponent(classifier.value)}`),
    getJson(`/api/control/failures?status=${encodeURIComponent(failureStatus.value)}`)
  ]);
  groups = groupPage.groups || [];
  failures = failurePage.messages || [];
  renderGroups();
  renderFailures();
  if (selectedId) {
    detail = await getJson(`/api/control/failures/${selectedId}`).catch(() => null);
    renderDetail();
  }
}

async function loadTraffic() {
  const selected = trafficEndpoint.value || "All";
  if (selected !== "All") {
    traffic = await getJson(`/api/control/traffic?endpoint=${encodeURIComponent(selected)}`);
    renderTraffic();
    return;
  }

  const names = [...trafficEndpoint.options].map(option => option.value).filter(name => name !== "All");
  const pages = await Promise.all(names.map(name =>
    getJson(`/api/control/traffic?endpoint=${encodeURIComponent(name)}`).catch(() => null)));
  const messages = pages.flatMap(page => page?.messages || []);
  messages.sort((left, right) => String(right.processedAt || right.sentAt || "").localeCompare(String(left.processedAt || left.sentAt || "")));
  const unanswered = pages.filter(page => !page?.available);
  traffic = {
    available: pages.some(page => page?.available),
    endpoint: "All",
    note: unanswered.length === pages.length ? (unanswered[0]?.note || "The audit instance did not answer.") : "",
    messages
  };
  renderTraffic();
}

async function postAction(path, body) {
  if (busy) return;
  busy = true;
  try {
    const response = await fetch(path, {
      method: "POST",
      headers: { "content-type": "application/json", accept: "application/json" },
      body: body ? JSON.stringify(body) : "{}"
    });
    if (!response.ok) {
      showBanner(`Action failed (${response.status}).`);
      return;
    }
    showBanner("");
    await Promise.all([loadBoard(), loadFaults()]);
  } finally {
    busy = false;
  }
}

document.addEventListener("click", event => {
  if (event.target.closest("#meter-queues")) showView("activity");

  const period = event.target.closest(".period");
  if (period) {
    windowPeriod = Number(period.dataset.history);
    loadActivity().catch(() => showBanner("Activity did not answer."));
  }

  const viewButton = event.target.closest(".view");
  if (viewButton) showView(viewButton.dataset.view);

  const failure = event.target.closest("[data-failure]");
  if (failure) {
    selectedId = failure.dataset.failure;
    renderFailures();
    getJson(`/api/control/failures/${selectedId}`).then(row => {
      detail = row;
      renderDetail();
    }).catch(() => showBanner("That fault could not be loaded."));
  }

  if (event.target.id === "retry-one" && selectedId) postAction(`/api/control/failures/${selectedId}/retry`);
  if (event.target.id === "archive-one" && selectedId) postAction(`/api/control/failures/${selectedId}/archive`);

  const groupRetry = event.target.closest("[data-group-retry]");
  if (groupRetry) postAction("/api/control/groups/retry", { id: groupRetry.dataset.groupRetry });
  const groupArchive = event.target.closest("[data-group-archive]");
  if (groupArchive) postAction("/api/control/groups/archive", { id: groupArchive.dataset.groupArchive });
});

classifier.addEventListener("change", loadFaults);
failureStatus.addEventListener("change", () => {
  selectedId = null;
  detail = null;
  renderDetail();
  loadFaults();
});
trafficEndpoint.addEventListener("change", loadTraffic);

const activityList = document.querySelector("#activity-list");
activityList.addEventListener("pointermove", event => {
  const face = event.target.closest(".plot-face");
  if (!face) {
    plotPointer = null;
    hidePlotReadouts();
    return;
  }
  plotPointer = { x: event.clientX, y: event.clientY };
  showPlotReadout(face, event.clientX);
});
activityList.addEventListener("pointerleave", () => {
  plotPointer = null;
  hidePlotReadouts();
});

clockTick();
setInterval(clockTick, 1000);

const pollEveryMs = 1000;

async function poll() {
  const started = performance.now();
  try {
    await loadBoard();
    if (view === "activity") await loadActivity();
    if (view === "faults") await loadFaults();
    if (view === "traffic") await loadTraffic();
  } catch (error) {
    showBanner(error.status ? `Control room ${error.status}` : "Control room did not answer.");
  } finally {
    const elapsed = performance.now() - started;
    window.setTimeout(poll, Math.max(0, pollEveryMs - elapsed));
  }
}

const hash = location.hash.replace(/^#/, "");
if (hash === "activity" || hash.startsWith("activity/")) {
  view = "activity";
} else if (["faults", "traffic", "log"].includes(hash)) {
  view = hash;
}

showView(view);
poll();
