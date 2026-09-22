#!/usr/bin/env node
/*
 * Offline checks for the website's download buttons (site/). No network and no packages: each page in site/ is
 * parsed into a small stub DOM, the page's release scripts (assets/site.js and assets/release.js; the interactive
 * demo's scripts are not run) run in a vm against it, and fetch answers like GitHub's API from a fixed list of releases.
 *
 * The rule every timbermods site follows: offer GitHub's Latest release (the newest one that is not a draft or a
 * pre-release), and only when there is none, the newest pre-release, and download that release's
 * MixedStorage-vX.Y.Z.zip, never another .zip attached to it. With no usable answer a page keeps the links and text it
 * was written with, and its download buttons lead to the Latest release page. What is on screen follows the page's
 * own stylesheets where they set `display` with a simple selector, because a class rule that sets `display` also
 * shows an element marked `hidden`.
 *
 *   node tests/test-site.mjs [repo root]      (default: this checkout)
 *
 * Prints one line per check and exits 1 if any check fails.
 */
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = path.resolve(process.argv[2] || path.join(path.dirname(fileURLToPath(import.meta.url)), '..'));
const site = path.join(root, 'site');
const REPO = 'timbermods/MixedStorage';
const RELEASES = `https://github.com/${REPO}/releases`;
const RELEASE_SCRIPTS = ['assets/site.js', 'assets/release.js'];

