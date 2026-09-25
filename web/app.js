'use strict';

const API = location.protocol === 'file:' ? 'http://localhost:5065' : '';
const ICON = {
  Laptop: 'laptop', Telefon: 'phone', Kulaklık: 'headphones', Saat: 'watch', Tablet: 'tablet', Aksesuar: 'plug',
  Televizyon: 'tv', Hoparlör: 'speaker', Kamera: 'camera', 'Oyun Konsolu': 'gamepad',
};
/** SVG ikon (index.html'deki sprite'tan). */
function icon(name) {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('class', 'ic');
  svg.setAttribute('aria-hidden', 'true');
  const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
  use.setAttribute('href', '#i-' + name);
  svg.append(use);
  return svg;
}
const GRAD = {
  Laptop: 'g-Laptop', Telefon: 'g-Telefon', Kulaklık: 'g-Kulaklık', Saat: 'g-Saat', Tablet: 'g-Tablet', Aksesuar: 'g-Aksesuar',
  Televizyon: 'g-Televizyon', Hoparlör: 'g-Hoparlor', Kamera: 'g-Kamera', 'Oyun Konsolu': 'g-Konsol',
};
const PAGE_SIZE = 24;
const $ = (id) => document.getElementById(id);

const state = {
  products: [], // ekrandaki (yüklenmiş) ürünler
  total: 0,
  page: 1,
  facets: { categories: [], total: 0 },
  category: null,
  query: '',
  sort: '',
  maxPrice: '',
  cart: loadCart(), // { [productId]: { product, qty } }
  chat: [], // { role, content }
  chatBusy: false,
};

function load(key, fallback) {
  try { return JSON.parse(localStorage.getItem(key)) ?? fallback; } catch { return fallback; }
}
function save(key, value) {
  try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* özel sekme vb. */ }
}

// Eski biçimden (id -> adet) kalan kayıtları at.
function loadCart() {
  const raw = load('cart', {});
  return Object.fromEntries(Object.entries(raw).filter(([, l]) => l && typeof l === 'object' && l.product));
}

const tl = (n) => new Intl.NumberFormat('tr-TR').format(n) + ' ₺';
const iconOf = (p) => icon(ICON[p.category] ?? 'bag');
const gradOf = (p) => GRAD[p.category] ?? 'g-default';
const known = new Map(); // id -> ürün (listeden, sohbetten, sepetten görülenler)
const remember = (list) => list.forEach((p) => known.set(p.id, p));
const byId = (id) => known.get(id);

function visual(p, cls) {
  const box = el('div', { class: cls });
  box.append(el('span', { class: 'emoji' }, iconOf(p)));
  if (p.imageUrl) {
    const img = el('img', { src: p.imageUrl, alt: p.name, loading: 'lazy', referrerpolicy: 'no-referrer' });
    img.addEventListener('load', () => box.querySelector('.emoji')?.remove());
    img.addEventListener('error', () => img.remove());
    box.append(img);
  }
  return box;
}

function el(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(props)) {
    if (k === 'class') node.className = v;
    else if (k.startsWith('on')) node.addEventListener(k.slice(2), v);
    else if (v !== false && v != null) node.setAttribute(k, v === true ? '' : v);
  }
  for (const c of children.flat()) if (c != null) node.append(c);
  return node;
}

let toastTimer;
function toast(msg, iconName) {
  const t = $('toast');
  t.replaceChildren(...(iconName ? [icon(iconName)] : []), msg);
  t.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => t.classList.remove('show'), 2200);
}

/* ---------- Ürünler ---------- */
let reqId = 0;

async function getJson(path) {
  const res = await fetch(`${API}${path}`);
  if (!res.ok) throw new Error(`Sunucu hatası (${res.status})`);
  return res.json();
}

async function loadFacets() {
  state.facets = await getJson('/api/facets');
  const f = state.facets;
  $('fact-products').textContent = new Intl.NumberFormat('tr-TR').format(f.total);
  $('fact-cats').textContent = f.categories.length;
  $('stat-products').dataset.to = f.total;
  $('stat-cats').dataset.to = f.categories.length;
  $('stat-brands').dataset.to = f.brandCount ?? 0;
  observeCounters();
  renderChips();
}

