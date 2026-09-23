// ─────────────────────────────────────────────────────────────────────────────
// picker.js — 광고 요소 선택기. prelude.js 와 같은 스코프에서 실행된다
// (CONFIG, postToHost, addHideRule, adoptHideSheet, lastContextTarget 를 함께 쓴다).
//
// 호스트가 window.__yeobaekPick(fromContextMenu) 로 진입시킨다. 최상위 프레임에서만 동작한다.
//   마우스 이동: 대상 강조 · 클릭/Enter: 숨기기 · ↑/↓: 범위 넓히기/좁히기 · Esc·우클릭: 취소
//
// 선택자는 안정성 순으로 만든다: 고유한 #id → [data-*] → 클래스 최대 2개 → :nth-of-type().
// 숫자 3자리 이상이나 난독화 해시(css-1a2b3c 등)가 섞인 이름은 버린다. 조상은 5단계까지만 본다.
// ─────────────────────────────────────────────────────────────────────────────

const PICKER_MAX_DEPTH = 5;
const PICKER_MAX_SELECTOR_LENGTH = 512;
const UNSTABLE_TOKEN = /\d{3,}|^(css|jss|jsx|sc|emotion|styled|svelte)-|__[a-z0-9]{5,}$|[a-z]\d[a-z]\d|\d[a-z]\d[a-z]/i;

function isStableToken(token) {
  return token.length > 0 && token.length <= 40 && !UNSTABLE_TOKEN.test(token);
}

function indexOfType(element) {
  let index = 1;
  for (let sibling = element.previousElementSibling; sibling; sibling = sibling.previousElementSibling) {
    if (sibling.localName === element.localName) index++;
  }
  return index;
}

/** 요소 하나를 가리키는 선택자 조각. unique 면 문서 전체에서 이 요소만 가리킨다. */
function selectorPart(element) {
  const tag = element.localName;

  if (element.id && isStableToken(element.id)) {
    const byId = '#' + CSS.escape(element.id);
    if (document.querySelectorAll(byId).length === 1) return { text: byId, unique: true };
  }

  for (const attribute of element.attributes) {
    const name = attribute.name;
    if (!name.startsWith('data-') || name.startsWith('data-v-') || !isStableToken(name)) continue;
    const text = isStableToken(attribute.value)
      ? `${tag}[${name}="${CSS.escape(attribute.value)}"]`
      : `${tag}[${name}]`;
    return { text, unique: false };
  }

  const classes = [...element.classList].filter(isStableToken).slice(0, 2);
  if (classes.length) return { text: tag + classes.map((c) => '.' + CSS.escape(c)).join(''), unique: false };

  return { text: `${tag}:nth-of-type(${indexOfType(element)})`, unique: false };
}

function selectsOnly(selector, target) {
  try {
    const found = document.querySelectorAll(selector);
    return found.length === 1 && found[0] === target;
  } catch (e) {
    return false;
  }
}

/** 대상 요소를 가리키는 선택자. 대상만 가리키게 되면 거기서 멈춘다. 만들 수 없으면 null. */
function buildSelector(target) {
  const parts = [];
  for (let element = target, depth = 0; element && depth < PICKER_MAX_DEPTH; element = element.parentElement, depth++) {
    if (element === document.body || element === document.documentElement) break;
    const part = selectorPart(element);
    parts.unshift(part.text);
    const selector = parts.join(' > ');
    if (part.unique || selectsOnly(selector, target)) return selector.length <= PICKER_MAX_SELECTOR_LENGTH ? selector : null;
  }
  const selector = parts.join(' > ');
  if (!selector || selector.length > PICKER_MAX_SELECTOR_LENGTH) return null;
  try { return target.matches(selector) ? selector : null; } catch (e) { return null; }
}

function makeElement(tag, className) {
  const element = document.createElement(tag);
  element.className = className;
  return element;
}

/**
 * 선택기 화면을 띄운다. 페이지 전체를 투명한 가림막으로 덮어 클릭이 광고(특히 iframe)로
 * 새지 않게 하고, 가림막 아래 요소는 elementsFromPoint 로 찾는다.
 * 페이지 CSS 가 섞이지 않도록 닫힌 그림자 DOM 안에 그린다.
 */
