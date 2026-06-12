// SetupView.cs — assistant de première mise en service du dashboard KPI.
//
// Drop ce fichier dans Kpi/Views/ (namespace Kpi.Views).
// Page autonome (CSS + JS inline, CSP-safe) : aucun JS externe (seules Google Fonts,
// déjà autorisées par la CSP), et la page ne parle qu'à des endpoints same-origin.
//
// Flux : /setup (admin, tant que !IsConfigured) →
//   1. POST /api/setup/test     → valide la connexion, renvoie projets + groupes
//   2. POST /api/setup/labels   → labels des projets sélectionnés (mapping phases)
//   3. POST /api/setup          → écrit appsettings.json + recharge → redirige vers /
//
// Voir WebDashboard.setup.patch.md pour le câblage serveur.
using System.Linq;
using Kpi.Config;

namespace Kpi.Views;

public static class SetupView
{
    public static string Page(AuthConfig auth, string culture = "en")
    {
        var defaultInstance = !string.IsNullOrWhiteSpace(auth.Authority) ? auth.Authority.TrimEnd('/') : "https://gitlab.com";
        var lc = Kpi.Localization.Loc.Normalize(culture);
        var langOptions = string.Join("", Kpi.Localization.Loc.List().Select(l =>
            $"<option value=\"{l[0]}\"{(l[0] == lc ? " selected" : "")}>{HtmlAttr(l[1])}</option>"));
        return Html
            .Replace("__DEFAULT_INSTANCE__", HtmlAttr(defaultInstance))
            .Replace("__SLANG__", lc)
            .Replace("__DIR__", Kpi.Localization.Loc.IsRtl(lc) ? " dir=\"rtl\"" : "")
            .Replace("__LANG_OPTIONS__", langOptions);
    }

    private static string HtmlAttr(string s) =>
        (s ?? "").Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

