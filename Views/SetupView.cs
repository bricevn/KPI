// SetupView.cs — assistant de première mise en service du dashboard KPI.
//
// Drop ce fichier dans GitLabExporter/Views/ (namespace GitLabExporter.Views).
// Page autonome (CSS + JS inline, CSP-safe) : aucun JS externe (seules Google Fonts,
// déjà autorisées par la CSP), et la page ne parle qu'à des endpoints same-origin.
//
// Flux : /setup (admin, tant que !IsConfigured) →
//   1. POST /api/setup/test     → valide la connexion, renvoie projets + groupes
//   2. POST /api/setup/labels   → labels des projets sélectionnés (mapping phases)
//   3. POST /api/setup          → écrit appsettings.json + recharge → redirige vers /
//
// Voir WebDashboard.setup.patch.md pour le câblage serveur.
using GitLabExporter.Config;

namespace GitLabExporter.Views;

public static class SetupView
{
    public static string Page(AuthConfig auth)
    {
        var defaultInstance = !string.IsNullOrWhiteSpace(auth.Authority) ? auth.Authority.TrimEnd('/') : "https://gitlab.com";
        return Html.Replace("__DEFAULT_INSTANCE__", HtmlAttr(defaultInstance));
    }

    private static string HtmlAttr(string s) =>
        (s ?? "").Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

