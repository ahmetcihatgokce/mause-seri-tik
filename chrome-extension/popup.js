const { defaults, normalize } = globalThis.MauseSeriTikSettings;
const form = document.querySelector("form");
const mode = document.querySelector("#mode");
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
  rateValue.value = `${rate.value} tık/sn`;
  burstValue.value = burst.value;
  rate.setAttribute("aria-valuetext", `${rate.value} tıklama/saniye`);
  burst.disabled = mode.value === "hold";
  burstField.setAttribute("aria-disabled", String(burst.disabled));
}

function readSettings() {
  return normalize({ mode: mode.value, rate: rate.valueAsNumber, burst: burst.valueAsNumber });
}

async function send(action, settings) {
  try {
    return await chrome.tabs.sendMessage(
      tabId,
      { namespace: "mause-seri-tik", action, settings },
      { frameId: 0 },
    );
  } catch {
    return null;
  }
}

async function toggleExtension() {
  toggle.disabled = true;
  try {
    await pendingSave;
    let response;
    if (enabled) {
      response = await send("disable");
    } else {
      const current = await send("status");
      if (!current) {
        await chrome.scripting.executeScript({
          target: { tabId },
          files: ["settings.js", "content.js"],
        });
      }
      response = await send("enable", readSettings());
    }
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

function saveSettings() {
  const settings = readSettings();
  pendingSave = pendingSave.then(async () => {
    await chrome.storage.local.set({ settings });
    if (enabled) {
      const response = await send("update", settings);
      enabled = response?.enabled === true;
      renderActivation();
    }
  }).catch(() => {
    setStatus("Ayarlar kaydedilemedi.", "error");
  });
}

async function initialize() {
  const { settings = defaults } = await chrome.storage.local.get("settings");
  const saved = normalize(settings);
  mode.value = saved.mode;
  rate.value = saved.rate;
  burst.value = saved.burst;
  renderSettings();

  form.addEventListener("input", renderSettings);
  form.addEventListener("change", saveSettings);
  toggle.addEventListener("click", toggleExtension);

  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab?.id || !/^https?:\/\//.test(tab.url ?? "")) {
    setStatus("Bir web sayfası açıp tekrar dene.", "error");
    return;
  }

  tabId = tab.id;
  const response = await send("status");
  enabled = response?.enabled === true;
  renderActivation();
  toggle.disabled = false;
}

initialize().catch(() => {
  setStatus("Eklenti ayarları açılırken hata oluştu.", "error");
});
