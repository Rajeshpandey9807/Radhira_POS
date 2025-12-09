(() => {
  const toast = document.querySelector('.toast');
  if (!toast) return;

  setTimeout(() => {
    toast.classList.add('hide');
  }, 3200);
})();
