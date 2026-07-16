// DataTable — tableau générique, triable, avec ligne total optionnelle.
// Contrat :
//   columns: [{ key, label, unit?, align?('left'|'right'), sortable?, render?(row)=>node, sortValue?(row) }]
//   rows: object[]        · total?: object (ligne en gras)  · sortable?: bool (défaut true)
// La 1re colonne s'aligne à gauche par défaut, les autres à droite (comme le pivot du dashboard).
(function () {
  const { createElement: h, useState } = React;

  function DataTable({ columns = [], rows = [], total, sortable = true }) {
    const [sort, setSort] = useState({ key: null, dir: 'desc' });
    const canSort = (col) => sortable && col.sortable !== false;
    const onSort = (col) => {
      if (!canSort(col)) return;
      setSort((p) => p.key !== col.key ? { key: col.key, dir: 'desc' }
        : p.dir === 'desc' ? { key: col.key, dir: 'asc' } : { key: null, dir: 'desc' });
    };
    const sv = (col, row) => col.sortValue ? col.sortValue(row) : row[col.key];

    let body = rows.slice();
    if (sort.key) {
      const col = columns.find((c) => c.key === sort.key);
      body.sort((a, b) => {
        const va = sv(col, a), vb = sv(col, b);
        const r = typeof va === 'string' ? String(va).localeCompare(String(vb)) : (va - vb);
        return sort.dir === 'desc' ? -r : r;
      });
    }
    const alignCls = (col, i) => ((col.align || (i === 0 ? 'left' : 'right')) === 'left' ? ' is-left' : '');
    const arrow = (col) => sort.key === col.key ? (sort.dir === 'desc' ? '▼' : '▲') : '';
    const cell = (col, row) => col.render ? col.render(row) : row[col.key];

    return h('div', { className: 'kpi-table-scroll' },
      h('table', { className: 'kpi-table' },
        h('thead', null, h('tr', null, columns.map((col, i) =>
          h('th', { key: col.key, className: alignCls(col, i) + (canSort(col) ? ' is-sortable' : ''), onClick: () => onSort(col) },
            col.label,
            col.unit ? h('span', { className: 'unit' }, '(' + col.unit + ')') : null,
            ' ', h('span', { className: 'ar' }, arrow(col)))))),
        h('tbody', null,
          body.map((row, ri) => h('tr', { key: ri },
            columns.map((col, i) => h('td', { key: col.key, className: alignCls(col, i) }, cell(col, row))))),
          total ? h('tr', { className: 'is-total' },
            columns.map((col, i) => h('td', { key: col.key, className: alignCls(col, i) },
              col.render ? col.render(total) : total[col.key]))) : null)));
  }

  // --- fixtures ---
  const dot = (color) => h('span', { style: { display: 'inline-block', width: 10, height: 10, borderRadius: 4, background: color, flexShrink: 0 } });
  const typeCell = (r) => h('span', { style: { display: 'inline-flex', alignItems: 'center', gap: 8 } }, dot(r.color), r.type);
  const days = (k) => (r) => (r[k] || 0).toFixed(1);

  const COLUMNS = [
    { key: 'type', label: 'Type', render: typeCell, sortValue: (r) => r.type },
    { key: 'issues', label: 'Issues O/F', render: (r) => r.closed + ' / ' + r.open, sortValue: (r) => r.closed + r.open },
    { key: 'weight', label: 'Poids' },
    { key: 'dev', label: 'Dev', unit: 'j', render: days('dev') },
    { key: 'review', label: 'Review', unit: 'j', render: days('review') },
    { key: 'qa', label: 'QA', unit: 'j', render: days('qa') },
    { key: 'comm', label: 'Comm.' },
  ];
  const ROWS = [
    { type: 'Feature', color: 'var(--color-feature)', closed: 8, open: 4, weight: 21, dev: 3.2, review: 1.5, qa: 2.1, comm: 34 },
    { type: 'Bug', color: 'var(--color-bug)', closed: 5, open: 1, weight: 9, dev: 1.1, review: 0.8, qa: 1.6, comm: 12 },
    { type: 'Enhancement', color: 'var(--color-enh)', closed: 3, open: 2, weight: 7, dev: 2.4, review: 1.1, qa: 0.9, comm: 8 },
  ];
  const TOTAL = { type: 'Total', closed: 16, open: 7, weight: 37, dev: 2.3, review: 1.1, qa: 1.5, comm: 54 };

  window.KPIGallery.register({
    name: 'DataTable', category: 'Blocs', render: DataTable,
    notes: 'Tableau générique : colonnes configurables, tri (desc→asc→off), ligne total, cellules custom via render(). Base du pivot « KPIs par Type ».',
    variants: [
      { label: 'Pivot par type (triable + total)', props: { columns: COLUMNS, rows: ROWS, total: TOTAL } },
      { label: 'Simple (non triable)', props: {
        sortable: false,
        columns: [{ key: 'k', label: 'Métrique' }, { key: 'v', label: 'Valeur' }],
        rows: [{ k: 'Vélocité', v: '18 pts/sem' }, { k: 'Lead time P50', v: '6 j' }, { k: 'Reouvertures', v: 3 }] } },
    ],
  });
})();
