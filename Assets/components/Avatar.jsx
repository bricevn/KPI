// Avatar — pastille d'initiales, teinte stable par index (palette --av-1..6).
// AvatarStack — chevauchement pour un groupe.
// props Avatar : { name: string, index?: number, size?: number }
// props AvatarStack : { people: string[] }
(function () {
  const { createElement: h } = React;

  function initials(name) {
    return (name || '').trim().split(/\s+/).map((s) => s[0] || '').slice(0, 2).join('').toUpperCase();
  }

  function Avatar({ name = '', index = 0, size }) {
    const style = { background: 'var(--av-' + ((index % 6) + 1) + ')' };
    if (size) { style.width = size + 'px'; style.height = size + 'px'; }
    return h('span', { className: 'avatar', style, title: name }, initials(name));
  }

  function AvatarStack({ people = [] }) {
    return h('span', { className: 'av-stack' },
      people.map((p, i) => h(Avatar, { key: i, name: p, index: i })));
  }

  window.KPIGallery.register({
    name: 'Avatar', category: 'Data', render: Avatar,
    notes: 'Teinte dérivée de l’index (palette colorblind-aware --av-1..6). Initiales dérivées du nom.',
    variants: [
      { label: 'Défaut', props: { name: 'Alice Martin', index: 0 } },
      { label: 'Autre teinte', props: { name: 'Bruno Payet', index: 2 } },
      { label: 'Grand (40px)', props: { name: 'Chloé Nkosi', index: 3, size: 40 } },
    ],
  });
  window.KPIGallery.register({
    name: 'AvatarStack', category: 'Data', render: AvatarStack,
    notes: 'Chevauchement pour représenter un groupe d’assignés.',
    variants: [
      { label: 'Groupe', props: { people: ['Alice Martin', 'Bruno Payet', 'Chloé Nkosi', 'Driss Alaoui'] } },
    ],
  });
})();
