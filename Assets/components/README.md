# Composants isolés — bibliothèque modulaire KPI

Base du futur système de pages configurables : chaque **élément** d'une page est un
composant **isolé et réutilisable**, testé seul dans la galerie (`/gallery`, dev only).

## Contrat d'un composant

1. **Fonction pure de ses props.** Un composant ne lit JAMAIS `window.APP` ni
   `window.__DATA__` : toutes les données arrivent par **props**. C'est ce qui le rend
   isolable (montable dans la galerie) et réutilisable (montable dans n'importe quelle page).
2. **Style via les tokens de la charte** (`Assets/design/charte-tokens.css`) et des classes
   dédiées (`components.css`, `charte-buttons.css`). Aucune couleur/taille en dur.
3. **Auto-enregistrement** dans le registre pour apparaître dans la galerie :

   ```js
   window.KPIGallery.register({
     name: 'MonComposant',
     category: 'Data',            // regroupement dans la galerie
     render: MonComposant,        // la fonction composant
     notes: 'À quoi il sert / contraintes.',
     variants: [                  // cas de test montés en isolation
       { label: 'Défaut', props: { /* ... */ } },
     ],
   });
   ```

## Ajouter un composant

1. Créer `Assets/components/MonComposant.jsx` (voir `Button.jsx` comme gabarit).
2. Ajouter ses styles éventuels dans `components.css` (tokens uniquement).
3. L'ajouter au tableau `ComponentFiles` de `Views/DashboardAssets.cs` (liste partagée galerie + dashboard live).
4. `dotnet build`, ouvrir `/gallery`, valider en clair + sombre + accents.

## Fichiers

- `registry.js` — le registre (`window.KPIGallery`).
- `charte-complement.js` — calcule `--accent-complement` (bouton secondaire).
- `gallery.jsx` — le banc d'isolation (galerie).
- `Button.jsx`, `StatusBadge.jsx`, `Avatar.jsx` — premiers composants.
- `components.css` — styles des composants (hors boutons).

> Contrainte projet : pas d'étape de build (JSX transpilé par Babel dans le navigateur,
> comme le reste de l'app). Les composants sont des IIFE qui s'enregistrent au chargement.
