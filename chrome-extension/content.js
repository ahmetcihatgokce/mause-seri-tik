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
  let sessionId = null;
  let stopChannel = null;
  let press = null;
  let timeout = null;

  function stopSequence(release = "both") {
    window.clearTimeout(timeout);
    timeout = null;
    const current = press;
    press = null;
    if (current?.syntheticDown && release !== "native") {
      finishSyntheticPress(current, release);
    }
  }

  function stopExtension(notify = false) {
    if (notify) {
      stopChannel?.postMessage("stop");
    }
    stopChannel?.close();
    stopChannel = null;
    sessionId = null;
    controls?.abort();
    controls = null;
    stopSequence();
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

  function mouseDetails(current, buttons) {
    return {
      ...current.position,
      bubbles: true,
      cancelable: true,
      composed: true,
      view: window,
      button: 0,
      buttons,
    };
  }

  function dispatchPointer(current, type, details) {
    current.target.dispatchEvent(new PointerEvent(type, {
      ...details, pointerId: current.pointerId, pointerType: "mouse", isPrimary: true,
    }));
  }

  function dispatchDown(current) {
    current.syntheticDown = true;
    const details = mouseDetails(current, current.otherButtons | 1);
    dispatchPointer(current, "pointerdown", { ...details, pressure: 0.5 });
    if (press === current) {
      current.target.dispatchEvent(new MouseEvent("mousedown", { ...details, detail: 1 }));
    }
  }

  function dispatchUp(current, release = "both") {
    const details = mouseDetails(current, current.otherButtons);
    dispatchPointer(current, "pointerup", details);
    if (release === "both") {
      current.target.dispatchEvent(new MouseEvent("mouseup", { ...details, detail: 1 }));
    }
  }

  function finishSyntheticPress(current, release = "both", click = false) {
    current.syntheticDown = false;
    dispatchUp(current, release);
    if (click && press === current) {
      const details = mouseDetails(current, current.otherButtons);
      dispatchPointer(current, "click", { ...details, detail: 1 });
    }
  }

  function schedule(current) {
    if (press !== current) {
      return;
    }
    if (current.remaining > 0 || current.syntheticDown) {
      timeout = window.setTimeout(tick, current.delay);
    } else {
      stopSequence();
    }
  }

  function tick() {
    timeout = null;
    const current = press;
    if (!current?.target.isConnected || document.hidden) {
      stopSequence();
      return;
    }

    if (current.initialGameRelease) {
      // Give a frame-polled game a released interval after the natural press.
      current.initialGameRelease = false;
      dispatchUp(current);
    } else if (current.syntheticDown) {
      finishSyntheticPress(current, "both", true);
    } else {
      current.remaining -= 1;
      dispatchDown(current);
      if (current.fast && press === current) {
        finishSyntheticPress(current, "both", true);
      }
    }
    schedule(current);
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

    const fast = settings.profile === "fast";
    press = {
      target,
      pointerId: event.pointerId,
      position: pointerPosition(event),
      otherButtons: event.buttons & ~1,
      remaining: settings.mode === "burst" ? settings.burst - 1 : Infinity,
      delay: fast ? Math.ceil(1000 / settings.rate) : Math.ceil(500 / settings.gameRate),
      initialGameRelease: !fast,
      syntheticDown: false,
      fast,
    };
    schedule(press);
  }

  function onPointerUp(event) {
    if (event.isTrusted && press?.pointerId === event.pointerId && !(event.buttons & 1)) {
      stopSequence("native");
    }
  }

  function onMouseUp(event) {
    if (event.isTrusted && event.button === 0) {
      if (press) {
        press.otherButtons = event.buttons & ~1;
      }
      // A chorded mouseup has no corresponding native pointerup.
      stopSequence(event.buttons & ~1 ? "pointer" : "native");
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
    press.position = pointerPosition(event);
    press.otherButtons = event.buttons & ~1;
    if (!(event.buttons & 1)) {
      stopSequence(event.button === 0 && press.otherButtons ? "pointer" : "both");
    }
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
    const target = document.pointerLockElement;
    if (!target) {
      stopSequence();
    } else if (press && !press.syntheticDown) {
      press.target = target;
    }
  }

  function onKeyDown(event) {
    if (event.isTrusted && event.key === "Escape") {
      stopExtension(true);
    }
  }

  function enable(nextSessionId) {
    stopSequence();
    stopChannel?.close();
    sessionId = typeof nextSessionId === "string" ? nextSessionId : null;
    stopChannel = sessionId ? new BroadcastChannel(`mause-seri-tik:${sessionId}`) : null;
    if (stopChannel) {
      stopChannel.onmessage = event => {
        if (event.data === "stop") {
          stopExtension();
        }
      };
    }

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
    window.addEventListener("blur", () => stopSequence(), { signal: controls.signal });
    window.addEventListener("pagehide", () => stopExtension(), listenerOptions);
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
        enable(message.sessionId);
        break;
      case "disable":
        stopExtension();
        break;
      default:
        return;
    }
    reply({ enabled: controls !== null, sessionId, settings });
  });
})();
