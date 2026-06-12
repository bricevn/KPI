// Options tab — apparence, régénération des données, configuration (formulaire).
(function () {
  const { useState } = React;
  const A = window.APP;
  const PROD_LABELS = ['Code In Progress', 'Code review', 'Code pre-review', 'QA Backlog', 'QA InProgress', 'To Fix', 'PO Validation', 'UI/UX To Do', 'UI/UX in progress', 'UI/UX Done'];
  const TRACKED_DEFAULT = ['Code In Progress', 'Code review', 'QA Backlog', 'QA InProgress', 'To Fix', 'PO Validation'];
  const ACCENTS = [['#2b7fff', 'Bleu'], ['#7A5AE0', 'Violet'], ['#0f9e8e', 'Teal'], ['#e0792e', 'Ambre'], ['#d6336c', 'Magenta']];
  const NUMFONTS = [['grotesk', 'Grotesk'], ['mono', 'Mono'], ['system', 'Système']];
  const DRILL_LAYOUTS = [['modal', 'Centré'], ['panel', 'Panneau'], ['full', 'Plein écran']];

  function Toggle({ on, onClick }) {
    return <button onClick={onClick} style={{ width: 42, height: 24, borderRadius: 999, border: 0, cursor: 'pointer', background: on ? 'var(--accent)' : 'var(--panel-3)', position: 'relative', transition: 'background .15s' }}>
      <span style={{ position: 'absolute', top: 3, left: on ? 21 : 3, width: 18, height: 18, borderRadius: '50%', background: '#fff', transition: 'left .15s' }}></span>
    </button>;
  }

  window.TabOptions = function TabOptions({ theme, setTheme, appearance }) {
    const { accent, setAccent, numFont, setNumFont, compact, setCompact, drillLayout, setDrillLayout } = appearance;
    const [tracked, setTracked] = useState(() => new Set(TRACKED_DEFAULT));
    const [showToken, setShowToken] = useState(false);
    const [selfSigned, setSelfSigned] = useState(false);
    const toggleLabel = (l) => setTracked(s => { const n = new Set(s); n.has(l) ? n.delete(l) : n.add(l); return n; });
    const teams = { 'Core': ['antoine', 'brice', 'carlo'], 'QA': ['denis', 'ash'], 'Front': ['kabbas', 'antoine'] };

    return (
      <div style={{ maxWidth: 860 }}>
        <div className="opt-sec">
          <h3>Apparence</h3>
          <p className="lead">Personnalisez l'affichage. Vos choix sont mémorisés sur cet appareil.</p>
          <div className="opt-row">
            <div className="lbl">Thème<span>Clair, sombre ou automatique</span></div>
            <div className="seg-lg">
              {['auto', 'light', 'dark'].map(t => <button key={t} className={theme === t ? 'on' : ''} onClick={() => setTheme(t)}>{t === 'auto' ? 'Auto' : t === 'light' ? 'Clair' : 'Sombre'}</button>)}
            </div>
          </div>
          <div className="opt-row">
            <div className="lbl">Couleur d'accent<span>Teinte des éléments actifs et boutons</span></div>
            <div className="swatches">
              {ACCENTS.map(([c, name]) => (
                <button key={c} className={'swatch' + (accent === c ? ' on' : '')} title={name}
                  style={{ background: c }} onClick={() => setAccent(c)}>{accent === c ? '✓' : ''}</button>
              ))}
            </div>
          </div>
          <div className="opt-row">
            <div className="lbl">Police des chiffres<span>Style des nombres et titres chiffrés</span></div>
            <div className="seg-lg">
              {NUMFONTS.map(([k, lbl]) => <button key={k} className={numFont === k ? 'on' : ''} onClick={() => setNumFont(k)}>{lbl}</button>)}
            </div>
          </div>
          <div className="opt-row">
            <div className="lbl">Mode compact<span>Densité élevée : plus d'informations à l'écran</span></div>
            <Toggle on={compact} onClick={() => setCompact(c => !c)} />
          </div>
          <div className="opt-row">
            <div className="lbl">Affichage du détail<span>Mise en page des popups de détail (issues, vélocité)</span></div>
            <div className="seg-lg">
              {DRILL_LAYOUTS.map(([k, lbl]) => <button key={k} className={drillLayout === k ? 'on' : ''} onClick={() => setDrillLayout(k)}>{lbl}</button>)}
            </div>
          </div>
        </div>

        <div className="opt-sec">
          <h3>Régénération des données</h3>
          <p className="lead">Relance une extraction GitLab. Dernière extraction le {A.meta.extracted}.</p>
          <div className="opt-row">
            <div className="lbl">Portée<span>Toutes les milestones sont conservées (merge intelligent)</span></div>
            <select className="" style={{ border: '1px solid var(--line)', borderRadius: 9, background: 'var(--panel-2)', color: 'var(--ink)', padding: '9px 12px', font: '14px system-ui' }}>
              <option>2026-R2 (milestone courante)</option>
              <option>Tout le projet</option>
            </select>
            <button className="btn btn-primary">{window.ICONS.refresh} Rafraîchir</button>
          </div>
        </div>

        <div className="opt-sec">
          <h3>Configuration</h3>
          <p className="lead">Édition de <code>appsettings.json</code>. « Sauvegarder » écrit le fichier et recharge la config à chaud.</p>
          <div style={{ fontSize: 11, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '.05em', color: 'var(--ink-faint)', margin: '6px 0 12px' }}>GitLab</div>
          <div className="field-grid">
            <div className="field"><label>Base URL</label><input placeholder="https://gitlab.exemple.com" /></div>
            <div className="field"><label>Project ID</label><input placeholder="namespace/projet ou ID" /></div>
            <div className="field"><label>Private Token</label>
              <div style={{ position: 'relative' }}>
                <input type={showToken ? 'text' : 'password'} defaultValue="glpat-xxxxxxxxxxxx" style={{ width: '100%', paddingRight: 38 }} />
                <button onClick={() => setShowToken(s => !s)} style={{ position: 'absolute', right: 6, top: 6, border: 0, background: 'transparent', cursor: 'pointer', color: 'var(--ink-faint)', fontSize: 12 }}>{showToken ? 'Cacher' : 'Voir'}</button>
              </div>
            </div>
            <div className="field"><label>Request timeout (s)</label><input defaultValue="60" /></div>
          </div>
          <div className="opt-row" style={{ borderTop: '1px solid var(--line-2)' }}>
            <div className="lbl">Certificats auto-signés<span>Autoriser les certificats self-signed</span></div>
            <Toggle on={selfSigned} onClick={() => setSelfSigned(s => !s)} />
          </div>

          <div style={{ fontSize: 11, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '.05em', color: 'var(--ink-faint)', margin: '16px 0 10px' }}>Tracked labels</div>
          <div className="checklist">
            {PROD_LABELS.map(l => <label key={l} className={tracked.has(l) ? 'on' : ''} onClick={() => toggleLabel(l)}><input type="checkbox" readOnly checked={tracked.has(l)} style={{ pointerEvents: 'none' }} />Prod::{l}</label>)}
          </div>

          <div style={{ fontSize: 11, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '.05em', color: 'var(--ink-faint)', margin: '18px 0 10px' }}>Équipes</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 12 }}>
            {Object.entries(teams).map(([name, ids]) => (
              <div key={name} style={{ border: '1px solid var(--line)', borderRadius: 12, padding: '12px 14px' }}>
                <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 8 }}>{name}</div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {ids.map(id => <span key={id} className="tag"><window.Avatar pid={id} size={18} />{A.peopleById[id].name}<span className="x">×</span></span>)}
                </div>
              </div>
            ))}
          </div>

          <div style={{ display: 'flex', gap: 10, marginTop: 20 }}>
            <button className="btn btn-primary btn-sm">+ Ajouter une équipe</button>
            <div style={{ marginLeft: 'auto', display: 'flex', gap: 10 }}>
              <button className="btn btn-danger btn-sm">Annuler</button>
              <button className="btn btn-ok btn-sm">Sauvegarder</button>
            </div>
          </div>
        </div>
      </div>
    );
  };
})();
