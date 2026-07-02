// Mapper : transforme le payload réel (window.__DATA__, issu de /api/data) en la forme
// window.APP attendue par l'app de référence Claude Design. La logique métier (temps ouvré,
// phases, segments) est reprise À L'IDENTIQUE de la vue C#. Aucune modif du design.
window.buildAPP = function (D) {
  D = D || {};
  var ISSUES = D.issues || [];
  // Catalogue complet (types/personnes) sur tout le périmètre du compte, pour rester STABLE
  // quand les filtres réduisent ISSUES. Sinon les onglets qui figent A.types au chargement
  // (ex. tab-velocity TYPES) référencent une clé absente du typeByKey filtré → crash.
  var CAT = D.allIssues || ISSUES;
  var MS_DAY = 86400000;
  var FIB = [1, 2, 3, 5, 8, 13];

  // ---------- temps ouvré + phases (copie fidèle de HypervisorReleaseView) ----------
  // Mapping label → phase. PILOTÉ PAR LA CONFIG (Export.LabelPhases, écrit par l'assistant /setup) :
  // si une config est fournie, ELLE SEULE décide ; sinon repli sur les labels Prod::* historiques (rétro-compat).
  // Phases de TEMPS : dev/review/qawait/qa/tofix/po. uiux = phase de segment (Gantt) uniquement, non chronométrée.
  var DEFAULT_PH = {
    'prod::code in progress': 'dev', 'prod::code review': 'review', 'prod::code pre-review': 'review',
    'prod::qa backlog': 'qawait', 'prod::qa hotfix backlog': 'qawait',
    'prod::qa inprogress': 'qa', 'prod::qa hotfix inprogress': 'qa',
    'prod::to fix': 'tofix', 'prod:: po validation': 'po'
  };
  // Catalogue des périodes (Export.Periods, payload window.__DATA__.periods). Source de vérité des
  // noms/couleurs/flag timed. Repli sur les phases standard si non configuré (rétro-compat).
  var PERIODS = (D.periods || []).filter(function (p) { return p && p.key; });
  var TIMED = {};
  if (PERIODS.length) PERIODS.forEach(function (p) { if (p.timed) TIMED[p.key] = 1; });
  else TIMED = { dev: 1, review: 1, qawait: 1, qa: 1, tofix: 1, po: 1 };
  // Clés de phase chronométrées, dans l'ordre admin — pilotent durées, colonnes pivot, moyennes.
  var PHASE_KEYS = Object.keys(TIMED);
  var cfgLP = D.labelPhases || {};
  var PH_HAS_CFG = false, PH_MAP = {};
  Object.keys(cfgLP).forEach(function (k) { var v = cfgLP[k]; if (v && v !== 'none') { PH_MAP[String(k).toLowerCase()] = v; PH_HAS_CFG = true; } });
  if (!PH_HAS_CFG) PH_MAP = DEFAULT_PH;
  // Phase d'un label (clé en minuscules). Config prioritaire ; repli UI/UX par préfixe seulement en mode défaut.
  function phaseOf(lo) {
    if (PH_MAP[lo]) return PH_MAP[lo];
    if (!PH_HAS_CFG && (lo.indexOf('prod::ui/ux') === 0 || lo.indexOf('prod:: ui/ux') === 0)) return 'uiux';
    return null;
  }
  function workingMs(s, e) {
    if (!(e > s)) return 0;
    var total = 0, cur = new Date(s); cur.setHours(0, 0, 0, 0);
    while (cur.getTime() < e) {
      var dow = cur.getDay();
      if (dow !== 0 && dow !== 6) {
        var ws = new Date(cur); ws.setHours(9, 0, 0, 0);
        var we = new Date(cur); we.setHours(19, 0, 0, 0);
        var lo = Math.max(s, ws.getTime()), hi = Math.min(e, we.getTime());
        if (hi > lo) total += (hi - lo);
      }
      cur.setDate(cur.getDate() + 1); cur.setHours(0, 0, 0, 0);
    }
    return total;
  }
  function evs(iss) { return (iss.labelEvents || []).slice().sort(function (a, b) { return new Date(a.at).getTime() - new Date(b.at).getTime(); }); }
  // Durées de phase (ms ouvré), incluant To fix, + retours.
  function times(iss) {
    var e = evs(iss);
    var acc = {}, cnt = {}, since = {};
    PHASE_KEYS.forEach(function (k) { acc[k] = 0; cnt[k] = 0; since[k] = null; });
    var retours = 0;
    // Comptage ÉQUILIBRÉ par phase (clés DYNAMIQUES depuis la config) : on accumule le temps ouvré tant
    // qu'au moins un label de la phase est actif. La phase de clé « tofix » → +1 retour à chaque ajout.
    for (var i = 0; i < e.length; i++) {
      var lo = (e[i].label || '').toLowerCase(), t = new Date(e[i].at).getTime(), add = e[i].action === 'add';
      if (isNaN(t)) continue;
      var ph = phaseOf(lo);
      if (!ph || !TIMED[ph]) continue;
      if (acc[ph] === undefined) { acc[ph] = 0; cnt[ph] = 0; since[ph] = null; } // clé timée hors PHASE_KEYS (sécurité)
      if (add) { if (cnt[ph] === 0) since[ph] = t; cnt[ph]++; if (ph === 'tofix') retours++; }
      else if (cnt[ph] > 0) { cnt[ph]--; if (cnt[ph] === 0 && since[ph] !== null) { acc[ph] += workingMs(since[ph], t); since[ph] = null; } }
    }
    var days = function (ms) { return ms > 0 ? Math.round(ms / 36000000 * 10) / 10 : 0; };
    var out = { total: 0, retours: retours };
    Object.keys(acc).forEach(function (k) { var d = days(acc[k]); out[k] = d; out.total += d; });
    return out;
  }
  // Segment Gantt : même mapping label → phase que les durées (inclut uiux). phaseOf gère config + repli.
  function segKey(lo) { return phaseOf(lo); }

  // ---------- fenêtre milestone ----------
  function parseDay(s) { if (!s) return NaN; var p = String(s).split('-'); if (p.length !== 3) return NaN; var d = new Date(+p[0], +p[1] - 1, +p[2]); return d.getTime(); }
  var MSD = D.milestoneDates || {};
  // Fenêtre temporelle pilotée par le filtre Milestone (pills) — prioritaire sur la milestone configurée.
  var selMs = (D.selectedMilestones || []).filter(function (m) { return m && MSD[m]; });
  var msName, START, END;
  if (selMs.length) {
    // Milestone(s) sélectionnée(s) : fenêtre = union des dates connues (min start, max due).
    var mStarts = selMs.map(function (m) { return parseDay(MSD[m].startDate); }).filter(function (x) { return !isNaN(x); });
    var mEnds = selMs.map(function (m) { return parseDay(MSD[m].dueDate); }).filter(function (x) { return !isNaN(x); });
    START = mStarts.length ? Math.min.apply(null, mStarts) : NaN;
    END = mEnds.length ? Math.max.apply(null, mEnds) : NaN;
    msName = selMs.length === 1 ? selMs[0] : selMs.join(' + ');
  } else if (D.selectedMilestones && D.selectedMilestones.length === 0) {
    // Filtre explicitement « Toutes » : fenêtre = étendue réelle des issues affichées (repli events ci-dessous).
    msName = 'All milestones'; START = NaN; END = NaN;
  } else {
    // Appel initial (sans info de filtre) : milestone CONFIGURÉE du compte.
    msName = D.milestone || '';
    var msd = MSD[msName] || {};
    START = parseDay(msd.startDate); END = parseDay(msd.dueDate);
  }
  if (isNaN(START) || isNaN(END) || END <= START) {
    // repli : borne sur les events réels
    var allT = [];
    ISSUES.forEach(function (i) { (i.labelEvents || []).forEach(function (e) { var t = new Date(e.at).getTime(); if (!isNaN(t)) allT.push(t); }); if (i.createdAt) { var c = new Date(i.createdAt).getTime(); if (!isNaN(c)) allT.push(c); } });
    START = allT.length ? Math.min.apply(null, allT) : Date.now() - 84 * MS_DAY;
    END = allT.length ? Math.max.apply(null, allT) : Date.now();
  }
  var DAYS = Math.max(1, Math.round((END - START) / MS_DAY));
  var WEEKS = Math.max(1, Math.ceil(DAYS / 7));
  var NOW = Date.now();
  // floor (pas round) : l'offset du jour COURANT — avec round, dès midi le marqueur « aujourd'hui »
  // sautait au lendemain (visible depuis que l'axe Calendrier est à la journée).
  var TODAY = Math.max(0, Math.min(DAYS, Math.floor((NOW - START) / MS_DAY)));
  var dayOff = function (ms) { return (ms - START) / MS_DAY; };

  // ---------- types ----------
  var KNOWN = { 'type::feature': 'feature', 'type::feature - enhancement': 'enh', 'type::bug': 'bug', 'type::client bug': 'clientbug', 'type::regression': 'regression' };
  function typeOf(iss) {
    var f = (iss.labels || []).find(function (l) { return l.toLowerCase().indexOf('type::') === 0; });
    if (!f) return null;
    var lo = f.toLowerCase();
    return { key: KNOWN[lo] || lo.replace(/^type::\s*/, '').replace(/\s+/g, '-'), name: f, short: f.replace(/^Type::\s*/i, '') };
  }
  var typeByKey = {};
  var hasUntyped = false;
  CAT.forEach(function (i) { var t = typeOf(i); if (t) { if (!typeByKey[t.key]) typeByKey[t.key] = t; } else hasUntyped = true; });
  if (hasUntyped) typeByKey['untyped'] = { key: 'untyped', name: 'Sans Type::*', short: 'Sans type' };
  var types = Object.keys(typeByKey).map(function (k) { return typeByKey[k]; });

  // ---------- people (à partir des assignés) ----------
  function avHash(n) { var h = 0; n = String(n || ''); for (var i = 0; i < n.length; i++) h = (h * 31 + n.charCodeAt(i)) >>> 0; return (h % 6) + 1; }
  var peopleById = {};
  function regPerson(a) { if (a && !peopleById[a]) peopleById[a] = { id: a, name: a, av: avHash(a) }; }
  // Catalogue COMPLET des personnes référencées (assignés + approbateurs + auteur/fermeture + acteurs d'events)
  // sur tout le périmètre, pour que les lookups A.peopleById[id] (approbateurs ligne 58, timeline) ne plantent jamais.
  CAT.forEach(function (i) {
    (i.assignees || []).forEach(regPerson);
    if (i.closedByUsername) regPerson(i.closedByUsername);
    if (i.authorUsername) regPerson(i.authorUsername);
    (i.mergeRequests || []).forEach(function (m) { (m.approvers || []).forEach(regPerson); });
    (i.labelEvents || []).forEach(function (e) { regPerson(e.user); });
  });
  // people = assignés présents dans le périmètre FILTRÉ courant (lignes de vélocité + repli du filtre Utilisateur).
  var peopleSet = {};
  ISSUES.forEach(function (i) { (i.assignees || []).forEach(function (a) { if (a) peopleSet[a] = 1; }); });
  var people = Object.keys(peopleSet).map(function (k) { return peopleById[k]; });

  // ---------- detail (issues façonnées comme data.js) ----------
  var detail = ISSUES.map(function (iss) {
    var t = typeOf(iss);
    var tm = times(iss);
    var closed = iss.state === 'closed';
    var mrs = iss.mergeRequests || [];
    var approvers = []; mrs.forEach(function (m) { (m.approvers || []).forEach(function (a) { if (approvers.indexOf(a) < 0) approvers.push(a); }); });
    var approval = approvers.length > 0;
    // segments → seg{phase:[[startOff,endOff(,who)]]}
    var endRef = iss.closedAt ? new Date(iss.closedAt).getTime() : NOW; if (isNaN(endRef)) endRef = NOW;
    var e = evs(iss), active = {}, seg = {};
    var clampOff = function (ms) { return Math.max(0, Math.min(DAYS, dayOff(ms))); };
    function pushSeg(k, a, b, who) { if (!(dayOff(a) <= DAYS && dayOff(b) >= 0)) return; var ca = clampOff(a), cb = clampOff(b); if (!seg[k]) seg[k] = []; var arr = [ca, cb]; if (k === 'dev') arr.push(who || (iss.assignees || [])[0] || null); seg[k].push(arr); }
    for (var i = 0; i < e.length; i++) {
      var lo = (e[i].label || '').toLowerCase(), k = segKey(lo), tt = new Date(e[i].at).getTime();
      if (!k || isNaN(tt)) continue;
      if (e[i].action === 'add') { if (active[lo] === undefined) active[lo] = { s: tt, u: e[i].user || null, k: k }; }
      else if (active[lo] !== undefined) { pushSeg(active[lo].k, active[lo].s, tt, active[lo].u); delete active[lo]; }
    }
    Object.keys(active).forEach(function (lo) { pushSeg(active[lo].k, active[lo].s, endRef, active[lo].u); });
    var allSeg = Object.keys(seg).reduce(function (acc, k) { return acc.concat(seg[k]); }, []);
    var start = allSeg.length ? Math.min.apply(null, allSeg.map(function (s) { return s[0]; })) : (iss.createdAt ? dayOff(new Date(iss.createdAt).getTime()) : 0);
    var end = allSeg.length ? Math.max.apply(null, allSeg.map(function (s) { return s[1]; })) : start;
    var labelsL = (iss.labels || []).map(function (l) { return l.toLowerCase(); });
    return {
      iid: iss.iid, title: iss.title || '', type: t ? t.key : 'untyped', weight: (iss.weight == null ? 0 : iss.weight),
      assignees: iss.assignees || [], state: closed ? 'closed' : 'open',
      validated: closed, approval: approval, retours: tm.retours, _times: tm,
      seg: seg, start: start, end: end, mrCount: mrs.length,
      mrs: mrs.map(function (m) { return { iid: m.iid, state: m.state }; }), comments: iss.commentsCount || 0,
      labels: iss.labels || [], approvers: approvers, closedBy: closed ? (approvers[0] || (iss.assignees || [])[0] || null) : null,
      noMilestone: !iss.milestone, noType: !t, noPrio: !labelsL.some(function (l) { return l.indexOf('prio::') === 0; }),
      multiType: (iss.labels || []).filter(function (l) { return l.toLowerCase().indexOf('type::') === 0; }).length > 1,
      noAssigneeFlag: (iss.assignees || []).length === 0,
      closedOpenMR: closed && mrs.some(function (m) { return (m.state || '').toLowerCase() === 'opened'; })
    };
  });

  // ---------- agrégats par type (pivot) ----------
  // Agrégats par groupe : moyennes de phase indexées par CLÉ DYNAMIQUE (g[phaseKey]). Plus d'alias « rev ».
  function blankAgg() { var g = { issues: 0, open: 0, closed: 0, appr: 0, wV: 0, wNV: 0, ret: 0, comm: 0, _n: {} }; PHASE_KEYS.forEach(function (k) { g[k] = 0; g._n[k] = 0; }); return g; }
  function addToAgg(g, d) {
    g.issues++; if (d.state === 'closed') g.closed++; else g.open++;
    if (d.approval) g.appr++;
    if (d.validated) g.wV += d.weight; else g.wNV += d.weight;
    g.ret += d.retours; g.comm += d.comments;
    var tm = d._times; PHASE_KEYS.forEach(function (k) { if (tm[k] > 0) { g[k] += tm[k]; g._n[k]++; } });
  }
  function finishAgg(g) { PHASE_KEYS.forEach(function (k) { g[k] = g._n[k] > 0 ? Math.round(g[k] / g._n[k] * 10) / 10 : 0; }); delete g._n; return g; }
  var pivotMap = {};
  detail.forEach(function (d) { if (!d.type) return; if (!pivotMap[d.type]) pivotMap[d.type] = Object.assign({ key: d.type }, blankAgg()); addToAgg(pivotMap[d.type], d); });
  var pivot = Object.keys(pivotMap).map(function (k) { return finishAgg(pivotMap[k]); }).sort(function (a, b) { return b.issues - a.issues; });
  var pivotByKey = {}; pivot.forEach(function (r) { pivotByKey[r.key] = r; });
  // Complète le catalogue : chaque type connu a une entrée (à zéro si absent du filtre courant),
  // pour que les lookups A.pivotByKey[k] (super-groupes à clés fixes, légendes figées) ne plantent jamais.
  Object.keys(typeByKey).forEach(function (k) { if (!pivotByKey[k]) pivotByKey[k] = finishAgg(Object.assign({ key: k }, blankAgg())); });

  // ---------- totaux + KPIs ----------
  var totG = blankAgg(); detail.forEach(function (d) { addToAgg(totG, d); }); finishAgg(totG);
  var totals = { issues: totG.issues, open: totG.open, closed: totG.closed, wV: totG.wV, wNV: totG.wNV, weight: totG.wV + totG.wNV, ret: totG.ret };
  var pct = function (a, b) { return b ? Math.round(a / b * 100) : 0; };
  var cycDays = (function () { var arr = detail.map(function (d) { return d._times.total; }).filter(function (x) { return x > 0; }); return arr.length ? Math.round(arr.reduce(function (s, x) { return s + x; }, 0) / arr.length * 10) / 10 : 0; })();
  var kpis = {
    progress: { closed: totals.closed, total: totals.issues, pct: pct(totals.closed, totals.issues) },
    weight: { v: totals.wV, total: totals.weight, pct: pct(totals.wV, totals.weight) },
    approvals: { with: totG.appr, total: totals.issues, pct: pct(totG.appr, totals.issues) },
    cycle: { days: cycDays }
  };

  // ---------- transversaux ----------
  var TRANSV = [['contractual', 'CONTRACTUAL'], ['unplanned', 'Unplanned'], ['surchargeqa', 'Surcharge QA']];
  var transversal = TRANSV.map(function (tv) {
    var g = Object.assign({ key: tv[0], name: tv[1] }, blankAgg());
    detail.forEach(function (d) { if (d.labels.some(function (l) { return l.toLowerCase() === tv[1].toLowerCase(); })) addToAgg(g, d); });
    finishAgg(g); g.ratio = pct(g.issues, totals.issues); return g;
  }).filter(function (g) { return g.issues > 0; });

  // ---------- temps moyen par phase (global) ----------
  // Phases chronométrées (clé+nom) PILOTÉES par les périodes configurées (ordre admin), repli standard.
  var PH = PERIODS.length
    ? PERIODS.filter(function (p) { return TIMED[p.key]; }).map(function (p) { return [p.key, p.name || p.key]; })
    : [['dev', 'Dev'], ['review', 'Review'], ['qawait', 'QA wait'], ['qa', 'QA'], ['tofix', 'To fix'], ['po', 'PO']];
  var phaseAvg = PH.map(function (p) {
    var arr = detail.map(function (d) { return d._times[p[0]]; }).filter(function (x) { return x > 0; });
    return { key: p[0], name: p[1], days: arr.length ? Math.round(arr.reduce(function (s, x) { return s + x; }, 0) / arr.length * 10) / 10 : 0 };
  });

  // ---------- weight buckets + matrix ----------
  var weightBuckets = FIB.map(function (w) {
    var v = 0, nv = 0; detail.forEach(function (d) { if (d.weight === w) { if (d.validated) v++; else nv++; } }); return { w: w, v: v, nv: nv };
  });
  var weightMatrix = {};
  Object.keys(pivotByKey).forEach(function (key) {
    weightMatrix[key] = FIB.map(function (w) { var v = 0, nv = 0; detail.forEach(function (d) { if (d.type === key && d.weight === w) { if (d.validated) v++; else nv++; } }); return { w: w, v: v, nv: nv }; });
  });

  // ---------- super-groupes ----------
  // Groupes curés (Features, Bugs) + un groupe « Autres types » qui ramasse TOUS les types
  // réellement présents (typeByKey = catalogue COMPLET sur CAT) non couverts par les curés
  // (Tooling, R&D, Refactor, Feature - Optimisation, Documentation, Performance, Sans type, etc.).
  // On filtre types/groupes vides pour ne jamais passer une clé inconnue ni un groupe sans type.
  var SG_CURATED = [
    { key: 'features', name: 'Features', color: 'var(--c-feature)', types: ['feature', 'enh'] },
    { key: 'bugs', name: 'Bugs & Régression', color: 'var(--c-bug)', types: ['bug', 'clientbug', 'regression'] }
  ];
  var sgClaimed = {};
  var superGroups = SG_CURATED.map(function (g) {
    var t = g.types.filter(function (k) { return typeByKey[k]; });
    t.forEach(function (k) { sgClaimed[k] = 1; });
    return { key: g.key, name: g.name, color: g.color, types: t };
  }).filter(function (g) { return g.types.length > 0; });
  var sgRest = Object.keys(typeByKey).filter(function (k) { return !sgClaimed[k]; });
  if (sgRest.length) superGroups.push({ key: 'divers', name: 'Autres types', color: 'var(--c-neutral)', types: sgRest });

  // ---------- vélocité (depuis detail.seg.dev, logique data.js) ----------
  var vel = {};
  people.forEach(function (p) { vel[p.id] = { weeks: Array.from({ length: WEEKS }, function () { return { total: 0, byType: {}, inprog: 0 }; }), devWeeks: new Set(), issues: { o: 0, c: 0 }, fib: {} }; });
  detail.forEach(function (d) {
    var devSegs = d.seg.dev || [];
    var totalDev = devSegs.reduce(function (s, seg) { return s + Math.max(0, seg[1] - seg[0]); }, 0) || 1;
    devSegs.forEach(function (seg) {
      var owner = seg[2] || d.assignees[0]; if (!owner || !vel[owner]) return;
      var wPerDay = d.weight / totalDev;
      var a = Math.max(0, seg[0]), b = Math.min(DAYS, seg[1]);
      for (var day = Math.floor(a); day < Math.ceil(b); day++) {
        // Pondération par le RECOUVREMENT réel du segment sur ce jour (fraction de jour), pas le
        // taux plein : un label Dev actif 30 s (totalDev ≈ 0,0004 j) donnait poids/0,0004 ≈ +22000
        // pts sur la semaine. Invariant restauré : Σ contributions d'une issue = son poids.
        var ov = Math.min(day + 1, b) - Math.max(day, a);
        if (ov <= 0) continue;
        var wAdd = wPerDay * ov;
        var wk = Math.min(WEEKS - 1, Math.floor(day / 7)); vel[owner].devWeeks.add(wk);
        if (d.validated) { vel[owner].weeks[wk].total += wAdd; vel[owner].weeks[wk].byType[d.type] = (vel[owner].weeks[wk].byType[d.type] || 0) + wAdd; }
        else vel[owner].weeks[wk].inprog += wAdd;
      }
    });
    d.assignees.forEach(function (aid) { if (!vel[aid]) return; vel[aid].issues[d.state === 'closed' ? 'c' : 'o']++; vel[aid].fib[d.weight] = (vel[aid].fib[d.weight] || 0) + 1; });
  });

  // ---------- anomalies ----------
  var anomalies = {
    noAssignee: detail.filter(function (d) { return d.noAssigneeFlag; }),
    noMilestone: detail.filter(function (d) { return d.noMilestone; }),
    noWeight: detail.filter(function (d) { return d.weight === 0; }),
    noType: detail.filter(function (d) { return d.noType; }),
    noPrio: detail.filter(function (d) { return d.noPrio; }),
    noApproval: detail.filter(function (d) { return !d.approval; }),
    stale: detail.filter(function (d) { return d.state === 'open' && (TODAY - d.start) >= 30; }),
    multiType: detail.filter(function (d) { return d.multiType; }),
    closedNoMR: detail.filter(function (d) { return d.state === 'closed' && d.mrCount === 0; }),
    closedOpenMR: detail.filter(function (d) { return d.closedOpenMR; })
  };

  // ---------- divers ----------
  var fmtFr = function (ms) { try { return new Date(ms).toLocaleDateString('en', { day: '2-digit', month: 'short' }); } catch (e) { return ''; } };
  var milestone = { name: msName, start: fmtFr(START), end: fmtFr(END - 1) + ' ' + new Date(END - 1).getFullYear(), dayPct: Math.max(0, Math.min(100, pct(NOW - START, END - START))), startDay: 0, endDay: DAYS, today: TODAY, weeks: WEEKS };
  // URL d'issue + nom de projet DÉRIVÉS du webUrl réel (générique : toute instance/projet GitLab, rien en dur).
  var sampleUrl = ''; for (var su = 0; su < CAT.length; su++) { if (CAT[su].webUrl) { sampleUrl = CAT[su].webUrl; break; } }
  var issueBase = '', projName = 'Project';
  var mB = sampleUrl.match(/^(.*\/-\/issues\/)/); if (mB) issueBase = mB[1];
  var mP = sampleUrl.match(/^https?:\/\/[^/]+\/(.+?)\/-\/issues\//); if (mP) { var pp = mP[1].split('/'); projName = pp[pp.length - 1]; }
  var meta = { generated: D.generatedAt || '', extracted: D.lastExtractedAt || '', project: projName, issueBase: issueBase };
  var phases = PH.map(function (p) { return { key: p[0], name: p[1] }; });
  var anomCount = Object.keys(anomalies).reduce(function (s, k) { return s + anomalies[k].length; }, 0);

  return {
    types: types, typeByKey: typeByKey, phases: phases, periods: PERIODS, people: people, peopleById: peopleById,
    // Sélection Utilisateur/Équipe active (usernames minuscules, null = pas de filtre) : consommée
    // par les vues par personne (Vélocité) pour restreindre leurs LIGNES. A.people reste le
    // catalogue des assignés du périmètre (l'onglet Options s'en sert pour lister les membres).
    selectedUsers: D.selectedUsers || null,
    detail: detail, vel: vel, anomalies: anomalies, totals: totals, kpis: kpis, pivot: pivot, pivotByKey: pivotByKey,
    superGroups: superGroups, weightMatrix: weightMatrix, transversal: transversal, phaseAvg: phaseAvg, weightBuckets: weightBuckets,
    milestone: milestone, meta: meta, FIB: FIB,
    filterOptions: { projects: projName ? [projName] : [], milestones: D.availableMilestones || [], labels: D.availableLabels || [], teams: Object.keys(D.teams || {}), users: D.availableUsers || people.map(function (p) { return p.id; }) },
    // Couleurs RÉELLES des labels GitLab (payload .NET : { name: { color, textColor } }) → map name → couleur.
    labelColors: (function () { var m = {}, lc = D.labelColors || {}; for (var k in lc) { var v = lc[k]; m[k] = (v && (typeof v === 'string' ? v : v.color)) || ''; } return m; })(),
    // dayDate/fmtDay : arithmétique CALENDAIRE (composants y/m/d) et non START + d*24h — sinon, au
    // passage à l'heure d'hiver, chaque instant retombe à 23h de la veille et getDate()/getDay()
    // décalent d'un jour tout l'axe (labels, week-ends, 1ers du mois) après la transition.
    cal: (function () {
      var S0 = new Date(START);
      var dayDate = function (d) { return new Date(S0.getFullYear(), S0.getMonth(), S0.getDate() + d); };
      return { START: S0, DAYS: DAYS, TODAY: TODAY, WEEKS: WEEKS, dayDate: dayDate, fmtDay: function (d) { return fmtFr(dayDate(d).getTime()); } };
    })(),
    tabs: [
      { id: 'dashboard', label: 'Dashboard' }, { id: 'charts', label: 'Graphiques' },
      { id: 'anomalies', label: 'Anomalies', count: anomCount }, { id: 'issues', label: 'Issues', count: totals.issues },
      { id: 'calendar', label: 'Calendrier' }, { id: 'velocity', label: 'Vélocité' }
    ]
  };
};

// Refiltre le périmètre (déjà restreint au compte) selon les pills, et reconstruit window.APP
// EN PLACE (les onglets relisent le même objet). Mémoïsé par signature de sélection.
window.__applyFilters = (function () {
  var lastSig = null;
  return function (sel) {
    sel = sel || {};
    var ms = sel.milestones || [], lb = sel.labels || [], tm = sel.teams || [], us = sel.users || [];
    var sig = JSON.stringify([ms, lb, tm, us]);
    if (sig === lastSig && window.APP) return; lastSig = sig;
    var D = window.__DATA__ || {}; var iss = D.issues || [];
    var teams = D.teams || {};
    var uset = {}; us.forEach(function (u) { uset[String(u).toLowerCase()] = 1; });
    tm.forEach(function (t) { (teams[t] || []).forEach(function (u) { uset[String(u).toLowerCase()] = 1; }); });
    var needUser = Object.keys(uset).length > 0;
    var f = iss.filter(function (i) {
      if (ms.length && ms.indexOf(i.milestone) < 0) return false;
      if (lb.length && !(i.labels || []).some(function (l) { return lb.indexOf(l) >= 0; })) return false;
      if (needUser && !(i.assignees || []).some(function (a) { return uset[String(a).toLowerCase()]; })) return false;
      return true;
    });
    var d = {}; for (var k in D) d[k] = D[k]; d.issues = f; d.allIssues = D.issues; d.selectedMilestones = ms;
    // Sélection Utilisateur/Équipe (usernames en minuscules) : transmise à buildAPP pour que les
    // vues PAR PERSONNE (Vélocité) restreignent leurs lignes à la sélection — sans elle, les
    // co-assignés hors sélection gardent une ligne (leurs issues communes restent au périmètre).
    d.selectedUsers = needUser ? Object.keys(uset) : null;
    var fresh = window.buildAPP(d);
    if (!window.APP) window.APP = fresh; else for (var kk in fresh) window.APP[kk] = fresh[kk];
  };
})();
