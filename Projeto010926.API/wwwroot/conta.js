'use strict';
const byId = id => document.getElementById(id);
const statusBox = byId('account-status');
const mode = location.pathname;
let registering = mode === '/cadastro';
function message(text, error = false) { statusBox.textContent = text; statusBox.classList.toggle('account-error', error); }
function element(tag, cls, text) { const el = document.createElement(tag); el.className = cls; el.textContent = text; return el; }
function nextPage() { const next = new URLSearchParams(location.search).get('voltar'); return ['/', '/minhas-vagas', '/minha-conta'].includes(next) ? next : '/minha-conta'; }
function displayProfile(user) {
  byId('profile-name').textContent = user.nome; byId('profile-email').textContent = user.email;
  byId('avatar').textContent = user.nome.slice(0, 2).toUpperCase(); byId('saved-count').textContent = user.vagasSalvas;
  byId('profile-input-name').value = user.nome; byId('profile-input-email').value = user.email; byId('profile-city').value = user.cidade;
}
async function loadSaved() {
  const saved = await Conta.api('/vagas'); const fragment = document.createDocumentFragment();
  byId('saved-empty').hidden = saved.length > 0;
  saved.forEach(item => {
    const job = item.dados, card = element('article', 'job', '');
    card.append(element('p', 'company', job.empresa || 'Empresa não informada'), element('h2', 'saved-title', job.titulo), element('p', 'job-location', job.localizacao || 'Localização não informada'));
    const details = document.createElement('details'); details.className = 'saved-description'; details.append(element('summary', '', 'Ver descrição'), element('p', '', job.descricao || 'Consulte o site da vaga.')); card.append(details);
    const actions = element('div', 'saved-actions', '');
    try { const url = new URL(job.linkCandidatura); if (['http:', 'https:'].includes(url.protocol)) { const link = element('a', 'secondary', 'Candidatar-se ↗'); link.href = url.href; link.target = '_blank'; link.rel = 'noopener noreferrer'; actions.append(link); } } catch {}
    const remove = element('button', 'remove-save', 'Remover'); remove.type = 'button'; remove.setAttribute('aria-label', `Remover vaga salva: ${job.titulo}`);
    remove.addEventListener('click', async () => { remove.disabled = true; try { await Conta.api('/vagas/' + item.id, 'DELETE'); await loadSaved(); message('Vaga removida da sua lista.'); } catch (error) { message(error.message, true); remove.disabled = false; } });
    actions.append(remove); card.append(actions); fragment.append(card);
  }); byId('saved-jobs').replaceChildren(fragment);
}
async function init() {
  await Conta.ready;
  if (mode === '/login' || mode === '/cadastro') {
    if (Conta.usuario) { location.replace(nextPage()); return; }
    byId('auth-section').hidden = false; setAuthMode(registering);
  } else {
    if (!Conta.usuario) { location.replace('/login?voltar=' + encodeURIComponent(mode)); return; }
    if (mode === '/minhas-vagas') { document.title = 'Minhas vagas · VagaPerto'; byId('saved-section').hidden = false; await loadSaved(); }
    else { byId('profile-section').hidden = false; displayProfile(Conta.usuario); }
  }
  message('');
}
function setAuthMode(createAccount) {
  registering = createAccount; document.title = (registering ? 'Criar conta' : 'Entrar') + ' · VagaPerto';
  byId('name-field').hidden = !registering; byId('auth-name').required = registering;
  byId('email-label').firstChild.textContent = registering ? 'E-mail' : 'E-mail ou usuário administrativo';
  byId('auth-email').placeholder = registering ? 'voce@exemplo.com' : 'voce@exemplo.com ou admin';
  byId('password-hint').hidden = !registering;
  byId('auth-title').textContent = registering ? 'Crie sua conta' : 'Que bom ter você de volta.';
  byId('auth-intro').textContent = registering ? 'Comece a guardar oportunidades que combinam com você.' : 'Suas próximas oportunidades estão esperando.';
  byId('admin-access').hidden = registering;
  byId('auth-submit').textContent = registering ? 'Criar minha conta ↗' : 'Entrar ↗';
  byId('auth-password').autocomplete = registering ? 'new-password' : 'current-password'; byId('auth-password').minLength = registering ? 10 : 1;
  const switchButton = element('button', 'secondary', registering ? 'Entrar' : 'Criar conta'); switchButton.type = 'button'; switchButton.addEventListener('click', () => setAuthMode(!registering));
  byId('auth-switch').replaceChildren(document.createTextNode(registering ? 'Já tem uma conta?' : 'Ainda não tem conta?'), switchButton);
}
byId('toggle-password').addEventListener('click', () => { const input = byId('auth-password'); const show = input.type === 'password'; input.type = show ? 'text' : 'password'; byId('toggle-password').textContent = show ? 'Ocultar' : 'Mostrar'; byId('toggle-password').setAttribute('aria-pressed', String(show)); byId('toggle-password').setAttribute('aria-label', show ? 'Ocultar senha' : 'Mostrar senha'); });
byId('auth-form').addEventListener('submit', async event => {
  event.preventDefault();
  const button = byId('auth-submit'); button.disabled = true; message('Aguarde…');
  try {
    const identificador = byId('auth-email').value.trim();
    if (!registering && identificador.toLowerCase() === 'admin') {
      const csrf = await fetch('/api/admin/csrf', { cache: 'no-store' });
      const token = (await csrf.json()).token;
      const response = await fetch('/api/admin/login', { method: 'POST', headers: { 'X-CSRF-Token': token, 'Content-Type': 'application/json' }, body: JSON.stringify({ usuario: identificador, senha: byId('auth-password').value }) });
      const data = await response.json().catch(() => null); if (!response.ok) throw new Error(data?.detail || 'Usuário ou senha administrativa incorretos.');
      location.assign('/admin'); return;
    }
    await Conta.api(registering ? '/cadastro' : '/login', 'POST', { nome: byId('auth-name').value.trim(), email: identificador, senha: byId('auth-password').value }); location.assign(registering ? '/' : nextPage());
  }
  catch (error) { message(error.message, true); button.disabled = false; }
});
byId('profile-form').addEventListener('submit', async event => {
  event.preventDefault(); const button = event.target.querySelector('button'); button.disabled = true;
  try { const user = await Conta.api('', 'PUT', { nome: byId('profile-input-name').value.trim(), cidade: byId('profile-city').value.trim() }); displayProfile(user); message('Informações atualizadas.'); }
  catch (error) { message(error.message, true); } finally { button.disabled = false; }
});
init().catch(error => message(error.message || 'Não foi possível carregar a conta.', true));
