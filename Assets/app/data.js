// Rich mock data for the full Studio prototype (release 2026-R2).
// Deterministic generator so every reload is identical.
window.APP = (function () {
  // ---- seeded RNG ----
  function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
  const rnd = mulberry32(20262);
  const pick = (arr) => arr[Math.floor(rnd() * arr.length)];
  const ri = (a, b) => a + Math.floor(rnd() * (b - a + 1));

  // ---- milestone calendar ----
  const START = new Date(2026, 2, 8);          // 08 Mar 2026
  const DAYS = 84;                              // -> 31 May 2026
  const TODAY = 60;                             // day offset "aujourd'hui"
  const WEEKS = 12;
  const MS_DAY = 86400000;
  const dayDate = (d) => new Date(START.getTime() + d * MS_DAY);
  // format de date unifié de l'app : ISO aaaa-mm-jj (composants locaux)
  const fmtDay = (d) => { const x = dayDate(d); const p = (n) => ('0' + n).slice(-2); return x.getFullYear() + '-' + p(x.getMonth() + 1) + '-' + p(x.getDate()); };

  // ---- types (GitLab-synced colours via CSS vars) ----
  const types = [
    { key: 'feature',    name: 'Type::Feature',               short: 'Feature' },
    { key: 'enh',        name: 'Type::Feature - Enhancement', short: 'Enhancement' },
    { key: 'bug',        name: 'Type::Bug',                   short: 'Bug' },
    { key: 'clientbug',  name: 'Type::Client Bug',            short: 'Client Bug' },
    { key: 'regression', name: 'Type::Regression',           short: 'Regression' },
  ];
  const typeByKey = Object.fromEntries(types.map(t => [t.key, t]));

  const phases = [
    { key: 'dev',    name: 'Dev' },
    { key: 'review', name: 'Review' },
    { key: 'qawait', name: 'QA wait' },
    { key: 'qa',     name: 'QA' },
    { key: 'tofix',  name: 'To fix' },
    { key: 'po',     name: 'PO' },
  ];

  const people = [
    { id: 'antoine', name: 'Antoine V.', av: 1 },
    { id: 'brice',   name: 'Brice M.',   av: 2 },
    { id: 'carlo',   name: 'Carlo P.',   av: 3 },
    { id: 'denis',   name: 'Denis F.',   av: 4 },
    { id: 'kabbas',  name: 'K. Abbas',   av: 5 },
    { id: 'ash',     name: 'Ash',        av: 6 },
  ];

  const FIB = [1, 2, 3, 5, 8, 13];
  const TITLES = [
    'Refonte du pipeline de déploiement hyperviseur',
    'Crash au démarrage sur cluster multi-nœuds',
    'Latence anormale sur la console de supervision',
    'Ajout du filtre par zone sur la carte temps réel',
    'Régression sur l’export CSV des alarmes',
    'Optimisation du cache des métriques',
    'Erreur 500 sur la synchronisation des agents',
    'Nouveau panneau de vérification des sessions',
    'Le marqueur d’alerte ne se rafraîchit pas',
    'Migration du chiffrement vers la clé de récupération',
    'Améliorer la pagination des conversations',
    'Notification push en double sur Android',
    'Fuite mémoire dans le worker de télémétrie',
    'Refonte de l’écran de connexion serveur',
    'Bug d’affichage des avatars hors-ligne',
    'Ajout du tri par poids validé dans le tableau',
    'Timeout sur l’upload de pièces jointes lourdes',
    'Support des zones dessinées sur la carte',
  ];

  // ---- generate detailed issues ----
  const detail = [];
  for (let i = 0; i < 18; i++) {
    const type = pick(['feature', 'feature', 'enh', 'bug', 'bug', 'clientbug', 'regression']);
    const weight = rnd() < 0.12 ? 0 : pick(FIB.slice(0, 5));
    const assignees = rnd() < 0.35
      ? [pick(people).id, pick(people).id].filter((v, k, a) => a.indexOf(v) === k)
      : [pick(people).id];
    // build phase intervals (day offsets) sequentially
    let cursor = ri(0, 46);
    const seg = {};
    const add = (key, dur) => { if (dur <= 0) return; seg[key] = seg[key] || []; seg[key].push([cursor, cursor + dur]); cursor += dur; };
    if (type === 'feature' || type === 'enh') add('uiux', ri(0, 4));
    add('dev', ri(2, 11));
    add('review', ri(1, 4));
    add('qawait', ri(0, 4));
    add('qa', ri(1, 3));
    let retours = 0;
    if (rnd() < 0.38) { // a to-fix loop
      add('tofix', ri(1, 3)); retours += ri(1, 4);
      add('dev', ri(1, 4));
      add('review', ri(1, 2));
      add('qa', ri(1, 2));
    }
    retours += ri(0, 2);
    add('po', ri(0, 2));
    const end = cursor;
    const closed = end <= TODAY && rnd() < 0.92;
    const validated = closed ? rnd() < 0.84 : rnd() < 0.25;
    const approval = closed ? rnd() < 0.82 : rnd() < 0.4;
    const mrCount = closed ? ri(1, 2) : ri(0, 1);
    detail.push({
      iid: 4800 + i * 7 + ri(0, 5),
      title: TITLES[i % TITLES.length],
      type, weight, assignees,
      state: closed ? 'closed' : 'open',
      validated, approval, retours,
      seg, start: Math.min(...Object.values(seg).flat().map(s => s[0])), end,
      mrCount,
      comments: ri(0, 9),
    });
  }
  detail.sort((a, b) => a.start - b.start);

  // dev author per dev-interval (for multi-dev tint) — pick from assignees
  detail.forEach(d => {
    (d.seg.dev || []).forEach((s, k) => { s[2] = d.assignees[k % d.assignees.length]; });
  });

  // labels currently on the issue + MR approvers (for the Issues detail view)
  const LABELMAP = { uiux: 'Prod::UI/UX', dev: 'Prod::Code In Progress', review: 'Prod::Code review', qawait: 'Prod::QA Backlog', qa: 'Prod::QA InProgress', tofix: 'Prod::To Fix', po: 'Prod:: PO Validation' };
  detail.forEach(d => {
    const present = Object.keys(d.seg);
    const lastPhase = present.sort((a, b) => Math.max(...d.seg[b].map(s => s[1])) - Math.max(...d.seg[a].map(s => s[1])))[0];
    d.labels = [typeByKey[d.type].name, 'Hypervisor', d.state === 'closed' ? 'Prod:: PO Validation' : LABELMAP[lastPhase]].filter(Boolean);
    d.approvers = d.state === 'closed' && d.approval ? Array.from(new Set([pick(people).id, rnd() < 0.4 ? pick(people).id : null].filter(Boolean))) : [];
    d.closedBy = d.state === 'closed' ? (d.approvers[0] || pick(people).id) : null;
  });

  // ---- velocity: weekly weight per person — validated (top) + in-progress (bottom) ----
  const vel = {};
  people.forEach(p => { vel[p.id] = { weeks: Array.from({ length: WEEKS }, () => ({ total: 0, byType: {}, inprog: 0 })), devWeeks: new Set(), issues: { o: 0, c: 0 }, fib: {} }; });
  detail.forEach(d => {
    const devSegs = d.seg.dev || [];
    const totalDev = devSegs.reduce((s, [a, b]) => s + (b - a), 0) || 1;
    devSegs.forEach(([a, b, who]) => {
      const owner = who || d.assignees[0];
      if (!vel[owner]) return;
      const dur = b - a;
      const wPerDay = d.weight / totalDev; // weight spread over this person's dev days
      for (let day = a; day < b; day++) {
        const wk = Math.min(WEEKS - 1, Math.floor(day / 7));
        vel[owner].devWeeks.add(wk);
        if (d.validated) {
          vel[owner].weeks[wk].total += wPerDay;
          vel[owner].weeks[wk].byType[d.type] = (vel[owner].weeks[wk].byType[d.type] || 0) + wPerDay;
        } else {
          vel[owner].weeks[wk].inprog += wPerDay;
        }
      }
    });
    d.assignees.forEach(aid => {
      if (!vel[aid]) return;
      vel[aid].issues[d.state === 'closed' ? 'c' : 'o']++;
      vel[aid].fib[d.weight] = (vel[aid].fib[d.weight] || 0) + 1;
    });
  });

  // flags for anomaly detection (deterministic subsets so lists are stable & non-empty)
  detail.forEach((d, i) => {
    d.noMilestone = i % 11 === 3;
    d.noType = i % 9 === 4;
    d.noPrio = i % 4 === 1;
    d.multiType = i % 7 === 2;
    d.noAssigneeFlag = i % 13 === 5;
    d.closedOpenMR = d.state === 'closed' && i % 6 === 0;
  });

  // ---- anomalies (derived) — list defined by Brice ----
  const anomalies = {
    noAssignee:   detail.filter(d => d.noAssigneeFlag),
    noMilestone:  detail.filter(d => d.noMilestone),
    noWeight:     detail.filter(d => d.weight === 0),
    noType:       detail.filter(d => d.noType),
    noPrio:       detail.filter(d => d.noPrio),
    noApproval:   detail.filter(d => !d.approval),
    stale:        detail.filter(d => d.state === 'open' && (TODAY - d.start) >= 30),
    multiType:    detail.filter(d => d.multiType),
    closedNoMR:   detail.filter(d => d.state === 'closed' && d.mrCount === 0),
    closedOpenMR: detail.filter(d => d.closedOpenMR),
  };

  // ---- top-line KPI (headline release totals) ----
  const totals = {
    issues: 207, open: 39, closed: 168, wV: 312, wNV: 99, weight: 411, ret: 59,
  };
  const kpis = {
    progress:  { closed: 168, total: 207, pct: 81 },
    weight:    { v: 312, total: 411, pct: 76 },
    approvals: { with: 154, total: 207, pct: 74 },
    cycle:     { days: 11.3 },
  };
  // pivot rows by type (headline)
  const pivot = [
    { key: 'feature',    issues: 58, open: 12, closed: 46, appr: 41, wV: 104, wNV: 28, dev: 5.1, rev: 2.2, qawait: 2.6, qa: 1.8, tofix: 1.1, po: 0.8, ret: 14, comm: 132 },
    { key: 'enh',        issues: 41, open: 6,  closed: 35, appr: 32, wV: 71,  wNV: 17, dev: 3.8, rev: 1.5, qawait: 1.9, qa: 1.1, tofix: 0.6, po: 0.5, ret: 7,  comm: 64 },
    { key: 'bug',        issues: 49, open: 9,  closed: 40, appr: 38, wV: 78,  wNV: 18, dev: 3.2, rev: 1.4, qawait: 2.0, qa: 1.6, tofix: 1.3, po: 0.4, ret: 19, comm: 98 },
    { key: 'clientbug',  issues: 33, open: 8,  closed: 25, appr: 24, wV: 44,  wNV: 17, dev: 2.6, rev: 1.1, qawait: 1.5, qa: 1.3, tofix: 0.9, po: 0.6, ret: 11, comm: 71 },
    { key: 'regression', issues: 26, open: 4,  closed: 22, appr: 19, wV: 15,  wNV: 19, dev: 4.5, rev: 1.9, qawait: 2.2, qa: 2.0, tofix: 1.5, po: 0.7, ret: 8,  comm: 43 },
  ];
  // transversal labels — cross-cutting, shown apart from Type::* (hors calcul du total)
  const transversal = [
    { key: 'contractual', name: 'CONTRACTUAL', issues: 38, open: 6, closed: 32, appr: 35, wV: 71, wNV: 14, dev: 4.1, rev: 1.9, qawait: 2.1, qa: 1.7, tofix: 1.0, po: 0.9, ret: 12, comm: 88, ratio: 18 },
    { key: 'unplanned',   name: 'Unplanned',   issues: 29, open: 11, closed: 18, appr: 16, wV: 33, wNV: 22, dev: 3.4, rev: 1.3, qawait: 1.8, qa: 1.5, tofix: 1.4, po: 0.5, ret: 17, comm: 61, ratio: 14 },
    { key: 'surchargeqa', name: 'Surcharge QA', issues: 21, open: 7, closed: 14, appr: 13, wV: 24, wNV: 16, dev: 2.2, rev: 1.1, qawait: 3.6, qa: 3.1, tofix: 1.9, po: 0.4, ret: 21, comm: 47, ratio: 10 },
  ];
  const phaseAvg = [
    { key: 'dev', name: 'Dev', days: 4.2 }, { key: 'review', name: 'Review', days: 1.8 },
    { key: 'qawait', name: 'QA wait', days: 2.3 }, { key: 'qa', name: 'QA', days: 1.4 },
    { key: 'tofix', name: 'To fix', days: 0.9 }, { key: 'po', name: 'PO', days: 0.7 },
  ];
  // weight buckets (issues par valeur de poids) validé/non-validé
  const weightBuckets = [
    { w: 1, v: 18, nv: 6 }, { w: 2, v: 31, nv: 9 }, { w: 3, v: 44, nv: 12 },
    { w: 5, v: 39, nv: 15 }, { w: 8, v: 21, nv: 10 }, { w: 13, v: 9, nv: 6 },
  ];
  const pivotByKey = Object.fromEntries(pivot.map(r => [r.key, r]));

  // super-groups: roll several Type::* into operational families
  const superGroups = [
    { key: 'features', name: 'Features', color: 'var(--c-feature)', types: ['feature', 'enh'] },
    { key: 'bugs', name: 'Bugs & Régression', color: 'var(--c-bug)', types: ['bug', 'clientbug', 'regression'] },
  ];

  // weight matrix — per type, issue count per Fibonacci value, split validated / non-validated.
  // Derived deterministically from the pivot (shape × validation ratio) so it always sums sensibly.
  const WSHAPE = [0.12, 0.20, 0.26, 0.22, 0.13, 0.07];
  const weightMatrix = {};
  pivot.forEach(r => {
    const valRatio = r.wV / (r.wV + r.wNV);
    weightMatrix[r.key] = FIB.map((w, i) => {
      const n = Math.max(1, Math.round(r.issues * WSHAPE[i]));
      const v = Math.round(n * valRatio);
      return { w, v, nv: n - v };
    });
  });

  const milestone = { name: '2026-R2', start: '2026-03-08', end: '2026-05-31', dayPct: 71, startDay: 0, endDay: DAYS, today: TODAY, weeks: WEEKS };
  const meta = { generated: '2026-06-08 15:04', extracted: '2026-06-04', project: 'Hypervisor' };

  return {
    types, typeByKey, phases, people, peopleById: Object.fromEntries(people.map(p => [p.id, p])),
    detail, vel, anomalies, totals, kpis, pivot, pivotByKey, superGroups, weightMatrix, transversal, phaseAvg, weightBuckets, milestone, meta,
    FIB,
    cal: { START, DAYS, TODAY, WEEKS, dayDate, fmtDay },
    tabs: [
      { id: 'dashboard', label: 'Dashboard' }, { id: 'charts', label: 'Graphiques' },
      { id: 'anomalies', label: 'Anomalies', count: 12 }, { id: 'issues', label: 'Issues', count: 207 },
      { id: 'calendar', label: 'Calendrier' }, { id: 'velocity', label: 'Vélocité' },
    ],
  };
})();
