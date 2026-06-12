// Dashboard tab — KPI cards + full pivot table by Type::* + phase summary.
(function () {
  const { useState } = React;
  window.TabDashboard = function TabDashboard() {
    const A = window.APP, K = A.kpis, T = A.totals;
    const [drill, setDrill] = useState(null);
    const { onSort, sorter, arrow } = window.useSort('issues');
    const get = (r, k) => {
      if (k === 'type') return A.typeByKey[r.key].short;
      if (k === 'wpc') return r.wV / (r.wV + r.wNV);
      if (k === 'apc') return r.appr;
      return r[k];
    };
    const rows = [...A.pivot];
    rows.sort((a, b) => sorter(a, b, get));
    const t = window.t;
    const Th = ({ k, children, num, unit, hint }) => <th className={'sortable' + (num ? ' num' : '')} onClick={() => onSort(k)} title={hint}>{children}{unit && <span className="th-unit">{unit}</span>} <span className="ar">{arrow(k)}</span></th>;

    const validated = A.detail.filter((d) => d.validated);
    const closed = A.detail.filter((d) => d.state === 'closed');
    const approved = A.detail.filter((d) => d.approval);
    const byCycle = [...A.detail].sort((a, b) => (b.end - b.start) - (a.end - a.start));
    const tComm = A.pivot.reduce((s, r) => s + r.comm, 0);
    // phase total = sum of average phase durations (mean lead time across phases)
    const phaseTotal = A.phaseAvg.reduce((s, p) => s + p.days, 0);
    // moyennes de phase indexées par key (pour la ligne Total) + max pour l'échelle des barres.
    const pa = {}; A.phaseAvg.forEach((p) => { pa[p.key] = p.days; });
    const phaseMax = Math.max(1, ...A.phaseAvg.map((p) => p.days));
    // tendance RÉELLE du cycle : moyenne du temps de cycle (_times.total) des issues regroupées
    // par jour de complétion (d.end) en 6 tranches sur la fenêtre. Delta = dernière vs première tranche non vide.
    const cycleTrend = (function () {
      const N = 6, span = Math.max(1, A.cal.DAYS), buckets = Array.from({ length: N }, () => []);
      A.detail.forEach((d) => { const tot = d._times && d._times.total; if (!(tot > 0)) return; const bi = Math.min(N - 1, Math.max(0, Math.floor(d.end / span * N))); buckets[bi].push(tot); });
      return buckets.map((b) => b.length ? Math.round(b.reduce((s, x) => s + x, 0) / b.length * 10) / 10 : 0);
    })();
    const cycleDelta = (function () { const nz = cycleTrend.filter((x) => x > 0); if (nz.length < 2) return null; return Math.round((nz[nz.length - 1] - nz[0]) / nz[0] * 100); })();

    const Kard = ({ anchor, chip, chipBg, label, value, suffix, pct, color, onClick, cue, bottom, cap }) => (
      <div className={'kcard' + (onClick ? ' clickable' : '')} data-comment-anchor={anchor} onClick={onClick} title={cue || undefined}>
        {onClick && <span className="kcard-go" aria-hidden="true">{window.ICONS.expand}</span>}
        <div className="top"><span className="kchip" style={{ background: chipBg }}>{chip}</span><span className="lab">{label}</span></div>
        <div className="big">{value}{suffix && <small> {suffix}</small>}</div>
        {pct != null && <window.Progress pct={pct} color={color} />}
        {cap &&
        <div className="kcap">
            <span><b style={{ color: cap[0].color }}>{cap[0].v}</b> {cap[0].label}</span>
            <span className="kcap-sep">·</span>
            <span><b>{cap[1].v}</b> {cap[1].label}</span>
          </div>}
        {bottom}
      </div>);

    return (
      <React.Fragment>
        <div className="kpis">
          <Kard anchor="1da3d31e09-div-17-11" chip={window.ICONS.issueDot} chipBg="var(--c-done)" label={t('dash.advancement')}
            value={K.progress.pct + '%'} pct={K.progress.pct} color={window.pctColor(K.progress.pct)}
            cap={[{ v: K.progress.closed, label: t('dash.closed'), color: 'var(--c-done)' }, { v: T.open, label: t('dash.open') }]}
            cue={t('dash.seeClosed')}
            onClick={() => setDrill({ title: t('dash.advancement'), headline: K.progress.pct + '%', subtitle: t('dash.subClosed', { closed: closed.length, open: T.open }), issues: closed, recap: 'issues' })} />
          <Kard chip={window.ICONS.weight} chipBg="var(--c-good)" label={t('dash.weightValidated')}
            value={K.weight.pct + '%'} pct={K.weight.pct} color={window.pctColor(K.weight.pct)}
            cap={[{ v: K.weight.v, label: t('dash.validated'), color: 'var(--c-good)' }, { v: K.weight.total - K.weight.v, label: t('dash.notValidated') }]}
            cue={t('dash.seeValidated')}
            onClick={() => setDrill({ title: t('dash.weightValidated'), headline: K.weight.pct + '%', subtitle: t('dash.subWeight', { v: K.weight.v, total: K.weight.total, n: validated.length }), issues: validated, recap: 'weight' })} />
          <Kard chip={window.ICONS.approve} chipBg="var(--c-regression)" label={t('dash.approvals')}
            value={K.approvals.pct + '%'} pct={K.approvals.pct} color={window.pctColor(K.approvals.pct)}
            cap={[{ v: K.approvals.with, label: t('dash.done'), color: 'var(--c-regression)' }, { v: K.approvals.total - K.approvals.with, label: t('dash.notDone') }]}
            cue={t('dash.seeApproved')}
            onClick={() => setDrill({ title: t('dash.approvals'), headline: K.approvals.pct + '%', subtitle: t('dash.subApprovals', { done: approved.length, notDone: A.detail.length - approved.length }), issues: approved, recap: 'issues' })} />
          <Kard chip={window.ICONS.clock} chipBg="var(--p-qawait)" label={t('dash.avgCycle')}
            value={K.cycle.days} suffix={t('unit_day')}
            cue={t('dash.seeLongest')}
            onClick={() => setDrill({ title: t('dash.cycleTitle'), subtitle: t('dash.cycleSub'), issues: byCycle, recap: false, mode: 'cycle' })}
            bottom={cycleTrend.some((x) => x > 0) &&
            <div className="kcard-trend">
                <window.SparkLine data={cycleTrend} color="var(--p-qawait)" />
                {cycleDelta != null && <span className="trend-delta" style={{ color: cycleDelta <= 0 ? 'var(--c-good)' : 'var(--c-bad)' }}>{cycleDelta <= 0 ? '↘' : '↗'} {cycleDelta > 0 ? '+' : ''}{cycleDelta} %</span>}
              </div>} />
        </div>

        <div className="pnl">
          <div className="pnl-h"><h3>{t('dash.kpisByType')}</h3><window.InfoTip text={t('dash.kpisByTypeTip')} /></div>
          <div className="tbl-scroll">
            <table className="tbl big">
              <thead><tr>
                <Th k="type" hint={t('tbl.hType')}>{t('tbl.type')}</Th>
                <Th k="issues" num hint={t('tbl.hIssues')}>{t('tbl.issuesOF')}</Th>
                <Th k="wpc" num hint={t('tbl.hWeight')}>{t('tbl.weightVT')}</Th>
                <Th k="apc" num hint={t('tbl.hApprovals')}>{t('tbl.approvalsFN')}</Th>
                {A.phases.map((ph) => <Th key={ph.key} k={ph.key} num unit={t('unit_day')}>{ph.name}</Th>)}
                <Th k="ret" num hint={t('tbl.hReturns')}>{t('tbl.returns')}</Th>
                <Th k="comm" num hint={t('tbl.hComments')}>{t('tbl.comments')}</Th>
              </tr></thead>
              <tbody>
                {rows.map((r) =>
                <tr key={r.key}>
                    <td><span className="type"><span className="dot" style={{ background: window.typeColor(r.key) }}></span>{A.typeByKey[r.key].short}</span></td>
                    <td><span className="oc"><span className="o">{r.open}</span><s>/</s><span className="c">{r.closed}</span></span></td>
                    <td><span className="oc"><span className="c">{r.wV}</span><s>/</s><span className="o">{r.wV + r.wNV}</span></span></td>
                    <td><span className="oc"><span className="c">{r.appr}</span><s>/</s><span className="o">{r.issues - r.appr}</span></span></td>
                    {A.phases.map((ph) => <td key={ph.key}>{(r[ph.key] || 0).toFixed(1)}</td>)}
                    <td>{r.ret}</td><td>{r.comm}</td>
                  </tr>
                )}
                <tr className="total">
                  <td>{t('tbl.total')}</td>
                  <td><span className="oc"><span className="o">{T.open}</span><s>/</s><span className="c">{T.closed}</span></span></td>
                  <td><span className="oc"><span className="c">{T.wV}</span><s>/</s><span className="o">{T.weight}</span></span></td>
                  <td><span className="oc"><span className="c">{K.approvals.with}</span><s>/</s><span className="o">{T.issues - K.approvals.with}</span></span></td>
                  {A.phases.map((ph) => <td key={ph.key}>{(pa[ph.key] || 0).toFixed(1)}</td>)}
                  <td>{T.ret}</td><td>{tComm}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div className="pnl">
          <div className="pnl-h"><h3>{t('dash.transversal')}</h3><window.InfoTip text={t('dash.transversalTip')} /></div>
          <div className="pnl-b">
            <div className="tv-cards">
              {A.transversal.map((r) => {
                const clo = window.pctOf(r.closed, r.issues);
                const val = window.pctOf(r.wV, r.wV + r.wNV);
                return (
                  <div key={r.key} className="tv-card">
                    <div className="tv-card-h">
                      <span className="tv-card-name">{r.name}</span>
                      <span className="tv-card-ratio">{t('dash.ofScope', { p: r.ratio })}</span>
                    </div>
                    <div className="tv-card-issues"><b>{r.issues}</b> {t('issues')} · <b>{r.open}</b> {t('dash.open')} · <b>{r.closed}</b> {t('dash.closed')}</div>
                    <div className="tv-prog">
                      <div className="tv-prog-row">
                        <span className="tv-prog-lbl">{t('dash.advancement')}</span>
                        <span className="tv-track"><i style={{ width: clo + '%', background: window.pctColor(clo) }}></i></span>
                        <span className="tv-prog-v">{clo}%</span>
                      </div>
                      <div className="tv-prog-row">
                        <span className="tv-prog-lbl">{t('dash.weightValidated')}</span>
                        <span className="tv-track"><i style={{ width: val + '%', background: window.pctColor(val) }}></i></span>
                        <span className="tv-prog-v">{r.wV}/{r.wV + r.wNV}</span>
                      </div>
                    </div>
                    <div className="tv-card-foot">
                      <span><b>{r.appr}</b> {t('dash.approvalsWord')}</span>
                      <span><b>{r.ret}</b> {t('dash.returnsWord')}</span>
                      <span><b>{r.comm}</b> {t('dash.commWord')}</span>
                    </div>
                  </div>);
              })}
            </div>
          </div>
        </div>

        <div className="pnl">
          <div className="pnl-h"><h3>{t('dash.avgTimePhase')}</h3><window.InfoTip text={t('dash.avgTimePhaseTip')} /></div>
          <div className="pnl-b">
            <div className="phase-grid">
              {A.phaseAvg.map((p) =>
              <div key={p.key} className="phase">
                  <span className="nm">{p.name}</span>
                  <span className="tr"><i style={{ width: Math.min(100, p.days / phaseMax * 100) + '%', background: window.phaseColor(p.key) }}></i></span>
                  <span className="v">{p.days.toFixed(1)}{t('unit_day')}</span>
                </div>
              )}
            </div>
            <div className="phase-total">
              <span className="nm">{t('dash.totalLead')}</span>
              <span className="v">{phaseTotal.toFixed(1)} {t('unit_day')}</span>
            </div>
          </div>
        </div>

        {drill && <window.IssueDrill {...drill} onClose={() => setDrill(null)} />}
      </React.Fragment>);

  };
})();
