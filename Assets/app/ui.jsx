// Shared UI helpers for the Studio prototype. Exported to window so every
// tab script can use them (Babel scripts don't share scope otherwise).
(function () {
  const TYPE_VAR = { feature: 'var(--c-feature)', enh: 'var(--c-enh)', bug: 'var(--c-bug)', clientbug: 'var(--c-clientbug)', regression: 'var(--c-regression)' };
  const PHASE_VAR = { uiux: 'var(--c-enh)', dev: 'var(--p-dev)', review: 'var(--p-review)', qawait: 'var(--p-qawait)', qa: 'var(--p-qa)', tofix: 'var(--p-tofix)', po: 'var(--p-po)' };
  // Noms de phase : défauts surchargés par les périodes configurées (Export.Periods) si présentes.
  const PHASE_NAME = function () {var m = { uiux: 'UI/UX', dev: 'Dev', review: 'Review', qawait: 'QA wait', qa: 'QA', tofix: 'To fix', po: 'PO' };try {(window.APP.periods || []).forEach(function (p) {if (p.key && p.name) m[p.key] = p.name;});} catch (e) {}return m;}();
  const typeColor = (k) => {
    try {const nm = window.APP.typeByKey[k] && window.APP.typeByKey[k].name;const c = gitlabColor(nm);if (c) return c;} catch (e) {}
    return TYPE_VAR[k] || 'var(--ink-faint)';
  };
  // Couleur RÉELLE d'un label GitLab par son nom (placeholder window.APP.labelColors — voir data.js).
  // ⚠️ CLAUDE CODE : à brancher sur le champ `color` de l'API GitLab Labels.
  const gitlabColor = (name) => {try {const c = window.APP && window.APP.labelColors && window.APP.labelColors[name];if (c) return c;} catch (e) {}return null;};
  const labelColor = (name) => gitlabColor(name) || 'var(--ink-faint)';
  const phaseColor = (k) => PHASE_VAR[k] || 'var(--ink-faint)';
  const fmt1 = (n) => (Math.round(n * 10) / 10).toString().replace(/\.0$/, '');
  const pctOf = (a, b) => b ? Math.round(a / b * 100) : 0;

  const Ic = ({ d }) => <svg className="ic" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">{d}</svg>;
  const ICONS = {
    dashboard: <Ic d={<g><rect x="3" y="3" width="7" height="9" /><rect x="14" y="3" width="7" height="5" /><rect x="14" y="12" width="7" height="9" /><rect x="3" y="16" width="7" height="5" /></g>} />,
    charts: <Ic d={<g><path d="M3 3v18h18" /><path d="M7 15l3-4 3 2 4-6" /></g>} />,
    anomalies: <Ic d={<g><path d="M12 9v4M12 17h.01" /><path d="M10.3 3.9L2 18a2 2 0 001.7 3h16.6a2 2 0 001.7-3L13.7 3.9a2 2 0 00-3.4 0z" /></g>} />,
    issues: <Ic d={<g><rect x="4" y="3" width="16" height="18" rx="2" /><path d="M8 8h6M8 12h8M8 16h5" /></g>} />,
    calendar: <Ic d={<g><rect x="3" y="4" width="18" height="17" rx="2" /><path d="M16 2v4M8 2v4M3 10h18" /></g>} />,
    velocity: <Ic d={<path d="M13 2L3 14h7l-1 8 10-12h-7l1-8z" />} />,
    comparison: <Ic d={<g><path d="M12 3v18" /><path d="M5 8l-3 6c2 1.3 4 1.3 6 0l-3-6z" /><path d="M19 6l-3 6c2 1.3 4 1.3 6 0l-3-6z" /><path d="M5 8l7-2 7-2" /><path d="M7 21h10" /></g>} />,
    options: <Ic d={<path d="M12 15a3 3 0 100-6 3 3 0 000 6z M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 11-2.83 2.83l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 11-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 11-2.83-2.83l.06-.06a1.65 1.65 0 00.33-1.82 1.65 1.65 0 00-1.51-1H3a2 2 0 110-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 112.83-2.83l.06.06a1.65 1.65 0 001.82.33H9a1.65 1.65 0 001-1.51V3a2 2 0 114 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 112.83 2.83l-.06.06a1.65 1.65 0 00-.33 1.82V9a1.65 1.65 0 001.51 1H21a2 2 0 110 4h-.09a1.65 1.65 0 00-1.51 1z" />} />,
    search: <Ic d={<g><circle cx="11" cy="11" r="7" /><path d="M21 21l-4-4" /></g>} />,
    refresh: <Ic d={<g><path d="M21 12a9 9 0 11-2.6-6.4" /><path d="M21 3v6h-6" /></g>} />,
    download: <Ic d={<g><path d="M12 3v12M7 10l5 5 5-5" /><path d="M4 21h16" /></g>} />,
    chevron: <Ic d={<path d="M9 6l6 6-6 6" />} />,
    git: <Ic d={<g><circle cx="6" cy="6" r="3" /><circle cx="6" cy="18" r="3" /><circle cx="18" cy="12" r="3" /><path d="M6 9v6M18 9a9 9 0 01-9 9" /></g>} />,
    info: <Ic d={<g><circle cx="12" cy="12" r="9" /><path d="M12 11v5M12 8h.01" /></g>} />,
    eraser: <Ic d={<g><path d="M7 21h13" /><path d="M5 13l6-6 7 7-5 5H8z" /><path d="M9 11l5 5" /></g>} />,
    expand: <Ic d={<g><path d="M15 3h6v6" /><path d="M10 14L21 3" /><path d="M21 14v5a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h5" /></g>} />,
    drag: <Ic d={<g><path d="M9 5l-3 3M9 19l-3-3M15 5l3 3M15 19l3-3M5 12h14" /></g>} />,
    issueDot: <Ic d={<g><rect x="4" y="3" width="16" height="18" rx="2" /><path d="M8 8h6M8 12h8M8 16h5" /></g>} />,
    weight: <Ic d={<g><circle cx="12" cy="5" r="2.6" /><path d="M7 8.5h10l1.7 10.8a2 2 0 01-2 2.2H7.3a2 2 0 01-2-2.2L7 8.5z" /></g>} />,
    approve: <Ic d={<g><circle cx="12" cy="12" r="9" /><path d="M8.3 12.4l2.4 2.4 4.8-5.2" /></g>} />,
    clock: <Ic d={<g><circle cx="12" cy="12" r="9" /><path d="M12 7.5V12l3 2" /></g>} />
  };

  function Donut({ pct, size = 98, stroke = 13, color = 'var(--c-done)' }) {
    const r = (size - stroke) / 2,c = 2 * Math.PI * r,on = c * pct / 100,cc = size / 2;
    return (
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        <circle cx={cc} cy={cc} r={r} fill="none" stroke="var(--panel-2)" strokeWidth={stroke} />
        <circle cx={cc} cy={cc} r={r} fill="none" stroke={color} strokeWidth={stroke} strokeLinecap="round"
        strokeDasharray={`${on} ${c - on}`} transform={`rotate(-90 ${cc} ${cc})`} />
        <text x={cc} y={cc + size * 0.06} textAnchor="middle" className="disp" fontSize={size * 0.23} fontWeight="700" fill="var(--ink)">{pct}%</text>
      </svg>);

  }
  // multi-segment donut
  function DonutMulti({ segments, size = 98, stroke = 13 }) {
    const r = (size - stroke) / 2,c = 2 * Math.PI * r,cc = size / 2;
    const tot = segments.reduce((s, x) => s + x.value, 0) || 1;
    let acc = 0;
    return (
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        <circle cx={cc} cy={cc} r={r} fill="none" stroke="var(--panel-2)" strokeWidth={stroke} />
        {segments.map((s, i) => {
          const len = c * s.value / tot,off = c * acc / tot;acc += s.value;
          return <circle key={i} cx={cc} cy={cc} r={r} fill="none" stroke={s.color} strokeWidth={stroke}
          strokeDasharray={`${len} ${c - len}`} strokeDashoffset={-off} transform={`rotate(-90 ${cc} ${cc})`} />;
        })}
        <text x={cc} y={cc + size * 0.06} textAnchor="middle" className="disp" fontSize={size * 0.2} fontWeight="700" fill="var(--ink)">{tot}</text>
      </svg>);

  }

  const Avatar = ({ pid, size = 24 }) => {
    const p = window.APP.peopleById[pid];
    if (!p) return null;
    const initials = p.name.split(' ').map((s) => s[0]).join('').slice(0, 2).toUpperCase();
    return <span className="avatar" style={{ width: size, height: size, fontSize: size * 0.42, background: `var(--av-${p.av})` }} title={p.name}>{initials}</span>;
  };

  const Spark = ({ data, color }) => {
    const m = Math.max(...data) || 1;
    return <div className="spark">{data.map((v, i) => <i key={i} style={{ height: v / m * 100 + '%', background: i === data.length - 1 ? color : 'var(--panel-2)' }}></i>)}</div>;
  };

  // progress bar for KPI cards that have a natural 0-100 value
  const Progress = ({ pct, color }) => <div className="kbar"><i style={{ width: Math.min(100, pct) + '%', background: color }}></i></div>;

  // line sparkline (trend) for values without a 0-100 scale (e.g. cycle time)
  const SparkLine = ({ data, color }) => {
    const w = 120,h = 30,max = Math.max(...data),min = Math.min(...data),rng = max - min || 1;
    const y = (v) => h - 3 - (v - min) / rng * (h - 8);
    const line = data.map((v, i) => `${(i / (data.length - 1) * w).toFixed(1)},${y(v).toFixed(1)}`).join(' ');
    const area = `0,${h} ${line} ${w},${h}`;
    return (
      <svg className="sparkline" width="100%" height={h} viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none">
        <polygon points={area} fill={color} opacity="0.12" />
        <polyline points={line} fill="none" stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" vectorEffect="non-scaling-stroke" />
        <circle cx="0" cy={y(data[0])} r="2.4" fill="none" stroke={color} strokeWidth="1.4" vectorEffect="non-scaling-stroke" />
        <circle cx={w} cy={y(data[data.length - 1])} r="3" fill={color} />
      </svg>);
  };

  // multi-select (or single) dropdown for the global filters
  function MultiSelect({ label, options, value, onChange, single }) {
    const [open, setOpen] = React.useState(false);
    const [q, setQ] = React.useState('');
    const ref = React.useRef(null);
    React.useEffect(() => {
      const h = (e) => {if (ref.current && !ref.current.contains(e.target)) setOpen(false);};
      document.addEventListener('mousedown', h);
      return () => document.removeEventListener('mousedown', h);
    }, []);
    React.useEffect(() => {if (!open) setQ('');}, [open]);
    const toggle = (o) => {
      if (single) {onChange([o]);setOpen(false);return;}
      onChange(value.includes(o) ? value.filter((x) => x !== o) : [...value, o]);
    };
    const summary = value.length === 0 ? window.t('common.all') : single ? value[0] : value.length === 1 ? value[0] : value.length + ' ' + window.t('common.selected');
    const ql = q.trim().toLowerCase();
    const shown = ql ? options.filter((o) => String(o).toLowerCase().includes(ql)) : options;
    const showSearch = options.length > 4;
    return (
      <div className="ms" ref={ref}>
        <button className={'pill' + (open ? ' on' : '')} onClick={() => setOpen((o) => !o)}>
          <span className="k">{label}</span><span className="v">{summary}</span><span className="cx">▾</span>
        </button>
        {open &&
        <div className="ms-pop">
          {showSearch &&
          <div className="ms-search">
            <span className="ms-search-ic">{ICONS.search}</span>
            <input type="text" autoFocus value={q} placeholder={window.t('common.search')}
            onChange={(e) => setQ(e.target.value)} onMouseDown={(e) => e.stopPropagation()} />
            {q && <button className="ms-search-x" onClick={() => setQ('')} aria-label={window.t('common.clear')}>✕</button>}
          </div>}
          {!single && <div className="ms-head">{value.length} {window.t('common.of')} {options.length}<button className="ms-clear" onClick={() => onChange([])}>{window.t('common.clear')}</button></div>}
          <div className="ms-list">
            {shown.length ? shown.map((o) =>
            <label key={o} className={'ms-opt' + (value.includes(o) ? ' on' : '')} onClick={() => toggle(o)}>
              <span className="ms-box">{value.includes(o) ? '✓' : ''}</span>{o}
            </label>
            ) : <div className="ms-empty">{window.t('common.none')}</div>}
          </div>
        </div>}
      </div>);
  }

  // sortable headers hook — click cycles desc -> asc -> none (clears the sort)
  function useSort(defaultKey, defaultDir = 'desc') {
    const [s, setS] = React.useState({ key: defaultKey, dir: defaultDir });
    const onSort = (key) => setS((p) => {
      if (p.key !== key) return { key, dir: 'desc' };
      if (p.dir === 'desc') return { key, dir: 'asc' };
      return { key: '', dir: 'desc' };
    });
    const sorter = (a, b, get) => {const va = get(a, s.key),vb = get(b, s.key);const r = typeof va === 'string' ? va.localeCompare(vb) : va - vb;return s.dir === 'desc' ? -r : r;};
    const arrow = (key) => s.key === key ? s.dir === 'desc' ? '▼' : '▲' : '';
    return { s, onSort, sorter, arrow };
  }

  // small info annotation — icon + tooltip (replaces inline « hint » prose)
  const InfoTip = ({ text, align }) =>
  <span className={'infotip' + (align === 'left' ? ' left' : '')} tabIndex={0} role="note" aria-label={text} data-tip={text}>
      {ICONS.info}
    </span>;

  // GitLab issue hyperlink — used wherever an issue IID appears.
  // Base d'URL RÉELLE dérivée du webUrl des issues (mapper → meta.issueBase : instance + groupe/projet
  // exacts, ex. …/obvious/ooda/-/issues/). L'ancienne constante n'est plus qu'un repli pour la démo
  // /ref (data.js ne pose pas meta.issueBase).
  const ISSUE_BASE = 'https://gitlab.obvious.tech/hypervisor/-/issues/';
  const issueUrl = (iid) => {
    try {const b = window.APP.meta.issueBase;if (b) return b + iid;} catch (e) {}
    return ISSUE_BASE + iid;
  };
  const IssueLink = ({ iid, className }) =>
  <a className={'iid-link' + (className ? ' ' + className : '')} href={issueUrl(iid)} target="_blank" rel="noopener noreferrer"
  title={'Ouvrir l’issue #' + iid + ' sur GitLab'} onClick={(e) => e.stopPropagation()}>#{iid}</a>;

  // progress → colour by value (colour-blind safe + glyph elsewhere)
  const pctColor = (pct, scheme) => {
    if (scheme === 'time') return pct >= 85 ? 'var(--c-bad)' : pct >= 60 ? 'var(--c-warn)' : 'var(--c-good)';
    return pct >= 75 ? 'var(--c-good)' : pct >= 50 ? 'var(--c-warn)' : 'var(--c-bad)';
  };

  // ---- Modal shell ----
  function Modal({ title, subtitle, onClose, children, wide, layout, headline }) {
    const lay = layout || 'modal';
    React.useEffect(() => {
      const h = (e) => {if (e.key === 'Escape') onClose();};
      document.addEventListener('keydown', h);
      document.body.style.overflow = 'hidden';
      return () => {document.removeEventListener('keydown', h);document.body.style.overflow = '';};
    }, []);
    return (
      <div className={'modal-back lay-' + lay} onClick={onClose}>
        <div className={'modal lay-' + lay + (wide && lay === 'modal' ? ' wide' : '')} onClick={(e) => e.stopPropagation()} role="dialog" aria-modal="true">
          <div className="modal-h">
            <div className="modal-h-txt"><h3>{title}</h3>{subtitle && <div className="modal-sub">{subtitle}</div>}</div>
            {headline != null && <span className="modal-headline">{headline}</span>}
            <button className="modal-x" onClick={onClose} aria-label={window.t('common.close')}>✕</button>
          </div>
          <div className="modal-b">{children}</div>
        </div>
      </div>);
  }

  // recap strip grouped by Type::* — metric 'weight' (poids) or 'issues' (count)
  function WeightRecap({ issues, metric }) {
    const A = window.APP;
    const useWeight = metric !== 'issues';
    const by = {};
    issues.forEach((d) => {by[d.type] = (by[d.type] || 0) + (useWeight ? d.weight : 1);});
    const rows = A.types.map((t) => ({ key: t.key, name: t.short, v: by[t.key] || 0 })).filter((r) => r.v > 0).sort((a, b) => b.v - a.v);
    const max = Math.max(...rows.map((r) => r.v), 1);
    const tot = rows.reduce((s, r) => s + r.v, 0);
    return (
      <div className="recap" style={{ '--name-col': 'calc(' + Math.min(30, Math.max(0, ...rows.map((r) => (r.name || '').length))) + 'ch + 18px)' }}>
        <div className="recap-h">{useWeight ? 'Poids par type' : 'Issues par type'}
          <span className="recap-tot">{useWeight ? `${tot} pts · ${issues.length} issues` : `${issues.length} issues`}</span>
        </div>
        {rows.map((r) =>
        <div key={r.key} className="recap-row">
            <span className="recap-nm"><span className="dot" style={{ background: window.typeColor(r.key) }}></span>{r.name}</span>
            <span className="recap-bar"><i style={{ width: r.v / max * 100 + '%', background: window.typeColor(r.key) }}></i></span>
            <span className="recap-v">{r.v}</span>
          </div>
        )}
      </div>);
  }

  // compact issue row for drill-down lists (Issues-tab look, static)
  function IssueRowMini({ d, meta }) {
    const A = window.APP;
    return (
      <div className="arow">
        <IssueLink iid={d.iid} />
        <span className="arow-ttl">{d.title}</span>
        {meta}
        <span className="chip"><span className="dot" style={{ background: window.typeColor(d.type) }}></span>{A.typeByKey[d.type].short}</span>
        <span className="wchip-l"><span className="lk">Poids</span>{d.weight}</span>
        <span className="av-stack">{d.assignees.map((a) => <Avatar key={a} pid={a} size={22} />)}</span>
        <span className={'st-badge ' + (d.state === 'closed' ? 'st-closed' : 'st-open')}><span className="sd"></span>{d.state === 'closed' ? 'Fermée' : 'Ouverte'}</span>
      </div>);
  }

  // colour a duration badge by magnitude (short = good, long = risk)
  const cycleTone = (days) => days >= 24 ? 'var(--c-bad)' : days >= 16 ? 'var(--c-warn)' : 'var(--c-good)';

  // Export the live Graphiques tab to a self-contained INTERACTIVE HTML file.
  // Plutôt qu'un instantané DOM figé, on recompose une mini-app React autonome : mêmes sources
  // inline que la page (react, react-dom, i18n, ui.jsx, tab-charts.jsx — repérées par data-asset),
  // données = le window.APP COURANT (filtres appliqués) sérialisé, JSX transpilé ICI via le Babel
  // déjà chargé (rien à télécharger, pas de Babel embarqué). Résultat : les cartes du récap ouvrent
  // leur popup et les sélecteurs Barres/Colonnes & Empilé/Par phase fonctionnent dans l'export.
  function buildChartsExportDoc(tweaks) {
    const appEl = document.querySelector('.app');
    const theme = appEl ? appEl.getAttribute('data-theme') : 'light';
    const appStyle = appEl ? appEl.getAttribute('style') || '' : '';
    let css = '';
    for (const sheet of document.styleSheets) {
      try {for (const rule of sheet.cssRules) css += rule.cssText + '\n';} catch (e) {/* cross-origin font sheet */}
    }
    const A = window.APP;
    // horodatage ISO aaaa-mm-jj hh:mm (format de date unifié de l'app)
    const stamp = (function () {const d = new Date();const p = (n) => ('0' + n).slice(-2);return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()) + ' ' + p(d.getHours()) + ':' + p(d.getMinutes());})();
    // ⚠ Ce fichier est lui-même INLINÉ dans un script de la page : son source ne doit JAMAIS
    // contenir la séquence de fermeture de script (le parseur HTML tronquerait la balise).
    // D'où les balises composées dynamiquement (SO/SC) + neutralisation dans les sources embarqués.
    const noClose = (s) => String(s).replace(/<\/script/gi, '<\\/script');
    const SO = '<scr' + 'ipt>',SC = '</scr' + 'ipt>';
    const tag = (code) => SO + noClose(code) + SC;
    // Échappement HTML des valeurs GitLab interpolées dans le MARKUP (titre de milestone…) :
    // React les échappe dans l'app vivante, l'export composé à la main doit le faire lui-même.
    const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    const src = (name) => {
      const el = document.querySelector('script[data-asset="' + name + '"]');
      return el ? el.textContent : '';
    };
    // Données : window.APP courant SANS les fonctions (cal) ni les gros blocs inutiles aux
    // graphiques (detail/vel/anomalies). Tout ce que TabCharts + ses modales lisent y est.
    const KEYS = ['types', 'typeByKey', 'phases', 'periods', 'people', 'peopleById', 'pivot', 'pivotByKey',
    'superGroups', 'weightMatrix', 'transversal', 'phaseAvg', 'weightBuckets', 'totals', 'kpis',
    'milestone', 'meta', 'FIB', 'labelColors', 'filterOptions'];
    const app = {};
    KEYS.forEach((k) => {if (A[k] !== undefined) app[k] = A[k];});
    // Défense XSS (titres/labels contrôlés côté GitLab) : mêmes remplacements que le serveur.
    const appJson = JSON.stringify(app).
    replace(/</g, '\\u003C').replace(/>/g, '\\u003E').replace(/&/g, '\\u0026').
    replace(/\u2028/g, '\\u2028').replace(/\u2029/g, '\\u2029');
    const t = tweaks || {};
    const tw = JSON.stringify({ recapStyle: t.recapStyle || 'cartes', poidsStyle: t.poidsStyle || 'barres', tempsStyle: t.tempsStyle || 'empile' });
    const lang = JSON.stringify(window.__LANG__ || 'fr');
    // Bootstrap : rend l'en-tête + TabCharts avec les tweaks courants (modales en layout modal).
    const boot = `
(function(){
  window.__drillLayout = 'modal';
  var TW = ${tw};
  ReactDOM.createRoot(document.getElementById('root')).render(<window.TabCharts tweaks={TW} />);
})();`;
    // Transpile ui.jsx + tab-charts.jsx + bootstrap MAINTENANT (Babel est chargé dans la page) :
    // l'export n'embarque que du JS prêt à exécuter.
    const js = Babel.transform(src('ui.jsx') + '\n' + src('tab-charts.jsx') + '\n' + boot, { presets: ['react'] }).code;
    // RTL (ar) : recopie l'attribut dir de la page vivante (posé côté serveur, Loc.IsRtl).
    const dirAttr = document.documentElement.getAttribute('dir') === 'rtl' ? ' dir="rtl"' : '';
    return `<!doctype html>
<html lang="${(window.__LANG__ || 'fr').slice(0, 2)}"${dirAttr} data-theme="${theme}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Graphiques — Release ${esc(A.milestone.name)}</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&family=IBM+Plex+Mono:wght@400;500;600&display=swap" rel="stylesheet">
<style>${css}</style>
<style>
  body{margin:0;}
  .export-wrap{padding:26px 30px;}
  .export-head{margin-bottom:18px;}
  .export-head h1{font-family:var(--disp-font,'Space Grotesk'),system-ui,sans-serif; margin:0 0 4px; font-size:22px;}
  .export-head .sub{font-size:12.5px; color:var(--ink-faint);}
</style>
</head>
<body>
<div class="app kpi-root" data-theme="${theme}" style="${esc(appStyle)}; display:block; min-height:0;">
  <div class="export-wrap">
    <div class="export-head">
      <h1 class="disp">Graphiques — Release ${esc(A.milestone.name)}</h1>
      <div class="sub">${esc(A.meta.project)} · ${A.totals.issues} issues · export du ${stamp}</div>
    </div>
    <div id="root"></div>
  </div>
</div>
${tag(src('react'))}
${tag(src('react-dom'))}
${tag('window.__LANG__ = ' + lang + ';')}
${tag(src('i18n'))}
${tag('window.APP = ' + appJson + ';')}
${tag(js)}
</body>
</html>`;
  }

  // Nom de fichier PARLANT : graphiques_<projet>_<milestone>[_<équipes>][_<utilisateurs>].html
  // (sel = sélection des filtres Équipe/Utilisateur, passée par le shell au clic Exporter).
  function chartsExportName(sel) {
    const A = window.APP;
    const slug = (s) => String(s || '').replace(/[^A-Za-z0-9._-]+/g, '-').replace(/^-+|-+$/g, '');
    const parts = ['graphiques', A.meta.project, A.milestone.name];
    if (sel && sel.teams && sel.teams.length) parts.push(sel.teams.join('+'));
    if (sel && sel.users && sel.users.length) parts.push(sel.users.join('+'));
    return parts.map(slug).filter(Boolean).join('_') + '.html';
  }

  function exportChartsHTML(tweaks, sel) {
    let doc;
    try {doc = buildChartsExportDoc(tweaks);} catch (e) {console.error('export charts:', e);return;}
    const blob = new Blob([doc], { type: 'text/html' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = chartsExportName(sel);
    document.body.appendChild(a);a.click();a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 2000);
  }

  // shared Gantt navigation — DIRECT MOUSE control:
  //   • drag (click-hold) to pan horizontally
  //   • wheel to scroll along the timeline
  //   • Ctrl / ⌘ + wheel to zoom (change the time range)
  //   • double-click to reset zoom
  // returns a ref for .gantt-scroll, a style for .gantt-grid, and a small zoom indicator.
  function useGanttNav(baseMin = 900) {
    const scrollRef = React.useRef(null);
    const zoomRef = React.useRef(1);
    // baseMin peut changer entre rendus (fenêtre temporelle pilotée par les filtres) : le handler
    // wheel (posé une seule fois) lit la valeur COURANTE via cette ref, pas celle de sa closure.
    const baseRef = React.useRef(baseMin);
    baseRef.current = baseMin;
    const [zoom, setZoom] = React.useState(1);
    const [dragging, setDragging] = React.useState(false);
    React.useEffect(() => {zoomRef.current = zoom;}, [zoom]);

    React.useEffect(() => {
      const el = scrollRef.current;
      if (!el) return;
      let down = false,startX = 0,startScroll = 0,moved = false;

      const onDown = (e) => {
        if (e.button !== 0) return;
        down = true;moved = false;
        startX = e.clientX;startScroll = el.scrollLeft;
        setDragging(true);
      };
      const onMove = (e) => {
        if (!down) return;
        const dx = e.clientX - startX;
        if (Math.abs(dx) > 3) moved = true;
        el.scrollLeft = startScroll - dx;
        e.preventDefault();
      };
      const onUp = () => {down = false;setDragging(false);};
      // suppress click (e.g. opening a drill) right after a real drag
      const onClick = (e) => {if (moved) {e.stopPropagation();e.preventDefault();moved = false;}};

      const onWheel = (e) => {
        if (e.ctrlKey || e.metaKey) {
          e.preventDefault();
          // Zoom MULTIPLICATIF, dézoom autorisé sous 100 % jusqu'à « tout visible »
          // (plancher dynamique : la grille tient exactement dans le conteneur).
          const fit = Math.min(1, (el.clientWidth || 900) / (baseRef.current || 900));
          const next = Math.min(3, Math.max(fit, Math.round(zoomRef.current * (e.deltaY < 0 ? 1.25 : 0.8) * 100) / 100));
          if (next === zoomRef.current) return;
          // keep the point under the cursor stable while zooming
          const rect = el.getBoundingClientRect();
          const pointerX = e.clientX - rect.left + el.scrollLeft;
          const ratio = next / zoomRef.current;
          setZoom(next);
          requestAnimationFrame(() => {el.scrollLeft = pointerX * ratio - (e.clientX - rect.left);});
        } else if (e.deltaY !== 0 && el.scrollWidth > el.clientWidth) {
          // turn vertical wheel into horizontal timeline scroll
          el.scrollLeft += e.deltaY;
          e.preventDefault();
        }
      };
      const onDbl = () => setZoom(1);

      el.addEventListener('mousedown', onDown);
      window.addEventListener('mousemove', onMove);
      window.addEventListener('mouseup', onUp);
      el.addEventListener('click', onClick, true);
      el.addEventListener('wheel', onWheel, { passive: false });
      el.addEventListener('dblclick', onDbl);
      return () => {
        el.removeEventListener('mousedown', onDown);
        window.removeEventListener('mousemove', onMove);
        window.removeEventListener('mouseup', onUp);
        el.removeEventListener('click', onClick, true);
        el.removeEventListener('wheel', onWheel);
        el.removeEventListener('dblclick', onDbl);
      };
    }, []);

    const Nav = () =>
    <div className="gantt-hint" title="Glissez pour défiler · molette pour parcourir · Ctrl/⌘ + molette pour zoomer/dézoomer · double-clic pour réinitialiser">
        {window.ICONS.drag}<span>glisser · molette · ⌘+molette = zoom</span>
        {zoom !== 1 && <span className="gnav-zoom">{Math.round(zoom * 100)}%</span>}
      </div>;
    // width plafonnée à 100 % en dézoom (la grille remplit le conteneur, les jours se resserrent).
    const gridStyle = { width: Math.max(100, zoom * 100) + '%', minWidth: Math.round(baseMin * zoom) + 'px' };
    return { scrollRef, zoom, dragging, Nav, gridStyle };
  }

  // carte « temps passé par type » du drill Cycle : temps MOYEN par type (même métrique cyc que
  // les badges du drill), barre proportionnelle au max, nombre d'issues par type.
  function CycleTypeRecap({ issues, cyc }) {
    const A = window.APP;
    const by = {};
    issues.forEach((d) => {const b = by[d.type] = by[d.type] || { sum: 0, n: 0 };b.sum += cyc(d);b.n++;});
    const rows = A.types.map((t) => {const b = by[t.key];return b ? { key: t.key, name: t.short, n: b.n, avg: b.sum / b.n } : null;}).
    filter(Boolean).sort((a, b) => b.avg - a.avg);
    if (!rows.length) return null;
    const max = Math.max(...rows.map((r) => r.avg), 0.1);
    return (
      <div className="recap" style={{ '--name-col': 'calc(' + Math.min(30, Math.max(0, ...rows.map((r) => (r.name || '').length))) + 'ch + 18px)' }}>
        <div className="recap-h">{window.t('drill.timeByType')}<span className="recap-tot">{issues.length} {window.t('issues')}</span></div>
        {rows.map((r) =>
        <div key={r.key} className="recap-row">
            <span className="recap-nm"><span className="dot" style={{ background: window.typeColor(r.key) }}></span>{r.name}</span>
            <span className="recap-bar"><i style={{ width: r.avg / max * 100 + '%', background: window.typeColor(r.key) }}></i></span>
            <span className="recap-v">{window.fmt1(r.avg)} {window.t('unit_day')} · {r.n}</span>
          </div>
        )}
      </div>);
  }

  // drill-down modal: issues list (+ optional recap). recap: false | 'weight' | 'issues'.
  // mode 'cycle' shows a lead-time badge per row + a duration summary instead of a type recap.
  // groups (optional): [{label, issues, recap}] renders labelled sections instead of a flat list.
  function IssueDrill({ title, subtitle, headline, issues, recap, mode, groups, onClose }) {
    const layout = typeof window !== 'undefined' && window.__drillLayout || 'modal';
    const cyc = (d) => d.end - d.start;
    const metric = recap === 'issues' ? 'issues' : 'weight';

    const Section = ({ items, recap: rc, metric: mc }) =>
    <React.Fragment>
        {rc && items.length > 0 && <WeightRecap issues={items} metric={mc || (rc === 'issues' ? 'issues' : 'weight')} />}
        <div className="drill-list">
          {items.length ? items.map((d) =>
        <IssueRowMini key={d.iid} d={d}
        meta={mode === 'cycle' ? <span className="cyc-badge" style={{ color: cycleTone(cyc(d)), borderColor: cycleTone(cyc(d)) }}>{Math.round(cyc(d) * 10) / 10} j</span> : null} />
        ) : <div className="empty">Aucune issue.</div>}
        </div>
      </React.Fragment>;

    let cycSummary = null;
    if (mode === 'cycle' && issues && issues.length) {
      const ds = issues.map(cyc).sort((a, b) => a - b);
      cycSummary = { min: ds[0], max: ds[ds.length - 1], med: ds[Math.floor(ds.length / 2)], avg: ds.reduce((s, x) => s + x, 0) / ds.length };
    }
    return (
      <Modal title={title} subtitle={subtitle} headline={headline} onClose={onClose} wide layout={layout}>
        {/* carte « temps passé par type » TOUT EN HAUT du drill cycle (demande explicite) */}
        {mode === 'cycle' && issues && issues.length > 0 && <CycleTypeRecap issues={issues} cyc={cyc} />}
        {mode === 'cycle' && cycSummary &&
        <div className="cycstat">
            <div className="cycstat-item"><span className="cv" style={{ color: cycleTone(cycSummary.avg) }}>{cycSummary.avg.toFixed(1)} j</span><span className="cl">moyenne</span></div>
            <div className="cycstat-item"><span className="cv">{cycSummary.med.toFixed(1)} j</span><span className="cl">médiane</span></div>
            <div className="cycstat-item"><span className="cv">{cycSummary.min.toFixed(1)} j</span><span className="cl">le plus court</span></div>
            <div className="cycstat-item"><span className="cv" style={{ color: cycleTone(cycSummary.max) }}>{cycSummary.max.toFixed(1)} j</span><span className="cl">le plus long</span></div>
          </div>}
        {groups ?
        groups.map((g, i) =>
        <div key={i} className="drill-group">
            <div className="drill-group-h"><span className="dgh-pip" style={{ background: g.color || 'var(--ink-faint)' }}></span>{g.label}<span className="dgh-n">{g.issues.length}</span></div>
            <Section items={g.issues} recap={g.recap} />
          </div>
        ) :
        <Section items={issues || []} recap={recap} metric={metric} />}
      </Modal>);
  }

  Object.assign(window, { TYPE_VAR, PHASE_VAR, PHASE_NAME, typeColor, phaseColor, gitlabColor, labelColor, fmt1, pctOf, ICONS, InfoTip, IssueLink, pctColor, Modal, WeightRecap, IssueRowMini, IssueDrill, Donut, DonutMulti, Avatar, Spark, Progress, SparkLine, MultiSelect, useSort, useGanttNav, exportChartsHTML, buildChartsExportDoc, chartsExportName });
})();
