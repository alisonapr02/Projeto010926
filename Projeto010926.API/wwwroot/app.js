'use strict';
const $ = (id) => document.getElementById(id);
const form = $('search-form');
const uf = $('estado');
const cidades = $('cidades');
const cidadesPorEstado = {
  AC: ['Rio Branco', 'Cruzeiro do Sul', 'Sena Madureira'], AL: ['Maceió', 'Arapiraca', 'Rio Largo'], AP: ['Macapá', 'Santana', 'Laranjal do Jari'], AM: ['Manaus', 'Parintins', 'Itacoatiara'],
  BA: ['Salvador', 'Feira de Santana', 'Vitória da Conquista', 'Camaçari'], CE: ['Fortaleza', 'Juazeiro do Norte', 'Caucaia', 'Sobral'], DF: ['Brasília'], ES: ['Vitória', 'Vila Velha', 'Serra', 'Cariacica'],
  GO: ['Goiânia', 'Aparecida de Goiânia', 'Anápolis', 'Rio Verde'], MA: ['São Luís', 'Imperatriz', 'São José de Ribamar', 'Timon'], MT: ['Cuiabá', 'Várzea Grande', 'Rondonópolis', 'Sinop'],
  MS: ['Campo Grande', 'Dourados', 'Três Lagoas', 'Corumbá'], MG: ['Belo Horizonte', 'Uberlândia', 'Contagem', 'Juiz de Fora', 'Betim'], PA: ['Belém', 'Ananindeua', 'Santarém', 'Marabá'],
  PB: ['João Pessoa', 'Campina Grande', 'Santa Rita', 'Patos'], PR: ['Curitiba', 'Londrina', 'Maringá', 'Ponta Grossa', 'Cascavel'], PE: ['Recife', 'Jaboatão dos Guararapes', 'Olinda', 'Caruaru', 'Petrolina'],
  PI: ['Teresina', 'Parnaíba', 'Picos', 'Floriano'], RJ: ['Rio de Janeiro', 'São Gonçalo', 'Duque de Caxias', 'Niterói', 'Nova Iguaçu'], RN: ['Natal', 'Mossoró', 'Parnamirim', 'São Gonçalo do Amarante'],
  RS: ['Porto Alegre', 'Caxias do Sul', 'Canoas', 'Pelotas', 'Santa Maria'], RO: ['Porto Velho', 'Ji-Paraná', 'Ariquemes', 'Vilhena'], RR: ['Boa Vista', 'Rorainópolis', 'Caracaraí'], SC: ['Florianópolis', 'Joinville', 'Blumenau', 'São José', 'Chapecó'],
  SP: ['São Paulo', 'Guarulhos', 'Campinas', 'São Bernardo do Campo', 'Santo André', 'Osasco', 'Ribeirão Preto'], SE: ['Aracaju', 'Nossa Senhora do Socorro', 'Lagarto', 'Itabaiana'], TO: ['Palmas', 'Araguaína', 'Gurupi', 'Porto Nacional']
};
function atualizarCidades() {
  const estado = uf.value;
  cidades.replaceChildren(...(cidadesPorEstado[estado] || []).map(cidade => new Option(cidade)));
  $('cidade').placeholder = estado ? `Capital e cidades de ${estado}` : 'Todo o Brasil';
}
uf.add(new Option('Brasil', '', true, true));
'AC AL AP AM BA CE DF ES GO MA MT MS MG PA PB PR PE PI RJ RN RS RO RR SC SP SE TO'.split(' ').forEach(value => {
  const option = new Option(value, value); uf.add(option);
});
uf.addEventListener('change', atualizarCidades);
atualizarCidades();
let jobs = [], cursor = null, view = 'all', loading = false, lastWasMore = false;
let filters = { cidade: '', estado: '', termo: '' };
function node(tag, className, text) {
  const element = document.createElement(tag); element.className = className;
  if (text !== undefined) element.textContent = text; return element;
}
function safeLink(value) {
  try { const url = new URL(value); return ['https:', 'http:'].includes(url.protocol) ? url.href : null; }
  catch { return null; }
}
function externalId(job) { return job.id || [job.titulo, job.empresa, job.linkCandidatura].filter(Boolean).join('|'); }
async function saveJob(job, button) {
  await Conta.ready;
  if (!Conta.usuario) { location.assign('/login?voltar=/'); return; }
  button.disabled = true; button.textContent = 'Salvando…';
  const timeout = new Promise((_, reject) => setTimeout(() => reject(new Error('O salvamento demorou demais. Tente novamente.')), 10000));
  try {
    await Promise.race([Conta.api('/vagas', 'POST', { idExterno: externalId(job), titulo: job.titulo || 'Vaga sem título', empresa: job.empresa, localizacao: job.localizacao || [job.cidade, job.estado].filter(Boolean).join(', '), descricao: job.descricao, linkCandidatura: job.linkCandidatura }), timeout]);
    button.textContent = 'Vaga salva'; button.classList.add('saved');
  } catch (error) {
    if (error.status === 401) { location.assign('/login?voltar=/'); return; }
    button.disabled = false; button.textContent = error.status === 409 ? 'Vaga já salva' : 'Salvar vaga';
    if (error.status === 409) button.classList.add('saved');
  }
}
function details(job) {
  $('detail-title').textContent = job.titulo || 'Vaga sem título';
  $('detail-company').textContent = [job.empresa, job.localizacao || job.cidade].filter(Boolean).join(' · ');
  $('detail-description').textContent = job.descricao || 'Consulte a descrição completa no site do anúncio.';
  const link = safeLink(job.linkCandidatura); $('detail-apply').hidden = !link;
  if (link) $('detail-apply').href = link; else $('detail-apply').removeAttribute('href');
  const save = $('detail-save'); save.textContent = 'Salvar vaga'; save.disabled = false; save.classList.remove('saved'); save.onclick = () => saveJob(job, save);
  $('details').showModal();
}
function render() {
  const visible = jobs.filter(job => view !== 'remote' || job.remota === true);
  const fragment = document.createDocumentFragment();
  visible.forEach(job => {
    const card = node('article', 'job');
    const top = node('div', 'job-top');
    const company = job.empresa || 'Empresa não informada';
    const icon = node('span', 'company-icon', company.trim().slice(0, 2).toUpperCase()); icon.setAttribute('aria-hidden', 'true');
    top.append(icon, node('span', 'company', company));
    card.append(top, node('h3', '', job.titulo || 'Vaga sem título'), node('p', 'job-location', job.localizacao || [job.cidade, job.estado].filter(Boolean).join(', ') || 'Localização não informada'));
    const tags = node('div', 'tags');
    if (job.remota === true) tags.append(node('span', 'tag', 'Remoto'));
    if (job.siteOrigem) tags.append(node('span', 'tag', job.siteOrigem));
    card.append(tags);
    const bottom = node('div', 'job-bottom');
    const button = node('button', 'details-button', 'Ver oportunidade ↗'); button.type = 'button';
    button.setAttribute('aria-label', `Ver oportunidade: ${job.titulo || 'vaga'} na ${company}`);
    button.addEventListener('click', () => details(job));
    bottom.append(node('span', 'salary', job.salario || 'Salário não informado'), button);
    card.append(bottom); fragment.append(card);
  });
  $('jobs').replaceChildren(fragment);
  const local = filters.cidade && filters.estado ? `${filters.cidade}, ${filters.estado}` : 'Brasil';
  $('summary').textContent = `${visible.length} de ${jobs.length} vagas carregadas · Busca: ${local}${view === 'remote' ? ' · Remotas entre os resultados carregados' : ''}`;
  $('empty').hidden = loading || visible.length > 0 || !$('error').hidden;
  $('load-more').hidden = !cursor;
}
function setLoading(value) {
  loading = value; $('jobs').setAttribute('aria-busy', String(value));
  document.querySelectorAll('#search-form button, #search-form input, #search-form select, [data-term], #load-more, #retry').forEach(el => el.disabled = value);
  $('search-button').textContent = value ? 'Buscando…' : 'Buscar vagas ↗';
}
async function search(more = false) {
  if (loading) return;
  lastWasMore = more;
  if (!more) { filters = { termo: $('termo').value.trim(), cidade: $('cidade').value.trim(), estado: uf.value }; jobs = []; cursor = null; }
  $('error').hidden = true; $('empty').hidden = true; setLoading(true); render();
  $('status').textContent = 'Buscando oportunidades. Isso pode levar alguns segundos…';
  const params = new URLSearchParams(filters); if (more && cursor) params.set('cursor', cursor);
  const controller = new AbortController(); const timeout = setTimeout(() => controller.abort(), 10000);
  try {
    const response = await fetch(`/api/vagas/externas?${params}`, { signal: controller.signal });
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || (data.errors ? 'Confira o cargo, a cidade e o estado informados.' : 'A busca está indisponível. Tente novamente.'));
    if (!Array.isArray(data.itens)) throw new Error('Recebemos uma resposta inesperada. Tente novamente.');
    const seen = new Set(jobs.map(job => job.id || `${job.titulo}|${job.empresa}|${job.linkCandidatura}`));
    data.itens.forEach(job => { const key = job.id || `${job.titulo}|${job.empresa}|${job.linkCandidatura}`; if (!seen.has(key)) { seen.add(key); jobs.push(job); } });
    const previousCursor = cursor; cursor = typeof data.proximoCursor === 'string' && data.proximoCursor !== previousCursor ? data.proximoCursor : null;
    $('status').textContent = 'Busca concluída. Selecione uma oportunidade para ver os detalhes.';
  } catch (error) {
    $('error').hidden = false;
    $('error-message').textContent = error.name === 'AbortError' ? 'A busca demorou mais que o esperado. Tente novamente.' : (error instanceof TypeError ? 'Não foi possível conectar. Confira sua conexão e se a API está ligada.' : error.message);
    $('status').textContent = '';
  } finally { clearTimeout(timeout); setLoading(false); render(); }
}
form.addEventListener('submit', event => { event.preventDefault(); search(); });
$('load-more').addEventListener('click', () => search(true));
$('retry').addEventListener('click', () => search(lastWasMore));
document.querySelectorAll('[data-term]').forEach(button => button.addEventListener('click', () => { $('termo').value = button.dataset.term; form.requestSubmit(); }));
document.querySelectorAll('[data-view]').forEach(button => button.addEventListener('click', () => {
  view = button.dataset.view;
  document.querySelectorAll('[data-view]').forEach(item => { item.classList.toggle('active', item === button); item.setAttribute('aria-pressed', String(item === button)); }); render();
}));
$('close-details').addEventListener('click', () => $('details').close());
$('details').addEventListener('click', event => { if (event.target === $('details')) { const r = $('details').getBoundingClientRect(); if (event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom) $('details').close(); } });
render();
