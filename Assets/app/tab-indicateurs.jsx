// tab-indicateurs.jsx — page « Indicateurs » (avant le Dashboard) : cartouches KPI (window.Kard).
// Couche MÉTIER : chaque KPI dérive une valeur (%) + une couleur de verdict + un ratio de barre, depuis
// window.APP (GitLab, filtré par les pills) et window.__CANNY__ (Canny, global). Ordre = liste produit.
// Regression Rate volontairement OMIS (à ignorer pour l'instant).
//
// Labels GitLab CONFIRMÉS (match EXACT, insensible à la casse) : « Unplanned », « Type::Client Bug »,
// « PRIO::Critical », « Type::Refactor ». MTTR = PRIO::Critical ET Type::Client Bug (les deux).
(function () {
  // Verdict : plus HAUT = mieux (up) ou plus BAS = mieux (down, seuils = maxima acceptables).
  const colorUp = (pct) => pct >= 85 ? 'var(--color-good)' : pct >= 70 ? 'var(--color-warn)' : 'var(--color-bad)';
  const colorDown = (pct, good, warn) => pct <= good ? 'var(--color-good)' : pct <= warn ? 'var(--color-warn)' : 'var(--color-bad)';
  const hasExact = (d, name) => (d.labels || []).some((l) => String(l).toLowerCase() === name);
  const hours = (a, b) => (new Date(b).getTime() - new Date(a).getTime()) / 3600000;

  // Acknowledge Time : regroupement du détail par TYPE DE POSTE (board Canny). Ordre demandé puis alpha.
  const BOARD_ORDER = ['Bug', 'Questions', 'Feature Requests'];
  function groupAckByBoard(rows) {
    const map = {};
    rows.forEach((r) => { (map[r.board] = map[r.board] || []).push(r); });
    return Object.keys(map).sort((a, b) => {
      const ia = BOARD_ORDER.indexOf(a), ib = BOARD_ORDER.indexOf(b);
      return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib) || a.localeCompare(b);
    }).map((b) => {
      const rws = map[b];
      return { board: b, rows: rws, answered: rws.filter((r) => r.answered).length, within: rws.filter((r) => r.within).length };
    });
  }

  // Métadonnées par KPI : grp = section (produit=Canny/roadmap, ingenierie=GitLab) ; src = source de
  // données (canny/gitlab/mixte) ; filt = filtrabilité par les pills (all/ms/none) ; aud = audience (rôle).
  // Ajustable ici de façon centralisée (badges + regroupement en dérivent).
  const KPI_META = {
    roadmap:   { grp: 'produit',    src: 'mixte',  filt: 'ms',   aud: 'produit' },
    ack:       { grp: 'produit',    src: 'canny',  filt: 'none', aud: 'support' },
    saydo:     { grp: 'produit',    src: 'canny',  filt: 'none', aud: 'produit' },
    unplanned: { grp: 'ingenierie', src: 'gitlab', filt: 'all',  aud: 'equipe' },
    patch:     { grp: 'ingenierie', src: 'gitlab', filt: 'all',  aud: 'equipe' },
    bugres:    { grp: 'ingenierie', src: 'gitlab', filt: 'all',  aud: 'equipe' },
    mttr:      { grp: 'ingenierie', src: 'gitlab', filt: 'all',  aud: 'equipe' },
    refactor:  { grp: 'ingenierie', src: 'gitlab', filt: 'all',  aud: 'equipe' },
  };
  const SRC_LABEL = { canny: 'Canny', gitlab: 'GitLab', mixte: 'Canny + GitLab' };
  function tagsFor(key) {
    const m = KPI_META[key]; if (!m) return null;
    const t = window.t;
    const tags = [{ text: SRC_LABEL[m.src] || m.src, tone: m.src }];
    if (m.filt === 'all') tags.push({ text: t('kpi.tagFilterable'), tone: 'filter' });
    else if (m.filt === 'ms') tags.push({ text: t('kpi.tagFilterMs'), tone: 'filter' });
    else tags.push({ text: t('kpi.tagGlobal'), tone: 'global' });
    tags.push({ text: t('kpi.aud_' + m.aud), tone: 'aud' });
    return tags;
  }

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

  // Une ligne d'issue GitLab (popups des KPI par issue) : badge d'état + #iid/titre (lien) + Type::* +
  // métrique propre au KPI (rowMeta : délai de résolution, retours, …). base = URL d'issue (A.meta.issueBase).
  function IssueRow({ d, base, rowMeta }) {
    const t = window.t;
    const closed = d.state === 'closed';
    const typeLbl = (d.labels || []).find((l) => /^type::/i.test(l));
    const short = typeLbl ? typeLbl.replace(/^type::\s*/i, '') : null;
    const href = base ? base + d.iid : null;
    const title = '#' + d.iid + ' ' + (d.title || '');
    return (
      <div className="ackp-row">
        <span className={'st-badge ' + (closed ? 'st-closed' : 'st-open')}><span className="sd"></span>{closed ? t('common.closedF') : t('common.openF')}</span>
        {href
          ? <a className="ackp-title" href={href} target="_blank" rel="noreferrer">{title}</a>
          : <span className="ackp-title">{title}</span>}
        {short && <span className="ackp-author">{short}</span>}
        <span className="ackp-resp">{rowMeta ? rowMeta(d) : null}</span>
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
    const [issModal, setIssModal] = React.useState(null); // { title, issues, rowMeta } | null

    const cards = [];
    const F = (a, aw, b, bw) => <span><b>{a}</b> {aw} · <b>{b}</b> {bw}</span>;
    const base = (A.meta || {}).issueBase || '';
    const good = 'var(--color-good)', bad = 'var(--color-bad)';
    // Tris des listes d'issues : ouvertes d'abord (puis iid décroissant), ou par délai de résolution décroissant.
    const byOpenFirst = (a, b) => (a.state === b.state ? b.iid - a.iid : (a.state === 'open' ? -1 : 1));
    const byTimeDesc = (a, b) => hours(b.createdAt, b.closedAt) - hours(a.createdAt, a.closedAt);
    // Métrique « délai de résolution » colorée selon un seuil (heures création → fermeture).
    const timeMeta = (limit) => (d) => { const h = hours(d.createdAt, d.closedAt); return <b style={{ color: h <= limit ? good : bad }}>{h.toFixed(1)}h</b>; };
    // Carte KPI. `issues` (optionnel) → carte CLIQUABLE ouvrant la liste des issues concernées ; `rowMeta` =
    // rendu de la colonne de droite par issue dans le popup.
    function card(key, icon, title, pct, has, barColor, footer, info, issues, rowMeta) {
      const col = has ? barColor : 'var(--color-neutral)';
      const list = issues || [];
      const clickable = list.length > 0;
      cards.push({ key, el: <K key={key} icon={icon} iconColor={col} title={title} info={info} tags={tagsFor(key)}
        value={has ? pct + ' %' : '—'} display="bar" ratio={has ? (pct || 0) / 100 : 0} barColor={col} footer={footer}
        popup={clickable ? true : null} onOpen={clickable ? () => setIssModal({ title, issues: list, rowMeta }) : undefined} /> });
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
    cards.push({ key: 'roadmap', el: (
      <K key="roadmap" icon="circle-check" iconColor={rmCol} title={t('kpi.roadmapTitle')} info={t('kpi.roadmapInfo')} tags={tagsFor('roadmap')}
        value={rmHas ? rmPct + ' %' : '—'} display="bar" ratio={rmHas ? rmPct / 100 : 0} barColor={rmCol}
        footer={<span><b>{rmAdh}</b> {t('kpi.adherent')} · <b>{rmTotal}</b> {t('kpi.topics')}</span>}
        popup onOpen={() => setRmOpen(true)} />
    ) });

    // 2. Acknowledge Time — part des demandes Canny répondues en ≤ SLA (heures ouvrées). Recalcul CLIENT
    //    filtré par deux listes configurables (Options → admin) : on ne compte QUE les posts d'auteurs
    //    CONCERNÉS (window.__ACKCFG__.authorIds — vide = tous) et une réponse d'un RÉPONDEUR autorisé
    //    (responderIds — vide = tout admin/status change, comportement historique). Par post, ackEvents =
    //    commentaires admin + changements de statut, avec l'id de l'auteur + délai ouvré (bh).
    const ackCfg = window.__ACKCFG__ || { authorIds: [], responderIds: [] };
    const ackSlaH = (CANNY && CANNY.meta && CANNY.meta.slaConfig && CANNY.meta.slaConfig.hours) || 4;
    const hasAck = CANNY && CANNY.posts && CANNY.posts.some((p) => Array.isArray(p.ackEvents));
    let ackRows = null, ackAnswered = 0, ackWithin = 0, ackGroups = null;   // exposés au popup (liste + récap par board)
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
          board: p.board || t('kpi.ackNoBoard'),
          answered: !!first, delay: first ? first.bh : null, within: !!first && first.bh <= ackSlaH,
          responder: first ? uname(first.a) : null, via: first ? first.v : null,
        });
      });
      ackAnswered = ackRows.filter((r) => r.answered).length;
      ackWithin = ackRows.filter((r) => r.within).length;
      // Tri : sans réponse d'abord, puis hors délai (le pire en haut), puis dans les délais.
      const rank = (r) => (r.answered ? (r.within ? 2 : 1) : 0);
      ackRows.sort((a, b) => rank(a) - rank(b) || ((b.delay || 0) - (a.delay || 0)));
      ackGroups = groupAckByBoard(ackRows); // récap + sections par type de poste (board Canny)
      const pct = ackAnswered ? Math.round(ackWithin / ackAnswered * 100) : 0;
      const col = ackAnswered ? colorUp(pct) : 'var(--color-neutral)';
      cards.push({ key: 'ack', el: (
        <K key="ack" icon="clock" iconColor={col} title={t('kpi.ackTitle')} info={t('kpi.ackInfo')} tags={tagsFor('ack')}
          value={ackAnswered ? pct + ' %' : '—'} display="bar" ratio={ackAnswered ? pct / 100 : 0} barColor={col}
          footer={F(ackWithin, '≤' + ackSlaH + 'h', ackAnswered, t('kpi.answered'))}
          popup onOpen={() => setAckOpen(true)} />
      ) });
    } else if (agg.sla) {
      // Repli : dataset extrait AVANT l'ajout des ackEvents (pas de liste par post ni de recalcul filtré).
      // Agrégat serveur historique (non filtré). Cliquable → invite à ré-extraire Canny.
      const answered = (agg.sla.compliant || 0) + (agg.sla.breached || 0);
      const pct = answered ? Math.round((agg.sla.within4h || 0) / answered * 100) : 0;
      const col = answered ? colorUp(pct) : 'var(--color-neutral)';
      cards.push({ key: 'ack', el: (
        <K key="ack" icon="clock" iconColor={col} title={t('kpi.ackTitle')} info={t('kpi.ackInfo')} tags={tagsFor('ack')}
          value={answered ? pct + ' %' : '—'} display="bar" ratio={answered ? pct / 100 : 0} barColor={col}
          footer={F(agg.sla.within4h || 0, '≤4h', answered, t('kpi.answered'))}
          popup onOpen={() => setAckOpen(true)} />
      ) });
    }

    // 3. Unplanned Work — part des issues « Unplanned » (cible < 15%, plus bas mieux).
    const unplIssues = det.filter((d) => hasExact(d, 'unplanned'));
    const upPct = det.length ? Math.round(unplIssues.length / det.length * 100) : 0;
    card('unplanned', 'alert-triangle', t('kpi.unplannedTitle'), upPct, det.length > 0, colorDown(upPct, 15, 25),
      F(unplIssues.length, t('kpi.unplannedIssues'), det.length, t('kpi.ofIssues')), t('kpi.unplannedInfo'),
      unplIssues.slice().sort(byOpenFirst));

    // 4. Patch Success — bugs fermés avec 0 aller-retour QA (retours === 0).
    const bugs = det.filter((d) => (d.type === 'bug' || d.type === 'clientbug') && d.state === 'closed');
    const patchOk = bugs.filter((d) => d.retours === 0).length;
    const patchPct = bugs.length ? Math.round(patchOk / bugs.length * 100) : 0;
    card('patch', 'badge-check', t('kpi.patchTitle'), patchPct, bugs.length > 0, colorUp(patchPct),
      F(patchOk, t('kpi.zeroReturn'), bugs.length, t('kpi.bugsClosed')), t('kpi.patchInfo'),
      bugs.slice().sort((a, b) => b.retours - a.retours),
      (d) => <b style={{ color: d.retours === 0 ? good : bad }}>{d.retours} {t('kpi.returns')}</b>);

    // 5. Bug Resolution — issues « Type::Client Bug » fermées en ≤ 72h (création → fermeture).
    const cb = det.filter((d) => hasExact(d, 'type::client bug') && d.state === 'closed' && d.createdAt && d.closedAt);
    const cbOk = cb.filter((d) => hours(d.createdAt, d.closedAt) <= 72).length;
    const cbPct = cb.length ? Math.round(cbOk / cb.length * 100) : 0;
    card('bugres', 'gauge', t('kpi.bugResTitle'), cbPct, cb.length > 0, colorUp(cbPct), F(cbOk, '≤72h', cb.length, t('kpi.clientBugs')), t('kpi.bugResInfo'),
      cb.slice().sort(byTimeDesc), timeMeta(72));

    // 6. MTTR — issues « PRIO::Critical » ET « Type::Client Bug » fermées en ≤ 48h.
    const crit = det.filter((d) => d.state === 'closed' && d.createdAt && d.closedAt
      && hasExact(d, 'prio::critical') && hasExact(d, 'type::client bug'));
    const critOk = crit.filter((d) => hours(d.createdAt, d.closedAt) <= 48).length;
    const mttrPct = crit.length ? Math.round(critOk / crit.length * 100) : 0;
    card('mttr', 'clock', t('kpi.mttrTitle'), mttrPct, crit.length > 0, colorUp(mttrPct), F(critOk, '≤48h', crit.length, t('kpi.critBugs')), t('kpi.mttrInfo'),
      crit.slice().sort(byTimeDesc), timeMeta(48));

    // 7. Refactoring — part des issues « Type::Refactor » sur le total (cible < 20%, plus bas mieux).
    const refacIssues = det.filter((d) => hasExact(d, 'type::refactor'));
    const rfPct = det.length ? Math.round(refacIssues.length / det.length * 100) : 0;
    card('refactor', 'activity', t('kpi.refactorTitle'), rfPct, det.length > 0, colorDown(rfPct, 20, 30),
      F(refacIssues.length, t('kpi.refactorIssues'), det.length, t('kpi.ofIssues')), t('kpi.refactorInfo'),
      refacIssues.slice().sort(byOpenFirst));

    // 8. Say/Do Ratio — roadmap Canny livré / prévu (cible > 85%). Proxy = validation roadmap.
    if (agg.roadmapValidation) {
      const rv = agg.roadmapValidation;
      const tot = rv.reduce((s, r) => s + (r.total || 0), 0);
      const val = rv.reduce((s, r) => s + (r.valide || 0), 0);
      const sdPct = tot ? Math.round(val / tot * 100) : 0;
      card('saydo', 'gauge', t('kpi.saydoTitle'), sdPct, tot > 0, colorUp(sdPct), F(val, t('kpi.delivered'), tot, t('kpi.planned')), t('kpi.saydoInfo'));
    }

    // Strip globale (rendue par le Shell sous les pills, sur toutes les pages) : grille à plat, sans
    // en-têtes de section. La source reste lisible via les badges de chaque cartouche. Ordre : Produit
    // (Canny/roadmap) puis Ingénierie (GitLab), sans titre.
    const ordered = cards.filter((c) => (KPI_META[c.key] || {}).grp === 'produit')
      .concat(cards.filter((c) => (KPI_META[c.key] || {}).grp !== 'produit'));

    return (
      <div className="kpi-root kpi-strip">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 'var(--space-4, 12px)', maxWidth: 1180 }}>
          {ordered.map((c) => c.el)}
        </div>
        {!CANNY && <p style={{ marginTop: 12, color: 'var(--color-ink-3, #888)', fontSize: 'var(--text-caption, 12px)' }}>{t('kpi.noCanny')}</p>}

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
            {ackGroups && ackGroups.length ? (
              <React.Fragment>
                {/* Récap comparatif des 3 types de poste : taux d'acquittement (barre + %) + volume (part du total). */}
                <div className="acksum">
                  {ackGroups.map((g) => {
                    const rate = g.answered ? Math.round(g.within / g.answered * 100) : 0;
                    const col = g.answered ? colorUp(rate) : 'var(--color-neutral)';
                    const share = ackRows.length ? Math.round(g.rows.length / ackRows.length * 100) : 0;
                    return (
                      <div className="acksum-card" key={g.board}>
                        <div className="acksum-name">{g.board}</div>
                        <div className="acksum-pct" style={{ color: col }}>{g.answered ? rate + ' %' : '—'}</div>
                        <div className="acksum-bar"><i style={{ width: (g.answered ? rate : 0) + '%', background: col }}></i></div>
                        <div className="acksum-meta"><b>{g.within}/{g.answered}</b> ≤{ackSlaH}h · <b>{g.rows.length}</b> {t('kpi.postsWord')} ({share}%)</div>
                      </div>
                    );
                  })}
                </div>
                {ackGroups.map((g) => (
                  <div className="ackg" key={g.board}>
                    <div className="ackg-hd">
                      <span className="ackg-name">{g.board}</span>
                      <span className="ackg-stat">{g.within} / {g.answered} ≤{ackSlaH}h{g.answered ? ' · ' + Math.round(g.within / g.answered * 100) + ' %' : ''}</span>
                      <span className="ackg-count">{g.rows.length}</span>
                    </div>
                    <div className="ackp-list">{g.rows.map((r) => <AckPost key={r.id} r={r} slaH={ackSlaH} />)}</div>
                  </div>
                ))}
              </React.Fragment>
            ) : <p className="empty">{t('kpi.ackNoDetail')}</p>}
          </window.Modal>
        )}

        {issModal && (
          <window.Modal title={issModal.title} subtitle={issModal.issues.length + ' ' + t('kpi.issuesConcerned')}
            wide layout={(typeof window !== 'undefined' && window.__drillLayout) || 'modal'} onClose={() => setIssModal(null)}>
            <div className="ackp-list">
              {issModal.issues.map((d) => <IssueRow key={d.iid} d={d} base={base} rowMeta={issModal.rowMeta} />)}
            </div>
          </window.Modal>
        )}
      </div>
    );
  };
})();
