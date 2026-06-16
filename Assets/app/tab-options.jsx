// Options tab — apparence, régénération des données, configuration (REFLET lecture seule de /setup).
// Les données de config (projets, phases, associations label→phase, équipes) viennent du payload réel
// window.__DATA__.setup (construit côté serveur depuis appsettings). L'édition se fait dans /setup.
(function () {
  const { useState } = React;
  const A = window.APP || {};
  const ACCENTS = [['#2b7fff', 'Bleu'], ['#7A5AE0', 'Violet'], ['#0f9e8e', 'Teal'], ['#e0792e', 'Ambre'], ['#d6336c', 'Magenta']];
  const NUMFONTS = [['grotesk', 'Grotesk'], ['mono', 'Mono'], ['system', 'Système']];
  const DRILL_LAYOUTS = [['modal', 'Centré'], ['panel', 'Panneau'], ['full', 'Plein écran']];
  const DRILL_T = { modal: 'opt.drillModal', panel: 'opt.drillPanel', full: 'opt.drillFull' };

  function Toggle({ on, onClick }) {
    return <button onClick={onClick} style={{ width: 42, height: 24, borderRadius: 999, border: 0, cursor: 'pointer', background: on ? 'var(--accent)' : 'var(--panel-3)', position: 'relative', transition: 'background .15s' }}>
      <span style={{ position: 'absolute', top: 3, left: on ? 21 : 3, width: 18, height: 18, borderRadius: '50%', background: '#fff', transition: 'left .15s' }}></span>
    </button>;
  }

  window.TabOptions = function TabOptions({ theme, setTheme, appearance }) {
    const { accent, setAccent, numFont, setNumFont, compact, setCompact, drillLayout, setDrillLayout } = appearance;

    // ---- config réelle (reflet de /setup), via le payload window.__DATA__.setup ----
    const S = (window.__DATA__ || {}).setup || {};
    const projects = S.projects || [];
    const periodsGlobal = S.periods || [];
    const lpGlobal = S.labelPhases || {};
    const pbp = S.periodsByProject || {};
    const lbp = S.labelPhasesByProject || {};
    const trackedLabels = S.trackedLabels || [];
    const allTeams = S.teams || [];
    const isAdmin = !!S.isAdmin;   // régénération + reconfiguration réservées aux admins (cf. /api/refresh, /setup)
    const milestones = (A.filterOptions || {}).milestones || [];
    const peopleById = A.peopleById || {};

    const [selProj, setSelProj] = useState(() => (projects[0] ? String(projects[0].id) : ''));
    const [regenMs, setRegenMs] = useState('');               // '' = tout le projet (toutes milestones)
    const [refreshState, setRefreshState] = useState('idle'); // idle | busy | done | err

    const sid = (pid) => String(pid);
    const periodsFor = (pid) => (pbp[sid(pid)] && pbp[sid(pid)].length ? pbp[sid(pid)] : periodsGlobal);
    const lpFor = (pid) => (lbp[sid(pid)] && Object.keys(lbp[sid(pid)]).length ? lbp[sid(pid)] : lpGlobal);
    const isPer = (pid) => !!(pbp[sid(pid)] && pbp[sid(pid)].length) || !!(lbp[sid(pid)] && Object.keys(lbp[sid(pid)]).length);
    const phaseColor = (periods, key) => key === 'none' ? '#5f6b7a' : ((periods.find((p) => p.key === key) || {}).color || '#5f6b7a');
    const phaseName = (periods, key) => key === 'none' ? window.t('opt.phNone') : ((periods.find((p) => p.key === key) || {}).name || key);
    // Équipes couvrant un projet : groupe d'équipe = namespace du projet OU ancêtre. Repli global si pas de groupes.
    const teamsFor = (proj) => {
      if (!proj) return allTeams;
      const pg = proj.group || '';
      const haveGroups = allTeams.some((t) => t.group);
      if (!haveGroups || !pg) return allTeams;
      return allTeams.filter((t) => t.group && (pg === t.group || pg.indexOf(t.group + '/') === 0));
    };
    const personName = (id) => (peopleById[id] || {}).name || id;

    const doRefresh = () => {
      setRefreshState('busy');
      fetch('/api/refresh', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(regenMs ? { milestones: [regenMs] } : {}) })
        .then((r) => setRefreshState(r.ok ? 'done' : 'err'))
        .catch(() => setRefreshState('err'));
    };

    const proj = projects.find((p) => String(p.id) === selProj) || projects[0] || null;
    const periods = proj ? periodsFor(proj.id) : periodsGlobal;
    const lp = proj ? lpFor(proj.id) : lpGlobal;
    const labelsToShow = trackedLabels.length ? trackedLabels : Object.keys(lp);
    const teams = teamsFor(proj);
    const perTag = proj && isPer(proj.id) ? proj.name : window.t('opt.globalTag');

    const selStyle = { border: '1px solid var(--line)', borderRadius: 9, background: 'var(--panel-2)', color: 'var(--ink)', padding: '9px 12px', font: '14px system-ui' };
    const subGrey = { fontWeight: 400, textTransform: 'none', letterSpacing: 0, color: 'var(--ink-faint)' };

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
              {ACCENTS.map(([c, name]) =>
              <button key={c} className={'swatch' + (accent === c ? ' on' : '')} title={name}
              style={{ background: c }} onClick={() => setAccent(c)}>{accent === c ? '✓' : ''}</button>
              )}
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
              {DRILL_LAYOUTS.map(([k]) => <button key={k} className={drillLayout === k ? 'on' : ''} onClick={() => setDrillLayout(k)}>{window.t(DRILL_T[k])}</button>)}
            </div>
          </div>
        </div>

        {isAdmin && (
        <div className="opt-sec">
          <h3>{window.t('opt.regen')}</h3>
          <p className="lead">{window.t('opt.regenLead', { date: (A.meta || {}).extracted || '—' })}</p>
          <div className="opt-row">
            <div className="lbl">{window.t('opt.scope')}<span>{window.t('opt.scopeSub')}</span></div>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <select style={selStyle} value={selProj} onChange={(e) => setSelProj(e.target.value)}>
                {projects.length ? projects.map((p) => <option key={p.id} value={String(p.id)}>{p.name}</option>) : <option value="">{window.t('opt.noProjects')}</option>}
              </select>
              <select style={selStyle} value={regenMs} onChange={(e) => setRegenMs(e.target.value)}>
                <option value="">{window.t('whole_project')}</option>
                {milestones.map((m) => <option key={m} value={m}>{m}</option>)}
              </select>
            </div>
            <button className="btn btn-primary" disabled={refreshState === 'busy'} onClick={doRefresh}>{window.ICONS.refresh} {window.t('opt.refresh')}</button>
          </div>
          {refreshState === 'done' && <p className="opt-note" style={{ color: 'var(--good,#2f9e44)' }}>{window.t('opt.refreshStarted')}</p>}
          {refreshState === 'err' && <p className="opt-note" style={{ color: 'var(--bad,#e5484d)' }}>{window.t('opt.refreshError')}</p>}
        </div>
        )}

        <div className="opt-sec">
          <h3>{window.t('opt.config')}</h3>
          <p className="lead">{window.t('opt.configLead', { setup: '/setup', file: 'appsettings.json' })}</p>

          <div className="opt-sub">{window.t('opt.importedProjects')}</div>
          <div className="checklist">
            {projects.length ? projects.map((p) =>
            <label key={p.id} className="on" style={{ cursor: 'default' }}><input type="checkbox" readOnly checked style={{ pointerEvents: 'none' }} />{p.name} <span style={{ opacity: .6, fontFamily: 'var(--font-mono,monospace)', fontSize: 11 }}>#{p.id}</span></label>
            ) : <p className="opt-note">{window.t('opt.noProjects')}</p>}
          </div>

          {projects.length > 1 &&
          <div className="opt-row">
            <div className="lbl">{window.t('opt.projectScope')}<span>{window.t('opt.projectScopeSub')}</span></div>
            <select style={selStyle} value={selProj} onChange={(e) => setSelProj(e.target.value)}>
              {projects.map((p) => <option key={p.id} value={String(p.id)}>{p.name}</option>)}
            </select>
          </div>}

          <div className="opt-sub">{window.t('opt.prodPhases')} <span className="opt-prereq">{perTag}</span></div>
          <p className="opt-note">{window.t('opt.phasesReadNote')}</p>
          <div className="opt-phases">
            {periods.length ? periods.map((p) => (
              <div className="opt-phrow" key={p.key}>
                <div className="opt-swatchwrap"><span className="opt-swatch" style={{ background: p.color, cursor: 'default', display: 'inline-block' }}></span></div>
                <span className="opt-phname" style={{ padding: '6px 4px' }}>{p.name}</span>
              </div>
            )) : <p className="opt-note">{window.t('opt.noPhases')}</p>}
          </div>

          <div className="opt-sub">{window.t('opt.assocLabels')} <span style={subGrey}>Prod::</span></div>
          <div className="opt-map">
            {labelsToShow.length ? labelsToShow.map((l) => {
              const key = lp[l] || 'none';
              return (
                <div className="opt-maprow" key={l}>
                  <span className="opt-dot" style={{ background: phaseColor(periods, key) }}></span>
                  <span className="opt-mlabel">{l}</span>
                  <span style={{ fontSize: 12, color: 'var(--ink-faint)', marginLeft: 'auto' }}>{phaseName(periods, key)}</span>
                </div>
              );
            }) : <p className="opt-note">{window.t('opt.noLabels')}</p>}
          </div>

          <div className="opt-sub">{window.t('opt.teams')} <span style={subGrey}>· {window.t('opt.teamsSub')}</span></div>
          {teams.length ?
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 12 }}>
            {teams.map((tm) =>
            <div key={tm.name} style={{ border: '1px solid var(--line)', borderRadius: 12, padding: '12px 14px' }}>
                <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 8 }}>{tm.name}</div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {(tm.members || []).map((id) => <span key={id} className="tag"><window.Avatar pid={id} size={18} />{personName(id)}</span>)}
                </div>
              </div>
            )}
          </div>
          : <p className="opt-note">{window.t('opt.noTeams')}</p>}

          {isAdmin &&
          <div style={{ marginTop: 20 }}>
            <a className="btn btn-primary btn-sm" href="/setup">{window.t('opt.reconfigure')}</a>
          </div>}
        </div>
      </div>);

  };
})();
