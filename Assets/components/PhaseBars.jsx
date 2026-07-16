// PhaseBars — temps moyen par phase : nom | barre proportionnelle | valeur, + total.
// props : { phases: [{name, value, color}], total?: {label, value}, unit?='j', width? }
(function () {
  const { createElement: h } = React;

  function PhaseBars({ phases = [], total, unit = 'j', width }) {
    const max = Math.max(0.1, ...phases.map((p) => p.value || 0));
    const style = width != null ? { width: typeof width === 'number' ? width + 'px' : width } : null;
    return h('div', { style },
      h('div', { className: 'kpi-phasebars' }, phases.map((p, i) => {
        const pct = Math.min(100, ((p.value || 0) / max) * 100);
        return h('div', { key: i, className: 'kpi-phasebar' },
          h('span', { className: 'kpi-phasebar-nm' }, p.name),
          h('span', { className: 'kpi-phasebar-tr' }, h('i', { style: { width: pct + '%', background: p.color } })),
          h('span', { className: 'kpi-phasebar-v' }, (p.value || 0).toFixed(1) + unit));
      })),
      total ? h('div', { className: 'kpi-phasebars-total' },
        h('span', { className: 'nm' }, total.label),
        h('span', { className: 'v' }, (total.value || 0).toFixed(1) + ' ' + unit)) : null);
  }

  const PHASES = [
    { name: 'Dev', value: 3.2, color: 'var(--p-in-progress)' },
    { name: 'Review', value: 1.5, color: 'var(--p-code-review)' },
    { name: 'QA wait', value: 2.8, color: 'var(--p-qa-backlog)' },
    { name: 'QA', value: 1.9, color: 'var(--p-qa)' },
    { name: 'PO', value: 0.8, color: 'var(--p-po-validation)' },
  ];

  window.KPIGallery.register({
    name: 'PhaseBars', category: 'Blocs', render: PhaseBars,
    notes: 'Temps moyen par phase (barres proportionnelles au max) + total. Couleurs de phase fournies par les données.',
    variants: [
      { label: 'Temps travaillé', props: { phases: PHASES, total: { label: 'Lead time total', value: 10.2 }, width: 360 } },
      { label: 'Sans total', props: { phases: PHASES.slice(0, 3), width: 360 } },
    ],
  });
})();
