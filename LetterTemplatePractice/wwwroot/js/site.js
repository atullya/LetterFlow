(() => {
  // ── Sidebar — pushes content right (Medium-style) ─────────────────────────
  const sidebar = document.getElementById('sidebar');
  const toggle  = document.getElementById('sidebarToggle');

  if (sidebar && toggle) {
    const open  = () => { sidebar.classList.add('open');    document.body.classList.add('sidebar-open');    toggle.classList.add('active'); };
    const close = () => { sidebar.classList.remove('open'); document.body.classList.remove('sidebar-open'); toggle.classList.remove('active'); };
    const flip  = () => sidebar.classList.contains('open') ? close() : open();

    toggle.addEventListener('click', flip);
  }

  // ── Reading progress bar ────────────────────────────────────────────────────
  const article         = document.getElementById('articleContent');
  const readingProgress = document.getElementById('readingProgress');

  if (article && readingProgress) {
    const update = () => {
      const top        = article.offsetTop;
      const height     = article.offsetHeight;
      const vh         = window.innerHeight;
      const scrollable = Math.max(height - vh, 1);
      const pct        = Math.min(Math.max((window.scrollY - top) / scrollable, 0), 1);
      readingProgress.style.width = `${pct * 100}%`;
    };
    update();
    window.addEventListener('scroll', update, { passive: true });
    window.addEventListener('resize', update);
  }

  // ── User avatar dropdown ────────────────────────────────────────────────────
  const uRoot = document.getElementById('userMenuRoot');
  const uBtn  = document.getElementById('userMenuToggle');
  const uDrop = document.getElementById('userDropdown');

  if (uRoot && uBtn && uDrop) {
    uBtn.addEventListener('click', e => {
      e.stopPropagation();
      const isOpen = uDrop.classList.toggle('open');
      uBtn.setAttribute('aria-expanded', String(isOpen));
    });
    document.addEventListener('click', e => {
      if (!uRoot.contains(e.target)) {
        uDrop.classList.remove('open');
        uBtn.setAttribute('aria-expanded', 'false');
      }
    });
  }

  // ── Story three-dot menus ───────────────────────────────────────────────────
  document.addEventListener('click', e => {
    // Three-dot toggle
    const dotsBtn = e.target.closest('.sc-dots-btn');
    if (dotsBtn) {
      e.stopPropagation();
      const drop    = dotsBtn.closest('.sc-menu-wrap').querySelector('.sc-dropdown');
      const wasOpen = drop.classList.contains('open');
      document.querySelectorAll('.sc-dropdown.open, .story-menu-dropdown.open').forEach(m => m.classList.remove('open'));
      if (!wasOpen) drop.classList.add('open');
      return;
    }

    // Old story-menu-btn (My Stories page)
    const oldBtn = e.target.closest('.story-menu-btn');
    if (oldBtn) {
      e.stopPropagation();
      const menu    = oldBtn.nextElementSibling;
      const wasOpen = menu.classList.contains('open');
      document.querySelectorAll('.sc-dropdown.open, .story-menu-dropdown.open').forEach(m => m.classList.remove('open'));
      if (!wasOpen) menu.classList.add('open');
      return;
    }

    // Copy link button
    const copyBtn = e.target.closest('.sc-copy-link');
    if (copyBtn) {
      e.stopPropagation();
      const url = copyBtn.dataset.url;
      navigator.clipboard.writeText(url).then(() => {
        const orig = copyBtn.innerHTML;
        copyBtn.innerHTML = '<i class="fas fa-check"></i> Copied!';
        setTimeout(() => { copyBtn.innerHTML = orig; }, 2000);
      });
      return;
    }

    // Close all on outside click
    document.querySelectorAll('.sc-dropdown.open, .story-menu-dropdown.open').forEach(m => m.classList.remove('open'));
  });
})();
