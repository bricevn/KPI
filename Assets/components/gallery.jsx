// gallery.jsx — banc d'isolation : monte chaque composant enregistré, seul,
// avec bascule thème (clair/sombre), accent, densité. C'est l'« environnement de
// test pour chaque composant ». Lit window.KPIGallery (registry.js).
(function () {
  const { useState, useEffect } = React;
  const ACCENTS = [['#2b7fff', 'Bleu'], ['#0f9e8e', 'Teal'], ['#8957e5', 'Violet'], ['#d97706', 'Ambre'], ['#e5484d', 'Rouge']];

  function Gallery() {
    const specs = (window.KPIGallery && window.KPIGallery.all()) || [];
    const cats = (window.KPIGallery && window.KPIGallery.categories()) || [];
    const [mode, setMode] = useState('components'); // 'components' | 'pages'
    const [sel, setSel] = useState(specs.length ? specs[0].name : null);
    const [theme, setTheme] = useState('dark');
    const [accent, setAccent] = useState('#2b7fff');
    const [compact, setCompact] = useState(false);

    // Recalcule le complémentaire de l'accent (bouton secondaire) au boot / accent / thème.
    useEffect(() => { if (window.updateAccentComplement) window.updateAccentComplement(accent); }, [accent, theme]);

    const current = specs.find((s) => s.name === sel);
    const rootStyle = {
      '--accent': accent, '--accent-2': accent, '--accent-hue': accent,
      '--accent-soft': 'color-mix(in srgb, ' + accent + ' 15%, transparent)',
      '--disp-font': "'Space Grotesk'",
    };

    return (
      <div className={'app kpi-root gx' + (compact ? ' compact' : '')} data-theme={theme} style={rootStyle}>
        <aside className="gx-sb">
          <div className="gx-brand">KPI · Design</div>
          <div className="gx-seg gx-modes">
            {[['components', 'Composants'], ['pages', 'Pages']].map(([k, l]) => (
              <button key={k} className={mode === k ? 'on' : ''} onClick={() => setMode(k)}>{l}</button>
            ))}
          </div>
          {mode === 'components' && cats.map((c) => (
            <div key={c} className="gx-cat">
              <div className="gx-cat-h">{c}</div>
              {specs.filter((s) => (s.category || 'Autres') === c).map((s) => (
                <button key={s.name} className={'gx-item' + (s.name === sel ? ' on' : '')} onClick={() => setSel(s.name)}>{s.name}</button>
              ))}
            </div>
          ))}
        </aside>

        <main className="gx-main">
          <div className="gx-toolbar">
            <div className="gx-seg">
              {[['light', 'Clair'], ['dark', 'Sombre']].map(([k, l]) => (
                <button key={k} className={theme === k ? 'on' : ''} onClick={() => setTheme(k)}>{l}</button>
              ))}
            </div>
            <div className="gx-swatches">
              {ACCENTS.map(([c, name]) => (
                <button key={c} className={'gx-sw' + (accent === c ? ' on' : '')} title={name} style={{ background: c }} onClick={() => setAccent(c)} />
              ))}
            </div>
            <label className="gx-toggle"><input type="checkbox" checked={compact} onChange={(e) => setCompact(e.target.checked)} /> Compact</label>
          </div>

          {mode === 'pages' ? (
            <div className="gx-canvas">
              <h1 className="gx-title">Page modulaire — démo</h1>
              <p className="gx-notes">Rendue par window.PageRenderer depuis un modèle JSON (window.__DEMO_PAGE) : chaque widget est résolu par type → window.KPI et alimenté par un adaptateur window.KPIData (fixtures). Preuve du contrat renderer + binding + isolation d'erreur, sans pipeline réel.</p>
              {window.PageRenderer
                ? React.createElement(window.PageRenderer, { page: window.__DEMO_PAGE, ctx: { t: window.t || ((k) => k), icon: () => null } })
                : <div className="gx-empty">PageRenderer non chargé.</div>}
            </div>
          ) : current ? (
            <div className="gx-canvas">
              <h1 className="gx-title">{current.name}</h1>
              {current.notes && <p className="gx-notes">{current.notes}</p>}
              <div className="gx-variants">
                {(current.variants || [{ label: 'Défaut', props: {} }]).map((v, i) => (
                  <div key={i} className="gx-variant">
                    <div className="gx-variant-h">{v.label}</div>
                    <div className="gx-stage">{React.createElement(current.render, v.props)}</div>
                  </div>
                ))}
              </div>
            </div>
          ) : <div className="gx-empty">Aucun composant enregistré.</div>}
        </main>
      </div>
    );
  }

  ReactDOM.createRoot(document.getElementById('root')).render(<Gallery />);
})();
