// Button — hiérarchie à 3 niveaux (charte §11).
//   primary   : fond accent — validations, actions courantes
//   secondary : fond complémentaire chromatique de l'accent — suppression, opposition
//   tertiary  : sans fond, bordure + focus accent — actions neutres
// Styles : charte-buttons.css (+ --accent-complement via charte-complement.js).
(function () {
  const { createElement: h } = React;

  function Button({ variant = 'primary', disabled = false, icon = null, children, onClick }) {
    const cls = 'btn btn-' + variant;
    return h('button', { className: cls, disabled, onClick }, icon, children);
  }

  window.KPIGallery.register({
    name: 'Button',
    category: 'Actions',
    render: Button,
    notes: 'Hiérarchie 3 niveaux (charte §11). Le secondaire est le complémentaire chromatique de l’accent (change avec l’accent). États : survol / focus (anneau) / désactivé.',
    variants: [
      { label: 'Primaire', props: { variant: 'primary', children: 'Enregistrer' } },
      { label: 'Secondaire', props: { variant: 'secondary', children: 'Supprimer' } },
      { label: 'Tertiaire', props: { variant: 'tertiary', children: 'Annuler' } },
      { label: 'Désactivé', props: { variant: 'primary', disabled: true, children: 'Indisponible' } },
    ],
  });
})();
