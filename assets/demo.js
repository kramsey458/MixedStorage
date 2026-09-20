/*
 * Interactive replica of the in-game Storage Allocation panel. The behavior and wording mirror
 * StorageView and StorageState in the mod: edits are drafts until Apply, lowering a limit never
 * deletes stock, and limits use the same rounding (see split.js).
 */
(function () {
  'use strict';
  var Split = window.MixedStorageSplit;
  var root = document.querySelector('[data-demo]');
  if (!root || !Split) return;
  var stage = root.closest('[data-demo-stage]') || root.parentNode;
  var TOTAL = Split.TOTAL;

  // Every good a Folktails warehouse accepts (see goods.js), sorted like the in-game list.
  var GOODS = window.MixedStorageGoods;
  if (!GOODS || !GOODS.length) return;
  var BUILDINGS = {
    small:  { name: 'Small Warehouse',  capacity: 30,   quote: '\"No dynamite in my pantry, please and thank you.\" \u2014Ma\u00a0\'Ngonel' },
    medium: { name: 'Medium Warehouse', capacity: 200,  quote: 'Proper storage of goods is crucial to surviving the hazards of a post-apocalyptic world.' },
    large:  { name: 'Large Warehouse',  capacity: 1200, quote: "With so much goods packed inside, there's no room to swing a tail." }
  };
  var PRESETS = {
    even:   { Bread: 5000, WheatFlour: 5000 },
    thirds: { Bread: 3333, Wheat: 3333, WheatFlour: 3334 },
    tiny:   { Bread: 9996, Berries: 4 }
  };
  var byId = {};
  GOODS.forEach(function (g) { byId[g.id] = g; });

  var $ = function (sel) { return root.querySelector(sel); };
  var els = {
    title: $('[data-title]'), quote: $('[data-quote]'), scroll: $('[data-scroll]'), summary: $('[data-summary]'),
    cards: $('[data-cards]'), search: $('[data-search]'), clearSearch: $('[data-clear-search]'), only: $('[data-only]'),
    count: $('[data-count]'), head: $('[data-head]'), rows: $('[data-rows]'), round: $('[data-round]'), msg: $('[data-msg]'),
    copy: $('[data-copy]'), paste: $('[data-paste]'), total: $('[data-total]'), clear: $('[data-clear]'),
    revert: $('[data-revert]'), apply: $('[data-apply]')
  };
  var rowEls = {};
  var state = { building: 'large', capacity: 1200, applied: {}, stock: {}, draft: {}, valid: {}, copied: null };
  var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  // ---- helpers (same rules as AllocationPlan) ----
  function parsePercent(text) {           // 0-100 with at most two decimals -> hundredths of a percent, or null
    text = String(text).trim().replace(',', '.');
    if (!/^\+?(\d+\.?\d*|\.\d+)$/.test(text)) return null;
    var value = Number(text);
    if (!(value >= 0 && value <= 100)) return null;
    var units = Math.round(value * 100);
    return Math.abs(value * 100 - units) < 1e-7 ? units : null;
  }
  function fmt(units) { return String(Math.round(units) / 100); }
  function limitsFor(units) {
    var shares = GOODS.map(function (g) { return { id: g.id, units: units[g.id] || 0 }; });
    var lim = Split.capacities(shares, state.capacity), out = {};
    GOODS.forEach(function (g, i) { out[g.id] = lim[i]; });
    return out;
  }
  function icon(g, slot) {
    var size = slot === 'card' ? 60 : 40;
    return '<img src="assets/goods/' + g.icon + '-' + size + '.png" alt="" width="' + size / 2 + '" height="' + size / 2 + '"' + (slot === 'card' ? '' : ' loading="lazy"') + '>';
  }
  function setMsg(text, kind) { els.msg.textContent = text; els.msg.className = 'ig-msg' + (kind ? ' is-' + kind : ''); }

  // ---- rows (built once) ----
  function buildRows() {
    els.rows.textContent = '';
    GOODS.forEach(function (g) {
      var row = document.createElement('div');
      row.className = 'ig-row';
      row.setAttribute('data-id', g.id);
      row.innerHTML =
        '<span class="ig-row__icon">' + icon(g, 'row') + '</span>' +
        '<span class="ig-row__main"><b>' + g.name + '</b><span></span></span>' +
        '<input class="ig-input" type="text" inputmode="decimal" autocomplete="off" spellcheck="false" aria-label="' + g.name + ' percent" ' +
          'title="0–100%, up to two decimal places. Changes are drafts until Apply.">' +
        '<button class="ig-btn ig-reset" type="button" title="Reset ' + g.name + ' to 0% (draft only)" aria-label="Reset ' + g.name + ' to 0 percent">×</button>' +
        '<button class="ig-btn ig-max" type="button" title="Set ' + g.name + ' to 100% and all other goods to 0% (draft only)" aria-label="Set ' + g.name + ' to 100 percent">Max</button>' +
        '<span class="ig-row__limit"></span>';
      var input = row.querySelector('input');
      input.addEventListener('input', function () { onPercent(g.id, input.value); });
      els.rows.appendChild(row);
      rowEls[g.id] = { root: row, input: input, stock: row.querySelector('.ig-row__main span'), limit: row.querySelector('.ig-row__limit') };
    });
  }

  // ---- draft handling ----
  function loadDraft() {
    GOODS.forEach(function (g) {
      state.draft[g.id] = state.applied[g.id] || 0;
      state.valid[g.id] = true;
      rowEls[g.id].input.value = fmt(state.draft[g.id]);
      rowEls[g.id].input.setAttribute('aria-invalid', 'false');
    });
    validate(); filter(); refreshStock();
  }
  function setDraft(map, message) {
    GOODS.forEach(function (g) {
      state.draft[g.id] = map[g.id] || 0;
      state.valid[g.id] = true;
      rowEls[g.id].input.value = fmt(state.draft[g.id]);
      rowEls[g.id].input.setAttribute('aria-invalid', 'false');
    });
    setMsg(message, '');
    validate(); filter();
  }
  function onPercent(id, text) {
    var units = parsePercent(text);
    state.valid[id] = units !== null;
    state.draft[id] = units === null ? 0 : units;
    rowEls[id].input.setAttribute('aria-invalid', units === null ? 'true' : 'false');
    setMsg('Unapplied changes', '');
    validate();
  }

  function validate() {
    var fieldsOk = GOODS.every(function (g) { return state.valid[g.id]; });
    var total = GOODS.reduce(function (a, g) { return a + state.draft[g.id]; }, 0);
    var valid = fieldsOk && total === TOTAL;
    els.apply.disabled = !valid;
    els.copy.disabled = !valid;
    els.paste.disabled = !state.copied;

    if (!fieldsOk) els.total.textContent = 'Enter valid percentages (0–100, 2 decimals)';
    else if (total === TOTAL) els.total.textContent = '100% / 100% allocated';
    else els.total.textContent = fmt(total) + '% / 100% — ' + fmt(Math.abs(total - TOTAL)) + (total < TOTAL ? '% remaining' : '% over');
    els.total.classList.toggle('is-bad', !valid);

    var preview = valid ? limitsFor(state.draft) : null;
    GOODS.forEach(function (g) { rowEls[g.id].limit.textContent = preview ? String(preview[g.id]) : '—'; });
    var zero = preview ? GOODS.filter(function (g) { return state.draft[g.id] > 0 && preview[g.id] === 0; }).length : 0;
    els.round.textContent = zero > 0
      ? zero + ' allocated good(s) round to 0 items. Increase their shares or use larger storage.'
      : 'Limits round to whole items; leftover slots go to the largest fractions. All slots are allocated.';
    els.round.classList.toggle('is-bad', zero > 0);
  }

  function filter() {
    var q = els.search.value.trim().toLowerCase(), visible = 0;
    GOODS.forEach(function (g) {
      var show = (!els.only.checked || state.draft[g.id] > 0) &&
                 (q === '' || g.name.toLowerCase().indexOf(q) >= 0 || g.id.toLowerCase().indexOf(q) >= 0);
      rowEls[g.id].root.hidden = !show;
      if (show) visible++;
    });
    els.count.textContent = visible + ' of ' + GOODS.length + ' goods shown · Total includes hidden rows';
  }

  // Summary cards and stock lines reflect the applied allocation, not the draft.
  function refreshStock() {
    var allocated = GOODS.filter(function (g) { return state.applied[g.id] > 0; });
    var limits = limitsFor(state.applied);
    var totalStock = GOODS.reduce(function (a, g) { return a + (state.stock[g.id] || 0); }, 0);
    els.summary.textContent = totalStock + ' / ' + state.capacity + ' items · ' + allocated.length +
      (allocated.length === 1 ? ' good allocated' : ' goods allocated');

    var html = '', shown = 0;
    GOODS.forEach(function (g) {
      var share = state.applied[g.id] || 0, stock = state.stock[g.id] || 0, limit = limits[g.id] || 0;
      var excess = stock > limit;
      rowEls[g.id].stock.textContent = stock + ' stored' + (excess ? ' · excess' : '');
      rowEls[g.id].stock.className = excess ? 'is-excess' : '';
      if (!(share > 0 || stock > 0)) return;
      shown++;
      var fill = limit > 0 ? Math.min(1, stock / limit) * 100 : (stock > 0 ? 100 : 0);
      html += '<div class="ig-card' + (excess ? ' is-excess' : '') + '">' +
        '<div class="ig-card__line"><span class="ig-card__icon">' + icon(g, 'card') + '</span>' +
        '<span class="ig-card__main"><b>' + g.name + '</b><span>' + fmt(share) + '% allocated</span></span>' +
        '<span class="ig-card__count"><b>' + stock + ' / ' + limit + '</b><span>stored / limit</span></span></div>' +
        (excess ? '<div class="ig-card__note">' + (stock - limit) + ' excess</div>' : '') +
        '<div class="ig-bar-track"><i style="width:' + fill + '%"></i></div></div>';
    });
    els.cards.innerHTML = shown ? html : '<p class="ig-empty">No goods allocated or stored.</p>';
  }

  // ---- actions ----
  function apply() {
    var fieldsOk = GOODS.every(function (g) { return state.valid[g.id]; });
    if (!fieldsOk || els.apply.disabled) return;
    state.applied = {};
    GOODS.forEach(function (g) { if (state.draft[g.id] > 0) state.applied[g.id] = state.draft[g.id]; });
    loadDraft();
    setMsg('Applied. Excess stock is preserved and can be hauled out.', 'good');
  }
  function revealList() {
    var top = els.head.getBoundingClientRect().top - els.scroll.getBoundingClientRect().top + els.scroll.scrollTop - 4;
    els.scroll.scrollTo({ top: Math.max(0, top), behavior: reduceMotion ? 'auto' : 'smooth' });
  }
  function setBuilding(key) {
    var b = BUILDINGS[key];
    state.building = key; state.capacity = b.capacity;
    els.title.textContent = b.name; els.quote.textContent = b.quote;
    state.applied = { Bread: 5000, WheatFlour: 5000 };
    state.stock = { Bread: b.capacity / 2, WheatFlour: b.capacity / 2 };
    els.search.value = ''; els.only.checked = false;
    loadDraft();
    setMsg('Edit percentages, then Apply. 0% disables a good.', '');
    Array.prototype.forEach.call(stage.querySelectorAll('[data-building]'), function (btn) {
      btn.setAttribute('aria-pressed', btn.getAttribute('data-building') === key ? 'true' : 'false');
    });
    els.scroll.scrollTop = 0;
  }

  els.rows.addEventListener('click', function (e) {
    var btn = e.target.closest('button');
    if (!btn) return;
    var id = btn.closest('.ig-row').getAttribute('data-id'), g = byId[id];
    if (btn.classList.contains('ig-reset')) {
      rowEls[id].input.value = '0';
      onPercent(id, '0');
      rowEls[id].input.focus(); rowEls[id].input.select();
    } else if (btn.classList.contains('ig-max')) {
      var only = {}; only[id] = TOTAL;
      setDraft(only, g.name + ' set to 100%. Press Apply.');
    }
  });
  els.search.addEventListener('input', filter);
  els.only.addEventListener('change', filter);
  els.clearSearch.addEventListener('click', function () { els.search.value = ''; filter(); els.search.focus(); });
  els.apply.addEventListener('click', apply);
  els.revert.addEventListener('click', function () { loadDraft(); setMsg('Draft reverted.', ''); });
  els.clear.addEventListener('click', function () {
    els.only.checked = false;
    setDraft({}, 'Draft cleared. Existing allocations remain active until Apply.');
  });
  els.copy.addEventListener('click', function () {
    if (els.copy.disabled) return;
    state.copied = {};
    GOODS.forEach(function (g) { state.copied[g.id] = state.draft[g.id]; });
    setMsg('Allocations copied. Select another storage building and Paste.', 'good');
    validate();
  });
  els.paste.addEventListener('click', function () {
    if (!state.copied) return;
    els.only.checked = true;
    setDraft(state.copied, "Allocations pasted. Limits use this building's capacity. Press Apply.");
    revealList();
  });
  Array.prototype.forEach.call(stage.querySelectorAll('[data-building]'), function (btn) {
    btn.addEventListener('click', function () { setBuilding(btn.getAttribute('data-building')); });
  });
  Array.prototype.forEach.call(stage.querySelectorAll('[data-preset]'), function (btn) {
    btn.addEventListener('click', function () {
      els.only.checked = true;
      setDraft(PRESETS[btn.getAttribute('data-preset')], 'Unapplied changes');
      revealList();
    });
  });
  var reset = stage.querySelector('[data-reset]');
  if (reset) reset.addEventListener('click', function () { state.copied = null; setBuilding(state.building); });

  buildRows();
  setBuilding('large');
})();