    private const string Html = """
<!DOCTYPE html>
<html lang="fr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Mise en service · KPI</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&family=IBM+Plex+Mono:wght@400;500;600&display=swap" rel="stylesheet">
<style>
:root{
  --bg:#0a0e13;--panel:#141a22;--panel-2:#1b232d;--panel-3:#222c37;
  --ink:#e9eef4;--ink-dim:#9aa6b6;--ink-faint:#5f6b7a;--line:#222c37;--line-2:#1b232d;
  --accent:#2b7fff;--accent-soft:rgba(43,127,255,.16);
  --good:#1f9d6b;--good-soft:rgba(31,157,107,.16);--bad:#ff6f63;--bad-soft:rgba(255,111,99,.14);
  --disp:'Space Grotesk',system-ui,sans-serif;--sans:system-ui,'Segoe UI',sans-serif;
  --mono:'IBM Plex Mono',ui-monospace,monospace;--ease:cubic-bezier(.2,0,0,1);
}
*{box-sizing:border-box;}
html,body{margin:0;height:100%;background:var(--bg);color:var(--ink);font-family:var(--sans);}
.suA{position:fixed;inset:0;display:flex;flex-direction:column;}
.suA-top{display:flex;align-items:center;justify-content:space-between;padding:22px 36px;border-bottom:1px solid var(--line-2);}
.brand{display:flex;align-items:center;gap:11px;}
.mark{width:36px;height:36px;border-radius:10px;background:var(--accent);display:flex;align-items:center;justify-content:center;flex:none;}
.bn{font-family:var(--disp);font-weight:700;font-size:16px;line-height:1;}
.bs{font-size:11px;color:var(--ink-faint);margin-top:3px;}
.count{font-family:var(--disp);font-weight:600;font-size:13px;color:var(--ink-faint);}
.step{padding:30px 36px 8px;}
.stepper{display:flex;align-items:center;max-width:760px;margin:0 auto;}
.node{display:flex;flex-direction:column;align-items:center;gap:8px;flex:none;width:96px;}
.dot{width:34px;height:34px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-family:var(--disp);font-weight:700;font-size:14px;background:var(--panel-3);color:var(--ink-faint);}
.node.cur .dot{background:var(--accent);color:#fff;box-shadow:0 0 0 5px var(--accent-soft);}
.node.done .dot{background:var(--good-soft);color:var(--good);}
.nl{font-size:11.5px;color:var(--ink-faint);font-weight:600;}
.node.cur .nl{color:var(--ink);}
.node.done{cursor:pointer;}
.line{flex:1;height:2px;background:var(--line);margin-bottom:25px;}
.line.done{background:var(--good);}
.body{flex:1;overflow-x:hidden;overflow-y:auto;scrollbar-width:thin;scrollbar-color:var(--panel-3) transparent;}
.body::-webkit-scrollbar{width:11px;}
.body::-webkit-scrollbar-thumb{background:var(--panel-3);border-radius:999px;border:3px solid var(--bg);}
.bodyinner{min-height:100%;display:flex;align-items:flex-start;justify-content:center;padding:14px 36px 30px;}
.card{width:100%;display:flex;flex-direction:column;gap:18px;}
.cardhead{display:flex;align-items:center;gap:16px;}
.hero{width:60px;height:60px;border-radius:16px;background:var(--accent-soft);color:var(--accent);display:flex;align-items:center;justify-content:center;flex:none;}
.eyebrow{font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.08em;color:var(--ink-faint);}
.h{font-family:var(--disp);font-weight:700;font-size:24px;letter-spacing:-.02em;margin:5px 0 0;}
.sub{color:var(--ink-dim);line-height:1.5;margin:0;font-size:14px;}
.field{display:flex;flex-direction:column;gap:7px;}
.flabel{font-size:12.5px;font-weight:600;color:var(--ink-dim);}
.flabel .req{color:var(--accent);}
.box{display:flex;align-items:center;gap:10px;height:48px;padding:0 13px;background:var(--panel-2);border:1.5px solid var(--line);border-radius:12px;transition:border-color .14s,box-shadow .14s,background .14s;}
.box:focus-within{border-color:var(--accent);background:var(--panel);box-shadow:0 0 0 4px var(--accent-soft);}
.box .ic{color:var(--ink-faint);flex:none;display:flex;}
.box input{flex:1;min-width:0;border:0;background:none;outline:none;color:var(--ink);font-family:var(--mono);font-size:13.5px;}
.box input::placeholder{color:var(--ink-faint);font-family:var(--sans);}
.box .eye{border:0;background:none;cursor:pointer;color:var(--ink-faint);display:flex;padding:4px;}
.fhint{font-size:11.5px;color:var(--ink-faint);}
.togrow{display:flex;align-items:center;gap:12px;}
.togrow .tt{font-size:13px;font-weight:600;}
.togrow .ts{font-size:11.5px;color:var(--ink-faint);margin-top:2px;}
.tog{width:44px;height:25px;border-radius:999px;border:0;cursor:pointer;background:var(--panel-3);position:relative;flex:none;transition:background .15s;}
.tog.on{background:var(--accent);}
.tog b{position:absolute;top:3px;left:3px;width:19px;height:19px;border-radius:50%;background:#fff;transition:left .15s;}
.tog.on b{left:22px;}
.btn{display:inline-flex;align-items:center;justify-content:center;gap:9px;height:46px;padding:0 22px;border:0;border-radius:12px;cursor:pointer;font-family:var(--disp);font-weight:600;font-size:14.5px;transition:transform .12s,filter .12s,background .12s,border-color .12s;}
.btn:hover{transform:translateY(-1px);}
.btn.primary{background:var(--accent);color:#fff;box-shadow:0 6px 18px rgba(43,127,255,.28);}
.btn.ghost{background:transparent;color:var(--ink-dim);border:1px solid var(--line);}
.btn.outline{background:var(--panel-2);color:var(--ink);border:1px solid var(--line);}
.btn.outline:hover{border-color:var(--accent);}
.btn.sm{height:40px;padding:0 16px;font-size:13.5px;}
.btn.disabled{opacity:.45;pointer-events:none;}
.chip{display:inline-flex;align-items:center;gap:6px;height:30px;padding:0 12px;border-radius:999px;font-size:12px;font-weight:600;}
.chip.neutral{background:var(--panel-3);color:var(--ink-dim);}
.chip.ok{background:var(--good-soft);color:var(--good);}
.chip.err{background:var(--bad-soft);color:var(--bad);}
.spin{width:13px;height:13px;border:2px solid var(--ink-faint);border-top-color:transparent;border-radius:50%;animation:sp .7s linear infinite;}
@keyframes sp{to{transform:rotate(360deg);}}
.req{font-size:11.5px;color:var(--ink-faint);}
.req .r{color:var(--accent);font-weight:700;}
.note{display:flex;gap:9px;align-items:flex-start;padding:11px 13px;border-radius:12px;background:var(--accent-soft);border-left:3px solid var(--accent);color:var(--ink-dim);font-size:12.5px;line-height:1.45;}
.note b{color:var(--ink);font-weight:600;}.note .ic{color:var(--accent);flex:none;display:flex;margin-top:1px;}
.checklist{display:grid;grid-template-columns:1fr 1fr;gap:9px;}
.chk{display:flex;align-items:center;gap:10px;padding:11px 13px;border-radius:12px;cursor:pointer;background:var(--panel-2);border:1.5px solid var(--line);text-align:left;}
.chk:hover{border-color:var(--ink-faint);}
.chk.on{border-color:var(--accent);background:color-mix(in srgb,var(--accent) 8%,var(--panel-2));}
.chkbox{width:19px;height:19px;border-radius:6px;flex:none;border:1.5px solid var(--line);display:flex;align-items:center;justify-content:center;color:#fff;}
.chk.on .chkbox{background:var(--accent);border-color:var(--accent);}
.chkl{flex:1;min-width:0;font-size:13px;font-weight:600;color:var(--ink);}
.chkl b{font-family:var(--mono);font-weight:600;color:var(--ink-faint);font-size:11.5px;margin-left:6px;}
.grp{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:var(--ink-faint);background:var(--panel-3);padding:3px 7px;border-radius:999px;flex:none;}
.mini{position:relative;display:inline-flex;align-items:center;height:34px;padding:0 7px 0 11px;flex:none;background:var(--panel);border:1px solid var(--line);border-radius:9px;}
.mini select{appearance:none;border:0;background:none;outline:none;color:var(--ink);font-family:var(--sans);font-size:12.5px;cursor:pointer;padding-right:3px;}
.mini select option{background:var(--panel-2);color:var(--ink);}
.mini .ic{color:var(--ink-faint);display:flex;pointer-events:none;}
.map{border:1px solid var(--line);border-radius:14px;overflow:hidden;background:var(--panel-2);}
.maprow{display:flex;align-items:center;gap:11px;padding:9px 14px;}
.maprow+.maprow{border-top:1px solid var(--line-2);}
.dot2{width:9px;height:9px;border-radius:50%;flex:none;}
.mlabel{flex:1;min-width:0;font-family:var(--mono);font-size:12.5px;color:var(--ink);}
.arrow{color:var(--ink-faint);display:flex;flex:none;}
.teamcol{display:flex;flex-direction:column;gap:12px;}
.team{background:var(--panel-2);border:1px solid var(--line);border-radius:14px;padding:13px 14px;}
.teamh{display:flex;align-items:center;gap:9px;margin-bottom:11px;}
.teamname{min-width:0;width:170px;background:none;border:0;outline:none;color:var(--ink);font-family:var(--disp);font-weight:700;font-size:14.5px;border-bottom:1.5px solid transparent;padding:2px 0;}
.teamname:focus{border-bottom-color:var(--accent);}
.glbadge,.newbadge{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.04em;padding:3px 8px;border-radius:999px;flex:none;}
.glbadge{background:var(--good-soft);color:var(--good);}.newbadge{background:var(--accent-soft);color:var(--accent);}
.teamx{margin-left:auto;border:0;background:none;color:var(--ink-faint);font-size:20px;line-height:1;cursor:pointer;padding:0 4px;flex:none;}
.teamx:hover{color:var(--bad);}
.mlist{display:flex;flex-direction:column;gap:7px;}
.mrow{display:flex;align-items:center;gap:10px;}
.av{border-radius:50%;color:#fff;display:inline-flex;align-items:center;justify-content:center;font-weight:700;flex:none;}
.mname{flex:1;min-width:0;font-size:13px;color:var(--ink);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
.mx{border:0;background:none;color:var(--ink-faint);font-size:18px;line-height:1;cursor:pointer;padding:0 4px;flex:none;}
.mx:hover{color:var(--bad);}
.addsel{display:inline-flex;align-items:center;gap:6px;padding:6px 11px 6px 9px;border-radius:999px;border:1px dashed var(--line);color:var(--ink-dim);margin-top:2px;}
.addsel:hover{border-color:var(--accent);color:var(--accent);}
.addsel select{appearance:none;border:0;background:none;outline:none;color:inherit;font-family:var(--sans);font-size:12px;cursor:pointer;}
.addsel select option{background:var(--panel-2);color:var(--ink);}
.empty{font-size:12px;color:var(--ink-faint);font-style:italic;padding:2px 0;}
.recap{border:1px solid var(--line);border-radius:14px;overflow:hidden;background:var(--panel-2);}
.rrow{display:flex;align-items:center;gap:13px;padding:14px 16px;}
.rrow+.rrow{border-top:1px solid var(--line-2);}
.ric{width:30px;height:30px;border-radius:9px;flex:none;display:flex;align-items:center;justify-content:center;background:var(--accent-soft);color:var(--accent);}
.rk{font-size:13px;font-weight:600;color:var(--ink-dim);width:118px;flex:none;}
.rv{flex:1;min-width:0;font-family:var(--mono);font-size:13px;color:var(--ink);}
.redit{border:0;background:none;color:var(--ink-faint);font-size:12.5px;cursor:pointer;padding:5px 9px;border-radius:8px;flex:none;}
.redit:hover{color:var(--accent);background:var(--accent-soft);}
.foot{display:flex;align-items:center;justify-content:space-between;padding:20px 36px;border-top:1px solid var(--line-2);}
@media(max-width:720px){.checklist{grid-template-columns:1fr;}.suA-top,.foot{padding-left:20px;padding-right:20px;}.bodyinner{padding:14px 20px 24px;}}
</style>
</head>
<body>
<div id="app"></div>
<script>
(function(){
  var PHASES=[['none','Non suivi','#5f6b7a'],['dev','Développement','#2188ff'],['review','Revue de code','#8957e5'],['qawait','Attente QA','#b8800a'],['qa','QA','#c79a06'],['tofix','À corriger','#ec4899'],['po','Validation PO','#0f9e8e'],['uiux','UI/UX','#2dd4bf']];
  var phaseColor=function(k){for(var i=0;i<PHASES.length;i++)if(PHASES[i][0]===k)return PHASES[i][2];return PHASES[0][2];};
  var STEP_META=[['Connexion','link'],['Projets','box'],['Phases','layers'],['Équipes','users'],['Vérif.','rocket']];
  var P={link:'<path d="M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1.5 1.5"/><path d="M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1.5-1.5"/>',server:'<rect x="3" y="4" width="18" height="7" rx="2"/><rect x="3" y="13" width="18" height="7" rx="2"/><path d="M7 7.5h.01M7 16.5h.01"/>',key:'<circle cx="7.5" cy="15.5" r="3.5"/><path d="M10 13 21 2M18 5l2.5 2.5M15.5 7.5L18 10"/>',eye:'<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/>',box:'<path d="M21 8 12 3 3 8v8l9 5 9-5Z"/><path d="m3 8 9 5 9-5M12 13v8"/>',layers:'<path d="m12 2 9 5-9 5-9-5z"/><path d="m21 12-9 5-9-5"/><path d="m21 17-9 5-9-5"/>',users:'<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/>',check:'<path d="M20 6 9 17l-5-5"/>',chevR:'<path d="m9 18 6-6-6-6"/>',chevL:'<path d="m15 18-6-6 6-6"/>',chevD:'<path d="m6 9 6 6 6-6"/>',arrow:'<path d="M5 12h14M13 6l6 6-6 6"/>',zap:'<path d="M13 2 3 14h9l-1 8 10-12h-9l1-8Z"/>',info:'<circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/>',plus:'<path d="M12 5v14M5 12h14"/>',rocket:'<path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z"/><path d="m12 15-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z"/><path d="M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0"/><path d="M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5"/>'};
  function ic(n,s){s=s||18;return '<svg width="'+s+'" height="'+s+'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">'+P[n]+'</svg>';}
  var MARK='<svg width="20" height="20" viewBox="0 0 24 24"><rect x="3" y="13" width="4.2" height="7" rx="1.4" fill="#fff" opacity="0.82"/><rect x="9.9" y="9" width="4.2" height="11" rx="1.4" fill="#fff" opacity="0.92"/><rect x="16.8" y="4.5" width="4.2" height="15.5" rx="1.4" fill="#fff"/><path d="M4 8.5 L11 6 L19 2.5" fill="none" stroke="#fff" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" opacity="0.9"/><circle cx="19" cy="2.5" r="1.7" fill="#fff"/></svg>';
  var AVC=['#0072B2','#8957e5','#0f9e8e','#d97706','#b3231b','#2b7fff','#c2410c','#6d28d9'];
  function esc(s){return (s==null?'':String(s)).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');}
  function avColor(id){var s=0;for(var i=0;i<id.length;i++)s+=id.charCodeAt(i);return AVC[s%AVC.length];}
  function av(id,name,sz){sz=sz||24;var ini=(name||id||'?').charAt(0).toUpperCase();return '<span class="av" style="width:'+sz+'px;height:'+sz+'px;font-size:'+(sz*0.46)+'px;background:'+avColor(id)+'">'+esc(ini)+'</span>';}
  function guessPhase(l){l=l.toLowerCase();if(l.indexOf('code')>=0&&l.indexOf('progress')>=0)return 'dev';if(l.indexOf('review')>=0)return 'review';if(l.indexOf('backlog')>=0)return 'qawait';if(l.indexOf('qa')>=0&&l.indexOf('progress')>=0)return 'qa';if(l.indexOf('to fix')>=0)return 'tofix';if(l.indexOf('validation')>=0||/\bpo\b/.test(l))return 'po';if(l.indexOf('ui/ux')>=0)return 'uiux';return 'none';}

  var ST={step:0,baseUrl:'__DEFAULT_INSTANCE__',token:'',timeout:'60',selfSigned:false,showTok:false,
    test:'idle',projects:[],groups:[],importIds:[],labels:[],labelsLoaded:false,labelPhase:{},teams:[],memberships:[],saving:false,saveErr:''};
  var app=document.getElementById('app');

  function canNext(){return ST.step!==0||ST.test==='ok';}

  function render(){
    var h='<div class="suA"><div class="suA-top"><div class="brand"><div class="mark">'+MARK+'</div><div><div class="bn">KPI</div><div class="bs">Mise en service</div></div></div><div class="count">Étape '+(ST.step+1)+' sur 5</div></div>';
    h+='<div class="step"><div class="stepper">';
    for(var i=0;i<STEP_META.length;i++){var st=i<ST.step?'done':i===ST.step?'cur':'';h+='<div class="node '+st+'" data-act="goto:'+i+'"><div class="dot">'+(i<ST.step?ic('check',16):(i+1))+'</div><div class="nl">'+STEP_META[i][0]+'</div></div>';if(i<STEP_META.length-1)h+='<div class="line'+(i<ST.step?' done':'')+'"></div>';}
    h+='</div></div><div class="body" id="body"><div class="bodyinner"><div class="card" style="max-width:'+[560,600,640,780,600][ST.step]+'px">'+head()+stepBody()+'</div></div></div>';
    h+='<div class="foot"><button class="btn ghost'+(ST.step===0?' disabled':'')+'" data-act="back">'+ic('chevL',16)+'Retour</button>';
    if(ST.step===4)h+='<button class="btn primary'+(ST.saving?' disabled':'')+'" data-act="launch">'+(ST.saving?'<span class="spin"></span>Enregistrement…':ic('rocket',16)+'Lancer le dashboard')+'</button>';
    else h+='<button class="btn primary'+(canNext()?'':' disabled')+'" data-act="next">Continuer '+ic('chevR',16)+'</button>';
    h+='</div></div>';
    app.innerHTML=h;
  }
  function head(){
    var meta=[['Étape 1 · Connexion','Connexion à GitLab','link'],['Étape 2 · Projets','Projets à importer','box'],['Étape 3 · Phases','Phases de production','layers'],['Étape 4 · Équipes','Équipes','users'],['Étape 5 · Vérification','Tout est prêt','rocket']][ST.step];
    var subs=["Renseignez l'instance et un token de service pour permettre l'extraction des données.","Choisissez les projets à suivre par défaut. La liste provient de la connexion testée.","Associez les labels qui représentent une phase de production ; laissez « Non suivi » pour les autres.","Vérifiez les équipes importées (groupes GitLab) et ajustez si besoin.","Vérifiez la configuration. Vous pourrez tout modifier ensuite dans Options."];
    return '<div class="cardhead"><div class="hero">'+ic(meta[2],28)+'</div><div><div class="eyebrow">'+meta[0]+'</div><h2 class="h">'+meta[1]+'</h2></div></div><p class="sub">'+subs[ST.step]+'</p>';
  }

  function stepBody(){
    if(ST.step===0)return s0();
    if(ST.step===1)return s1();
    if(ST.step===2)return s2();
    if(ST.step===3)return s3();
    return s4();
  }
  function s0(){
    var chip=ST.test==='testing'?'<span class="chip neutral"><span class="spin"></span>Test en cours…</span>':ST.test==='ok'?'<span class="chip ok">'+ic('check',14)+'Connecté · '+ST.projects.length+' projets accessibles</span>':ST.test==='err'?'<span class="chip err">Échec · vérifiez l\u0027URL et le token</span>':'<span class="chip neutral">Non testé</span>';
    return '<div class="field"><div class="flabel">Base URL <span class="req">*</span></div><div class="box">'+sic('server')+'<input data-field="baseUrl" value="'+esc(ST.baseUrl)+'"></div></div>'
      +'<div class="field"><div class="flabel">Token de service <span class="req">*</span></div><div class="box">'+sic('key')+'<input data-field="token" type="'+(ST.showTok?'text':'password')+'" placeholder="glpat-xxxxxxxxxxxxxxxxxxxx" value="'+esc(ST.token)+'"><button class="eye" data-act="eye">'+ic('eye',17)+'</button></div><div class="fhint">Scope <b>read_api</b>. Stocké côté serveur pour l\u0027extraction, jamais affiché aux utilisateurs.</div></div>'
      +'<div style="display:grid;grid-template-columns:150px 1fr;gap:20px;align-items:start"><div class="field"><div class="flabel">Timeout (s)</div><div class="box"><input data-field="timeout" value="'+esc(ST.timeout)+'"></div><div class="fhint">Délai max d\u0027une requête à l\u0027API.</div></div>'
      +'<div class="togrow" style="margin-top:26px"><button class="tog'+(ST.selfSigned?' on':'')+'" data-act="self"><b></b></button><div><div class="tt">Certificats auto-signés</div><div class="ts">Pour les instances internes</div></div></div></div>'
      +'<div style="display:flex;align-items:center;gap:12px;flex-wrap:wrap"><button class="btn outline sm" data-act="test"'+(ST.test==='testing'?' disabled':'')+'>'+ic('zap',16)+'Tester la connexion</button>'+chip+'</div>'
      +'<div class="req"><span class="r">*</span> champ obligatoire · une connexion réussie est requise pour continuer.</div>';
  }
  function s1(){
    var h='<div class="note">'+sic('info')+'<div><b>'+ST.importIds.length+'</b> projet(s) sélectionné(s) sur '+ST.projects.length+' accessibles.</div></div><div class="checklist">';
    for(var i=0;i<ST.projects.length;i++){var p=ST.projects[i];var on=ST.importIds.indexOf(p.id)>=0;h+='<button class="chk'+(on?' on':'')+'" data-act="proj:'+p.id+'"><span class="chkbox">'+(on?ic('check',13):'')+'</span><span class="chkl">'+esc(p.name)+'<b>#'+p.id+'</b></span><span class="grp">'+esc(p.group||'')+'</span></button>';}
    return h+'</div>';
  }
  function s2(){
    if(!ST.labelsLoaded)return '<div class="note">'+sic('info')+'<div>Chargement des labels des projets sélectionnés…</div></div>';
    var h='<div class="note">'+sic('info')+'<div>Associez les labels qui représentent une phase ; laissez <b>Non suivi</b> pour les autres. Tous les labels des projets sélectionnés sont listés.</div></div><div class="map">';
    for(var i=0;i<ST.labels.length;i++){var l=ST.labels[i];var ph=ST.labelPhase[l]||'none';h+='<div class="maprow"><span class="dot2" style="background:'+phaseColor(ph)+'"></span><span class="mlabel">'+esc(l)+'</span><span class="arrow">'+ic('arrow',15)+'</span>'+miniSel('phase:'+i,ph,PHASES.map(function(p){return [p[0],p[1]];}))+'</div>';}
    return h+'</div>';
  }
  function s3(){
    var roleOpts=[['lead','Lead'],['member','Membre']];
    var h='<div class="note">'+sic('info')+'<div>Équipes importées depuis les <b>groupes GitLab</b>. <b>Lead</b> = Maintainer · <b>Membre</b> = Developer. Un membre peut appartenir à <b>plusieurs équipes</b>.</div></div><div class="teamcol">';
    for(var t=0;t<ST.teams.length;t++){var tm=ST.teams[t];var mem=ST.memberships.filter(function(m){return m.teamId===tm.id;});
      h+='<div class="team"><div class="teamh"><input class="teamname" data-team="'+t+'" value="'+esc(tm.name)+'">'+(tm.gitlab?'<span class="glbadge">groupe GitLab</span>':'<span class="newbadge">nouvelle</span>')+'<button class="teamx" data-act="rmteam:'+tm.id+'">×</button></div><div class="mlist">';
      if(mem.length===0)h+='<div class="empty">Aucun membre — ajoutez-en ci-dessous</div>';
      for(var j=0;j<mem.length;j++){var m=mem[j];var nm=POOLname(m.pid);h+='<div class="mrow">'+av(m.pid,nm,24)+'<span class="mname">'+esc(nm)+'</span>'+miniSel('role:'+m.pid+'~'+tm.id,m.role,roleOpts)+'<button class="mx" data-act="rmmem:'+m.pid+'~'+tm.id+'">×</button></div>';}
      var avail=allPeople().filter(function(pid){return !mem.some(function(m){return m.pid===pid;});});
      if(avail.length){h+='<div class="addsel">'+ic('plus',14)+'<select data-add="'+tm.id+'"><option value="">Ajouter un membre…</option>';for(var a=0;a<avail.length;a++)h+='<option value="'+esc(avail[a])+'">'+esc(POOLname(avail[a]))+'</option>';h+='</select></div>';}
      h+='</div></div>';
    }
    return h+'</div><button class="btn outline sm" style="align-self:flex-start;margin-top:12px" data-act="addteam">'+ic('plus',16)+'Nouvelle équipe</button>';
  }
  function s4(){
    var imp=ST.projects.filter(function(p){return ST.importIds.indexOf(p.id)>=0;}).map(function(p){return p.name;});
    var mapped=0;for(var k in ST.labelPhase)if(ST.labelPhase[k]&&ST.labelPhase[k]!=='none')mapped++;
    var ppl={};ST.memberships.forEach(function(m){ppl[m.pid]=1;});
    var rows=[['link','Connexion',ST.baseUrl.replace(/^https?:\/\//,''),0],['box','Projets',imp.length?imp.join(', '):'Aucun',1],['layers','Phases',mapped+' labels liés à une phase',2],['users','Équipes',ST.teams.length+' équipes · '+Object.keys(ppl).length+' personnes',3]];
    var h='<div class="recap">';
    for(var i=0;i<rows.length;i++)h+='<div class="rrow"><span class="ric">'+ic(rows[i][0],15)+'</span><div class="rk">'+rows[i][1]+'</div><div class="rv">'+esc(rows[i][2])+'</div><button class="redit" data-act="goto:'+rows[i][3]+'">Modifier</button></div>';
    h+='</div>';
    if(ST.saveErr)h+='<div class="note" style="background:var(--bad-soft);border-left-color:var(--bad)"><span class="ic" style="color:var(--bad)">'+ic('info',16)+'</span><div>'+esc(ST.saveErr)+'</div></div>';
    return h;
  }
  function sic(n){return '<span class="ic">'+ic(n,16)+'</span>';}
  function miniSel(act,val,opts){var h='<div class="mini"><select data-sel="'+act+'">';for(var i=0;i<opts.length;i++)h+='<option value="'+esc(opts[i][0])+'"'+(opts[i][0]===val?' selected':'')+'>'+esc(opts[i][1])+'</option>';return h+'</select><span class="ic">'+ic('chevD',13)+'</span></div>';}

  // people directory built from group members returned by /api/setup/test
  var PEOPLE={};
  function POOLname(id){return PEOPLE[id]||id;}
  function allPeople(){return Object.keys(PEOPLE);}

  // ---- actions ----
  function conn(){return {baseUrl:ST.baseUrl.trim().replace(/\/+$/,''),token:ST.token.trim(),selfSigned:ST.selfSigned,timeout:parseInt(ST.timeout,10)||60};}
  function doTest(){
    ST.test='testing';render();
    fetch('/api/setup/test',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(conn())})
      .then(function(r){return r.json();}).then(function(j){
        if(!j.ok){ST.test='err';render();return;}
        ST.projects=j.projects||[];ST.groups=j.groups||[];
        if(!ST.importIds.length)ST.importIds=ST.projects.map(function(p){return p.id;});
        // build teams + memberships + people from groups
        PEOPLE={};ST.teams=[];ST.memberships=[];
        (ST.groups||[]).forEach(function(g,gi){var id='g'+gi;ST.teams.push({id:id,name:g.name,gitlab:true});
          (g.members||[]).forEach(function(mb){PEOPLE[mb.username]=mb.name||mb.username;ST.memberships.push({pid:mb.username,teamId:id,role:mb.role==='lead'?'lead':'member'});});});
        ST.test='ok';render();
      }).catch(function(){ST.test='err';render();});
  }
  function loadLabels(cb){
    ST.labelsLoaded=false;
    fetch('/api/setup/labels',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(Object.assign(conn(),{projectIds:ST.importIds}))})
      .then(function(r){return r.json();}).then(function(j){
        ST.labels=(j.ok&&j.labels)?j.labels:[];
        ST.labels.forEach(function(l){if(!(l in ST.labelPhase))ST.labelPhase[l]=guessPhase(l);});
        ST.labelsLoaded=true;cb&&cb();
      }).catch(function(){ST.labels=[];ST.labelsLoaded=true;cb&&cb();});
  }
  function save(){
    ST.saving=true;ST.saveErr='';render();
    var payload={baseUrl:conn().baseUrl,token:conn().token,selfSigned:ST.selfSigned,timeout:conn().timeout,
      projectIds:ST.importIds,labelPhases:ST.labelPhase,
      teams:ST.teams.map(function(t){return {name:t.name,members:ST.memberships.filter(function(m){return m.teamId===t.id;}).map(function(m){return {username:m.pid,role:m.role};})};})};
    fetch('/api/setup',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)})
      .then(function(r){return r.json();}).then(function(j){if(j.ok){window.location.href='/';}else{ST.saving=false;ST.saveErr=j.error||'Enregistrement impossible.';render();}})
      .catch(function(){ST.saving=false;ST.saveErr='Serveur injoignable.';render();});
  }
  function go(n){ if(n>ST.step && !canNext())return; if(n===2 && !ST.labelsLoaded){ST.step=2;render();loadLabels(render);return;} ST.step=n; render(); document.getElementById('body').scrollTop=0; }

  app.addEventListener('click',function(e){
    var b=e.target.closest('[data-act]');if(!b)return;var a=b.dataset.act;
    if(a==='eye'){ST.showTok=!ST.showTok;render();}
    else if(a==='self'){ST.selfSigned=!ST.selfSigned;render();}
    else if(a==='test'){doTest();}
    else if(a==='back'){go(Math.max(0,ST.step-1));}
    else if(a==='next'){go(ST.step+1);}
    else if(a==='launch'){save();}
    else if(a.indexOf('goto:')===0){var n=+a.slice(5);if(n<ST.step||n===ST.step)go(n);}
    else if(a.indexOf('proj:')===0){var id=+a.slice(5);var k=ST.importIds.indexOf(id);if(k>=0)ST.importIds.splice(k,1);else ST.importIds.push(id);ST.labelsLoaded=false;render();}
    else if(a.indexOf('rmteam:')===0){var tid=a.slice(7);ST.memberships=ST.memberships.filter(function(m){return m.teamId!==tid;});ST.teams=ST.teams.filter(function(t){return t.id!==tid;});render();}
    else if(a==='addteam'){ST.teams.push({id:'t'+Date.now(),name:'Nouvelle équipe',gitlab:false});render();}
    else if(a.indexOf('rmmem:')===0){var pr=a.slice(6).split('~');ST.memberships=ST.memberships.filter(function(m){return !(m.pid===pr[0]&&m.teamId===pr[1]);});render();}
  });
  app.addEventListener('input',function(e){
    var f=e.target.closest('[data-field]');if(f){ST[f.dataset.field]=f.value;if(f.dataset.field==='baseUrl'||f.dataset.field==='token')ST.test='idle';return;}
    var tn=e.target.closest('[data-team]');if(tn){ST.teams[+tn.dataset.team].name=tn.value;}
  });
  app.addEventListener('change',function(e){
    var s=e.target.closest('[data-sel]');if(s){var a=s.dataset.sel;
      if(a.indexOf('phase:')===0){var i=+a.slice(6);ST.labelPhase[ST.labels[i]]=s.value;render();}
      else if(a.indexOf('role:')===0){var pr=a.slice(5).split('~');ST.memberships.forEach(function(m){if(m.pid===pr[0]&&m.teamId===pr[1])m.role=s.value;});render();}
      return;}
    var ad=e.target.closest('[data-add]');if(ad&&ad.value){var tid=ad.dataset.add;if(!ST.memberships.some(function(m){return m.pid===ad.value&&m.teamId===tid;}))ST.memberships.push({pid:ad.value,teamId:tid,role:'member'});render();}
  });
  render();
})();
</script>
</body>
</html>
""";
}
