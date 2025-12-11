(() => {
  const toast = document.querySelector('.toast');
  if (!toast) {
    return;
  }

  setTimeout(() => {
    toast.classList.add('hide');
  }, 3200);
})();

(() => {
  const toggle = document.querySelector('[data-sidebar-toggle]');
  if (!toggle) {
    return;
  }

  const body = document.body;
  const label = toggle.querySelector('[data-sidebar-toggle-label]');
  const storageKey = 'radhira-pos:sidebar-collapsed';

  const readPreference = () => {
    try {
      return localStorage.getItem(storageKey) === '1';
    } catch {
      return false;
    }
  };

  const persistPreference = (collapsed) => {
    try {
      localStorage.setItem(storageKey, collapsed ? '1' : '0');
    } catch {
      // Ignore storage failures (e.g., private mode).
    }
  };

  const applyState = (collapsed) => {
    body.classList.toggle('sidebar-collapsed', collapsed);
    toggle.setAttribute('aria-pressed', collapsed ? 'true' : 'false');
    if (label) {
      label.textContent = collapsed ? 'Show menu' : 'Hide menu';
    }
  };

  applyState(readPreference());

  toggle.addEventListener('click', () => {
    const next = !body.classList.contains('sidebar-collapsed');
    applyState(next);
    persistPreference(next);
  });
})();

(() => {
  const toggle = document.querySelector('[data-theme-toggle]');
  if (!toggle) {
    return;
  }

  const root = document.documentElement;
  const label = toggle.querySelector('[data-theme-toggle-label]');
  const storageKey = 'radhira-pos:theme';
  const mediaQuery = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

  const readPreference = () => {
    try {
      const stored = localStorage.getItem(storageKey);
      return stored === 'light' || stored === 'dark' ? stored : null;
    } catch {
      return null;
    }
  };

  const persistPreference = (theme) => {
    try {
      localStorage.setItem(storageKey, theme);
    } catch {
      // Ignore storage problems.
    }
  };

  const applyTheme = (theme) => {
    const next = theme === 'light' ? 'light' : 'dark';
    root.setAttribute('data-theme', next);
    toggle.setAttribute('aria-pressed', next === 'light' ? 'true' : 'false');
    if (label) {
      label.textContent = next === 'light' ? 'Dark mode' : 'Light mode';
    }
  };

  const systemTheme = () => {
    if (!mediaQuery) {
      return 'dark';
    }
    return mediaQuery.matches ? 'dark' : 'light';
  };
  const stored = readPreference();
  let followSystem = !stored;

  applyTheme(stored ?? systemTheme());

  if (mediaQuery) {
    const handleChange = () => {
      if (followSystem) {
        applyTheme(systemTheme());
      }
    };

    if (typeof mediaQuery.addEventListener === 'function') {
      mediaQuery.addEventListener('change', handleChange);
    } else if (typeof mediaQuery.addListener === 'function') {
      mediaQuery.addListener(handleChange);
    }
  }

  toggle.addEventListener('click', () => {
    const current = root.getAttribute('data-theme') === 'light' ? 'light' : 'dark';
    const next = current === 'light' ? 'dark' : 'light';
    followSystem = false;
    applyTheme(next);
    persistPreference(next);
  });
})();
