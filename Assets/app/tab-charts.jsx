// Graphiques tab — récap par super-groupe (→ popup par type), section Poids, section Temps.
// Design alternatives are driven by Tweaks (recapStyle / poidsStyle / tempsStyle).
(function () {
  const { useState } = React;
  const A = window.APP;
  // Phases DYNAMIQUES (clés config) : k = pk = clé de phase (plus d'alias 'rev').
  const PH = (A.phases || []).map((ph) => ({ k: ph.key, pk: ph.key }));

  const FIB = A.FIB;

  // aggregate a set of type keys into one row (counts summed, times issue-weighted)
  function agg(typeKeys) {
    const rows = typeKeys.map((k) => A.pivotByKey[k]);
    const sum = (f) => rows.reduce((s, r) => s + f(r), 0);
    const issues = sum((r) => r.issues);
    const o = {};
    PH.forEach(({ k }) => {o[k] = sum((r) => r[k] * r.issues) / issues;});
    return {
      issues, open: sum((r) => r.open), closed: sum((r) => r.closed),
      appr: sum((r) => r.appr), wV: sum((r) => r.wV), wNV: sum((r) => r.wNV),
      ret: sum((r) => r.ret), comm: sum((r) => r.comm), times: o,
      lead: PH.reduce((s, { k }) => s + o[k], 0)
    };
  }
  const leadOf = (r) => PH.reduce((s, { k }) => s + r[k], 0);

  // ── small shared bits ──────────────────────────────────────────────
  const MetricBar = ({ label, value, total, pct, color, suffix }) =>
  <div className="gm-row">
      <span className="gm-lbl">{label}</span>
      <span className="gm-track"><i style={{ width: (pct != null ? pct : window.pctOf(value, total)) + '%', background: color }}></i></span>
      <span className="gm-val">{suffix != null ? suffix : `${value}/${total}`}</span>
    </div>;

  function TypeBreakdown({ typeKeys }) {
    return (
      <table className="tbl">
        <thead><tr><th>{window.t('tbl.type')}</th><th className="num">{window.t('tbl.issuesOF')}</th><th className="num">{window.t('tbl.weightVT')}</th><th className="num">{window.t('tbl.approvalsFN')}</th><th className="num">{window.t('charts.time')}</th></tr></thead>
        <tbody>
          {typeKeys.map((k) => {
            const r = A.pivotByKey[k];
            return (
              <tr key={k}>
                <td><span className="type"><span className="dot" style={{ background: window.typeColor(k) }}></span>{A.typeByKey[k].short}</span></td>
                <td><span className="oc"><span className="o">{r.open}</span><s>/</s><span className="c">{r.closed}</span></span></td>
                <td><span className="oc"><span className="c">{r.wV}</span><s>/</s><span className="o">{r.wV + r.wNV}</span></span></td>
                <td><span className="oc"><span className="c">{r.appr}</span><s>/</s><span className="o">{r.issues - r.appr}</span></span></td>
                <td>{leadOf(r).toFixed(1)} {window.t('unit_day')}</td>
              </tr>);
          })}
        </tbody>
      </table>);
  }

  // ── Section 1: Récap par super-groupe ──────────────────────────────
  function RecapSection({ style }) {
    const [drill, setDrill] = useState(null);
    const groups = A.superGroups.map((g) => ({ ...g, d: agg(g.types) }));
    const totalPts = groups.reduce((s, g) => s + g.d.wV + g.d.wNV, 0);
    const shareOf = (d) => Math.round((d.wV + d.wNV) / totalPts * 100);

    const openDrill = (g) => setDrill(g);
    const Metrics = ({ d }) =>
    <div className="gm-list">
        <MetricBar label={window.t('dash.advancement')} value={d.closed} total={d.issues} color="var(--c-done)" />
        <MetricBar label={window.t('dash.weightValidated')} value={d.wV} total={d.wV + d.wNV} color="var(--c-good)" />
        <MetricBar label={window.t('dash.approvals')} value={d.appr} total={d.issues} color="var(--c-regression)" />
        <MetricBar label={window.t('charts.timeLead')} pct={Math.min(100, d.lead / 18 * 100)} suffix={d.lead.toFixed(1) + ' ' + window.t('unit_day')} color="var(--p-qawait)" />
      </div>;

    return (
      <div className="g-section">
        <div className="g-sec-h"><h3>{window.t('charts.recap')}</h3><window.InfoTip text={window.t('charts.recapTip')} /></div>

        {style === 'cartes' ?
        <div className="sg-cards">
            {groups.map((g) =>
          <button key={g.key} className="sg-card" onClick={() => openDrill(g)}>
                <span className="kcard-go">{window.ICONS.expand}</span>
                <div className="sg-card-h"><span className="dot" style={{ background: g.color }}></span><span className="sg-name">{g.name}</span><span className="sg-share" title={window.t('charts.shareTitle')}>{window.t('charts.ofTotal', { p: shareOf(g.d) })}</span></div>
                <div className="sg-issues"><b>{g.d.issues}</b> {window.t('issues')} · <b>{g.d.closed}</b> {window.t('dash.closed')} · <b>{g.types.length}</b> {window.t('charts.typesWord')}</div>
                <Metrics d={g.d} />
              </button>
          )}
          </div> :

        <div className="pnl"><div className="tbl-scroll"><table className="tbl big">
            <thead><tr><th>{window.t('charts.superGroup')}</th><th className="num">{window.t('tbl.issuesOF')}</th><th className="num">{window.t('tbl.weightVT')}</th><th className="num">{window.t('tbl.approvalsFN')}</th><th className="num">{window.t('charts.time')}</th><th className="num" title={window.t('charts.shareTitle')}>{window.t('charts.share')}</th><th></th></tr></thead>
            <tbody>
              {groups.map((g) =>
                <tr key={g.key} className="sg-trow" onClick={() => openDrill(g)}>
                  <td><span className="type"><span className="dot" style={{ background: g.color }}></span><b>{g.name}</b></span></td>
                  <td><span className="oc"><span className="o">{g.d.open}</span><s>/</s><span className="c">{g.d.closed}</span></span></td>
                  <td><span className="oc"><span className="c">{g.d.wV}</span><s>/</s><span className="o">{g.d.wV + g.d.wNV}</span></span></td>
                  <td><span className="oc"><span className="c">{g.d.appr}</span><s>/</s><span className="o">{g.d.issues - g.d.appr}</span></span></td>
                  <td>{g.d.lead.toFixed(1)} {window.t('unit_day')}</td>
                  <td className="num"><b>{shareOf(g.d)}%</b></td>
                  <td><span className="sg-go">{window.ICONS.expand}</span></td>
                </tr>
                )}
            </tbody>
          </table></div></div>
        }

        {drill && <window.Modal title={drill.name} subtitle={window.t('charts.byType')} headline={<React.Fragment>{drill.d.issues} {window.t('issues')}<span className="mh-share">{window.t('charts.ofTotal', { p: shareOf(drill.d) })}</span></React.Fragment>} wide layout={typeof window !== 'undefined' && window.__drillLayout || 'modal'} onClose={() => setDrill(null)}>
          <div className="dm-metrics">
            <div className="dm-kpi"><span className="dm-v" style={{ color: 'var(--c-done)' }}>{window.pctOf(drill.d.closed, drill.d.issues)}%</span><span className="dm-l">{window.t('charts.advancementLc')}</span></div>
            <div className="dm-kpi"><span className="dm-v" style={{ color: 'var(--c-good)' }}>{window.pctOf(drill.d.wV, drill.d.wV + drill.d.wNV)}%</span><span className="dm-l">{window.t('charts.weightValidatedLc')}</span></div>
            <div className="dm-kpi"><span className="dm-v" style={{ color: 'var(--c-regression)' }}>{window.pctOf(drill.d.appr, drill.d.issues)}%</span><span className="dm-l">{window.t('charts.approvalsLc')}</span></div>
            <div className="dm-kpi"><span className="dm-v" style={{ color: 'var(--p-qawait)' }}>{drill.d.lead.toFixed(1)} {window.t('unit_day')}</span><span className="dm-l">{window.t('charts.timeLeadLc')}</span></div>
          </div>
          <TypeBreakdown typeKeys={drill.types} />
        </window.Modal>}
      </div>);
  }

  // ── Section 2: Poids ───────────────────────────────────────────────
  function PoidsSection({ style }) {
    const types = A.types.map((t) => t.key);
    const [pin, setPin] = useState(null);
    const [hov, setHov] = useState(null);
    const hi = hov || pin;
    React.useEffect(() => {setPin(null);setHov(null);}, [style]);
    const legProps = (k) => ({
      className: 'g-leg clickable' + (pin === k ? ' pinned' : '') + (hi && hi !== k ? ' faded' : ''),
      onMouseEnter: () => setHov(k), onMouseLeave: () => setHov(null),
      onClick: () => setPin((p) => p === k ? null : k),
      role: 'button', tabIndex: 0
    });
    // segment opacity: validated (v) and non-validated (nv) of a given type
    const vOp = (tk) => hi == null ? 1 : hi === tk ? 1 : 0.1;
    const nvOp = (tk) => hi == null ? 0.5 : hi === tk || hi === 'nv' ? 0.6 : 0.08;
    // counts[fibIndex][typeKey] = {v, nv}
    const cell = (tk, wi) => A.weightMatrix[tk][wi];
    const totalAt = (wi) => types.reduce((s, tk) => s + cell(tk, wi).v + cell(tk, wi).nv, 0);
    const maxRow = Math.max(...FIB.map((_, wi) => totalAt(wi)));
    const typeTotal = (tk) => A.weightMatrix[tk].reduce((s, c) => s + c.v + c.nv, 0);
    const maxCell = Math.max(...types.flatMap((tk) => A.weightMatrix[tk].map((c) => c.v + c.nv)));

    const Legend = () =>
    <div className={'g-legend' + (hi ? ' has-hi' : '')}>
        {types.map((tk) => <span key={tk} {...legProps(tk)}><span className="sw" style={{ background: window.typeColor(tk) }}></span>{A.typeByKey[tk].short}</span>)}
        <span {...legProps('nv')}><span className="sw hatch"></span>{window.t('charts.notValidatedLeg')}</span>
      </div>;

    return (
      <div className="g-section">
        <div className="g-sec-h"><h3>{window.t('charts.poids')}</h3><window.InfoTip text={window.t('charts.poidsTip')} /></div>

        {style === 'barres' &&
        <div className="pnl"><div className="pnl-b"><Legend />
          <div className="pw-bars">
            {FIB.map((w, wi) =>
              <div key={w} className="pw-row">
                <span className="pw-w">{w}</span>
                <span className="pw-stack" style={{ width: totalAt(wi) / maxRow * 100 + '%' }}>
                  {types.map((tk) => {
                    const c = cell(tk, wi);const tot = totalAt(wi);
                    return <React.Fragment key={tk}>
                    <i style={{ width: c.v / tot * 100 + '%', background: window.typeColor(tk), opacity: vOp(tk), transition: 'opacity .15s' }} title={`${A.typeByKey[tk].short} · ${w} pts · ${c.v} ${window.t('charts.validatedSeg')}`}></i>
                    <i className="hatch-seg" style={{ width: c.nv / tot * 100 + '%', background: window.typeColor(tk), opacity: nvOp(tk), transition: 'opacity .15s' }} title={`${A.typeByKey[tk].short} · ${w} pts · ${c.nv} ${window.t('charts.notValidatedSeg')}`}></i>
                  </React.Fragment>;
                  })}
                </span>
                <span className="pw-tot">{totalAt(wi)}</span>
              </div>
              )}
          </div></div></div>
        }

        {style === 'colonnes' &&
        <div className="pnl"><div className="pnl-b"><Legend />
          <div className="pw-grouped">
            {types.map((tk) =>
              <div key={tk} className="pw-gcol">
                <div className="pw-gbars">
                  {FIB.map((w, wi) => {
                    const c = cell(tk, wi);const tot = c.v + c.nv;
                    return <div key={w} className="pw-gbar" title={`${A.typeByKey[tk].short} · ${w} pts · ${c.v}✓ / ${c.nv}`}>
                    <div className="pw-gfill" style={{ height: tot / maxCell * 100 + '%' }}>
                      <i className="hatch-seg" style={{ height: c.nv / (tot || 1) * 100 + '%', background: window.typeColor(tk), opacity: nvOp(tk), transition: 'opacity .15s' }}></i>
                      <i style={{ height: c.v / (tot || 1) * 100 + '%', background: window.typeColor(tk), opacity: vOp(tk), transition: 'opacity .15s' }}></i>
                    </div>
                    <span className="pw-gw">{w}</span>
                  </div>;
                  })}
                </div>
                <div className="pw-gname"><span className="dot" style={{ background: window.typeColor(tk) }}></span>{A.typeByKey[tk].short}</div>
              </div>
              )}
          </div></div></div>
        }

        {style === 'matrice' &&
        <div className="pnl"><div className="tbl-scroll"><table className="pw-matrix">
            <thead><tr><th>{window.t('charts.typeWeight')}</th>{FIB.map((w) => <th key={w} className="num">{w}</th>)}<th className="num">Σ</th></tr></thead>
            <tbody>
              {types.map((tk) =>
                <tr key={tk}>
                  <td><span className="type"><span className="dot" style={{ background: window.typeColor(tk) }}></span>{A.typeByKey[tk].short}</span></td>
                  {FIB.map((w, wi) => {
                    const c = cell(tk, wi);const tot = c.v + c.nv;
                    return <td key={w} className="pw-cell" title={`${c.v} validé · ${c.nv} non validé`}>
                    <span className="pw-chip" style={{ background: window.typeColor(tk), opacity: 0.18 + tot / maxCell * 0.72 }}>{tot}</span>
                    <span className="pw-sub">{c.v}✓</span>
                  </td>;
                  })}
                  <td className="num"><b>{typeTotal(tk)}</b></td>
                </tr>
                )}
            </tbody>
          </table></div></div>
        }
      </div>);
  }

  // ── Section 3: Temps par type ──────────────────────────────────────
  function TempsSection({ style }) {
    const types = A.types.map((t) => t.key);
    const maxLead = Math.max(...types.map((tk) => leadOf(A.pivotByKey[tk])));
    const maxPhase = Math.max(...types.flatMap((tk) => PH.map(({ k }) => A.pivotByKey[tk][k])));
    const [pin, setPin] = useState(null);
    const [hov, setHov] = useState(null);
    const hi = hov || pin;
    React.useEffect(() => {setPin(null);setHov(null);}, [style]);
    const legProps = (k) => ({
      className: 'g-leg clickable' + (pin === k ? ' pinned' : '') + (hi && hi !== k ? ' faded' : ''),
      onMouseEnter: () => setHov(k), onMouseLeave: () => setHov(null),
      onClick: () => setPin((p) => p === k ? null : k),
      role: 'button', tabIndex: 0
    });
    const segOp = (k) => hi == null ? 1 : hi === k ? 1 : 0.1;

    const Legend = () =>
    <div className={'g-legend' + (hi ? ' has-hi' : '')}>
        {PH.map(({ k, pk }) => <span key={k} {...legProps(k)}><span className="sw" style={{ background: window.phaseColor(pk) }}></span>{window.PHASE_NAME[pk]}</span>)}
      </div>;

    return (
      <div className="g-section">
        <div className="g-sec-h"><h3>{window.t('charts.temps')}</h3><window.InfoTip text={window.t('charts.tempsTip')} /></div>

        {style === 'empile' &&
        <div className="pnl"><div className="pnl-b"><Legend />
          <div className="tt-stacked">
            {types.map((tk) => {
                const r = A.pivotByKey[tk];const lead = leadOf(r);
                return <div key={tk} className="tt-row">
              <span className="tt-name"><span className="dot" style={{ background: window.typeColor(tk) }}></span>{A.typeByKey[tk].short}</span>
              <span className="tt-stack" style={{ width: lead / maxLead * 100 + '%' }}>
                {PH.map(({ k, pk }) => <i key={k} style={{ width: r[k] / lead * 100 + '%', background: window.phaseColor(pk), opacity: segOp(k), transition: 'opacity .15s' }} title={`${window.PHASE_NAME[pk]} · ${r[k].toFixed(1)} j`}></i>)}
              </span>
              <span className="tt-lead">{lead.toFixed(1)} {window.t('unit_day')}</span>
            </div>;
              })}
          </div></div></div>
        }

        {style === 'phase' &&
        <div className="pnl"><div className="pnl-b">
          <div className={'g-legend' + (hi ? ' has-hi' : '')}>{types.map((tk) => <span key={tk} {...legProps(tk)}><span className="sw" style={{ background: window.typeColor(tk) }}></span>{A.typeByKey[tk].short}</span>)}</div>
          <div className="tt-byphase">
            {PH.map(({ k, pk }) =>
              <div key={k} className="tt-pgroup">
                <div className="tt-pname">{window.PHASE_NAME[pk]}</div>
                <div className="tt-pbars">
                  {types.map((tk) => {
                    const v = A.pivotByKey[tk][k];
                    return <div key={tk} className="tt-pbar" title={`${A.typeByKey[tk].short} · ${v.toFixed(1)} j`}>
                    <div className="tt-pfill" style={{ height: v / maxPhase * 100 + '%', background: window.typeColor(tk), opacity: segOp(tk), transition: 'opacity .15s' }}></div>
                  </div>;
                  })}
                </div>
              </div>
              )}
          </div></div></div>
        }

        {style === 'matrice' &&
        <div className="pnl"><div className="tbl-scroll"><table className="pw-matrix">
            <thead><tr><th>{window.t('charts.typePhase')}</th>{PH.map(({ pk }) => <th key={pk} className="num">{window.PHASE_NAME[pk]}</th>)}<th className="num">{window.t('charts.lead')}</th></tr></thead>
            <tbody>
              {types.map((tk) => {
                  const r = A.pivotByKey[tk];
                  return <tr key={tk}>
                <td><span className="type"><span className="dot" style={{ background: window.typeColor(tk) }}></span>{A.typeByKey[tk].short}</span></td>
                {PH.map(({ k, pk }) => <td key={k} className="pw-cell">
                  <span className="pw-chip" style={{ background: window.phaseColor(pk), opacity: 0.18 + r[k] / maxPhase * 0.72 }}>{r[k].toFixed(1)}</span>
                </td>)}
                <td className="num"><b>{leadOf(r).toFixed(1)}</b></td>
              </tr>;
                })}
            </tbody>
          </table></div></div>
        }
      </div>);
  }

  window.TabCharts = function TabCharts({ tweaks }) {
    const t = tweaks || {};
    return (
      <div className="charts">
        <RecapSection style={t.recapStyle || 'cartes'} />
        <PoidsSection style={t.poidsStyle || 'barres'} />
        <TempsSection style={t.tempsStyle || 'empile'} />
      </div>);
  };
})();