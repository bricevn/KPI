// page-data — adaptateurs LIVE du dashboard modulaire. window.KPIData[key] : (APP, params, ctx)
// -> props d'un composant. SEULE couche autorisée à lire window.APP. Réutilise à l'identique la
// dérivation des tab-*.jsx (aucune nouvelle logique métier). Charge APRÈS ui.jsx (window.ICONS /
// pctColor / typeColor / phaseColor / t) — mais les fonctions ne lisent ces globales qu'au render.
(function () {
  window.KPIData = window.KPIData || {};
  const t = (k, p) => (window.t ? window.t(k, p) : k);
  const ICON = (k) => (window.ICONS ? window.ICONS[k] : null);

  window.KPIData['kpi.progress'] = function (A) {
    const K = A.kpis, T = A.totals;
    return { icon: ICON('issueDot'), iconBg: 'var(--c-done)', label: t('dash.advancement'), value: K.progress.pct + '%',
      progress: { value: K.progress.pct, color: window.pctColor(K.progress.pct) },
      caption: [{ value: K.progress.closed, label: t('dash.closed'), color: 'var(--c-done)' }, { value: T.open, label: t('dash.open') }] };
  };
  window.KPIData['kpi.weight'] = function (A) {
    const K = A.kpis;
    return { icon: ICON('weight'), iconBg: 'var(--c-good)', label: t('dash.weightValidated'), value: K.weight.pct + '%',
      progress: { value: K.weight.pct, color: window.pctColor(K.weight.pct) },
      caption: [{ value: K.weight.v, label: t('dash.validated'), color: 'var(--c-good)' }, { value: K.weight.total - K.weight.v, label: t('dash.notValidated') }] };
  };
  window.KPIData['kpi.approvals'] = function (A) {
    const K = A.kpis;
    return { icon: ICON('approve'), iconBg: 'var(--c-regression)', label: t('dash.approvals'), value: K.approvals.pct + '%',
      progress: { value: K.approvals.pct, color: window.pctColor(K.approvals.pct) },
      caption: [{ value: K.approvals.with, label: t('dash.done'), color: 'var(--c-regression)' }, { value: K.approvals.total - K.approvals.with, label: t('dash.notDone') }] };
  };
  window.KPIData['kpi.cycle'] = function (A) {
    const K = A.kpis;
    // Dérivation PARTAGÉE (window.KPICompute) — même calcul que l'onglet natif (tab-dashboard.jsx).
    const { trend, delta } = window.KPICompute.cycleTrend(A);
    const out = { icon: ICON('clock'), iconBg: 'var(--p-qawait)', label: t('dash.avgCycle'), value: K.cycle.days, suffix: t('unit_day'),
      caption: [{ value: K.cycle.p50 + ' ' + t('unit_day'), label: 'P50', color: 'var(--p-qawait)' }, { value: K.cycle.p85 + ' ' + t('unit_day'), label: 'P85' }] };
    // delta null (et non '') quand indéterminable → KpiCard n'affiche pas la flèche (test trend.delta != null).
    if (trend.some((x) => x > 0)) out.trend = { data: trend, color: 'var(--p-qawait)', delta: (delta != null ? (delta > 0 ? '+' : '') + delta + ' %' : null), deltaGood: delta == null || delta <= 0 };
    return out;
  };
  const phaseProps = (phs, label) => ({
    phases: phs.map((p) => ({ name: p.name, value: p.days, color: window.phaseColor(p.key) })),
    total: { label, value: phs.reduce((s, p) => s + p.days, 0) }, width: '100%',
  });
  window.KPIData['phase.worked'] = (A) => phaseProps(A.phaseAvg, t('dash.totalLead'));
  window.KPIData['phase.effective'] = (A) => phaseProps(A.phaseAvg.filter((p) => p.active), t('dash.tEffective'));
  window.KPIData['phase.wait'] = (A) => phaseProps(A.phaseAvg.filter((p) => !p.active), t('dash.tWait'));

  window.KPIData['types.distribution'] = function (A) {
    return { segments: A.pivot
      .map((r) => ({ label: A.typeByKey[r.key].short, value: r.open + r.closed, color: window.typeColor(r.key) }))
      .filter((s) => s.value > 0) };
  };

  window.KPIData['pivot.byType'] = function (A) {
    const cols = [
      { key: 'type', label: t('tbl.type'), sortValue: (r) => A.typeByKey[r.key].short,
        render: (r) => React.createElement('span', { className: 'type' },
          React.createElement('span', { className: 'dot', style: { background: window.typeColor(r.key) } }), A.typeByKey[r.key].short) },
      { key: 'issues', label: t('tbl.issuesOF'), render: (r) => r.closed + ' / ' + r.open, sortValue: (r) => r.open + r.closed },
      { key: 'wpc', label: t('tbl.weightVT'), render: (r) => r.wV + ' / ' + (r.wV + r.wNV), sortValue: (r) => r.wV },
    ];
    (A.phases || []).forEach((ph) => cols.push({ key: ph.key, label: ph.name, unit: t('unit_day'), render: (r) => (r[ph.key] || 0).toFixed(1) }));
    return { columns: cols, rows: A.pivot.slice() };
  };
})();
