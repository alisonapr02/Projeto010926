'use strict';
window.Conta = {
  usuario: null,
  async api(path, method = 'GET', body) {
    const headers = {};
    if (method !== 'GET') {
      const tokenResponse = await fetch('/api/conta/csrf', { cache: 'no-store' });
      if (!tokenResponse.ok) throw new Error('Não foi possível iniciar a sessão. Atualize a página.');
      headers['X-CSRF-Token'] = (await tokenResponse.json()).token;
      headers['Content-Type'] = 'application/json';
    }
    const response = await fetch('/api/conta' + path, { method, headers, cache: 'no-store', body: body === undefined ? undefined : JSON.stringify(body) });
    const data = await response.json().catch(() => null);
    if (!response.ok) {
      const message = data?.detail || (data?.errors ? Object.values(data.errors).flat().join(' ') : null) || (response.status === 401 ? 'Entre na sua conta para continuar.' : response.status === 429 ? 'Muitas tentativas. Aguarde cinco minutos e tente novamente.' : 'Não foi possível concluir. Tente novamente.');
      const error = new Error(message); error.status = response.status; throw error;
    }
    return data;
  },
  async atualizar() {
    try { this.usuario = await this.api(''); }
    catch (error) { if (error.status !== 401) throw error; this.usuario = null; }
    document.querySelectorAll('[data-guest]').forEach(el => el.hidden = !!this.usuario);
    document.querySelectorAll('[data-member]').forEach(el => el.hidden = !this.usuario);
    return this.usuario;
  }
};
Conta.ready = Conta.atualizar().catch(() => null);
document.addEventListener('click', async event => {
  const button = event.target.closest('[data-logout]'); if (!button) return;
  button.disabled = true;
  try { await Conta.api('/sair', 'POST'); location.assign('/login'); }
  catch (error) { button.disabled = false; const status = document.getElementById('account-status'); if (status) status.textContent = error.message; else alert(error.message); }
});
