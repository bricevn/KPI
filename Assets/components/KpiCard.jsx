// KpiCard — carte KPI signature du dashboard. COMPOSE ProgressBar + Sparkline
// (via window.KPI). Définit le contrat data d'un « widget de page » :
//   { icon, iconBg, label, value, suffix?, progress?:{value,max?,color},
//     caption?:[{value,label,color?},{value,label}], trend?:{data,color,delta?,deltaGood?}, onClick? }
(function () {
  const { createElement: h } = React;
  const EXPAND = h('svg', { viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' },
    h('path', { d: 'M15 3h6v6M9 21H3v-6M21 3l-7 7M3 21l7-7' }));

  function KpiCard({ icon, iconBg, label, value, suffix, progress, caption, trend, onClick }) {
    const ProgressBar = window.KPI.ProgressBar, Sparkline = window.KPI.Sparkline;
    return h('div', { className: 'kpi-card' + (onClick ? ' is-clickable' : ''), onClick },
      onClick ? h('span', { className: 'kpi-card-go', 'aria-hidden': true }, EXPAND) : null,
      h('div', { className: 'kpi-card-top' },
        h('span', { className: 'kpi-card-chip', style: { background: iconBg || 'var(--color-accent)' } }, icon),
        h('span', { className: 'kpi-card-label' }, label)),
      h('div', { className: 'kpi-card-value' }, value, suffix ? h('small', null, ' ' + suffix) : null),
      progress ? h('div', { className: 'kpi-card-progress' },
        h(ProgressBar, { value: progress.value, max: progress.max || 100, color: progress.color })) : null,
      caption ? h('div', { className: 'kpi-card-cap' },
        h('span', null, h('b', { style: { color: caption[0].color } }, caption[0].value), ' ' + caption[0].label),
        caption[1] ? h('span', { className: 'kpi-card-cap-sep' }, '·') : null,
        caption[1] ? h('span', null, h('b', null, caption[1].value), ' ' + caption[1].label) : null) : null,
      trend ? h('div', { className: 'kpi-card-trend' },
        h(Sparkline, { data: trend.data, color: trend.color || 'var(--color-accent)', width: 120, height: 18 }),
        trend.delta != null ? h('span', { className: 'delta', style: { color: trend.deltaGood ? 'var(--color-good)' : 'var(--color-bad)' } },
          (trend.deltaGood ? '↘ ' : '↗ ') + trend.delta) : null) : null);
  }

  // Icônes de démo (dans l'app réelle, fournies via props depuis window.ICONS / Lucide).
  const svg = (d) => h('svg', { viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' }, d);
  const ICO = {
    issue: svg([h('circle', { key: 1, cx: 12, cy: 12, r: 9 }), h('circle', { key: 2, cx: 12, cy: 12, r: 3, fill: 'currentColor' })]),
    weight: svg([h('path', { key: 1, d: 'M12 3a2 2 0 1 0 0 4 2 2 0 0 0 0-4z' }), h('path', { key: 2, d: 'M6.5 7h11l2.5 11a2 2 0 0 1-2 2.4H6a2 2 0 0 1-2-2.4z' })]),
    clock: svg([h('circle', { key: 1, cx: 12, cy: 12, r: 9 }), h('path', { key: 2, d: 'M12 7.5V12l3 2' })]),
  };

  window.KPIGallery.register({
    name: 'KpiCard', category: 'Blocs', render: KpiCard,
    notes: 'Carte KPI signature. Composée de ProgressBar + Sparkline. Contrat data d’un widget de page (icône, valeur, progression, caption, tendance, cliquable).',
    variants: [
      { label: 'Avancement (progression + caption)', props: {
        icon: ICO.issue, iconBg: 'var(--color-done)', label: 'Avancement', value: '72%',
        progress: { value: 72, color: 'var(--color-done)' },
        caption: [{ value: 18, label: 'fermées', color: 'var(--color-done)' }, { value: 7, label: 'ouvertes' }] } },
      { label: 'Poids validé', props: {
        icon: ICO.weight, iconBg: 'var(--color-good)', label: 'Poids validé', value: '61%',
        progress: { value: 61, color: 'var(--color-good)' },
        caption: [{ value: 44, label: 'validés', color: 'var(--color-good)' }, { value: 28, label: 'restants' }] } },
      { label: 'Cycle (tendance + delta)', props: {
        icon: ICO.clock, iconBg: 'var(--p-qa-backlog)', label: 'Cycle moyen', value: '8.4', suffix: 'j',
        caption: [{ value: '6 j', label: 'P50', color: 'var(--p-qa-backlog)' }, { value: '14 j', label: 'P85' }],
        trend: { data: [11, 10, 12, 9, 8, 9, 8.4], color: 'var(--p-qa-backlog)', delta: '-12 %', deltaGood: true } } },
      { label: 'Cliquable', props: {
        icon: ICO.issue, iconBg: 'var(--color-accent)', label: 'Approvals', value: '45%',
        progress: { value: 45 },
        caption: [{ value: 9, label: 'faits', color: 'var(--color-accent)' }, { value: 11, label: 'restants' }],
        onClick: () => {} } },
    ],
  });
})();
