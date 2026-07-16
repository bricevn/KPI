// StatusBadge — statut coloré + glyphe (charte §03). Daltonien-safe : la couleur
// n'est JAMAIS le seul signal, un glyphe l'accompagne toujours.
// props : { status: 'good'|'warn'|'bad'|'done'|'neutral', label?: string }
(function () {
  const { createElement: h } = React;
  const svg = (children) => h('svg', { viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor',
    strokeWidth: 2.4, strokeLinecap: 'round', strokeLinejoin: 'round' }, children);

  const GLYPH = {
    good: svg(h('path', { d: 'M5 12l5 5L20 6' })),                                   // check
    warn: svg([h('path', { key: 'a', d: 'M12 3l9 16H3z' }), h('path', { key: 'b', d: 'M12 10v4' }), h('path', { key: 'c', d: 'M12 17h.01' })]), // triangle-alert
    bad: svg([h('path', { key: 'a', d: 'M6 6l12 12' }), h('path', { key: 'b', d: 'M18 6L6 18' })]), // x
    done: svg([h('circle', { key: 'c', cx: 12, cy: 12, r: 9 }), h('path', { key: 'p', d: 'M8 12l3 3 5-6' })]), // check-circle
    neutral: svg(h('circle', { cx: 12, cy: 12, r: 6 })),                            // dot
  };
  const LABEL = { good: 'Validé', warn: 'Attention', bad: 'Risque', done: 'Fermé', neutral: 'Ouvert' };

  function StatusBadge({ status = 'good', label }) {
    return h('span', { className: 'kpi-badge is-' + status },
      GLYPH[status] || null,
      h('span', null, label != null ? label : (LABEL[status] || status)));
  }

  window.KPIGallery.register({
    name: 'StatusBadge',
    category: 'Data',
    render: StatusBadge,
    notes: 'Statut = couleur Okabe-Ito + glyphe (jamais la couleur seule). Lisible en clair et sombre.',
    variants: [
      { label: 'Validé', props: { status: 'good' } },
      { label: 'Attention', props: { status: 'warn' } },
      { label: 'Risque', props: { status: 'bad' } },
      { label: 'Fermé', props: { status: 'done' } },
      { label: 'Ouvert', props: { status: 'neutral' } },
    ],
  });
})();
