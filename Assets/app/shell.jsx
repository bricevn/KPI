// Shell — sidebar nav, header, global filters, tab routing, theme toggle.
(function () {
  const { useState, useEffect } = React;
  const NAV_IDS = ['dashboard', 'charts', 'anomalies', 'issues', 'calendar', 'velocity', 'comparison'];

  function Stub({ name }) {return <div className="empty">Onglet « {name} » — à venir</div>;}

  // App logo mark — ascending bars (matches the login page).
  const BrandLogo = () =>
  <svg width="21" height="21" viewBox="0 0 24 24" aria-hidden="true">
      <rect x="3" y="13" width="4.2" height="7" rx="1.4" fill="#fff" opacity="0.82" />
      <rect x="9.9" y="9" width="4.2" height="11" rx="1.4" fill="#fff" opacity="0.92" />
      <rect x="16.8" y="4.5" width="4.2" height="15.5" rx="1.4" fill="#fff" />
      <path d="M4 8.5 L11 6 L19 2.5" fill="none" stroke="#fff" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" opacity="0.9" />
      <circle cx="19" cy="2.5" r="1.7" fill="#fff" />
    </svg>;
  const LOGOUT_ICON = <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" /><path d="M16 17l5-5-5-5M21 12H9" /></svg>;

  const NUM_FONTS = { grotesk: "'Space Grotesk'", mono: "'IBM Plex Mono'", system: "system-ui" };
  const load = (k, d) => {try {const v = localStorage.getItem(k);return v == null ? d : v;} catch (e) {return d;}};

  // ---- i18n : le dictionnaire + window.t vivent dans i18n.js (chargé avant les .jsx). ----
  // Ici on ne garde que la liste des langues exposée au sélecteur d'Options.
  const LANGS = typeof window !== 'undefined' && window.__LANGS__ || [['fr', 'Français'], ['en', 'English']];

  const CHART_TWEAKS = /*EDITMODE-BEGIN*/{
    "recapStyle": "cartes",
    "poidsStyle": "barres",
    "tempsStyle": "empile"
  } /*EDITMODE-END*/;

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
    const [lang, setLang] = useState(() => window.__LANG__ || load('app-lang', 'fr')); // serveur (cookie) prioritaire
    // Filtres : options et valeurs par défaut DÉRIVÉES des données réelles (A.filterOptions), pas en dur.
    const [fProject, setFProject] = useState(() => {try {const ps = (window.APP.filterOptions || {}).projects || [];return ps.length ? [ps[0]] : [];} catch (e) {return [];}});
    const [fMilestone, setFMilestone] = useState([window.t('whole_project')]); // « Tout le projet » = toutes les milestones
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
    useEffect(() => {try {localStorage.setItem('app-lang', lang);} catch (e) {}}, [lang]);
    window.__drillLayout = drillLayout;
    window.__LANG__ = lang;

    const appearance = { accent, setAccent, numFont, setNumFont, compact, setCompact, drillLayout, setDrillLayout, lang, setLang, langs: LANGS };

    // Identité affichée sous la marque : compte connecté (/api/me), jamais de nom en dur.
    // Repli silencieux (harness / API indisponible) : ligne vide.
    const [me, setMe] = React.useState(null);
    React.useEffect(() => {
      let dead = false;
      fetch('/api/me').then((r) => r.json()).then((j) => {if (!dead && j && j.authenticated) setMe(j);}).catch(() => {});
      return () => {dead = true;};
    }, []);
    const meLine = me ? ((me.role === 'admin' ? 'Admin · ' : '') + (me.displayName || me.login || '')) : '';

    const resolved = theme === 'auto' ? window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light' : theme;

    const TabComp = {
      dashboard: window.TabDashboard, charts: window.TabCharts, comparison: window.TabComparison, anomalies: window.TabAnomalies,
      issues: window.TabIssues, calendar: window.TabCalendar, velocity: window.TabVelocity, options: window.TabOptions,
      pageeditor: window.TabPageEditor
    }[tab];
    const isAdmin = !!(((window.__DATA__) || {}).setup || {}).isAdmin;

    // Pages MODULAIRES : pages PARTAGÉES (config Dashboard.Pages → A.pages) + pages PERSO de l'utilisateur
    // (window.__USER_PAGES__, injectées par compte). Dédoublonnées par id (la perso prime → un utilisateur
    // peut surcharger une page partagée), triées par nav.order. Vide ⇒ nav = onglets historiques.
    const okPage = (p) => p && p.id && p.kind === 'modular';
    const _byId = {};
    (A.pages || []).filter(okPage).forEach((p) => { _byId[p.id] = p; });
    ((window.__USER_PAGES__) || []).filter(okPage).forEach((p) => { _byId[p.id] = p; });
    const PAGES = Object.keys(_byId).map((k) => _byId[k])
      .sort((a, b) => ((a.nav && a.nav.order) || 100) - ((b.nav && b.nav.order) || 100));
    const pageDef = PAGES.find((p) => p.id === tab);
    const navTitle = pageDef ? ((pageDef.nav && (pageDef.nav.labelKey ? window.t(pageDef.nav.labelKey) : pageDef.nav.label)) || pageDef.id)
      : tab === 'pageeditor' ? 'Éditeur de pages' : window.t('nav_' + tab);
    const pageCtx = { t: window.t, icon: (k) => (window.ICONS ? window.ICONS[k] : null), lang, appearance };

    const showFilters = tab !== 'options' && tab !== 'pageeditor' && (!pageDef || (pageDef.nav && pageDef.nav.showFilters !== false));
    const isCharts = tab === 'charts';
    const filtersActive = fLabel.length > 0 || fTeam.length > 0 || fUser.length > 0;
    const clearFilters = () => {setFLabel([]);setFTeam([]);setFUser([]);};

    // Filtre Équipe SYNCHRONISÉ avec le filtre Utilisateur : cocher une équipe coche ses membres ;
    // la décocher les retire, SAUF ceux couverts par une autre équipe encore sélectionnée.
    // Les membres sont mappés sur les entrées CANONIQUES du filtre Utilisateur (casse des usernames).
    const onTeamChange = (next) => {
      const teams = (window.__DATA__ || {}).teams || {}; // nom d'équipe → [usernames]
      const userOpts = (A.filterOptions || {}).users || A.people.map((p) => p.name);
      const canon = (u) => userOpts.find((x) => String(x).toLowerCase() === String(u).toLowerCase()) || u;
      const added = next.filter((tn) => fTeam.indexOf(tn) < 0);
      const removed = fTeam.filter((tn) => next.indexOf(tn) < 0);
      let users = fUser.slice();
      added.forEach((tn) => (teams[tn] || []).forEach((u) => {const c = canon(u);if (users.indexOf(c) < 0) users.push(c);}));
      const still = {};
      next.forEach((tn) => (teams[tn] || []).forEach((u) => {still[canon(u)] = 1;}));
      removed.forEach((tn) => (teams[tn] || []).forEach((u) => {const c = canon(u);if (!still[c]) users = users.filter((x) => x !== c);}));
      setFTeam(next);
      setFUser(users);
    };

    const rootStyle = {
      '--accent': accent, '--accent-2': accent,
      '--accent-soft': `color-mix(in srgb, ${accent} 15%, transparent)`,
      // Pont charte : --accent-hue pilote --color-accent (et --color-accent-soft dérivé) des tokens.
      '--accent-hue': accent,
      '--disp-font': NUM_FONTS[numFont] || NUM_FONTS.grotesk
    };
    // Couleurs de phase pilotées par la config (Export.Periods) : surcharge les --p-<key> du CSS.
    (A.periods || []).forEach((p) => {if (p.key && p.color) rootStyle['--p-' + p.key] = p.color;});

    // Filtrage réel : on ne garde de fMilestone que les VRAIES milestones (le sentinel « Tout le projet »
    // n'en est pas une → []=toutes). Reconstruit window.APP EN PLACE (mémoïsé par signature).
    const realMs = fMilestone.filter((m) => ((A.filterOptions || {}).milestones || []).indexOf(m) >= 0);
    if (window.__applyFilters) window.__applyFilters({ milestones: realMs, labels: fLabel, teams: fTeam, users: fUser });

    return (
      <div className={'app kpi-root' + (compact ? ' compact' : '')} data-theme={resolved} style={rootStyle}>
        <aside className={'sb' + (sbCollapsed ? ' collapsed' : '')}>
          <div className="sb-brand">
            <div className="sb-mark"><BrandLogo /></div>
            <div><div className="nm">KPI</div><div className="sub">{meLine}</div></div>
          </div>
          <div className="sb-h">{window.t('pilotage')}</div>
          <nav className="sb-nav">
            {NAV_IDS.map((id) =>
            <button key={id} className={'sb-item' + (tab === id ? ' on' : '')} onClick={() => setTab(id)}>
                {window.ICONS[id]}<span>{window.t('nav_' + id)}</span>
                {id === 'anomalies' && <span className="badge">{(A.tabs.find((t) => t.id === id) || {}).count}</span>}
                {id === 'issues' && <span className="cnt">{(A.tabs.find((t) => t.id === id) || {}).count}</span>}
              </button>
            )}
            {PAGES.map((p) =>
            <button key={p.id} className={'sb-item' + (tab === p.id ? ' on' : '')} onClick={() => setTab(p.id)}>
                {(window.ICONS && (window.ICONS[p.nav && p.nav.icon] || window.ICONS.dashboard))}
                <span>{(p.nav && (p.nav.labelKey ? window.t(p.nav.labelKey) : p.nav.label)) || p.id}</span>
              </button>
            )}
          </nav>
          <div className="sb-sp"></div>
          <button className={'sb-item' + (tab === 'pageeditor' ? ' on' : '')} onClick={() => setTab('pageeditor')} title="Éditeur de pages">{window.ICONS.dashboard}<span>Éditeur de pages</span></button>
          <button className={'sb-item' + (tab === 'options' ? ' on' : '')} onClick={() => setTab('options')} title={window.t('nav_options')}>{window.ICONS.options}<span>{window.t('nav_options')}</span></button>
          <button className="sb-item sb-logout" onClick={() => {window.location.href = '/logout';}} title={window.t('logout')}>{LOGOUT_ICON}<span>{window.t('logout')}</span></button>
          <button className="sb-collapse" onClick={() => setSbCollapsed((c) => !c)} title={sbCollapsed ? window.t('expandT') : window.t('reduceT')}>
            <span className="sb-collapse-ic">{window.ICONS.chevron}</span><span>{window.t('reduce')}</span>
          </button>
        </aside>

        <main className="main">
          <div className="hd">
            <div>
              <h1 className="disp">{realMs.length === 0 ? window.t('whole_project') : realMs.length === 1 ? realMs[0] : realMs.length <= 3 ? realMs.join(' · ') : realMs.length + ' ' + window.t('f_milestone').toLowerCase()} · {navTitle}</h1>
              <div className="meta">{A.meta.project} · {window.t('hd_gen')} {A.meta.generated} · {A.totals.issues} {window.t('issues')}</div>
            </div>
            <div className="hd-ms">
              <div className="r"><span>{window.t('hd_progress')}</span><b>{A.milestone.dayPct}%</b></div>
              <div className="track"><i style={{ width: A.milestone.dayPct + '%', background: window.pctColor(A.milestone.dayPct, 'time') }}></i></div>
              <div className="r" style={{ marginTop: 6, marginBottom: 0 }}><span>{A.milestone.start}</span><span>{A.milestone.end}</span></div>
            </div>
          </div>

          {showFilters &&
          <div className="fl">
              <window.MultiSelect label={window.t('f_project')} value={fProject} onChange={setFProject}
            options={(A.filterOptions || {}).projects || []} />
              <window.MultiSelect label={window.t('f_milestone')} value={fMilestone} onChange={(next) => {
            const WP = window.t('whole_project');
            const addedWP = next.indexOf(WP) >= 0 && fMilestone.indexOf(WP) < 0;
            if (addedWP || next.length === 0) {setFMilestone([WP]);return;} // « Tout le projet » exclusif / vide = tout
            setFMilestone(next.filter((m) => m !== WP)); // une vraie milestone retire le sentinel
          }}
            options={[window.t('whole_project')].concat((A.filterOptions || {}).milestones || [])} />
              <window.MultiSelect label={window.t('f_label')} value={fLabel} onChange={setFLabel}
            options={(A.filterOptions || {}).labels || []} />
              <window.MultiSelect label={window.t('f_team')} value={fTeam} onChange={onTeamChange}
            options={(A.filterOptions || {}).teams || []} />
              <window.MultiSelect label={window.t('f_user')} value={fUser} onChange={setFUser}
            options={(A.filterOptions || {}).users || A.people.map((p) => p.name)} />
              <div className="fl-actions">
                {isCharts && <button className="btn btn-sm btn-outline-accent export" onClick={() => window.exportChartsHTML(t, { teams: fTeam, users: fUser })}>{window.ICONS.download} {window.t('export')}</button>}
                <button className="btn btn-sm" onClick={clearFilters} disabled={!filtersActive} title={window.t('clearT')}>{window.ICONS.eraser} {window.t('clear')}</button>
              </div>
            </div>
          }

          {pageDef
          ? <window.PageRenderer page={pageDef} ctx={pageCtx} />
          : TabComp ? <TabComp theme={theme} setTheme={setTheme} appearance={appearance} tweaks={t} lang={lang} /> : <Stub name={navTitle} />}
        </main>

        {isCharts &&
        <window.TweaksPanel>
            <window.TweakSection label="Récap par super-groupe" />
            <window.TweakRadio label="Style" value={t.recapStyle} options={['cartes', 'tableau']}
          onChange={(v) => setTweak('recapStyle', v)} />
          </window.TweaksPanel>
        }
      </div>);

  };
})();