// Camera scanning of the scan page (M1 C2). Imported as an ES module by BrowserBarcodeScanner.
// Uses the browser's BarcodeDetector; browsers without it report "not available" and the page
// offers manual entry. Camera images stay in the browser: only the decoded text goes to Blazor.

const formats = ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'qr_code'];
const pollMilliseconds = 250;
// The same code is ignored for this long so a code held in front of the camera counts once.
const repeatMilliseconds = 2000;

let session = null;
// Bumped by every start and stop: a start that finds a newer number after an await was cancelled
// (page left, or started twice while the permission prompt was open) and releases what it got.
let generation = 0;

export async function isAvailable() {
    if (!('BarcodeDetector' in window) || !navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        return false;
    }

    try {
        const supported = await window.BarcodeDetector.getSupportedFormats();
        return formats.some((format) => supported.includes(format));
    } catch (error) {
        return false;
    }
}

// Returns false when the camera cannot be started (denied, busy, missing, no decoder).
export async function start(videoId, dotnetRef) {
    stop();
    const token = generation;
    const video = document.getElementById(videoId);
    if (!video || !(await isAvailable()) || token !== generation) {
        return false;
    }

    let stream;
    try {
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false });
    } catch (error) {
        return false;
    }

    if (token !== generation) {
        stream.getTracks().forEach((track) => track.stop());
        return false;
    }

    let detector;
    try {
        const supported = await window.BarcodeDetector.getSupportedFormats();
        detector = new window.BarcodeDetector({ formats: formats.filter((format) => supported.includes(format)) });
    } catch (error) {
        stream.getTracks().forEach((track) => track.stop());
        return false;
    }

    if (token !== generation) {
        stream.getTracks().forEach((track) => track.stop());
        return false;
    }

    const current = { stream, video, dotnetRef, detector, timer: null, seen: new Map(), stopped: false };
    session = current;
    video.srcObject = stream;
    video.setAttribute('playsinline', 'true');
    try {
        await video.play();
    } catch (error) {
        // Only end our own session: a newer start may have taken over (play() then rejects with AbortError).
        if (session === current) {
            stop();
        }

        return false;
    }

    if (current.stopped) {
        return false; // stop() ran while the video was starting and already released the stream.
    }

    schedule(current);
    return true;
}

export function stop() {
    generation++;
    const current = session;
    session = null;
    if (!current) {
        return;
    }

    current.stopped = true;
    clearTimeout(current.timer);
    current.stream.getTracks().forEach((track) => track.stop());
    current.video.srcObject = null;
}

function schedule(current) {
    current.timer = setTimeout(() => poll(current), pollMilliseconds);
}

async function poll(current) {
    if (current.stopped) {
        return;
    }

    try {
        const codes = await current.detector.detect(current.video);
        const now = Date.now();
        // Sliding window per code: a code that stays in view keeps being "the same one"; it only
        // counts again after it was out of view for repeatMilliseconds (a held can is used once,
        // and two codes in one frame do not trigger each other).
        const fresh = [];
        for (const code of codes) {
            const text = code.rawValue;
            if (!text) {
                continue;
            }

            const seenAt = current.seen.get(text);
            current.seen.set(text, now);
            if (seenAt === undefined || now - seenAt > repeatMilliseconds) {
                fresh.push(text);
            }
        }

        // One code per frame; the rest is picked up by a later frame if still in view and not yet seen.
        if (fresh.length > 0) {
            // Blazor answers when the whole flow (dialogs included) is done, so no code is
            // handled twice at once.
            await current.dotnetRef.invokeMethodAsync('OnCode', fresh[0]);
            // Whatever is in front of the camera right after a flow is not a new scan.
            const after = Date.now();
            current.seen.forEach((value, key) => current.seen.set(key, after));
        }
    } catch (error) {
        // A frame that cannot be decoded, or a page that went away: try again, stop() ends the loop.
    }

    if (!current.stopped) {
        schedule(current);
    }
}