function listUrl(page) {
  const q = new URLSearchParams({ page, pageSize: PAGE_SIZE });
  if (state.query.trim()) q.set('q', state.query.trim());
  if (state.category) q.set('category', state.category);
  if (state.sort) q.set('sort', state.sort);
  if (state.maxPrice) q.set('maxPrice', state.maxPrice);
  return `/api/products?${q}`;
}

async function fetchProducts({ append = false } = {}) {
  const id = ++reqId;
  if (!append) {
    $('grid').replaceChildren(...Array.from({ length: 6 }, () => el('div', { class: 'skeleton' })));
    $('state').replaceChildren();
    $('more').hidden = true;
  }
  try {
    const data = await getJson(listUrl(append ? state.page + 1 : 1));
    if (id !== reqId) return; // daha yeni bir istek var
    state.page = data.page;
    state.total = data.total;
    state.products = append ? [...state.products, ...data.items] : data.items;
    remember(data.items);
    renderGrid(append ? data.items.length : 0);
    renderCart();
  } catch (e) {
    if (id !== reqId) return;
    $('grid').replaceChildren();
    $('result-info').textContent = '';
    $('more').hidden = true;
    $('state').replaceChildren(
      el('div', { class: 'state-msg' }, icon('alert'), 'Ürünler yüklenemedi.'),
      el('div', { class: 'small' }, String(e.message || e)),
      el('button', { class: 'btn ghost retry', onclick: () => fetchProducts() }, 'Tekrar dene'),
    );
  }
}

let searchTimer;
function refetchSoon() {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => fetchProducts(), 250);
}

function renderChips() {
  const make = (label, value, iconName) =>
    el('button', {
      class: 'chip', role: 'tab', 'aria-selected': String(state.category === value),
      onclick: () => { state.category = value; renderChips(); fetchProducts(); },
    }, iconName ? icon(iconName) : null, label);
  $('chips').replaceChildren(
    make('Tümü', null),
    ...state.facets.categories.map((c) => make(`${c.name} (${c.count})`, c.name, ICON[c.name] ?? 'bag')),
  );
}

function stockTag(p) {
  if (p.stock <= 0) return el('span', { class: 'stock-tag out' }, 'Tükendi');
  if (p.stock <= 10) return el('span', { class: 'stock-tag low' }, `Son ${p.stock} adet`);
  return el('span', { class: 'stock-tag' }, 'Stokta');
}

function ratingEl(p) {
  if (!p.rating) return null;
  return el('span', { class: 'rating' }, el('b', {}, icon('star')), p.rating.toFixed(1), p.ratingCount ? ` (${p.ratingCount})` : '');
}

function renderGrid(appendedCount = 0) {
  const list = state.products;
  $('result-info').textContent = `${state.total} ürün bulundu`;
  $('state').replaceChildren(list.length ? '' : el('div', { class: 'state-msg' }, icon('search'), 'Aramana uygun ürün bulunamadı.'));
  $('more').hidden = list.length >= state.total;
  const cards = list.map((p, i) => {
    const vis = visual(p, `visual ${gradOf(p)}`);
    vis.append(stockTag(p));
    const card = el('article', { class: 'card', tabindex: '0' },
      vis,
      el('div', { class: 'card-body' },
        el('div', { class: 'meta' }, `${p.brand} · ${p.category}`),
        el('h3', {}, p.name),
        ratingEl(p),
        el('p', { class: 'desc' }, p.description),
        el('div', { class: 'card-foot' },
          el('span', { class: 'price' }, tl(p.price)),
          el('button', {
            class: 'add', disabled: p.stock <= 0,
            onclick: (e) => { e.stopPropagation(); addToCart(p.id); },
          }, p.stock <= 0 ? 'Tükendi' : 'Sepete ekle'),
        ),
      ),
    );
    card.addEventListener('click', () => openModal(p.id));
    card.addEventListener('keydown', (e) => { if (e.key === 'Enter') openModal(p.id); });
    return card;
  });
  $('grid').replaceChildren(...cards);
}

