(() => {
  // ── Sidebar — pushes content right (Medium-style) ─────────────────────────
  const sidebar = document.getElementById('sidebar');
  const toggle  = document.getElementById('sidebarToggle');

  if (sidebar && toggle) {
    const open  = () => {
      sidebar.classList.add('open');
      document.body.classList.add('sidebar-open');
      toggle.classList.add('active');
      sessionStorage.setItem('sidebarOpen', '1');
    };
    const close = () => {
      sidebar.classList.remove('open');
      document.body.classList.remove('sidebar-open');
      toggle.classList.remove('active');
      sessionStorage.removeItem('sidebarOpen');
    };
    const flip  = () => sidebar.classList.contains('open') ? close() : open();

    toggle.addEventListener('click', flip);

    // Restore sidebar state instantly (no slide animation on page load)
    if (sessionStorage.getItem('sidebarOpen') === '1') {
      sidebar.classList.add('open');
      document.body.classList.add('sidebar-open');
      toggle.classList.add('active');
    }
    // Remove the pre-open class so transitions work normally from here on
    document.documentElement.classList.remove('sidebar-preopen');
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

  // ── Follow / Unfollow toggle (AJAX) ──────────────────────────────────────
  document.addEventListener('click', e => {
    const btn = e.target.closest('.btn-follow');
    if (!btn) return;
    e.preventDefault();

    const url      = btn.dataset.url;
    const token    = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const formData = new FormData();
    if (token) formData.append('__RequestVerificationToken', token);

    btn.disabled = true;

    fetch(url, { method: 'POST', body: formData })
      .then(r => { if (!r.ok) throw new Error('Request failed'); return r.json(); })
      .then(data => {
        // Toggle button style
        if (data.following) {
          btn.classList.add('btn-following');
          btn.textContent = 'Following';
        } else {
          btn.classList.remove('btn-following');
          btn.textContent = 'Follow';
        }

        // Update follower count on profile page
        const userId  = btn.dataset.userId;
        const counter = document.getElementById('follower-count-' + userId);
        if (counter) counter.textContent = data.followerCount;
      })
      .catch(() => {
        // Silently re-enable on error
      })
      .finally(() => { btn.disabled = false; });
  });

  // ── Notifications dropdown + polling ─────────────────────────────────────
  const notifRoot   = document.getElementById('notificationRoot');
  const notifBtn    = document.getElementById('notificationToggle');
  const notifDrop   = document.getElementById('notificationDropdown');
  const notifBadge  = document.getElementById('notificationBadge');
  const notifList   = document.getElementById('notificationList');
  const notifEmpty  = document.getElementById('notificationEmpty');
  const markReadBtn = document.getElementById('markAllReadBtn');

  if (notifRoot && notifBtn && notifDrop) {

    // Toggle dropdown
    notifBtn.addEventListener('click', e => {
      e.stopPropagation();
      const wasOpen = notifDrop.classList.contains('open');
      // Close other dropdowns
      document.querySelectorAll('.user-dropdown.open, .sc-dropdown.open, .story-menu-dropdown.open')
        .forEach(m => m.classList.remove('open'));
      notifDrop.classList.toggle('open', !wasOpen);
    });

    // Close on outside click
    document.addEventListener('click', e => {
      if (!notifRoot.contains(e.target)) {
        notifDrop.classList.remove('open');
      }
    });

    // Mark all as read
    if (markReadBtn) {
      markReadBtn.addEventListener('click', () => {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const fd = new FormData();
        if (token) fd.append('__RequestVerificationToken', token);

        fetch('/Notifications/MarkRead', { method: 'POST', body: fd })
          .then(r => r.json())
          .then(() => {
            if (notifBadge) { notifBadge.style.display = 'none'; notifBadge.textContent = '0'; }
            document.querySelectorAll('.notification-item.unread').forEach(el => el.classList.remove('unread'));
          });
      });
    }

    // Render notification items into the dropdown
    function renderNotifications(notifications) {
      if (!notifications || notifications.length === 0) {
        if (notifEmpty) notifEmpty.style.display = '';
        return;
      }

      if (notifEmpty) notifEmpty.style.display = 'none';

      // Clear old items (keep the empty state)
      notifList.querySelectorAll('.notification-item').forEach(el => el.remove());

      notifications.forEach(n => {
        const div = document.createElement('div');
        div.className = 'notification-item' + (n.isRead ? '' : ' unread');

        const avatarHtml = n.actor.avatarUrl
          ? '<img src="' + n.actor.avatarUrl + '" class="notif-avatar-img" />'
          : '<span class="notif-avatar-initial">' + (n.actor.displayName || n.actor.username || '?')[0].toUpperCase() + '</span>';

        let message = '';
        if (n.type === 'follow') {
          message = '<strong>' + escapeHtml(n.actor.displayName || n.actor.username) + '</strong> started following you.';
        } else if (n.type === 'new_post') {
          message = '<strong>' + escapeHtml(n.actor.displayName || n.actor.username) + '</strong> published a new story.';
        } else if (n.type === 'like') {
          message = '<strong>' + escapeHtml(n.actor.displayName || n.actor.username) + '</strong> liked your story.';
        } else if (n.type === 'comment') {
          message = '<strong>' + escapeHtml(n.actor.displayName || n.actor.username) + '</strong> commented on your story.';
        } else {
          message = '<strong>' + escapeHtml(n.actor.displayName || n.actor.username) + '</strong> interacted with your content.';
        }

        let link = '/@' + n.actor.username;
        if (n.post && n.post.slug) link = '/Blog/Details/' + n.post.slug;

        const timeAgo = getTimeAgo(n.createdAt);

        div.innerHTML =
          '<a href="' + link + '" class="notification-item-link">' +
            '<div class="notif-avatar">' + avatarHtml + '</div>' +
            '<div class="notif-body">' +
              '<div class="notif-message">' + message + '</div>' +
              '<div class="notif-time">' + timeAgo + '</div>' +
            '</div>' +
          '</a>';

        notifList.appendChild(div);
      });
    }

    // Helper: time ago
    function getTimeAgo(dateStr) {
      const now  = Date.now();
      const then = new Date(dateStr).getTime();
      const diff = Math.max(0, Math.floor((now - then) / 1000));
      if (diff < 60)    return 'just now';
      if (diff < 3600)  return Math.floor(diff / 60) + 'm ago';
      if (diff < 86400) return Math.floor(diff / 3600) + 'h ago';
      return Math.floor(diff / 86400) + 'd ago';
    }

    // Helper: escape HTML
    function escapeHtml(str) {
      const d = document.createElement('div');
      d.textContent = str || '';
      return d.innerHTML;
    }

    // Poll for notifications
    function pollNotifications() {
      fetch('/Notifications/Unread')
        .then(r => { if (!r.ok) throw new Error(); return r.json(); })
        .then(data => {
          if (notifBadge) {
            if (data.unreadCount > 0) {
              notifBadge.textContent = data.unreadCount > 99 ? '99+' : data.unreadCount;
              notifBadge.style.display = '';
            } else {
              notifBadge.style.display = 'none';
            }
          }
          renderNotifications(data.notifications);
        })
        .catch(() => { /* silently ignore polling errors */ });
    }

    // Initial poll + every 30 seconds
    pollNotifications();
    setInterval(pollNotifications, 30000);
  }
})();
