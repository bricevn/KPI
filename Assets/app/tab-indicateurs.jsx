// tab-indicateurs.jsx — page « Indicateurs » (avant le Dashboard) : cartouches KPI (window.Kard).
// Couche MÉTIER : chaque KPI dérive une valeur (%) + une couleur de verdict (good/warn/bad selon des
// seuils) + un ratio de barre, à partir de window.APP (GitLab, filtré) et window.__CANNY__ (Canny).
// 1er lot : Acknowledge Time (Canny <4h), Patch Success (bug fermé sans retour), Bug Resolution
// (Client Bug fermé <72h). Les autres KPI (Unplanned, MTTR, Refactoring, Say/Do, Roadmap Adherence)
// viendront ensuite (labels/config à confirmer). window.TabIndicateurs exposé sur window.
(function () {
  // Verdict : valeur en % où PLUS HAUT = MIEUX → couleur token de la charte.
  const G = 85, W = 70; // seuils good / warn (bad en dessous)
  const color = (pct) => pct >= G ? 'var(--color-good)' : pct >= W ? 'var(--color-warn)' : 'var(--color-bad)';

  window.TabIndicateurs = function TabIndicateurs() {
    const A = window.APP || {};
    const CANNY = window.__CANNY__;
    const t = window.t;

    // KPI 1 — Acknowledge Time (Canny, réponse ≤ 4h ouvrées). Global Canny (hors filtres GitLab).
    const sla = (CANNY && CANNY.aggregates && CANNY.aggregates.sla) || null;
    let ack = null;
    if (sla) {
      const answered = (sla.compliant || 0) + (sla.breached || 0); // posts avec réponse
      ack = { pct: answered ? Math.round((sla.within4h || 0) / answered * 100) : 0, within4h: sla.within4h || 0, answered };
    }

    // KPI 2 — Patch Success : % des bugs FERMÉS avec 0 aller-retour QA (detail[].retours === 0).
    const det = A.detail || [];
    const bugs = det.filter((d) => (d.type === 'bug' || d.type === 'clientbug') && d.state === 'closed');
    const patchOk = bugs.filter((d) => d.retours === 0).length;
    const patch = { pct: bugs.length ? Math.round(patchOk / bugs.length * 100) : 0, ok: patchOk, total: bugs.length };

    // KPI 3 — Bug Resolution : % des Client Bug FERMÉS résolus en ≤ 72h (calendaire).
    const cbugs = det.filter((d) => d.type === 'clientbug' && d.state === 'closed' && d.createdAt && d.closedAt);
    const within72 = cbugs.filter((d) => (new Date(d.closedAt).getTime() - new Date(d.createdAt).getTime()) / 3600000 <= 72).length;
    const bugres = { pct: cbugs.length ? Math.round(within72 / cbugs.length * 100) : 0, within: within72, total: cbugs.length };

    const K = window.Kard;
    const cards = [];
    if (ack) cards.push(
      <K key="ack" icon="clock" iconColor={color(ack.pct)} title={t('kpi.ackTitle')} value={ack.pct + ' %'}
        display="bar" ratio={ack.pct / 100} barColor={color(ack.pct)}
        footer={<span><b>{ack.within4h}</b> ≤4h · <b>{ack.answered}</b> {t('kpi.answered')}</span>} />
    );
    cards.push(
      <K key="patch" icon="badge-check" iconColor={color(patch.pct)} title={t('kpi.patchTitle')} value={patch.total ? patch.pct + ' %' : '—'}
        display="bar" ratio={patch.pct / 100} barColor={color(patch.pct)}
        footer={<span><b>{patch.ok}</b> {t('kpi.zeroReturn')} · <b>{patch.total}</b> {t('kpi.bugsClosed')}</span>} />
    );
    cards.push(
      <K key="bugres" icon="gauge" iconColor={color(bugres.pct)} title={t('kpi.bugResTitle')} value={bugres.total ? bugres.pct + ' %' : '—'}
        display="bar" ratio={bugres.pct / 100} barColor={color(bugres.pct)}
        footer={<span><b>{bugres.within}</b> ≤72h · <b>{bugres.total}</b> {t('kpi.clientBugs')}</span>} />
    );

    return (
      <div className="kpi-root" style={{ padding: 'var(--space-5, 20px)' }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 'var(--space-4, 16px)', maxWidth: 1100 }}>
          {cards}
        </div>
        {!ack && <p style={{ marginTop: 16, color: 'var(--color-ink-3, #888)', fontSize: 'var(--text-caption, 12px)' }}>{t('kpi.noCanny')}</p>}
      </div>
    );
  };
})();