/* ---------- Ürün detayı ---------- */
let modalProduct = null;
function openModal(id) {
  const p = byId(id);
  if (!p) return;
  modalProduct = p;
  const v = visual(p, `m-visual ${gradOf(p)}`);
  v.id = 'm-visual';
  $('m-visual').replaceWith(v);
  $('m-name').textContent = p.name;
  const specLines = Object.entries(p.specifications ?? {}).map(([k, v]) => `${k}: ${v}`);
  $('m-desc').textContent = [p.description, specLines.join('\n')].filter(Boolean).join('\n\n');
  $('m-price').textContent = tl(p.price);
  $('m-tags').replaceChildren(
    el('span', { class: 'tag' }, p.brand),
    el('span', { class: 'tag' }, p.category),
    el('span', { class: `tag ${p.stock > 0 ? 'ok' : 'bad'}` }, p.stock > 0 ? `Stokta: ${p.stock}` : 'Stokta yok'),
  );
  const add = $('m-add');
  add.disabled = p.stock <= 0;
  add.textContent = p.stock > 0 ? 'Sepete ekle' : 'Tükendi';
  $('modal').hidden = false;
  document.body.style.overflow = 'hidden';
}
function closeModal() {
  $('modal').hidden = true;
  document.body.style.overflow = '';
}

/* ---------- Sepet ---------- */
function addToCart(id, qty = 1) {
  const p = byId(id);
  if (!p || p.stock <= 0) return;
  const cur = state.cart[id]?.qty || 0;
  const next = Math.min(cur + qty, p.stock);
  if (next === cur) return toast(`En fazla ${p.stock} adet ekleyebilirsin`);
  state.cart[id] = { product: p, qty: next };
  persistCart();
  toast(`${p.name} sepete eklendi`);
}
function setQty(id, qty) {
  const line = state.cart[id];
  if (!line || qty <= 0) delete state.cart[id];
  else line.qty = Math.min(qty, line.product.stock);
  persistCart();
}
function persistCart() {
  save('cart', state.cart);
  renderCart();
}

function renderCart() {
  const entries = Object.values(state.cart).map((l) => ({ p: l.product, qty: l.qty }));
  const count = entries.reduce((s, e) => s + e.qty, 0);
  const total = entries.reduce((s, e) => s + e.qty * e.p.price, 0);
  $('cart-count').hidden = count === 0;
  $('cart-count').textContent = count;

  if (!entries.length) {
    $('cart-items').replaceChildren(el('div', { class: 'empty' }, el('span', {}, icon('cart')), el('strong', {}, 'Sepetin boş'), el('small', {}, 'Beğendiğin ürünleri sepete ekle.')));
    $('cart-foot').replaceChildren();
    return;
  }
  $('cart-items').replaceChildren(...entries.map(({ p, qty }) =>
    el('div', { class: 'line' },
      visual(p, `mini ${gradOf(p)}`),
      el('div', {},
        el('strong', {}, p.name),
        el('small', {}, tl(p.price)),
        el('div', { class: 'qty' },
          el('button', { 'aria-label': 'Azalt', onclick: () => setQty(p.id, qty - 1) }, '−'),
          el('span', {}, String(qty)),
          el('button', { 'aria-label': 'Artır', onclick: () => setQty(p.id, qty + 1) }, '+'),
        ),
      ),
      el('button', { class: 'remove', 'aria-label': `${p.name} ürününü kaldır`, onclick: () => setQty(p.id, 0) }, icon('trash')),
    ),
  ));
  $('cart-foot').replaceChildren(
    el('div', { class: 'total' }, el('span', {}, 'Toplam'), el('b', {}, tl(total))),
    el('button', { class: 'btn primary block', onclick: checkout }, 'Siparişi tamamla'),
  );
}

function checkout() {
  // Demo: ödeme entegrasyonu yok, sadece akışı gösteriyoruz.
  state.cart = {};
  persistCart();
  closeDrawer();
  toast('Siparişin alındı! (demo)', 'check');
}

function openDrawer() {
  $('scrim').hidden = false;
  $('drawer').classList.add('open');
  $('drawer').setAttribute('aria-hidden', 'false');
}
function closeDrawer() {
  $('scrim').hidden = true;
  $('drawer').classList.remove('open');
  $('drawer').setAttribute('aria-hidden', 'true');
}

