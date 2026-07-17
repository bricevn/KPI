// tab-page-editor — éditeur VISUEL des pages modulaires PAR UTILISATEUR. Accessible à TOUT utilisateur
// connecté ; agit uniquement sur SES pages (écrites sous son propre compte). Créer/éditer/supprimer des
// pages, composer des widgets (type + source de données + largeur), réordonner, avec APERÇU LIVE, puis
// POST /api/my-pages. Réutilise les classes UI existantes (.opt-*, .field, .btn). window.TabPageEditor.
(function () {
  const { useState } = React;
  const CATALOG = () => window.KPIWidgets || {};
  const DATALABEL = (k) => (window.KPIDataCatalog && window.KPIDataCatalog[k]) || k;
  const ICON_KEYS = ['dashboard', 'charts', 'anomalies', 'issues', 'calendar', 'velocity', 'comparison', 'options'];
  const clone = (x) => JSON.parse(JSON.stringify(x || null));

  function nextWid(p) {
    let max = 0;
    (p.widgets || []).forEach((w) => { const m = /^w(\d+)$/.exec(w.id || ''); if (m) max = Math.max(max, +m[1]); });
    return 'w' + (max + 1);
  }

  window.TabPageEditor = function TabPageEditor() {
    // Modèle « tout par utilisateur » : chacun n'édite QUE ses propres pages (window.__USER_PAGES__),
    // sauvegardées par compte (/api/my-pages). Pas de couche partagée/admin.
    const srcPages = () => ((window.__USER_PAGES__) || []);

    const [pages, setPages] = useState(() => clone(srcPages()) || []);
    const [sel, setSel] = useState(() => { const s = srcPages(); return (s[0] && s[0].id) || null; });
    const [status, setStatus] = useState(''); // '' | 'saving' | 'saved' | 'err:<msg>'
    const page = pages.find((p) => p.id === sel) || null;

    const mutate = (fn) => { setStatus(''); setPages((ps) => { const n = clone(ps); const p = n.find((x) => x.id === sel); if (p) fn(p); return n; }); };
    const types = Object.keys(CATALOG());

    const addPage = () => {
      const ids = new Set(pages.map((p) => p.id));
      let id = 'page', i = 2; while (ids.has(id)) id = 'page' + (i++);
      const np = { id, kind: 'modular', nav: { label: 'Nouvelle page', labelKey: '', icon: 'dashboard', order: 100, showFilters: true, badgeSource: '' }, layout: { cols: 12, gap: 'var(--space-4)', rowUnit: 88 }, widgets: [] };
      setPages((ps) => [...ps, np]); setSel(id); setStatus('');
    };
    const delPage = () => { if (!page) return; const next = pages.filter((p) => p.id !== sel); setPages(next); setSel((next[0] && next[0].id) || null); persist(next); };
    const addWidget = () => mutate((p) => {
      const type = types[0] || 'KpiCard'; const spec = CATALOG()[type] || {};
      p.widgets.push({ id: nextWid(p), type, data: (spec.data && spec.data[0]) || '', layout: { w: spec.defaultW || 4, h: 1, x: -1, y: -1 }, params: {} });
    });
    const setWidgetType = (wi, type) => mutate((p) => { const w = p.widgets[wi]; w.type = type; const spec = CATALOG()[type] || {}; if (!spec.data || spec.data.indexOf(w.data) < 0) w.data = (spec.data && spec.data[0]) || ''; if (spec.defaultW) w.layout.w = spec.defaultW; });
    const move = (wi, d) => mutate((p) => { const j = wi + d; if (j < 0 || j >= p.widgets.length) return; const a = p.widgets; const t = a[wi]; a[wi] = a[j]; a[j] = t; });

    // Persiste une liste de pages (POST /api/my-pages) puis recharge. Partagé par « Enregistrer » et la
    // suppression (qui doit persister immédiatement, y compris quand la liste devient vide).
    const persist = (list) => {
      setStatus('saving');
      fetch('/api/my-pages', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ schemaVersion: 1, defaultPageId: '', pages: list }) })
        .then((r) => r.json()).then((j) => {
          if (j.ok) { setStatus('saved'); setTimeout(() => window.location.reload(), 600); }
          else setStatus('err:' + (j.error || 'inconnue'));
        }).catch(() => setStatus('err:réseau'));
    };
    const save = () => persist(pages);

    const selStyle = { border: '1px solid var(--line)', borderRadius: 8, background: 'var(--panel-2)', color: 'var(--ink)', font: '13px system-ui,sans-serif', padding: '6px 9px', outline: 'none' };
    const num = (v, on, min, max) => <input type="number" value={v} min={min} max={max} onChange={(e) => on(Math.max(min, Math.min(max, +e.target.value || min)))} style={{ ...selStyle, width: 64 }} />;

    return (
      <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start' }}>
        {/* Colonne gauche : liste des pages */}
        <div className="opt-sec" style={{ width: 240, flex: 'none', marginBottom: 0 }}>
          <h3>Pages</h3>
          <p className="lead">Vos pages personnelles.</p>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginBottom: 12 }}>
            {pages.length ? pages.map((p) =>
              <button key={p.id} className={'gx-item' + (p.id === sel ? ' on' : '')} style={{ borderRadius: 9, padding: 9, textAlign: 'left', border: 0, cursor: 'pointer', background: p.id === sel ? 'var(--accent-soft)' : 'transparent', color: p.id === sel ? 'var(--accent)' : 'var(--ink-dim)', fontWeight: p.id === sel ? 600 : 500 }} onClick={() => setSel(p.id)}>
                {(p.nav && p.nav.label) || p.id} <span style={{ color: 'var(--ink-faint)', fontSize: 11 }}>· {p.widgets ? p.widgets.length : 0} widgets</span>
              </button>
            ) : <div className="opt-note">Aucune page. Créez-en une.</div>}
          </div>
          <button className="btn btn-sm" onClick={addPage}>{window.ICONS ? window.ICONS.plus : '+'} Nouvelle page</button>
        </div>

        {/* Colonne droite : éditeur de la page sélectionnée + aperçu */}
        <div style={{ flex: 1, minWidth: 0 }}>
          {!page ? <div className="opt-sec"><p className="lead">Sélectionnez une page à gauche, ou créez-en une.</p></div> : (
            <React.Fragment>
              <div className="opt-sec" style={{ marginBottom: 14 }}>
                <h3>Réglages de la page</h3>
                <div className="opt-row"><div className="lbl">Nom<span>Titre affiché dans la nav.</span></div>
                  <input value={(page.nav && page.nav.label) || ''} onChange={(e) => mutate((p) => { p.nav.label = e.target.value; })} style={{ ...selStyle, flex: 1, minWidth: 0 }} /></div>
                <div className="opt-row"><div className="lbl">Icône</div>
                  <select value={(page.nav && page.nav.icon) || 'dashboard'} onChange={(e) => mutate((p) => { p.nav.icon = e.target.value; })} style={selStyle}>
                    {ICON_KEYS.map((k) => <option key={k} value={k}>{k}</option>)}</select></div>
                <div className="opt-row"><div className="lbl">Ordre<span>Position dans la nav (croissant).</span></div>{num((page.nav && page.nav.order) || 100, (v) => mutate((p) => { p.nav.order = v; }), 0, 999)}</div>
                <div className="opt-row"><div className="lbl">Colonnes<span>Largeur de la grille (12 recommandé).</span></div>{num((page.layout && page.layout.cols) || 12, (v) => mutate((p) => { p.layout.cols = v; }), 1, 24)}</div>
              </div>

              <div className="opt-sec" style={{ marginBottom: 14 }}>
                <h3>Widgets</h3>
                <p className="lead">Chaque widget = un composant + une source de données + une largeur (colonnes).</p>
                {(page.widgets || []).map((w, wi) => {
                  const spec = CATALOG()[w.type] || {};
                  const dataOpts = spec.data || (w.data ? [w.data] : []);
                  return (
                    <div key={w.id} className="opt-maprow" style={{ gap: 8, flexWrap: 'wrap' }}>
                      <select value={w.type} onChange={(e) => setWidgetType(wi, e.target.value)} style={selStyle}>
                        {types.map((t) => <option key={t} value={t}>{(CATALOG()[t] || {}).label || t}</option>)}
                      </select>
                      <select value={w.data} onChange={(e) => mutate((p) => { p.widgets[wi].data = e.target.value; })} style={{ ...selStyle, flex: 1, minWidth: 140 }}>
                        {dataOpts.map((d) => <option key={d} value={d}>{DATALABEL(d)}</option>)}
                      </select>
                      <span style={{ fontSize: 11, color: 'var(--ink-faint)' }}>larg.</span>
                      {num((w.layout && w.layout.w) || 4, (v) => mutate((p) => { p.widgets[wi].layout.w = v; }), 1, (page.layout && page.layout.cols) || 12)}
                      <button className="btn btn-sm" title="Monter" onClick={() => move(wi, -1)}>↑</button>
                      <button className="btn btn-sm" title="Descendre" onClick={() => move(wi, 1)}>↓</button>
                      <button className="btn btn-sm" title="Retirer" onClick={() => mutate((p) => { p.widgets.splice(wi, 1); })}>✕</button>
                    </div>
                  );
                })}
                <div style={{ marginTop: 10 }}><button className="btn btn-sm" onClick={addWidget}>{window.ICONS ? window.ICONS.plus : '+'} Ajouter un widget</button></div>
              </div>

              <div className="opt-sec" style={{ marginBottom: 14 }}>
                <h3>Aperçu</h3>
                <p className="lead">Rendu live avec les données réelles.</p>
                <window.PageRenderer page={page} ctx={{ t: window.t, icon: (k) => (window.ICONS ? window.ICONS[k] : null) }} />
              </div>

              <div className="opt-row" style={{ borderTop: 0 }}>
                <button className="btn btn-primary" onClick={save} disabled={status === 'saving'}>{status === 'saving' ? 'Enregistrement…' : 'Enregistrer'}</button>
                <button className="btn" onClick={delPage} style={{ color: 'var(--c-bad)' }}>Supprimer cette page</button>
                {status === 'saved' && <span className="opt-note" style={{ color: 'var(--c-good,#2f9e44)', margin: 0 }}>Enregistré — rechargement…</span>}
                {status.indexOf('err:') === 0 && <span className="opt-note" style={{ color: 'var(--c-bad)', margin: 0 }}>Erreur : {status.slice(4)}</span>}
              </div>
            </React.Fragment>
          )}
        </div>
      </div>);
  };
})();
