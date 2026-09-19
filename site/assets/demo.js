/* Interactive split demo. Uses the same rounding as the mod (see split.js). */
(function () {
  'use strict';
  var root = document.querySelector('[data-demo]');
  if (!root || !window.MixedStorageSplit) return;

  var Split = window.MixedStorageSplit;
  var goods = [
    { id: 'Carrots', color: '#ffa60f' },
    { id: 'Gears', color: '#7cc4d9' },
    { id: 'Planks', color: '#c9a56a' }
  ];
  var presets = {
    even:   { capacity: 1200, values: ['50', '50', '0'] },
    thirds: { capacity: 200,  values: ['33.33', '33.33', '33.34'] },
    tiny:   { capacity: 200,  values: ['99.8', '0.2', '0'] }
  };

  var capInput = root.querySelector('[data-cap]');
  var bar = root.querySelector('[data-bar]');
  var total = root.querySelector('[data-total]');
  var note = root.querySelector('[data-note]');
  var rows = goods.map(function (g, i) {
    return {
      good: g,
      input: root.querySelector('[data-pct="' + i + '"]'),
      limit: root.querySelector('[data-limit="' + i + '"]'),
      seg: bar.children[i]
    };
  });

  // Same rules as the mod: 0-100 with at most two decimals. Returns hundredths of a percent, or null.
  function parsePercent(text) {
    text = String(text).trim().replace(',', '.');
    if (!/^\+?(\d+\.?\d*|\.\d+)$/.test(text)) return null;
    var value = Number(text);
    if (!(value >= 0 && value <= 100)) return null;
    var units = Math.round(value * 100);
    return Math.abs(value * 100 - units) < 1e-7 ? units : null;
  }
  function pctText(units) { return (units / 100).toLocaleString('en-US', { maximumFractionDigits: 2 }); }

  function update() {
    var capacity = Math.floor(Number(capInput.value));
    var capacityOk = isFinite(capacity) && capacity >= 1 && capacity <= 100000;
    capInput.setAttribute('aria-invalid', capacityOk ? 'false' : 'true');

    var units = rows.map(function (r) { return parsePercent(r.input.value); });
    var fieldsOk = true;
    rows.forEach(function (r, i) {
      var ok = units[i] !== null;
      if (!ok) fieldsOk = false;
      r.input.setAttribute('aria-invalid', ok ? 'false' : 'true');
    });

    var sum = units.reduce(function (a, u) { return a + (u || 0); }, 0);
    var valid = fieldsOk && capacityOk && sum === Split.TOTAL;

    var limits = null;
    if (valid) limits = Split.capacities(rows.map(function (r, i) { return { id: r.good.id, units: units[i] }; }), capacity);

    rows.forEach(function (r, i) {
      r.limit.textContent = limits ? limits[i].toLocaleString('en-US') : '—';
      r.seg.style.width = limits ? (limits[i] / capacity * 100) + '%' : '0%';
      r.seg.style.background = r.good.color;
    });

    var totalBad = !fieldsOk || sum !== Split.TOTAL;
    if (!fieldsOk) {
      total.textContent = 'Enter valid percentages (0–100, 2 decimals)';
    } else if (sum === Split.TOTAL) {
      total.textContent = '100% / 100% allocated';
    } else {
      var diff = Math.abs(sum - Split.TOTAL);
      total.textContent = pctText(sum) + '% / 100% — ' + pctText(diff) + (sum < Split.TOTAL ? '% remaining' : '% over');
    }
    total.classList.toggle('is-bad', totalBad);

    var zero = 0;
    if (limits) rows.forEach(function (r, i) { if (units[i] > 0 && limits[i] === 0) zero++; });
    var bad = false, text;
    if (!capacityOk) { text = 'Capacity must be a whole number from 1 to 100,000.'; bad = true; }
    else if (!valid) { text = 'Apply is unavailable until every percentage is valid and the total is exactly 100%.'; bad = true; }
    else if (zero > 0) { text = zero + ' allocated good(s) round to 0 items. Increase their shares or use larger storage.'; bad = true; }
    else { text = 'Limits round to whole items; leftover slots go to the largest fractions. All slots are allocated.'; }
    note.textContent = text;
    note.classList.toggle('is-bad', bad);
  }

  function setPreset(name) {
    var p = presets[name];
    if (!p) return;
    capInput.value = p.capacity;
    rows.forEach(function (r, i) { r.input.value = p.values[i]; });
    update();
  }

  capInput.addEventListener('input', update);
  rows.forEach(function (r) { r.input.addEventListener('input', update); });
  Array.prototype.forEach.call(root.querySelectorAll('[data-preset]'), function (btn) {
    btn.addEventListener('click', function () { setPreset(btn.getAttribute('data-preset')); });
  });
  setPreset('even');
})();
