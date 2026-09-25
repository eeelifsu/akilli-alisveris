'use strict';

const API = location.protocol === 'file:' ? 'http://localhost:5065' : '';
const EMOJI = { Laptop: '💻', Telefon: '📱', Kulaklık: '🎧', Saat: '⌚', Tablet: '📲' };
const $ = (id) => document.getElementById(id);

const state = {
  products: [],
  category: null,
  query: '',
  sort: 'default',
  cart: load('cart', {}), // { [productId]: qty }
  chat: [], // { role, content }
  chatBusy: false,
};

function load(key, fallback) {
  try { return JSON.parse(localStorage.getItem(key)) ?? fallback; } catch { return fallback; }
}
function save(key, value) {
  try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* özel sekme vb. */ }
}

const tl = (n) => new Intl.NumberFormat('tr-TR').format(n) + ' ₺';
const emojiOf = (p) => EMOJI[p.category] ?? '🛍️';
const gradOf = (p) => (EMOJI[p.category] ? `g-${p.category}` : 'g-default');
const byId = (id) => state.products.find((p) => p.id === id);

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
function toast(msg) {
  const t = $('toast');
  t.textContent = msg;
  t.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => t.classList.remove('show'), 2200);
}

/* ---------- Ürünler ---------- */
async function fetchProducts() {
  const skeleton = Array.from({ length: 6 }, () => el('div', { class: 'skeleton' }));
  $('grid').replaceChildren(...skeleton);
  $('state').replaceChildren();
  try {
    const res = await fetch(`${API}/api/products`);
    if (!res.ok) throw new Error(`Sunucu hatası (${res.status})`);
    state.products = await res.json();
    $('fact-products').textContent = state.products.length;
    $('fact-cats').textContent = new Set(state.products.map((p) => p.category)).size;
    renderChips();
    renderGrid();
    renderCart();
  } catch (e) {
    $('grid').replaceChildren();
    $('result-info').textContent = '';
    $('state').replaceChildren(
      el('div', {}, '😕 Ürünler yüklenemedi.'),
      el('div', { class: 'small' }, String(e.message || e)),
      el('button', { class: 'btn ghost retry', onclick: fetchProducts }, 'Tekrar dene'),
    );
  }
}

function visibleProducts() {
  const q = state.query.trim().toLocaleLowerCase('tr');
  let list = state.products.filter((p) => {
    if (state.category && p.category !== state.category) return false;
    if (!q) return true;
    return [p.name, p.brand, p.category, p.description].some((s) => s.toLocaleLowerCase('tr').includes(q));
  });
  if (state.sort === 'asc') list = [...list].sort((a, b) => a.price - b.price);
  if (state.sort === 'desc') list = [...list].sort((a, b) => b.price - a.price);
  return list;
}

function renderChips() {
  const cats = [...new Set(state.products.map((p) => p.category))].sort((a, b) => a.localeCompare(b, 'tr'));
  const make = (label, value) =>
    el('button', {
      class: 'chip', role: 'tab', 'aria-selected': String(state.category === value),
      onclick: () => { state.category = value; renderChips(); renderGrid(); },
    }, label);
  $('chips').replaceChildren(make('Tümü', null), ...cats.map((c) => make(`${EMOJI[c] ?? '🛍️'} ${c}`, c)));
}

function stockTag(p) {
  if (p.stock <= 0) return el('span', { class: 'stock-tag out' }, 'Tükendi');
  if (p.stock <= 10) return el('span', { class: 'stock-tag low' }, `Son ${p.stock} adet`);
  return el('span', { class: 'stock-tag' }, 'Stokta');
}

