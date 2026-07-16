// GanttChart — amorce timeline : lignes d'issues + segments de phase datés (offset de jour).
// Version statique (le zoom/pan molette viendra plus tard) ; pose le CONTRAT DE DONNÉES.
// props :
//   rows: [{ label, segments: [{ phase, start, end, color }] }]   // start/end = offsets de jour [0..days]
//   days: nombre de jours de l'axe
//   startDate?: 'YYYY-MM-DD' (active week-ends estompés + libellés datés ; sinon libellés « J{i} »)
//   today?: offset du repère « aujourd'hui »
//   msStart?, msEnd?: offsets des bornes de milestone
//   labelHeader?: entête de la colonne de gauche · width?
(function () {
  const { createElement: h } = React;

  function GanttChart({ rows = [], days = 21, startDate, today, msStart, msEnd, labelHeader = 'Issue', width }) {
    const base = startDate ? new Date(startDate + 'T00:00:00') : null;
    const pos = (x) => (x / days) * 100;
    const dayInfo = (i) => {
      if (!base) return { label: i % 7 === 0 ? 'J' + i : '', we: false, m1: false };
      const d = new Date(base); d.setDate(d.getDate() + i);
      // Date ISO construite en LOCAL (pas toISOString, qui passe en UTC et décale d'un jour).
      const dow = d.getDay(), dom = d.getDate();
      const iso = d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(dom).padStart(2, '0');
      const label = (i === 0 || dom === 1) ? iso.slice(5) : (dow === 1 ? String(dom) : '');
      return { label, we: dow === 0 || dow === 6, m1: dom === 1, iso };
    };
    const style = width != null ? { width: typeof width === 'number' ? width + 'px' : width } : null;

    return h('div', { className: 'kpi-gantt', style },
      h('div', { className: 'kpi-gantt-scroll' },
        h('div', { className: 'kpi-gantt-grid' },
          // axe
          h('div', { className: 'kpi-gantt-axis' },
            h('div', { className: 'kpi-gantt-corner' }, labelHeader),
            Array.from({ length: days }, (_, i) => {
              const di = dayInfo(i);
              return h('span', { key: i, className: 'day' + (di.we ? ' we' : '') + (di.m1 ? ' m1' : ''), title: di.iso || ('J' + i) }, di.label);
            })),
          // lignes
          rows.map((r, ri) =>
            h('div', { key: ri, className: 'kpi-gantt-row' },
              h('div', { className: 'kpi-gantt-label' }, h('span', { className: 'nm' }, r.label)),
              h('div', { className: 'kpi-gantt-track' },
                msStart != null ? h('span', { className: 'kpi-gantt-mark ms', style: { left: pos(msStart) + '%' }, title: 'Début milestone' }) : null,
                msEnd != null ? h('span', { className: 'kpi-gantt-mark ms', style: { left: pos(msEnd) + '%' }, title: 'Fin milestone' }) : null,
                today != null ? h('span', { className: 'kpi-gantt-mark today', style: { left: pos(today) + '%' }, title: 'Aujourd’hui' }) : null,
                (r.segments || []).map((s, si) =>
                  h('span', { key: si, className: 'kpi-gantt-seg',
                    title: `${s.phase} · J${s.start}→J${s.end}`,
                    style: { left: pos(s.start) + '%', width: Math.max(0.8, pos(s.end - s.start)) + '%', background: s.color } }))))))));
  }

  const ROWS = [
    { label: '#412 Login flow', segments: [
      { phase: 'Dev', start: 0, end: 4, color: 'var(--p-in-progress)' },
      { phase: 'Review', start: 4, end: 6, color: 'var(--p-code-review)' },
      { phase: 'QA', start: 6, end: 9, color: 'var(--p-qa)' }] },
    { label: '#418 Export CSV', segments: [
      { phase: 'Dev', start: 3, end: 8, color: 'var(--p-in-progress)' },
      { phase: 'Review', start: 8, end: 10, color: 'var(--p-code-review)' },
      { phase: 'PO', start: 10, end: 12, color: 'var(--p-po-validation)' }] },
    { label: '#423 Dark theme', segments: [
      { phase: 'Dev', start: 7, end: 13, color: 'var(--p-in-progress)' },
      { phase: 'Review', start: 13, end: 15, color: 'var(--p-code-review)' },
      { phase: 'QA wait', start: 15, end: 18, color: 'var(--p-qa-backlog)' }] },
  ];

  window.KPIGallery.register({
    name: 'GanttChart', category: 'Blocs', render: GanttChart,
    notes: 'Amorce timeline (statique) : segments de phase datés par offset de jour, axe en jours (week-ends estompés), repères aujourd’hui/milestone. Le zoom/pan viendra plus tard.',
    variants: [
      { label: 'Timeline (dates + repères)', props: {
        rows: ROWS, days: 21, startDate: '2026-06-01', today: 12, msStart: 1, msEnd: 20, width: 640 } },
      { label: 'Sans dates (offsets J{i})', props: {
        rows: ROWS, days: 21, today: 12, width: 640 } },
    ],
  });
})();
