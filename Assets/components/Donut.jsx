// Donut — camembert en anneau + légende + total au centre. Les couleurs viennent des
// données (palette daltonien-safe fournie par l'appelant).
// props : { segments: [{label, value, color}], size?, thickness?, showLegend? }
(function () {
  const { createElement: h } = React;

  function Donut({ segments = [], size = 120, thickness = 16, showLegend = true }) {
    const total = segments.reduce((s, x) => s + (x.value || 0), 0) || 1;
    const r = (size - thickness) / 2, cx = size / 2, cy = size / 2, circ = 2 * Math.PI * r;
    let acc = 0;
    const arcs = segments.map((s, i) => {
      const frac = (s.value || 0) / total;
      const dash = frac * circ;
      const el = h('circle', {
        key: i, cx, cy, r, fill: 'none', stroke: s.color, strokeWidth: thickness,
        strokeDasharray: `${dash} ${circ - dash}`, strokeDashoffset: -acc * circ,
        transform: `rotate(-90 ${cx} ${cy})`,
      });
      acc += frac;
      return el;
    });
    const svg = h('svg', { width: size, height: size, viewBox: `0 0 ${size} ${size}` },
      h('circle', { cx, cy, r, fill: 'none', stroke: 'var(--color-surface-3)', strokeWidth: thickness }),
      arcs,
      h('text', {
        x: cx, y: cy, textAnchor: 'middle', dominantBaseline: 'central',
        fontFamily: 'var(--font-display)', fontWeight: 700, fontSize: size * 0.22, fill: 'var(--color-ink-1)',
      }, String(total)));
    if (!showLegend) return svg;
    return h('div', { className: 'kpi-donut-wrap' }, svg,
      h('div', { className: 'kpi-donut-legend' }, segments.map((s, i) =>
        h('span', { key: i, className: 'kpi-donut-leg' },
          h('span', { className: 'sw', style: { background: s.color } }),
          `${s.label} · ${s.value}`))));
  }

  window.KPIGallery.register({
    name: 'Donut', category: 'Data', render: Donut,
    notes: 'Anneau + légende + total au centre. Couleurs fournies par les données (palette daltonien-safe).',
    variants: [
      { label: 'Par type', props: { segments: [
        { label: 'Feature', value: 12, color: 'var(--color-feature)' },
        { label: 'Bug', value: 5, color: 'var(--color-bug)' },
        { label: 'Enhancement', value: 3, color: 'var(--color-enh)' },
      ] } },
      { label: 'Par phase', props: { segments: [
        { label: 'Dev', value: 8, color: 'var(--p-in-progress)' },
        { label: 'Review', value: 4, color: 'var(--p-code-review)' },
        { label: 'QA', value: 6, color: 'var(--p-qa)' },
        { label: 'PO', value: 2, color: 'var(--p-po-validation)' },
      ] } },
    ],
  });
})();
