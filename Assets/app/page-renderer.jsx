// page-renderer — cœur du dashboard modulaire. Rend une PAGE (modèle JSON) en résolvant
// chaque widget : type -> window.KPI[type] (composant pur) et data -> window.KPIData[key]
// (adaptateur, seule couche qui lit window.APP). Deux garde-fous : try/catch autour de
// l'adaptateur + WidgetBoundary (error boundary) — un widget qui plante n'efface pas la page.
// Expose window.PageRenderer, window.WidgetBoundary, window.coerceParams.
(function () {
  const { createElement: h } = React;

  // Les params du JSON sont des chaînes : on les type ('3'->3, 'true'->true ; '#fff'/'var(--x)' restent).
  function coerceParams(params) {
    const out = {};
    for (const k in params) {
      const v = params[k];
      if (typeof v !== 'string') { out[k] = v; continue; }
      if (v === 'true') out[k] = true;
      else if (v === 'false') out[k] = false;
      else if (/^-?\d+(\.\d+)?$/.test(v)) out[k] = Number(v);
      else out[k] = v;
    }
    return out;
  }

  // Error boundary : isole le crash d'un widget AU RENDER (Babel navigateur supporte les classes).
  class WidgetBoundary extends React.Component {
    constructor(props) { super(props); this.state = { err: false }; }
    static getDerivedStateFromError() { return { err: true }; }
    componentDidCatch() { /* isolé : pas de remontée */ }
    render() {
      if (this.state.err) return h('div', { className: 'widget-error' }, 'Widget « ' + this.props.type + ' » : erreur de rendu');
      return this.props.children;
    }
  }

  function PageRenderer({ page, ctx }) {
    const APP = window.APP; // relu à chaque render → réactivité aux filtres gratuite (Phase live)
    const L = (page && page.layout) || { cols: 12 };
    const cols = L.cols || 12;
    const grid = {
      display: 'grid',
      gridTemplateColumns: 'repeat(' + cols + ', 1fr)',
      gap: L.gap || 'var(--space-4)',
      alignItems: 'start',
    };
    const widgets = (page && page.widgets) || [];
    return h('div', { className: 'page-grid', style: grid },
      widgets.map((w) => {
        const cell = { gridColumn: 'span ' + Math.min(cols, (w.layout && w.layout.w) || 4) };
        const Comp = window.KPI && window.KPI[w.type];
        if (!Comp) return h('div', { key: w.id, className: 'widget-missing', style: cell }, 'Widget « ' + w.type + ' » inconnu');
        let props;
        try {
          const adapter = window.KPIData && window.KPIData[w.data];
          const base = adapter ? adapter(APP, w.params || {}, ctx || {}) : {};
          props = Object.assign({}, base, coerceParams(w.params || {}));
        } catch (e) {
          return h('div', { key: w.id, className: 'widget-error', style: cell }, 'Données « ' + w.data + ' » indisponibles');
        }
        return h('div', { key: w.id, className: 'widget-cell', style: cell },
          h(WidgetBoundary, { type: w.type }, h(Comp, props)));
      }));
  }

  window.coerceParams = coerceParams;
  window.WidgetBoundary = WidgetBoundary;
  window.PageRenderer = PageRenderer;
})();
