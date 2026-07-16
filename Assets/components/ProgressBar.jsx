// ProgressBar — barre de progression 0→max. Couleur libre (défaut accent).
// props : { value, max?, color? }
(function () {
  const { createElement: h } = React;

  function ProgressBar({ value = 0, max = 100, color }) {
    const pct = max > 0 ? Math.max(0, Math.min(100, (value / max) * 100)) : 0;
    return h('div', { className: 'kpi-progress' },
      h('i', { style: { width: pct + '%', background: color || 'var(--color-accent)' } }));
  }

  window.KPIGallery.register({
    name: 'ProgressBar', category: 'Data', render: ProgressBar,
    notes: 'Progression 0→max. Couleur libre (défaut accent). Pleine largeur de son conteneur.',
    variants: [
      { label: '72 %', props: { value: 72 } },
      { label: 'Validé', props: { value: 45, color: 'var(--color-good)' } },
      { label: 'Sur échelle (6/8)', props: { value: 6, max: 8, color: 'var(--p-qa)' } },
    ],
  });
})();
