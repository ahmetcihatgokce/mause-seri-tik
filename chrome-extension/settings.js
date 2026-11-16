(() => {
  const defaults = Object.freeze({ mode: "hold", rate: 250, burst: 5 });

  function wholeNumber(value, minimum, maximum, fallback) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return fallback;
    }
    return Math.min(maximum, Math.max(minimum, Math.round(value)));
  }

  function normalize(value) {
    return {
      mode: value?.mode === "burst" ? "burst" : defaults.mode,
      rate: wholeNumber(value?.rate, 1, 250, defaults.rate),
      burst: wholeNumber(value?.burst, 2, 30, defaults.burst),
    };
  }

  globalThis.MauseSeriTikSettings = Object.freeze({ defaults, normalize });
})();
