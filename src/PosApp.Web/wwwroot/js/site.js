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
  const toggles = document.querySelectorAll('[data-settings-toggle]');
  if (!toggles.length) {
    return;
  }

  const body = document.body;
  const storageKey = 'radhira-pos:nav-mode';

  const readPreference = () => {
    try {
      const stored = localStorage.getItem(storageKey);
      return stored === 'settings' ? 'settings' : 'default';
    } catch {
      return 'default';
    }
  };

  const persistPreference = (mode) => {
    try {
      localStorage.setItem(storageKey, mode);
    } catch {
      // ignore
    }
  };

  const applyMode = (mode) => {
    const showSettings = mode === 'settings';
    body.classList.toggle('settings-menu', showSettings);
    const nextLabel = showSettings ? 'Back to menu' : 'Settings';
    toggles.forEach((toggle) => {
      toggle.setAttribute('aria-pressed', showSettings ? 'true' : 'false');
      toggle.setAttribute('aria-label', nextLabel);
      const labelNode = toggle.querySelector('[data-settings-toggle-label]');
      if (labelNode) {
        labelNode.textContent = nextLabel;
      }
    });
  };

  let mode = readPreference();
  applyMode(mode);

  toggles.forEach((toggle) => {
    toggle.addEventListener('click', () => {
      mode = mode === 'settings' ? 'default' : 'settings';
      applyMode(mode);
      persistPreference(mode);
    });
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

(() => {
  const dropdowns = Array.from(document.querySelectorAll('[data-nav-dropdown]'));
  if (!dropdowns.length) {
    return;
  }

  const closeAll = () => {
    dropdowns.forEach((dropdown) => dropdown.removeAttribute('open'));
  };

  document.addEventListener('click', (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    const clickedInside = dropdowns.some((dropdown) => dropdown.contains(target));
    if (!clickedInside) {
      closeAll();
    }
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      closeAll();
    }
  });

  dropdowns.forEach((dropdown) => {
    dropdown.addEventListener('click', (event) => {
      const target = event.target;
      if (target instanceof HTMLAnchorElement) {
        dropdown.removeAttribute('open');
      }
    });
  });
})();
