









(function () {
  function qsa(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }


  function syncCards(name, form) {
    qsa('input.sw-input[type=radio][name="' + name + '"]', form || document).forEach(function (r) {
      var card = r.closest('.pay-card, .type-card');
      if (card) card.classList.toggle('sel', r.checked);
    });
  }
  document.addEventListener('change', function (e) {
    var t = e.target;
    if (t.matches && t.matches('.pay-card .sw-input[type=radio], .type-card .sw-input[type=radio]')) syncCards(t.name, t.form);
  });


  qsa('[data-sw-countdown]').forEach(function (el) {
    var left = parseInt(el.getAttribute('data-sw-countdown'), 10) || 0;
    function draw() {
      var m = Math.floor(left / 60), s = left % 60;
      el.textContent = (m < 10 ? '0' : '') + m + ':' + (s < 10 ? '0' : '') + s;
    }
    draw();
    var timer = setInterval(function () { left = Math.max(0, left - 1); draw(); if (!left) { clearInterval(timer); if (el.hasAttribute('data-sw-reload')) location.reload(); } }, 1000);
  });


  qsa('[data-sw-username]').forEach(function (input) {
    var url = input.getAttribute('data-sw-username'), t;
    var ok = document.getElementById('usernameOk'), taken = document.getElementById('usernameTaken');
    function update() {
      var v = input.value.trim();
      qsa('[data-sw-uname]').forEach(function (s) { s.textContent = v; });
      clearTimeout(t);
      if (!/^[a-z0-9._]{3,20}$/.test(v)) { if (ok) ok.style.display = 'none'; if (taken) taken.hidden = true; return; }
      t = setTimeout(function () {
        fetch(url + '?u=' + encodeURIComponent(v), { headers: { 'Accept': 'application/json' } })
          .then(function (r) { return r.json(); })
          .then(function (j) { if (ok) ok.style.display = j.available ? 'flex' : 'none'; if (taken) taken.hidden = !!j.available; })
          .catch(function () {});
      }, 250);
    }
    input.addEventListener('input', update);
    update();
  });


  qsa('form[data-sw-autosubmit]').forEach(function (form) {
    form.addEventListener('change', function (e) {
      if (e.target.matches('input[type=number], input[type=text]')) return;
      if (form.requestSubmit) form.requestSubmit(); else form.submit();
    });
    qsa('input[type=number], input[type=text]', form).forEach(function (i) {
      i.addEventListener('change', function () { if (form.requestSubmit) form.requestSubmit(); else form.submit(); });
    });
  });


  qsa('[data-sw-gallery]').forEach(function (g) {
    var main = g.querySelector('[data-sw-main]');
    qsa('img.thumb', g).forEach(function (th) {
      th.style.cursor = 'pointer';
      th.addEventListener('click', function () {
        if (main) main.src = th.getAttribute('data-full') || th.src;
        qsa('img.thumb', g).forEach(function (o) { o.classList.toggle('sel', o === th); });
      });
    });
  });


  function money(n) { return 'SAR ' + n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
  function commission(v) { var c = Math.round(v * 100); return Math.floor((c * 5 + 50) / 100) / 100; }
  qsa('form[data-sw-summary]').forEach(function (form) {
    var kind = form.getAttribute('data-sw-summary');
    function out(key, html, asHtml) {
      qsa('[data-sw-out="' + key + '"]').forEach(function (el) { if (asHtml) el.innerHTML = html; else el.textContent = html; });
    }
    function val(name) {
      var el = form.querySelector('[name="' + name + '"]:checked') || form.querySelector('select[name="' + name + '"], input[name="' + name + '"]:not([type=radio]):not([type=checkbox]), textarea[name="' + name + '"]');
      return el ? el.value : '';
    }
    function label(name) {
      var el = form.querySelector('[name="' + name + '"]');
      if (el && el.tagName === 'SELECT') return el.value ? el.options[el.selectedIndex].text : '';
      var r = form.querySelector('[name="' + name + '"]:checked');
      return r ? (r.getAttribute('data-label') || r.value) : '';
    }
    function update() {
      if (kind === 'buy') {
        var p = label('meeting_point'), d = form.querySelector('[name=meeting_day]'), tm = form.querySelector('[name=meeting_time]');
        out('point', p || 'Not selected');
        var dt = (d && d.value) ? d.options[d.selectedIndex].getAttribute('data-short') : '';
        var tt = (tm && tm.value) ? tm.options[tm.selectedIndex].text : '';
        out('when', dt && tt ? dt + ', ' + tt : (dt || tt || 'Not selected'));
        var pm = form.querySelector('[name=PaymentMethod]:checked');
        out('pay', pm ? '<i class="bi ' + pm.getAttribute('data-icon') + '"></i> ' + pm.value : '<i class="bi bi-dash-circle"></i> Not selected', true);
      }
      if (kind === 'item' || kind === 'service') {
        var t = val('Title').trim();
        out('title', t || 'Not set yet');
        out('category', label('CategoryID') || 'Not set');
        if (kind === 'item') out('condition', label('ItemCondition') || 'Not set');
        var price = parseFloat(String(val('Price')).replace(',', '.'));
        if (!(price >= 1 && price <= 20000)) price = 0;
        var unit = kind === 'service' ? label('PriceUnit').toLowerCase() : '';
        var suffix = unit ? ' / ' + unit : '';
        out('price', money(price) + suffix);
        out('earnings', money(Math.round((price - commission(price)) * 100) / 100) + suffix);
        out('commission', money(commission(price)));
        var cont = form.querySelector('[data-sw-continue]') || (form.id ? document.querySelector('[data-sw-continue][form="' + form.id + '"]') : null);
        if (cont) {
          var ready = form.checkValidity();
          cont.classList.toggle('btn-jic', ready); cont.classList.toggle('btn-ghost', !ready); cont.classList.toggle('btn-disabled', !ready);
        }
      }
    }
    form.addEventListener('input', update);
    form.addEventListener('change', update);
    update();
  });


  qsa('select[data-sw-ranges]').forEach(function (sel) {
    var map = {}; try { map = JSON.parse(sel.getAttribute('data-sw-ranges')) || {}; } catch (e) {}
    sel.addEventListener('change', function () {
      qsa('[data-sw-range-out]').forEach(function (o) { var t = map[sel.value]; o.hidden = !t; o.textContent = t || ''; });
    });
  });


  qsa('input[type=file]').forEach(function (input) {
    input.addEventListener('change', function () {
      var zone = input.closest('.upload-zone, .panel-body'); var out = zone ? zone.querySelector('[data-sw-chosen]') : null;
      if (out) out.textContent = Array.prototype.map.call(input.files, function (f) { return f.name; }).join(', ');
    });
  });
  qsa('input[type=file][data-sw-maxkb]').forEach(function (input) {
    input.addEventListener('change', function () {
      var max = parseInt(input.getAttribute('data-sw-maxkb'), 10) * 1024, bad = [];
      Array.prototype.forEach.call(input.files, function (f) { if (f.size > max) bad.push(f.name); });
      var box = document.getElementById(input.getAttribute('data-sw-error') || '');
      if (bad.length) {
        if (box) { box.hidden = false; var n = box.querySelector('[data-sw-files]'); if (n) n.textContent = bad.join(', '); }

        try {
          var dt = new DataTransfer();
          Array.prototype.forEach.call(input.files, function (f) { if (f.size <= max) dt.items.add(f); });
          input.files = dt.files;
        } catch (e) { input.value = ''; }
        if (!input.files.length) return;
      } else if (box) box.hidden = true;
      if (input.hasAttribute('data-sw-autoupload') && input.files.length) {
        var f = input.form || document.getElementById(input.getAttribute('form'));
        if (f) { if (f.querySelector('[data-sw-overlay-target]')) {} f.submit(); }
      }
    });
  });


  document.addEventListener('click', function (e) {
    var c = e.target.closest('[data-sw-copy]');
    if (!c) return;
    e.preventDefault();
    var url = c.getAttribute('data-sw-copy');
    var done = function () { var s = c.querySelector('span[data-sw-copied]'); if (s) s.textContent = 'Link copied'; };
    if (navigator.clipboard) navigator.clipboard.writeText(url).then(done, done); else done();
  });
  qsa('form[data-sw-overlay]').forEach(function (form) {
    form.addEventListener('submit', function () {
      var o = document.getElementById(form.getAttribute('data-sw-overlay'));
      if (o) o.hidden = false;
    });
  });
  qsa('form[data-sw-autopost]').forEach(function (form) {
    setTimeout(function () { form.submit(); }, parseInt(form.getAttribute('data-sw-autopost'), 10) || 2000);
  });
})();



(function () {
  function qsa(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }


  var headers = qsa('.site-header, .app-header');
  function onScroll() { var on = window.scrollY > 4; headers.forEach(function (h) { h.classList.toggle('is-scrolled', on); }); }
  window.addEventListener('scroll', onScroll, { passive: true }); onScroll();


  var dlg = null, pending = null;
  function build() {
    dlg = document.createElement('div');
    dlg.className = 'modal-veil sw-dialog sw-confirm';
    dlg.hidden = true;
    dlg.setAttribute('role', 'dialog');
    dlg.setAttribute('aria-modal', 'true');
    dlg.innerHTML = '<div class="modal-panel"><div class="panel-head" style="padding:18px 24px;"><h4 data-c-title></h4></div>'
      + '<div class="panel-body" style="padding:28px 24px;text-align:center;"><div class="sw-confirm-icon"><i class="bi"></i></div>'
      + '<p data-c-msg style="font-size:14.5px;color:var(--ink);max-width:400px;margin:0 auto;"></p>'
      + '<div style="display:flex;gap:12px;margin-top:26px;"><button type="button" class="btn-block btn-lg-jic" data-c-ok></button>'
      + '<button type="button" class="btn-ghost btn-block btn-lg-jic" data-c-cancel>Cancel</button></div></div></div>';
    document.body.appendChild(dlg);
    dlg.querySelector('[data-c-cancel]').addEventListener('click', close);
    dlg.addEventListener('click', function (e) { if (e.target === dlg) close(); });
    dlg.querySelector('[data-c-ok]').addEventListener('click', function () {
      var p = pending; pending = null; dlg.hidden = true;
      if (!p) return;
      if (p.href) { if (p.method) { var f = document.createElement('form'); f.method = 'POST'; f.action = p.href; f.innerHTML = '<input type="hidden" name="_token" value="' + (document.querySelector('meta[name=csrf-token]') || {}).content + '">'; document.body.appendChild(f); f.submit(); } else { location.href = p.href; } return; }
      p.form.dataset.swConfirmed = '1';
      if (p.submitter && p.form.requestSubmit) p.form.requestSubmit(p.submitter); else if (p.form.requestSubmit) p.form.requestSubmit(); else p.form.submit();
    });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !dlg.hidden) close(); });
  }
  function close() {
    if (pending && pending.form && pending.form.hasAttribute('data-sw-autosubmit')) pending.form.reset();
    pending = null; dlg.hidden = true;
  }
  function ask(src, data) {
    if (!dlg) build();
    var tone = src.getAttribute('data-sw-confirm-tone') || 'danger';
    dlg.querySelector('[data-c-title]').textContent = src.getAttribute('data-sw-confirm-title') || 'Please confirm';
    dlg.querySelector('[data-c-msg]').textContent = src.getAttribute('data-sw-confirm');
    var ok = dlg.querySelector('[data-c-ok]');
    ok.textContent = src.getAttribute('data-sw-confirm-ok') || 'Yes, continue';
    ok.className = (tone === 'danger' ? 'btn-danger-jic' : 'btn-jic') + ' btn-block btn-lg-jic';
    var icon = dlg.querySelector('.sw-confirm-icon');
    icon.className = 'sw-confirm-icon ' + (tone === 'danger' ? 'danger' : 'info');
    icon.querySelector('i').className = 'bi ' + (src.getAttribute('data-sw-confirm-icon') || (tone === 'danger' ? 'bi-exclamation-triangle' : 'bi-question-circle'));
    pending = data; dlg.hidden = false; ok.focus();
  }
  document.addEventListener('submit', function (e) {
    var form = e.target, sub = e.submitter;
    if (form.dataset.swConfirmed === '1') { form.dataset.swConfirmed = ''; return; }
    var src = (sub && sub.hasAttribute('data-sw-confirm')) ? sub : (form.hasAttribute('data-sw-confirm') ? form : null);
    if (!src) return;
    e.preventDefault();
    ask(src, { form: form, submitter: sub });
  }, true);
  document.addEventListener('click', function (e) {
    var a = e.target.closest('a[data-sw-confirm]');
    if (!a) return;
    e.preventDefault();
    ask(a, { href: a.href, method: a.getAttribute('data-sw-method') });
  });


  var items = qsa('.sw-reveal');
  if ('IntersectionObserver' in window && items.length) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) { if (en.isIntersecting) { en.target.classList.add('is-in'); io.unobserve(en.target); } });
    }, { threshold: 0.12 });
    items.forEach(function (el, i) { el.style.transitionDelay = ((el.getAttribute('data-delay') || (i % 6)) * 80) + 'ms'; io.observe(el); });
  } else items.forEach(function (el) { el.classList.add('is-in'); });
})();
