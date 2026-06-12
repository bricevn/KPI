// Options tab — apparence, régénération des données, configuration (formulaire).
(function () {
  const { useState } = React;
  const A = window.APP;
  const PROD_LABELS = ['Code In Progress', 'Code review', 'Code pre-review', 'QA Backlog', 'QA InProgress', 'To Fix', 'PO Validation', 'UI/UX To Do', 'UI/UX in progress', 'UI/UX Done'];
  const PHASES_DEFAULT = [['dev', '#2188ff'], ['review', '#8957e5'], ['qawait', '#b8800a'], ['qa', '#c79a06'], ['tofix', '#ec4899'], ['po', '#0f9e8e'], ['uiux', '#2dd4bf']];
  const PHASE_PALETTE = ['#2188ff', '#8957e5', '#b8800a', '#c79a06', '#ec4899', '#0f9e8e', '#2dd4bf', '#e0792e', '#d6336c', '#5f6b7a'];
  const LABEL_PHASE_DEFAULT = { 'Code In Progress': 'dev', 'Code review': 'review', 'Code pre-review': 'review', 'QA Backlog': 'qawait', 'QA InProgress': 'qa', 'To Fix': 'tofix', 'PO Validation': 'po', 'UI/UX To Do': 'uiux', 'UI/UX in progress': 'uiux', 'UI/UX Done': 'none' };
  const PROJECTS = [['Hypervisor Core', 4], ['Agenz Suite', 11], ['Telemetry', 7], ['Map Service', 19], ['Operator Desk', 23]];
  const ACCENTS = [['#2b7fff', 'Bleu'], ['#7A5AE0', 'Violet'], ['#0f9e8e', 'Teal'], ['#e0792e', 'Ambre'], ['#d6336c', 'Magenta']];
  const NUMFONTS = [['grotesk', 'Grotesk'], ['mono', 'Mono'], ['system', 'Système']];
  const DRILL_LAYOUTS = [['modal', 'Centré'], ['panel', 'Panneau'], ['full', 'Plein écran']];
  const PHASE_T = { none: 'opt.phNone', dev: 'opt.phDev', review: 'opt.phReview', qawait: 'opt.phQawait', qa: 'opt.phQa', tofix: 'opt.phTofix', po: 'opt.phPo', uiux: 'opt.phUiux' };
  const DRILL_T = { modal: 'opt.drillModal', panel: 'opt.drillPanel', full: 'opt.drillFull' };

  function Toggle({ on, onClick }) {
    return <button onClick={onClick} style={{ width: 42, height: 24, borderRadius: 999, border: 0, cursor: 'pointer', background: on ? 'var(--accent)' : 'var(--panel-3)', position: 'relative', transition: 'background .15s' }}>
      <span style={{ position: 'absolute', top: 3, left: on ? 21 : 3, width: 18, height: 18, borderRadius: '50%', background: '#fff', transition: 'left .15s' }}></span>
    </button>;
  }

  window.TabOptions = function TabOptions({ theme, setTheme, appearance }) {
    const { accent, setAccent, numFont, setNumFont, compact, setCompact, drillLayout, setDrillLayout } = appearance;
    const [labelPhase, setLabelPhase] = useState(() => ({ ...LABEL_PHASE_DEFAULT }));
    const [phases, setPhases] = useState(() => PHASES_DEFAULT.map(([id, color]) => ({ id, name: window.t(PHASE_T[id]), color })));
    const [openColor, setOpenColor] = useState(null);
    const [imported, setImported] = useState(() => new Set([4, 11]));
    const [showToken, setShowToken] = useState(false);
    const [selfSigned, setSelfSigned] = useState(false);
    const setPhase = (l, v) => setLabelPhase((m) => ({ ...m, [l]: v }));
    const toggleProject = (id) => setImported((s) => { const n = new Set(s); n.has(id) ? n.delete(id) : n.add(id); return n; });
    const phaseColor = (id) => id === 'none' ? '#5f6b7a' : ((phases.find((p) => p.id === id) || {}).color || '#5f6b7a');
    const renamePhase = (id, name) => setPhases((ps) => ps.map((p) => p.id === id ? { ...p, name } : p));
    const setPhaseColor = (id, color) => { setPhases((ps) => ps.map((p) => p.id === id ? { ...p, color } : p)); setOpenColor(null); };
    const addPhase = () => { const id = 'ph-' + Date.now(); setPhases((ps) => [...ps, { id, name: window.t('opt.newPhase'), color: PHASE_PALETTE[ps.length % PHASE_PALETTE.length] }]); };
    const removePhase = (id) => { setLabelPhase((m) => { const n = { ...m }; Object.keys(n).forEach((k) => { if (n[k] === id) n[k] = 'none'; }); return n; }); setPhases((ps) => ps.filter((p) => p.id !== id)); };
    const teamsRoles = { 'Core': [['antoine', 'lead'], ['brice', 'member'], ['carlo', 'member']], 'QA': [['denis', 'lead'], ['ash', 'member']], 'Front': [['kabbas', 'lead'], ['antoine', 'member']] };
    const selStyle = { border: '1px solid var(--line)', borderRadius: 9, background: 'var(--panel-2)', color: 'var(--ink)', padding: '9px 12px', font: '14px system-ui' };

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

        <div className="opt-sec">
          <h3>{window.t('opt.regen')}</h3>
          <p className="lead">{window.t('opt.regenLead', { date: A.meta.extracted })}</p>
          <div className="opt-row">
            <div className="lbl">{window.t('opt.scope')}<span>{window.t('opt.scopeSub')}</span></div>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <select style={selStyle}>
                {PROJECTS.map(([name, id]) => <option key={id}>{name}</option>)}
              </select>
              <select style={selStyle} defaultValue="Tout le projet">
                <option>{window.t('whole_project')}</option>
                <option>2026-R2</option>
                <option>2026-R1</option>
                <option>2026-R3</option>
              </select>
            </div>
            <button className="btn btn-primary">{window.ICONS.refresh} {window.t('opt.refresh')}</button>
          </div>
        </div>

        <div className="opt-sec">
          <h3>{window.t('opt.config')}</h3>
          <p className="lead">{window.t('opt.configLead', { setup: '/setup', file: 'appsettings.json' })}</p>

          <div className="opt-sub">{window.t('opt.connection')}</div>
          <div className="field-grid">
            <div className="field"><label>Base URL</label><input defaultValue="https://gitlab.obvious.tech" /></div>
            <div className="field"><label>{window.t('opt.timeout')}</label><input defaultValue="60" /></div>
            <div className="field" style={{ gridColumn: '1 / -1' }}><label>{window.t('opt.serviceToken')}</label>
              <div style={{ position: 'relative' }}>
                <input type={showToken ? 'text' : 'password'} defaultValue="glpat-xxxxxxxxxxxx" style={{ width: '100%', paddingRight: 38 }} />
                <button onClick={() => setShowToken((s) => !s)} style={{ position: 'absolute', right: 6, top: 6, border: 0, background: 'transparent', cursor: 'pointer', color: 'var(--ink-faint)', fontSize: 12 }}>{showToken ? window.t('opt.hide') : window.t('opt.show')}</button>
              </div>
            </div>
          </div>
          <div className="opt-row" style={{ borderTop: '1px solid var(--line-2)' }}>
            <div className="lbl">{window.t('opt.selfSigned')}<span>{window.t('opt.selfSignedSub')}</span></div>
            <Toggle on={selfSigned} onClick={() => setSelfSigned((s) => !s)} />
          </div>

          <div className="opt-sub">{window.t('opt.importedProjects')}</div>
          <div className="checklist">
            {PROJECTS.map(([name, id]) => <label key={id} className={imported.has(id) ? 'on' : ''} onClick={() => toggleProject(id)}><input type="checkbox" readOnly checked={imported.has(id)} style={{ pointerEvents: 'none' }} />{name} <span style={{ opacity: .6, fontFamily: 'var(--font-mono,monospace)', fontSize: 11 }}>#{id}</span></label>)}
          </div>

          <div className="opt-sub">{window.t('opt.prodPhases')} <span className="opt-prereq">{window.t('opt.prereq')}</span></div>
          <p className="opt-note">{window.t('opt.phasesEditNote')}</p>
          <div className="opt-phases">
            {phases.map((p) => (
              <div className="opt-phrow" key={p.id}>
                <div className="opt-swatchwrap">
                  <button className="opt-swatch" style={{ background: p.color }} onClick={() => setOpenColor(openColor === p.id ? null : p.id)} title={window.t('opt.accent')}></button>
                  {openColor === p.id &&
                  <div className="opt-pop">
                      {PHASE_PALETTE.map((c) => <button key={c} className={'opt-pc' + (c === p.color ? ' on' : '')} style={{ background: c }} onClick={() => setPhaseColor(p.id, c)}></button>)}
                    </div>}
                </div>
                <input className="opt-phname" value={p.name} onChange={(e) => renamePhase(p.id, e.target.value)} />
                <button className="opt-phx" onClick={() => removePhase(p.id)} title="×">×</button>
              </div>
            ))}
          </div>
          <button className="btn btn-sm" style={{ marginBottom: 14 }} onClick={addPhase}>+ {window.t('opt.addPhase')}</button>
          <div className="opt-sub">{window.t('opt.assocLabels')} <span style={{ fontWeight: 400, textTransform: 'none', letterSpacing: 0, color: 'var(--ink-faint)' }}>Prod::</span></div>
          <div className="opt-map">
            {PROD_LABELS.map((l) => (
              <div className="opt-maprow" key={l}>
                <span className="opt-dot" style={{ background: phaseColor(labelPhase[l] || 'none') }}></span>
                <span className="opt-mlabel">Prod::{l}</span>
                <select className="opt-mini" value={labelPhase[l] || 'none'} onChange={(e) => setPhase(l, e.target.value)}>
                  {[['none', window.t('opt.phNone')], ...phases.map((p) => [p.id, p.name])].map(([k, lbl]) => <option key={k} value={k}>{lbl}</option>)}
                </select>
              </div>
            ))}
          </div>

          <div className="opt-sub">{window.t('opt.teams')} <span style={{ fontWeight: 400, textTransform: 'none', letterSpacing: 0, color: 'var(--ink-faint)' }}>· {window.t('opt.teamsSub')}</span></div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 12 }}>
            {Object.entries(teamsRoles).map(([name, members]) =>
            <div key={name} style={{ border: '1px solid var(--line)', borderRadius: 12, padding: '12px 14px' }}>
                <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 8 }}>{name}</div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {members.map(([id, role]) => <span key={id} className="tag"><window.Avatar pid={id} size={18} />{(A.peopleById[id] || {}).name || id}<span className="opt-role">{role === 'lead' ? window.t('opt.lead') : window.t('opt.memberRole')}</span></span>)}
                </div>
              </div>
            )}
          </div>

          <div style={{ display: 'flex', gap: 10, marginTop: 20 }}>
            <button className="btn btn-primary btn-sm">{window.t('opt.addTeam')}</button>
            <div style={{ marginLeft: 'auto', display: 'flex', gap: 10 }}>
              <button className="btn btn-danger btn-sm">{window.t('opt.cancel')}</button>
              <button className="btn btn-ok btn-sm">{window.t('opt.save')}</button>
            </div>
          </div>
        </div>
      </div>);

  };
})();