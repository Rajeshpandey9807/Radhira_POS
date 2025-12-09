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
