// kard.jsx — Cartouche KPI (handoff §13) VENDORÉE dans le produit + helper window.Icon.
// Cette branche (nav native) n'a pas de window.Icon(name,size) : on le fournit ici en icônes
// dessinées à la main dans le style Lucide (viewBox 24, stroke 2, round), au minimum celles
// utilisées par les cartouches KPI + 'expand' (affordance popup de Kard). Styles : kcard.css
// (dépend de charte-tokens.css). Exposé sur window (les scripts Babel ne partagent pas leur scope).
(function () {
  const { createElement: h } = React;
  const c = (cx, cy, r) => h('circle', { cx, cy, r });
  const p = (d) => h('path', { d });
  const pl = (points) => h('polyline', { points });
  const ln = (x1, y1, x2, y2) => h('line', { x1, y1, x2, y2 });
  // name -> () => [éléments SVG]. Repli sur 'circle-dot' si nom inconnu.
  const ICON = {
    clock: () => [c(12, 12, 9), pl('12 7 12 12 15 14')],
    'badge-check': () => [c(12, 12, 9), pl('8 12 11 15 16 9')],
    'circle-check': () => [c(12, 12, 9), pl('8 12 11 15 16 9')],
    gauge: () => [p('M12 14l4-4'), p('M4.2 18a9 9 0 1 1 15.6 0')],
    'circle-dot': () => [c(12, 12, 9), c(12, 12, 1.6)],
    activity: () => [pl('3 12 7 12 10 5 14 19 17 12 21 12')],
    'alert-triangle': () => [p('M12 4 21 19 3 19Z'), ln(12, 10, 12, 14), ln(12, 17, 12, 17)],
    expand: () => [pl('15 4 20 4 20 9'), pl('9 20 4 20 4 15'), ln(20, 4, 14, 10), ln(4, 20, 10, 14)],
  };
  window.Icon = function (name, size) {
    const parts = (ICON[name] || ICON['circle-dot'])().map((el, i) => React.cloneElement(el, { key: i }));
    return h('svg', {
      width: size, height: size, viewBox: '0 0 24 24', fill: 'none',
      stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round',
    }, parts);
  };

  // --- Kard (verbatim du handoff) ---
  function Kard({ icon, iconColor, title, value, display = 'bar', ratio = 0,
                  barColor = 'var(--color-accent)', series, footer, popup, onOpen, info }) {
    const clickable = !!popup;
    const open = () => { if (clickable && onOpen) onOpen(popup); };

    let body = null;
    if (display === 'bar') {
      body = <div className="kbar"><i style={{ width: Math.max(0, Math.min(1, ratio)) * 100 + '%', background: barColor }}></i></div>;
    } else if (display === 'spark' && series && series.length) {
      const max = Math.max.apply(null, series) || 1;
      body = (
        <div className="spark">
          {series.map((v, i) => (
            <i key={i} style={{ height: (v / max * 100) + '%', background: i === series.length - 1 ? barColor : 'var(--color-surface-3)' }}></i>
          ))}
        </div>
      );
    }

    return (
      <div
        className={'kcard kcard-kpi' + (clickable ? ' clickable' : '')}
        onClick={open}
        role={clickable ? 'button' : undefined}
        tabIndex={clickable ? 0 : undefined}
        onKeyDown={clickable ? (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); open(); } } : undefined}
        title={clickable ? 'Ouvrir le détail' : undefined}
      >
        {info && window.InfoTip
          ? <span className="kinfo" onClick={(e) => e.stopPropagation()}><window.InfoTip text={info} /></span>
          : null}
        <div className="ktop">
          <span className="kchip" style={{ background: iconColor || 'var(--color-accent)' }}>{window.Icon(icon, 16)}</span>
          <span className="klab">{title}</span>
        </div>
        <div className="kbig">{value}</div>
        {body}
        {footer ? <div className="kcap">{footer}</div> : null}
      </div>
    );
  }

  Object.assign(window, { Kard });
})();
