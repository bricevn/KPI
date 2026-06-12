// Vélocité tab — per-person weekly weight: validated (top) + in-progress (bottom).
// Click a week cell for its detail.
(function () {
  const { useState } = React;
  const A = window.APP,WEEKS = A.cal.WEEKS;
  const TYPES = A.types.map((t) => t.key);
  const FIB = [1, 2, 3, 5, 8, 13];
  const CUR_WEEK = Math.floor(A.cal.TODAY / 7);
  const weekLabel = (i) => {
    const a = A.cal.fmtDay(i * 7),b = A.cal.fmtDay(Math.min(A.cal.DAYS, i * 7 + 6));
    return a + ' – ' + b;
  };
  const issuesFor = (pid, wk) => A.detail.filter((d) => d.validated && (d.seg.dev || []).some(([a, b, who]) =>
  who === pid && Math.floor(a / 7) <= wk && Math.floor((b - 1) / 7) >= wk));
  // in-progress (not yet validated) issues a person worked on during a given week
  const inprogFor = (pid, wk) => A.detail.filter((d) => !d.validated && (d.seg.dev || []).some(([a, b, who]) =>
  who === pid && Math.floor(a / 7) <= wk && Math.floor((b - 1) / 7) >= wk));

  const INFO = 'Chaque case = une semaine. La partie haute (couleur) = poids validé par type, la partie basse (hachurée) = travail en cours. Cliquez sur un type pour trier les personnes, sur une semaine pour voir le détail. Le poids d’une issue multi-assignés est réparti au prorata du temps de dev.';

  window.TabVelocity = function TabVelocity() {
    const { s, onSort, arrow } = window.useSort('', 'desc');
    const { scrollRef, dragging, Nav, gridStyle } = window.useGanttNav(900);
    const [drill, setDrill] = useState(null);
    // global max = tallest validated+in-progress stack, for a consistent scale
    let gmax = 1;
    A.people.forEach((p) => A.vel[p.id].weeks.forEach((w) => {const t = w.total + w.inprog;if (t > gmax) gmax = t;}));
    const H = 82;
    const typeWeight = (pid, k) => A.vel[pid].weeks.reduce((sum, w) => sum + (w.byType[k] || 0), 0);
    let ppl = [...A.people];
    if (s.key) {ppl.sort((a, b) => {const r = typeWeight(a.id, s.key) - typeWeight(b.id, s.key);return s.dir === 'desc' ? -r : r;});}

    return (
      <React.Fragment>
        <div className="cal-toolbar">
          <span className="muted" style={{ fontSize: 12, fontWeight: 600 }}>Poids validé / semaine</span>
          <window.InfoTip text={INFO} />
          <div className="cal-legend" style={{ marginLeft: 6 }}>
            {TYPES.map((k) => <span key={k} className={'cal-lg' + (s.key === k ? ' sort' : '')} style={{ cursor: 'pointer' }} onClick={() => onSort(k)}><span className="sw" style={{ background: window.typeColor(k) }}></span>{A.typeByKey[k].short} <span className="ar">{arrow(k)}</span></span>)}
            <span className="cal-lg" style={{ cursor: 'default' }}><span className="sw hatch"></span>en cours</span>
          </div>
          <Nav />
        </div>

        <div className="gantt">
          <div className={'gantt-scroll gantt-drag' + (dragging ? ' grabbing' : '')} ref={scrollRef}>
            <div className="gantt-grid" style={gridStyle}>
              <div className="gantt-axis vel-axis">
                <div className="gantt-axis-corner">Membre</div>
                {Array.from({ length: WEEKS }, (_, i) =>
                <span key={i} className={'wk' + (i === CUR_WEEK ? ' cur' : '')} title={weekLabel(i)}>
                    <span className="wk-n">S{i + 1}</span>
                    <span className="wk-d">{A.cal.fmtDay(i * 7)}</span>
                    {i === CUR_WEEK && <span className="wk-now">en cours</span>}
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
                        <span className="vavg" title="Poids validé moyen par semaine">{'moy. ' + window.fmt1(weekAvg) + '/sem.'}</span>
                      </div>
                      <div className="vdist" title="Répartition des poids (nombre d’issues par valeur)">
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
                      {v.weeks.map((w, i) => {
                        const tot = w.total + w.inprog;
                        return (
                          <div key={i} className={'vweek clickable' + (i === CUR_WEEK ? ' cur' : '')}
                          title={weekLabel(i) + ' · ' + window.fmt1(w.total) + ' pts validés' + (w.inprog ? ' · ' + window.fmt1(w.inprog) + ' en cours' : '')}
                          onClick={() => setDrill({ pid: p.id, name: p.name, wk: i })}>
                            <div className="vbar" style={{ height: tot / gmax * H + 'px' }}>
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

        {drill && (() => {
          const dp = A.vel[drill.pid] && A.vel[drill.pid].weeks[drill.wk] || { total: 0, inprog: 0 };
          return <window.IssueDrill
            title={drill.name + ' · semaine ' + (drill.wk + 1)}
            headline={window.fmt1(dp.total) + ' pts'}
            subtitle={weekLabel(drill.wk) + (dp.inprog ? ' · ' + window.fmt1(dp.inprog) + ' pts en cours' : '')}
            groups={[
              { label: 'Validées', issues: issuesFor(drill.pid, drill.wk), recap: 'weight', color: 'var(--c-good)' },
              { label: 'En cours (non validé)', issues: inprogFor(drill.pid, drill.wk), color: 'var(--ink-faint)' }]}
            onClose={() => setDrill(null)} />;
        })()}
      </React.Fragment>);

  };
})();