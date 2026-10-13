// Kept as a file rather than reached through eval: a strict CSP forbids unsafe-eval,
// and interop that works today would stop working the day the header is added.
window.geteilt = {
  browserLanguages: () => Array.from(navigator.languages ?? [navigator.language ?? 'de']),

  // The Clipboard API exists only in a secure context, and the app is tried on phones over plain
  // http on the local network. The textarea route is older but works there.
  copyText: async (text) => {
    if (navigator.clipboard && window.isSecureContext) {
      try {
        await navigator.clipboard.writeText(text);
        return true;
      } catch (error) {
        console.warn('Clipboard API refused, trying the fallback', error);
      }
    }

    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.opacity = '0';
    document.body.appendChild(area);
    area.select();

    try {
      return document.execCommand('copy');
    } catch (error) {
      console.warn('Copy fallback failed', error);
      return false;
    } finally {
      area.remove();
    }
  },

  // A phone that dims while a friend aims their camera at it is the most common way a scan fails.
  // Answers whether the lock was granted, so the screen only claims what is true.
  keepScreenOn: async () => {
    window.geteilt.screenWanted = true;
    return await window.geteilt.requestWakeLock();
  },

  letScreenSleep: async () => {
    window.geteilt.screenWanted = false;
    const lock = window.geteilt.wakeLock;
    window.geteilt.wakeLock = null;
    await lock?.release();
  },

  requestWakeLock: async () => {
    if (!('wakeLock' in navigator)) {
      return false;
    }

    try {
      const lock = await navigator.wakeLock.request('screen');

      // The page may have been left while the request was pending; then nobody would release it.
      if (!window.geteilt.screenWanted) {
        await lock.release();
        return false;
      }

      window.geteilt.wakeLock = lock;
      return true;
    } catch (error) {
      console.warn('Wake lock refused', error);
      return false;
    }
  },
};

// The browser drops a wake lock whenever the tab is hidden: switching to the banking app to check
// the transfer is enough. Taken again on return, while the QR screen still wants it.
document.addEventListener('visibilitychange', () => {
  const held = window.geteilt.wakeLock && !window.geteilt.wakeLock.released;

  if (document.visibilityState === 'visible' && window.geteilt.screenWanted && !held) {
    window.geteilt.requestWakeLock();
  }
});
