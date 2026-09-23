/*
 * The hero's cabinet: one Large Warehouse (1,200) drawn as a cabinet whose drawers take the building's space by
 * share. The split buttons re-proportion the drawers; the counts come from MixedStorageSplit.capacities (split.js),
 * the same rounding the mod uses. The page ships with the 50 / 50 split already drawn, so this only enhances it.
 */
(function () {
  'use strict';
  var cab = document.querySelector('[data-cabinet]');
  var split = window.MixedStorageSplit;
  if (!cab || !split) return;

  var CAPACITY = 1200;
  var SPLITS = {
    even: [['Carrot', 'Carrots', 'carrot', 5000], ['Gear', 'Gears', 'gear', 5000]],
    bakery: [['Bread', 'Bread', 'bread', 2500], ['WheatFlour', 'Wheat flour', 'wheat-flour', 7500]],
    thirds: [['Carrot', 'Carrots', 'carrot', 3334], ['Potato', 'Potatoes', 'potato', 3333], ['Berries', 'Berries', 'berries', 3333]]
  };

  var body = cab.querySelector('.cab__body');
  var reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  var said = document.querySelector('[data-cabinet-said]');
  var buttons = document.querySelectorAll('[data-split]');

  function pct(units) { return (units / 100).toFixed(units % 100 ? 2 : 0) + '%'; }

  function draw(name) {
    var goods = SPLITS[name];
    var limits = split.capacities(goods.map(function (g) { return { id: g[0], units: g[3] }; }), CAPACITY);
    var drawers = Array.prototype.slice.call(body.querySelectorAll('.drawer:not(.is-leaving)'));
    // A good that joins opens a new drawer from nothing; one that leaves closes to nothing and is taken out.
    while (drawers.length < goods.length) {
      var d = document.createElement('div');
      d.className = 'drawer is-entering';
      d.style.setProperty('--share', 0);
      d.innerHTML = '<span class="holder"><img src="' + drawers[0].querySelector('img').src + '" alt="" width="30" height="30">'
        + '<span class="name"></span><span class="count"><b></b><small></small></span></span><i class="pull" aria-hidden="true"></i>';
      body.appendChild(d);
      drawers.push(d);
    }
    drawers.slice(goods.length).forEach(function (d) {
      d.style.setProperty('--share', 0);
      d.classList.add('is-leaving');
      setTimeout(function () { if (d.parentNode) d.parentNode.removeChild(d); }, reduced ? 0 : 650);
    });
    void body.offsetHeight;
    // Every remaining drawer takes its new share and slides to its new height.
    goods.forEach(function (g, i) {
      drawers[i].classList.remove('is-entering');
      var d = drawers[i];
      d.style.setProperty('--share', g[3]);
      d.querySelector('img').src = 'assets/goods/' + g[2] + '-60.png';
      d.querySelector('.name').textContent = g[1];
      d.querySelector('.count b').textContent = limits[i].toLocaleString('en-US');
      d.querySelector('.count small').textContent = pct(g[3]);
    });
    if (said) said.textContent = goods.map(function (g, i) { return pct(g[3]) + ' ' + g[1].toLowerCase() + ': ' + limits[i].toLocaleString('en-US'); }).join(', ') + ', of 1,200.';
    Array.prototype.forEach.call(buttons, function (b) { b.setAttribute('aria-pressed', b.getAttribute('data-split') === name ? 'true' : 'false'); });
  }

  Array.prototype.forEach.call(buttons, function (b) {
    b.hidden = false;
    b.addEventListener('click', function () { draw(b.getAttribute('data-split')); });
  });
})();
