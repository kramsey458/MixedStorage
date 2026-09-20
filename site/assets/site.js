(function () {
  'use strict';

  // Mobile navigation
  var toggle = document.querySelector('.nav-toggle');
  var nav = document.getElementById('site-nav');
  if (toggle && nav) {
    var close = function () { nav.classList.remove('is-open'); toggle.setAttribute('aria-expanded', 'false'); };
    toggle.addEventListener('click', function () {
      var open = nav.classList.toggle('is-open');
      toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && nav.classList.contains('is-open')) { close(); toggle.focus(); }
    });
  }

  // Open an accordion entry when it is linked to, and support expand/collapse all
  function openFromHash() {
    var id = decodeURIComponent(location.hash.slice(1));
    if (!id) return;
    var el = document.getElementById(id);
    if (el && el.tagName === 'DETAILS') { el.open = true; el.scrollIntoView(); }
  }
  window.addEventListener('hashchange', openFromHash);
  openFromHash();
  Array.prototype.forEach.call(document.querySelectorAll('[data-expand]'), function (btn) {
    btn.addEventListener('click', function () {
      var open = btn.getAttribute('data-expand') === 'all';
      Array.prototype.forEach.call(document.querySelectorAll('details.qa'), function (d) { d.open = open; });
    });
  });

  // Point download buttons at the newest release. The static links (the releases page) remain if this fails.
  var CACHE_KEY = 'mixedstorage-release-v1';
  var TTL = 10 * 60 * 1000;
  var each = function (selector, fn) { Array.prototype.forEach.call(document.querySelectorAll(selector), fn); };

  function apply(rel) {
    each('[data-download]', function (a) { a.href = rel.zip || rel.url; });
    each('[data-download-label]', function (el) { el.textContent = 'Download ' + rel.tag; });
    each('[data-version]', function (el) { el.textContent = rel.tag; });
    each('[data-zip-name]', function (el) { el.textContent = rel.zipName || ('MixedStorage-' + rel.tag + '.zip'); });
    each('[data-release-link]', function (a) { a.href = rel.url; });
    each('[data-release-kind]', function (el) {
      el.textContent = rel.prerelease ? 'Pre-release' : 'Latest release';
      el.hidden = false;
    });
  }

  if (!document.querySelector('[data-download],[data-version],[data-release-kind],[data-zip-name],[data-release-link]')) return;
  var cached = null;
  try { cached = JSON.parse(sessionStorage.getItem(CACHE_KEY)); } catch (e) { cached = null; }
  if (cached && cached.rel && Date.now() - cached.t < TTL) { apply(cached.rel); return; }

  if (!window.fetch) return;
  fetch('https://api.github.com/repos/timbermods/MixedStorage/releases?per_page=10', { headers: { Accept: 'application/vnd.github+json' } })
    .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
    .then(function (list) {
      var r = list.filter(function (x) { return !x.draft; })[0];
      if (!r) return;
      var zip = (r.assets || []).filter(function (a) { return /\.zip$/i.test(a.name); })[0];
      var rel = { tag: r.tag_name, prerelease: !!r.prerelease, url: r.html_url,
                  zip: zip ? zip.browser_download_url : null, zipName: zip ? zip.name : null };
      try { sessionStorage.setItem(CACHE_KEY, JSON.stringify({ t: Date.now(), rel: rel })); } catch (e) { /* storage unavailable */ }
      apply(rel);
    })
    .catch(function () { /* keep the static links */ });
})();
