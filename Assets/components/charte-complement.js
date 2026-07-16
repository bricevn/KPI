// ============================================================================
// files/charte-complement.js — calcule --accent-complement (charte §11, bouton secondaire).
// La couleur secondaire = complémentaire chromatique de l'accent (roue +180°).
// Appeler updateAccentComplement() : au boot, à chaque changement d'accent, à chaque
// changement de thème (l'accent par défaut diffère clair/sombre).
// ============================================================================
(function () {
  const root = document.documentElement; // ou l'élément .app / .kpi-root porteur des tokens

  function hexToHsl(hex) {
    let h = (hex || '').trim().replace('#', '');
    if (h.length === 3) h = h.split('').map((c) => c + c).join('');
    const r = parseInt(h.slice(0, 2), 16) / 255, g = parseInt(h.slice(2, 4), 16) / 255, b = parseInt(h.slice(4, 6), 16) / 255;
    const mx = Math.max(r, g, b), mn = Math.min(r, g, b), d = mx - mn;
    let hue = 0; const l = (mx + mn) / 2; const s = d === 0 ? 0 : d / (1 - Math.abs(2 * l - 1));
    if (d !== 0) {
      if (mx === r) hue = ((g - b) / d) % 6;
      else if (mx === g) hue = (b - r) / d + 2;
      else hue = (r - g) / d + 4;
      hue *= 60; if (hue < 0) hue += 360;
    }
    return [hue, s, l];
  }

  window.updateAccentComplement = function (accentHex) {
    const src = accentHex || getComputedStyle(root).getPropertyValue('--accent-hue') || '#1f6feb';
    const [h, s, l] = hexToHsl(src);
    const comp = (h + 180) % 360;
    // remonte légèrement saturation/luminosité pour un plein lisible avec texte blanc
    const S = Math.max(s * 100, 65).toFixed(0);
    const L = Math.min(Math.max(l * 100, 44), 52).toFixed(0);
    root.style.setProperty('--accent-complement', `hsl(${comp.toFixed(0)} ${S}% ${L}%)`);
  };
})();
