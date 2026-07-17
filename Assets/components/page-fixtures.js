// page-fixtures — banc de preuve du renderer (galerie uniquement). Fournit :
//   - window.KPIData[key] : adaptateurs de démo (mêmes CLÉS qu'en live), lisant un fixture local
//     (en live, Assets/app/page-data.jsx fournira les mêmes clés en lisant le vrai window.APP) ;
//   - window.__DEMO_PAGE : un modèle de page JSON complet (2 KpiCard + PhaseBars + Donut + DataTable).
// JS pur (hors Babel). Les adaptateurs peuvent renvoyer des nœuds/fonctions (icônes, colonnes) —
// seul le MODÈLE JSON (params) doit rester sérialisable.
(function () {
  window.KPIData = window.KPIData || {};
  const h = function () { return React.createElement.apply(null, arguments); };
  const svg = (children) => h('svg', { viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' }, children);
  const ICO = {
    issue: svg([h('circle', { key: 1, cx: 12, cy: 12, r: 9 }), h('circle', { key: 2, cx: 12, cy: 12, r: 3, fill: 'currentColor' })]),
    clock: svg([h('circle', { key: 1, cx: 12, cy: 12, r: 9 }), h('path', { key: 2, d: 'M12 7.5V12l3 2' })]),
  };

  // Fixture autonome (forme miroir de window.APP, en minimal).
  const FX = {
    progress: { pct: 72, closed: 18, open: 7 },
    cycle: { days: 8.4, p50: 6, p85: 14, trend: [11, 10, 12, 9, 8, 9, 8.4], delta: -12 },
    phases: [
      { name: 'Dev', value: 3.2, color: 'var(--p-in-progress)' },
      { name: 'Review', value: 1.5, color: 'var(--p-code-review)' },
      { name: 'QA', value: 2.1, color: 'var(--p-qa)' },
      { name: 'PO', value: 0.8, color: 'var(--p-po-validation)' },
    ],
    types: [
      { label: 'Feature', value: 12, color: 'var(--color-feature)' },
      { label: 'Bug', value: 5, color: 'var(--color-bug)' },
      { label: 'Enhancement', value: 3, color: 'var(--color-enh)' },
    ],
    pivotRows: [
      { type: 'Feature', color: 'var(--color-feature)', closed: 8, open: 4, weight: 21, dev: 3.2, qa: 2.1 },
      { type: 'Bug', color: 'var(--color-bug)', closed: 5, open: 1, weight: 9, dev: 1.1, qa: 1.6 },
      { type: 'Enhancement', color: 'var(--color-enh)', closed: 3, open: 2, weight: 7, dev: 2.4, qa: 0.9 },
    ],
    pivotTotal: { type: 'Total', closed: 16, open: 7, weight: 37, dev: 2.3, qa: 1.5 },
  };
  const dot = (c) => h('span', { style: { display: 'inline-block', width: 10, height: 10, borderRadius: 4, background: c, flexShrink: 0 } });

  // Adaptateurs (APP, params, ctx) -> props du composant. Ici ils lisent le fixture local.
  window.KPIData['kpi.progress'] = function () {
    return { icon: ICO.issue, iconBg: 'var(--color-done)', label: 'Avancement', value: FX.progress.pct + '%',
      progress: { value: FX.progress.pct, color: 'var(--color-done)' },
      caption: [{ value: FX.progress.closed, label: 'fermées', color: 'var(--color-done)' }, { value: FX.progress.open, label: 'ouvertes' }] };
  };
  window.KPIData['kpi.cycle'] = function () {
    return { icon: ICO.clock, iconBg: 'var(--p-qa-backlog)', label: 'Cycle moyen', value: FX.cycle.days, suffix: 'j',
      caption: [{ value: FX.cycle.p50 + ' j', label: 'P50', color: 'var(--p-qa-backlog)' }, { value: FX.cycle.p85 + ' j', label: 'P85' }],
      trend: { data: FX.cycle.trend, color: 'var(--p-qa-backlog)', delta: FX.cycle.delta + ' %', deltaGood: FX.cycle.delta <= 0 } };
  };
  window.KPIData['phase.effective'] = function () {
    return { phases: FX.phases, total: { label: 'Lead time effectif', value: FX.phases.reduce((s, p) => s + p.value, 0) }, width: '100%' };
  };
  window.KPIData['types.distribution'] = function () {
    return { segments: FX.types };
  };
  window.KPIData['pivot.byType'] = function () {
    const days = (k) => (r) => (r[k] || 0).toFixed(1);
    return {
      columns: [
        { key: 'type', label: 'Type', render: (r) => h('span', { style: { display: 'inline-flex', alignItems: 'center', gap: 8 } }, dot(r.color), r.type), sortValue: (r) => r.type },
        { key: 'issues', label: 'Issues O/F', render: (r) => r.closed + ' / ' + r.open, sortValue: (r) => r.closed + r.open },
        { key: 'weight', label: 'Poids' },
        { key: 'dev', label: 'Dev', unit: 'j', render: days('dev') },
        { key: 'qa', label: 'QA', unit: 'j', render: days('qa') },
      ],
      rows: FX.pivotRows, total: FX.pivotTotal,
    };
  };

  // Modèle de page de démo (même forme que les pages persistées dans user-pages.json, injectées via
  // window.__USER_PAGES__).
  window.__DEMO_PAGE = {
    id: 'demo', kind: 'modular',
    nav: { label: 'Démo', icon: 'dashboard' },
    layout: { cols: 12, gap: 'var(--space-4)' },
    widgets: [
      { id: 'w1', type: 'KpiCard', data: 'kpi.progress', layout: { w: 3 }, params: {} },
      { id: 'w2', type: 'KpiCard', data: 'kpi.cycle', layout: { w: 3 }, params: {} },
      { id: 'w3', type: 'PhaseBars', data: 'phase.effective', layout: { w: 6 }, params: {} },
      { id: 'w4', type: 'Donut', data: 'types.distribution', layout: { w: 6 }, params: {} },
      { id: 'w5', type: 'DataTable', data: 'pivot.byType', layout: { w: 6 }, params: {} },
    ],
  };
})();
