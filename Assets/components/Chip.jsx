// Chip — étiquette compacte, point coloré optionnel. Variante mono pour les labels
// GitLab (« Prod::… »). props : { label, dotColor?, mono? }
(function () {
  const { createElement: h } = React;

  function Chip({ label, dotColor, mono = false }) {
    return h('span', { className: 'kpi-chip' + (mono ? ' is-mono' : '') },
      dotColor ? h('span', { className: 'kpi-chip-dot', style: { background: dotColor } }) : null,
      label);
  }

  window.KPIGallery.register({
    name: 'Chip', category: 'Data', render: Chip,
    notes: 'Étiquette compacte. Point coloré optionnel. Variante mono = label GitLab (Prod::…).',
    variants: [
      { label: 'Simple', props: { label: 'Frontend' } },
      { label: 'Avec point', props: { label: 'Feature', dotColor: 'var(--color-feature)' } },
      { label: 'Label (mono)', props: { label: 'Prod::Code review', dotColor: 'var(--p-code-review)', mono: true } },
    ],
  });
})();
