// tab-comparison.jsx — onglet « Évolution » : compare les KPI sur plusieurs milestones.
// CONNECTÉ AUX DONNÉES RÉELLES : les séries par jalon sont calculées depuis le payload déjà filtré par
// COMPTE (window.__DATA__.issues), en réutilisant TOUTE la logique métier du mapper (window.buildAPP) par
// milestone. Aucune donnée hors périmètre n'est exposée (le payload est restreint côté serveur).
// Matrice à colonnes fixes (Métrique · Départ · Actuel · Écart · Tendance) + graphe d'évolution.
(function () {
  const { useState, useMemo } = React;

  // Construit { MILESTONES (triés chrono), METRICS:[{k,label,unit,hb,vals:{ms:val}}] } depuis le payload réel.
  // hb = higherBetter (false → une baisse est positive : cycle, anomalies, retours).
  // selU = sélection Équipe/Utilisateur active (usernames minuscules, null = tout) : l'Évolution suit
  // les mêmes filtres que le reste du dashboard (le filtre Milestone, lui, est piloté par l'onglet).
  function buildEvolution(D, selU) {
    D = D || {};
    const all = (D.issues || []).filter((i) => !selU || (i.assignees || []).some((a) => selU.indexOf(String(a).toLowerCase()) >= 0));
    const dates = D.milestoneDates || {};
    let ms = (D.availableMilestones || []).slice();
    // Tri chronologique : jalons DATÉS d'abord (date de début, sinon échéance), triés par date ISO ; puis les
    // jalons SANS date, par nom — évite de mélanger un titre arbitraire avec des dates ISO.
    const keyOf = (m) => { const d = dates[m] || {}; return d.startDate || d.dueDate || m; };
    const hasD = (m) => { const d = dates[m] || {}; return !!(d.startDate || d.dueDate); };
    ms.sort((a, b) => { const ha = hasD(a), hbb = hasD(b); if (ha !== hbb) return ha ? -1 : 1; return String(keyOf(a)).localeCompare(String(keyOf(b))); });
    if (!ms.length || typeof window.buildAPP !== 'function') return { MILESTONES: [], METRICS: [] };
    const per = {};
    ms.forEach((m) => {
      // Sous-périmètre = issues du jalon (issues DÉJÀ filtrées par compte) → KPI recalculés par le mapper.
      const sub = window.buildAPP(Object.assign({}, D, { issues: all.filter((i) => i.milestone === m), allIssues: all, selectedMilestones: [m] }));
      const anom = Object.keys(sub.anomalies || {}).reduce((s, k) => s + (sub.anomalies[k] || []).length, 0);
      per[m] = {
        prog: (sub.kpis.progress || {}).pct || 0,
        weight: (sub.kpis.weight || {}).pct || 0,
        appr: (sub.kpis.approvals || {}).pct || 0,
        cycle: (sub.kpis.cycle || {}).days || 0,
        p85: (sub.kpis.cycle || {}).p85 || 0,
        closed: (sub.totals || {}).closed || 0,
        anom: anom,
        ret: (sub.totals || {}).ret || 0,
      };
    });
    const defs = [
      { k: 'prog', label: 'dash.advancement', unit: '%', hb: true },
      { k: 'weight', label: 'dash.weightValidated', unit: '%', hb: true },
      { k: 'appr', label: 'dash.approvals', unit: '%', hb: true },
      { k: 'cycle', label: 'dash.avgCycle', unit: ' j', hb: false },
      { k: 'p85', label: 'cmpv.cycleP85', unit: ' j', hb: false },
      { k: 'closed', label: 'cmpv.closed', unit: '', hb: true },
      { k: 'anom', label: 'cmpv.anomalies', unit: '', hb: false },
      { k: 'ret', label: 'cmpv.returns', unit: '', hb: false },
    ];
    const METRICS = defs.map((d) => Object.assign({}, d, { vals: Object.fromEntries(ms.map((m) => [m, per[m][d.k]])) }));
    return { MILESTONES: ms, METRICS };
  }

  // Cache HORS-React : window.__DATA__ est immuable pour la page ; le calcul (mapper par jalon) est coûteux.
  // useMemo([]) ne survit pas au démontage/remontage de l'onglet → on mémorise par (payload, sélection).
  let _evoCache = null, _evoKey = null;
  function evolutionFor(D, selU) {
    const key = (selU ? selU.slice().sort().join('|') : '') + '§';
    if (_evoKey && _evoKey.d === D && _evoKey.k === key && _evoCache) return _evoCache;
    _evoKey = { d: D, k: key }; _evoCache = buildEvolution(D, selU); return _evoCache;
  }

  const fmt = (v, u) => Math.round(v * 10) / 10 + (u || '');
  const round1 = (v) => Math.round(v * 10) / 10;

  function MiniSpark({ vals, hb, w = 104, h = 30 }) {
    // 1 seul point → pas de tendance traçable : un point centré (évite des coordonnées NaN dans le polyline).
    if (!vals || vals.length < 2) return <svg className="cmpv-spark" width={w} height={h} viewBox={`0 0 ${w} ${h}`}><circle cx={w / 2} cy={h / 2} r="2.6" fill="var(--ink-faint)" /></svg>;
    const mn = Math.min(...vals), mx = Math.max(...vals), span = mx - mn || 1;
    const last = vals[vals.length - 1] - vals[vals.length - 2];
    const good = last === 0 ? null : hb ? last > 0 : last < 0;
    const col = good == null ? 'var(--ink-faint)' : good ? 'var(--c-good)' : 'var(--c-bad)';
    const pts = vals.map((v, i) => `${i / (vals.length - 1) * (w - 4) + 2},${h - 3 - (v - mn) / span * (h - 8)}`);
    const lastPt = pts[pts.length - 1].split(',');
    return (
      <svg className="cmpv-spark" width={w} height={h} viewBox={`0 0 ${w} ${h}`}>
        <polyline points={pts.join(' ')} fill="none" stroke={col} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
        <circle cx={lastPt[0]} cy={lastPt[1]} r="2.6" fill={col} />
      </svg>);
  }

  function DeltaPill({ d, unit, good }) {
    if (d === 0) return <span className="cmpv-delta flat">=</span>;
    return <span className={'cmpv-delta ' + (good ? 'up' : 'down')}>
      <span className="ar">{d > 0 ? '↑' : '↓'}</span>{(d > 0 ? '+' : '') + round1(d)}{unit}
    </span>;
  }

  // Graphe d'évolution : la valeur est imprimée directement sur chaque point. viewBox uniforme → pas de scroll.
  function FocusChart({ cols, fm, last }) {
    const [hover, setHover] = React.useState(null);
    const [pinned, setPinned] = React.useState(() => new Set());
    // reset l'épinglage quand on change de métrique ou de sélection de milestones
    React.useEffect(() => {setPinned(new Set());setHover(null);}, [fm.k, cols.join('|')]);
    const togglePin = (i) => setPinned((prev) => {const nx = new Set(prev);nx.has(i) ? nx.delete(i) : nx.add(i);return nx;});
    const vals = cols.map((c) => fm.vals[c]);
    const n = vals.length;
    const W = 1000, H = 300, padL = 40, padR = 40, padT = 50, padB = 40;
    const mn = Math.min(...vals), mx = Math.max(...vals), span = mx - mn || 1;
    const x = (i) => padL + i / (n - 1 || 1) * (W - padL - padR);
    const y = (v) => padT + (1 - (v - mn) / span) * (H - padT - padB);
    const pts = vals.map((v, i) => [x(i), y(v)]);
    const linePts = pts.map((p) => p.join(',')).join(' ');
    const areaPts = `${pts[0][0]},${H - padB} ` + linePts + ` ${pts[n - 1][0]},${H - padB}`;
    const step = Math.max(1, Math.ceil(n / 8));
    return (
      <svg className="cmpv-fc" viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="xMidYMid meet" width="100%" role="img">
        <defs>
          <linearGradient id="cmpvFcFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--accent)" stopOpacity="0.16" />
            <stop offset="100%" stopColor="var(--accent)" stopOpacity="0" />
          </linearGradient>
        </defs>
        <polygon points={areaPts} fill="url(#cmpvFcFill)" />
        <polyline points={linePts} fill="none" stroke="var(--accent)" strokeWidth="2.5" strokeLinejoin="round" strokeLinecap="round" />
        {pts.map((p, i) => {
          const isCur = cols[i] === last;
          const dprev = i > 0 ? vals[i] - vals[i - 1] : null;
          const g = dprev == null || dprev === 0 ? null : fm.hb ? dprev > 0 : dprev < 0;
          const dotCol = isCur ? 'var(--accent)' : g == null ? 'var(--ink-faint)' : g ? 'var(--c-good)' : 'var(--c-bad)';
          const showX = i % step === 0 || isCur || i === 0;
          const isHover = hover === i;
          const isPinned = pinned.has(i);
          const showVal = isCur || isHover || isPinned; // la valeur courante reste toujours visible
          return (
            <g key={cols[i]} className="cmpv-fc-pt" onMouseEnter={() => setHover(i)} onMouseLeave={() => setHover((h) => h === i ? null : h)} onClick={() => togglePin(i)} style={{ cursor: 'pointer' }}>
              {(isCur || isHover || isPinned) && <line x1={p[0]} y1={padT - 18} x2={p[0]} y2={H - padB} stroke={isCur ? 'var(--accent)' : 'var(--line)'} strokeWidth="1" strokeDasharray="3 4" opacity={isCur ? 0.5 : 0.9} />}
              <circle cx={p[0]} cy={p[1]} r={isCur || isHover ? 5.5 : isPinned ? 5 : 4} fill={dotCol} />
              {isPinned && !isCur && <circle cx={p[0]} cy={p[1]} r="8.5" fill="none" stroke={dotCol} strokeWidth="1.5" opacity="0.6" />}
              {showVal && <text x={p[0]} y={p[1] - 15} className={'cmpv-fc-val' + (isCur ? ' cur' : '') + (isHover && !isCur ? ' hov' : '')} textAnchor="middle">{fmt(vals[i], fm.unit)}</text>}
              {showX && <text x={p[0]} y={H - padB + 24} className={'cmpv-fc-x' + (isCur ? ' cur' : '')} textAnchor="middle">{cols[i]}</text>}
              <circle cx={p[0]} cy={p[1]} r="16" fill="transparent" />
            </g>);
        })}
      </svg>);
  }

  window.TabComparison = function TabComparison() {
    const t = window.t;
    // Sélection Équipe/Utilisateur courante (posée par __applyFilters) → recalcul quand elle change.
    const selU = window.APP.selectedUsers;
    const selSig = selU ? selU.slice().sort().join('|') : '';
    const ev = useMemo(() => evolutionFor(window.__DATA__, selU), [selSig]);
    const MILESTONES = ev.MILESTONES, METRICS = ev.METRICS;
    // Hooks AVANT tout retour conditionnel (ordre des hooks stable).
    const [sel, setSel] = useState(() => MILESTONES.slice(-Math.min(4, MILESTONES.length || 1)));
    const [focus, setFocus] = useState(() => (METRICS[0] || {}).k || 'prog');
    const [pickOpen, setPickOpen] = useState(false);

    if (!MILESTONES.length)
      return <div className="cmpv"><div className="pnl"><div className="pnl-b"><p style={{ color: 'var(--ink-faint)', padding: '10px 2px' }}>{t('cmpv.noData')}</p></div></div></div>;

    const cols = MILESTONES.filter((m) => sel.includes(m)); // ordre chronologique
    const YEARS = MILESTONES.reduce((a, m) => { const y = m.split('-')[0]; (a[y] = a[y] || []).push(m); return a; }, {});
    const setLastN = (n) => setSel(MILESTONES.slice(-n));
    const presetN = sel.length === MILESTONES.length ? 'all' : cols.join() === MILESTONES.slice(-sel.length).join() ? sel.length : null;
    const toggle = (m) => setSel((s) => {
      if (s.includes(m)) { return s.length <= 2 ? s : s.filter((x) => x !== m); }
      return MILESTONES.filter((x) => s.includes(x) || x === m);
    });
    const last = cols[cols.length - 1], prev = cols[cols.length - 2] || cols[0]; // prev=cols[0] si 1 seul jalon → delta 0

    let improved = 0, regressed = 0;
    METRICS.forEach((m) => {
      const d = m.vals[last] - m.vals[prev];
      if (d === 0) return; const g = m.hb ? d > 0 : d < 0; g ? improved++ : regressed++;
    });

    const fm = METRICS.find((m) => m.k === focus) || METRICS[0];

    return (
      <div className="cmpv">
        <div className="pnl cmpv-controls">
          <div className="cmpv-ctl-h">
            <span className="cmpv-ctl-lbl">{t('cmpv.milestones')}</span>
            <window.InfoTip text={t('cmpv.tip')} />
          </div>
          <div className="cmpv-pickbar">
            <div className="cmpv-presets">
              <button className={presetN === 4 ? 'on' : ''} onClick={() => setLastN(4)}>{t('cmpv.last', { n: 4 })}</button>
              <button className={presetN === 8 ? 'on' : ''} onClick={() => setLastN(8)}>{t('cmpv.last', { n: 8 })}</button>
              <button className={presetN === 'all' ? 'on' : ''} onClick={() => setSel(MILESTONES.slice())}>{t('cmpv.all')}</button>
            </div>
            <div className="cmpv-pickwrap">
              <button className={'cmpv-pickbtn' + (pickOpen ? ' on' : '')} onClick={() => setPickOpen((o) => !o)}>
                {t('cmpv.nSelected', { n: cols.length })} <span className="cmpv-cv">{cols[0]} → {last}</span>
                <span className="cmpv-caret">{window.ICONS.chevron}</span>
              </button>
              {pickOpen &&
              <div className="cmpv-pop">
                  <div className="cmpv-pop-h"><span>{t('cmpv.pickTitle')}</span><button onClick={() => setPickOpen(false)} className="cmpv-pop-x">✕</button></div>
                  <div className="cmpv-pop-body">
                    {Object.keys(YEARS).map((y) =>
                  <div key={y} className="cmpv-pop-year">
                        <div className="cmpv-pop-yh">{y}</div>
                        <div className="cmpv-pop-qs">
                          {YEARS[y].map((m) =>
                      <button key={m} className={'cmpv-q' + (sel.includes(m) ? ' on' : '') + (m === last ? ' cur' : '')} onClick={() => toggle(m)}>
                              {(m.split('-')[1] || m)}{m === last && <span className="cmpv-qdot"></span>}
                            </button>)}
                        </div>
                      </div>)}
                  </div>
                </div>}
            </div>
          </div>
        </div>

        <div className="cmpv-summary">
          <div className="cmpv-sum-card up">
            <span className="cmpv-sum-n">{improved}</span>
            <span className="cmpv-sum-l">{t('cmpv.improved')}</span>
            <span className="cmpv-sum-sub">{t('cmpv.vsPrev', { m: prev })}</span>
          </div>
          <div className="cmpv-sum-card down">
            <span className="cmpv-sum-n">{regressed}</span>
            <span className="cmpv-sum-l">{t('cmpv.regressed')}</span>
            <span className="cmpv-sum-sub">{t('cmpv.vsPrev', { m: prev })}</span>
          </div>
          <div className="cmpv-sum-card neutral">
            <span className="cmpv-sum-n">{cols.length}</span>
            <span className="cmpv-sum-l">{t('cmpv.compared')}</span>
            <span className="cmpv-sum-sub">{cols[0]} → {last}</span>
          </div>
        </div>

        <div className="pnl">
          <div className="pnl-h"><h3>{t('cmpv.matrix')}</h3><window.InfoTip text={t('cmpv.matrixTip')} /></div>
          <div className="tbl-scroll">
            <table className="cmpv-tbl">
              <thead>
                <tr>
                  <th>{t('cmpv.metric')}</th>
                  <th className="num">{t('cmpv.start')}<span className="cmpv-th-ms">{cols[0]}</span></th>
                  <th className="num cur">{t('cmpv.latest')}<span className="cmpv-th-ms">{last}</span></th>
                  <th className="num">{t('cmpv.delta')}</th>
                  <th className="num">{t('cmpv.trend')}<span className="cmpv-th-ms">{cols[0]} → {last}</span></th>
                </tr>
              </thead>
              <tbody>
                {METRICS.map((m) => {
                  const seriesV = cols.map((c) => m.vals[c]);
                  const d = m.vals[last] - m.vals[cols[0]];
                  const g = d === 0 ? null : m.hb ? d > 0 : d < 0;
                  return (
                    <tr key={m.k} className={focus === m.k ? 'on' : ''} onClick={() => setFocus(m.k)}>
                      <td className="cmpv-mname">{t(m.label)}</td>
                      <td className="num cmpv-cell">{fmt(m.vals[cols[0]], m.unit)}</td>
                      <td className="num cmpv-cell cur">{fmt(m.vals[last], m.unit)}</td>
                      <td className="num"><DeltaPill d={d} unit={m.unit} good={g} /></td>
                      <td className="cmpv-sparkc"><MiniSpark vals={seriesV} hb={m.hb} w={180} /></td>
                    </tr>);
                })}
              </tbody>
            </table>
          </div>
        </div>

        {fm &&
        <div className="pnl">
          <div className="pnl-h"><h3>{t('cmpv.evolution')} · {t(fm.label)}</h3><window.InfoTip text={t('cmpv.evolutionTip')} /></div>
          <div className="pnl-b">
            <div className="cmpv-fc-hint">{t('cmpv.fcHint')}</div>
            <div className="cmpv-focus-full">
              <FocusChart cols={cols} fm={fm} last={last} />
            </div>
          </div>
        </div>}
      </div>);
  };
})();
