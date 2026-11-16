(() => {
  if (globalThis.mauseSeriTikInitialized) {
    return;
  }
  globalThis.mauseSeriTikInitialized = true;

  const { defaults, normalize } = globalThis.MauseSeriTikSettings;
  const typingInputTypes = new Set([
    "date", "datetime-local", "email", "file", "month", "number", "password",
    "search", "tel", "text", "time", "url", "week",
  ]);
  let settings = defaults;
  let controls = null;
  let press = null;
  let timeout = null;

  function stopSequence() {
    window.clearTimeout(timeout);
    timeout = null;
    press = null;
  }

  function stopExtension() {
    stopSequence();
    controls?.abort();
    controls = null;
  }

  function pointerPosition(event) {
    return {
      screenX: event.screenX,
      screenY: event.screenY,
      clientX: event.clientX,
      clientY: event.clientY,
      altKey: event.altKey,
      ctrlKey: event.ctrlKey,
      metaKey: event.metaKey,
      shiftKey: event.shiftKey,
    };
  }

  function dispatchClick(current) {
    const { target, pointerId, position, otherButtons } = current;
    const common = {
      ...position,
      bubbles: true,
      cancelable: true,
      composed: true,
      view: window,
      button: 0,
    };
    const pointer = { ...common, pointerId, pointerType: "mouse", isPrimary: true };

    target.dispatchEvent(new PointerEvent("pointerdown", {
      ...pointer, buttons: otherButtons | 1, pressure: 0.5,
    }));
    target.dispatchEvent(new MouseEvent("mousedown", {
      ...common, buttons: otherButtons | 1, detail: 1,
    }));
    target.dispatchEvent(new PointerEvent("pointerup", {
      ...pointer, buttons: otherButtons,
    }));
    target.dispatchEvent(new MouseEvent("mouseup", {
      ...common, buttons: otherButtons, detail: 1,
    }));
    target.dispatchEvent(new PointerEvent("click", {
      ...pointer, buttons: otherButtons, detail: 1,
    }));
  }

  function schedule() {
    if (!press || (settings.mode === "burst" && press.burstRemaining === 0)) {
      stopSequence();
      return;
    }

    if (timeout === null) {
      timeout = window.setTimeout(tick, Math.ceil(1000 / settings.rate));
    }
  }

  function tick() {
    timeout = null;
    const current = press;
    if (!current?.target.isConnected || document.hidden) {
      stopSequence();
      return;
    }

    if (settings.mode === "burst") {
      current.burstRemaining -= 1;
    }
    dispatchClick(current);
    schedule();
  }

  function isTypingTarget(target) {
    return target.isContentEditable
      || target.closest("textarea, select, [role='textbox']") !== null
      || (target instanceof HTMLInputElement && typingInputTypes.has(target.type));
  }

  function onPointerDown(event) {
    if (!event.isTrusted || event.pointerType !== "mouse" || event.button !== 0) {
      return;
    }

    stopSequence();
    const target = document.pointerLockElement ?? event.composedPath()[0];
    if (!(target instanceof Element) || isTypingTarget(target)) {
      return;
    }

    press = {
      target,
      pointerId: event.pointerId,
      position: pointerPosition(event),
      otherButtons: event.buttons & ~1,
      burstRemaining: settings.burst - 1,
    };
    schedule();
  }

  function onPointerUp(event) {
    if (event.isTrusted && press?.pointerId === event.pointerId && !(event.buttons & 1)) {
      stopSequence();
    }
  }

  function onMouseUp(event) {
    if (event.isTrusted && event.button === 0) {
      stopSequence();
    }
  }

  function onPointerMove(event) {
    if (!event.isTrusted || event.pointerType !== "mouse") {
      return;
    }

    // Pointer events represent an additional mouse button press as a move.
    if (event.button === 0 && (event.buttons & 1) && !press) {
      onPointerDown(event);
      return;
    }
    if (!press || press.pointerId !== event.pointerId) {
      return;
    }
    if (!(event.buttons & 1)) {
      stopSequence();
      return;
    }
    press.position = pointerPosition(event);
    press.otherButtons = event.buttons & ~1;
  }

  function onPointerCancel(event) {
    if (event.isTrusted && press?.pointerId === event.pointerId) {
      stopSequence();
    }
  }

  function onMouseOut(event) {
    if (event.isTrusted && event.relatedTarget === null && !document.pointerLockElement) {
      stopSequence();
    }
  }

  function onVisibilityChange() {
    if (document.hidden) {
      stopSequence();
    }
  }

  function onPointerLockChange() {
    if (!document.pointerLockElement) {
      stopExtension();
    }
  }

  function onKeyDown(event) {
    if (event.isTrusted && event.key === "Escape") {
      stopExtension();
    }
  }

  function enable() {
    if (controls) {
      return;
    }
    controls = new AbortController();
    const listenerOptions = { capture: true, passive: true, signal: controls.signal };

    window.addEventListener("pointerdown", onPointerDown, listenerOptions);
    window.addEventListener("pointerup", onPointerUp, listenerOptions);
    window.addEventListener("mouseup", onMouseUp, listenerOptions);
    window.addEventListener("pointermove", onPointerMove, listenerOptions);
    window.addEventListener("pointercancel", onPointerCancel, listenerOptions);
    window.addEventListener("mouseout", onMouseOut, listenerOptions);
    window.addEventListener("keydown", onKeyDown, listenerOptions);
    window.addEventListener("blur", stopSequence, { signal: controls.signal });
    window.addEventListener("pagehide", stopExtension, listenerOptions);
    document.addEventListener("visibilitychange", onVisibilityChange, listenerOptions);
    document.addEventListener("pointerlockchange", onPointerLockChange, listenerOptions);
  }

  chrome.runtime.onMessage.addListener((message, sender, reply) => {
    if (sender.id !== chrome.runtime.id || message?.namespace !== "mause-seri-tik") {
      return;
    }

    switch (message.action) {
      case "status":
        break;
      case "update":
        stopSequence();
        settings = normalize(message.settings);
        break;
      case "enable":
        settings = normalize(message.settings);
        enable();
        break;
      case "disable":
        stopExtension();
        break;
      default:
        return;
    }
    reply({ enabled: controls !== null });
  });
})();