// A local file a page refers to. 404.html uses absolute /MixedStorage/ paths, because GitHub Pages serves it anywhere.
const local = (ref) => path.join(site, ref.replace(/^\/MixedStorage\//, ''));
const isRemote = (ref) => /^([a-z]+:)?\/\//i.test(ref);

// ---------- fake GitHub ----------

// A release as GitHub's API returns it. build.ps1 names the zip MixedStorage-v<version>.zip.
function release(version, { prerelease = false, draft = false, files } = {}) {
  const tag = `v${version}`;
  const names = files || [`MixedStorage-${tag}.zip`];
  return {
    tag_name: tag, name: tag, draft, prerelease, html_url: `${RELEASES}/tag/${tag}`,
    assets: names.map((name) => ({ name, size: 40960, digest: `sha256:${'ab'.repeat(32)}`, browser_download_url: `${RELEASES}/download/${tag}/${name}` })),
  };
}
const zipOf = (r) => r.assets.find((a) => a.name === `MixedStorage-${r.tag_name}.zip`);

// A fetch that answers like api.github.com for this repository, `list` newest first. /releases?per_page=N returns
// the first N; /releases/latest returns the newest release that is not a draft or a pre-release, or 404 when there
// is none. `limited` answers 403 to everything, as GitHub does once an address has used its 60 lookups an hour, and
// `latestStatus` makes /releases/latest alone fail with that status, as in an outage.
function fakeGitHub({ list = [], limited = false, latestStatus = 0 }) {
  return async (url) => {
    const reply = (status, body) => ({ ok: status === 200, status, json: async () => JSON.parse(JSON.stringify(body)) });
    const m = /^https:\/\/api\.github\.com\/repos\/([\w.-]+\/[\w.-]+)\/releases(\/latest)?(?:\?per_page=(\d+))?$/.exec(String(url));
    if (!m || m[1] !== REPO) return reply(404, { message: 'Not Found' });
    if (limited) return reply(403, { message: 'API rate limit exceeded' });
    if (!m[2]) return reply(200, list.slice(0, Number(m[3] || 30)));
    if (latestStatus) return reply(latestStatus, { message: 'Server Error' });
    const latest = list.find((r) => !r.draft && !r.prerelease);
    return latest ? reply(200, latest) : reply(404, { message: 'Not Found' });
  };
}

// ---------- stub DOM ----------

class Text {
  constructor(data) { this.nodeType = 3; this.data = data; this.parentNode = null; }
  get textContent() { return this.data; }
}

class Element {
  constructor(tag, attributes = {}) {
    this.nodeType = 1;
    this.tagName = tag.toUpperCase();
    this.attributes = { ...attributes };
    this.childNodes = [];
    this.parentNode = null;
  }
  get children() { return this.childNodes.filter((n) => n.nodeType === 1); }
  getAttribute(name) { return Object.hasOwn(this.attributes, name) ? this.attributes[name] : null; }
  setAttribute(name, value) { this.attributes[name] = String(value); }
  hasAttribute(name) { return Object.hasOwn(this.attributes, name); }
  removeAttribute(name) { delete this.attributes[name]; }
  get id() { return this.getAttribute('id') || ''; }
  get className() { return this.getAttribute('class') || ''; }
  set className(value) { this.setAttribute('class', value); }
  get href() { return this.getAttribute('href') || ''; }
  set href(value) { this.setAttribute('href', value); }
  get hidden() { return this.hasAttribute('hidden'); }
  set hidden(value) { if (value) this.setAttribute('hidden', ''); else this.removeAttribute('hidden'); }
  get textContent() { return this.childNodes.map((n) => n.textContent).join(''); }
  set textContent(value) {
    for (const n of this.childNodes) n.parentNode = null;
    this.childNodes = [];
    if (String(value) !== '') this.appendChild(new Text(String(value)));
  }
  appendChild(node) {
    if (node.parentNode) node.parentNode.removeChild(node);
    node.parentNode = this;
    this.childNodes.push(node);
    return node;
  }
  removeChild(node) {
    const i = this.childNodes.indexOf(node);
    if (i < 0) throw new Error('stub DOM: removeChild of a node that is not a child');
    this.childNodes.splice(i, 1);
    node.parentNode = null;
    return node;
  }
  addEventListener() {} // clicks and key presses never happen here
  querySelectorAll(selector) {
    const tests = compile(selector);
    const found = [];
    const walk = (el) => {
      for (const child of el.children) {
        if (tests.some((t) => t(child))) found.push(child);
        walk(child);
      }
    };
    walk(this);
    return found;
  }
  querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
}

// Compound selectors only: an optional tag name, then any number of .class and [attr] or [attr="value"], in a
// comma-separated list. Anything else throws, so a script that needs more shows up as a failure, not a quiet pass.
function compile(selector) {
  return String(selector).split(',').map((part) => {
    const s = part.trim();
    const m = /^([a-zA-Z][\w-]*)?((?:\.[\w-]+|\[[\w-]+(?:=(?:"[^"]*"|'[^']*'|[\w-]+))?\])*)$/.exec(s);
    if (!s || !m) throw new Error(`stub DOM: unsupported selector "${s}"`);
    const tag = m[1] ? m[1].toUpperCase() : null;
    const checks = [];
    for (const t of m[2].matchAll(/\.([\w-]+)|\[([\w-]+)(?:=(?:"([^"]*)"|'([^']*)'|([\w-]+)))?\]/g)) {
      if (t[1]) checks.push((el) => el.className.split(/\s+/).includes(t[1]));
      else if (t[3] === undefined && t[4] === undefined && t[5] === undefined) checks.push((el) => el.hasAttribute(t[2]));
      else { const want = t[3] ?? t[4] ?? t[5]; checks.push((el) => el.getAttribute(t[2]) === want); }
    }
    const test = (el) => (!tag || el.tagName === tag) && checks.every((c) => c(el));
    test.specificity = [checks.length, tag ? 1 : 0];
    return test;
  });
}

const VOID = new Set(['area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'source', 'track', 'wbr']);
const ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ', middot: '·', rarr: '→', larr: '←', hellip: '…', mdash: '—', ndash: '–', times: '×' };
const decode = (s) => s.replace(/&(#x[0-9a-f]+|#\d+|[a-z]+);/gi, (all, e) => {
  if (e[0] === '#') return String.fromCodePoint(e[1] === 'x' || e[1] === 'X' ? parseInt(e.slice(2), 16) : Number(e.slice(1)));
  return ENTITIES[e.toLowerCase()] ?? all;
});

function attributesOf(source) {
  const attributes = {};
  for (const a of source.matchAll(/([^\s=>\/]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s"'>]+)))?/g)) {
    attributes[a[1].toLowerCase()] = decode(a[2] ?? a[3] ?? a[4] ?? '');
  }
  return attributes;
}

// The pages are hand-written and well-formed, so a tokenizer with a stack is enough. Script and style bodies are kept
// as raw text.
function parse(html) {
  const doc = new Element('#document');
  const stack = [doc];
  const re = /<!--[\s\S]*?-->|<![^>]*>|<\/([a-zA-Z][\w-]*)\s*>|<([a-zA-Z][\w-]*)((?:\s+[^\s=>\/]+(?:\s*=\s*(?:"[^"]*"|'[^']*'|[^\s"'>]+))?)*)\s*(\/?)>|[^<]+|</g;
  let m;
  while ((m = re.exec(html))) {
    const top = stack[stack.length - 1];
    if (m[1]) {
      const tag = m[1].toUpperCase();
      for (let i = stack.length - 1; i > 0; i--) if (stack[i].tagName === tag) { stack.length = i; break; }
    } else if (m[2]) {
      const el = top.appendChild(new Element(m[2], attributesOf(m[3])));
      const tag = m[2].toLowerCase();
      if (tag === 'script' || tag === 'style') {
        const end = html.toLowerCase().indexOf(`</${tag}`, re.lastIndex);
        const stop = end < 0 ? html.length : end;
        if (stop > re.lastIndex) el.appendChild(new Text(html.slice(re.lastIndex, stop)));
        re.lastIndex = stop;
      } else if (!VOID.has(tag) && !m[4]) stack.push(el);
    } else if (!m[0].startsWith('<!')) {
      top.appendChild(new Text(decode(m[0])));
    }
  }
  return doc;
}

// The `display` rules in a page's own stylesheets whose selectors compile() understands, in source order.
function displayRules(document) {
  const rules = [];
  for (const link of document.querySelectorAll('link')) {
    const href = link.getAttribute('href') || '';
    if (link.getAttribute('rel') !== 'stylesheet' || isRemote(href)) continue;
    const css = readFileSync(local(href), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
    for (const m of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
      const d = /(?:^|;)\s*display\s*:\s*([^;!]+?)\s*(!important)?\s*(?:;|$)/.exec(m[2]);
      if (!d) continue;
      for (const selector of m[1].split(',')) {
        let test;
        try { [test] = compile(selector); } catch { continue; }
        rules.push({ selector: selector.trim(), test, none: d[1] === 'none', important: !!d[2], order: rules.length });
      }
    }
  }
  return rules;
}

const beats = (a, b) => (a.important !== b.important ? a.important
  : a.test.specificity[0] !== b.test.specificity[0] ? a.test.specificity[0] > b.test.specificity[0]
  : a.test.specificity[1] !== b.test.specificity[1] ? a.test.specificity[1] > b.test.specificity[1]
  : a.order > b.order);

class Document {
  constructor(page) {
    this.root = parse(readFileSync(path.join(site, page), 'utf8'));
    this.documentElement = this.root.children.find((el) => el.tagName === 'HTML') || this.root.appendChild(new Element('html'));
    this.rules = displayRules(this);
    this.readyState = 'interactive'; // what a deferred script sees
    this.currentScript = null;
    this.listeners = [];
  }
  get body() { return this.documentElement.querySelector('body'); }
  querySelectorAll(selector) { return this.root.querySelectorAll(selector); }
  querySelector(selector) { return this.root.querySelector(selector); }
  getElementById(id) { return this.root.querySelectorAll('[id]').find((el) => el.id === id) || null; }
  createElement(tag) { return new Element(tag); }
  createTextNode(data) { return new Text(String(data)); }
  addEventListener(type, fn) { this.listeners.push({ type, fn }); }
  removeEventListener() {}
  dispatchEvent(event) { for (const l of this.listeners) if (l.type === event.type) l.fn(event); return true; }
  // The page's rule that decides whether `el` is displayed, if any. Without one, `hidden` hides it.
  displayRule(el) {
    let win = null;
    for (const r of this.rules) if (r.test(el) && (!win || beats(r, win))) win = r;
    return win;
  }
  displayed(el) {
    if (['HEAD', 'SCRIPT', 'STYLE', 'TEMPLATE', 'NOSCRIPT'].includes(el.tagName)) return false;
    const rule = this.displayRule(el);
    return rule ? !rule.none : !el.hidden;
  }
  onScreen(el) {
    for (let n = el; n && n.nodeType === 1; n = n.parentNode) if (!this.displayed(n)) return false;
    return true;
  }
  // What a visitor reads.
  visibleText(node = this.root) {
    if (node.nodeType === 3) return node.data;
    if (node !== this.root && !this.displayed(node)) return '';
    return node.childNodes.map((n) => this.visibleText(n)).join('');
  }
}

function memoryStorage() {
  const items = new Map();
  return {
    getItem: (k) => (items.has(k) ? items.get(k) : null),
    setItem: (k, v) => { items.set(k, String(v)); },
    removeItem: (k) => { items.delete(k); },
  };
}

// ---------- running a page ----------

const pages = readdirSync(site).filter((f) => f.endsWith('.html')).sort();

// Loads one page, runs the release scripts it loads with fresh storage, then lets the fetches settle.
async function load(page, github) {
  const document = new Document(page);
  const sandbox = {
    document, fetch: fakeGitHub(github), localStorage: memoryStorage(), sessionStorage: memoryStorage(),
    CustomEvent: class CustomEvent { constructor(type, init = {}) { this.type = type; this.detail = init.detail; } },
    location: { hash: '', href: `https://timbermods.github.io/MixedStorage/${page}` },
    addEventListener() {}, removeEventListener() {},
    console, setTimeout, clearTimeout,
  };
  sandbox.window = sandbox;
  sandbox.self = sandbox;
  const context = vm.createContext(sandbox);
  for (const script of document.querySelectorAll('script')) {
    const src = script.getAttribute('src');
    if (!src || isRemote(src) || !RELEASE_SCRIPTS.includes(src.replace(/^\/MixedStorage\//, ''))) continue;
    document.currentScript = script;
    vm.runInContext(readFileSync(local(src), 'utf8'), context, { filename: src });
    document.currentScript = null;
  }
  for (let i = 0; i < 20; i++) await new Promise((resolve) => setTimeout(resolve, 0));
  return document;
}

// The download buttons, whatever marks them: button-styled links that offer a download, and links a script sends
// to the zip.
const downloadButtons = (document) => document.querySelectorAll('a').filter((a) =>
  (a.className.split(/\s+/).includes('btn') && /\bdownload\b/i.test(a.textContent))
  || a.hasAttribute('data-download') || a.getAttribute('data-release-href') === 'download');
const buttonText = (document, a) => document.visibleText(a).replace(/\s+/g, ' ').trim();
// The release labels ("Latest release", "Pre-release") on screen.
const pills = (document) => document.querySelectorAll('.pill').filter((el) => document.onScreen(el)).map((el) => el.textContent.trim());
// Every link on the page that names one release (its page or one of its files), as [tag, href].
const releaseLinks = (document) => document.querySelectorAll('a').flatMap((a) => {
  const m = new RegExp(`^${RELEASES.replace(/[.]/g, '\\.')}/(?:tag|download)/([^/]+)`).exec(a.href);
  return m ? [[decodeURIComponent(m[1]), a.href]] : [];
});
// The links release.js points at the release it offers (data-release-href), as [page, kind, href].
const scriptLinks = (docs) => [...docs].flatMap(([page, d]) => d.querySelectorAll('[data-release-href]')
  .map((a) => [page, a.getAttribute('data-release-href'), a.href]));
const loadsReleaseScript = (document) => document.querySelectorAll('script').some((s) => /(^|\/)release\.js$/.test(s.getAttribute('src') || ''));
const mentions = (text, word) => new RegExp(`(^|[^\\w.-])${word.replace(/[.+]/g, '\\$&')}(?![\\w.-]*\\w)`).test(text);

// ---------- checks ----------

let passed = 0;
let failed = 0;
function check(ok, what, detail) {
  if (ok) { passed++; console.log(`ok   ${what}`); } else { failed++; console.log(`FAIL ${what}${detail ? ` (${detail})` : ''}`); }
}

const written = new Map(pages.map((page) => [page, new Document(page)]));
const buttonsOf = (docs) => [...docs.values()].flatMap((d) => downloadButtons(d).map((a) => ({ href: a.href, text: buttonText(d, a) })));
const writtenButtons = buttonsOf(written);
const writtenLinks = [...written.values()].flatMap(releaseLinks).map(([, href]) => href);
const buttonCount = writtenButtons.length;
check(['index.html', 'install.html'].every((page) => written.has(page) && downloadButtons(written.get(page)).length > 0),
  `the overview and the install guide have download buttons to check (${buttonCount} on the site)`);
const fallbacks = writtenButtons.map((b) => b.href);
check(fallbacks.every((href) => href === `${RELEASES}/latest`),
  'as written, before any script runs, every download button leads to the Latest release page',
  [...new Set(fallbacks.filter((href) => href !== `${RELEASES}/latest`))].join(', '));
const shownHidden = pages.flatMap((page) => {
  const d = written.get(page);
  return d.querySelectorAll('[hidden]').filter((el) => d.onScreen(el))
    .map((el) => `${page}: <${el.tagName.toLowerCase()} class="${el.className}"> shown by "${d.displayRule(el).selector}"`);
});
check(shownHidden.length === 0, 'as written, before any script runs, the stylesheets hide every element marked hidden', shownHidden.join(', '));
const relative = written.has('404.html') ? written.get('404.html').querySelectorAll('script, link').map((el) => el.getAttribute('src') || el.getAttribute('href') || '')
  .filter((ref) => ref && !isRemote(ref) && !ref.startsWith('/MixedStorage/')) : ['404.html is missing'];
check(relative.length === 0, '404.html, which GitHub Pages serves at any address, loads its scripts and styles by absolute path', relative.join(', '));
const unserved = pages.filter((page) => written.get(page).querySelectorAll('[data-release], [data-release-href], [data-release-show], [data-release-pinned]').length > 0
  && !loadsReleaseScript(written.get(page)));
check(unserved.length === 0, 'every page with markup for release.js loads it', unserved.join(', '));
// release.js caches its answer per repository, not per page, so every page must ask for the same zip.
const configs = new Set(pages.flatMap((page) => written.get(page).querySelectorAll('script').filter((s) => /(^|\/)release\.js$/.test(s.getAttribute('src') || ''))
  .map((s) => `data-repo="${s.getAttribute('data-repo')}" data-asset="${s.getAttribute('data-asset')}"`)));
check(configs.size <= 1, 'every page loads release.js with the same data-repo and data-asset', [...configs].join(' vs '));
const writtenScriptLinks = scriptLinks(written).map(([page, kind, href]) => `${page} ${kind}: ${href}`);
const slots = new Set([...written.values()].flatMap((d) => d.querySelectorAll('.pill').map((el) => el.parentNode))).size;

const pre = (version, options = {}) => release(version, { ...options, prerelease: true });
const scenarios = [
  { name: 'a pre-release newer than the Latest', list: [pre('1.1.0'), release('1.0.0'), release('0.5.8')], offer: 'v1.0.0' },
  {
    name: 'twelve pre-releases after the Latest (more than one page of ten)',
    list: [...Array.from({ length: 12 }, (_, i) => pre(`1.1.${12 - i}`)), release('1.0.0')],
    offer: 'v1.0.0',
  },
  { name: 'a newer draft', list: [release('1.1.0', { draft: true }), release('1.0.0')], offer: 'v1.0.0' },
  {
    // v0.2.0 to v0.4.3 shipped the separate multiplayer add-on beside the mod, and GitHub listed it first.
    name: 'another zip listed before the mod zip',
    list: [release('0.4.3', { files: ['MixedStorage-BeaverBuddies-v0.4.3.zip', 'MixedStorage-v0.4.3.zip'] })],
    offer: 'v0.4.3',
  },
  { name: 'only stable releases', list: [release('1.0.0'), release('0.5.8')], offer: 'v1.0.0' },
  { name: 'only pre-releases', list: [pre('1.1.0'), pre('1.0.0')], offer: 'v1.1.0' },
  { name: 'the Latest has no MixedStorage zip yet', list: [release('1.1.0', { files: [] }), release('1.0.0')], offer: null },
  { name: 'GitHub refuses the lookup', list: [release('1.0.0')], limited: true, offer: null },
  // Only a 404 (no Latest at all) may fall back to a pre-release; an outage must not offer one over the Latest.
  { name: '/releases/latest fails but the release list works', list: [pre('1.1.0'), release('1.0.0')], latestStatus: 500, offer: null },
  { name: 'no releases yet', list: [], offer: null },
];

for (const scenario of scenarios) {
  const offered = scenario.list.find((r) => r.tag_name === scenario.offer) || null;
  const docs = new Map();
  let threw = null;
  for (const page of pages) {
    try { docs.set(page, await load(page, scenario)); } catch (e) { threw = `${page}: ${e && e.message}`; }
  }
  check(!threw, `${scenario.name}: every page's release scripts run`, threw);
  if (threw) continue;
  const buttons = buttonsOf(docs);
  const labels = [...docs.values()].flatMap(pills);
  const install = docs.get('install.html').visibleText();
  const links = [...docs.values()].flatMap(releaseLinks);
  // The pages' prose mentions past versions, so only what the scripts fill in is checked for other releases.
  const modZip = offered ? zipOf(offered).name : null;
  const otherZips = scenario.list.flatMap((r) => r.assets.map((a) => a.name)).filter((name) => name !== modZip && mentions(install, name));
  if (offered) {
    const zip = zipOf(offered);
    const off = buttons.filter((b) => b.href !== zip.browser_download_url).map((b) => b.href);
    check(buttons.length === buttonCount && off.length === 0, `${scenario.name}: all ${buttonCount} download buttons fetch ${zip.name}`,
      off.length ? `got ${[...new Set(off)].join(', ')}` : `found ${buttons.length} buttons`);
    const wrongText = buttons.filter((b) => b.text !== `Download ${offered.tag_name}`).map((b) => `"${b.text}"`);
    check(wrongText.length === 0, `${scenario.name}: every download button reads "Download ${offered.tag_name}"`, [...new Set(wrongText)].join(', '));
    const label = offered.prerelease ? 'Pre-release' : 'Latest release';
    check(labels.length === slots && labels.every((l) => l === label), `${scenario.name}: the ${slots} release labels read "${label}"`, `got ${JSON.stringify(labels)}`);
    check(mentions(install, zip.name) && otherZips.length === 0, `${scenario.name}: the install guide names ${zip.name} and no other file`,
      otherZips.join(', '));
    const stray = links.filter(([tag]) => tag !== offered.tag_name).map(([, href]) => href);
    check(stray.length === 0, `${scenario.name}: every link to a release goes to ${offered.tag_name}`, [...new Set(stray)].join(', '));
    const want = { download: zip.browser_download_url, notes: offered.html_url };
    const unfilled = scriptLinks(docs).filter(([, kind, href]) => href !== want[kind]).map(([page, kind, href]) => `${page} ${kind}: ${href}`);
    check(unfilled.length === 0, `${scenario.name}: on every page, every link marked for release.js goes to ${offered.tag_name}`, unfilled.join(', '));
  } else {
    const moved = buttons.filter((b, i) => b.href !== fallbacks[i]).map((b) => b.href);
    check(buttons.length === buttonCount && moved.length === 0, `${scenario.name}: the download buttons keep the links the page was written with`,
      moved.length ? [...new Set(moved)].join(', ') : `found ${buttons.length} buttons`);
    const changed = buttons.filter((b, i) => b.text !== writtenButtons[i].text).map((b) => `"${b.text}"`);
    check(changed.length === 0, `${scenario.name}: the download buttons keep the text they were written with`, [...new Set(changed)].join(', '));
    check(labels.length === 0, `${scenario.name}: no release label is shown`, `got ${JSON.stringify(labels)}`);
    check(install.includes('MixedStorage-vX.Y.Z.zip') && otherZips.length === 0, `${scenario.name}: the install guide keeps its placeholder file name`,
      otherZips.join(', '));
    const hrefs = links.map(([, href]) => href);
    check(hrefs.join('|') === writtenLinks.join('|'), `${scenario.name}: no page links to a release it was not written with`,
      [...new Set(hrefs.filter((href) => !writtenLinks.includes(href)))].join(', '));
    const now = scriptLinks(docs).map(([page, kind, href]) => `${page} ${kind}: ${href}`);
    check(now.join('|') === writtenScriptLinks.join('|'), `${scenario.name}: every link marked for release.js keeps the address it was written with`,
      now.filter((l) => !writtenScriptLinks.includes(l)).join(', '));
  }
}

console.log(failed ? `FAILED: ${failed} of ${passed + failed} site checks` : `${passed}/${passed} site checks passed`);
process.exit(failed ? 1 : 0);
