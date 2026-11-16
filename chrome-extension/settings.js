(() => {
  const rates = Object.freeze({ game: 25, fast: 250 });
  const defaults = Object.freeze({
    mode: "hold", profile: "game", rate: rates.fast, gameRate: 20, burst: 5,
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
    };
  }

  globalThis.MauseSeriTikSettings = Object.freeze({ defaults, normalize, rates });
})();
