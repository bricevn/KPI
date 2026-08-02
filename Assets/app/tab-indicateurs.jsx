// tab-indicateurs.jsx — page « Indicateurs » (avant le Dashboard) : cartouches KPI (window.Kard).
// Couche MÉTIER : chaque KPI dérive une valeur (%) + une couleur de verdict + un ratio de barre, depuis
// window.APP (GitLab, filtré par les pills) et window.__CANNY__ (Canny, global). Ordre = liste produit.
// Regression Rate volontairement OMIS (à ignorer pour l'instant).
//
// ⚠️ Libellés GitLab NON confirmés (données chiffrées) → matchés par MOT-CLÉ (robuste quel que soit le
//    préfixe Prio::/Type::) : 'critical' (MTTR) et 'refactor' (Refactoring). À figer quand confirmés.
//    'unplanned' et 'contractual' = labels transversaux (match exact).
(function () {
  // Verdict : plus HAUT = mieux (up) ou plus BAS = mieux (down, seuils = maxima acceptables).
  const colorUp = (pct) => pct >= 85 ? 'var(--color-good)' : pct >= 70 ? 'var(--color-warn)' : 'var(--color-bad)';
  const colorDown = (pct, good, warn) => pct <= good ? 'var(--color-good)' : pct <= warn ? 'var(--color-warn)' : 'var(--color-bad)';
  const hasKw = (d, kw) => (d.labels || []).some((l) => String(l).toLowerCase().indexOf(kw) >= 0);
  const hasExact = (d, name) => (d.labels || []).some((l) => String(l).toLowerCase() === name);
  const hours = (a, b) => (new Date(b).getTime() - new Date(a).getTime()) / 3600000;

  window.TabIndicateurs = function TabIndicateurs() {
    const A = window.APP || {};
    const CANNY = window.__CANNY__ || null;
    const agg = (CANNY && CANNY.aggregates) || {};
    const t = window.t;
    const det = A.detail || [];
    const K = window.Kard;

    const cards = [];
    const F = (a, aw, b, bw) => <span><b>{a}</b> {aw} · <b>{b}</b> {bw}</span>;
    function card(key, icon, title, pct, has, barColor, footer) {
      // Sans données : couleur NEUTRE (pas de faux verdict good/bad) + barre vide.
      const col = has ? barColor : 'var(--color-neutral)';
      cards.push(<K key={key} icon={icon} iconColor={col} title={title}
        value={has ? pct + ' %' : '—'} display="bar" ratio={has ? (pct || 0) / 100 : 0} barColor={col} footer={footer} />);
    }

    // 1. Roadmap Adherence — sujet CONTRACTUAL fait (GitLab issues closed + Canny posts complete).
    const glC = det.filter((d) => hasExact(d, 'contractual'));
    const glCdone = glC.filter((d) => d.state === 'closed').length;
    let cnC = 0, cnCdone = 0;
    if (CANNY) {
      const ids = (CANNY.tags || []).filter((tg) => String(tg.name).toLowerCase() === 'contractual').map((tg) => tg.id);
      const posts = (CANNY.posts || []).filter((p) => (p.tagIds || []).some((id) => ids.indexOf(id) >= 0));
      cnC = posts.length; cnCdone = posts.filter((p) => p.valide || p.status === 'complete').length;
    }
    const raT = glC.length + cnC, raD = glCdone + cnCdone;
    const raPct = raT ? Math.round(raD / raT * 100) : 0;
    card('roadmap', 'circle-check', t('kpi.roadmapTitle'), raPct, raT > 0, colorUp(raPct),
      F(glCdone + '/' + glC.length, 'GitLab', cnCdone + '/' + cnC, 'Canny'));

    // 2. Acknowledge Time — réponse Canny ≤ 4h ouvrées.
    if (agg.sla) {
      const answered = (agg.sla.compliant || 0) + (agg.sla.breached || 0);
      const pct = answered ? Math.round((agg.sla.within4h || 0) / answered * 100) : 0;
      card('ack', 'clock', t('kpi.ackTitle'), pct, answered > 0, colorUp(pct), F(agg.sla.within4h || 0, '≤4h', answered, t('kpi.answered')));
    }

    // 3. Unplanned Work — part des issues « Unplanned » (cible < 15%, plus bas mieux).
    const unpl = det.filter((d) => hasExact(d, 'unplanned')).length;
    const upPct = det.length ? Math.round(unpl / det.length * 100) : 0;
    card('unplanned', 'alert-triangle', t('kpi.unplannedTitle'), upPct, det.length > 0, colorDown(upPct, 15, 25),
      F(unpl, t('kpi.unplannedIssues'), det.length, t('kpi.ofIssues')));

    // 4. Patch Success — bugs fermés avec 0 aller-retour QA (retours === 0).
    const bugs = det.filter((d) => (d.type === 'bug' || d.type === 'clientbug') && d.state === 'closed');
    const patchOk = bugs.filter((d) => d.retours === 0).length;
    const patchPct = bugs.length ? Math.round(patchOk / bugs.length * 100) : 0;
    card('patch', 'badge-check', t('kpi.patchTitle'), patchPct, bugs.length > 0, colorUp(patchPct),
      F(patchOk, t('kpi.zeroReturn'), bugs.length, t('kpi.bugsClosed')));

    // 5. Bug Resolution — Client Bug fermés en ≤ 72h.
    const cb = det.filter((d) => d.type === 'clientbug' && d.state === 'closed' && d.createdAt && d.closedAt);
    const cbOk = cb.filter((d) => hours(d.createdAt, d.closedAt) <= 72).length;
    const cbPct = cb.length ? Math.round(cbOk / cb.length * 100) : 0;
    card('bugres', 'gauge', t('kpi.bugResTitle'), cbPct, cb.length > 0, colorUp(cbPct), F(cbOk, '≤72h', cb.length, t('kpi.clientBugs')));

    // 6. MTTR — Critical + Client Bug fermés en ≤ 48h.
    const crit = det.filter((d) => d.state === 'closed' && d.createdAt && d.closedAt
      && hasKw(d, 'critical') && (d.type === 'clientbug' || hasKw(d, 'client bug')));
    const critOk = crit.filter((d) => hours(d.createdAt, d.closedAt) <= 48).length;
    const mttrPct = crit.length ? Math.round(critOk / crit.length * 100) : 0;
    card('mttr', 'clock', t('kpi.mttrTitle'), mttrPct, crit.length > 0, colorUp(mttrPct), F(critOk, '≤48h', crit.length, t('kpi.critBugs')));

    // 7. Refactoring — part des issues « Refactor » (cible < 20%, plus bas mieux).
    const refac = det.filter((d) => hasKw(d, 'refactor')).length;
    const rfPct = det.length ? Math.round(refac / det.length * 100) : 0;
    card('refactor', 'activity', t('kpi.refactorTitle'), rfPct, det.length > 0, colorDown(rfPct, 20, 30),
      F(refac, t('kpi.refactorIssues'), det.length, t('kpi.ofIssues')));

    // 8. Say/Do Ratio — roadmap Canny livré / prévu (cible > 85%). Proxy = validation roadmap.
    if (agg.roadmapValidation) {
      const rv = agg.roadmapValidation;
      const tot = rv.reduce((s, r) => s + (r.total || 0), 0);
      const val = rv.reduce((s, r) => s + (r.valide || 0), 0);
      const sdPct = tot ? Math.round(val / tot * 100) : 0;
      card('saydo', 'gauge', t('kpi.saydoTitle'), sdPct, tot > 0, colorUp(sdPct), F(val, t('kpi.delivered'), tot, t('kpi.planned')));
    }

    return (
      <div className="kpi-root" style={{ padding: 'var(--space-5, 20px)' }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 'var(--space-4, 16px)', maxWidth: 1180 }}>
          {cards}
        </div>
        {!CANNY && <p style={{ marginTop: 16, color: 'var(--color-ink-3, #888)', fontSize: 'var(--text-caption, 12px)' }}>{t('kpi.noCanny')}</p>}
      </div>
    );
  };
})();
