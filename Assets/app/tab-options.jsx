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
  const DRILL_LAYOUTS = [['modal', 'Centré'], ['panel', 'Panneau'], ['full', 'Plein écran']];
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
  const clonePhases = (arr) => (arr || []).map((p) => ({ key: p.key, name: p.name, color: p.color, timed: p.timed !== false }));
  const cloneTeams = (arr) => (arr || []).map((t) => ({ name: t.name, members: (t.members || []).slice(), lead: (t.members || [])[0] || null }));
  const orderTeam = (t) => ({ name: t.name, members: t.lead ? [t.lead].concat((t.members || []).filter((m) => m !== t.lead)) : (t.members || []).slice() });

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
    const [labelsCache, setLabelsCache] = useState({});
    const [openColor, setOpenColor] = useState(null);
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

    // ---- phases (globales) ----
    const addPhase = () => setPhases((ps) => ps.concat([{ key: 'ph' + Date.now().toString(36), name: window.t('opt.newPhase'), color: PALETTE[ps.length % PALETTE.length], timed: true }]));
    const removePhase = (key) => { setPhases((ps) => ps.filter((p) => p.key !== key)); setLpGlobal((m) => { const n = {}; Object.keys(m).forEach((l) => { n[l] = m[l] === key ? 'none' : m[l]; }); return n; }); };
    const renamePhase = (key, name) => setPhases((ps) => ps.map((p) => p.key === key ? Object.assign({}, p, { name }) : p));
    const recolorPhase = (key, color) => { setPhases((ps) => ps.map((p) => p.key === key ? Object.assign({}, p, { color }) : p)); setOpenColor(null); };
    const phaseColorOf = (key) => key === 'none' ? '#5f6b7a' : ((phases.find((p) => p.key === key) || {}).color || '#5f6b7a');

    const filterImport = (obj) => { const o = {}; Object.keys(obj || {}).forEach((k) => { if (importIds.indexOf(+k) >= 0) o[k] = obj[k]; }); return o; };
    const payload = () => ({
      projectIds: importIds,
      projects: projList.filter((p) => importIds.indexOf(p.id) >= 0).map((p) => ({ id: p.id, name: p.name, group: p.groupFull || p.group || '' })),
      periods: phases.map((p) => ({ key: p.key, name: p.name, color: p.color, timed: p.timed !== false })),
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

        {/* ---- Phases de production (globales) ---- */}
        <div className="opt-row" style={{ flexDirection: 'column', alignItems: 'stretch', gap: 12 }}>
          <div className="lbl">{window.t('opt.prodPhases')}<span>{window.t('opt.phasesEditNote')}</span></div>
          <div className="opt-phases">
            {phases.length ? phases.map((p) =>
            <div className="opt-phrow" key={p.key}>
              <div className="opt-swatchwrap">
                <button className="opt-swatch" style={{ background: p.color }} onClick={() => setOpenColor(openColor === p.key ? null : p.key)} title={window.t('opt.accent')}></button>
                {openColor === p.key &&
                <div className="opt-pop">
                  {PALETTE.map((c) => <button key={c} className={'opt-pc' + (c === p.color ? ' on' : '')} style={{ background: c }} onClick={() => recolorPhase(p.key, c)}></button>)}
                </div>}
              </div>
              <input className="opt-phname" value={p.name} onChange={(e) => renamePhase(p.key, e.target.value)} />
              <button className="opt-phx" onClick={() => removePhase(p.key)} title="×">×</button>
            </div>
            ) : <p className="opt-note">{window.t('opt.noPhases')}</p>}
          </div>
          <button className="btn btn-sm" style={{ alignSelf: 'flex-start' }} onClick={addPhase}>+ {window.t('opt.addPhase')}</button>
        </div>

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
        <div className="opt-sub">{window.t('opt.teams')} <span style={subGrey}>· {window.t('opt.teamsSub')}</span></div>
        <TeamsEditor teams={teams} setTeams={() => {}} readOnly={true} />
      </div>);
  }

  // ===================== CALCUL DU TEMPS (admin) =====================
  // Fenêtre de temps ouvré + anti-bruit des durées de phase (cycle). Persisté via
  // POST /api/options/worktime ; le recalcul s'applique au rechargement de la page.
  function WorkTimeEditor() {
    const W = (window.__DATA__ || {}).workTime || {};
    const [start, setStart] = useState(W.startHour != null ? W.startHour : 9);
    const [end, setEnd] = useState(W.endHour != null ? W.endHour : 19);
    const [daysOnly, setDaysOnly] = useState(W.workingDaysOnly !== false);
    const [holidays, setHolidays] = useState((W.holidays || []).join('\n'));
    const [noise, setNoise] = useState(W.minPhaseMinutes || 0);
    // Phases de « travail actif » (temps effectif) — cases à cocher parmi les phases chronométrées.
    // Repli si non configuré : dev/review/qa/tofix (intersecté avec les phases qui existent réellement).
    const allPhases = A.phases || [];
    const [effPhases, setEffPhases] = useState(() => {
      const cfg = (W.effectivePhases && W.effectivePhases.length) ? W.effectivePhases : ['dev', 'review', 'qa', 'tofix'];
      return allPhases.filter((p) => cfg.indexOf(p.key) >= 0).map((p) => p.key);
    });
    const toggleEff = (k) => setEffPhases((s) => s.indexOf(k) >= 0 ? s.filter((x) => x !== k) : s.concat([k]));
    const [st, setSt] = useState('idle'); // idle | busy | done | err
    const [err, setErr] = useState('');
    const save = () => {
      setSt('busy');setErr('');
      const body = {
        workStartHour: parseInt(start, 10) || 0,
        workEndHour: parseInt(end, 10) || 0,
        workingDaysOnly: daysOnly,
        holidays: String(holidays).split('\n').map((s) => s.trim()).filter(Boolean),
        minPhaseMinutes: parseInt(noise, 10) || 0,
        effectivePhases: effPhases
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
        <div className="opt-row" style={{ alignItems: 'flex-start' }}>
          <div className="lbl">{window.t('opt.holidays')}<span>{window.t('opt.holidaysSub')}</span></div>
          <textarea style={Object.assign({}, selStyle, { width: 220, height: 92, resize: 'vertical', fontFamily: 'var(--mono, monospace)', fontSize: 12, lineHeight: 1.6, padding: '8px 10px' })}
          placeholder={'2026-01-01\n2026-05-01'} value={holidays} onChange={(e) => setHolidays(e.target.value)} />
        </div>
        <div className="opt-row">
          <div className="lbl">{window.t('opt.noise')}<span>{window.t('opt.noiseSub')}</span></div>
          <input style={numStyle} type="number" min="0" max="1440" value={noise} onChange={(e) => setNoise(e.target.value)} />
          <span className="muted">min</span>
        </div>
        <div className="opt-row" style={{ alignItems: 'flex-start' }}>
          <div className="lbl">{window.t('opt.effectivePhases')}<span>{window.t('opt.effectivePhasesSub')}</span></div>
          {allPhases.length
            ? <div className="checklist">
                {allPhases.map((p) => { const on = effPhases.indexOf(p.key) >= 0; return (
                  <label key={p.key} className={on ? 'on' : ''}><input type="checkbox" checked={on} onChange={() => toggleEff(p.key)} />{p.name}</label>); })}
              </div>
            : <p className="opt-note">{window.t('opt.noPhases')}</p>}
        </div>
        <div className="opt-row">
          <div className="lbl"></div>
          <button className="btn btn-primary" disabled={st === 'busy'} onClick={save}>{window.t('opt.save')}</button>
        </div>
        {st === 'done' && <p className="opt-note" style={{ color: 'var(--c-good,#2f9e44)' }}>{window.t('opt.wtSaved')} <button className="btn btn-sm" style={{ marginLeft: 8 }} onClick={() => window.location.reload()}>{window.t('opt.reload')}</button></p>}
        {st === 'err' && <p className="opt-note" style={{ color: 'var(--c-bad,#e5484d)' }}>{err || window.t('opt.refreshError')}</p>}
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
              {DRILL_LAYOUTS.map(([k]) => <button key={k} className={drillLayout === k ? 'on' : ''} onClick={() => setDrillLayout(k)}>{window.t(DRILL_T[k])}</button>)}
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

        {isAdmin && <WorkTimeEditor />}

        {isAdmin ? <AdminConfigEditor S={S} /> : <ReadOnlyConfig S={S} />}
      </div>);
  };
})();
