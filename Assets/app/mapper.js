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
  // TIMED = phases chronométrées. Piste 2 : dérivé de p.role (!== 'nogc') ; rétro-compat sur p.timed si role absent.
  var TIMED = {};
  if (PERIODS.length) PERIODS.forEach(function (p) {
    var timed = p.role ? (p.role !== 'nogc') : (p.timed !== false);
    if (timed) TIMED[p.key] = 1;
  });
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
  // ---------- fenêtre de temps ouvré (Options → « Calcul du temps », payload.workTime) ----------
  // Défauts : 9 h → 19 h (10 h/j = maximum légal cadre ; les collaborateurs n'ont pas tous la même
  // plage, une fenêtre large capte tout le monde), jours ouvrés seuls, pas de férié, anti-bruit 0.
  var WT = D.workTime || {};
  var W_START = (typeof WT.startHour === 'number' && WT.startHour >= 0 && WT.startHour <= 23) ? WT.startHour : 9;
  var W_END = (typeof WT.endHour === 'number' && WT.endHour > W_START && WT.endHour <= 24) ? WT.endHour : (W_START < 19 ? 19 : 24);
  var W_WEEKDAYS_ONLY = WT.workingDaysOnly !== false;
  var HOLIDAYS = {}; (WT.holidays || []).forEach(function (h) { if (h) HOLIDAYS[h] = 1; });
  var MIN_SEG_MS = Math.max(0, +WT.minPhaseMinutes || 0) * 60000;
  var HOURS_PER_DAY_MS = (W_END - W_START) * 3600000;
  // Phases de « travail actif » (temps EFFECTIF = somme ouvrée de ces phases, hors attentes).
  // Piste 2 : dérivé des périodes role === 'active' (rétro-compat : role absent → 'active' si chronométrée).
  // WT.effectivePhases n'est plus lu (l'appartenance vit désormais sur la période).
  var EFF_SET = {};
  if (PERIODS.length) PERIODS.forEach(function (p) { if ((p.role || (p.timed === false ? 'nogc' : 'active')) === 'active') EFF_SET[p.key] = 1; });
  else ['dev', 'review', 'qa', 'tofix'].forEach(function (k) { EFF_SET[k] = 1; });
  var isoOf = function (d) { var p = function (n) { return ('0' + n).slice(-2); }; return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()); };
  function workingMs(s, e) {
    if (!(e > s)) return 0;
    var total = 0, cur = new Date(s); cur.setHours(0, 0, 0, 0);
    while (cur.getTime() < e) {
      var dow = cur.getDay();
      if ((!W_WEEKDAYS_ONLY || (dow !== 0 && dow !== 6)) && !HOLIDAYS[isoOf(cur)]) {
        var ws = new Date(cur); ws.setHours(W_START, 0, 0, 0);
        var we = new Date(cur); we.setHours(W_END, 0, 0, 0);
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
    // Anti-bruit : un passage de phase plus court que le seuil (temps RÉEL) est ignoré — élimine
    // les poses/retraits de label accidentels qui polluaient les cycles.
    // acc = temps TRAVAILLÉ (fenêtre ouvrée uniquement).
    var flush = function (ph, from, to) { if (to > from && (to - from) >= MIN_SEG_MS) { acc[ph] += workingMs(from, to); } };
    // Comptage ÉQUILIBRÉ par phase (clés DYNAMIQUES depuis la config) : on accumule le temps ouvré tant
    // qu'au moins un label de la phase est actif. La phase de clé « tofix » → +1 retour à chaque ajout.
    for (var i = 0; i < e.length; i++) {
      var lo = (e[i].label || '').toLowerCase(), t = new Date(e[i].at).getTime(), add = e[i].action === 'add';
      if (isNaN(t)) continue;
      var ph = phaseOf(lo);
      if (!ph || !TIMED[ph]) continue;
      if (acc[ph] === undefined) { acc[ph] = 0; cnt[ph] = 0; since[ph] = null; } // clé timée hors PHASE_KEYS (sécurité)
      if (add) { if (cnt[ph] === 0) since[ph] = t; cnt[ph]++; if (ph === 'tofix') retours++; }
      else if (cnt[ph] > 0) { cnt[ph]--; if (cnt[ph] === 0 && since[ph] !== null) { flush(ph, since[ph], t); since[ph] = null; } }
    }
    // Clôture à closedAt : une issue FERMÉE avec un label de phase encore posé comptait ZÉRO pour
    // cette phase (le temps n'était ajouté qu'au retrait du label) → sous-estimation systématique.
    // Les phases encore actives d'une issue fermée sont clôturées à sa date de fermeture.
    if (iss.state === 'closed' && iss.closedAt) {
      var endRef = new Date(iss.closedAt).getTime();
      if (!isNaN(endRef)) Object.keys(cnt).forEach(function (k) { if (cnt[k] > 0 && since[k] !== null) { flush(k, since[k], endRef); since[k] = null; cnt[k] = 0; } });
    }
    var days = function (ms) { return ms > 0 ? Math.round(ms / HOURS_PER_DAY_MS * 10) / 10 : 0; };
    var out = { total: 0, retours: retours };
    Object.keys(acc).forEach(function (k) { var d = days(acc[k]); out[k] = d; out.total += d; });
    return out;
  }
  // Segment Gantt : même mapping label → phase que les durées (inclut uiux). phaseOf gère config + repli.
  function segKey(lo) { return phaseOf(lo); }

  // ---------- fenêtre milestone (MARQUEURS) + timeline (activité réelle) ----------
  function parseDay(s) { if (!s) return NaN; var p = String(s).split('-'); if (p.length !== 3) return NaN; var d = new Date(+p[0], +p[1] - 1, +p[2]); return d.getTime(); }
  var MSD = D.milestoneDates || {};
  // Bornes de la milestone (filtre pills prioritaire sur la milestone configurée). Elles ne
  // RESTREIGNENT plus la timeline : ce sont des MARQUEURS (barres verticales Calendrier/Vélocité)
  // + la fenêtre du bandeau d'en-tête.
  var selMs = (D.selectedMilestones || []).filter(function (m) { return m && MSD[m]; });
  var msName, MS_START, MS_END;
  if (selMs.length) {
    // Milestone(s) sélectionnée(s) : bornes = union des dates connues (min start, max due).
    var mStarts = selMs.map(function (m) { return parseDay(MSD[m].startDate); }).filter(function (x) { return !isNaN(x); });
    var mEnds = selMs.map(function (m) { return parseDay(MSD[m].dueDate); }).filter(function (x) { return !isNaN(x); });
    MS_START = mStarts.length ? Math.min.apply(null, mStarts) : NaN;
    MS_END = mEnds.length ? Math.max.apply(null, mEnds) : NaN;
    msName = selMs.length === 1 ? selMs[0] : selMs.join(' + ');
  } else if (D.selectedMilestones && D.selectedMilestones.length === 0) {
    // Filtre explicitement « Toutes » : pas de bornes de milestone à marquer.
    msName = 'All milestones'; MS_START = NaN; MS_END = NaN;
  } else {
    // Appel initial (sans info de filtre) : milestone CONFIGURÉE du compte.
    msName = D.milestone || '';
    var msd = MSD[msName] || {};
    MS_START = parseDay(msd.startDate); MS_END = parseDay(msd.dueDate);
  }
  if (!isNaN(MS_START) && !isNaN(MS_END) && MS_END <= MS_START) { MS_START = NaN; MS_END = NaN; } // fenêtre incohérente
  // Timeline = TOUTE l'activité réelle des issues filtrées (événements + création), élargie aux
  // bornes de la milestone (pour que ses barres restent visibles). Rien n'est tronqué aux bornes.
  var allT = [];
  ISSUES.forEach(function (i) {
    (i.labelEvents || []).forEach(function (e) { var t = new Date(e.at).getTime(); if (!isNaN(t)) allT.push(t); });
    if (i.createdAt) { var c = new Date(i.createdAt).getTime(); if (!isNaN(c)) allT.push(c); }
    // closedAt : la fermeture est de l'activité réelle (la vélocité compte le poids validé CETTE semaine-là).
    if (i.closedAt) { var cl = new Date(i.closedAt).getTime(); if (!isNaN(cl)) allT.push(cl); }
  });
  // START ancré à MINUIT LOCAL : les offsets jour (dayOff) et la grille calendaire d'affichage
  // (cal.dayDate, ancrée sur les composants y/m/j) doivent partager la MÊME origine. Avec un START
  // brut (heure du 1er événement, ex. 10h23), toutes les dates affichées glissaient de cette heure :
  // un événement à 04h27 était daté de la VEILLE (écart constaté GitLab vs KPI sur #1405).
  var dayFloor = function (ms) { var d = new Date(ms); return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime(); };
  var START = allT.length ? dayFloor(Math.min.apply(null, allT)) : NaN;
  var END = allT.length ? Math.max.apply(null, allT) : NaN;
  if (!isNaN(MS_START)) START = isNaN(START) ? MS_START : Math.min(START, MS_START);
  if (!isNaN(MS_END)) END = isNaN(END) ? MS_END : Math.max(END, MS_END);
  if (isNaN(START) || isNaN(END)) { START = dayFloor(Date.now() - 84 * MS_DAY); END = Date.now(); }
  // ceil (et non round) : le dernier jour PARTIEL a sa cellule — un événement à 23 h le dernier
  // jour reste dans la fenêtre au lieu d'être écrêté sur l'offset DAYS.
  var DAYS = Math.max(1, Math.ceil((END - START) / MS_DAY));
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

  // ---------- detail (issues façonnées pour les vues : forme de référence de window.APP) ----------
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
      // closeDay : offset (jours) de la FERMETURE — la semaine où la vélocité compte le poids validé.
      closeDay: closed ? clampOff(endRef) : null,
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
  // mr = issues AVEC au moins une MR : dénominateur des approbations (une issue sans MR ne peut
  // pas être approuvée — la compter « non approuvée » fausserait le taux).
  function blankAgg() { var g = { issues: 0, open: 0, closed: 0, appr: 0, mr: 0, wV: 0, wNV: 0, ret: 0, comm: 0, _n: {} }; PHASE_KEYS.forEach(function (k) { g[k] = 0; g._n[k] = 0; }); return g; }
  function addToAgg(g, d) {
    g.issues++; if (d.state === 'closed') g.closed++; else g.open++;
    if (d.mrCount > 0) g.mr++;
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
  // Cycle : moyenne + PERCENTILES (rang le plus proche) sur les issues à temps travaillé > 0.
  // P50 (médiane) = 50 % des issues sortent plus vite ; P85 = engagement tenable (85 % en dessous) —
  // robustes aux issues extrêmes qui déforment la moyenne.
  var cycArr = detail.map(function (d) { return d._times.total; }).filter(function (x) { return x > 0; }).sort(function (a, b) { return a - b; });
  var pctl = function (p) { if (!cycArr.length) return 0; return Math.round(cycArr[Math.min(cycArr.length - 1, Math.max(0, Math.ceil(p / 100 * cycArr.length) - 1))] * 10) / 10; };
  var cycDays = cycArr.length ? Math.round(cycArr.reduce(function (s, x) { return s + x; }, 0) / cycArr.length * 10) / 10 : 0;
  var kpis = {
    progress: { closed: totals.closed, total: totals.issues, pct: pct(totals.closed, totals.issues) },
    weight: { v: totals.wV, total: totals.weight, pct: pct(totals.wV, totals.weight) },
    // Approbations rapportées aux issues AVEC MR (une issue sans MR ne peut pas être approuvée).
    approvals: { with: totG.appr, total: totG.mr, pct: pct(totG.appr, totG.mr) },
    cycle: { days: cycDays, p50: pctl(50), p85: pctl(85) }
  };

  // ---------- transversaux ----------
  // Labels transversaux CONFIGURABLES (Options → Configuration → payload.transversalLabels).
  // Chaque nom de label devient un groupe (clé = slug, pour la key React). Repli sur les labels
  // historiques si non configuré (rétro-compat + export statique CLI qui n'émet pas le champ).
  var DEFAULT_TRANSVERSAL = ['CONTRACTUAL', 'Unplanned', 'Surcharge QA'];
  // Config AUTORITAIRE : un tableau (même VIDE) est respecté tel quel → l'admin peut tout retirer
  // (0 groupe transversal). Le repli sur les défauts ne sert que si le champ est ABSENT du payload
  // (export statique/legacy qui ne le fournit pas).
  var transversalNames = Array.isArray(D.transversalLabels) ? D.transversalLabels : DEFAULT_TRANSVERSAL;
  var tvSlug = function (s) { return String(s).toLowerCase().replace(/[^a-z0-9]+/g, '') || 'tv'; };
  var transversal = transversalNames.map(function (name) {
    var lo = String(name).toLowerCase();
    var g = Object.assign({ key: tvSlug(name), name: name }, blankAgg());
    detail.forEach(function (d) { if (d.labels.some(function (l) { return l.toLowerCase() === lo; })) addToAgg(g, d); });
    finishAgg(g); g.ratio = pct(g.issues, totals.issues); return g;
  }).filter(function (g) { return g.issues > 0; });

  // ---------- temps moyen par phase (global) ----------
  // Phases chronométrées (clé+nom) PILOTÉES par les périodes configurées (ordre admin), repli standard.
  var PH = PERIODS.length
    ? PERIODS.filter(function (p) { return TIMED[p.key]; }).map(function (p) { return [p.key, p.name || p.key]; })
    : [['dev', 'Dev'], ['review', 'Review'], ['qawait', 'QA wait'], ['qa', 'QA'], ['tofix', 'To fix'], ['po', 'PO']];
  // Par phase : days = temps TRAVAILLÉ moyen (jours de fenêtre ouvrée) ; active = la phase compte
  // dans le « temps effectif » (EFF_SET) → la vue Effectif du dashboard ne montre que celles-là.
  var avg1 = function (arr) { return arr.length ? Math.round(arr.reduce(function (s, x) { return s + x; }, 0) / arr.length * 10) / 10 : 0; };
  var phaseAvg = PH.map(function (p) {
    var arr = detail.map(function (d) { return d._times[p[0]]; }).filter(function (x) { return x > 0; });
    return { key: p[0], name: p[1], days: avg1(arr), active: !!EFF_SET[p[0]] };
  });
  // Total de la section « Temps moyen par phase » : effective = Σ des moyennes des SEULES phases de
  // travail actif (= somme des barres affichées en vue Effectif, hors attentes).
  var phaseTotals = {
    effective: Math.round(phaseAvg.reduce(function (a, p) { return a + (p.active ? p.days : 0); }, 0) * 10) / 10
  };

  // ---------- weight matrix ----------
  var weightMatrix = {};
  Object.keys(pivotByKey).forEach(function (key) {
    weightMatrix[key] = FIB.map(function (w) { var v = 0, nv = 0; detail.forEach(function (d) { if (d.type === key && d.weight === w) { if (d.validated) v++; else nv++; } }); return { w: w, v: v, nv: nv }; });
  });

  // ---------- super-groupes ----------
  // Groupes curés (Features, Bugs) + un groupe « Autres types » qui ramasse les types restants
  // (Tooling, R&D, Refactor, Documentation, Performance, Sans type, etc.).
  // En plus des clés curées, chaque groupe ABSORBE par MOTIF les sous-types de son label
  // (ex. « Type::Feature - Optimisation » → Features ; « Type::Bug - Xxx » → Bugs) : les
  // sous-types dynamiques ne tombent plus à tort dans « Autres types ».
  // On filtre types/groupes vides pour ne jamais passer une clé inconnue ni un groupe sans type.
  var SG_CURATED = [
    { key: 'features', name: 'Features', color: 'var(--c-feature)', types: ['feature', 'enh'], match: /^type::\s*feature/ },
    { key: 'bugs', name: 'Bugs & Régression', color: 'var(--c-bug)', types: ['bug', 'clientbug', 'regression'], match: /^type::\s*(client\s*)?bug|^type::\s*regression/ }
  ];
  var sgClaimed = {};
  var superGroups = SG_CURATED.map(function (g) {
    var t = g.types.filter(function (k) { return typeByKey[k]; });
    Object.keys(typeByKey).forEach(function (k) {
      if (sgClaimed[k] || t.indexOf(k) >= 0) return;
      var nm = (typeByKey[k].name || '').toLowerCase();
      if (g.match && g.match.test(nm)) t.push(k);
    });
    t.forEach(function (k) { sgClaimed[k] = 1; });
    return { key: g.key, name: g.name, color: g.color, types: t };
  }).filter(function (g) { return g.types.length > 0; });
  var sgRest = Object.keys(typeByKey).filter(function (k) { return !sgClaimed[k]; });
  if (sgRest.length) superGroups.push({ key: 'divers', name: 'Autres types', color: 'var(--c-neutral)', types: sgRest });

  // ---------- vélocité (depuis detail.seg.dev) ----------
  // VALIDÉ = compté UNE SEULE FOIS, la semaine de la FERMETURE de l'issue (vélocité classique :
  // points livrés par semaine). Le poids est PARTAGÉ À PARTS ÉGALES entre les ASSIGNÉS —
  // indépendamment du temps passé par chacun (règle d'équipe : poids ÷ nb d'assignés).
  // Repli : les porteurs de segments de dev si l'issue n'a aucun assigné.
  // EN COURS (hachuré) = la part de chaque contributeur, étalée sur les jours de dev de l'issue.
  var vel = {};
  people.forEach(function (p) { vel[p.id] = { weeks: Array.from({ length: WEEKS }, function () { return { total: 0, byType: {}, inprog: 0 }; }), devWeeks: new Set(), issues: { o: 0, c: 0 }, fib: {} }; });
  detail.forEach(function (d) {
    var devSegs = d.seg.dev || [];
    // Contributeurs = les assignés (parts égales) ; repli : porteurs de dev distincts.
    var contrib = (d.assignees || []).filter(function (a) { return vel[a]; });
    if (!contrib.length) {
      var seen = {};
      devSegs.forEach(function (seg) { var o = seg[2]; if (o && vel[o] && !seen[o]) { seen[o] = 1; contrib.push(o); } });
    }
    if (contrib.length) {
      var share = d.weight / contrib.length;
      if (!d.validated) {
        // Part étalée sur les jours de dev de l'ISSUE (même chronologie pour tous les contributeurs).
        var totalDev = devSegs.reduce(function (s, seg) { return s + Math.max(0, seg[1] - seg[0]); }, 0);
        if (totalDev > 0 && share > 0) {
          var wPerDay = share / totalDev;
          devSegs.forEach(function (seg) {
            var a = Math.max(0, seg[0]), b = Math.min(DAYS, seg[1]);
            for (var day = Math.floor(a); day < Math.ceil(b); day++) {
              // Pondération par le RECOUVREMENT réel du segment sur ce jour (fraction de jour).
              var ov = Math.min(day + 1, b) - Math.max(day, a);
              if (ov <= 0) continue;
              var wk = Math.min(WEEKS - 1, Math.floor(day / 7));
              contrib.forEach(function (o) { vel[o].devWeeks.add(wk); vel[o].weeks[wk].inprog += wPerDay * ov; });
            }
          });
        }
      } else {
        var wkC = Math.min(WEEKS - 1, Math.max(0, Math.floor((d.closeDay != null ? d.closeDay : d.end) / 7)));
        contrib.forEach(function (o) {
          vel[o].weeks[wkC].total += share;
          vel[o].weeks[wkC].byType[d.type] = (vel[o].weeks[wkC].byType[d.type] || 0) + share;
          vel[o].devWeeks.add(wkC);
        });
      }
    }
    d.assignees.forEach(function (aid) { if (!vel[aid]) return; vel[aid].issues[d.state === 'closed' ? 'c' : 'o']++; vel[aid].fib[d.weight] = (vel[aid].fib[d.weight] || 0) + 1; });
  });

  // ---------- anomalies ----------
  var anomalies = {
    noAssignee: detail.filter(function (d) { return d.noAssigneeFlag; }),
    noMilestone: detail.filter(function (d) { return d.noMilestone; }),
    noWeight: detail.filter(function (d) { return d.weight === 0; }),
    noType: detail.filter(function (d) { return d.noType; }),
    noPrio: detail.filter(function (d) { return d.noPrio; }),
    // « Sans approbation » = une MR EXISTE mais aucune n'est approuvée. Une issue sans MR ne peut
    // pas avoir d'approbation : elle relève de « fermée sans MR », pas d'un double signalement ici.
    noApproval: detail.filter(function (d) { return !d.approval && d.mrCount > 0; }),
    stale: detail.filter(function (d) { return d.state === 'open' && (TODAY - d.start) >= 30; }),
    multiType: detail.filter(function (d) { return d.multiType; }),
    closedNoMR: detail.filter(function (d) { return d.state === 'closed' && d.mrCount === 0; }),
    closedOpenMR: detail.filter(function (d) { return d.closedOpenMR; }),
    // Issues marquées « Surcharge QA » : signal de débordement du circuit QA.
    surchargeQa: detail.filter(function (d) { return d.labels.some(function (l) { return l.toLowerCase() === 'surcharge qa'; }); })
  };

  // ---------- divers ----------
  // Format de date UNIFIÉ de l'app : ISO aaaa-mm-jj — composants LOCAUX (pas de toISOString UTC,
  // qui décalerait d'un jour selon le fuseau).
  var fmtFr = function (ms) { try { var d = new Date(ms); var p = function (n) { return ('0' + n).slice(-2); }; return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()); } catch (e) { return ''; } };
  // Bandeau d'en-tête : dates/avancement de la MILESTONE quand ses bornes sont connues (la
  // timeline élargie ne doit pas diluer la progression affichée) ; repli sur la timeline sinon.
  var HD_START = isNaN(MS_START) ? START : MS_START, HD_END = isNaN(MS_END) ? END : MS_END;
  // end : plus de suffixe d'année — le format ISO aaaa-mm-jj la porte déjà.
  var milestone = { name: msName, start: fmtFr(HD_START), end: fmtFr(HD_END - 1), dayPct: Math.max(0, Math.min(100, pct(NOW - HD_START, HD_END - HD_START))) };
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
    superGroups: superGroups, weightMatrix: weightMatrix, transversal: transversal, transversalNames: transversalNames, phaseAvg: phaseAvg, phaseTotals: phaseTotals,
    milestone: milestone, meta: meta, FIB: FIB,
    // Dashboard modulaire (config Dashboard, payload window.__DATA__.dashboard). Vide ⇒ nav historique.
    pages: (D.dashboard && D.dashboard.pages) || [],
    defaultPageId: (D.dashboard && D.dashboard.defaultPageId) || null,
    filterOptions: { projects: projName ? [projName] : [], milestones: D.availableMilestones || [], labels: D.availableLabels || [], teams: Object.keys(D.teams || {}), users: D.availableUsers || people.map(function (p) { return p.id; }) },
    // Couleurs RÉELLES des labels GitLab (payload .NET : { name: { color, textColor } }) → map name → couleur.
    labelColors: (function () { var m = {}, lc = D.labelColors || {}; for (var k in lc) { var v = lc[k]; m[k] = (v && (typeof v === 'string' ? v : v.color)) || ''; } return m; })(),
    // dayDate/fmtDay : arithmétique CALENDAIRE (composants y/m/d) et non START + d*24h — sinon, au
    // passage à l'heure d'hiver, chaque instant retombe à 23h de la veille et getDate()/getDay()
    // décalent d'un jour tout l'axe (labels, week-ends, 1ers du mois) après la transition.
    cal: (function () {
      var S0 = new Date(START);
      var dayDate = function (d) { return new Date(S0.getFullYear(), S0.getMonth(), S0.getDate() + d); };
      // msStart/msEnd : offsets (jours) des bornes de la milestone sur la timeline — null si inconnues.
      // Consommés par Calendrier/Vélocité pour tracer les barres verticales de début/fin.
      var msOff = function (t) { return isNaN(t) ? null : Math.max(0, Math.min(DAYS, Math.round((t - START) / MS_DAY))); };
      return { DAYS: DAYS, TODAY: TODAY, WEEKS: WEEKS, msStart: msOff(MS_START), msEnd: msOff(MS_END), dayDate: dayDate, fmtDay: function (d) { return fmtFr(dayDate(d).getTime()); } };
    })(),
    // Badges de la sidebar (les libellés d'onglets viennent de l'i18n : t('nav_'+id)).
    tabs: [
      { id: 'anomalies', count: anomCount }, { id: 'issues', count: totals.issues }
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