    private const string Html = """
<!DOCTYPE html>
<html lang="__SLANG__"__DIR__>
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
.topright{display:flex;align-items:center;gap:18px;}
.lang-switch{display:flex;align-items:center;}
.lang-sel{background:var(--panel-2);color:var(--ink-dim);border:1px solid var(--line);border-radius:8px;font:inherit;font-size:12px;padding:4px 8px;cursor:pointer;outline:none;}
.lang-sel:focus{border-color:var(--accent);}
.lang-sel option{background:var(--panel-2);color:var(--ink);}
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
/* étape Phases : éditeur (couleur/nom/ajout/suppr) + sous-titres */
.prereq{font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.04em;color:var(--accent);background:var(--accent-soft);padding:2px 7px;border-radius:999px;margin-right:6px;}
.subh{font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:var(--ink-faint);margin:16px 0 9px;display:flex;align-items:center;gap:8px;}
.subc{font-family:var(--mono);font-weight:600;color:var(--ink-dim);font-size:11px;background:var(--panel-3);padding:1px 7px;border-radius:999px;}
.phases{display:flex;flex-direction:column;gap:7px;margin-bottom:12px;}
.phrow{display:flex;align-items:center;gap:10px;}
.swatchwrap{position:relative;flex:none;}
.swatch{width:26px;height:26px;border-radius:8px;border:1px solid rgba(255,255,255,.14);cursor:pointer;padding:0;}
.pop{position:absolute;top:32px;left:0;z-index:20;display:grid;grid-template-columns:repeat(5,1fr);gap:6px;padding:8px;background:var(--panel);border:1px solid var(--line);border-radius:10px;box-shadow:0 10px 30px rgba(0,0,0,.4);}
.pc{width:22px;height:22px;border-radius:6px;border:1px solid rgba(255,255,255,.14);cursor:pointer;padding:0;}
.pc.on{outline:2px solid var(--ink);outline-offset:1px;}
.phname{flex:1;min-width:0;background:var(--panel-2);border:1px solid var(--line);border-radius:9px;color:var(--ink);font:600 13.5px var(--sans);padding:8px 11px;outline:none;}
.phname:focus{border-color:var(--accent);}
.phx{flex:none;border:0;background:none;color:var(--ink-faint);font-size:20px;line-height:1;cursor:pointer;padding:0 6px;border-radius:6px;}
.phx:hover{color:var(--bad);}
/* écran de chargement post-setup (loader temps réel) */
.ld-wrap{flex:1;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:18px;padding:30px;}
.ld-bars{display:flex;align-items:flex-end;gap:9px;height:120px;}
.ld-bars i{display:block;width:16px;height:100%;background:var(--panel-2);border-radius:6px;display:flex;align-items:flex-end;overflow:hidden;}
.ld-bars b{display:block;width:100%;background:var(--accent);border-radius:6px;transition:height .5s var(--ease);}
.ld-pct{font-family:var(--disp);font-weight:700;font-size:48px;line-height:1;color:var(--ink);}
.ld-pct span{font-size:26px;color:var(--ink-faint);margin-left:2px;}
.ld-status{display:flex;align-items:center;gap:9px;font-size:14px;color:var(--ink-dim);font-weight:600;}
.ld-status.err{color:var(--bad);}
.ld-status .okic{color:var(--good);display:flex;}
.ld-meta{font-size:12px;color:var(--ink-faint);}
@media(max-width:720px){.checklist{grid-template-columns:1fr;}.suA-top,.foot{padding-left:20px;padding-right:20px;}.bodyinner{padding:14px 20px 24px;}}
</style>
</head>
<body>
<div id="app"></div>
<script>
(function(){
  // Périodes par défaut PROPOSÉES (éditables à l'étape 3 : renommer / couleur / ajouter / supprimer).
  var DEFAULT_PHASES=[{id:'dev',name:'Development',color:'#2188ff'},{id:'review',name:'Code review',color:'#8957e5'},{id:'qawait',name:'QA wait',color:'#b8800a'},{id:'qa',name:'QA',color:'#c79a06'},{id:'tofix',name:'To fix',color:'#ec4899'},{id:'po',name:'PO validation',color:'#0f9e8e'},{id:'uiux',name:'UI/UX',color:'#2dd4bf'}];
  var PALETTE=['#2188ff','#8957e5','#b8800a','#c79a06','#ec4899','#0f9e8e','#2dd4bf','#e0792e','#d6336c','#5f6b7a'];
  var NONE_COLOR='#5f6b7a';
  // Couleur d'une phase par sa clé, depuis la liste ÉDITABLE (ST.phases). 'none' = gris.
  var phaseColor=function(k){if(k==='none')return NONE_COLOR;for(var i=0;i<ST.phases.length;i++)if(ST.phases[i].id===k)return ST.phases[i].color;return NONE_COLOR;};
  var P={link:'<path d="M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1.5 1.5"/><path d="M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1.5-1.5"/>',server:'<rect x="3" y="4" width="18" height="7" rx="2"/><rect x="3" y="13" width="18" height="7" rx="2"/><path d="M7 7.5h.01M7 16.5h.01"/>',key:'<circle cx="7.5" cy="15.5" r="3.5"/><path d="M10 13 21 2M18 5l2.5 2.5M15.5 7.5L18 10"/>',eye:'<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/>',box:'<path d="M21 8 12 3 3 8v8l9 5 9-5Z"/><path d="m3 8 9 5 9-5M12 13v8"/>',layers:'<path d="m12 2 9 5-9 5-9-5z"/><path d="m21 12-9 5-9-5"/><path d="m21 17-9 5-9-5"/>',users:'<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/>',check:'<path d="M20 6 9 17l-5-5"/>',chevR:'<path d="m9 18 6-6-6-6"/>',chevL:'<path d="m15 18-6-6 6-6"/>',chevD:'<path d="m6 9 6 6 6-6"/>',arrow:'<path d="M5 12h14M13 6l6 6-6 6"/>',zap:'<path d="M13 2 3 14h9l-1 8 10-12h-9l1-8Z"/>',info:'<circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/>',plus:'<path d="M12 5v14M5 12h14"/>',rocket:'<path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z"/><path d="m12 15-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z"/><path d="M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0"/><path d="M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5"/>'};
  function ic(n,s){s=s||18;return '<svg width="'+s+'" height="'+s+'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">'+P[n]+'</svg>';}
  var MARK='<svg width="20" height="20" viewBox="0 0 24 24"><rect x="3" y="13" width="4.2" height="7" rx="1.4" fill="#fff" opacity="0.82"/><rect x="9.9" y="9" width="4.2" height="11" rx="1.4" fill="#fff" opacity="0.92"/><rect x="16.8" y="4.5" width="4.2" height="15.5" rx="1.4" fill="#fff"/><path d="M4 8.5 L11 6 L19 2.5" fill="none" stroke="#fff" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" opacity="0.9"/><circle cx="19" cy="2.5" r="1.7" fill="#fff"/></svg>';
  var AVC=['#0072B2','#8957e5','#0f9e8e','#d97706','#b3231b','#2b7fff','#c2410c','#6d28d9'];
  function esc(s){return (s==null?'':String(s)).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');}
  function avColor(id){var s=0;for(var i=0;i<id.length;i++)s+=id.charCodeAt(i);return AVC[s%AVC.length];}
  function av(id,name,sz){sz=sz||24;var ini=(name||id||'?').charAt(0).toUpperCase();return '<span class="av" style="width:'+sz+'px;height:'+sz+'px;font-size:'+(sz*0.46)+'px;background:'+avColor(id)+'">'+esc(ini)+'</span>';}
  function guessPhase(l){l=l.toLowerCase();if(l.indexOf('code')>=0&&l.indexOf('progress')>=0)return 'dev';if(l.indexOf('review')>=0)return 'review';if(l.indexOf('backlog')>=0)return 'qawait';if(l.indexOf('qa')>=0&&l.indexOf('progress')>=0)return 'qa';if(l.indexOf('to fix')>=0)return 'tofix';if(l.indexOf('validation')>=0||/\bpo\b/.test(l))return 'po';if(l.indexOf('ui/ux')>=0)return 'uiux';return 'none';}

  // ---- i18n (FR/EN) ----
  var SLANG='__SLANG__';
  var I18N={
    fr:{
      bs:"Mise en service", stepOf:"Étape {n} sur 5",
      back:"Retour", continue:"Continuer", launch:"Lancer le dashboard", saving:"Enregistrement…",
      stepConnexion:"Connexion", stepProjets:"Projets", stepPhases:"Phases", stepEquipes:"Équipes", stepVerif:"Vérif.",
      eb1:"Étape 1 · Connexion", eb2:"Étape 2 · Projets", eb3:"Étape 3 · Phases", eb4:"Étape 4 · Équipes", eb5:"Étape 5 · Vérification",
      ht1:"Connexion à GitLab", ht2:"Projets à importer", ht3:"Phases de production", ht4:"Équipes", ht5:"Tout est prêt",
      hs1:"Renseignez l'instance et un token de service pour permettre l'extraction des données.",
      hs2:"Choisissez les projets à suivre par défaut. La liste provient de la connexion testée.",
      hs3:"Associez les labels qui représentent une phase de production ; laissez « Non suivi » pour les autres.",
      hs4:"Vérifiez les équipes importées (groupes GitLab) et ajustez si besoin.",
      hs5:"Vérifiez la configuration. Vous pourrez tout modifier ensuite dans Options.",
      baseUrl:"Base URL", serviceToken:"Token de service",
      tokenHint:"Scope read_api. Stocké côté serveur pour l'extraction, jamais affiché aux utilisateurs.",
      timeout:"Timeout (s)", timeoutHint:"Délai max d'une requête à l'API.",
      selfSigned:"Certificats auto-signés", selfSignedSub:"Pour les instances internes",
      testConn:"Tester la connexion", testing:"Test en cours…",
      connected:"Connecté · ", accessibleProjects:" projets accessibles",
      testFailed:"Échec · vérifiez l'URL et le token", notTested:"Non testé",
      requiredField:"champ obligatoire · une connexion réussie est requise pour continuer.",
      projSelectedOf:" projet(s) sélectionné(s) sur ", accessible:" accessibles.",
      loadingLabels:"Chargement des labels des projets sélectionnés…",
      prereq:"Prérequis", prereqText:"Seuls les labels Prod:: sont pris en compte. Personnalisez vos phases (nom, couleur, ajout/suppression), puis reliez-y vos labels.",
      phases:"Phases", changeColor:"Changer la couleur", deletePhase:"Supprimer la phase", addPhase:"Ajouter une phase",
      labelMapping:"Association des labels", notTracked:"Non suivi",
      teamsIntro:"Équipes importées depuis les <b>groupes GitLab</b>. <b>Lead</b> = Maintainer · <b>Membre</b> = Developer. Un membre peut appartenir à <b>plusieurs équipes</b>.",
      glGroup:"groupe GitLab", newTeam:"nouvelle", noMembers:"Aucun membre — ajoutez-en ci-dessous",
      addMember:"Ajouter un membre…", newTeamName:"Nouvelle équipe", roleLead:"Lead", roleMember:"Membre",
      none:"Aucun", labelsLinked:" labels liés à une phase", teamsCount:" équipes · ", peopleCount:" personnes", edit:"Modifier",
      starting:"Démarrage…", extracting:"Extraction des données · vous pouvez laisser cette page ouverte",
      left:" restant", done:"Terminé — ouverture du dashboard…", failed:"Échec : ", cancel:"Annuler",
      backToConfig:"Revenir à la configuration",
      saveImpossible:"Enregistrement impossible.", serverUnreachable:"Serveur injoignable.",
      newPhase:"Nouvelle phase", extractingShort:"Extraction…"
    },
    en:{
      bs:"Setup", stepOf:"Step {n} of 5",
      back:"Back", continue:"Continue", launch:"Launch dashboard", saving:"Saving…",
      stepConnexion:"Connection", stepProjets:"Projects", stepPhases:"Phases", stepEquipes:"Teams", stepVerif:"Review",
      eb1:"Step 1 · Connection", eb2:"Step 2 · Projects", eb3:"Step 3 · Phases", eb4:"Step 4 · Teams", eb5:"Step 5 · Verification",
      ht1:"Connect to GitLab", ht2:"Projects to import", ht3:"Production phases", ht4:"Teams", ht5:"All set",
      hs1:"Enter the instance and a service token to allow data extraction.",
      hs2:"Choose the projects to track by default. The list comes from the tested connection.",
      hs3:"Map the labels that represent a production phase; leave \"Not tracked\" for the others.",
      hs4:"Review the imported teams (GitLab groups) and adjust if needed.",
      hs5:"Review the configuration. You can change everything later in Options.",
      baseUrl:"Base URL", serviceToken:"Service token",
      tokenHint:"Scope read_api. Stored server-side for extraction, never shown to users.",
      timeout:"Timeout (s)", timeoutHint:"Max delay for an API request.",
      selfSigned:"Self-signed certificates", selfSignedSub:"For internal instances",
      testConn:"Test connection", testing:"Testing…",
      connected:"Connected · ", accessibleProjects:" accessible projects",
      testFailed:"Failed · check URL and token", notTested:"Not tested",
      requiredField:"required field · a successful connection is required to continue.",
      projSelectedOf:" project(s) selected of ", accessible:" accessible.",
      loadingLabels:"Loading labels…",
      prereq:"Prerequisite", prereqText:"Only Prod:: labels are taken into account. Customize your phases (name, color, add/remove), then link your labels to them.",
      phases:"Phases", changeColor:"Change color", deletePhase:"Delete phase", addPhase:"Add a phase",
      labelMapping:"Label mapping", notTracked:"Not tracked",
      teamsIntro:"Teams imported from <b>GitLab groups</b>. <b>Lead</b> = Maintainer · <b>Member</b> = Developer. A member can belong to <b>several teams</b>.",
      glGroup:"GitLab group", newTeam:"new", noMembers:"No members — add some below",
      addMember:"Add a member…", newTeamName:"New team", roleLead:"Lead", roleMember:"Member",
      none:"None", labelsLinked:" labels linked to a phase", teamsCount:" teams · ", peopleCount:" people", edit:"Edit",
      starting:"Starting…", extracting:"Extracting data · you can leave this page open",
      left:" left", done:"Done — opening dashboard…", failed:"Failed: ", cancel:"Cancel",
      backToConfig:"Back to configuration",
      saveImpossible:"Could not save.", serverUnreachable:"Server unreachable.",
      newPhase:"New phase", extractingShort:"Extracting…"
    },
    es:{
      "bs": "Configuración",
      "stepOf": "Paso {n} de 5",
      "back": "Atrás",
      "continue": "Continuar",
      "launch": "Lanzar panel",
      "saving": "Guardando…",
      "stepConnexion": "Conexión",
      "stepProjets": "Proyectos",
      "stepPhases": "Fases",
      "stepEquipes": "Equipos",
      "stepVerif": "Revisión",
      "eb1": "Paso 1 · Conexión",
      "eb2": "Paso 2 · Proyectos",
      "eb3": "Paso 3 · Fases",
      "eb4": "Paso 4 · Equipos",
      "eb5": "Paso 5 · Verificación",
      "ht1": "Conectar a GitLab",
      "ht2": "Proyectos a importar",
      "ht3": "Fases de producción",
      "ht4": "Equipos",
      "ht5": "Todo listo",
      "hs1": "Ingresa la instancia y un token de servicio para permitir la extracción de datos.",
      "hs2": "Elige los proyectos a rastrear de forma predeterminada. La lista proviene de la conexión probada.",
      "hs3": "Asigna las etiquetas que representan una fase de producción; deja «No rastreada» para las otras.",
      "hs4": "Revisa los equipos importados (grupos de GitLab) y ajusta si es necesario.",
      "hs5": "Revisa la configuración. Puedes cambiar todo más tarde en Opciones.",
      "baseUrl": "URL base",
      "serviceToken": "Token de servicio",
      "tokenHint": "Alcance read_api. Almacenado en el servidor para la extracción, nunca se muestra a los usuarios.",
      "timeout": "Tiempo de espera (s)",
      "timeoutHint": "Retraso máximo para una solicitud de API.",
      "selfSigned": "Certificados autofirmados",
      "selfSignedSub": "Para instancias internas",
      "testConn": "Probar conexión",
      "testing": "Probando…",
      "connected": "Conectado · ",
      "accessibleProjects": " proyectos accesibles",
      "testFailed": "Falló · verifica la URL y el token",
      "notTested": "No probado",
      "requiredField": "campo obligatorio · se requiere una conexión exitosa para continuar.",
      "projSelectedOf": " proyecto(s) seleccionado(s) de ",
      "accessible": " accesibles.",
      "loadingLabels": "Cargando etiquetas…",
      "prereq": "Requisito previo",
      "prereqText": "Solo se tienen en cuenta las etiquetas Prod::. Personaliza tus fases (nombre, color, añadir/quitar), luego vincula tus etiquetas a ellas.",
      "phases": "Fases",
      "changeColor": "Cambiar color",
      "deletePhase": "Eliminar fase",
      "addPhase": "Añadir una fase",
      "labelMapping": "Mapeo de etiquetas",
      "notTracked": "No rastreada",
      "teamsIntro": "Equipos importados desde <b>grupos de GitLab</b>. <b>Lead</b> = Maintainer · <b>Miembro</b> = Developer. Un miembro puede pertenecer a <b>varios equipos</b>.",
      "glGroup": "grupo de GitLab",
      "newTeam": "nuevo",
      "noMembers": "Sin miembros — añade algunos a continuación",
      "addMember": "Añadir un miembro…",
      "newTeamName": "Nuevo equipo",
      "roleLead": "Lead",
      "roleMember": "Miembro",
      "none": "Ninguno",
      "labelsLinked": " etiquetas vinculadas a una fase",
      "teamsCount": " equipos · ",
      "peopleCount": " personas",
      "edit": "Editar",
      "starting": "Iniciando…",
      "extracting": "Extrayendo datos · puedes dejar esta página abierta",
      "left": " restante",
      "done": "Listo — abriendo panel…",
      "failed": "Falló: ",
      "cancel": "Cancelar",
      "backToConfig": "Volver a la configuración",
      "saveImpossible": "No se pudo guardar.",
      "serverUnreachable": "Servidor no disponible.",
      "newPhase": "Nueva fase",
      "extractingShort": "Extrayendo…",
    },
    de:{
      "bs": "Einrichtung",
      "stepOf": "Schritt {n} von 5",
      "back": "Zurück",
      "continue": "Fortfahren",
      "launch": "Dashboard starten",
      "saving": "Speichern…",
      "stepConnexion": "Verbindung",
      "stepProjets": "Projekte",
      "stepPhases": "Phasen",
      "stepEquipes": "Teams",
      "stepVerif": "Überprüfung",
      "eb1": "Schritt 1 · Verbindung",
      "eb2": "Schritt 2 · Projekte",
      "eb3": "Schritt 3 · Phasen",
      "eb4": "Schritt 4 · Teams",
      "eb5": "Schritt 5 · Überprüfung",
      "ht1": "Mit GitLab verbinden",
      "ht2": "Zu importierende Projekte",
      "ht3": "Produktionsphasen",
      "ht4": "Teams",
      "ht5": "Alles bereit",
      "hs1": "Geben Sie die Instanz und ein Service-Token ein, um die Datenextraktion zu ermöglichen.",
      "hs2": "Wählen Sie die Standard-Projekte aus, die Sie verfolgen möchten. Die Liste stammt aus der getesteten Verbindung.",
      "hs3": "Ordnen Sie die Label, die eine Produktionsphase darstellen, einer Phase zu; lassen Sie \"Nicht verfolgt\" für die anderen.",
      "hs4": "Überprüfen Sie die importierten Teams (GitLab-Gruppen) und passen Sie sie ggf. an.",
      "hs5": "Überprüfen Sie die Konfiguration. Sie können später alles unter \"Optionen\" ändern.",
      "baseUrl": "Basis-URL",
      "serviceToken": "Service-Token",
      "tokenHint": "Bereich read_api. Serverseitig für die Extraktion gespeichert, wird Benutzern niemals angezeigt.",
      "timeout": "Timeout (s)",
      "timeoutHint": "Maximale Verzögerung für eine API-Anfrage.",
      "selfSigned": "Selbstsignierte Zertifikate",
      "selfSignedSub": "Für interne Instanzen",
      "testConn": "Verbindung testen",
      "testing": "Wird getestet…",
      "connected": "Verbunden · ",
      "accessibleProjects": " verfügbare Projekte",
      "testFailed": "Fehlgeschlagen · überprüfen Sie URL und Token",
      "notTested": "Nicht getestet",
      "requiredField": "erforderliches Feld · eine erfolgreiche Verbindung ist erforderlich, um fortzufahren.",
      "projSelectedOf": " Projekt(e) ausgewählt von ",
      "accessible": " verfügbar.",
      "loadingLabels": "Label werden geladen…",
      "prereq": "Voraussetzung",
      "prereqText": "Nur Label Prod:: werden berücksichtigt. Passen Sie Ihre Phasen an (Name, Farbe, Hinzufügen/Entfernen), verknüpfen Sie dann Ihre Label damit.",
      "phases": "Phasen",
      "changeColor": "Farbe ändern",
      "deletePhase": "Phase löschen",
      "addPhase": "Phase hinzufügen",
      "labelMapping": "Label-Zuordnung",
      "notTracked": "Nicht verfolgt",
      "teamsIntro": "Teams importiert aus <b>GitLab-Gruppen</b>. <b>Lead</b> = Maintainer · <b>Mitglied</b> = Developer. Ein Mitglied kann zu <b>mehreren Teams</b> gehören.",
      "glGroup": "GitLab-Gruppe",
      "newTeam": "neu",
      "noMembers": "Keine Mitglieder — fügen Sie einige hinzu",
      "addMember": "Mitglied hinzufügen…",
      "newTeamName": "Neues Team",
      "roleLead": "Lead",
      "roleMember": "Mitglied",
      "none": "Keine",
      "labelsLinked": " Label mit einer Phase verknüpft",
      "teamsCount": " Teams · ",
      "peopleCount": " Personen",
      "edit": "Bearbeiten",
      "starting": "Wird gestartet…",
      "extracting": "Daten werden extrahiert · Sie können diese Seite offen lassen",
      "left": " verbleibend",
      "done": "Fertig — Dashboard wird geöffnet…",
      "failed": "Fehlgeschlagen: ",
      "cancel": "Abbrechen",
      "backToConfig": "Zurück zur Konfiguration",
      "saveImpossible": "Speichern nicht möglich.",
      "serverUnreachable": "Server nicht erreichbar.",
      "newPhase": "Neue Phase",
      "extractingShort": "Extraktion…",
    },
    it:{
      "bs": "Configurazione",
      "stepOf": "Passaggio {n} su 5",
      "back": "Indietro",
      "continue": "Continua",
      "launch": "Avvia dashboard",
      "saving": "Salvataggio…",
      "stepConnexion": "Connessione",
      "stepProjets": "Progetti",
      "stepPhases": "Fasi",
      "stepEquipes": "Team",
      "stepVerif": "Revisione",
      "eb1": "Passaggio 1 · Connessione",
      "eb2": "Passaggio 2 · Progetti",
      "eb3": "Passaggio 3 · Fasi",
      "eb4": "Passaggio 4 · Team",
      "eb5": "Passaggio 5 · Verifica",
      "ht1": "Connessione a GitLab",
      "ht2": "Progetti da importare",
      "ht3": "Fasi di produzione",
      "ht4": "Team",
      "ht5": "Tutto è pronto",
      "hs1": "Inserisci l'istanza e un token di servizio per consentire l'estrazione dei dati.",
      "hs2": "Scegli i progetti da tracciare per impostazione predefinita. L'elenco proviene dalla connessione testata.",
      "hs3": "Mappa le etichette che rappresentano una fase di produzione; lascia \"Non tracciato\" per le altre.",
      "hs4": "Rivedi i team importati (gruppi GitLab) e regola se necessario.",
      "hs5": "Rivedi la configurazione. Potrai modificare tutto in seguito in Opzioni.",
      "baseUrl": "URL base",
      "serviceToken": "Token di servizio",
      "tokenHint": "Scope read_api. Archiviato lato server per l'estrazione, mai mostrato agli utenti.",
      "timeout": "Timeout (s)",
      "timeoutHint": "Ritardo massimo per una richiesta API.",
      "selfSigned": "Certificati autofirmati",
      "selfSignedSub": "Per istanze interne",
      "testConn": "Prova connessione",
      "testing": "Test in corso…",
      "connected": "Connesso · ",
      "accessibleProjects": " progetti accessibili",
      "testFailed": "Fallito · verifica URL e token",
      "notTested": "Non testato",
      "requiredField": "campo obbligatorio · è richiesta una connessione riuscita per continuare.",
      "projSelectedOf": " progetto(i) selezionato(i) su ",
      "accessible": " accessibili.",
      "loadingLabels": "Caricamento etichette…",
      "prereq": "Prerequisito",
      "prereqText": "Solo le etichette Prod:: vengono prese in considerazione. Personalizza le tue fasi (nome, colore, aggiungi/rimuovi), quindi collega le tue etichette ad esse.",
      "phases": "Fasi",
      "changeColor": "Cambia colore",
      "deletePhase": "Elimina fase",
      "addPhase": "Aggiungi una fase",
      "labelMapping": "Mapping etichette",
      "notTracked": "Non tracciato",
      "teamsIntro": "Team importati da <b>gruppi GitLab</b>. <b>Lead</b> = Maintainer · <b>Membro</b> = Developer. Un membro può appartenere a <b>più team</b>.",
      "glGroup": "gruppo GitLab",
      "newTeam": "nuovo",
      "noMembers": "Nessun membro — aggiungine di seguito",
      "addMember": "Aggiungi un membro…",
      "newTeamName": "Nuovo team",
      "roleLead": "Lead",
      "roleMember": "Membro",
      "none": "Nessuno",
      "labelsLinked": " etichette collegate a una fase",
      "teamsCount": " team · ",
      "peopleCount": " persone",
      "edit": "Modifica",
      "starting": "Avvio…",
      "extracting": "Estrazione dati · puoi lasciare questa pagina aperta",
      "left": " rimasto",
      "done": "Terminato — apertura dashboard…",
      "failed": "Fallito: ",
      "cancel": "Annulla",
      "backToConfig": "Torna alla configurazione",
      "saveImpossible": "Impossibile salvare.",
      "serverUnreachable": "Server non raggiungibile.",
      "newPhase": "Nuova fase",
      "extractingShort": "Estrazione…",
    },
    pt:{
      "bs": "Implementação",
      "stepOf": "Passo {n} de 5",
      "back": "Atrás",
      "continue": "Continuar",
      "launch": "Iniciar dashboard",
      "saving": "A guardar…",
      "stepConnexion": "Ligação",
      "stepProjetos": "Projetos",
      "stepPhases": "Fases",
      "stepEquipes": "Equipes",
      "stepVerif": "Análise",
      "eb1": "Passo 1 · Ligação",
      "eb2": "Passo 2 · Projetos",
      "eb3": "Passo 3 · Fases",
      "eb4": "Passo 4 · Equipes",
      "eb5": "Passo 5 · Verificação",
      "ht1": "Ligar-se ao GitLab",
      "ht2": "Projetos para importar",
      "ht3": "Fases de produção",
      "ht4": "Equipes",
      "ht5": "Tudo pronto",
      "hs1": "Introduza a instância e um token de serviço para permitir a extração de dados.",
      "hs2": "Escolha os projetos a acompanhar por padrão. A lista provém da ligação testada.",
      "hs3": "Mapeie os rótulos que representam uma fase de produção; deixe \"Não rastreado\" para os restantes.",
      "hs4": "Analise as equipes importadas (grupos GitLab) e ajuste se necessário.",
      "hs5": "Analise a configuração. Pode alterar tudo depois em Opções.",
      "baseUrl": "URL Base",
      "serviceToken": "Token de serviço",
      "tokenHint": "Scope read_api. Armazenado do lado do servidor para extração, nunca mostrado aos utilizadores.",
      "timeout": "Timeout (s)",
      "timeoutHint": "Atraso máximo para um pedido de API.",
      "selfSigned": "Certificados auto-assinados",
      "selfSignedSub": "Para instâncias internas",
      "testConn": "Testar ligação",
      "testing": "A testar…",
      "connected": "Ligado · ",
      "accessibleProjects": " projetos acessíveis",
      "testFailed": "Falha · verifique URL e token",
      "notTested": "Não testado",
      "requiredField": "campo obrigatório · uma ligação bem-sucedida é obrigatória para continuar.",
      "projSelectedOf": " projeto(s) selecionado(s) de ",
      "accessible": " acessíveis.",
      "loadingLabels": "A carregar rótulos…",
      "prereq": "Pré-requisito",
      "prereqText": "Apenas rótulos Prod:: são considerados. Personalize as suas fases (nome, cor, adicionar/remover), depois associe os seus rótulos a elas.",
      "phases": "Fases",
      "changeColor": "Alterar cor",
      "deletePhase": "Eliminar fase",
      "addPhase": "Adicionar uma fase",
      "labelMapping": "Mapeamento de rótulos",
      "notTracked": "Não rastreado",
      "teamsIntro": "Equipes importadas a partir de <b>grupos GitLab</b>. <b>Lead</b> = Maintainer · <b>Membro</b> = Developer. Um membro pode pertencer a <b>várias equipes</b>.",
      "glGroup": "grupo GitLab",
      "newTeam": "novo",
      "noMembers": "Sem membros — adicione alguns abaixo",
      "addMember": "Adicionar um membro…",
      "newTeamName": "Nova equipe",
      "roleLead": "Lead",
      "roleMember": "Membro",
      "none": "Nenhum",
      "labelsLinked": " rótulos associados a uma fase",
      "teamsCount": " equipes · ",
      "peopleCount": " pessoas",
      "edit": "Editar",
      "starting": "A iniciar…",
      "extracting": "A extrair dados · pode deixar esta página aberta",
      "left": " restante",
      "done": "Concluído — a abrir dashboard…",
      "failed": "Falha: ",
      "cancel": "Cancelar",
      "backToConfig": "Voltar à configuração",
      "saveImpossible": "Não foi possível guardar.",
      "serverUnreachable": "Servidor inacessível.",
      "newPhase": "Nova fase",
      "extractingShort": "A extrair…",
    },
    ru:{
      "bs": "Конфигурация",
      "stepOf": "Шаг {n} из 5",
      "back": "Назад",
      "continue": "Продолжить",
      "launch": "Запустить панель управления",
      "saving": "Сохранение…",
      "stepConnexion": "Соединение",
      "stepProjets": "Проекты",
      "stepPhases": "Фазы",
      "stepEquipes": "Команды",
      "stepVerif": "Проверка",
      "eb1": "Шаг 1 · Соединение",
      "eb2": "Шаг 2 · Проекты",
      "eb3": "Шаг 3 · Фазы",
      "eb4": "Шаг 4 · Команды",
      "eb5": "Шаг 5 · Проверка",
      "ht1": "Подключиться к GitLab",
      "ht2": "Проекты для импорта",
      "ht3": "Фазы производства",
      "ht4": "Команды",
      "ht5": "Все готово",
      "hs1": "Введите инстанцию и токен обслуживания для разрешения извлечения данных.",
      "hs2": "Выберите проекты для отслеживания по умолчанию. Список поступает из проверенного соединения.",
      "hs3": "Сопоставьте метки, которые представляют фазу производства; оставьте «Не отслеживается» для остальных.",
      "hs4": "Проверьте импортированные команды (группы GitLab) и при необходимости отрегулируйте.",
      "hs5": "Проверьте конфигурацию. Вы можете все изменить позже в параметрах.",
      "baseUrl": "Base URL",
      "serviceToken": "Токен обслуживания",
      "tokenHint": "Scope read_api. Сохраняется на сервере для извлечения, никогда не показывается пользователям.",
      "timeout": "Таймаут (сек)",
      "timeoutHint": "Максимальная задержка для запроса API.",
      "selfSigned": "Самоподписанные сертификаты",
      "selfSignedSub": "Для внутренних инстанций",
      "testConn": "Проверить соединение",
      "testing": "Проверка…",
      "connected": "Подключено · ",
      "accessibleProjects": " доступные проекты",
      "testFailed": "Не удалось · проверьте URL и токен",
      "notTested": "Не проверено",
      "requiredField": "обязательное поле · для продолжения требуется успешное соединение.",
      "projSelectedOf": " проект(ов) выбрано из ",
      "accessible": " доступные.",
      "loadingLabels": "Загрузка меток…",
      "prereq": "Предварительное условие",
      "prereqText": "Учитываются только метки Prod::. Настройте ваши фазы (название, цвет, добавление/удаление), затем свяжите с ними ваши метки.",
      "phases": "Фазы",
      "changeColor": "Изменить цвет",
      "deletePhase": "Удалить фазу",
      "addPhase": "Добавить фазу",
      "labelMapping": "Сопоставление меток",
      "notTracked": "Не отслеживается",
      "teamsIntro": "Команды, импортированные из <b>групп GitLab</b>. <b>Lead</b> = Maintainer · <b>Member</b> = Developer. Участник может принадлежать <b>нескольким командам</b>.",
      "glGroup": "группа GitLab",
      "newTeam": "новая",
      "noMembers": "Нет участников — добавьте некоторых ниже",
      "addMember": "Добавить участника…",
      "newTeamName": "Новая команда",
      "roleLead": "Lead",
      "roleMember": "Участник",
      "none": "Нет",
      "labelsLinked": " меток, связанных с фазой",
      "teamsCount": " команд · ",
      "peopleCount": " человек",
      "edit": "Редактировать",
      "starting": "Запуск…",
      "extracting": "Извлечение данных · вы можете оставить эту страницу открытой",
      "left": " осталось",
      "done": "Готово — открытие панели управления…",
      "failed": "Не удалось: ",
      "cancel": "Отмена",
      "backToConfig": "Вернуться к конфигурации",
      "saveImpossible": "Не удается сохранить.",
      "serverUnreachable": "Сервер недоступен.",
      "newPhase": "Новая фаза",
      "extractingShort": "Извлечение…",
    },
    ar:{
      "bs": "الإعداد",
      "stepOf": "الخطوة {n} من 5",
      "back": "رجوع",
      "continue": "متابعة",
      "launch": "تشغيل لوحة التحكم",
      "saving": "جاري الحفظ…",
      "stepConnexion": "الاتصال",
      "stepProjets": "المشاريع",
      "stepPhases": "المراحل",
      "stepEquipes": "الفرق",
      "stepVerif": "المراجعة",
      "eb1": "الخطوة 1 · الاتصال",
      "eb2": "الخطوة 2 · المشاريع",
      "eb3": "الخطوة 3 · المراحل",
      "eb4": "الخطوة 4 · الفرق",
      "eb5": "الخطوة 5 · التحقق",
      "ht1": "الاتصال بـ GitLab",
      "ht2": "المشاريع المراد استيرادها",
      "ht3": "مراحل الإنتاج",
      "ht4": "الفرق",
      "ht5": "كل شيء جاهز",
      "hs1": "أدخل المثيل ورمز خدمة للسماح باستخراج البيانات.",
      "hs2": "اختر المشاريع المراد تتبعها بشكل افتراضي. تأتي القائمة من الاتصال المختبر.",
      "hs3": "عيّن التسميات التي تمثل مرحلة إنتاج؛ اترك \"غير مُتابعة\" للآخرين.",
      "hs4": "راجع الفرق المستوردة (مجموعات GitLab) وأجرِ التعديلات إذا لزم الأمر.",
      "hs5": "راجع الإعدادات. يمكنك تغيير كل شيء لاحقاً في الخيارات.",
      "baseUrl": "عنوان URL الأساسي",
      "serviceToken": "رمز الخدمة",
      "tokenHint": "نطاق read_api. مخزن على جانب الخادم للاستخراج، لا يُعرض أبداً للمستخدمين.",
      "timeout": "انتظار (ثانية)",
      "timeoutHint": "أقصى تأخير لطلب API.",
      "selfSigned": "شهادات موقعة ذاتياً",
      "selfSignedSub": "للمثيلات الداخلية",
      "testConn": "اختبار الاتصال",
      "testing": "جاري الاختبار…",
      "connected": "متصل · ",
      "accessibleProjects": "مشاريع متاحة",
      "testFailed": "فشل · تحقق من عنوان URL والرمز",
      "notTested": "لم يتم اختباره",
      "requiredField": "حقل مطلوب · يجب أن يكون الاتصال ناجحاً للمتابعة.",
      "projSelectedOf": " مشروع (مشاريع) مختار (مختارة) من",
      "accessible": " متاح (متاحة).",
      "loadingLabels": "جاري تحميل التسميات…",
      "prereq": "المتطلب الأساسي",
      "prereqText": "فقط تسميات Prod:: يتم أخذها بعين الاعتبار. قم بتخصيص مراحلك (الاسم واللون والإضافة/الحذف)، ثم ربط تسمياتك بها.",
      "phases": "المراحل",
      "changeColor": "تغيير اللون",
      "deletePhase": "حذف المرحلة",
      "addPhase": "إضافة مرحلة",
      "labelMapping": "تعيين التسميات",
      "notTracked": "غير مُتابعة",
      "teamsIntro": "فرق مستوردة من <b>مجموعات GitLab</b>. <b>Lead</b> = المسؤول · <b>العضو</b> = المطور. يمكن لعضو الانتماء إلى <b>عدة فرق</b>.",
      "glGroup": "مجموعة GitLab",
      "newTeam": "جديدة",
      "noMembers": "بلا أعضاء — أضف البعض أدناه",
      "addMember": "إضافة عضو…",
      "newTeamName": "فريق جديد",
      "roleLead": "Lead",
      "roleMember": "عضو",
      "none": "بلا",
      "labelsLinked": " تسميات مرتبطة بمرحلة",
      "teamsCount": " فرق · ",
      "peopleCount": " أشخاص",
      "edit": "تحرير",
      "starting": "جاري البدء…",
      "extracting": "جاري استخراج البيانات · يمكنك ترك هذه الصفحة مفتوحة",
      "left": " متبقي",
      "done": "تم — فتح لوحة التحكم…",
      "failed": "فشل: ",
      "cancel": "إلغاء",
      "backToConfig": "العودة إلى الإعدادات",
      "saveImpossible": "تعذر الحفظ.",
      "serverUnreachable": "الخادم غير متاح.",
      "newPhase": "مرحلة جديدة",
      "extractingShort": "جاري الاستخراج…",
    },
    zh:{
      "bs": "设置",
      "stepOf": "第 {n} 步（共5步）",
      "back": "返回",
      "continue": "继续",
      "launch": "启动仪表板",
      "saving": "保存中…",
      "stepConnexion": "连接",
      "stepProjets": "项目",
      "stepPhases": "阶段",
      "stepEquipes": "团队",
      "stepVerif": "审查",
      "eb1": "第1步 · 连接",
      "eb2": "第2步 · 项目",
      "eb3": "第3步 · 阶段",
      "eb4": "第4步 · 团队",
      "eb5": "第5步 · 验证",
      "ht1": "连接到GitLab",
      "ht2": "要导入的项目",
      "ht3": "生产阶段",
      "ht4": "团队",
      "ht5": "一切就绪",
      "hs1": "输入实例和服务令牌以允许数据提取。",
      "hs2": "选择默认跟踪的项目。该列表来自测试的连接。",
      "hs3": "映射代表生产阶段的标签；对于其他标签，保留\"未跟踪\"。",
      "hs4": "审查导入的团队（GitLab组）并根据需要调整。",
      "hs5": "审查配置。之后可以在选项中更改所有内容。",
      "baseUrl": "基础URL",
      "serviceToken": "服务令牌",
      "tokenHint": "范围read_api。存储在服务器端用于提取，永远不会向用户显示。",
      "timeout": "超时（秒）",
      "timeoutHint": "API请求的最大延迟。",
      "selfSigned": "自签名证书",
      "selfSignedSub": "用于内部实例",
      "testConn": "测试连接",
      "testing": "测试中…",
      "connected": "已连接 · ",
      "accessibleProjects": " 个可访问的项目",
      "testFailed": "失败 · 检查URL和令牌",
      "notTested": "未测试",
      "requiredField": "必填字段 · 需要成功的连接才能继续。",
      "projSelectedOf": " 个项目已选择（共 ",
      "accessible": " 个可访问的）。",
      "loadingLabels": "正在加载标签…",
      "prereq": "先决条件",
      "prereqText": "仅考虑Prod:: 标签。自定义您的阶段（名称、颜色、添加/移除），然后将您的标签链接到它们。",
      "phases": "阶段",
      "changeColor": "更改颜色",
      "deletePhase": "删除阶段",
      "addPhase": "添加阶段",
      "labelMapping": "标签映射",
      "notTracked": "未跟踪",
      "teamsIntro": "从 <b>GitLab组</b>导入的团队。<b>Lead</b> = 维护者 · <b>成员</b> = 开发者。一个成员可以属于 <b>多个团队</b>。",
      "glGroup": "GitLab组",
      "newTeam": "新建",
      "noMembers": "无成员 — 在下方添加",
      "addMember": "添加成员…",
      "newTeamName": "新团队",
      "roleLead": "Lead",
      "roleMember": "成员",
      "none": "无",
      "labelsLinked": " 个标签链接到阶段",
      "teamsCount": " 个团队 · ",
      "peopleCount": " 个人",
      "edit": "编辑",
      "starting": "启动中…",
      "extracting": "正在提取数据 · 您可以打开此页面",
      "left": " 剩余",
      "done": "完成 — 正在打开仪表板…",
      "failed": "失败：",
      "cancel": "取消",
      "backToConfig": "返回配置",
      "saveImpossible": "无法保存。",
      "serverUnreachable": "服务器无法访问。",
      "newPhase": "新阶段",
      "extractingShort": "提取中…",
    },
    ja:{
      "bs": "セットアップ",
      "stepOf": "ステップ {n} / 5",
      "back": "戻る",
      "continue": "続行",
      "launch": "ダッシュボードを起動",
      "saving": "保存中…",
      "stepConnexion": "接続",
      "stepProjets": "プロジェクト",
      "stepPhases": "フェーズ",
      "stepEquipes": "チーム",
      "stepVerif": "確認",
      "eb1": "ステップ 1 · 接続",
      "eb2": "ステップ 2 · プロジェクト",
      "eb3": "ステップ 3 · フェーズ",
      "eb4": "ステップ 4 · チーム",
      "eb5": "ステップ 5 · 確認",
      "ht1": "GitLabに接続",
      "ht2": "インポートするプロジェクト",
      "ht3": "本番フェーズ",
      "ht4": "チーム",
      "ht5": "準備完了",
      "hs1": "インスタンスとサービストークンを入力してデータ抽出を許可。",
      "hs2": "デフォルトで追跡するプロジェクトを選択。リストは確認済み接続から。",
      "hs3": "本番フェーズを表す ラベルをマッピング。その他は「未追跡」に設定。",
      "hs4": "インポート済みチーム（GitLabグループ）を確認し、必要に応じて調整。",
      "hs5": "設定を確認。後でオプションで すべて変更可能。",
      "baseUrl": "ベースURL",
      "serviceToken": "サービストークン",
      "tokenHint": "スコープ read_api。サーバーサイドで抽出に保存され、ユーザーには表示されません。",
      "timeout": "タイムアウト（秒）",
      "timeoutHint": "APIリクエストの最大遅延。",
      "selfSigned": "自己署名証明書",
      "selfSignedSub": "内部インスタンス向け",
      "testConn": "接続をテスト",
      "testing": "テスト中…",
      "connected": "接続済み · ",
      "accessibleProjects": "アクセス可能なプロジェクト",
      "testFailed": "失敗 · URLとトークンを確認",
      "notTested": "未テスト",
      "requiredField": "必須フィールド · 続行するには接続成功が必要。",
      "projSelectedOf": "プロジェクト選択 / ",
      "accessible": "アクセス可能。",
      "loadingLabels": "ラベルを読み込み中…",
      "prereq": "前提条件",
      "prereqText": "Prod::ラベルのみが対象。フェーズをカスタマイズ（名前、色、追加/削除）してからラベルをリンク。",
      "phases": "フェーズ",
      "changeColor": "色を変更",
      "deletePhase": "フェーズを削除",
      "addPhase": "フェーズを追加",
      "labelMapping": "ラベルマッピング",
      "notTracked": "未追跡",
      "teamsIntro": "<b>GitLabグループ</b>からインポート済みチーム。<b>リード</b> = メンテナー · <b>メンバー</b> = 開発者。メンバーは<b>複数チーム</b>に属せます。",
      "glGroup": "GitLabグループ",
      "newTeam": "新規",
      "noMembers": "メンバーなし — 以下から追加",
      "addMember": "メンバーを追加…",
      "newTeamName": "新しいチーム",
      "roleLead": "リード",
      "roleMember": "メンバー",
      "none": "なし",
      "labelsLinked": "フェーズにリンク済みラベル",
      "teamsCount": "チーム · ",
      "peopleCount": "人",
      "edit": "編集",
      "starting": "起動中…",
      "extracting": "データを抽出中 · このページを開いたままに",
      "left": "残り",
      "done": "完了 — ダッシュボードを開く中…",
      "failed": "失敗: ",
      "cancel": "キャンセル",
      "backToConfig": "設定に戻る",
      "saveImpossible": "保存できませんでした。",
      "serverUnreachable": "サーバーに接続できません。",
      "newPhase": "新しいフェーズ",
      "extractingShort": "抽出中…",
    },
  };
  var T=I18N[SLANG]||I18N.en;
  var STEP_META=[[T.stepConnexion,'link'],[T.stepProjets,'box'],[T.stepPhases,'layers'],[T.stepEquipes,'users'],[T.stepVerif,'rocket']];
  var LANG_SWITCH='<div class="lang-switch"><select class="lang-sel" data-setlang>__LANG_OPTIONS__</select></div>';

  var ST={step:0,baseUrl:'__DEFAULT_INSTANCE__',token:'',timeout:'60',selfSigned:false,showTok:false,
    test:'idle',projects:[],groups:[],importIds:[],labels:[],labelsLoaded:false,labelPhase:{},
    phases:DEFAULT_PHASES.map(function(p){return {id:p.id,name:p.name,color:p.color};}),openColor:null,
    teams:[],memberships:[],saving:false,saveErr:'',launching:false,progress:null};
  var app=document.getElementById('app');

  function canNext(){return ST.step!==0||ST.test==='ok';}

  function render(){
    if(ST.launching){app.innerHTML=launchHtml();return;}
    var h='<div class="suA"><div class="suA-top"><div class="brand"><div class="mark">'+MARK+'</div><div><div class="bn">KPI</div><div class="bs">'+T.bs+'</div></div></div><div class="topright"><div class="count">'+T.stepOf.replace('{n}',ST.step+1)+'</div>'+LANG_SWITCH+'</div></div>';
    h+='<div class="step"><div class="stepper">';
    for(var i=0;i<STEP_META.length;i++){var st=i<ST.step?'done':i===ST.step?'cur':'';h+='<div class="node '+st+'" data-act="goto:'+i+'"><div class="dot">'+(i<ST.step?ic('check',16):(i+1))+'</div><div class="nl">'+STEP_META[i][0]+'</div></div>';if(i<STEP_META.length-1)h+='<div class="line'+(i<ST.step?' done':'')+'"></div>';}
    h+='</div></div><div class="body" id="body"><div class="bodyinner"><div class="card" style="max-width:'+[560,600,640,780,600][ST.step]+'px">'+head()+stepBody()+'</div></div></div>';
    h+='<div class="foot"><button class="btn ghost'+(ST.step===0?' disabled':'')+'" data-act="back">'+ic('chevL',16)+T.back+'</button>';
    if(ST.step===4)h+='<button class="btn primary'+(ST.saving?' disabled':'')+'" data-act="launch">'+(ST.saving?'<span class="spin"></span>'+T.saving:ic('rocket',16)+T.launch)+'</button>';
    else h+='<button class="btn primary'+(canNext()?'':' disabled')+'" data-act="next">'+T.continue+' '+ic('chevR',16)+'</button>';
    h+='</div></div>';
    app.innerHTML=h;
  }
  function head(){
    var meta=[[T.eb1,T.ht1,'link'],[T.eb2,T.ht2,'box'],[T.eb3,T.ht3,'layers'],[T.eb4,T.ht4,'users'],[T.eb5,T.ht5,'rocket']][ST.step];
    var subs=[T.hs1,T.hs2,T.hs3,T.hs4,T.hs5];
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
    var chip=ST.test==='testing'?'<span class="chip neutral"><span class="spin"></span>'+T.testing+'</span>':ST.test==='ok'?'<span class="chip ok">'+ic('check',14)+T.connected+ST.projects.length+T.accessibleProjects+'</span>':ST.test==='err'?'<span class="chip err">'+T.testFailed+'</span>':'<span class="chip neutral">'+T.notTested+'</span>';
    return '<div class="field"><div class="flabel">'+T.baseUrl+' <span class="req">*</span></div><div class="box">'+sic('server')+'<input data-field="baseUrl" value="'+esc(ST.baseUrl)+'"></div></div>'
      +'<div class="field"><div class="flabel">'+T.serviceToken+' <span class="req">*</span></div><div class="box">'+sic('key')+'<input data-field="token" type="'+(ST.showTok?'text':'password')+'" placeholder="glpat-xxxxxxxxxxxxxxxxxxxx" value="'+esc(ST.token)+'"><button class="eye" data-act="eye">'+ic('eye',17)+'</button></div><div class="fhint">'+T.tokenHint+'</div></div>'
      +'<div style="display:grid;grid-template-columns:150px 1fr;gap:20px;align-items:start"><div class="field"><div class="flabel">'+T.timeout+'</div><div class="box"><input data-field="timeout" value="'+esc(ST.timeout)+'"></div><div class="fhint">'+T.timeoutHint+'</div></div>'
      +'<div class="togrow" style="margin-top:26px"><button class="tog'+(ST.selfSigned?' on':'')+'" data-act="self"><b></b></button><div><div class="tt">'+T.selfSigned+'</div><div class="ts">'+T.selfSignedSub+'</div></div></div></div>'
      +'<div style="display:flex;align-items:center;gap:12px;flex-wrap:wrap"><button class="btn outline sm" data-act="test"'+(ST.test==='testing'?' disabled':'')+'>'+ic('zap',16)+T.testConn+'</button>'+chip+'</div>'
      +'<div class="req"><span class="r">*</span> '+T.requiredField+'</div>';
  }
  function s1(){
    var h='<div class="note">'+sic('info')+'<div><b>'+ST.importIds.length+'</b>'+T.projSelectedOf+ST.projects.length+T.accessible+'</div></div><div class="checklist">';
    for(var i=0;i<ST.projects.length;i++){var p=ST.projects[i];var on=ST.importIds.indexOf(p.id)>=0;h+='<button class="chk'+(on?' on':'')+'" data-act="proj:'+p.id+'"><span class="chkbox">'+(on?ic('check',13):'')+'</span><span class="chkl">'+esc(p.name)+'<b>#'+p.id+'</b></span><span class="grp">'+esc(p.group||'')+'</span></button>';}
    return h+'</div>';
  }
  function s2(){
    if(!ST.labelsLoaded)return '<div class="note">'+sic('info')+'<div>'+T.loadingLabels+'</div></div>';
    var h='<div class="note">'+sic('info')+'<div><span class="prereq">'+T.prereq+'</span> '+T.prereqText+'</div></div>';
    // Éditeur de phases : couleur (palette), nom éditable, suppression.
    h+='<div class="subh">'+T.phases+' <span class="subc">'+ST.phases.length+'</span></div><div class="phases">';
    for(var i=0;i<ST.phases.length;i++){var p=ST.phases[i];
      h+='<div class="phrow"><div class="swatchwrap"><button class="swatch" style="background:'+esc(p.color)+'" data-act="phcol:'+esc(p.id)+'" title="'+T.changeColor+'"></button>';
      if(ST.openColor===p.id){h+='<div class="pop">';for(var c=0;c<PALETTE.length;c++)h+='<button class="pc'+(PALETTE[c]===p.color?' on':'')+'" style="background:'+PALETTE[c]+'" data-act="phpick:'+esc(p.id)+'~'+PALETTE[c]+'"></button>';h+='</div>';}
      h+='</div><input class="phname" data-phname="'+esc(p.id)+'" value="'+esc(p.name)+'"><button class="phx" data-act="phrm:'+esc(p.id)+'" title="'+T.deletePhase+'">×</button></div>';
    }
    h+='</div><button class="btn outline sm" style="align-self:flex-start" data-act="phadd">'+ic('plus',16)+T.addPhase+'</button>';
    // Association : labels Prod:: → phase (repli sur tous les labels si aucun Prod::).
    var prod=ST.labels.filter(function(l){return l.toLowerCase().indexOf('prod::')===0;});
    if(!prod.length)prod=ST.labels;
    var phOpts=[['none',T.notTracked]].concat(ST.phases.map(function(p){return [p.id,p.name];}));
    h+='<div class="subh" style="margin-top:6px">'+T.labelMapping+' <span class="subc">Prod::</span></div><div class="map">';
    for(var j=0;j<prod.length;j++){var ll=prod[j];var phv=ST.labelPhase[ll]||'none';
      h+='<div class="maprow"><span class="dot2" style="background:'+phaseColor(phv)+'"></span><span class="mlabel">'+esc(ll)+'</span><span class="arrow">'+ic('arrow',15)+'</span>'+miniSel('phase:'+ST.labels.indexOf(ll),phv,phOpts)+'</div>';}
    return h+'</div>';
  }
  function s3(){
    var roleOpts=[['lead',T.roleLead],['member',T.roleMember]];
    var h='<div class="note">'+sic('info')+'<div>'+T.teamsIntro+'</div></div><div class="teamcol">';
    for(var t=0;t<ST.teams.length;t++){var tm=ST.teams[t];var mem=ST.memberships.filter(function(m){return m.teamId===tm.id;});
      h+='<div class="team"><div class="teamh"><input class="teamname" data-team="'+t+'" value="'+esc(tm.name)+'">'+(tm.gitlab?'<span class="glbadge">'+T.glGroup+'</span>':'<span class="newbadge">'+T.newTeam+'</span>')+'<button class="teamx" data-act="rmteam:'+tm.id+'">×</button></div><div class="mlist">';
      if(mem.length===0)h+='<div class="empty">'+T.noMembers+'</div>';
      for(var j=0;j<mem.length;j++){var m=mem[j];var nm=POOLname(m.pid);h+='<div class="mrow">'+av(m.pid,nm,24)+'<span class="mname">'+esc(nm)+'</span>'+miniSel('role:'+m.pid+'~'+tm.id,m.role,roleOpts)+'<button class="mx" data-act="rmmem:'+m.pid+'~'+tm.id+'">×</button></div>';}
      var avail=allPeople().filter(function(pid){return !mem.some(function(m){return m.pid===pid;});});
      if(avail.length){h+='<div class="addsel">'+ic('plus',14)+'<select data-add="'+tm.id+'"><option value="">'+T.addMember+'</option>';for(var a=0;a<avail.length;a++)h+='<option value="'+esc(avail[a])+'">'+esc(POOLname(avail[a]))+'</option>';h+='</select></div>';}
      h+='</div></div>';
    }
    return h+'</div><button class="btn outline sm" style="align-self:flex-start;margin-top:12px" data-act="addteam">'+ic('plus',16)+T.newTeamName+'</button>';
  }
  function s4(){
    var imp=ST.projects.filter(function(p){return ST.importIds.indexOf(p.id)>=0;}).map(function(p){return p.name;});
    var mapped=0;for(var k in ST.labelPhase)if(ST.labelPhase[k]&&ST.labelPhase[k]!=='none')mapped++;
    var ppl={};ST.memberships.forEach(function(m){ppl[m.pid]=1;});
    var rows=[['link',T.stepConnexion,ST.baseUrl.replace(/^https?:\/\//,''),0],['box',T.stepProjets,imp.length?imp.join(', '):T.none,1],['layers',T.stepPhases,mapped+T.labelsLinked,2],['users',T.stepEquipes,ST.teams.length+T.teamsCount+Object.keys(ppl).length+T.peopleCount,3]];
    var h='<div class="recap">';
    for(var i=0;i<rows.length;i++)h+='<div class="rrow"><span class="ric">'+ic(rows[i][0],15)+'</span><div class="rk">'+rows[i][1]+'</div><div class="rv">'+esc(rows[i][2])+'</div><button class="redit" data-act="goto:'+rows[i][3]+'">'+T.edit+'</button></div>';
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
      periods:ST.phases.map(function(p){return {key:p.id,name:p.name,color:p.color,timed:p.id!=='uiux'};}),
      teams:ST.teams.map(function(t){return {name:t.name,members:ST.memberships.filter(function(m){return m.teamId===t.id;}).map(function(m){return {username:m.pid,role:m.role};})};})};
    fetch('/api/setup',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)})
      .then(function(r){return r.json();}).then(function(j){
        if(j.ok){ ST.saving=false; ST.launching=true; ST.progress={status:'running',percent:0,message:T.starting}; render(); setTimeout(pollProgress,500); }
        else{ST.saving=false;ST.saveErr=j.error||T.saveImpossible;render();}})
      .catch(function(){ST.saving=false;ST.saveErr=T.serverUnreachable;render();});
  }
  // Loader temps réel : poll /api/setup/progress jusqu'à done/error.
  function pollProgress(){
    if(!ST.launching)return;
    fetch('/api/setup/progress').then(function(r){return r.json();}).then(function(s){
      if(!ST.launching)return; ST.progress=s; render();
      if(s.status==='done'){ setTimeout(function(){window.location.href='/';},700); return; }
      if(s.status==='error'){ return; }
      setTimeout(pollProgress,700);
    }).catch(function(){ if(ST.launching) setTimeout(pollProgress,1200); });
  }
  function cancelLaunch(){ fetch('/api/setup/cancel',{method:'POST'}).catch(function(){}); ST.launching=false;ST.progress=null;render(); }
  function fmtClock(s){s=Math.max(0,Math.round(s));var m=Math.floor(s/60);return m+':'+('0'+(s%60)).slice(-2);}
  function launchHtml(){
    var pr=ST.progress||{status:'running',percent:0,message:T.starting};
    var done=pr.status==='done',err=pr.status==='error',pct=Math.max(0,Math.min(100,pr.percent||0));
    var heights=[40,62,52,86,66,96],bars='';
    for(var i=0;i<heights.length;i++)bars+='<i><b style="height:'+Math.round(pct*heights[i]/100)+'%"></b></i>';
    var eta=(pr.etaSeconds!=null&&!done&&!err)?' · ~ '+fmtClock(pr.etaSeconds)+T.left:'';
    var status=done?('<span class="okic">'+ic('check',17)+'</span> '+T.done)
      :err?(T.failed+esc(pr.error||pr.message||''))
      :('<span class="spin"></span> '+esc(pr.message||T.extractingShort));
    return '<div class="suA"><div class="suA-top"><div class="brand"><div class="mark">'+MARK+'</div><div><div class="bn">KPI</div><div class="bs">'+T.bs+'</div></div></div><div class="topright">'+LANG_SWITCH+'</div></div>'
      +'<div class="ld-wrap"><div class="ld-bars">'+bars+'</div>'
      +'<div class="ld-pct">'+pct+'<span>%</span></div>'
      +'<div class="ld-status'+(err?' err':'')+'">'+status+'</div>'
      +'<div class="ld-meta">'+(err?'':T.extracting+eta)+'</div>'
      +(done?'':'<button class="btn '+(err?'outline':'ghost')+' sm" data-act="cancelLaunch">'+(err?T.backToConfig:ic('chevL',15)+T.cancel)+'</button>')
      +'</div></div>';
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
    else if(a==='addteam'){ST.teams.push({id:'t'+Date.now(),name:T.newTeamName,gitlab:false});render();}
    else if(a.indexOf('rmmem:')===0){var pr=a.slice(6).split('~');ST.memberships=ST.memberships.filter(function(m){return !(m.pid===pr[0]&&m.teamId===pr[1]);});render();}
    else if(a==='cancelLaunch'){cancelLaunch();}
    else if(a.indexOf('phcol:')===0){var pid=a.slice(6);ST.openColor=(ST.openColor===pid?null:pid);render();}
    else if(a.indexOf('phpick:')===0){var pp=a.slice(7).split('~');ST.phases.forEach(function(p){if(p.id===pp[0])p.color=pp[1];});ST.openColor=null;render();}
    else if(a.indexOf('phrm:')===0){var rid=a.slice(5);ST.phases=ST.phases.filter(function(p){return p.id!==rid;});Object.keys(ST.labelPhase).forEach(function(k){if(ST.labelPhase[k]===rid)ST.labelPhase[k]='none';});render();}
    else if(a==='phadd'){ST.phases.push({id:'ph-'+Date.now(),name:T.newPhase,color:PALETTE[ST.phases.length%PALETTE.length]});render();}
  });
  app.addEventListener('input',function(e){
    var f=e.target.closest('[data-field]');if(f){ST[f.dataset.field]=f.value;if(f.dataset.field==='baseUrl'||f.dataset.field==='token')ST.test='idle';return;}
    var tn=e.target.closest('[data-team]');if(tn){ST.teams[+tn.dataset.team].name=tn.value;return;}
    var pn=e.target.closest('[data-phname]');if(pn){var pid=pn.dataset.phname;ST.phases.forEach(function(p){if(p.id===pid)p.name=pn.value;});}
  });
  app.addEventListener('change',function(e){
    var sl=e.target.closest('[data-setlang]');if(sl){location.href='/set-lang?lang='+encodeURIComponent(sl.value)+'&return=/setup';return;}
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
