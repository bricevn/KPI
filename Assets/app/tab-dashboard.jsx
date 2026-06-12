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
    const Th = ({ k, children, num, unit, hint }) => <th className={'sortable' + (num ? ' num' : '')} onClick={() => onSort(k)} title={hint}>{children}{unit && <span className="th-unit">{unit}</span>} <span className="ar">{arrow(k)}</span></th>;

    const validated = A.detail.filter((d) => d.validated);
    const closed = A.detail.filter((d) => d.state === 'closed');
    const approved = A.detail.filter((d) => d.approval);
    const byCycle = [...A.detail].sort((a, b) => (b.end - b.start) - (a.end - a.start));
    const tComm = A.pivot.reduce((s, r) => s + r.comm, 0);
    // phase total = sum of average phase durations (mean lead time across phases)
    const phaseTotal = A.phaseAvg.reduce((s, p) => s + p.days, 0);

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
          <Kard anchor="1da3d31e09-div-17-11" chip={window.ICONS.issueDot} chipBg="var(--c-done)" label="Avancement"
            value={K.progress.pct + '%'} pct={K.progress.pct} color={window.pctColor(K.progress.pct)}
            cap={[{ v: K.progress.closed, label: 'fermées', color: 'var(--c-done)' }, { v: T.open, label: 'ouvertes' }]}
            cue="Voir les issues fermées"
            onClick={() => setDrill({ title: 'Avancement', headline: K.progress.pct + '%', subtitle: `${closed.length} fermées · ${T.open} ouvertes`, issues: closed, recap: 'issues' })} />
          <Kard chip={window.ICONS.weight} chipBg="var(--c-good)" label="Poids validé"
            value={K.weight.pct + '%'} pct={K.weight.pct} color={window.pctColor(K.weight.pct)}
            cap={[{ v: K.weight.v, label: 'validés', color: 'var(--c-good)' }, { v: K.weight.total - K.weight.v, label: 'non validés' }]}
            cue="Voir les issues validées"
            onClick={() => setDrill({ title: 'Poids validé', headline: K.weight.pct + '%', subtitle: `${K.weight.v}/${K.weight.total} pts validés · ${validated.length} issues`, issues: validated, recap: 'weight' })} />
          <Kard chip={window.ICONS.approve} chipBg="var(--c-regression)" label="Approvals"
            value={K.approvals.pct + '%'} pct={K.approvals.pct} color={window.pctColor(K.approvals.pct)}
            cap={[{ v: K.approvals.with, label: 'faits', color: 'var(--c-regression)' }, { v: K.approvals.total - K.approvals.with, label: 'non faits' }]}
            cue="Voir les issues approuvées"
            onClick={() => setDrill({ title: 'Approvals', headline: K.approvals.pct + '%', subtitle: `${approved.length} faits · ${A.detail.length - approved.length} non faits`, issues: approved, recap: 'issues' })} />
          <Kard chip={window.ICONS.clock} chipBg="var(--p-qawait)" label="Cycle moyen"
            value={K.cycle.days} suffix="j"
            cue="Voir les issues les plus longues"
            onClick={() => setDrill({ title: 'Cycle — temps par issue', subtitle: 'Temps de cycle complet par issue (création → fermeture), des plus longues aux plus courtes', issues: byCycle, recap: false, mode: 'cycle' })}
            bottom={
            <div className="kcard-trend">
                <window.SparkLine data={[16, 14, 13, 12, 11.8, 11.3]} color="var(--p-qawait)" />
                <span className="trend-delta" style={{ color: 'var(--c-good)' }}>↘ −29 %</span>
              </div>} />
        </div>

        <div className="pnl">
          <div className="pnl-h"><h3>KPIs par Type::*</h3><window.InfoTip text="Temps moyen passé dans chaque phase, en jours. Cliquez sur une colonne pour trier les types." /></div>
          <div className="tbl-scroll">
            <table className="tbl big">
              <thead><tr>
                <Th k="type" hint="Catégorie d'issue (label Type::*)">Type</Th>
                <Th k="issues" num hint="Issues ouvertes / fermées — tri par nombre total">Issues O/F</Th>
                <Th k="wpc" num hint="Poids validé / poids total — tri par taux de validation">Poids V/T</Th>
                <Th k="apc" num hint="Approvals faits / non faits — tri par nombre d'approbations">Approvals F/N</Th>
                <Th k="dev" num unit="j" hint="Temps moyen en phase Dev (jours)">Dev</Th>
                <Th k="rev" num unit="j" hint="Temps moyen en Review (jours)">Review</Th>
                <Th k="qawait" num unit="j" hint="Temps moyen en attente de QA (jours)">QA att.</Th>
                <Th k="qa" num unit="j" hint="Temps moyen en QA (jours)">QA</Th>
                <Th k="tofix" num unit="j" hint="Temps moyen en To fix (jours)">To fix</Th>
                <Th k="po" num unit="j" hint="Temps moyen en validation PO (jours)">PO</Th>
                <Th k="ret" num hint="Nombre de retours (aller-retours QA / To fix)">Retours</Th>
                <Th k="comm" num hint="Nombre de commentaires">Comm.</Th>
              </tr></thead>
              <tbody>
                {rows.map((r) =>
                <tr key={r.key}>
                    <td><span className="type"><span className="dot" style={{ background: window.typeColor(r.key) }}></span>{A.typeByKey[r.key].short}</span></td>
                    <td><span className="oc"><span className="o">{r.open}</span><s>/</s><span className="c">{r.closed}</span></span></td>
                    <td><span className="oc"><span className="c">{r.wV}</span><s>/</s><span className="o">{r.wV + r.wNV}</span></span></td>
                    <td><span className="oc"><span className="c">{r.appr}</span><s>/</s><span className="o">{r.issues - r.appr}</span></span></td>
                    <td>{r.dev.toFixed(1)}</td><td>{r.rev.toFixed(1)}</td><td>{r.qawait.toFixed(1)}</td>
                    <td>{r.qa.toFixed(1)}</td><td>{r.tofix.toFixed(1)}</td><td>{r.po.toFixed(1)}</td>
                    <td>{r.ret}</td><td>{r.comm}</td>
                  </tr>
                )}
                <tr className="total">
                  <td>Total</td>
                  <td><span className="oc"><span className="o">{T.open}</span><s>/</s><span className="c">{T.closed}</span></span></td>
                  <td><span className="oc"><span className="c">{T.wV}</span><s>/</s><span className="o">{T.weight}</span></span></td>
                  <td><span className="oc"><span className="c">154</span><s>/</s><span className="o">{T.issues - 154}</span></span></td>
                  <td>4.2</td><td>1.8</td><td>2.3</td><td>1.4</td><td>0.9</td><td>0.7</td>
                  <td>{T.ret}</td><td>{tComm}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div className="pnl">
          <div className="pnl-h"><h3>Labels transversaux</h3><window.InfoTip text="Labels transverses (CONTRACTUAL, Unplanned, Surcharge QA) qui recoupent plusieurs Type::*. Comptés à part du total, en % du périmètre filtré." /></div>
          <div className="pnl-b">
            <div className="tv-cards">
              {A.transversal.map((r) => {
                const clo = window.pctOf(r.closed, r.issues);
                const val = window.pctOf(r.wV, r.wV + r.wNV);
                return (
                  <div key={r.key} className="tv-card">
                    <div className="tv-card-h">
                      <span className="tv-card-name">{r.name}</span>
                      <span className="tv-card-ratio">{r.ratio}% du périmètre</span>
                    </div>
                    <div className="tv-card-issues"><b>{r.issues}</b> issues · <b>{r.open}</b> ouvertes · <b>{r.closed}</b> fermées</div>
                    <div className="tv-prog">
                      <div className="tv-prog-row">
                        <span className="tv-prog-lbl">Avancement</span>
                        <span className="tv-track"><i style={{ width: clo + '%', background: window.pctColor(clo) }}></i></span>
                        <span className="tv-prog-v">{clo}%</span>
                      </div>
                      <div className="tv-prog-row">
                        <span className="tv-prog-lbl">Poids validé</span>
                        <span className="tv-track"><i style={{ width: val + '%', background: window.pctColor(val) }}></i></span>
                        <span className="tv-prog-v">{r.wV}/{r.wV + r.wNV}</span>
                      </div>
                    </div>
                    <div className="tv-card-foot">
                      <span><b>{r.appr}</b> approvals</span>
                      <span><b>{r.ret}</b> retours</span>
                      <span><b>{r.comm}</b> comm.</span>
                    </div>
                  </div>);
              })}
            </div>
          </div>
        </div>

        <div className="pnl">
          <div className="pnl-h"><h3>Temps moyen par phase</h3><window.InfoTip text="Durée moyenne en jours, toutes issues confondues." /></div>
          <div className="pnl-b">
            <div className="phase-grid">
              {A.phaseAvg.map((p) =>
              <div key={p.key} className="phase">
                  <span className="nm">{p.name}</span>
                  <span className="tr"><i style={{ width: p.days / 4.2 * 100 + '%', background: window.phaseColor(p.key) }}></i></span>
                  <span className="v">{p.days.toFixed(1)}j</span>
                </div>
              )}
            </div>
            <div className="phase-total">
              <span className="nm">Total (lead time moyen)</span>
              <span className="v">{phaseTotal.toFixed(1)} j</span>
            </div>
          </div>
        </div>

        {drill && <window.IssueDrill {...drill} onClose={() => setDrill(null)} />}
      </React.Fragment>);

  };
})();
