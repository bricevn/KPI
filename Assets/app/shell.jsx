// Shell — sidebar nav, header, global filters, tab routing, theme toggle.
(function () {
  const { useState, useEffect } = React;
  const NAV = [['dashboard', 'Dashboard'], ['charts', 'Graphiques'], ['anomalies', 'Anomalies'], ['issues', 'Issues'], ['calendar', 'Calendrier'], ['velocity', 'Vélocité']];
  const TITLES = { dashboard: 'Dashboard', charts: 'Graphiques', anomalies: 'Anomalies', issues: 'Issues', calendar: 'Calendrier', velocity: 'Vélocité', options: 'Options' };

  function Stub({ name }) {return <div className="empty">Onglet « {name} » — à venir</div>;}

  const NUM_FONTS = { grotesk: "'Space Grotesk'", mono: "'IBM Plex Mono'", system: "system-ui" };
  const load = (k, d) => {try {const v = localStorage.getItem(k);return v == null ? d : v;} catch (e) {return d;}};

  const CHART_TWEAKS = /*EDITMODE-BEGIN*/{
    "recapStyle": "cartes",
    "poidsStyle": "barres",
    "tempsStyle": "empile"
  }/*EDITMODE-END*/;

  window.Shell = function Shell() {
    const A = window.APP;
    const [t, setTweak] = window.useTweaks(CHART_TWEAKS);
    const [tab, setTab] = useState(() => {try {return localStorage.getItem('app-tab') || 'dashboard';} catch (e) {return 'dashboard';}});
    const [theme, setTheme] = useState(() => load('app-theme', 'dark'));
    const [accent, setAccent] = useState(() => load('app-accent', '#2b7fff'));
    const [numFont, setNumFont] = useState(() => load('app-numfont', 'grotesk'));
    const [compact, setCompact] = useState(() => load('app-compact', '0') === '1');
    const [sbCollapsed, setSbCollapsed] = useState(() => load('app-sb', '0') === '1');
    const [drillLayout, setDrillLayout] = useState(() => load('app-drill', 'modal'));
    const [fMilestone, setFMilestone] = useState(() => { try { return window.APP && window.APP.milestone && window.APP.milestone.name ? [window.APP.milestone.name] : []; } catch (e) { return []; } });
    const [fLabel, setFLabel] = useState([]);
    const [fTeam, setFTeam] = useState([]);
    const [fUser, setFUser] = useState([]);
    useEffect(() => {try {localStorage.setItem('app-tab', tab);} catch (e) {}}, [tab]);
    useEffect(() => {try {localStorage.setItem('app-theme', theme);} catch (e) {}}, [theme]);
    useEffect(() => {try {localStorage.setItem('app-accent', accent);} catch (e) {}}, [accent]);
    useEffect(() => {try {localStorage.setItem('app-numfont', numFont);} catch (e) {}}, [numFont]);
    useEffect(() => {try {localStorage.setItem('app-compact', compact ? '1' : '0');} catch (e) {}}, [compact]);
    useEffect(() => {try {localStorage.setItem('app-sb', sbCollapsed ? '1' : '0');} catch (e) {}}, [sbCollapsed]);
    useEffect(() => {try {localStorage.setItem('app-drill', drillLayout);} catch (e) {}}, [drillLayout]);
    window.__drillLayout = drillLayout;

    // Identité réelle de la session (cookie) — remplace le libellé codé en dur du prototype.
    const [me, setMe] = useState(null);
    useEffect(() => { fetch('/api/me', { credentials: 'same-origin' }).then((r) => r.ok ? r.json() : null).then(setMe).catch(() => {}); }, []);
    const ROLE_LBL = { admin: 'Admin', group: 'Groupe', user: 'Utilisateur' };

    const appearance = { accent, setAccent, numFont, setNumFont, compact, setCompact, drillLayout, setDrillLayout };

    const resolved = theme === 'auto' ? window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light' : theme;

    const TabComp = {
      dashboard: window.TabDashboard, charts: window.TabCharts, anomalies: window.TabAnomalies,
      issues: window.TabIssues, calendar: window.TabCalendar, velocity: window.TabVelocity, options: window.TabOptions
    }[tab];

    const showFilters = tab !== 'options';
    const isCharts = tab === 'charts';
    const filtersActive = fLabel.length > 0 || fTeam.length > 0 || fUser.length > 0;
    const clearFilters = () => {setFLabel([]);setFTeam([]);setFUser([]);};

    // Refiltre les données (en place) selon les pills avant que les onglets ne relisent window.APP.
    if (window.__applyFilters) window.__applyFilters({ milestones: fMilestone, labels: fLabel, teams: fTeam, users: fUser });

    const rootStyle = {
      '--accent': accent, '--accent-2': accent,
      '--accent-soft': `color-mix(in srgb, ${accent} 15%, transparent)`,
      '--disp-font': NUM_FONTS[numFont] || NUM_FONTS.grotesk
    };

    return (
      <div className={'app kpi-root' + (compact ? ' compact' : '')} data-theme={resolved} style={rootStyle}>
        <aside className={'sb' + (sbCollapsed ? ' collapsed' : '')}>
          <div className="sb-brand">
            <div className="sb-mark">O</div>
            <div><div className="nm">OODA KPI</div><div className="sub">{me ? (ROLE_LBL[me.role] || me.role) + ' · ' + me.login : '…'}</div></div>
          </div>
          <div className="sb-h">Pilotage</div>
          <nav className="sb-nav">
            {NAV.map(([id, label]) =>
            <button key={id} className={'sb-item' + (tab === id ? ' on' : '')} onClick={() => setTab(id)}>
                {window.ICONS[id]}<span>{label}</span>
                {id === 'anomalies' && <span className="badge">{Object.keys(A.anomalies || {}).reduce((s, k) => s + (A.anomalies[k] ? A.anomalies[k].length : 0), 0)}</span>}
                {id === 'issues' && <span className="cnt">{A.totals.issues}</span>}
              </button>
            )}
          </nav>
          <div className="sb-sp"></div>
          <div className="sb-h">Réglages</div>
          <button className={'sb-item' + (tab === 'options' ? ' on' : '')} onClick={() => setTab('options')} title="Options">{window.ICONS.options}<span>Options</span></button>
          <button className="sb-item" onClick={() => { window.location.href = '/logout'; }} title="Se déconnecter">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4" /><path d="M16 17l5-5-5-5" /><path d="M21 12H9" /></svg><span>Déconnexion</span>
          </button>
          <button className="sb-collapse" onClick={() => setSbCollapsed((c) => !c)} title={sbCollapsed ? 'Étendre la barre' : 'Réduire la barre'}>
            <span className="sb-collapse-ic">{window.ICONS.chevron}</span><span>Réduire</span>
          </button>
        </aside>

        <main className="main">
          <div className="hd">
            <div>
              <h1 className="disp">Release {A.milestone.name} · {TITLES[tab]}</h1>
              <div className="meta">{A.meta.project} · généré le {A.meta.generated} · {A.totals.issues} issues</div>
            </div>
            <div className="hd-ms">
              <div className="r"><span>Avancement milestone</span><b>{A.milestone.dayPct}%</b></div>
              <div className="track"><i style={{ width: A.milestone.dayPct + '%', background: window.pctColor(A.milestone.dayPct, 'time') }}></i></div>
              <div className="r" style={{ marginTop: 6, marginBottom: 0 }}><span>{A.milestone.start}</span><span>{A.milestone.end}</span></div>
            </div>
          </div>

          {showFilters &&
          <div className="fl">
              <window.MultiSelect label="Milestone" single value={fMilestone} onChange={setFMilestone}
            options={(A.filterOptions || {}).milestones || []} />
              <window.MultiSelect label="Label" value={fLabel} onChange={setFLabel}
            options={(A.filterOptions || {}).labels || []} />
              <window.MultiSelect label="Équipe" value={fTeam} onChange={setFTeam}
            options={(A.filterOptions || {}).teams || []} />
              <window.MultiSelect label="Utilisateur" value={fUser} onChange={setFUser}
            options={(A.filterOptions || {}).users || A.people.map((p) => p.name)} />
              <div className="fl-actions">
                {isCharts && <button className="btn btn-sm btn-outline-accent export" onClick={() => window.exportChartsHTML()}>{window.ICONS.download} Exporter en HTML</button>}
                <button className="btn btn-sm" onClick={clearFilters} disabled={!filtersActive} title="Réinitialise Label, Équipe et Utilisateur">{window.ICONS.eraser} Effacer les filtres</button>
              </div>
            </div>
          }

          {TabComp ? <TabComp theme={theme} setTheme={setTheme} appearance={appearance} tweaks={t} /> : <Stub name={TITLES[tab]} />}
        </main>

        {isCharts &&
        <window.TweaksPanel>
            <window.TweakSection label="Récap par super-groupe" />
            <window.TweakRadio label="Style" value={t.recapStyle} options={['cartes', 'tableau']}
          onChange={(v) => setTweak('recapStyle', v)} />
            <window.TweakSection label="Section Poids" />
            <window.TweakRadio label="Représentation" value={t.poidsStyle} options={['barres', 'colonnes', 'matrice']}
          onChange={(v) => setTweak('poidsStyle', v)} />
            <window.TweakSection label="Section Temps" />
            <window.TweakRadio label="Représentation" value={t.tempsStyle} options={['empile', 'phase', 'matrice']}
          onChange={(v) => setTweak('tempsStyle', v)} />
          </window.TweaksPanel>
        }
      </div>);

  };
})();