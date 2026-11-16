(() => {
  const sampleSize = 96;
  const maxRadius = 100;
  const maxStep = 38;
  const tolerance = 52;
  let capture = null;
  let failedCanvas = null;

  function stop() {
    if (capture) {
      capture.stream.getTracks().forEach((track) => track.stop());
      capture.video.srcObject = null;
      capture = null;
    }
    failedCanvas = null;
  }

  function watch(canvas) {
    if (!canvas || capture?.canvas === canvas || failedCanvas === canvas) {
      return;
    }

    stop();
    let stream;
    try {
      const buffer = document.createElement("canvas");
      buffer.width = sampleSize;
      buffer.height = sampleSize;
      const context = buffer.getContext("2d", { willReadFrequently: true });
      if (!context) {
        return;
      }

      stream = canvas.captureStream(30);
      const video = document.createElement("video");
      video.muted = true;
      video.playsInline = true;
      video.srcObject = stream;
      const current = { canvas, video, stream, context };
      capture = current;
      video.play().catch(() => {
        if (capture === current) {
          stop();
          failedCanvas = canvas;
        }
      });
    } catch {
      stream?.getTracks().forEach((track) => track.stop());
      failedCanvas = canvas;
    }
  }

  function matchTarget(pixels, color) {
    const red = Number.parseInt(color.slice(1, 3), 16);
    const green = Number.parseInt(color.slice(3, 5), 16);
    const blue = Number.parseInt(color.slice(5, 7), 16);
    const matches = new Uint8Array(sampleSize * sampleSize);
    let matchingPixels = 0;

    for (let i = 0; i < matches.length; i += 1) {
      const offset = i * 4;
      const dr = pixels[offset] - red;
      const dg = pixels[offset + 1] - green;
      const db = pixels[offset + 2] - blue;
      if (pixels[offset + 3] > 200 && dr * dr + dg * dg + db * db <= tolerance * tolerance) {
        matches[i] = 1;
        matchingPixels += 1;
      }
    }

    // Large areas of the selected color are usually scenery rather than a distinct target.
    if (matchingPixels > matches.length / 5) {
      return null;
    }

    const queue = new Int32Array(matches.length);
    const center = (sampleSize - 1) / 2;
    let nearest = null;
    let closest = Infinity;
    let secondClosest = Infinity;
    for (let start = 0; start < matches.length; start += 1) {
      if (!matches[start]) {
        continue;
      }
      let length = 1;
      let sumX = 0;
      let sumY = 0;
      let left = sampleSize;
      let top = sampleSize;
      let right = 0;
      let bottom = 0;
      queue[0] = start;
      matches[start] = 0;

      for (let head = 0; head < length; head += 1) {
        const index = queue[head];
        const x = index % sampleSize;
        const y = Math.floor(index / sampleSize);
        sumX += x;
        sumY += y;
        left = Math.min(left, x);
        top = Math.min(top, y);
        right = Math.max(right, x);
        bottom = Math.max(bottom, y);
        for (let row = Math.max(0, y - 1); row <= Math.min(sampleSize - 1, y + 1); row += 1) {
          for (let column = Math.max(0, x - 1); column <= Math.min(sampleSize - 1, x + 1); column += 1) {
            const neighbor = row * sampleSize + column;
            if (matches[neighbor]) {
              matches[neighbor] = 0;
              queue[length++] = neighbor;
            }
          }
        }
      }

      const width = right - left + 1;
      const height = bottom - top + 1;
      const shortestSide = Math.min(width, height);
      if (length < 5 || length > 620 || shortestSide < 2 ||
          Math.max(width, height) > shortestSide * 6 || length * 5 < width * height) {
        continue;
      }

      const x = sumX / length - center;
      const y = sumY / length - center;
      const distance = x * x + y * y;
      if (distance < closest && distance <= center * center) {
        secondClosest = closest;
        closest = distance;
        nearest = { x, y };
      } else if (distance < secondClosest) {
        secondClosest = distance;
      }
    }
    return secondClosest - closest > Math.max(4, closest / 10) ? nearest : null;
  }

  function adjustment(canvas, color) {
    watch(canvas);
    if (capture?.canvas !== canvas || capture.video.readyState < 2) {
      return null;
    }

    const { video, context } = capture;
    const { width, height } = canvas.getBoundingClientRect();
    const radius = Math.min(maxRadius, Math.min(width, height) * 0.24);
    if (radius < 10 || !video.videoWidth || !video.videoHeight) {
      return null;
    }

    try {
      const cropWidth = (radius * 2 / width) * video.videoWidth;
      const cropHeight = (radius * 2 / height) * video.videoHeight;
      context.drawImage(video, (video.videoWidth - cropWidth) / 2, (video.videoHeight - cropHeight) / 2,
        cropWidth, cropHeight, 0, 0, sampleSize, sampleSize);
      const target = matchTarget(context.getImageData(0, 0, sampleSize, sampleSize).data, color);
      if (!target) {
        return null;
      }

      const distance = Math.hypot(target.x, target.y) * radius * 2 / sampleSize;
      if (distance < 2) {
        return null;
      }

      const scale = (radius * 2 / sampleSize) * Math.min(0.85, maxStep / distance);
      return { movementX: Math.round(target.x * scale), movementY: Math.round(target.y * scale) };
    } catch {
      stop();
      failedCanvas = canvas;
      return null;
    }
  }

  globalThis.MauseSeriTikAim = Object.freeze({ watch, stop, adjustment });
})();
