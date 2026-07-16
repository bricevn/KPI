// DeltaBadge — variation chiffrée + flèche. La couleur (bon/mauvais) dépend de
// positiveIsGood : une hausse n'est pas toujours « bonne » (ex. temps de cycle).
// props : { value, unit?, positiveIsGood?, decimals? }
(function () {
  const { createElement: h } = React;

  function DeltaBadge({ value = 0, unit = '', positiveIsGood = true, decimals = 1 }) {
    const dir = value > 0 ? 'up' : value < 0 ? 'down' : 'flat';
    const tone = dir === 'flat' ? 'flat' : ((dir === 'up') === positiveIsGood ? 'up' : 'down');
    const arrow = dir === 'up' ? '▲' : dir === 'down' ? '▼' : '→';
    const num = Math.abs(value).toFixed(decimals).replace(/\.0+$/, '');
    return h('span', { className: 'kpi-delta is-' + tone },
      h('span', { className: 'ar' }, arrow),
      h('span', null, num + unit));
  }

  window.KPIGallery.register({
    name: 'DeltaBadge', category: 'Data', render: DeltaBadge,
    notes: 'Variation + flèche. Le sens de la couleur suit positiveIsGood (une hausse de cycle est « mauvaise »).',
    variants: [
      { label: 'Hausse (bonne)', props: { value: 12.4, unit: '%' } },
      { label: 'Baisse (mauvaise)', props: { value: -8, unit: '%' } },
      { label: 'Hausse cycle (mauvaise)', props: { value: 2.3, unit: ' j', positiveIsGood: false } },
      { label: 'Stable', props: { value: 0, unit: '%' } },
    ],
  });
})();
