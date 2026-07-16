// Sparkline — mini-courbe de tendance (valeurs sans échelle 0-100). Aire + ligne +
// point de fin. props : { data: number[], color?, width?, height? }
(function () {
  const { createElement: h } = React;

  function Sparkline({ data = [], color, width = 140, height = 34 }) {
    const c = color || 'var(--color-accent)';
    if (data.length < 2) return h('svg', { className: 'kpi-spark', width, height });
    const max = Math.max(...data), min = Math.min(...data), rng = max - min || 1;
    const y = (v) => height - 3 - ((v - min) / rng) * (height - 8);
    const pts = data.map((v, i) => `${((i / (data.length - 1)) * width).toFixed(1)},${y(v).toFixed(1)}`).join(' ');
    const area = `0,${height} ${pts} ${width},${height}`;
    return h('svg', { className: 'kpi-spark', width, height, viewBox: `0 0 ${width} ${height}`, preserveAspectRatio: 'none' },
      h('polygon', { points: area, fill: c, opacity: 0.12 }),
      h('polyline', { points: pts, fill: 'none', stroke: c, strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', vectorEffect: 'non-scaling-stroke' }),
      h('circle', { cx: width, cy: y(data[data.length - 1]), r: 3, fill: c }));
  }

  window.KPIGallery.register({
    name: 'Sparkline', category: 'Data', render: Sparkline,
    notes: 'Courbe de tendance compacte, sans axe. Couleur libre (défaut accent).',
    variants: [
      { label: 'Tendance', props: { data: [3, 5, 4, 6, 7, 6, 9, 8, 11] } },
      { label: 'Décroissante (validé)', props: { data: [12, 10, 11, 8, 7, 5, 4], color: 'var(--color-good)' } },
    ],
  });
})();