/* ---------- Sohbet ---------- */
const SUGGESTIONS = [
  '50 bin TL altı bir laptop öner',
  'Kablosuz kulaklık arıyorum',
  'En yüksek puanlı telefonlar hangileri?',
  'Samsung tablet var mı?',
];

function openChat() {
  $('chat').hidden = false;
  $('chat-fab').hidden = true;
  if (!$('chat-body').children.length) {
    botMessage('Merhaba! Bütçeni ve ne aradığını yaz, sana uygun ürünleri önereyim.');
    $('suggestions').replaceChildren(...SUGGESTIONS.map((s) =>
      el('button', { type: 'button', onclick: () => sendChat(s) }, s)));
  }
  $('chat-input').focus();
}
function closeChat() {
  $('chat').hidden = true;
  $('chat-fab').hidden = false;
}

function pushBody(node) {
  const body = $('chat-body');
  body.append(node);
  body.scrollTop = body.scrollHeight;
  return node;
}
function botMessage(text, cls = '') { return pushBody(el('div', { class: `msg bot ${cls}` }, text)); }

function recommendation(p) {
  return el('div', { class: 'rec' },
    visual(p, `mini ${gradOf(p)}`),
    el('div', {}, el('strong', { onclick: () => openModal(p.id) }, p.name), el('small', {}, `${tl(p.price)} · ${p.brand}`)),
    el('button', { class: 'add', disabled: p.stock <= 0, onclick: () => addToCart(p.id) }, 'Ekle'),
  );
}

async function sendChat(text) {
  text = text.trim();
  if (!text || state.chatBusy) return;
  state.chatBusy = true;
  $('chat-form').querySelector('.send').disabled = true;
  $('suggestions').replaceChildren();
  state.chat.push({ role: 'user', content: text });
  pushBody(el('div', { class: 'msg user' }, text));
  const typing = pushBody(el('div', { class: 'msg bot' }, el('span', { class: 'typing' }, el('i'), el('i'), el('i'))));

  try {
    const res = await fetch(`${API}/api/chat`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ messages: state.chat }),
    });
    if (!res.ok) throw new Error(`Asistan cevap veremedi (${res.status})`);
    const data = await res.json();
    typing.remove();
    state.chat.push({ role: 'assistant', content: data.reply });
    botMessage(data.reply);
    remember(data.products ?? []);
    for (const p of data.products ?? []) pushBody(recommendation(p));
    if (data.options?.length) {
      pushBody(el('div', { class: 'opts' }, ...data.options.map((t) =>
        el('button', { type: 'button', onclick: (e) => { e.currentTarget.parentElement.remove(); sendChat(t); } }, t))));
    }
  } catch (e) {
    typing.remove();
    state.chat.pop(); // başarısız mesaj geçmişte kalmasın
    botMessage(`${e.message || e}. Biraz sonra tekrar dene.`, 'err');
  } finally {
    state.chatBusy = false;
    $('chat-form').querySelector('.send').disabled = false;
    $('chat-input').focus();
  }
}

/* ---------- Ana sayfa: hero, demolar, sayaçlar ---------- */
const HERO_PROMPTS = [
  '50 bin TL altı yazılım için laptop',
  'Kablosuz kulaklık öner',
  '10 bin TL altı telefon',
  'Kargo ve iade nasıl?',
];

/** Sohbeti açar; metin varsa doğrudan asistana gönderir. */
function askAssistant(text) {
  openChat();
  if (text) sendChat(text);
}

function renderHeroPrompts() {
  $('hero-prompts').replaceChildren(...HERO_PROMPTS.map((t) =>
    el('button', { type: 'button', onclick: () => askAssistant(t) }, t)));
}

function startWordRotation() {
  const words = ['laptopu', 'telefonu', 'kulaklığı', 'televizyonu', 'tableti', 'kamerayı'];
  const node = $('rot');
  let i = 0;
  setInterval(() => {
    i = (i + 1) % words.length;
    node.textContent = words[i];
    node.style.animation = 'none';
    void node.offsetWidth; // animasyonu yeniden başlat
    node.style.animation = '';
  }, 2200);
}

