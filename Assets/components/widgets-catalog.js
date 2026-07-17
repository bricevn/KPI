// widgets-catalog — métadonnées pour l'ÉDITEUR de pages (Phase 5). Décrit, par type de widget,
// les sources de données compatibles (clés window.KPIData) + une largeur par défaut. Permet à
// l'éditeur d'auto-générer les formulaires (choix type -> sources compatibles).
// JS pur. Ne rend rien ; ne fait que déclarer window.KPIWidgets + window.KPIDataCatalog.
(function () {
  window.KPIWidgets = {
    KpiCard:   { label: 'Carte KPI',       data: ['kpi.progress', 'kpi.weight', 'kpi.approvals', 'kpi.cycle'], defaultW: 3 },
    PhaseBars: { label: 'Barres de phase', data: ['phase.worked', 'phase.effective', 'phase.wait'],            defaultW: 6 },
    Donut:     { label: 'Camembert',       data: ['types.distribution'],                                        defaultW: 6 },
    DataTable: { label: 'Tableau',         data: ['pivot.byType'],                                              defaultW: 12 },
    // Vues natives (pont de migration) : onglet entier, sans source de données (lit window.APP), pleine largeur.
    NativeDashboard: { label: 'Vue Dashboard (native)', data: [], defaultW: 12, native: true },
    NativeCharts: { label: 'Graphiques (native)', data: [], defaultW: 12, native: true },
    NativeAnomalies: { label: 'Anomalies (native)', data: [], defaultW: 12, native: true },
    NativeIssues: { label: 'Issues (native)', data: [], defaultW: 12, native: true },
    NativeCalendar: { label: 'Calendrier / Gantt (native)', data: [], defaultW: 12, native: true },
    NativeVelocity: { label: 'Vélocité (native)', data: [], defaultW: 12, native: true },
    NativeComparison: { label: 'Évolution / Comparaison (native)', data: [], defaultW: 12, native: true },
  };
  // Libellés lisibles des sources de données (clés window.KPIData).
  window.KPIDataCatalog = {
    'kpi.progress': 'Avancement', 'kpi.weight': 'Poids validé', 'kpi.approvals': 'Approvals', 'kpi.cycle': 'Cycle moyen',
    'phase.worked': 'Temps par phase — travaillé', 'phase.effective': 'Temps par phase — effectif', 'phase.wait': 'Temps par phase — attente',
    'types.distribution': 'Répartition par type', 'pivot.byType': 'Pivot par type',
  };
})();
