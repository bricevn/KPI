// native-widgets — pont de MIGRATION : expose chaque onglet natif (window.Tab*) comme un widget de
// page « vue native » (largeur pleine). Permet de composer une page modulaire contenant une vue native
// entière (Dashboard, Graphiques, Issues, Calendrier, Vélocité, Évolution, Anomalies). Étape 1 de la
// migration ; la décomposition fine de chaque onglet en sous-widgets est un chantier ultérieur.
//
// ⚠️ Chargé UNIQUEMENT dans le dashboard live (DashboardView) — PAS dans la galerie, car il dépend des
// window.Tab* (définis par les tab-*.jsx). Doit donc être chargé APRÈS les tab-*.jsx.
(function () {
  const { createElement: h } = React;
  // tweaks par défaut pour l'onglet Graphiques (seul onglet à exiger une prop).
  const DEFAULT_TWEAKS = { recapStyle: 'cartes', poidsStyle: 'barres', tempsStyle: 'empile' };

  const wrap = (tabName, props) => function NativeView() {
    const T = window[tabName];
    return T ? h(T, props || {}) : h('div', { className: 'widget-error' }, 'Vue « ' + tabName + ' » indisponible');
  };

  // [nom du widget, window.Tab*, libellé, props éventuelles]
  const DEFS = [
    ['NativeDashboard', 'TabDashboard', 'Vue Dashboard (native)', null],
    ['NativeCharts', 'TabCharts', 'Graphiques (native)', { tweaks: DEFAULT_TWEAKS }],
    ['NativeAnomalies', 'TabAnomalies', 'Anomalies (native)', null],
    ['NativeIssues', 'TabIssues', 'Issues (native)', null],
    ['NativeCalendar', 'TabCalendar', 'Calendrier / Gantt (native)', null],
    ['NativeVelocity', 'TabVelocity', 'Vélocité (native)', null],
    ['NativeComparison', 'TabComparison', 'Évolution / Comparaison (native)', null],
  ];

  DEFS.forEach(function (d) {
    window.KPIGallery.register({
      name: d[0], category: 'Vues natives', render: wrap(d[1], d[3]),
      notes: 'Onglet natif entier, disponible comme widget de page (largeur pleine, lit window.APP).',
      variants: [{ label: d[2], props: {} }],
    });
  });
})();
