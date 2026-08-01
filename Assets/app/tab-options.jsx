// Options tab — apparence, régénération, et CONFIGURATION.
// Admin : carte de configuration ÉDITABLE (projets / phases / labels / équipes), persistée via POST /api/options.
//   Portée par projet : le dropdown de chaque section choisit le périmètre. « Tous les projets importés » = global ;
//   un sous-ensemble = override par projet (labelPhasesByProject / teamsByProject). Phases = toujours globales.
// Non-admin : reflet lecture seule de la configuration (window.__DATA__.setup).
(function () {
  const { useState, useEffect } = React;
  const A = window.APP || {};
  const ACCENTS = [['#2b7fff', 'Bleu'], ['#7A5AE0', 'Violet'], ['#0f9e8e', 'Teal'], ['#e0792e', 'Ambre'], ['#d6336c', 'Magenta']];
  const NUMFONTS = [['grotesk', 'Grotesk'], ['mono', 'Mono'], ['system', 'Système']];
  const DRILL_LAYOUTS = ['modal', 'panel', 'full']; // libellés via i18n (DRILL_T)
  const DRILL_T = { modal: 'opt.drillModal', panel: 'opt.drillPanel', full: 'opt.drillFull' };
  const PALETTE = ['#2188ff', '#0ea5e9', '#06b6d4', '#2dd4bf', '#0f9e8e', '#22c55e', '#84cc16', '#eab308', '#c79a06', '#e0792e', '#f97316', '#ef4444', '#d6336c', '#ec4899', '#d946ef', '#a855f7', '#8957e5', '#6366f1', '#64748b', '#94a3b8'];

  function guessPhase(label, keys) {
    const l = (label || '').toLowerCase();
    let g = 'none';
    if (l.indexOf('code') >= 0 && l.indexOf('progress') >= 0) g = 'dev';
    else if (l.indexOf('review') >= 0) g = 'review';
    else if (l.indexOf('backlog') >= 0) g = 'qawait';
    else if (l.indexOf('qa') >= 0 && l.indexOf('progress') >= 0) g = 'qa';
    else if (l.indexOf('to fix') >= 0) g = 'tofix';
    else if (l.indexOf('validation') >= 0 || /\bpo\b/.test(l)) g = 'po';
    else if (l.indexOf('ui/ux') >= 0) g = 'uiux';
    return keys.indexOf(g) >= 0 ? g : 'none';
  }
  const mapClone = (obj, fn) => { const o = {}; Object.keys(obj || {}).forEach((k) => { o[k] = fn(obj[k]); }); return o; };
  const sameSet = (a, b) => a.length === b.length && a.slice().sort().join(',') === b.slice().sort().join(',');
  const clonePhases = (arr) => (arr || []).map((p) => { const role = p.role || (p.timed === false ? 'nogc' : 'active'); return { key: p.key, name: p.name, color: p.color, role: role, timed: role !== 'nogc' }; });
  const cloneTeams = (arr) => (arr || []).map((t) => ({ name: t.name, members: (t.members || []).slice(), lead: (t.members || [])[0] || null }));
  const orderTeam = (t) => ({ name: t.name, members: t.lead ? [t.lead].concat((t.members || []).filter((m) => m !== t.lead)) : (t.members || []).slice() });

  // ===================== JOURS FÉRIÉS AUTOMATIQUES (langue → pays par défaut) =====================
  // Génère une ESTIMATION éditable des fériés d'un pays sur les années couvertes par les données, 100 %
  // côté client (aucun appel externe) : dates fixes + fêtes mobiles pascales (Meeus) + n-ième jour de
  // semaine + équinoxes (Japon). Les fêtes LUNAIRES (islamiques, chinoises) ne se calculent pas
  // simplement → laissées à la saisie manuelle (cf. note d'aide).
  const HL = (function () {
    const pad = (n) => ('0' + n).slice(-2);
    const isoD = (dt) => dt.getFullYear() + '-' + pad(dt.getMonth() + 1) + '-' + pad(dt.getDate());
    const mk = (y, m, d, off) => { const dt = new Date(y, m - 1, d); if (off) dt.setDate(dt.getDate() + off); return dt; };
    const fx = (y, m, d) => isoD(mk(y, m, d, 0));
    const easter = (y) => { // Pâques grégorienne (Meeus/Jones/Butcher)
      const a = y % 19, b = Math.floor(y / 100), c = y % 100, d = Math.floor(b / 4), e = b % 4,
        f = Math.floor((b + 8) / 25), g = Math.floor((b - f + 1) / 3), h = (19 * a + b - d - g + 15) % 30,
        i = Math.floor(c / 4), k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, mm = Math.floor((a + 11 * h + 22 * l) / 451);
      return { m: Math.floor((h + l - 7 * mm + 114) / 31), d: ((h + l - 7 * mm + 114) % 31) + 1 };
    };
    const ea = (y, off) => { const e = easter(y); return isoD(mk(y, e.m, e.d, off)); };
    const nth = (y, m, dow, n) => { const dt = new Date(y, m - 1, 1); dt.setDate(1 + ((dow - dt.getDay() + 7) % 7) + (n - 1) * 7); return isoD(dt); };
    const last = (y, m, dow) => { const dt = new Date(y, m, 0); dt.setDate(dt.getDate() - ((dt.getDay() - dow + 7) % 7)); return isoD(dt); };
    const vEq = (y) => fx(y, 3, Math.floor(20.8431 + 0.242194 * (y - 1980) - Math.floor((y - 1980) / 4))); // équinoxes JP (~1980-2099)
    const aEq = (y) => fx(y, 9, Math.floor(23.2488 + 0.242194 * (y - 1980) - Math.floor((y - 1980) / 4)));
    const RULES = {
      FR: (y) => [fx(y, 1, 1), ea(y, 1), fx(y, 5, 1), fx(y, 5, 8), ea(y, 39), ea(y, 50), fx(y, 7, 14), fx(y, 8, 15), fx(y, 11, 1), fx(y, 11, 11), fx(y, 12, 25)],
      US: (y) => [fx(y, 1, 1), nth(y, 1, 1, 3), nth(y, 2, 1, 3), last(y, 5, 1), fx(y, 6, 19), fx(y, 7, 4), nth(y, 9, 1, 1), nth(y, 10, 1, 2), fx(y, 11, 11), nth(y, 11, 4, 4), fx(y, 12, 25)],
      GB: (y) => [fx(y, 1, 1), ea(y, -2), ea(y, 1), nth(y, 5, 1, 1), last(y, 5, 1), last(y, 8, 1), fx(y, 12, 25), fx(y, 12, 26)],
      ES: (y) => [fx(y, 1, 1), fx(y, 1, 6), ea(y, -2), fx(y, 5, 1), fx(y, 8, 15), fx(y, 10, 12), fx(y, 11, 1), fx(y, 12, 6), fx(y, 12, 8), fx(y, 12, 25)],
      DE: (y) => [fx(y, 1, 1), ea(y, -2), ea(y, 1), fx(y, 5, 1), ea(y, 39), ea(y, 50), fx(y, 10, 3), fx(y, 12, 25), fx(y, 12, 26)],
      IT: (y) => [fx(y, 1, 1), fx(y, 1, 6), ea(y, 1), fx(y, 4, 25), fx(y, 5, 1), fx(y, 6, 2), fx(y, 8, 15), fx(y, 11, 1), fx(y, 12, 8), fx(y, 12, 25), fx(y, 12, 26)],
      PT: (y) => [fx(y, 1, 1), ea(y, -2), ea(y, 0), fx(y, 4, 25), fx(y, 5, 1), ea(y, 60), fx(y, 6, 10), fx(y, 8, 15), fx(y, 10, 5), fx(y, 11, 1), fx(y, 12, 1), fx(y, 12, 8), fx(y, 12, 25)],
      BR: (y) => [fx(y, 1, 1), ea(y, -2), fx(y, 4, 21), fx(y, 5, 1), ea(y, 60), fx(y, 9, 7), fx(y, 10, 12), fx(y, 11, 2), fx(y, 11, 15), fx(y, 12, 25)],
      RU: (y) => [fx(y, 1, 1), fx(y, 1, 2), fx(y, 1, 3), fx(y, 1, 4), fx(y, 1, 5), fx(y, 1, 6), fx(y, 1, 7), fx(y, 1, 8), fx(y, 2, 23), fx(y, 3, 8), fx(y, 5, 1), fx(y, 5, 9), fx(y, 6, 12), fx(y, 11, 4)],
      JP: (y) => [fx(y, 1, 1), nth(y, 1, 1, 2), fx(y, 2, 11), fx(y, 2, 23), vEq(y), fx(y, 4, 29), fx(y, 5, 3), fx(y, 5, 4), fx(y, 5, 5), nth(y, 7, 1, 3), fx(y, 8, 11), nth(y, 9, 1, 3), aEq(y), nth(y, 10, 1, 2), fx(y, 11, 3), fx(y, 11, 23)],
      CN: (y) => [fx(y, 1, 1), fx(y, 5, 1), fx(y, 10, 1), fx(y, 10, 2), fx(y, 10, 3)], // fériés fixes seulement (lunaires manuels)
      SA: (y) => [fx(y, 2, 22), fx(y, 9, 23)] // fériés civils fixes (Aïd = lunaire, manuel)
    };
    const LANG = { fr: 'FR', en: 'US', es: 'ES', de: 'DE', it: 'IT', pt: 'PT', ru: 'RU', ar: 'SA', zh: 'CN', ja: 'JP' };
    const NAMES = [['FR', 'France'], ['US', 'United States'], ['GB', 'United Kingdom'], ['ES', 'Spain'], ['DE', 'Germany'], ['IT', 'Italy'], ['PT', 'Portugal'], ['BR', 'Brazil'], ['RU', 'Russia'], ['JP', 'Japan'], ['CN', 'China'], ['SA', 'Saudi Arabia']];
    // pays sans fêtes lunaires calculables → l'estimation est partielle (note renforcée).
    const LUNAR = { CN: 1, SA: 1 };
    return {
      countries: NAMES,
      defaultFor: (lang) => LANG[lang] || 'FR',
      partial: (country) => !!LUNAR[country],
      generate: (country, years) => {
        const fn = RULES[country] || RULES.FR, set = {};
        years.forEach((y) => fn(y).forEach((iso) => { set[iso] = 1; }));
        return Object.keys(set).sort();
      }
    };
  })();

  // ===================== A3 — ÉDITEUR DE PHASES GROUPÉ (drag entre Travail actif / Attentes / Hors chrono) =====================
  // phases : [{ key, name, color, role }] — role ∈ 'active' | 'wait' | 'nogc'. Glisser une phase écrit son role.
  function PhasesGroupEditor({ phases, setPhases }) {
    const [openColor, setOpenColor] = useState(null);
    const [dragKey, setDragKey] = useState(null);
    const [over, setOver] = useState(null);

    // Rétro-compat : si `role` absent (vieille config non migrée), le déduire de `timed`.
    const roleOf = (p) => p.role || (p.timed === false ? 'nogc' : 'active');

    const GROUPS = [
      ['active', 'opt.groupActive', 'opt.groupActiveSub', '#27e07a'],
      ['wait',   'opt.groupWait',   'opt.groupWaitSub',   '#e0a93b'],
      ['nogc',   'opt.groupNogc',   'opt.groupNogcSub',   '#5f6b7a'],
    ];

    const move    = (key, role) => setPhases((ps) => ps.map((p) => p.key === key ? Object.assign({}, p, { role, timed: role !== 'nogc' }) : p));
    const rename  = (key, name) => setPhases((ps) => ps.map((p) => p.key === key ? Object.assign({}, p, { name }) : p));
    const recolor = (key, color) => { setPhases((ps) => ps.map((p) => p.key === key ? Object.assign({}, p, { color }) : p)); setOpenColor(null); };
    const remove  = (key) => setPhases((ps) => ps.filter((p) => p.key !== key));
    const add     = () => setPhases((ps) => ps.concat([{ key: 'ph' + Date.now().toString(36), name: window.t('opt.newPhase'), color: PALETTE[ps.length % PALETTE.length], role: 'active', timed: true }]));

    const activeN = phases.filter((p) => roleOf(p) === 'active').length;

    const Row = (p) => (
      <div className="opt-phrow" key={p.key} draggable
        onDragStart={() => setDragKey(p.key)} onDragEnd={() => { setDragKey(null); setOver(null); }}
        style={dragKey === p.key ? { opacity: .4 } : undefined}>
        <span className="opt-grip" aria-hidden="true">⠿</span>
        <div className="opt-swatchwrap">
          <button className="opt-swatch" style={{ background: p.color }} onClick={() => setOpenColor(openColor === p.key ? null : p.key)} title={window.t('opt.accent')}></button>
          {openColor === p.key &&
          <div className="opt-pop">
            {PALETTE.map((c) => <button key={c} className={'opt-pc' + (c === p.color ? ' on' : '')} style={{ background: c }} onClick={() => recolor(p.key, c)}></button>)}
          </div>}
        </div>
        <input className="opt-phname" value={p.name} onChange={(e) => rename(p.key, e.target.value)} />
        <button className="opt-phx" onClick={() => remove(p.key)} title="×">×</button>
      </div>
    );

    return (
      <div className="opt-row" style={{ flexDirection: 'column', alignItems: 'stretch', gap: 12 }}>
        <div className="lbl">{window.t('opt.prodPhases')}<span>{window.t('opt.phasesGroupNote')}</span></div>

        {GROUPS.map(([id, tKey, subKey, dot]) => {
          const list = phases.filter((p) => roleOf(p) === id);
          return (
            <div className="opt-phgroup" key={id}>
              <div className="opt-phghead">
                <span className="opt-phgdot" style={{ background: dot }}></span>
                <span className="opt-phgt">{window.t(tKey)}</span>
                <span className="opt-phgc">{list.length} · {window.t(subKey)}</span>
              </div>
              <div className={'opt-phdrop' + (over === id ? ' over' : '') + (list.length ? '' : ' empty')}
                data-empty={window.t('opt.dropPhaseHere')}
                onDragOver={(e) => { e.preventDefault(); if (over !== id) setOver(id); }}
                onDragLeave={(e) => { if (e.currentTarget === e.target) setOver(null); }}
                onDrop={() => { if (dragKey) move(dragKey, id); setOver(null); setDragKey(null); }}>
                {list.map(Row)}
              </div>
            </div>
          );
        })}

        <button className="btn btn-sm" style={{ alignSelf: 'flex-start' }} onClick={add}>+ {window.t('opt.addPhase')}</button>

        <div className="opt-eff">
          <span className="opt-eff-n">{activeN}</span>
          <span className="opt-eff-t">{window.t('opt.effSummary')}</span>
        </div>
      </div>
    );
  }

  // ===================== B3 — JOURS FÉRIÉS EN APERÇU CALENDRIER (clic pour basculer) =====================
  // value : tableau d'ISO (aaaa-mm-jj). Le pays + « Générer » vivent ici (helper HL partagé).
  function HolidaysCalendar({ value, onChange, lang }) {
    const L = lang || window.__LANG__ || 'fr';
    const [country, setCountry] = useState(() => HL.defaultFor(L));
    const set = new Set(value || []);
    const yearsInData = (value || []).map((d) => +String(d).slice(0, 4)).filter(Boolean);
    const [year, setYear] = useState(() => yearsInData.length ? Math.min.apply(null, yearsInData) : new Date().getFullYear());

    const pad = (n) => ('0' + n).slice(-2);
    const iso = (y, m, d) => y + '-' + pad(m + 1) + '-' + pad(d);
    const toggle = (id) => { const n = new Set(set); n.has(id) ? n.delete(id) : n.add(id); onChange(Array.from(n).sort()); };

    // « Générer » : mêmes années couvertes par l'activité que l'ancien genHolidays.
    const generate = () => {
      let min = Infinity, max = -Infinity;
      ((window.__DATA__ || {}).issues || []).forEach((is) => {
        (is.labelEvents || []).forEach((e) => { const y = new Date(e.at).getFullYear(); if (y) { if (y < min) min = y; if (y > max) max = y; } });
        [is.closedAt, is.createdAt].forEach((s) => { if (s) { const y = new Date(s).getFullYear(); if (y) { if (y < min) min = y; if (y > max) max = y; } } });
      });
      const now = new Date().getFullYear();
      if (!isFinite(min)) { min = now - 2; max = now + 1; }
      if (max < now) max = now;
      if (max - min > 15) min = max - 15;
      const years = []; for (let y = min; y <= max; y++) years.push(y);
      onChange(HL.generate(country, years));
      setYear(min);
    };

    const monthName = (m) => new Date(year, m, 1).toLocaleDateString(L, { month: 'long' });
    const dowLetters = []; // 2024-01-01 est un lundi → semaine lundi-first, localisée
    for (let i = 0; i < 7; i++) dowLetters.push(new Date(2024, 0, 1 + i).toLocaleDateString(L, { weekday: 'narrow' }));

    const Month = (m) => {
      const startDow = (new Date(year, m, 1).getDay() + 6) % 7; // lundi = 0
      const days = new Date(year, m + 1, 0).getDate();
      const cells = [];
      for (let i = 0; i < startDow; i++) cells.push(<span className="opt-cald mut" key={'p' + i}></span>);
      for (let d = 1; d <= days; d++) {
        const id = iso(year, m, d);
        const dow = new Date(year, m, d).getDay();
        const we = dow === 0 || dow === 6;
        cells.push(<span key={d} className={'opt-cald day' + (set.has(id) ? ' hol' : (we ? ' we' : ''))} onClick={() => toggle(id)} title={id}>{d}</span>);
      }
      return (
        <div className="opt-calm" key={m}>
          <div className="opt-calmh">{monthName(m)}</div>
          <div className="opt-caldays">
            {dowLetters.map((x, i) => <span key={'h' + i} className="opt-cald hdr">{x}</span>)}
            {cells}
          </div>
        </div>
      );
    };

    const countYear = (value || []).filter((d) => String(d).slice(0, 4) === String(year)).length;
    const calSel = { border: '1px solid var(--line)', borderRadius: 9, background: 'var(--panel-2)', color: 'var(--ink)', padding: '9px 12px', font: '13px system-ui' };

    return (
      <div className="opt-row" style={{ flexDirection: 'column', alignItems: 'stretch', gap: 12 }}>
        <div className="lbl">{window.t('opt.holidays')}<span>{window.t('opt.holidaysSub')}</span></div>

        <div className="opt-holbar">
          <select aria-label={window.t('opt.holidaysGen')} style={calSel} value={country} onChange={(e) => setCountry(e.target.value)}>
            {HL.countries.map(([c, n]) => <option key={c} value={c}>{n}</option>)}
          </select>
          <button className="btn btn-sm" type="button" onClick={generate}>{window.ICONS.refresh} {window.t('opt.holidaysGen')}</button>
          <span className="opt-yrnav">
            <button type="button" onClick={() => setYear((y) => y - 1)} title="−1">‹</button>
            <span className="y">{year}</span>
            <button type="button" onClick={() => setYear((y) => y + 1)} title="+1">›</button>
          </span>
          <span className="opt-holcount">{countYear} · {year}</span>
        </div>

        <div className="opt-calgrid">{[0,1,2,3,4,5,6,7,8,9,10,11].map((m) => Month(m))}</div>

        <div className="opt-callegend"><span className="sw"></span> {window.t('opt.holidaysLegend')}</div>
        <span className="muted" style={{ fontSize: 11, lineHeight: 1.5 }}>{window.t(HL.partial(country) ? 'opt.holidaysHintLunar' : 'opt.holidaysHint')}</span>
      </div>
    );
  }

  function Toggle({ on, onClick }) {
    return <button onClick={onClick} style={{ width: 42, height: 24, borderRadius: 999, border: 0, cursor: 'pointer', background: on ? 'var(--accent)' : 'var(--panel-3)', position: 'relative', transition: 'background .15s' }}>
      <span style={{ position: 'absolute', top: 3, left: on ? 21 : 3, width: 18, height: 18, borderRadius: '50%', background: '#fff', transition: 'left .15s' }}></span>
    </button>;
  }

  const selStyle = { border: '1px solid var(--line)', borderRadius: 9, background: 'var(--panel-2)', color: 'var(--ink)', padding: '9px 12px', font: '14px system-ui' };
  const subGrey = { fontWeight: 400, textTransform: 'none', letterSpacing: 0, color: 'var(--ink-faint)' };

  // ===================== ÉDITEUR D'ÉQUIPES (contrôlé ; readOnly = affichage seul) =====================
  function TeamsEditor({ teams, setTeams, readOnly }) {
    const peopleById = A.peopleById || {};
    const allPeople = A.people || Object.keys(peopleById).map((id) => peopleById[id]);
    const personName = (id) => (peopleById[id] || {}).name || id;
    const [dragOver, setDragOver] = useState(null);
    const dragRef = React.useRef(null); // { from, id }

    if (readOnly) {
      if (!teams.length) return <p className="opt-note">{window.t('opt.noTeams')}</p>;
      return (
        <div className="opt-teams">
          {teams.map((tm, i) => {
            const members = tm.members || [];
            const lead = tm.lead || members[0] || null;
            return (
              <div key={i} className="opt-team">
                <div className="opt-team-hd"><span style={{ fontWeight: 700, fontSize: 13 }}>{tm.name}</span></div>
                <div className="opt-team-members">
                  {members.map((id) =>
                  <div key={id} className={'opt-member' + (lead === id ? ' lead' : '')} style={{ cursor: 'default' }}>
                    <window.Avatar pid={id} size={20} />
                    <span className="opt-member-nm">{personName(id)}</span>
                    {lead === id && <span className="opt-lead-tag">{window.t('opt.lead')}</span>}
                  </div>)}
                </div>
              </div>);
          })}
        </div>);
    }

    const addGroup = () => setTeams((ts) => ts.concat([{ name: window.t('opt.newTeam'), members: [], lead: null }]));
    const removeGroup = (i) => setTeams((ts) => ts.filter((_, j) => j !== i));
    const renameGroup = (i, name) => setTeams((ts) => ts.map((t, j) => j === i ? Object.assign({}, t, { name }) : t));
    const addMember = (i, id) => { if (!id) return; setTeams((ts) => ts.map((t, j) => j === i && t.members.indexOf(id) < 0 ? Object.assign({}, t, { members: t.members.concat([id]), lead: t.lead || id }) : t)); };
    const removeMember = (i, id) => setTeams((ts) => ts.map((t, j) => { if (j !== i) return t; const members = t.members.filter((m) => m !== id); return Object.assign({}, t, { members: members, lead: t.lead === id ? members[0] || null : t.lead }); }));
    const setLead = (i, id) => setTeams((ts) => ts.map((t, j) => j === i ? Object.assign({}, t, { lead: id }) : t));
    const moveMember = (from, id, to) => {
      if (to == null || to === from) return;
      setTeams((ts) => {
        const arr = ts.map((t) => Object.assign({}, t, { members: t.members.slice() }));
        const f = arr[from]; if (!f) return ts; f.members = f.members.filter((m) => m !== id); if (f.lead === id) f.lead = f.members[0] || null;
        const tg = arr[to]; if (tg && tg.members.indexOf(id) < 0) { tg.members.push(id); tg.lead = tg.lead || id; }
        return arr;
      });
    };
    const onDrop = (to) => { const d = dragRef.current; setDragOver(null); if (d) moveMember(d.from, d.id, to); dragRef.current = null; };
    return (
      <div className="opt-teams">
        {teams.map((tm, i) => {
          const avail = allPeople.filter((p) => tm.members.indexOf(p.id) < 0);
          return (
            <div key={i} className={'opt-team' + (dragOver === i ? ' dragover' : '')}
              onDragOver={(e) => { e.preventDefault(); if (dragOver !== i) setDragOver(i); }}
              onDragLeave={(e) => { if (e.currentTarget === e.target) setDragOver(null); }}
              onDrop={() => onDrop(i)}>
              <div className="opt-team-hd">
                <input className="opt-phname" value={tm.name} onChange={(e) => renameGroup(i, e.target.value)} />
                <button className="opt-phx" title="×" onClick={() => removeGroup(i)}>×</button>
              </div>
              <div className="opt-team-members">
                {tm.members.length ? tm.members.map((id) =>
                <div key={id} className={'opt-member' + (tm.lead === id ? ' lead' : '')} draggable
                  onDragStart={(e) => { dragRef.current = { from: i, id: id }; e.dataTransfer.effectAllowed = 'move'; }}
                  onDragEnd={() => { dragRef.current = null; setDragOver(null); }}>
                  <span className="opt-grip" aria-hidden="true">⠿</span>
                  <window.Avatar pid={id} size={20} />
                  <span className="opt-member-nm">{personName(id)}</span>
                  {tm.lead === id && <span className="opt-lead-tag">{window.t('opt.lead')}</span>}
                  <button className={'opt-star' + (tm.lead === id ? ' on' : '')} title={window.t('opt.setLeadHint')} onClick={() => setLead(i, id)}>★</button>
                  <button className="opt-phx" title="×" onClick={() => removeMember(i, id)}>×</button>
                </div>
                ) : <p className="opt-note opt-dropempty" style={{ margin: '2px 0' }}>{window.t('opt.dropHere')}</p>}
              </div>
              {avail.length > 0 &&
              <select className="opt-mini opt-addmem" value="" onChange={(e) => { addMember(i, e.target.value); e.target.value = ''; }}>
                <option value="">+ {window.t('opt.addMember')}</option>
                {avail.map((p) => <option key={p.id} value={p.id}>{personName(p.id)}</option>)}
              </select>}
            </div>);
        })}
        <button className="btn btn-sm" style={{ alignSelf: 'flex-start' }} onClick={addGroup}>{window.t('opt.addTeam')}</button>
      </div>);
  }

  // ===================== ÉDITEUR ADMIN (carte riche, persistée, portée par projet) =====================
  function AdminConfigEditor({ S }) {
    const initImport = (S.projects || []).map((p) => p.id);
    const [allProjects, setAllProjects] = useState(null); // null = chargement ; [] = vide/erreur
    const [importIds, setImportIds] = useState(initImport);
    const [phases, setPhases] = useState(() => clonePhases(S.periods));            // phases GLOBALES
    const [lpGlobal, setLpGlobal] = useState(() => Object.assign({}, S.labelPhases || {}));
    const [lpByProj, setLpByProj] = useState(() => mapClone(S.labelPhasesByProject || {}, (m) => Object.assign({}, m)));
    const [teamsGlobal, setTeamsGlobal] = useState(() => cloneTeams(S.teams));
    const [teamsByProj, setTeamsByProj] = useState(() => mapClone(S.teamsByProject || {}, (arr) => cloneTeams(arr)));
    const [assocProjects, setAssocProjects] = useState((S.projects || []).map((p) => p.name));
    const [teamProjects, setTeamProjects] = useState((S.projects || []).map((p) => p.name));
    // Labels transversaux (globaux). Pré-remplis avec l'effectif courant (config, sinon repli mapper
    // exposé via A.transversalNames) → l'admin voit ce qui est actif et l'ajuste.
    const [transversal, setTransversal] = useState(() =>
      ((S.transversalLabels && S.transversalLabels.length) ? S.transversalLabels : ((A.transversalNames) || [])).slice());
    const [labelsCache, setLabelsCache] = useState({});
    const [saving, setSaving] = useState('idle'); // idle | busy | done | err
    const [err, setErr] = useState('');

    useEffect(() => {
      fetch('/api/options/projects').then((r) => r.json()).then((j) => setAllProjects(j && j.ok ? (j.projects || []) : [])).catch(() => setAllProjects([]));
    }, []);

    const projList = allProjects || [];
    const projById = (id) => projList.find((p) => p.id === id) || (S.projects || []).find((p) => p.id === id) || null;
    const projName = (id) => (projById(id) || {}).name || ('#' + id);
    const allNames = projList.map((p) => p.name);
    const importedNames = importIds.map(projName);
    const idsOf = (names) => projList.filter((p) => names.indexOf(p.name) >= 0).map((p) => p.id);
    const setImportedByNames = (names) => setImportIds(idsOf(names));

    // ---- Portée Association : « tous importés » = global ; sous-ensemble = projet actif (1er sélectionné) ----
    const assocIds = idsOf(assocProjects).filter((id) => importIds.indexOf(id) >= 0);
    const assocGlobal = !assocIds.length || sameSet(assocIds, importIds);
    const assocKey = assocGlobal ? '' : String(assocIds[0]);
    const assocMap = assocGlobal ? lpGlobal : Object.assign({}, lpGlobal, lpByProj[assocKey] || {});
    const setAssocMap = (label, val) => {
      if (assocGlobal) setLpGlobal((m) => Object.assign({}, m, { [label]: val }));
      else setLpByProj((prev) => Object.assign({}, prev, { [assocKey]: Object.assign({}, lpGlobal, prev[assocKey] || {}, { [label]: val }) }));
    };

    // ---- Labels du périmètre Association (rechargés à chaque changement de portée → réactif) ----
    const labelScopeIds = (assocGlobal ? importIds : assocIds).slice().sort((a, b) => a - b);
    const labelsKey = labelScopeIds.join(',');
    useEffect(() => {
      if (!labelScopeIds.length || labelsCache[labelsKey] !== undefined) return;
      fetch('/api/options/labels?projectIds=' + encodeURIComponent(labelScopeIds.join(','))).then((r) => r.json())
        .then((j) => setLabelsCache((prev) => Object.assign({}, prev, { [labelsKey]: j && j.ok ? (j.labels || []) : [] })))
        .catch(() => setLabelsCache((prev) => Object.assign({}, prev, { [labelsKey]: [] })));
    }, [labelsKey]);
    const curLabels = labelScopeIds.length ? labelsCache[labelsKey] : [];
    const prodLabels = (curLabels || []).filter((l) => /^prod::/i.test(l));

    // Pré-remplit les associations Prod:: manquantes (guessPhase) dans la portée courante.
    useEffect(() => {
      if (!prodLabels.length) return;
      const keys = phases.map((p) => p.key);
      if (assocGlobal) {
        const n = Object.assign({}, lpGlobal); let ch = false;
        prodLabels.forEach((l) => { if (n[l] == null) { n[l] = guessPhase(l, keys); ch = true; } });
        if (ch) setLpGlobal(n);
      } else {
        const ov = Object.assign({}, lpByProj[assocKey] || {}); let ch = false;
        prodLabels.forEach((l) => { if (assocMap[l] == null) { ov[l] = guessPhase(l, keys); ch = true; } });
        if (ch) setLpByProj((prev) => Object.assign({}, prev, { [assocKey]: ov }));
      }
    }, [curLabels, assocKey, assocGlobal]);

    // ---- Portée Équipes : idem (global vs projet actif), persistée via teamsByProject ----
    const teamIds = idsOf(teamProjects).filter((id) => importIds.indexOf(id) >= 0);
    const teamsGlobalScope = !teamIds.length || sameSet(teamIds, importIds);
    const teamKey = teamsGlobalScope ? '' : String(teamIds[0]);
    const curTeams = teamsGlobalScope ? teamsGlobal : (teamsByProj[teamKey] !== undefined ? teamsByProj[teamKey] : cloneTeams(teamsGlobal));
    const setCurTeams = (updater) => {
      if (teamsGlobalScope) setTeamsGlobal(updater);
      else setTeamsByProj((prev) => { const base = prev[teamKey] !== undefined ? prev[teamKey] : cloneTeams(teamsGlobal); return Object.assign({}, prev, { [teamKey]: updater(base) }); });
    };

    // ---- phases (globales) : édition déléguée à <PhasesGroupEditor> ; phaseColorOf sert encore à l'association labels. ----
    const phaseColorOf = (key) => key === 'none' ? '#5f6b7a' : ((phases.find((p) => p.key === key) || {}).color || '#5f6b7a');

    const filterImport = (obj) => { const o = {}; Object.keys(obj || {}).forEach((k) => { if (importIds.indexOf(+k) >= 0) o[k] = obj[k]; }); return o; };
    // Options du sélecteur de labels transversaux : les labels présents dans les données + ceux déjà
    // sélectionnés (pour qu'un label absent des données extraites reste visible et retirable).
    const tvOptions = [...new Set((((A.filterOptions || {}).labels) || []).concat(transversal))].sort();
    const payload = () => ({
      projectIds: importIds,
      projects: projList.filter((p) => importIds.indexOf(p.id) >= 0).map((p) => ({ id: p.id, name: p.name, group: p.groupFull || p.group || '' })),
      periods: phases.map((p) => ({ key: p.key, name: p.name, color: p.color, role: p.role || 'active' })),
      transversalLabels: transversal,
      labelPhases: lpGlobal,
      labelPhasesByProject: filterImport(lpByProj),
      teams: teamsGlobal.map(orderTeam),
      teamsByProject: mapClone(filterImport(teamsByProj), (arr) => arr.map(orderTeam)),
      refetch: !sameSet(importIds, initImport)
    });
    const doSave = () => {
      if (allProjects === null) return;
      if (!importIds.length) { setSaving('err'); setErr(window.t('opt.noProjects')); return; }
      setSaving('busy'); setErr('');
      fetch('/api/options', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload()) })
        .then((r) => r.json()).then((j) => {
          if (j && j.ok) { setSaving('done'); setTimeout(() => window.location.reload(), j.refetch ? 1300 : 500); }
          else { setSaving('err'); setErr((j && j.error) || window.t('opt.saveError')); }
        }).catch(() => { setSaving('err'); setErr(window.t('opt.saveError')); });
    };

    return (
      <div className="opt-sec">
        <h3>{window.t('opt.config')}</h3>
        <p className="lead">{window.t('opt.configLead', { setup: '/setup', file: 'appsettings.json' })}</p>

        {/* ---- Projets importés ---- */}
        <div className="opt-row">
          <div className="lbl">{window.t('opt.importedProjects')}<span>{window.t('opt.importedProjectsSub')}</span></div>
          {allProjects === null
            ? <p className="opt-note">{window.t('opt.loadingProjects')}</p>
            : projList.length === 0
              ? <p className="opt-note" style={{ color: 'var(--bad,#e5484d)' }}>{window.t('opt.projectsError')} <a href="/setup">/setup</a></p>
              : <span><window.MultiSelect label="" options={allNames} value={importedNames} onChange={setImportedByNames} /></span>}
        </div>

        {/* ---- Phases de production (A3 : groupes glissables Travail actif / Attentes / Hors chrono) ---- */}
        <PhasesGroupEditor phases={phases} setPhases={setPhases} />

        {/* ---- Association labels → phases (Prod::*, portée par projet) ---- */}
        <div className="opt-row" style={{ flexDirection: 'column', alignItems: 'stretch', gap: 12 }}>
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 14 }}>
            <div className="lbl">{window.t('opt.assocLabels')}<span>{window.t('opt.assocProdOnly')}</span></div>
            <window.MultiSelect label={window.t('opt.projectScope')} options={importedNames} value={assocProjects} onChange={setAssocProjects} />
          </div>
          {!labelScopeIds.length ? <p className="opt-note">{window.t('opt.noProjects')}</p>
            : curLabels === undefined ? <p className="opt-note">{window.t('opt.loadingLabels')}</p>
            : !prodLabels.length ? <p className="opt-note">{window.t('opt.noLabels')}</p>
            : <div className="opt-map">
                {prodLabels.map((l) => { const key = assocMap[l] || 'none'; return (
                  <div className="opt-maprow" key={l}>
                    <span className="opt-dot" style={{ background: phaseColorOf(key) }}></span>
                    <span className="opt-mlabel">{l}</span>
                    <select className="opt-mini" value={key} onChange={(e) => setAssocMap(l, e.target.value)}>
                      <option value="none">{window.t('opt.phNone')}</option>
                      {phases.map((p) => <option key={p.key} value={p.key}>{p.name}</option>)}
                    </select>
                  </div>); })}
              </div>}
        </div>

        {/* ---- Labels transversaux (globaux) : recoupent plusieurs types, affichés à part au dashboard.
             Ligne standard (label + contrôle sur la même ligne), comme « Projets importés ». ---- */}
        <div className="opt-row">
          <div className="lbl">{window.t('opt.transversalLabels')}<span>{window.t('opt.transversalLabelsSub')}</span></div>
          <window.MultiSelect label="" options={tvOptions} value={transversal} onChange={setTransversal} />
        </div>

        {/* ---- Équipes (portée par projet) ---- */}
        <div className="opt-row" style={{ flexDirection: 'column', alignItems: 'stretch', gap: 12 }}>
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 14 }}>
            <div className="lbl">{window.t('opt.teams')}<span>{window.t('opt.teamsSub')}</span></div>
            <window.MultiSelect label={window.t('opt.projectScope')} options={importedNames} value={teamProjects} onChange={setTeamProjects} />
          </div>
          <TeamsEditor teams={curTeams} setTeams={setCurTeams} />
        </div>

        {/* ---- Barre d'actions ---- */}
        <div className="opt-row" style={{ justifyContent: 'flex-end', gap: 12 }}>
          {saving === 'done' && <span style={{ color: 'var(--good,#2f9e44)', fontSize: 13 }}>✓ {window.t('opt.saved')}</span>}
          {saving === 'err' && <span style={{ color: 'var(--bad,#e5484d)', fontSize: 13 }}>{err || window.t('opt.saveError')}</span>}
          <a className="btn btn-sm" href="/setup">{window.t('opt.advancedSetup')}</a>
          <button className="btn btn-primary" disabled={saving === 'busy' || allProjects === null} onClick={doSave}>{saving === 'busy' ? window.t('opt.saving') : window.t('opt.save')}</button>
        </div>
      </div>);
  }

  // ===================== VUE LECTURE SEULE (non-admin) =====================
  function ReadOnlyConfig({ S }) {
    const projects = S.projects || [];
    const periods = S.periods || [];
    const lp = S.labelPhases || {};
    const teams = cloneTeams(S.teams);
    const trackedLabels = S.trackedLabels || [];
    const allLabels = trackedLabels.length ? trackedLabels : Object.keys(lp);
    const prodLabels = allLabels.filter((l) => /^prod::/i.test(l));
    const phaseColorOf = (k) => k === 'none' ? '#5f6b7a' : ((periods.find((p) => p.key === k) || {}).color || '#5f6b7a');
    const phaseName = (k) => k === 'none' ? window.t('opt.phNone') : ((periods.find((p) => p.key === k) || {}).name || k);
    return (
      <div className="opt-sec">
        <h3>{window.t('opt.config')}</h3>
        <p className="lead">{window.t('opt.configLead', { setup: '/setup', file: 'appsettings.json' })}</p>
        <div className="opt-sub">{window.t('opt.importedProjects')}</div>
        <div className="checklist">
          {projects.length ? projects.map((p) => <label key={p.id} className="on" style={{ cursor: 'default' }}><input type="checkbox" readOnly checked style={{ pointerEvents: 'none' }} />{p.name} <span style={{ opacity: .6, fontFamily: 'var(--font-mono,monospace)', fontSize: 11 }}>#{p.id}</span></label>) : <p className="opt-note">{window.t('opt.noProjects')}</p>}
        </div>
        <div className="opt-sub">{window.t('opt.prodPhases')}</div>
        <div className="opt-phases">
          {periods.length ? periods.map((p) => <div className="opt-phrow" key={p.key}><div className="opt-swatchwrap"><span className="opt-swatch" style={{ background: p.color, cursor: 'default', display: 'inline-block' }}></span></div><span className="opt-phname" style={{ padding: '6px 4px' }}>{p.name}</span></div>) : <p className="opt-note">{window.t('opt.noPhases')}</p>}
        </div>
        <div className="opt-sub">{window.t('opt.assocLabels')} <span style={subGrey}>· {window.t('opt.assocProdOnly')}</span></div>
        <div className="opt-map">
          {prodLabels.length ? prodLabels.map((l) => { const k = lp[l] || 'none'; return <div className="opt-maprow" key={l}><span className="opt-dot" style={{ background: phaseColorOf(k) }}></span><span className="opt-mlabel">{l}</span><span style={{ fontSize: 12, color: 'var(--ink-faint)', marginLeft: 'auto' }}>{phaseName(k)}</span></div>; }) : <p className="opt-note">{window.t('opt.noLabels')}</p>}
        </div>
        <div className="opt-sub">{window.t('opt.transversalLabels')} <span style={subGrey}>· {window.t('opt.transversalLabelsSub')}</span></div>
        {(() => { const tv = (S.transversalLabels && S.transversalLabels.length) ? S.transversalLabels : ((A.transversalNames) || []);
          return tv.length
            ? <div className="opt-map">{tv.map((l) => <div className="opt-maprow" key={l}><span className="opt-mlabel">{l}</span></div>)}</div>
            : <p className="opt-note">{window.t('opt.noLabels')}</p>; })()}
        <div className="opt-sub">{window.t('opt.teams')} <span style={subGrey}>· {window.t('opt.teamsSub')}</span></div>
        <TeamsEditor teams={teams} setTeams={() => {}} readOnly={true} />
      </div>);
  }

  // ===================== CALCUL DU TEMPS (admin) =====================
  // Fenêtre de temps ouvré + anti-bruit des durées de phase (cycle). Persisté via
  // POST /api/options/worktime ; le recalcul s'applique au rechargement de la page.
  function WorkTimeEditor({ lang }) {
    const W = (window.__DATA__ || {}).workTime || {};
    const [start, setStart] = useState(W.startHour != null ? W.startHour : 9);
    const [end, setEnd] = useState(W.endHour != null ? W.endHour : 19);
    const [daysOnly, setDaysOnly] = useState(W.workingDaysOnly !== false);
    // Jours fériés : tableau d'ISO (aaaa-mm-jj). Le pays + « Générer » + l'aperçu calendrier vivent dans <HolidaysCalendar>.
    const [holidays, setHolidays] = useState(W.holidays || []);
    const [noise, setNoise] = useState(W.minPhaseMinutes || 0);
    const [st, setSt] = useState('idle'); // idle | busy | done | err
    const [err, setErr] = useState('');
    const save = () => {
      setSt('busy');setErr('');
      const body = {
        workStartHour: parseInt(start, 10) || 0,
        workEndHour: parseInt(end, 10) || 0,
        workingDaysOnly: daysOnly,
        holidays: holidays,
        minPhaseMinutes: parseInt(noise, 10) || 0
      };
      fetch('/api/options/worktime', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
        .then((r) => r.json()).then((j) => {if (j.ok) setSt('done');else {setSt('err');setErr(j.error || '');}})
        .catch(() => {setSt('err');setErr('');});
    };
    const numStyle = Object.assign({}, selStyle, { width: 74, textAlign: 'center' });
    return (
      <div className="opt-sec">
        <h3>{window.t('opt.workcalc')}</h3>
        <p className="lead">{window.t('opt.workcalcLead')}</p>
        <div className="opt-row">
          <div className="lbl">{window.t('opt.workHours')}<span>{window.t('opt.workHoursSub')}</span></div>
          <input style={numStyle} type="number" min="0" max="23" value={start} onChange={(e) => setStart(e.target.value)} />
          <span className="muted">→</span>
          <input style={numStyle} type="number" min="1" max="24" value={end} onChange={(e) => setEnd(e.target.value)} />
          <span className="muted">h</span>
        </div>
        <div className="opt-row">
          <div className="lbl">{window.t('opt.workDays')}<span>{window.t('opt.workDaysSub')}</span></div>
          <Toggle on={daysOnly} onClick={() => setDaysOnly((v) => !v)} />
        </div>
        <div className="opt-row">
          <div className="lbl">{window.t('opt.noise')}<span>{window.t('opt.noiseSub')}</span></div>
          <input style={numStyle} type="number" min="0" max="1440" value={noise} onChange={(e) => setNoise(e.target.value)} />
          <span className="muted">min</span>
        </div>
        <HolidaysCalendar value={holidays} onChange={setHolidays} lang={lang} />
        <div className="opt-row">
          <div className="lbl"></div>
          <button className="btn btn-primary" disabled={st === 'busy'} onClick={save}>{window.t('opt.save')}</button>
        </div>
        {st === 'done' && <p className="opt-note" style={{ color: 'var(--c-good,#2f9e44)' }}>{window.t('opt.wtSaved')} <button className="btn btn-sm" style={{ marginLeft: 8 }} onClick={() => window.location.reload()}>{window.t('opt.reload')}</button></p>}
        {st === 'err' && <p className="opt-note" style={{ color: 'var(--c-bad,#e5484d)' }}>{err || window.t('opt.refreshError')}</p>}
      </div>);
  }

  // Connexion externe CANNY (feedback / roadmap) : saisir/valider la clé API (POST /api/options/canny,
  // testée côté serveur) puis lancer l'extraction (POST /api/refresh-canny, suivi via /api/canny-status).
  // La clé n'est jamais renvoyée au client. Réservé aux admins.
  function CannyConfigEditor() {
    const C = ((window.__DATA__ || {}).setup || {}).canny || {};
    const [apiKey, setApiKey] = useState('');
    const [connected, setConnected] = useState(!!C.connected);
    const lastExtracted = C.lastExtracted || '';
    const [st, setSt] = useState('idle'); // connexion : idle | busy | done | err
    const [err, setErr] = useState('');
    const [refreshState, setRefreshState] = useState('idle'); // extraction : idle | busy | done | err
    const pollRef = React.useRef(null);
    const stopPoll = () => {if (pollRef.current) {clearInterval(pollRef.current);pollRef.current = null;}};
    const startPoll = () => {
      stopPoll();
      pollRef.current = setInterval(() => {
        fetch('/api/canny-status').then((r) => r.json()).then((s) => {
          if (!s.running) {stopPoll();setRefreshState(s.lastError ? 'err' : 'done');if (s.lastError) setErr(s.lastError);}
        }).catch(() => {});
      }, 1500);
    };
    React.useEffect(() => {
      fetch('/api/canny-status').then((r) => r.json()).then((s) => {if (s.running) {setRefreshState('busy');startPoll();}}).catch(() => {});
      return stopPoll;
    }, []);
    const connect = () => {
      setSt('busy');setErr('');
      fetch('/api/options/canny', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ apiKey }) })
        .then((r) => r.json()).then((j) => {if (j.ok) {setSt('done');setConnected(!!j.connected);setApiKey('');} else {setSt('err');setErr(j.error || '');}})
        .catch(() => {setSt('err');setErr('');});
    };
    const refresh = () => {
      setRefreshState('busy');setErr('');
      fetch('/api/refresh-canny', { method: 'POST' })
        .then((r) => {if (r.ok || r.status === 409) startPoll();else setRefreshState('err');})
        .catch(() => setRefreshState('err'));
    };
    const busy = refreshState === 'busy';
    const keyStyle = Object.assign({}, selStyle, { width: 300, fontFamily: 'var(--mono, monospace)' });
    return (
      <div className="opt-sec">
        <h3>{window.t('opt.cannyTitle')}</h3>
        <p className="lead">{window.t('opt.cannyLead')}</p>
        <div className="opt-row">
          <div className="lbl">{window.t('opt.cannyStatus')}<span>{connected ? (lastExtracted ? window.t('opt.cannyLast') + ' ' + lastExtracted : window.t('opt.cannyNoData')) : window.t('opt.cannyNotConnected')}</span></div>
          <span style={{ fontSize: 13, fontWeight: 600, color: connected ? 'var(--c-good,#2f9e44)' : 'var(--ink-dim,#888)' }}>{connected ? window.t('opt.cannyConnected') : '—'}</span>
        </div>
        <div className="opt-row">
          <div className="lbl">{window.t('opt.cannyKey')}<span>{window.t('opt.cannyKeySub')}</span></div>
          <input style={keyStyle} type="password" value={apiKey} placeholder={connected ? '••••••••' : ''} autoComplete="off" onChange={(e) => setApiKey(e.target.value)} />
          <button className="btn" disabled={st === 'busy' || (!apiKey && !connected)} onClick={connect}>{window.t('opt.cannyConnect')}</button>
        </div>
        {connected &&
        <div className="opt-row">
          <div className="lbl">{window.t('opt.cannyRefresh')}<span>{window.t('opt.cannyRefreshSub')}</span></div>
          <button className="btn btn-primary" disabled={busy} onClick={refresh}>{window.ICONS.refresh} {busy ? window.t('opt.cannyRefreshing') : window.t('opt.cannyRefreshBtn')}</button>
        </div>}
        {st === 'done' && <p className="opt-note" style={{ color: 'var(--c-good,#2f9e44)' }}>{window.t('opt.cannySaved')}</p>}
        {refreshState === 'done' && <p className="opt-note" style={{ color: 'var(--c-good,#2f9e44)' }}>{window.t('opt.cannyDone')} <button className="btn btn-sm" style={{ marginLeft: 8 }} onClick={() => window.location.reload()}>{window.t('opt.reload')}</button></p>}
        {(st === 'err' || refreshState === 'err') && <p className="opt-note" style={{ color: 'var(--c-bad,#e5484d)' }}>{err || window.t('opt.refreshError')}</p>}
      </div>);
  }

  // ===================== ONGLET =====================
  window.TabOptions = function TabOptions({ theme, setTheme, appearance }) {
    const { accent, setAccent, numFont, setNumFont, compact, setCompact, drillLayout, setDrillLayout } = appearance;
    const S = (window.__DATA__ || {}).setup || {};
    const isAdmin = !!S.isAdmin;

    // ---- Régénération des données : projet → milestone → lancement, avec progression + annulation.
    const projects = S.projects || [];
    const [regenProj, setRegenProj] = useState('');
    const [regenMs, setRegenMs] = useState([]); // MULTI : plusieurs milestones ré-extraites en une course (le serveur boucle + merge)
    // Milestones du sélecteur : récupérées EN DIRECT sur GitLab (/api/options/milestones) — le
    // catalogue extrait (availableMilestones) est VIDE avant la 1re extraction (œuf et poule).
    // Repli initial : catalogue du payload. Rechargées quand le projet ciblé change.
    const [msOpts, setMsOpts] = useState(() => (A.filterOptions || {}).milestones || []);
    React.useEffect(() => {
      if (!isAdmin) return;
      let dead = false;
      fetch('/api/options/milestones' + (regenProj ? '?projectIds=' + encodeURIComponent(regenProj) : ''))
        .then((r) => r.json()).then((j) => {if (!dead && j.ok && j.milestones) setMsOpts(j.milestones);}).catch(() => {});
      return () => {dead = true;};
    }, [regenProj]);
    const [refreshState, setRefreshState] = useState('idle'); // idle | busy | done | cancelled | err
    const [prog, setProg] = useState(null); // snapshot /api/status {running,current,total,...}
    const pollRef = React.useRef(null);
    const stopPoll = () => {if (pollRef.current) {clearInterval(pollRef.current);pollRef.current = null;}};
    const startPoll = () => {
      stopPoll();
      pollRef.current = setInterval(() => {
        fetch('/api/status').then((r) => r.json()).then((s) => {
          setProg(s);
          if (!s.running) {
            stopPoll();
            // le serveur pose « Annulé par l'utilisateur. » dans lastError sur un cancel
            setRefreshState(s.lastError ? (/annul/i.test(s.lastError) ? 'cancelled' : 'err') : 'done');
          }
        }).catch(() => {});
      }, 1200);
    };
    React.useEffect(() => {
      // une acquisition tourne déjà (lancée ailleurs / avant l'arrivée sur l'onglet) → reprendre l'affichage
      if (isAdmin) fetch('/api/status').then((r) => r.json()).then((s) => {if (s.running) {setRefreshState('busy');setProg(s);startPoll();}}).catch(() => {});
      return stopPoll;
    }, []);
    const doRefresh = () => {
      setRefreshState('busy');setProg(null);
      const body = {};
      if (regenProj) body.project = String(regenProj);
      if (regenMs.length) body.milestones = regenMs;
      fetch('/api/refresh', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
        .then((r) => {if (r.ok || r.status === 409) startPoll();else setRefreshState('err');}) // 409 = déjà en cours → suivre celle-là
        .catch(() => setRefreshState('err'));
    };
    const doCancel = () => {fetch('/api/cancel', { method: 'POST' }).catch(() => {});};
    const regenBusy = refreshState === 'busy';

    return (
      <div style={{ maxWidth: 860 }}>
        <div className="opt-sec">
          <h3>{window.t('opt.appearance')}</h3>
          <p className="lead">{window.t('opt.appearanceLead')}</p>
          <div className="opt-row">
            <div className="lbl">{window.t('lang')}<span>{window.t('lang_sub')}</span></div>
            <select style={selStyle} value={appearance.lang} onChange={(e) => { window.location.href = '/set-lang?lang=' + encodeURIComponent(e.target.value) + '&return=' + encodeURIComponent(window.location.pathname); }}>
              {(appearance.langs || [['fr', 'Français'], ['en', 'English']]).map(([k, lbl]) => <option key={k} value={k}>{lbl}</option>)}
            </select>
          </div>
          <div className="opt-row">
            <div className="lbl">{window.t('opt.theme')}<span>{window.t('opt.themeSub')}</span></div>
            <div className="seg-lg">
              {['auto', 'light', 'dark'].map((th) => <button key={th} className={theme === th ? 'on' : ''} onClick={() => setTheme(th)}>{window.t(th === 'auto' ? 'opt.auto' : th === 'light' ? 'opt.light' : 'opt.dark')}</button>)}
            </div>
          </div>
          <div className="opt-row">
            <div className="lbl">{window.t('opt.accent')}<span>{window.t('opt.accentSub')}</span></div>
            <div className="swatches">
              {ACCENTS.map(([c, name]) => <button key={c} className={'swatch' + (accent === c ? ' on' : '')} title={name} style={{ background: c }} onClick={() => setAccent(c)}>{accent === c ? '✓' : ''}</button>)}
            </div>
          </div>
          <div className="opt-row">
            <div className="lbl">{window.t('opt.numFont')}<span>{window.t('opt.numFontSub')}</span></div>
            <div className="seg-lg">
              {NUMFONTS.map(([k, lbl]) => <button key={k} className={numFont === k ? 'on' : ''} onClick={() => setNumFont(k)}>{k === 'system' ? window.t('opt.system') : lbl}</button>)}
            </div>
          </div>
          <div className="opt-row">
            <div className="lbl">{window.t('opt.compact')}<span>{window.t('opt.compactSub')}</span></div>
            <Toggle on={compact} onClick={() => setCompact((c) => !c)} />
          </div>
          <div className="opt-row">
            <div className="lbl">{window.t('opt.drill')}<span>{window.t('opt.drillSub')}</span></div>
            <div className="seg-lg">
              {DRILL_LAYOUTS.map((k) => <button key={k} className={drillLayout === k ? 'on' : ''} onClick={() => setDrillLayout(k)}>{window.t(DRILL_T[k])}</button>)}
            </div>
          </div>
        </div>

        {isAdmin &&
        <div className="opt-sec">
          <h3>{window.t('opt.regen')}</h3>
          <p className="lead">{window.t('opt.regenLead', { date: (A.meta || {}).extracted || '—' })}</p>
          <div className="opt-row">
            <div className="lbl">{window.t('opt.regenProject')}<span>{window.t('opt.regenProjectSub')}</span></div>
            {/* changer de projet vide la sélection de milestones (elles peuvent ne pas exister ailleurs) */}
            <select style={selStyle} value={regenProj} disabled={regenBusy} onChange={(e) => {setRegenProj(e.target.value);setRegenMs([]);}}>
              <option value="">{window.t('opt.allProjects')}</option>
              {projects.map((p) => <option key={p.id} value={String(p.id)}>{p.name}</option>)}
            </select>
          </div>
          <div className="opt-row">
            <div className="lbl">{window.t('opt.regenMs')}<span>{window.t('opt.regenMsSub')}</span></div>
            {/* MULTI-sélection (checkboxes + recherche) — vide = toutes les milestones. */}
            <div style={regenBusy ? { pointerEvents: 'none', opacity: 0.55 } : null}>
              <window.MultiSelect label={window.t('opt.regenMs')} value={regenMs} onChange={setRegenMs} options={msOpts} />
            </div>
          </div>
          {!regenBusy &&
          <div className="opt-row">
            <div className="lbl"></div>
            <button className="btn btn-primary" onClick={doRefresh}>{window.ICONS.refresh} {window.t('opt.refresh')}</button>
          </div>}
          {regenBusy &&
          <div className="opt-row">
            <div className="lbl">{window.t('opt.extracting')}<span>{prog && prog.total > 0 ? prog.current + ' / ' + prog.total + ' ' + window.t('issues') : '…'}</span></div>
            <div className={'opt-progress' + (prog && prog.total > 0 ? '' : ' ind')}><i style={{ width: (prog && prog.total > 0 ? Math.min(100, Math.round(prog.current / prog.total * 100)) : 12) + '%' }}></i></div>
            <button className="btn" onClick={doCancel}>{window.t('opt.cancel')}</button>
          </div>}
          {refreshState === 'done' && <p className="opt-note" style={{ color: 'var(--c-good,#2f9e44)' }}>{window.t('opt.refreshDone')} <button className="btn btn-sm" style={{ marginLeft: 8 }} onClick={() => window.location.reload()}>{window.t('opt.reload')}</button></p>}
          {refreshState === 'cancelled' && <p className="opt-note">{window.t('opt.refreshCancelled')}</p>}
          {refreshState === 'err' && <p className="opt-note" style={{ color: 'var(--c-bad,#e5484d)' }}>{window.t('opt.refreshError')}</p>}
        </div>}

        {isAdmin && <WorkTimeEditor lang={appearance.lang} />}

        {isAdmin && <CannyConfigEditor />}

        {isAdmin ? <AdminConfigEditor S={S} /> : <ReadOnlyConfig S={S} />}
      </div>);
  };
})();