function renderGrid() {
  const list = visibleProducts();
  $('result-info').textContent = `${list.length} ürün bulundu`;
  $('state').replaceChildren(list.length ? '' : el('div', {}, '🔍 Aramana uygun ürün bulunamadı.'));
  $('grid').replaceChildren(...list.map((p, i) => {
    const card = el('article', { class: 'card', style: `animation-delay:${Math.min(i, 12) * 40}ms`, tabindex: '0' },
      el('div', { class: `visual ${gradOf(p)}` }, el('span', { class: 'emoji' }, emojiOf(p)), stockTag(p)),
      el('div', { class: 'card-body' },
        el('div', { class: 'meta' }, `${p.brand} · ${p.category}`),
        el('h3', {}, p.name),
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
  }));
}

/* ---------- Ürün detayı ---------- */
let modalProduct = null;
function openModal(id) {
  const p = byId(id);
  if (!p) return;
  modalProduct = p;
  const v = $('m-visual');
  v.className = `m-visual ${gradOf(p)}`;
  v.textContent = emojiOf(p);
  $('m-name').textContent = p.name;
  $('m-desc').textContent = p.description;
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
  const next = Math.min((state.cart[id] || 0) + qty, p.stock);
  if (next === state.cart[id]) return toast(`En fazla ${p.stock} adet ekleyebilirsin`);
  state.cart[id] = next;
  persistCart();
  toast(`${p.name} sepete eklendi`);
}
function setQty(id, qty) {
  const p = byId(id);
  if (!p || qty <= 0) delete state.cart[id];
  else state.cart[id] = Math.min(qty, p.stock);
  persistCart();
}
function persistCart() {
  save('cart', state.cart);
  renderCart();
}

function renderCart() {
  const entries = Object.entries(state.cart)
    .map(([id, qty]) => ({ p: byId(Number(id)), qty }))
    .filter((e) => e.p);
  const count = entries.reduce((s, e) => s + e.qty, 0);
  const total = entries.reduce((s, e) => s + e.qty * e.p.price, 0);
  $('cart-count').hidden = count === 0;
  $('cart-count').textContent = count;

  if (!entries.length) {
    $('cart-items').replaceChildren(el('div', { class: 'empty' }, el('span', {}, '🛒'), el('strong', {}, 'Sepetin boş'), el('small', {}, 'Beğendiğin ürünleri sepete ekle.')));
    $('cart-foot').replaceChildren();
    return;
  }
  $('cart-items').replaceChildren(...entries.map(({ p, qty }) =>
    el('div', { class: 'line' },
      el('div', { class: `mini ${gradOf(p)}` }, emojiOf(p)),
      el('div', {},
        el('strong', {}, p.name),
        el('small', {}, tl(p.price)),
        el('div', { class: 'qty' },
          el('button', { 'aria-label': 'Azalt', onclick: () => setQty(p.id, qty - 1) }, '−'),
          el('span', {}, String(qty)),
          el('button', { 'aria-label': 'Artır', onclick: () => setQty(p.id, qty + 1) }, '+'),
        ),
      ),
      el('button', { class: 'remove', 'aria-label': `${p.name} ürününü kaldır`, onclick: () => setQty(p.id, 0) }, '🗑'),
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
  toast('🎉 Siparişin alındı! (demo)');
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
  'Öğrenciyim, 25 bin TL altı laptop öner',
  'Uygun fiyatlı bir kulaklık arıyorum',
  'Oyun için güçlü bir bilgisayar',
  'Hediye için akıllı saat',
];

function openChat() {
  $('chat').hidden = false;
  $('chat-fab').hidden = true;
  if (!$('chat-body').children.length) {
    botMessage('Merhaba! 👋 Bütçeni ve ne aradığını yaz, sana uygun ürünleri önereyim.');
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
    el('div', { class: `mini ${gradOf(p)}` }, emojiOf(p)),
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
    for (const id of data.productIds ?? []) {
      const p = byId(id);
      if (p) pushBody(recommendation(p));
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

/* ---------- Bağlantılar ---------- */
$('search').addEventListener('input', (e) => { state.query = e.target.value; renderGrid(); });
$('sort').addEventListener('change', (e) => { state.sort = e.target.value; renderGrid(); });
$('cart-btn').addEventListener('click', openDrawer);
$('drawer-close').addEventListener('click', closeDrawer);
$('scrim').addEventListener('click', closeDrawer);
$('modal-close').addEventListener('click', closeModal);
$('modal').addEventListener('click', (e) => { if (e.target === $('modal')) closeModal(); });
$('m-add').addEventListener('click', () => { if (modalProduct) addToCart(modalProduct.id); });
$('chat-fab').addEventListener('click', openChat);
$('hero-chat').addEventListener('click', openChat);
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
fetchProducts();
