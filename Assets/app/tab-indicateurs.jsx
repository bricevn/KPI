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

  // Ligne d'issue GitLab (épic ou directe) : pastille d'état + lien.
  const RmIssue = (it) => (
    <div className="rm-issue" key={(it.webUrl || '') + '#' + it.iid}>
      <span className={'rm-st ' + (it.state === 'closed' ? 'closed' : 'opened')}>{it.state}</span>
      <a href={it.webUrl} target="_blank" rel="noreferrer">#{it.iid} {it.title}</a>
    </div>
  );

  // Un sujet roadmap = une ligne d'ACCORDÉON (design .issue de l'onglet Issues). Repliée : titre +
  // statut Canny + compteur d'issues. Dépliée : les issues liées (issues de l'épic si épic, sinon directes).
  function RmTopic({ tp }) {
    const t = window.t;
    const [open, setOpen] = React.useState(false);
    return (
      <div className="issue">
        <div className="issue-hd" onClick={() => setOpen((o) => !o)}>
          <span className={'issue-chev' + (open ? ' open' : '')}>{window.ICONS.chevron}</span>
          <span className="rm-dot" style={{ background: tp.adherent ? 'var(--color-good)' : 'var(--color-bad)' }}></span>
          <a className="issue-ttl rm-title" href={tp.url} target="_blank" rel="noreferrer" onClick={(e) => e.stopPropagation()}>{tp.title}</a>
          <span className="issue-meta">
            <span className="rm-badge">{tp.status}</span>
            <span className="rm-count">{tp.targetClosed}/{tp.targetTotal} {t('kpi.ofIssues')}</span>
          </span>
        </div>
        {open && (
          <div className="issue-body">
            {tp.epics.map((e) => (
              <div className="rm-epic" key={'e' + e.iid}>
                <a className="rm-epic-h" href={e.webUrl} target="_blank" rel="noreferrer">
                  {window.Icon('activity', 12)} <b>épic &{e.iid}</b> {e.title}
                  <span className={'rm-st ' + (e.allClosed ? 'closed' : 'opened')} style={{ marginLeft: 6 }}>{e.closed}/{e.total}</span>
                </a>
                <div className="rm-epic-issues">{e.issues.map(RmIssue)}</div>
              </div>
            ))}
            {tp.issues.length > 0 && <div className="rm-epic-issues" style={{ marginLeft: 0 }}>{tp.issues.map(RmIssue)}</div>}
            {tp.epics.length === 0 && tp.issues.length === 0 && <span className="muted">—</span>}
          </div>
        )}
      </div>
    );
  }

  // Une ligne de post « concerné » (popup Acknowledge Time) : pastille de verdict + titre (lien Canny) +
  // auteur + répondeur/délai. Vert = réponse ≤ SLA, rouge = hors délai, neutre = aucune réponse éligible.
  function AckPost({ r }) {
    const t = window.t;
    const col = r.answered ? (r.within ? 'var(--color-good)' : 'var(--color-bad)') : 'var(--color-neutral)';
    return (
      <div className="ackp-row">
        <span className="ackp-dot" style={{ background: col }}></span>
        <a className="ackp-title" href={r.url} target="_blank" rel="noreferrer">{r.title}</a>
        <span className="ackp-author">{r.author}</span>
        <span className="ackp-resp">
          {r.answered
            ? <span>{r.responder} · <b style={{ color: col }}>{r.delay}h</b></span>
            : <span className="ackp-none">{t('kpi.ackNoResp')}</span>}
        </span>
      </div>
    );
  }

  window.TabIndicateurs = function TabIndicateurs() {
    const A = window.APP || {};
    const CANNY = window.__CANNY__ || null;
    const agg = (CANNY && CANNY.aggregates) || {};
    const t = window.t;
    const det = A.detail || [];
    const K = window.Kard;
    const [rmOpen, setRmOpen] = React.useState(false);
    const [ackOpen, setAckOpen] = React.useState(false);

    const cards = [];
    const F = (a, aw, b, bw) => <span><b>{a}</b> {aw} · <b>{b}</b> {bw}</span>;
    function card(key, icon, title, pct, has, barColor, footer, info) {
      // Sans données : couleur NEUTRE (pas de faux verdict good/bad) + barre vide.
      const col = has ? barColor : 'var(--color-neutral)';
      cards.push(<K key={key} icon={icon} iconColor={col} title={title} info={info}
        value={has ? pct + ' %' : '—'} display="bar" ratio={has ? (pct || 0) / 100 : 0} barColor={col} footer={footer} />);
    }

    // 1. Roadmap Adherence — sujets roadmap « [N] » corrélés à GitLab. Adhérent = statut Canny « complete »
    //    ET toutes les issues liées fermées (celles de l'épic et/ou les issues directes). Pré-résolu côté
    //    serveur dans window.__ROADMAP__ (voir RoadmapAdherenceResolver). Carte CLIQUABLE → détail par sujet.
    //    FILTRÉ par la milestone active : corrélation milestone GitLab ↔ roadmap Canny résolue PAR NOM
    //    (« 2026-R2 » ↔ « Roadmap 2026.r2 » : minuscules + retrait de « roadmap » et des non-alphanumériques).
    const RM = window.__ROADMAP__ || null;
    const rmAllTopics = (RM && RM.topics) || [];
    const activeMs = window.__activeMilestones || [];
    const normMs = (s) => String(s).toLowerCase().replace(/[^a-z0-9]/g, '');
    const normRm = (s) => String(s).toLowerCase().replace(/roadmap/g, '').replace(/[^a-z0-9]/g, '');
    const selMsNorm = activeMs.map(normMs).filter(Boolean);
    const rmInScope = (tp) => !selMsNorm.length || (tp.roadmaps || []).map(normRm).some((r) => selMsNorm.indexOf(r) >= 0);
    const rmTopics = rmAllTopics.filter(rmInScope);
    const rmDataExists = rmAllTopics.length > 0;   // des sujets existent (extraction Canny faite) vs filtre sans correspondance
    const rmTotal = rmTopics.length;
    const rmAdh = rmTopics.filter((x) => x.adherent).length;
    const rmPct = rmTotal ? Math.round(rmAdh / rmTotal * 100) : 0;
    const rmHas = rmTotal > 0;
    const rmCol = rmHas ? colorUp(rmPct) : 'var(--color-neutral)';
    cards.push(
      <K key="roadmap" icon="circle-check" iconColor={rmCol} title={t('kpi.roadmapTitle')} info={t('kpi.roadmapInfo')}
        value={rmHas ? rmPct + ' %' : '—'} display="bar" ratio={rmHas ? rmPct / 100 : 0} barColor={rmCol}
        footer={<span><b>{rmAdh}</b> {t('kpi.adherent')} · <b>{rmTotal}</b> {t('kpi.topics')}</span>}
        popup onOpen={() => setRmOpen(true)} />
    );

    // 2. Acknowledge Time — part des demandes Canny répondues en ≤ SLA (heures ouvrées). Recalcul CLIENT
    //    filtré par deux listes configurables (Options → admin) : on ne compte QUE les posts d'auteurs
    //    CONCERNÉS (window.__ACKCFG__.authorIds — vide = tous) et une réponse d'un RÉPONDEUR autorisé
    //    (responderIds — vide = tout admin/status change, comportement historique). Par post, ackEvents =
    //    commentaires admin + changements de statut, avec l'id de l'auteur + délai ouvré (bh).
    const ackCfg = window.__ACKCFG__ || { authorIds: [], responderIds: [] };
    const ackSlaH = (CANNY && CANNY.meta && CANNY.meta.slaConfig && CANNY.meta.slaConfig.hours) || 4;
    const hasAck = CANNY && CANNY.posts && CANNY.posts.some((p) => Array.isArray(p.ackEvents));
    let ackRows = null, ackAnswered = 0, ackWithin = 0;   // exposés au popup (liste des posts concernés)
    if (hasAck) {
      const authorSet = new Set(ackCfg.authorIds || []);
      const respSet = new Set(ackCfg.responderIds || []);
      const byId = {}; (CANNY.users || []).forEach((u) => { byId[u.id] = u; });
      const uname = (id) => { const u = byId[id]; return u ? (u.name || u.email || id) : id; };
      ackRows = [];
      CANNY.posts.forEach((p) => {
        if (authorSet.size && !authorSet.has(p.authorId)) return;            // hors périmètre « concernés »
        let ev = p.ackEvents || [];
        if (respSet.size) ev = ev.filter((e) => respSet.has(e.a));           // réponse d'un répondeur autorisé
        const first = ev.length ? ev.reduce((a, b) => (b.bh < a.bh ? b : a)) : null; // 1re réponse éligible
        ackRows.push({
          id: p.id, title: p.title || p.id, url: p.url, author: p.authorName || uname(p.authorId),
          answered: !!first, delay: first ? first.bh : null, within: !!first && first.bh <= ackSlaH,
          responder: first ? uname(first.a) : null, via: first ? first.v : null,
        });
      });
      ackAnswered = ackRows.filter((r) => r.answered).length;
      ackWithin = ackRows.filter((r) => r.within).length;
      // Tri : sans réponse d'abord, puis hors délai (le pire en haut), puis dans les délais.
      const rank = (r) => (r.answered ? (r.within ? 2 : 1) : 0);
      ackRows.sort((a, b) => rank(a) - rank(b) || ((b.delay || 0) - (a.delay || 0)));
      const pct = ackAnswered ? Math.round(ackWithin / ackAnswered * 100) : 0;
      const col = ackAnswered ? colorUp(pct) : 'var(--color-neutral)';
      cards.push(
        <K key="ack" icon="clock" iconColor={col} title={t('kpi.ackTitle')} info={t('kpi.ackInfo')}
          value={ackAnswered ? pct + ' %' : '—'} display="bar" ratio={ackAnswered ? pct / 100 : 0} barColor={col}
          footer={F(ackWithin, '≤' + ackSlaH + 'h', ackAnswered, t('kpi.answered'))}
          popup onOpen={() => setAckOpen(true)} />
      );
    } else if (agg.sla) {
      // Repli : dataset extrait AVANT l'ajout des ackEvents (pas de liste par post ni de recalcul filtré).
      // Agrégat serveur historique (non filtré). Cliquable → invite à ré-extraire Canny.
      const answered = (agg.sla.compliant || 0) + (agg.sla.breached || 0);
      const pct = answered ? Math.round((agg.sla.within4h || 0) / answered * 100) : 0;
      const col = answered ? colorUp(pct) : 'var(--color-neutral)';
      cards.push(
        <K key="ack" icon="clock" iconColor={col} title={t('kpi.ackTitle')} info={t('kpi.ackInfo')}
          value={answered ? pct + ' %' : '—'} display="bar" ratio={answered ? pct / 100 : 0} barColor={col}
          footer={F(agg.sla.within4h || 0, '≤4h', answered, t('kpi.answered'))}
          popup onOpen={() => setAckOpen(true)} />
      );
    }

    // 3. Unplanned Work — part des issues « Unplanned » (cible < 15%, plus bas mieux).
    const unpl = det.filter((d) => hasExact(d, 'unplanned')).length;
    const upPct = det.length ? Math.round(unpl / det.length * 100) : 0;
    card('unplanned', 'alert-triangle', t('kpi.unplannedTitle'), upPct, det.length > 0, colorDown(upPct, 15, 25),
      F(unpl, t('kpi.unplannedIssues'), det.length, t('kpi.ofIssues')), t('kpi.unplannedInfo'));

    // 4. Patch Success — bugs fermés avec 0 aller-retour QA (retours === 0).
    const bugs = det.filter((d) => (d.type === 'bug' || d.type === 'clientbug') && d.state === 'closed');
    const patchOk = bugs.filter((d) => d.retours === 0).length;
    const patchPct = bugs.length ? Math.round(patchOk / bugs.length * 100) : 0;
    card('patch', 'badge-check', t('kpi.patchTitle'), patchPct, bugs.length > 0, colorUp(patchPct),
      F(patchOk, t('kpi.zeroReturn'), bugs.length, t('kpi.bugsClosed')), t('kpi.patchInfo'));

    // 5. Bug Resolution — Client Bug fermés en ≤ 72h.
    const cb = det.filter((d) => d.type === 'clientbug' && d.state === 'closed' && d.createdAt && d.closedAt);
    const cbOk = cb.filter((d) => hours(d.createdAt, d.closedAt) <= 72).length;
    const cbPct = cb.length ? Math.round(cbOk / cb.length * 100) : 0;
    card('bugres', 'gauge', t('kpi.bugResTitle'), cbPct, cb.length > 0, colorUp(cbPct), F(cbOk, '≤72h', cb.length, t('kpi.clientBugs')), t('kpi.bugResInfo'));

    // 6. MTTR — Critical + Client Bug fermés en ≤ 48h.
    const crit = det.filter((d) => d.state === 'closed' && d.createdAt && d.closedAt
      && hasKw(d, 'critical') && (d.type === 'clientbug' || hasKw(d, 'client bug')));
    const critOk = crit.filter((d) => hours(d.createdAt, d.closedAt) <= 48).length;
    const mttrPct = crit.length ? Math.round(critOk / crit.length * 100) : 0;
    card('mttr', 'clock', t('kpi.mttrTitle'), mttrPct, crit.length > 0, colorUp(mttrPct), F(critOk, '≤48h', crit.length, t('kpi.critBugs')), t('kpi.mttrInfo'));

    // 7. Refactoring — part des issues « Refactor » (cible < 20%, plus bas mieux).
    const refac = det.filter((d) => hasKw(d, 'refactor')).length;
    const rfPct = det.length ? Math.round(refac / det.length * 100) : 0;
    card('refactor', 'activity', t('kpi.refactorTitle'), rfPct, det.length > 0, colorDown(rfPct, 20, 30),
      F(refac, t('kpi.refactorIssues'), det.length, t('kpi.ofIssues')), t('kpi.refactorInfo'));

    // 8. Say/Do Ratio — roadmap Canny livré / prévu (cible > 85%). Proxy = validation roadmap.
    if (agg.roadmapValidation) {
      const rv = agg.roadmapValidation;
      const tot = rv.reduce((s, r) => s + (r.total || 0), 0);
      const val = rv.reduce((s, r) => s + (r.valide || 0), 0);
      const sdPct = tot ? Math.round(val / tot * 100) : 0;
      card('saydo', 'gauge', t('kpi.saydoTitle'), sdPct, tot > 0, colorUp(sdPct), F(val, t('kpi.delivered'), tot, t('kpi.planned')), t('kpi.saydoInfo'));
    }

    return (
      <div className="kpi-root" style={{ padding: 'var(--space-5, 20px)' }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 'var(--space-4, 12px)', maxWidth: 1180 }}>
          {cards}
        </div>
        {!CANNY && <p style={{ marginTop: 16, color: 'var(--color-ink-3, #888)', fontSize: 'var(--text-caption, 12px)' }}>{t('kpi.noCanny')}</p>}

        {rmOpen && (
          <window.Modal title={t('kpi.roadmapTitle')}
            subtitle={[rmHas ? rmAdh + ' / ' + rmTotal + ' ' + t('kpi.adherent') : null, activeMs.length ? activeMs.join(', ') : null].filter(Boolean).join(' · ') || undefined}
            wide layout={(typeof window !== 'undefined' && window.__drillLayout) || 'modal'} onClose={() => setRmOpen(false)}>
            <p className="rm-intro">{t('kpi.roadmapInfo')}</p>
            {rmHas
              ? <div className="rm-list">{rmTopics.map((tp) => <RmTopic key={tp.postId} tp={tp} />)}</div>
              : <p className="empty">{rmDataExists ? t('kpi.roadmapNoScope') : t('kpi.roadmapEmpty')}</p>}
          </window.Modal>
        )}

        {ackOpen && (
          <window.Modal title={t('kpi.ackTitle')}
            subtitle={ackRows && ackRows.length ? ackRows.length + ' ' + t('kpi.postsConcerned') + ' · ' + ackWithin + ' / ' + ackAnswered + ' ≤' + ackSlaH + 'h' : undefined}
            wide layout={(typeof window !== 'undefined' && window.__drillLayout) || 'modal'} onClose={() => setAckOpen(false)}>
            <p className="rm-intro">{t('kpi.ackInfo')}</p>
            {ackRows && ackRows.length
              ? <div className="ackp-list">{ackRows.map((r) => <AckPost key={r.id} r={r} slaH={ackSlaH} />)}</div>
              : <p className="empty">{t('kpi.ackNoDetail')}</p>}
          </window.Modal>
        )}
      </div>
    );
  };
})();
