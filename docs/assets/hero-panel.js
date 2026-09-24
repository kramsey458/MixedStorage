/*
 * The hero's panel: the top of the mod's Storage Allocation panel for a full Large Warehouse (1200). The split
 * buttons redraw its cards; the limits come from MixedStorageSplit.capacities (split.js), the same rounding the mod
 * uses, and the labels are the mod's own (StorageView). The page ships with the 50 / 50 split drawn, so this only
 * enhances it.
 */
(function () {
  'use strict';
  var panel = document.querySelector('[data-hero-panel]');
  var split = window.MixedStorageSplit;
  if (!panel || !split) return;

  var CAPACITY = 1200;
  var SPLITS = {
    even: [['Carrot', 'Carrots', 'carrot', 5000], ['Gear', 'Gears', 'gear', 5000]],
    bakery: [['Bread', 'Bread', 'bread', 2500], ['WheatFlour', 'Wheat flour', 'wheat-flour', 7500]],
    thirds: [['Carrot', 'Carrots', 'carrot', 3334], ['Potato', 'Potatoes', 'potato', 3333], ['Berries', 'Berries', 'berries', 3333]]
  };

  var summary = panel.querySelector('[data-hero-summary]');
  var cards = panel.querySelector('[data-hero-cards]');
  var said = document.querySelector('[data-hero-said]');
  var buttons = document.querySelectorAll('[data-split]');

  function pct(units) { return String(units / 100); }

  function draw(name) {
    var goods = SPLITS[name];
    var limits = split.capacities(goods.map(function (g) { return { id: g[0], units: g[3] }; }), CAPACITY);
    summary.textContent = CAPACITY + ' / ' + CAPACITY + ' items · ' + goods.length + ' goods allocated';
    cards.innerHTML = goods.map(function (g, i) {
      return '<div class="ig-card"><div class="ig-card__line"><span class="ig-card__icon"><img src="assets/goods/' + g[2] + '-60.png" alt="" width="30" height="30"></span>' +
        '<span class="ig-card__main"><b>' + g[1] + '</b><span>' + pct(g[3]) + '% allocated</span></span>' +
        '<span class="ig-card__count"><b>' + limits[i] + ' / ' + limits[i] + '</b><span>stored / limit</span></span></div>' +
        '<div class="ig-bar-track"><i style="width:100%"></i></div></div>';
    }).join('');
    if (said) said.textContent = goods.map(function (g, i) { return pct(g[3]) + '% ' + g[1].toLowerCase() + ': ' + limits[i]; }).join(', ') + ', of 1200.';
    Array.prototype.forEach.call(buttons, function (b) { b.setAttribute('aria-pressed', b.getAttribute('data-split') === name ? 'true' : 'false'); });
  }

  Array.prototype.forEach.call(buttons, function (b) {
    b.hidden = false;
    b.addEventListener('click', function () { draw(b.getAttribute('data-split')); });
  });
})();