function openPicker(initialTarget, onClose) {
  const root = document.createElement('yeobaek-picker');
  root.style.setProperty('display', 'block', 'important');
  const shadow = root.attachShadow({ mode: 'closed' });
  const sheet = new CSSStyleSheet();
  sheet.replaceSync(`
    :host { all: initial; }
    .shield { position: fixed; inset: 0; z-index: 2147483646; cursor: crosshair; }
    .box { position: fixed; z-index: 2147483647; display: none; pointer-events: none; box-sizing: border-box;
           border: 2px solid #d9480f; background: rgba(217, 72, 15, .16); border-radius: 3px; }
    .label { position: fixed; z-index: 2147483647; left: 50%; bottom: 16px; transform: translateX(-50%);
             max-width: min(90vw, 760px); padding: 8px 14px; border-radius: 8px; pointer-events: none;
             background: rgba(24, 24, 24, .92); color: #fff; font: 13px/1.6 "Malgun Gothic", system-ui, sans-serif; }
    .selector { display: block; font-family: Consolas, monospace; color: #ffd8a8;
                overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  `);
  shadow.adoptedStyleSheets = [sheet];

  const shield = makeElement('div', 'shield');
  const box = makeElement('div', 'box');
  const label = makeElement('div', 'label');
  const selectorLine = makeElement('span', 'selector');
  label.append(selectorLine, document.createTextNode('클릭: 숨기기 · ↑/↓: 범위 조절 · Esc: 취소'));
  shadow.append(shield, box, label);
  document.documentElement.appendChild(root);

  let pointed = null;       // 포인터 바로 아래 요소
  let target = null;        // 지금 숨기려는 요소 (pointed 또는 그 조상)
  const narrower = [];      // ↑ 로 넓힐 때 지나온 요소들. ↓ 로 되돌아간다
  let lastPoint = null;

  function render() {
    if (!target || !target.isConnected) {
      box.style.display = 'none';
      selectorLine.textContent = '숨길 영역에 마우스를 올리세요';
      return;
    }
    const rect = target.getBoundingClientRect();
    Object.assign(box.style, {
      display: 'block', left: rect.left + 'px', top: rect.top + 'px', width: rect.width + 'px', height: rect.height + 'px',
    });
    selectorLine.textContent = buildSelector(target) || '(이 요소는 선택자를 만들 수 없습니다)';
  }

  function elementAt(x, y) {
    for (const candidate of document.elementsFromPoint(x, y)) {
      if (candidate !== root && candidate !== document.documentElement && candidate !== document.body) return candidate;
    }
    return null;
  }

  function pointAt(x, y) {
    lastPoint = { x, y };
    const element = elementAt(x, y);
    if (element === pointed) return;
    pointed = target = element;
    narrower.length = 0;
    render();
  }

  function widen() {
    const parent = target && target.parentElement;
    if (!parent || parent === document.body || parent === document.documentElement) return;
    narrower.push(target);
    target = parent;
    render();
  }

  function narrow() {
    if (!narrower.length) return;
    target = narrower.pop();
    render();
  }

  function confirm() {
    const chosen = target;
    close();
    if (!chosen) return;
    const selector = buildSelector(chosen);
    if (!selector || !addHideRule(selector)) return;
    // 이 페이지에서는 바로 숨기고, 저장은 호스트가 맡는다. 다음 방문부터는 문서 생성 시점에 적용된다.
    adoptHideSheet();
    postToHost('pick', null, selector);
  }

  function close() {
    window.removeEventListener('keydown', onKeyDown, true);
    window.removeEventListener('scroll', onScroll, true);
    root.remove();
    onClose();
  }

  const keyActions = { Escape: close, ArrowUp: widen, ArrowDown: narrow, Enter: confirm };
  function onKeyDown(e) {
    const action = keyActions[e.key];
    if (!action) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    action();
  }

  function onScroll() {
    if (lastPoint) {
      pointed = null;
      pointAt(lastPoint.x, lastPoint.y);
    } else {
      render();
    }
  }

  // 가림막 위 마우스 이벤트가 페이지의 문서 수준 핸들러로 퍼지지 않게 한다
  for (const type of ['pointerdown', 'pointerup', 'mousedown', 'mouseup', 'dblclick', 'auxclick']) {
    shield.addEventListener(type, (e) => { e.preventDefault(); e.stopPropagation(); });
  }
  shield.addEventListener('mousemove', (e) => pointAt(e.clientX, e.clientY));
  shield.addEventListener('click', (e) => {
    e.preventDefault();
    e.stopPropagation();
    pointAt(e.clientX, e.clientY);
    confirm();
  });
  shield.addEventListener('contextmenu', (e) => {
    e.preventDefault();
    e.stopPropagation();
    close();
  });
  window.addEventListener('keydown', onKeyDown, true);
  window.addEventListener('scroll', onScroll, true);

  if (initialTarget && initialTarget.isConnected && initialTarget !== document.body && initialTarget !== document.documentElement) {
    pointed = target = initialTarget;
  }
  render();
}

if (isTopFrame) {
  let pickerOpen = false;
  Object.defineProperty(window, '__yeobaekPick', {
    value: (fromContextMenu) => {
      if (pickerOpen || !document.documentElement) return;
      pickerOpen = true;
      openPicker(fromContextMenu ? lastContextTarget : null, () => { pickerOpen = false; });
    },
  });
}
