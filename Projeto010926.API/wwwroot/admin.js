'use strict';
const byId = id => document.getElementById(id);
let selectedId = null;
let allUsers = [];
function status(text, error = false) { const box = byId('admin-status'); box.textContent = text; box.classList.toggle('account-error', error); }
function resetSaveButton() { const button = byId('user-form').querySelector('button[type="submit"]'); button.textContent = 'Salvar usuário'; button.classList.remove('admin-saved'); button.disabled = false; }
async function api(path, method = 'GET', body) {
  const headers = {};
  if (method !== 'GET') {
    const csrf = await fetch('/api/admin/csrf', { cache: 'no-store', credentials: 'same-origin' });
    if (!csrf.ok) throw new Error('Não foi possível iniciar o painel.');
    headers['X-CSRF-Token'] = (await csrf.json()).token; headers['Content-Type'] = 'application/json';
  }
  const response = await fetch('/api/admin' + path, { method, headers, cache: 'no-store', credentials: 'same-origin', body: body === undefined ? undefined : JSON.stringify(body) });
  const data = await response.json().catch(() => null);
  if (!response.ok) { const validation = data?.errors ? Object.values(data.errors).flat().join(' ') : null; const error = new Error(data?.detail || validation || 'Não foi possível concluir a operação.'); error.status = response.status; throw error; }
  return data;
}
function clearEditor() {
  selectedId = null; byId('editor-title').textContent = 'Novo usuário'; byId('user-form').reset(); byId('user-city').value = 'João Pessoa';
  byId('user-profile').value = 'usuario';
  updateIdentifierField();
  resetSaveButton();
  byId('user-password').required = true; byId('password-note').textContent = 'obrigatória, mínimo de 10 caracteres'; byId('user-details').hidden = true;
}
function updateIdentifierField() {
  const unrestricted = byId('user-profile').value !== 'usuario';
  byId('identifier-label').firstChild.textContent = unrestricted ? 'Usuário ou e-mail' : 'E-mail ou usuário';
  byId('identifier-note').textContent = unrestricted ? 'Este perfil aceita um usuário sem @.' : 'Usuário comum exige e-mail com @. Administrador e Programador aceitam usuário sem @.';
  byId('user-email').placeholder = unrestricted ? 'admin ou dev01' : 'voce@exemplo.com';
}
function renderUsers(users) {
  const body = byId('users-body'); body.replaceChildren();
  users.forEach(user => {
    const row = document.createElement('tr');
    [user.nome, user.email, user.perfil === 'administrador' ? 'Administrador' : user.perfil === 'programador' ? 'Programador' : 'Usuário comum', user.cidade, String(user.vagasSalvas)].forEach(value => { const cell = document.createElement('td'); cell.textContent = value; row.append(cell); });
    const actions = document.createElement('td'); actions.className = 'admin-actions';
    const edit = document.createElement('button'); edit.className = 'secondary'; edit.type = 'button'; edit.textContent = 'Ver / editar'; edit.addEventListener('click', () => selectUser(user.id));
    const remove = document.createElement('button'); remove.className = 'remove-save'; remove.type = 'button'; remove.textContent = 'Excluir'; remove.addEventListener('click', () => deleteUser(user)); actions.append(edit, remove); row.append(actions); body.append(row);
  });
}
async function loadUsers() { allUsers = await api('/usuarios'); filterUsers(); }
function filterUsers() {
  const profile = byId('profile-filter').value;
  renderUsers(profile === 'todos' ? allUsers : allUsers.filter(user => (user.perfil || 'usuario') === profile));
}
async function selectUser(id) {
  const user = await api('/usuarios/' + id); selectedId = user.id; byId('editor-title').textContent = 'Editar usuário';
  byId('user-name').value = user.nome; byId('user-email').value = user.email; byId('user-profile').value = user.perfil || 'usuario'; updateIdentifierField(); byId('user-city').value = user.cidade; byId('user-password').value = ''; byId('user-password').required = false; byId('password-note').textContent = 'deixe em branco para manter a atual';
  const list = byId('saved-users-jobs'); list.replaceChildren(); user.vagas.forEach(item => { const line = document.createElement('p'); line.textContent = `${item.dados.titulo} · ${item.dados.empresa || 'Empresa não informada'}`; list.append(line); });
  if (!user.vagas.length) { const empty = document.createElement('p'); empty.textContent = 'Nenhuma vaga salva.'; list.append(empty); }
  byId('user-details').hidden = false;
}
async function deleteUser(user) {
  let senhaPrincipal = null;
  if (user.perfil === 'administrador') {
    senhaPrincipal = window.prompt(`Para excluir o administrador ${user.nome}, informe a senha principal:`);
    if (senhaPrincipal === null) return;
  } else if (!window.confirm(`Excluir a conta de ${user.nome}? Essa ação não pode ser desfeita.`)) return;
  try { await api('/usuarios/' + user.id, 'DELETE', { senhaPrincipal }); status('Usuário excluído.'); if (selectedId === user.id) clearEditor(); await loadUsers(); }
  catch (error) { status(error.message, true); }
}
byId('admin-login-form').addEventListener('submit', async event => {
  event.preventDefault(); const button = event.target.querySelector('button'); button.disabled = true; status('Entrando…');
  try { await api('/login', 'POST', { usuario: byId('admin-user').value.trim(), senha: byId('admin-password').value }); byId('admin-login').hidden = true; byId('admin-panel').hidden = false; status(''); await loadUsers(); }
  catch (error) { status(error.message, true); button.disabled = false; }
});
byId('user-form').addEventListener('submit', async event => {
  event.preventDefault(); const button = event.target.querySelector('button'); button.disabled = true;
  const body = { nome: byId('user-name').value.trim(), email: byId('user-email').value.trim(), perfil: byId('user-profile').value, cidade: byId('user-city').value.trim(), senha: byId('user-password').value || null };
  try { await api(selectedId ? '/usuarios/' + selectedId : '/usuarios', selectedId ? 'PUT' : 'POST', body); button.textContent = 'Usuário salvo'; button.classList.add('admin-saved'); status('Usuário salvo com sucesso.'); await loadUsers(); }
  catch (error) { status(error.message, true); }
  finally { button.disabled = false; }
});
byId('new-user').addEventListener('click', clearEditor); byId('cancel-edit').addEventListener('click', clearEditor); byId('profile-filter').addEventListener('change', filterUsers);
byId('user-profile').addEventListener('change', updateIdentifierField); updateIdentifierField();
byId('admin-logout').addEventListener('click', async () => { try { await api('/sair', 'POST'); location.reload(); } catch (error) { status(error.message, true); } });
api('/usuarios').then(data => { byId('admin-login').hidden = true; byId('admin-panel').hidden = false; allUsers = data; filterUsers(); }).catch(() => {});
