// ============================================================================
// registry.js — registre des composants isolés (base du système modulaire).
// Chaque composant s'enregistre lui-même via window.KPIGallery.register({...}),
// la galerie (/gallery) lit ce registre pour monter chaque composant en isolation.
//
// CONTRAT D'UN COMPOSANT (cf. Assets/components/README.md) :
//   - fonction PURE de ses props : ne lit JAMAIS window.APP / window.__DATA__ ;
//     toutes les données arrivent par props (c'est ce qui le rend isolable et
//     réutilisable dans n'importe quelle page).
//   - style via les tokens de la charte (charte-tokens.css) + classes dédiées.
//
// register(spec) :
//   { name, category, render, notes?, variants: [{ label, props }] }
// ============================================================================
(function () {
  const reg = [];
  // Namespace de composition : tout composant enregistré est aussi exposé sous window.KPI.<Nom>,
  // pour qu'un composant puisse en réutiliser un autre (ex. KpiCard compose ProgressBar + Sparkline).
  window.KPI = window.KPI || {};
  window.KPIGallery = {
    register(spec) {
      if (!spec || !spec.name || typeof spec.render !== 'function') {
        console.warn('KPIGallery.register : spec invalide', spec);
        return;
      }
      window.KPI[spec.name] = spec.render;
      reg.push(spec);
    },
    all() { return reg.slice(); },
    get(name) { return reg.find((s) => s.name === name) || null; },
    categories() { return [...new Set(reg.map((s) => s.category || 'Autres'))]; },
  };
})();
