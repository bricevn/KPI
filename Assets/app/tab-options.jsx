// Options tab — apparence, régénération, et CONFIGURATION.
// Admin : éditeur (projets / phases / labels / équipes), porte la logique de /setup en React, sauve via /api/options.
// Non-admin : reflet lecture seule de la config (window.__DATA__.setup).
(function () {
  const { useState, useEffect } = React;
  const A = window.APP || {};
  const ACCENTS = [['#2b7fff', 'Bleu'], ['#7A5AE0', 'Violet'], ['#0f9e8e', 'Teal'], ['#e0792e', 'Ambre'], ['#d6336c', 'Magenta']];
  const NUMFONTS = [['grotesk', 'Grotesk'], ['mono', 'Mono'], ['system', 'Système']];
  const DRILL_LAYOUTS = [['modal', 'Centré'], ['panel', 'Panneau'], ['full', 'Plein écran']];
  const DRILL_T = { modal: 'opt.drillModal', panel: 'opt.drillPanel', full: 'opt.drillFull' };
  const PALETTE = ['#2188ff', '#0ea5e9', '#06b6d4', '#2dd4bf', '#0f9e8e', '#22c55e', '#84cc16', '#eab308', '#c79a06', '#e0792e', '#f97316', '#ef4444', '#d6336c', '#ec4899', '#d946ef', '#a855f7', '#8957e5', '#6366f1', '#64748b', '#94a3b8'];

  // Reprise de la suggestion de phase du setup (clé proposée seulement si elle existe dans les phases courantes).
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
  // Équipes : modèle d'édition local { name, members:[username], lead } — lead = 1er membre (convention de persistance).
  const cloneTeams = (arr) => (arr || []).map((t) => ({ name: t.name, members: (t.members || []).slice(), lead: (t.members || [])[0] || null }));
  // Pour le POST : remet le lead en tête de la liste des membres (aucun champ « lead » dans appsettings).
  const orderTeam = (t) => ({ name: t.name, members: t.lead ? [t.lead].concat((t.members || []).filter((m) => m !== t.lead)) : (t.members || []).slice() });

  function Toggle({ on, onClick }) {
    return <button onClick={onClick} style={{ width: 42, height: 24, borderRadius: 999, border: 0, cursor: 'pointer', background: on ? 'var(--accent)' : 'var(--panel-3)', position: 'relative', transition: 'background .15s' }}>
      <span style={{ position: 'absolute', top: 3, left: on ? 21 : 3, width: 18, height: 18, borderRadius: '50%', background: '#fff', transition: 'left .15s' }}></span>
    </button>;
  }

  const selStyle = { border: '1px solid var(--line)', borderRadius: 9, background: 'var(--panel-2)', color: 'var(--ink)', padding: '9px 12px', font: '14px system-ui' };
  const subGrey = { fontWeight: 400, textTransform: 'none', letterSpacing: 0, color: 'var(--ink-faint)' };

  // ===================== ÉDITEUR D'ÉQUIPES (admin, contrôlé) =====================
  // État porté par le parent (AdminConfigEditor) pour être inclus dans le POST /api/options.
  // Par membre : lead (★), glisser-déposer entre groupes, retirer. Groupes : ajouter / renommer / supprimer.
  function TeamsEditor({ teams, setTeams }) {
    const peopleById = A.peopleById || {};
    const allPeople = A.people || Object.keys(peopleById).map((id) => peopleById[id]);
    const personName = (id) => (peopleById[id] || {}).name || id;
    const [dragOver, setDragOver] = useState(null);
    const dragRef = React.useRef(null); // { from, id }
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

  // Équipes en lecture seule (non-admin) — lead = 1er membre, mis en évidence (contour jaune + badge).
  function TeamsReadOnly({ teams }) {
    const peopleById = A.peopleById || {};
    const personName = (id) => (peopleById[id] || {}).name || id;
    if (!teams.length) return <p className="opt-note">{window.t('opt.noTeams')}</p>;
    return (
      <div className="opt-teams">
        {teams.map((tm) => {
          const members = tm.members || [];
          const lead = members[0] || null;
          return (
            <div key={tm.name} className="opt-team">
              <div className="opt-team-hd"><span style={{ fontWeight: 700, fontSize: 13 }}>{tm.name}</span></div>
              <div className="opt-team-members">
                {members.map((id) =>
                <div key={id} className={'opt-member' + (lead === id ? ' lead' : '')} style={{ cursor: 'default' }}>
                  <window.Avatar pid={id} size={20} />
                  <span className="opt-member-nm">{personName(id)}</span>
                  {lead === id && <span className="opt-lead-tag">{window.t('opt.lead')}</span>}
                </div>
                )}
              </div>
            </div>);
        })}
      </div>);
  }

  // ===================== ÉDITEUR ADMIN =====================
  function AdminConfigEditor({ S }) {
    const initImport = (S.projects || []).map((p) => p.id);
    const [allProjects, setAllProjects] = useState(null);                 // null = chargement ; [] = vide/erreur
    const [importIds, setImportIds] = useState(initImport);
    const [phScope, setPhScope] = useState('all');                        // all | per
    const [lbScope, setLbScope] = useState('all');
    const [activeProj, setActiveProj] = useState(initImport[0] != null ? initImport[0] : null);
    const [phGlobal, setPhGlobal] = useState(() => clonePhases(S.periods));
    const [phByProj, setPhByProj] = useState(() => mapClone(S.periodsByProject || {}, clonePhases));
    const [lpGlobal, setLpGlobal] = useState(() => Object.assign({}, S.labelPhases || {}));
    const [lpByProj, setLpByProj] = useState(() => mapClone(S.labelPhasesByProject || {}, (m) => Object.assign({}, m)));
    const [teams, setTeams] = useState(() => cloneTeams(S.teams));
    const [labelsCache, setLabelsCache] = useState({});
    const [openColor, setOpenColor] = useState(null);
    const [saving, setSaving] = useState('idle');                         // idle | busy | done | err
    const [err, setErr] = useState('');

    useEffect(() => {
      fetch('/api/options/projects').then((r) => r.json()).then((j) => setAllProjects(j && j.ok ? (j.projects || []) : [])).catch(() => setAllProjects([]));
    }, []);

    const projList = allProjects || [];
    const projById = (id) => projList.find((p) => p.id === id) || (S.projects || []).find((p) => p.id === id) || null;
    const projName = (id) => (projById(id) || {}).name || ('#' + id);
    const importedProjects = () => projList.filter((p) => importIds.indexOf(p.id) >= 0);
    const ap = (activeProj != null && importIds.indexOf(activeProj) >= 0) ? activeProj : (importIds.length ? importIds[0] : null);

    const toggleProj = (id) => setImportIds((s) => s.indexOf(id) >= 0 ? s.filter((x) => x !== id) : s.concat([id]));
    const toggleAll = () => setImportIds((s) => s.length === projList.length ? [] : projList.map((p) => p.id));

    // ---- phases (scope courant) ----
    const phaseList = () => phScope === 'all' ? phGlobal : (phByProj[ap] || phGlobal);
    const commitPhases = (next) => { if (phScope === 'all') setPhGlobal(next); else setPhByProj((prev) => Object.assign({}, prev, { [ap]: next })); };
    const addPhase = () => { const list = phaseList(); commitPhases(list.concat([{ key: 'ph' + Date.now().toString(36), name: window.t('opt.newPhase'), color: PALETTE[list.length % PALETTE.length], timed: true }])); };
    const renamePhase = (key, name) => commitPhases(phaseList().map((p) => p.key === key ? Object.assign({}, p, { name }) : p));
    const recolorPhase = (key, color) => { commitPhases(phaseList().map((p) => p.key === key ? Object.assign({}, p, { color }) : p)); setOpenColor(null); };
    const removePhase = (key) => {
      commitPhases(phaseList().filter((p) => p.key !== key));
      // Cascade dans la MÊME portée que la phase supprimée (phScope) — pas la portée des labels (indépendante),
      // sinon supprimer une phase par-projet réécrirait la map de labels globale.
      const clear = (m) => { const n = {}; Object.keys(m).forEach((l) => { n[l] = m[l] === key ? 'none' : m[l]; }); return n; };
      if (phScope === 'all') setLpGlobal(clear);
      else if (ap != null) setLpByProj((prev) => Object.assign({}, prev, { [ap]: clear(prev[ap] || {}) }));
    };

    // ---- associations label→phase (scope courant) ----
    const mapObj = () => lbScope === 'all' ? lpGlobal : (lpByProj[ap] || lpGlobal);
    const commitMap = (next) => { if (lbScope === 'all') setLpGlobal(next); else setLpByProj((prev) => Object.assign({}, prev, { [ap]: next })); };
    const setMap = (label, val) => commitMap(Object.assign({}, mapObj(), { [label]: val }));
    // Les options de phase pour les labels suivent le scope LABEL (par-projet → phases du projet sinon globales).
    const labelPhases = () => lbScope === 'all' ? phGlobal : (phByProj[ap] || phGlobal);
    const phaseColor = (list, key) => key === 'none' ? '#5f6b7a' : ((list.find((p) => p.key === key) || {}).color || '#5f6b7a');

    // ---- labels du périmètre courant (fetch) ----
    const labelsKey = lbScope === 'all' ? importIds.slice().sort((a, b) => a - b).join(',') : String(ap || '');
    useEffect(() => {
      if (!labelsKey || labelsCache[labelsKey] !== undefined) return;
      const ids = lbScope === 'all' ? importIds.join(',') : String(ap || '');
      if (!ids) { setLabelsCache((prev) => Object.assign({}, prev, { [labelsKey]: [] })); return; }
      fetch('/api/options/labels?projectIds=' + encodeURIComponent(ids)).then((r) => r.json())
        .then((j) => setLabelsCache((prev) => Object.assign({}, prev, { [labelsKey]: (j && j.ok ? (j.labels || []) : []) })))
        .catch(() => setLabelsCache((prev) => Object.assign({}, prev, { [labelsKey]: [] })));
    }, [labelsKey]);
    const curLabels = labelsCache[labelsKey];

    // Pré-remplit les associations MANQUANTES avec la suggestion (comme /setup), pour qu'elles soient
    // effectivement enregistrées même si l'admin ne touche pas chaque select. Ne s'exécute qu'une fois
    // par (portée, projet) : une fois toutes les clés posées, plus rien à changer (pas de boucle).
    useEffect(() => {
      if (!curLabels || !curLabels.length) return;
      const keys = labelPhases().map((p) => p.key);
      const m = mapObj(); let changed = false; const n = Object.assign({}, m);
      curLabels.forEach((l) => { if (n[l] == null) { n[l] = guessPhase(l, keys); changed = true; } });
      if (changed) commitMap(n);
    }, [curLabels, lbScope, ap]);

    const payload = () => {
      const out = {
        projectIds: importIds,
        projects: importedProjects().map((p) => ({ id: p.id, name: p.name, group: p.groupFull || p.group || '' })),
        periods: phGlobal.map((p) => ({ key: p.key, name: p.name, color: p.color, timed: p.timed !== false })),
        labelPhases: lpGlobal,
        // Toujours renvoyer les overrides par-projet DEPUIS L'ÉTAT (le toggle n'est qu'un mode d'édition) :
        // passer en « Pour tous » ne doit PAS effacer les phases/labels par-projet déjà enregistrés.
        // Un projet retiré (filterImport) est exclu → ses overrides sont nettoyés côté serveur.
        periodsByProject: mapClone(filterImport(phByProj), (a) => a.map((p) => ({ key: p.key, name: p.name, color: p.color, timed: p.timed !== false }))),
        labelPhasesByProject: mapClone(filterImport(lpByProj), (m) => Object.assign({}, m)),
        // Équipes : lead remis en tête de liste (persisté comme 1er membre). Groupes vides ignorés côté serveur.
        teams: teams.map(orderTeam),
        refetch: !sameSet(importIds, initImport),
      };
      return out;
    };
    const filterImport = (obj) => { const o = {}; Object.keys(obj || {}).forEach((k) => { if (importIds.indexOf(+k) >= 0) o[k] = obj[k]; }); return o; };

    const doSave = () => {
      if (allProjects === null) return; // liste des projets pas encore chargée → ne pas écraser les métadonnées
      if (!importIds.length) { setSaving('err'); setErr(window.t('opt.noProjects')); return; }
      setSaving('busy'); setErr('');
      fetch('/api/options', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload()) })
        .then((r) => r.json()).then((j) => {
          if (j && j.ok) { setSaving('done'); setTimeout(() => window.location.reload(), j.refetch ? 1300 : 500); }
          else { setSaving('err'); setErr((j && j.error) || window.t('opt.saveError')); }
        }).catch(() => { setSaving('err'); setErr(window.t('opt.saveError')); });
    };

    const ScopeSeg = ({ scope, set }) => (
      <div className="seg-lg" style={{ marginBottom: 12 }}>
        <button className={scope === 'all' ? 'on' : ''} onClick={() => set('all')}>{window.t('opt.scopeGlobal')}</button>
        <button className={scope === 'per' ? 'on' : ''} onClick={() => set('per')}>{window.t('opt.scopePer')}</button>
      </div>);
    const ProjTabs = () => (
      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 12 }}>
        {importedProjects().map((p) => <button key={p.id} className={'btn btn-sm' + (ap === p.id ? ' btn-primary' : '')} onClick={() => setActiveProj(p.id)}>{p.name}</button>)}
      </div>);

    const phases = phaseList();
    const lmap = mapObj();
    const lphases = labelPhases();

    return (
      <React.Fragment>
        {/* ---- Projets ---- */}
        <div className="opt-sec">
          <h3>{window.t('opt.editProjects')}</h3>
          <p className="lead">{window.t('opt.editProjectsLead')}</p>
          {allProjects === null
            ? <p className="opt-note">{window.t('opt.loadingProjects')}</p>
            : (projList.length === 0
              ? <p className="opt-note" style={{ color: 'var(--bad,#e5484d)' }}>{window.t('opt.projectsError')} <a href="/setup">/setup</a></p>
              : <React.Fragment>
                <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 8 }}>
                  <button className="btn btn-sm" onClick={toggleAll}>{importIds.length === projList.length ? window.t('opt.deselectAll') : window.t('opt.selectAll')} ({importIds.length}/{projList.length})</button>
                </div>
                <div className="checklist">
                  {projList.map((p) => <label key={p.id} className={importIds.indexOf(p.id) >= 0 ? 'on' : ''} onClick={() => toggleProj(p.id)}><input type="checkbox" readOnly checked={importIds.indexOf(p.id) >= 0} style={{ pointerEvents: 'none' }} />{p.name} <span style={{ opacity: .6, fontFamily: 'var(--font-mono,monospace)', fontSize: 11 }}>#{p.id}</span></label>)}
                </div>
              </React.Fragment>)}
        </div>

        {/* ---- Phases ---- */}
        <div className="opt-sec">
          <h3>{window.t('opt.prodPhases')}</h3>
          <p className="lead">{window.t('opt.phasesEditNote')}</p>
          <ScopeSeg scope={phScope} set={setPhScope} />
          {phScope === 'per' && (importIds.length ? <ProjTabs /> : <p className="opt-note">{window.t('opt.noProjects')}</p>)}
          {(phScope === 'all' || ap != null) &&
          <React.Fragment>
            <div className="opt-phases">
              {phases.map((p) => (
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
              ))}
            </div>
            <button className="btn btn-sm" onClick={addPhase}>+ {window.t('opt.addPhase')}</button>
          </React.Fragment>}
        </div>

        {/* ---- Associations labels → phases ---- */}
        <div className="opt-sec">
          <h3>{window.t('opt.assocLabels')}</h3>
          <p className="lead">{window.t('opt.assocLabelsLead')}</p>
          <ScopeSeg scope={lbScope} set={setLbScope} />
          {lbScope === 'per' && (importIds.length ? <ProjTabs /> : <p className="opt-note">{window.t('opt.noProjects')}</p>)}
          {curLabels === undefined
            ? <p className="opt-note">{window.t('opt.loadingLabels')}</p>
            : (curLabels.length === 0
              ? <p className="opt-note">{window.t('opt.noLabels')}</p>
              : <div className="opt-map">
                {curLabels.map((l) => {
                  const key = lmap[l] != null ? lmap[l] : guessPhase(l, lphases.map((p) => p.key));
                  return (
                    <div className="opt-maprow" key={l}>
                      <span className="opt-dot" style={{ background: phaseColor(lphases, key) }}></span>
                      <span className="opt-mlabel">{l}</span>
                      <select className="opt-mini" value={key} onChange={(e) => setMap(l, e.target.value)}>
                        <option value="none">{window.t('opt.phNone')}</option>
                        {lphases.map((p) => <option key={p.key} value={p.key}>{p.name}</option>)}
                      </select>
                    </div>
                  );
                })}
              </div>)}
        </div>

        {/* ---- Équipes ---- */}
        <div className="opt-sec">
          <h3>{window.t('opt.teams')}</h3>
          <p className="lead">{window.t('opt.teamsSub')}</p>
          <TeamsEditor teams={teams} setTeams={setTeams} />
        </div>

        {/* ---- Barre d'actions ---- */}
        <div className="opt-sec" style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <button className="btn btn-primary" disabled={saving === 'busy' || allProjects === null} onClick={doSave}>{saving === 'busy' ? window.t('opt.saving') : window.t('opt.save')}</button>
          <a className="btn btn-sm" href="/setup">{window.t('opt.advancedSetup')}</a>
          {saving === 'done' && <span style={{ color: 'var(--good,#2f9e44)', fontSize: 13 }}>✓ {window.t('opt.saved')}</span>}
          {saving === 'err' && <span style={{ color: 'var(--bad,#e5484d)', fontSize: 13 }}>{err || window.t('opt.saveError')}</span>}
        </div>
      </React.Fragment>);
  }

  // ===================== VUE LECTURE SEULE (non-admin) =====================
  function ReadOnlyConfig({ S }) {
    const projects = S.projects || [];
    const periodsGlobal = S.periods || [];
    const lpGlobal = S.labelPhases || {};
    const trackedLabels = S.trackedLabels || [];
    const teams = S.teams || [];
    const phaseColor = (key) => key === 'none' ? '#5f6b7a' : ((periodsGlobal.find((p) => p.key === key) || {}).color || '#5f6b7a');
    const phaseName = (key) => key === 'none' ? window.t('opt.phNone') : ((periodsGlobal.find((p) => p.key === key) || {}).name || key);
    // Cette section ne reflète QUE les labels « Prod::* » (phases de production).
    const allLabels = trackedLabels.length ? trackedLabels : Object.keys(lpGlobal);
    const prodLabels = allLabels.filter((l) => /^prod::/i.test(l));
    return (
      <div className="opt-sec">
        <h3>{window.t('opt.config')}</h3>
        <p className="lead">{window.t('opt.configLead', { setup: '/setup' })}</p>
        <div className="opt-sub">{window.t('opt.importedProjects')}</div>
        <div className="checklist">
          {projects.length ? projects.map((p) => <label key={p.id} className="on" style={{ cursor: 'default' }}><input type="checkbox" readOnly checked style={{ pointerEvents: 'none' }} />{p.name} <span style={{ opacity: .6, fontFamily: 'var(--font-mono,monospace)', fontSize: 11 }}>#{p.id}</span></label>) : <p className="opt-note">{window.t('opt.noProjects')}</p>}
        </div>
        <div className="opt-sub">{window.t('opt.prodPhases')}</div>
        <div className="opt-phases">
          {periodsGlobal.length ? periodsGlobal.map((p) => <div className="opt-phrow" key={p.key}><div className="opt-swatchwrap"><span className="opt-swatch" style={{ background: p.color, cursor: 'default', display: 'inline-block' }}></span></div><span className="opt-phname" style={{ padding: '6px 4px' }}>{p.name}</span></div>) : <p className="opt-note">{window.t('opt.noPhases')}</p>}
        </div>
        <div className="opt-sub">{window.t('opt.assocLabels')} <span style={subGrey}>· {window.t('opt.assocProdOnly')}</span></div>
        <div className="opt-map">
          {prodLabels.length ? prodLabels.map((l) => { const key = lpGlobal[l] || 'none'; return <div className="opt-maprow" key={l}><span className="opt-dot" style={{ background: phaseColor(key) }}></span><span className="opt-mlabel">{l}</span><span style={{ fontSize: 12, color: 'var(--ink-faint)', marginLeft: 'auto' }}>{phaseName(key)}</span></div>; }) : <p className="opt-note">{window.t('opt.noLabels')}</p>}
        </div>
        <div className="opt-sub">{window.t('opt.teams')} <span style={subGrey}>· {window.t('opt.teamsSub')}</span></div>
        <TeamsReadOnly teams={teams} />
      </div>);
  }

  // ===================== ONGLET =====================
  window.TabOptions = function TabOptions({ theme, setTheme, appearance }) {
    const { accent, setAccent, numFont, setNumFont, compact, setCompact, drillLayout, setDrillLayout } = appearance;
    const S = (window.__DATA__ || {}).setup || {};
    const isAdmin = !!S.isAdmin;
    const milestones = (A.filterOptions || {}).milestones || [];

    const [regenMs, setRegenMs] = useState('');
    const [refreshState, setRefreshState] = useState('idle');
    const doRefresh = () => {
      setRefreshState('busy');
      fetch('/api/refresh', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(regenMs ? { milestones: [regenMs] } : {}) })
        .then((r) => setRefreshState(r.ok ? 'done' : 'err')).catch(() => setRefreshState('err'));
    };

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
            <div className="lbl">{window.t('opt.scope')}<span>{window.t('opt.scopeSub')}</span></div>
            <select style={selStyle} value={regenMs} onChange={(e) => setRegenMs(e.target.value)}>
              <option value="">{window.t('whole_project')}</option>
              {milestones.map((m) => <option key={m} value={m}>{m}</option>)}
            </select>
            <button className="btn btn-primary" disabled={refreshState === 'busy'} onClick={doRefresh}>{window.ICONS.refresh} {window.t('opt.refresh')}</button>
          </div>
          {refreshState === 'done' && <p className="opt-note" style={{ color: 'var(--good,#2f9e44)' }}>{window.t('opt.refreshStarted')}</p>}
          {refreshState === 'err' && <p className="opt-note" style={{ color: 'var(--bad,#e5484d)' }}>{window.t('opt.refreshError')}</p>}
        </div>}

        {isAdmin ? <AdminConfigEditor S={S} /> : <ReadOnlyConfig S={S} />}
      </div>);
  };
})();
