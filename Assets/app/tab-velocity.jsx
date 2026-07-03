// Vélocité tab — per-person weekly weight: validated (top) + in-progress (bottom).
// Click a week cell for its detail.
(function () {
  const { useState } = React;
  const A = window.APP;
  const TYPES = A.types.map((t) => t.key);
  const FIB = [1, 2, 3, 5, 8, 13];
  const weekLabel = (i) => {
    // borne DAYS-1 : le dernier jour AFFICHÉ de la fenêtre (l'axe rend les offsets 0..DAYS-1).
    const a = A.cal.fmtDay(i * 7),b = A.cal.fmtDay(Math.min(A.cal.DAYS - 1, i * 7 + 6));
    return a + ' – ' + b;
  };
  // Semaine de la FERMETURE — celle où le poids validé est compté (voir mapper : vélocité classique).
  const closeWk = (d) => Math.min(A.cal.WEEKS - 1, Math.max(0, Math.floor((d.closeDay != null ? d.closeDay : d.end) / 7)));
  // pid contribue à l'issue : ASSIGNÉ (parts égales, voir mapper) ; repli porteur de dev si aucun assigné.
  const contributes = (d, pid) => {
    if (d.assignees.length) return d.assignees.indexOf(pid) >= 0;
    return (d.seg.dev || []).some(([a, b, who]) => who === pid && b > a);
  };
  // validées de la semaine = issues FERMÉES cette semaine-là (cohérent avec les barres).
  const issuesFor = (pid, wk) => A.detail.filter((d) => d.validated && closeWk(d) === wk && contributes(d, pid));
  // en cours = issues ouvertes du contributeur dont le dev (tous porteurs) chevauche la semaine.
  const inprogFor = (pid, wk) => A.detail.filter((d) => !d.validated && contributes(d, pid) && (d.seg.dev || []).some(([a, b]) =>
  Math.floor(a / 7) <= wk && Math.floor((b - 1) / 7) >= wk));

  const INFO = () => window.t('vel.tip');

  window.TabVelocity = function TabVelocity() {
    // WEEKS/CUR_WEEK lus À CHAQUE RENDU : les filtres reconstruisent window.APP (fenêtre temporelle
    // incluse) — un module-scope figé désalignerait les colonnes sur A.vel[..].weeks fraîchement calé.
    const WEEKS = A.cal.WEEKS;
    // clamp : TODAY est borné INCLUSIVEMENT à DAYS (milestone terminée) → floor(TODAY/7) peut
    // valoir WEEKS quand DAYS % 7 === 0, et le marqueur « maintenant » sortirait de l'axe.
    const CUR_WEEK = Math.min(WEEKS - 1, Math.floor(A.cal.TODAY / 7));
    // Barres début/fin de milestone : position en % de la piste, qui couvre WEEKS × 7 jours
    // (les colonnes-semaines sont de largeur égale — la timeline n'est plus bornée à la milestone).
    const msPos = (d) => d / (WEEKS * 7) * 100;
    const MSS = A.cal.msStart,MSE = A.cal.msEnd;
    const { s, onSort, arrow } = window.useSort('', 'desc');
    // ~40 px mini par semaine : en « Tout le projet » la fenêtre peut couvrir des dizaines de
    // semaines — à 900 px fixes les colonnes deviennent des slivers et les barres disparaissent
    // (la donnée n'existe plus que dans les tooltips). Défilement/zoom comme le Calendrier.
    const { scrollRef, dragging, Nav, gridStyle } = window.useGanttNav(Math.max(900, WEEKS * 40));
    const [drill, setDrill] = useState(null);
    const typeWeight = (pid, k) => A.vel[pid].weeks.reduce((sum, w) => sum + (w.byType[k] || 0), 0);
    // Lignes = assignés du périmètre ∩ sélection Utilisateur/Équipe (A.selectedUsers, minuscules).
    // Sans cette restriction, les co-assignés hors sélection gardent leur ligne (leurs issues
    // partagées avec la sélection restent au périmètre filtré).
    const selU = A.selectedUsers;
    let ppl = A.people.filter((p) => !selU || selU.indexOf(String(p.id).toLowerCase()) >= 0);
    if (s.key) {ppl.sort((a, b) => {const r = typeWeight(a.id, s.key) - typeWeight(b.id, s.key);return s.dir === 'desc' ? -r : r;});}
    // global max = tallest validated+in-progress stack among DISPLAYED rows, for a consistent scale
    let gmax = 1;
    ppl.forEach((p) => A.vel[p.id].weeks.forEach((w) => {const t = w.total + w.inprog;if (t > gmax) gmax = t;}));
    const H = 82;

    return (
      <React.Fragment>
        <div className="cal-toolbar">
          <span className="muted" style={{ fontSize: 12, fontWeight: 600 }}>{window.t('vel.validatedPerWeek')}</span>
          <window.InfoTip text={INFO()} />
          <div className="cal-legend" style={{ marginLeft: 6 }}>
            {TYPES.map((k) => <span key={k} className={'cal-lg' + (s.key === k ? ' sort' : '')} style={{ cursor: 'pointer' }} onClick={() => onSort(k)}><span className="sw" style={{ background: window.typeColor(k) }}></span>{A.typeByKey[k].short} <span className="ar">{arrow(k)}</span></span>)}
            <span className="cal-lg" style={{ cursor: 'default' }}><span className="sw hatch"></span>{window.t('vel.inProgress')}</span>
          </div>
          <Nav />
        </div>

        <div className="gantt">
          <div className={'gantt-scroll gantt-drag' + (dragging ? ' grabbing' : '')} ref={scrollRef}>
            <div className="gantt-grid" style={gridStyle}>
              <div className="gantt-axis vel-axis">
                <div className="gantt-axis-corner">{window.t('vel.member')}</div>
                {Array.from({ length: WEEKS }, (_, i) =>
                <span key={i} className={'wk' + (i === CUR_WEEK ? ' cur' : '')} title={weekLabel(i)}>
                    <span className="wk-n">{window.t('common.weekShort')}{i + 1}</span>
                    <span className="wk-d">{A.cal.fmtDay(i * 7)}</span>
                    {i === CUR_WEEK && <span className="wk-now">{window.t('vel.now')}</span>}
                  </span>
                )}
              </div>
              {ppl.map((p) => {
                const v = A.vel[p.id];
                const total = v.weeks.reduce((s, w) => s + w.total, 0);
                const weekAvg = total / WEEKS;
                const fibN = Object.values(v.fib).reduce((s, n) => s + n, 0) || 1;
                const fibSum = Object.entries(v.fib).reduce((s, [w, n]) => s + w * n, 0);
                const avgW = fibSum / fibN;
                const maxFib = Math.max(...FIB.map((w) => v.fib[w] || 0), 1);
                return (
                  <div key={p.id} className="vrow">
                    <div className="vlabel">
                      <div className="top">
                        <window.Avatar pid={p.id} size={30} />
                        <span className="nm">{p.name}</span>
                        <span className="vavg" title={window.t('vel.avgTitle')}>{window.t('vel.avgPre') + ' ' + window.fmt1(weekAvg) + window.t('vel.avgSuf')}</span>
                      </div>
                      <div className="vdist" title={window.t('vel.distTitle')}>
                        {FIB.map((w) => {
                          const n = v.fib[w] || 0;
                          return (
                            <div key={w} className="col">
                              <span className="colw">{n || ''}</span>
                              <span className={'colbar' + (n ? '' : ' empty')} style={{ height: (n ? 10 + n / maxFib * 16 : 5) + 'px' }}></span>
                              <span className="coln">{w}</span>
                            </div>);
                        })}
                      </div>
                    </div>
                    <div className="vtrack">
                      {MSS != null && <span className="gmark ms" title={window.t('cal.msBounds')} style={{ left: msPos(MSS) + '%' }}></span>}
                      {MSE != null && <span className="gmark ms" title={window.t('cal.msBounds')} style={{ left: msPos(MSE) + '%' }}></span>}
                      {v.weeks.map((w, i) => {
                        const tot = w.total + w.inprog;
                        return (
                          <div key={i} className={'vweek clickable' + (i === CUR_WEEK ? ' cur' : '')}
                          title={weekLabel(i) + ' · ' + window.fmt1(w.total) + ' ' + window.t('vel.ptsValidated') + (w.inprog ? ' · ' + window.fmt1(w.inprog) + ' ' + window.t('vel.inProgress') : '')}
                          onClick={() => setDrill({
                            title: p.name + ' · ' + window.t('vel.week') + ' ' + (i + 1),
                            headline: window.fmt1(w.total) + ' ' + window.t('vel.pts'),
                            subtitle: weekLabel(i) + (w.inprog ? ' · ' + window.fmt1(w.inprog) + ' ' + window.t('vel.ptsInProgress') : ''),
                            groups: [
                            { label: window.t('vel.validatedGroup'), issues: issuesFor(p.id, i), recap: 'weight', color: 'var(--c-good)' },
                            { label: window.t('vel.inProgressGroup'), issues: inprogFor(p.id, i), color: 'var(--ink-faint)' }]

                          })}>
                            {/* hauteur plancher 2px : une petite semaine (0,3 pt) face à un gmax
                                élevé donnait une barre sous-pixel — data visible au survol mais
                                graphe vide. Plancher UNIQUEMENT si la semaine est non vide. */}
                            <div className="vbar" style={{ height: (tot > 0 ? Math.max(2, tot / gmax * H) : 0) + 'px' }}>
                              {w.inprog > 0 && <i className="vseg-prog" style={{ height: w.inprog / tot * 100 + '%' }}></i>}
                              {TYPES.filter((k) => w.byType[k]).map((k) =>
                              <i key={k} style={{ height: w.byType[k] / tot * 100 + '%', background: window.typeColor(k) }}></i>
                              )}
                            </div>
                          </div>);
                      })}
                    </div>
                  </div>);
              })}
            </div>
          </div>
        </div>

        {drill && <window.IssueDrill {...drill} onClose={() => setDrill(null)} />}
      </React.Fragment>);

  };
})();