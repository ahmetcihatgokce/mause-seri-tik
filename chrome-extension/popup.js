const { defaults, normalize, rates } = globalThis.MauseSeriTikSettings;
const form = document.querySelector("form");
const mode = document.querySelector("#mode");
const profile = document.querySelector("#profile");
const rate = document.querySelector("#rate");
const burst = document.querySelector("#burst");
const burstField = document.querySelector("#burst-field");
const rateValue = document.querySelector("#rate-value");
const burstValue = document.querySelector("#burst-value");
const status = document.querySelector("#status");
const toggle = document.querySelector("#toggle");

let tabId = null;
let enabled = false;
let pendingSave = Promise.resolve();
let selected = defaults;

function setStatus(text, state = "") {
  status.textContent = text;
  status.dataset.state = state;
}

function renderActivation() {
  const state = enabled ? "active" : "";
  toggle.dataset.state = state;
  toggle.textContent = enabled ? "Bu sekmede durdur" : "Bu sekmede başlat";
  setStatus(enabled ? "Bu sekmede etkin" : "Bu sekmede kapalı", state);
}

function renderSettings() {
  mode.value = selected.mode;
  profile.value = selected.profile;
  rate.max = rates[selected.profile];
  rate.value = selected.profile === "game" ? selected.gameRate : selected.rate;
  burst.value = selected.burst;
  rateValue.value = `${rate.value} tık/sn`;
  burstValue.value = burst.value;
  rate.setAttribute("aria-valuetext", `${rate.value} tıklama/saniye`);
  burst.disabled = mode.value === "hold";
  burstField.setAttribute("aria-disabled", String(burst.disabled));
}

function changeSettings(event) {
  const next = {
    ...selected, mode: mode.value, profile: profile.value, burst: burst.valueAsNumber,
  };
  if (event.target === rate) {
    const rateKey = next.profile === "game" ? "gameRate" : "rate";
    next[rateKey] = rate.valueAsNumber;
  }
  selected = normalize(next);
  renderSettings();
}

async function send(action, settings, options, sessionId) {
  try {
    return await chrome.tabs.sendMessage(
      tabId,
      { namespace: "mause-seri-tik", action, settings, sessionId },
      options,
    );
  } catch {
    return null;
  }
}

function tabStatus() {
  return send("status", undefined, { frameId: 0 });
}

async function enableTab() {
  const files = ["settings.js", "content.js"];
  let frames;
  try {
    frames = await chrome.scripting.executeScript({ target: { tabId, allFrames: true }, files });
  } catch {
    // Chrome can reject the whole injection if a frame is on a different origin.
    frames = await chrome.scripting.executeScript({ target: { tabId }, files });
  }

  const sessionId = crypto.randomUUID();
  await Promise.all(frames.map(({ frameId, documentId }) =>
    send("enable", selected, documentId ? { documentId } : { frameId }, sessionId),
  ));
}

async function toggleExtension() {
  toggle.disabled = true;
  try {
    await pendingSave;
    if (enabled) {
      await send("disable");
    } else {
      await enableTab();
    }
    const response = await tabStatus();
    enabled = response?.enabled === true;
    if (response) {
      renderActivation();
    } else {
      setStatus("Sekmeye erişilemedi. Sayfayı kontrol et.", "error");
    }
  } catch {
    setStatus("Bu sayfada seri tıklama başlatılamadı.", "error");
  } finally {
    toggle.disabled = false;
  }
}

function saveSettings(event) {
  changeSettings(event);
  const settings = selected;
  pendingSave = pendingSave.then(async () => {
    await chrome.storage.local.set({ settings });
    if (enabled) {
      await send("update", settings);
      const response = await tabStatus();
      enabled = response?.enabled === true;
      renderActivation();
    }
  }).catch(() => {
    setStatus("Ayarlar kaydedilemedi.", "error");
  });
}

async function initialize() {
  const { settings = defaults } = await chrome.storage.local.get("settings");
  selected = normalize(settings);
  renderSettings();

  form.addEventListener("input", changeSettings);
  form.addEventListener("change", saveSettings);
  toggle.addEventListener("click", toggleExtension);

  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab?.id || !/^https?:\/\//.test(tab.url ?? "")) {
    setStatus("Bir web sayfası açıp tekrar dene.", "error");
    return;
  }

  tabId = tab.id;
  const response = await tabStatus();
  enabled = response?.enabled === true;
  renderActivation();
  toggle.disabled = false;
}

initialize().catch(() => {
  setStatus("Eklenti ayarları açılırken hata oluştu.", "error");
});
