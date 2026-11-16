(() => {
  const rates = Object.freeze({ game: 25, fast: 250 });
  const defaults = Object.freeze({
    mode: "hold", profile: "game", rate: rates.fast, gameRate: 20, burst: 5,
    aimAssist: false, aimColor: "#ff3b30", autoSprint: false,
  });

  function wholeNumber(value, minimum, maximum, fallback) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return fallback;
    }
    return Math.min(maximum, Math.max(minimum, Math.round(value)));
  }

  function normalize(value) {
    return {
      mode: value?.mode === "burst" ? "burst" : defaults.mode,
      profile: value?.profile === "fast" ? "fast" : defaults.profile,
      rate: wholeNumber(value?.rate, 1, rates.fast, defaults.rate),
      gameRate: wholeNumber(value?.gameRate, 1, rates.game, defaults.gameRate),
      burst: wholeNumber(value?.burst, 2, 30, defaults.burst),
      aimAssist: value?.aimAssist === true,
      aimColor: typeof value?.aimColor === "string" && /^#[a-f0-9]{6}$/i.test(value.aimColor)
        ? value.aimColor.toLowerCase() : defaults.aimColor,
      autoSprint: value?.autoSprint === true,
    };
  }

  globalThis.MauseSeriTikSettings = Object.freeze({ defaults, normalize, rates });
})();
