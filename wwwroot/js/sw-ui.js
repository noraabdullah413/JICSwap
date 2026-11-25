







(function () {
  var body = document.body;

  function setNav(open) {
    body.classList.toggle('md-nav-open', open);
    document.querySelectorAll('[data-md-toggle]').forEach(function (b) {
      b.setAttribute('aria-expanded', open ? 'true' : 'false');
    });
  }
  function closeMenus(except) {
    document.querySelectorAll('.sw-menu:not([hidden])').forEach(function (m) {
      if (m !== except) m.hidden = true;
    });
  }

  document.addEventListener('click', function (e) {
    var t = e.target;
    if (t.closest('[data-md-toggle]')) { setNav(!body.classList.contains('md-nav-open')); return; }
    if (t.closest('.app-nav a, .main-nav a')) setNav(false);
    else if (body.classList.contains('md-nav-open') && !t.closest('.app-nav, .main-nav')) setNav(false);

    var menuBtn = t.closest('[data-sw-menu]');
    if (menuBtn) {
      var menu = document.getElementById(menuBtn.getAttribute('data-sw-menu'));
      if (menu && !t.closest('.sw-menu')) { closeMenus(menu); menu.hidden = !menu.hidden; e.preventDefault(); return; }
    }
    if (!t.closest('.sw-menu')) closeMenus(null);

    var opener = t.closest('[data-sw-open]');
    if (opener) {
      var dlg = document.getElementById(opener.getAttribute('data-sw-open'));
      if (dlg) { dlg.hidden = false; e.preventDefault(); var f = dlg.querySelector('input, textarea, select, button'); if (f) f.focus(); }
      return;
    }
    if (t.closest('[data-sw-close]') || t.classList.contains('sw-dialog')) {
      var d = t.closest('.sw-dialog'); if (d) { d.hidden = true; e.preventDefault(); }
    }
  });

  document.addEventListener('keydown', function (e) {
    if (e.key !== 'Escape') return;
    setNav(false); closeMenus(null);
    document.querySelectorAll('.sw-dialog:not([hidden])').forEach(function (d) { d.hidden = true; });
  });
  window.addEventListener('resize', function () { if (window.innerWidth >= 1200) setNav(false); });


  document.querySelectorAll('.code-row').forEach(function (row) {
    var boxes = Array.prototype.slice.call(row.querySelectorAll('input.code-box'));
    var target = document.querySelector('input[name="' + row.getAttribute('data-sw-code-target') + '"]');
    function sync() { if (target) target.value = boxes.map(function (b) { return b.value; }).join(''); }
    boxes.forEach(function (box, i) {
      box.addEventListener('input', function () {
        box.value = box.value.replace(/\D/g, '').slice(-1);
        if (box.value && boxes[i + 1]) boxes[i + 1].focus();
        sync();
      });
      box.addEventListener('keydown', function (e) {
        if (e.key === 'Backspace' && !box.value && boxes[i - 1]) { boxes[i - 1].focus(); boxes[i - 1].value = ''; sync(); }
      });
      box.addEventListener('paste', function (e) {
        var digits = (e.clipboardData || window.clipboardData).getData('text').replace(/\D/g, '').slice(0, boxes.length);
        if (!digits) return;
        e.preventDefault();
        boxes.forEach(function (b, k) { b.value = digits[k] || ''; });
        (boxes[digits.length] || boxes[boxes.length - 1]).focus();
        sync();
      });
    });
    sync();
  });


  document.querySelectorAll('label.upload-zone').forEach(function (zone) {
    var input = zone.querySelector('input[type=file]');
    ['dragenter', 'dragover'].forEach(function (ev) {
      zone.addEventListener(ev, function (e) { e.preventDefault(); zone.classList.add('is-drag'); });
    });
    ['dragleave', 'drop'].forEach(function (ev) {
      zone.addEventListener(ev, function (e) { e.preventDefault(); zone.classList.remove('is-drag'); });
    });
    zone.addEventListener('drop', function (e) {
      if (!input || !e.dataTransfer.files.length) return;
      input.files = e.dataTransfer.files;
      input.dispatchEvent(new Event('change', { bubbles: true }));
    });
  });
})();
