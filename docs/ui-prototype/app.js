const pages = [...document.querySelectorAll('.page')];
const navItems = [...document.querySelectorAll('.nav-item[data-page]')];
const breadcrumb = document.getElementById('breadcrumb');
const names = { home: '홈', care: 'PC 최적화', security: '보안', history: '기록 및 복구', settings: '설정' };
let careState = 'ready';
let scanTimer = null;

function goTo(pageName) {
  pages.forEach(page => page.classList.toggle('active', page.id === `page-${pageName}`));
  navItems.forEach(item => item.classList.toggle('active', item.dataset.page === pageName));
  breadcrumb.textContent = names[pageName] || pageName;
  document.querySelector('.main-area').scrollTo({ top: 0, behavior: 'smooth' });
  if (pageName === 'care') renderCare();
}

function setCareState(nextState) {
  careState = nextState;
  renderCare();
}

function renderCare() {
  const host = document.getElementById('care-content');
  const template = document.getElementById(`care-${careState}-template`);
  if (!host || !template) return;
  host.replaceChildren(template.content.cloneNode(true));
  const order = ['ready', 'scanning', 'results', 'complete'];
  const current = order.indexOf(careState);
  document.querySelectorAll('.rail-step').forEach((step, index) => {
    step.classList.toggle('active', index === current);
    step.classList.toggle('done', index < current);
    const marker = step.querySelector('span');
    marker.textContent = index < current ? '✓' : String(index + 1);
  });
  bindDynamicActions();
}

function bindDynamicActions() {
  document.querySelectorAll('[data-care-action]').forEach(button => {
    button.addEventListener('click', () => {
      const action = button.dataset.careAction;
      if (action === 'start') {
        setCareState('scanning');
        clearTimeout(scanTimer);
        scanTimer = setTimeout(() => setCareState('results'), 2400);
      }
      if (action === 'pause') button.textContent = button.textContent === '계속하기' ? '일시 정지' : '계속하기';
      if (action === 'cancel' || action === 'rescan') { clearTimeout(scanTimer); setCareState('ready'); }
      if (action === 'clean') setCareState('complete');
      if (action === 'restart') { setCareState('ready'); goTo('home'); }
      if (action === 'details') setCareState('results');
    });
  });
  document.querySelectorAll('.result-category .category-head').forEach(button => {
    button.addEventListener('click', event => {
      if (event.target.matches('input')) return;
      const category = button.closest('.result-category');
      category.classList.toggle('expanded');
      button.querySelector('.expand').textContent = category.classList.contains('expanded') ? '⌃' : '⌄';
    });
  });
  document.querySelectorAll('[data-go]').forEach(button => button.addEventListener('click', () => goTo(button.dataset.go)));
}

navItems.forEach(item => item.addEventListener('click', () => goTo(item.dataset.page)));
document.querySelectorAll('[data-go]').forEach(button => button.addEventListener('click', () => goTo(button.dataset.go)));
document.getElementById('home-scan').addEventListener('click', () => { careState = 'scanning'; goTo('care'); clearTimeout(scanTimer); scanTimer = setTimeout(() => setCareState('results'), 2400); });
document.getElementById('state-demo').addEventListener('click', () => setCareState(careState === 'results' ? 'ready' : 'results'));
renderCare();
