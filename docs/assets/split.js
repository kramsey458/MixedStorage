/*
 * Mirrors AllocationPlan.Capacities from the MixedStorage mod (source/AllocationPlan.cs).
 * Percentages are integer hundredths of a percent (10,000 = 100%). Each share is floored to whole
 * items, then leftover slots go to the largest fractions. Ties break by ordinal good id.
 */
(function (root) {
  'use strict';
  var TOTAL = 10000;

  // shares: [{ id: string, units: integer 0..10000 }] totaling exactly TOTAL. Returns limits in the same order.
  function capacities(shares, capacity) {
    var limits = shares.map(function (s) { return Math.floor(capacity * s.units / TOTAL); });
    var remainder = capacity - limits.reduce(function (a, b) { return a + b; }, 0);
    var order = [];
    shares.forEach(function (s, i) {
      if (s.units > 0) order.push({ i: i, id: s.id, fraction: (capacity * s.units) % TOTAL });
    });
    order.sort(function (a, b) {
      if (a.fraction !== b.fraction) return b.fraction - a.fraction;
      return a.id < b.id ? -1 : a.id > b.id ? 1 : 0;
    });
    order.slice(0, remainder).forEach(function (x) { limits[x.i]++; });
    return limits;
  }

  var api = { TOTAL: TOTAL, capacities: capacities };
  root.MixedStorageSplit = api;
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