/** Telefon maketlerindeki mini ürün kartı. */
function demoCard(p) {
  const img = visual(p, `dimg ${gradOf(p)}`);
  return el('div', { class: 'dcard' },
    img,
    el('div', { class: 'dbody' },
      el('strong', {}, p.name),
      el('small', {}, p.brand),
      el('span', { class: 'dprice' }, tl(p.price)),
      el('button', { onclick: () => openModal(p.id) }, 'Detay'),
      el('button', { class: 'dark', onclick: () => addToCart(p.id) }, 'Sepete ekle'),
    ),
  );
}

const DEMOS = {
  laptop: 'category=Laptop&maxPrice=40000&sort=rating&inStock=true&pageSize=2',
  headphone: 'category=Kulakl%C4%B1k&sort=rating&inStock=true&pageSize=2',
  phone: 'category=Telefon&maxPrice=10000&sort=rating&inStock=true&pageSize=2',
};

async function loadDemos() {
  const targets = [
    ...document.querySelectorAll('.mini-cards[data-scenario]'),
    { dataset: { scenario: 'laptop' }, hero: $('hero-cards') },
  ];
  await Promise.all(targets.map(async (t) => {
    const box = t.hero ?? t;
    try {
      const data = await getJson(`/api/products?${DEMOS[t.dataset.scenario]}`);
      remember(data.items);
      box.replaceChildren(...data.items.map(demoCard));
    } catch { box.replaceChildren(); }
  }));
}

/** Sayaçları ekrana girince 0'dan hedef değere doğru saydırır. */
let counterObserver;
function observeCounters() {
  counterObserver?.disconnect();
  counterObserver = new IntersectionObserver((entries) => {
    for (const e of entries) {
      if (!e.isIntersecting) continue;
      counterObserver.unobserve(e.target);
      countUp(e.target, Number(e.target.dataset.to) || 0);
    }
  }, { threshold: 0.4 });
  document.querySelectorAll('.count').forEach((n) => counterObserver.observe(n));
}
function countUp(node, to) {
  const start = performance.now(), dur = 1200;
  const fmt = new Intl.NumberFormat('tr-TR');
  const tick = (now) => {
    const t = Math.min((now - start) / dur, 1);
    node.textContent = fmt.format(Math.round(to * (1 - Math.pow(1 - t, 3))));
    if (t < 1) requestAnimationFrame(tick);
  };
  requestAnimationFrame(tick);
}

/* ---------- Bağlantılar ---------- */
$('search').addEventListener('input', (e) => { state.query = e.target.value; refetchSoon(); });
$('sort').addEventListener('change', (e) => { state.sort = e.target.value; fetchProducts(); });
$('max-price').addEventListener('change', (e) => { state.maxPrice = e.target.value; fetchProducts(); });
$('more').addEventListener('click', () => fetchProducts({ append: true }));
$('cart-btn').addEventListener('click', openDrawer);
$('drawer-close').addEventListener('click', closeDrawer);
$('scrim').addEventListener('click', closeDrawer);
$('modal-close').addEventListener('click', closeModal);
$('modal').addEventListener('click', (e) => { if (e.target === $('modal')) closeModal(); });
$('m-add').addEventListener('click', () => { if (modalProduct) addToCart(modalProduct.id); });
$('chat-fab').addEventListener('click', openChat);
$('nav-chat').addEventListener('click', openChat);
$('footer-chat').addEventListener('click', openChat);
$('ask-form').addEventListener('submit', (e) => {
  e.preventDefault();
  const text = $('ask-input').value.trim();
  $('ask-input').value = '';
  askAssistant(text);
});
$('chat-close').addEventListener('click', closeChat);
$('chat-form').addEventListener('submit', (e) => {
  e.preventDefault();
  const input = $('chat-input');
  const text = input.value;
  input.value = '';
  sendChat(text);
});
document.addEventListener('keydown', (e) => {
  if (e.key !== 'Escape') return;
  if (!$('modal').hidden) closeModal();
  else if ($('drawer').classList.contains('open')) closeDrawer();
  else if (!$('chat').hidden) closeChat();
});

$('year').textContent = new Date().getFullYear();
renderCart();
renderHeroPrompts();
startWordRotation();
fetchProducts();
loadFacets().catch(() => {});
loadDemos();
