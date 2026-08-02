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

public static partial class SetupView
{
    public static string Page(AuthConfig auth, string culture = "en", bool cannyConfigured = false)
    {
        // Champ instance TOUJOURS vide par défaut (placeholder gitlab.com) — AUCUN pré-remplissage ET AUCUNE trace
        // de l'instance configurée dans la page (ni champ, ni indicateur). L'« Instance : … » ne reflète que ce que
        // l'utilisateur saisit dans le champ base url (mis à jour en direct) ; vide → « — ».
        var lc = Kpi.Localization.Loc.Normalize(culture);
        var langOptions = string.Join("", Kpi.Localization.Loc.List().Select(l =>
            $"<option value=\"{l[0]}\"{(l[0] == lc ? " selected" : "")}>{HtmlAttr(l[1])}</option>"));
        return Html
            .Replace("__I18N__", I18nJs) // i18n extrait dans SetupView.I18n.cs (partial)
            .Replace("__OAUTH__", auth.OAuthConfigured ? "true" : "false")
            .Replace("__CANNY_OK__", cannyConfigured ? "true" : "false")
            .Replace("__DEFAULT_INSTANCE__", "")
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
  --accent:#2b7fff;--accent-soft:rgba(43,127,255,.16);--accent-2:#4d97ff;
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
.btn.ghost:hover{color:var(--ink);border-color:var(--ink-faint);}
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
/* dropdown custom (popover) — remplace les <select> natifs du mapping/rôles/milestones */
.dd{position:relative;flex:none;}
.ddbtn{display:inline-flex;align-items:center;gap:7px;height:34px;padding:0 8px 0 11px;background:var(--panel);border:1px solid var(--line);border-radius:9px;color:var(--ink);font-family:var(--sans);font-size:12.5px;cursor:pointer;transition:border-color .13s;}
.ddbtn:hover{border-color:var(--ink-faint);}
.ddbtn.open{border-color:var(--accent);}
.ddbtn .ic{color:var(--ink-faint);display:flex;transition:transform .15s;}
.ddbtn.open .ic{transform:rotate(180deg);}
.ddlabel{white-space:nowrap;}
.dddot{width:9px;height:9px;border-radius:50%;flex:none;}
.ddmenu{position:absolute;z-index:40;top:calc(100% + 5px);min-width:100%;max-height:260px;overflow-y:auto;background:var(--panel-2);border:1px solid var(--line);border-radius:11px;padding:5px;box-shadow:0 16px 40px rgba(0,0,0,.5);display:flex;flex-direction:column;gap:1px;}
.ddmenu.right{right:0;}
.ddopt{display:flex;align-items:center;gap:8px;width:100%;height:34px;padding:0 9px;border:0;border-radius:7px;background:none;color:var(--ink-dim);font-family:var(--sans);font-size:12.5px;text-align:left;cursor:pointer;white-space:nowrap;}
.ddopt:hover{background:var(--panel-3);color:var(--ink);}
.ddopt.on{color:var(--ink);}
.ddopt .ddlabel{flex:1;}
.ddcheck{color:var(--accent);display:flex;flex:none;}
/* accordéon OUVERT : overflow visible, sinon le popover .ddmenu serait coupé (overflow:hidden) */
.suA-acc.open,.suA-teamacc.open{overflow:visible;}
/* barre de recherche (étape Projets) */
.search{display:flex;align-items:center;gap:9px;height:40px;padding:0 12px;margin-bottom:11px;background:var(--panel);border:1px solid var(--line);border-radius:11px;transition:border-color .13s;}
.search:focus-within{border-color:var(--accent);}
.search .ic{color:var(--ink-faint);display:flex;flex:none;}
.search input{flex:1;min-width:0;border:0;background:none;outline:none;color:var(--ink);font-family:var(--sans);font-size:13.5px;}
.search input::placeholder{color:var(--ink-faint);}
.searchx{display:flex;align-items:center;justify-content:center;width:22px;height:22px;flex:none;border:0;border-radius:6px;background:var(--panel-3);color:var(--ink-dim);cursor:pointer;}
.searchx:hover{color:var(--ink);}
/* récap : milestone de départ de l'export, par projet */
.mstones{margin-top:14px;background:var(--panel);border:1px solid var(--line);border-radius:14px;padding:4px 0;}
.msecthead{display:flex;align-items:center;gap:6px;padding:11px 16px 7px;}
.msectlabel{font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:var(--ink-faint);}
.msrow{display:flex;align-items:center;gap:12px;padding:10px 16px;}
.msrow+.msrow{border-top:1px solid var(--line-2);}
.msdot{width:30px;height:30px;border-radius:9px;flex:none;display:flex;align-items:center;justify-content:center;background:var(--accent-soft);color:var(--accent);}
.msname{flex:1;font-size:13.5px;font-weight:600;color:var(--ink);}
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
.phases{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-bottom:12px;}
.phrow{display:flex;align-items:center;gap:9px;background:var(--panel);border:1px solid var(--line);border-radius:11px;padding:7px 9px;}
.phadd{grid-column:1/-1;display:inline-flex;align-items:center;justify-content:center;gap:7px;border:1px dashed var(--line);background:none;color:var(--ink-dim);font:600 13px var(--sans);padding:10px;border-radius:11px;cursor:pointer;transition:border-color .12s,color .12s;}
.phadd:hover{border-color:var(--accent);color:var(--accent);}
.swatchwrap{position:relative;flex:none;}
.swatch{width:26px;height:26px;border-radius:8px;border:1px solid rgba(255,255,255,.14);cursor:pointer;padding:0;}
.pop{position:absolute;top:32px;left:0;z-index:20;display:grid;grid-template-columns:repeat(5,1fr);gap:6px;padding:8px;background:var(--panel);border:1px solid var(--line);border-radius:10px;box-shadow:0 10px 30px rgba(0,0,0,.4);}
.pc{width:22px;height:22px;border-radius:6px;border:1px solid rgba(255,255,255,.14);cursor:pointer;padding:0;}
.pc.on{outline:2px solid var(--ink);outline-offset:1px;}
.phname{flex:1;min-width:0;background:none;border:1px solid transparent;border-radius:8px;color:var(--ink);font:600 13px var(--sans);padding:6px 8px;outline:none;}
.phname:hover{border-color:var(--line);}
.phname:focus{border-color:var(--accent);background:var(--panel-2);}
.phx{flex:none;border:0;background:none;color:var(--ink-faint);font-size:20px;line-height:1;cursor:pointer;padding:0 6px;border-radius:6px;}
.phx:hover{color:var(--bad);}
/* étape Phases : accordéons (Phases / Association des labels), un seul ouvert à la fois */
.suA-acc{border:1px solid var(--line);border-radius:14px;overflow:hidden;background:var(--panel-2);}
.suA-acc+.suA-acc{margin-top:10px;}
.suA-acchead{display:flex;align-items:center;gap:10px;width:100%;padding:13px 15px;background:none;border:0;cursor:pointer;color:var(--ink);font:inherit;text-align:left;}
.suA-acchead:hover{background:var(--panel-3);}
.suA-acchead .ic{color:var(--accent);display:flex;flex:none;}
.suA-acct{font-weight:700;font-size:14px;display:inline-flex;align-items:center;gap:8px;}
.suA-accchev{margin-left:auto;display:flex;color:var(--ink-faint);transition:transform .2s var(--ease);}
.suA-acc.open .suA-accchev{transform:rotate(90deg);}
.suA-accbody{padding:4px 15px 16px;display:flex;flex-direction:column;gap:12px;}
/* overflow:visible : sans lui, le popover .ddmenu d'une ligne du bas est CLIPPÉ par
   l'overflow:hidden hérité de .map → il « passe sous » la zone Rafraîchir les labels. */
.suA-accbody .map{border:0;border-radius:0;background:none;overflow:visible;}
.suA-accbody .maprow:first-child{padding-top:0;}
/* étape Projets : tout cocher / décocher + compteur */
.suA-selall{display:flex;align-items:center;justify-content:space-between;margin-bottom:11px;}
.suA-selallbtn{display:inline-flex;align-items:center;gap:7px;height:32px;padding:0 13px;border-radius:999px;border:1px solid var(--line);background:var(--panel-2);color:var(--ink-dim);font:inherit;font-size:12.5px;cursor:pointer;}
.suA-selallbtn:hover{border-color:var(--accent);color:var(--accent);}
.suA-selcount{font-size:12px;color:var(--ink-faint);}
/* étape 1 — bulle d'aide « i » (InfoTip), remplace les sous-textes permanents */
.su-info{position:relative;display:inline-flex;vertical-align:middle;margin-left:6px;cursor:help;}
.su-info-i{width:15px;height:15px;border-radius:50%;background:var(--panel-3);color:var(--ink-dim);font:italic 700 10px Georgia,serif;display:flex;align-items:center;justify-content:center;line-height:1;}
.su-info:hover .su-info-i,.su-info:focus .su-info-i{background:var(--accent);color:#fff;}
/* position:fixed → le popup échappe au clipping des ancêtres overflow:hidden (.suA-acc) / overflow:auto (.body).
   Coordonnées posées en JS (positionInfo) au survol/focus. pointer-events:none → pas de capture de souris. */
.su-info-pop{position:fixed;transform:translateX(-50%);width:230px;max-width:calc(100vw - 24px);z-index:1000;pointer-events:none;background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:9px 11px;font:400 11.5px/1.45 system-ui,sans-serif;color:var(--ink-dim);text-transform:none;letter-spacing:0;box-shadow:0 10px 30px rgba(0,0,0,.45);opacity:0;visibility:hidden;transition:opacity .12s;}
.su-info:hover .su-info-pop,.su-info:focus .su-info-pop{opacity:1;visibility:visible;}
.su-info-pop b{color:var(--ink);font-weight:600;}
/* étape 1 — aperçu/compteur dans l'en-tête d'accordéon */
.suA-accprev{margin-left:auto;display:inline-flex;align-items:center;gap:4px;}
.suA-accprev i{width:9px;height:9px;border-radius:50%;display:block;}
.suA-acccount{font-size:11.5px;font-weight:600;color:var(--ink-faint);background:var(--panel-3);padding:2px 9px;border-radius:999px;display:inline-flex;align-items:center;gap:4px;}
.suA-acccount.full{color:var(--good);background:var(--good-soft);}
.suA-acchead .suA-accchev{margin-left:4px;}
/* étape 1 — options avancées (Timeout + certificats) */
.suA-testrow{display:flex;align-items:center;gap:12px;flex-wrap:wrap;}
.suA-adv{border-top:1px solid var(--line-2);padding-top:4px;}
.suA-adv summary{cursor:pointer;font-size:12.5px;font-weight:600;color:var(--ink-dim);padding:8px 0;list-style:none;display:flex;align-items:center;gap:6px;}
.suA-adv summary::-webkit-details-marker{display:none;}
.suA-adv summary::before{content:'▸';color:var(--ink-faint);transition:transform .15s;display:inline-block;}
.suA-adv[open] summary::before{transform:rotate(90deg);}
.suA-advgrid{display:flex;align-items:center;justify-content:space-between;gap:20px;flex-wrap:wrap;width:100%;padding:6px 2px;}
.suA-advitem{display:flex;align-items:center;gap:10px;min-height:38px;}
.suA-advlabel{display:inline-flex;align-items:center;font-size:13px;font-weight:600;color:var(--ink);white-space:nowrap;}
.suA-advunit{color:var(--ink-faint);font-weight:400;margin-left:2px;}
.suA-advinput{width:64px;height:38px;text-align:center;background:var(--panel-2);border:1.5px solid var(--line);border-radius:10px;color:var(--ink);font:600 13.5px var(--mono),monospace;outline:none;}
.suA-advinput:focus{border-color:var(--accent);background:var(--panel);}
/* étape 1 — admin OAuth GitLab */
.suA-adminrow{display:flex;flex-direction:column;align-items:center;justify-content:center;gap:10px;padding:14px;}
.suA-glbtn{display:inline-flex;align-items:center;justify-content:center;gap:10px;align-self:center;height:44px;padding:0 20px;border-radius:999px;border:0;cursor:pointer;background:#fc6d26;color:#fff;font:600 14px var(--sans);transition:filter .12s;}
.suA-glbtn:hover{filter:brightness(1.06);}
.suA-glbtn.busy{background:var(--panel-3);color:var(--ink-dim);cursor:default;}
.suA-glmark{display:flex;background:#fff;border-radius:6px;padding:3px;}
.suA-admincard{display:flex;align-items:center;gap:12px;padding:12px 14px;border-radius:12px;background:var(--panel);border:1px solid var(--line);}
.suA-adminav{width:38px;height:38px;border-radius:50%;flex:none;display:flex;align-items:center;justify-content:center;color:#fff;font-weight:700;font-size:16px;}
.suA-adminmeta{flex:1;min-width:0;}
.suA-adminname{font-weight:700;font-size:14px;display:flex;align-items:center;gap:8px;}
.suA-adminrole{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.04em;color:var(--accent);background:var(--accent-soft);padding:2px 7px;border-radius:999px;}
.suA-adminhandle{font-family:var(--mono);font-size:12px;color:var(--ink-faint);margin-top:2px;}
.suA-adminok{display:inline-flex;align-items:center;gap:5px;font-size:11.5px;font-weight:600;color:var(--good);background:var(--good-soft);padding:4px 10px;border-radius:999px;flex:none;}
.suA-adminchange{border:0;background:none;color:var(--ink-faint);font-size:12px;cursor:pointer;padding:5px 8px;border-radius:8px;flex:none;}
.suA-adminchange:hover{color:var(--ink);background:var(--panel-3);}
.suA-oauthcur{margin-top:12px;font-size:12px;color:var(--ink-dim);display:flex;align-items:center;gap:7px;flex-wrap:wrap;}
.suA-oauthcur code{font-family:var(--mono);color:var(--ink);background:var(--panel-3);padding:2px 8px;border-radius:6px;}
.suA-reconf{margin-top:10px;}
/* étape 1 — formulaire de configuration OAuth (in-app, sans édition d'appsettings) */
.suA-oauthsetup{display:flex;flex-direction:column;gap:12px;}
.suA-oauthstep{display:flex;gap:14px;align-items:center;flex-wrap:wrap;font-size:13px;color:var(--ink-dim);line-height:1.45;}
.suA-oauthlink{color:var(--accent);text-decoration:none;font-weight:600;display:inline-flex;align-items:center;gap:4px;}
.suA-oauthlink:hover{text-decoration:underline;}
.suA-oauthhint{font-size:11.5px;color:var(--ink-faint);line-height:1.45;margin-top:-4px;}
.suA-oauthhint b{color:var(--ink-dim);font-weight:600;}
.suA-oauthhint code{font-family:var(--mono);background:var(--panel-3);padding:1px 6px;border-radius:5px;color:var(--ink-dim);}
/* étape 3 — portée des phases (tous / par projet) */
.suA-scope{display:flex;align-items:center;gap:12px;flex-wrap:wrap;margin-bottom:12px;}
.suA-seg{display:inline-flex;background:var(--panel-3);border-radius:10px;padding:3px;gap:2px;flex:none;}
.suA-seg button{border:0;background:none;color:var(--ink-dim);font:600 12.5px var(--sans);padding:6px 13px;border-radius:8px;cursor:pointer;}
.suA-seg button.on{background:var(--accent);color:#fff;}
.suA-scopehint{font-size:11.5px;color:var(--ink-faint);}
.suA-projtabs{display:flex;flex-wrap:wrap;gap:7px;margin-bottom:12px;}
.suA-projtab{border:1px solid var(--line);background:var(--panel-2);color:var(--ink-dim);font:600 12px var(--sans);padding:6px 12px;border-radius:999px;cursor:pointer;}
.suA-projtab:hover{border-color:var(--ink-faint);color:var(--ink);}
.suA-projtab.on{border-color:var(--accent);background:color-mix(in srgb,var(--accent) 14%,var(--panel-2));color:var(--ink);}
.suA-maprow.unset .mlabel{color:var(--ink-faint);}
.suA-maprow.unset .dot2{opacity:.4;}
/* étape 4 — équipes en accordéons */
.suA-teamacc{padding:0;overflow:hidden;}
.suA-teamacc .teamh{margin-bottom:0;padding:11px 14px;cursor:pointer;}
.suA-teamacc .teamh:hover{background:var(--panel-3);}
.suA-teamacc .mlist{padding:2px 14px 14px;}
.suA-teamchev{display:flex;color:var(--ink-faint);flex:none;transition:transform .2s var(--ease);}
.suA-teamacc.open .suA-teamchev{transform:rotate(90deg);}
.suA-teamcount{margin-left:auto;font-size:11.5px;color:var(--ink-faint);flex:none;}
.suA-teampreview{display:inline-flex;flex:none;}
.suA-teampreview>*+*{margin-left:-6px;}
.suA-teampreview>*{box-shadow:0 0 0 2px var(--panel-2);border-radius:50%;}
/* écran de chargement post-setup (loader temps réel — design « barres de marque ») */
.ld-dots{display:inline-flex;align-items:center;}
.ld-dots b{display:inline-block;width:5px;height:5px;border-radius:50%;background:var(--accent);margin-left:4px;animation:lddot 1.2s ease-in-out infinite;}
.ld-dots b:nth-child(2){animation-delay:.18s;}.ld-dots b:nth-child(3){animation-delay:.36s;}
@keyframes lddot{0%,60%,100%{opacity:.25;transform:translateY(0);}30%{opacity:1;transform:translateY(-3px);}}
.ld-launch{flex:1;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:28px;padding:40px;text-align:center;position:relative;}
.ld-logo{width:64px;height:64px;border-radius:18px;background:var(--accent);display:flex;align-items:center;justify-content:center;box-shadow:0 14px 40px rgba(43,127,255,.32);}
.ld-bars{display:flex;align-items:flex-end;gap:13px;height:150px;width:340px;max-width:100%;}
.ld-bars i{flex:1;height:100%;border-radius:8px 8px 0 0;background:var(--panel-2);position:relative;overflow:hidden;}
.ld-bars i b{position:absolute;left:0;right:0;bottom:0;display:block;border-radius:8px 8px 0 0;background:linear-gradient(180deg,var(--accent-2),var(--accent));transition:height .35s var(--ease);}
.ld-bars i b::after{content:'';position:absolute;inset:0;background:linear-gradient(180deg,rgba(255,255,255,.18),transparent 60%);}
.ld-pct{font-family:var(--disp);font-weight:700;letter-spacing:-.03em;font-size:54px;line-height:1;color:var(--ink);font-variant-numeric:tabular-nums;}
.ld-pct span{font-size:28px;color:var(--ink-faint);margin-left:2px;}
.ld-status{display:flex;align-items:center;gap:8px;justify-content:center;min-height:20px;font-size:14px;color:var(--ink-dim);}
.ld-status.err{color:var(--bad);}
.ld-status .okic{color:var(--good);display:flex;}
.ld-meta{display:flex;align-items:center;justify-content:center;gap:8px;font-family:var(--mono);font-size:12px;color:var(--ink-faint);font-variant-numeric:tabular-nums;}
.ld-meta .ld-sep{opacity:.5;}
.ld-cancel{position:absolute;bottom:26px;right:28px;display:inline-flex;align-items:center;gap:7px;height:38px;padding:0 15px;border-radius:999px;border:1px solid var(--line);background:var(--panel-2);color:var(--ink-dim);font-family:var(--sans);font-size:13px;cursor:pointer;transition:border-color .14s,color .14s;}
.ld-cancel:hover{border-color:var(--bad);color:var(--bad);}
@media(max-width:720px){.checklist{grid-template-columns:1fr;}.suA-top,.foot{padding-left:20px;padding-right:20px;}.bodyinner{padding:14px 20px 24px;}}
</style>
</head>
<body>
<div id="app"></div>
<script>
(function(){
  // Périodes par défaut PROPOSÉES (éditables à l'étape 3 : renommer / couleur / ajouter / supprimer).
  var DEFAULT_PHASES=[{id:'dev',name:'Development',color:'#2188ff'},{id:'review',name:'Code review',color:'#8957e5'},{id:'qawait',name:'QA wait',color:'#b8800a'},{id:'qa',name:'QA',color:'#c79a06'},{id:'tofix',name:'To fix',color:'#ec4899'},{id:'po',name:'PO validation',color:'#0f9e8e'},{id:'uiux',name:'UI/UX',color:'#2dd4bf'}];
  var PALETTE=['#2188ff','#0ea5e9','#06b6d4','#2dd4bf','#0f9e8e','#22c55e','#84cc16','#eab308','#c79a06','#e0792e','#f97316','#ef4444','#d6336c','#ec4899','#d946ef','#a855f7','#8957e5','#6366f1','#64748b','#94a3b8'];
  var NONE_COLOR='#5f6b7a';
  // Couleur d'une phase par sa clé, depuis la liste ÉDITABLE (ST.phases). 'none' = gris.
  var phaseColor=function(k){if(k==='none')return NONE_COLOR;var ps=curPhases();for(var i=0;i<ps.length;i++)if(ps[i].id===k)return ps[i].color;return NONE_COLOR;};
  var P={link:'<path d="M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1.5 1.5"/><path d="M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1.5-1.5"/>',server:'<rect x="3" y="4" width="18" height="7" rx="2"/><rect x="3" y="13" width="18" height="7" rx="2"/><path d="M7 7.5h.01M7 16.5h.01"/>',key:'<circle cx="7.5" cy="15.5" r="3.5"/><path d="M10 13 21 2M18 5l2.5 2.5M15.5 7.5L18 10"/>',eye:'<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/>',box:'<path d="M21 8 12 3 3 8v8l9 5 9-5Z"/><path d="m3 8 9 5 9-5M12 13v8"/>',layers:'<path d="m12 2 9 5-9 5-9-5z"/><path d="m21 12-9 5-9-5"/><path d="m21 17-9 5-9-5"/>',users:'<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/>',check:'<path d="M20 6 9 17l-5-5"/>',chevR:'<path d="m9 18 6-6-6-6"/>',chevL:'<path d="m15 18-6-6 6-6"/>',chevD:'<path d="m6 9 6 6 6-6"/>',arrow:'<path d="M5 12h14M13 6l6 6-6 6"/>',zap:'<path d="M13 2 3 14h9l-1 8 10-12h-9l1-8Z"/>',info:'<circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/>',plus:'<path d="M12 5v14M5 12h14"/>',rocket:'<path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z"/><path d="m12 15-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z"/><path d="M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0"/><path d="M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5"/>',x:'<path d="M18 6 6 18M6 6l12 12"/>',search:'<circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/>',flag:'<path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V4s-1 1-4 1-5-2-8-2-4 1-4 1z"/><path d="M4 22v-7"/>'};
  function ic(n,s){s=s||18;return '<svg width="'+s+'" height="'+s+'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">'+P[n]+'</svg>';}
  var MARK='<svg width="20" height="20" viewBox="0 0 24 24"><rect x="3" y="13" width="4.2" height="7" rx="1.4" fill="#fff" opacity="0.82"/><rect x="9.9" y="9" width="4.2" height="11" rx="1.4" fill="#fff" opacity="0.92"/><rect x="16.8" y="4.5" width="4.2" height="15.5" rx="1.4" fill="#fff"/><path d="M4 8.5 L11 6 L19 2.5" fill="none" stroke="#fff" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" opacity="0.9"/><circle cx="19" cy="2.5" r="1.7" fill="#fff"/></svg>';
  var GITLAB_MARK='<svg width="20" height="20" viewBox="0 0 24 24" aria-hidden="true"><path fill="#E24329" d="m12 21.5 3.3-10.2H8.7z"/><path fill="#FC6D26" d="M12 21.5 8.7 11.3H4.1z"/><path fill="#FCA326" d="M4.1 11.3 3.1 14.4a.7.7 0 0 0 .25.78L12 21.5z"/><path fill="#E24329" d="M4.1 11.3h4.6L6.7 5.2a.35.35 0 0 0-.66 0z"/><path fill="#FC6D26" d="M12 21.5l3.3-10.2h4.6z"/><path fill="#FCA326" d="M19.9 11.3l1 3.1a.7.7 0 0 1-.25.78L12 21.5z"/><path fill="#E24329" d="M19.9 11.3h-4.6l2-6.1a.35.35 0 0 1 .66 0z"/></svg>';
  var AVC=['#0072B2','#8957e5','#0f9e8e','#d97706','#b3231b','#2b7fff','#c2410c','#6d28d9'];
  function esc(s){return (s==null?'':String(s)).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');}
  function avColor(id){var s=0;for(var i=0;i<id.length;i++)s+=id.charCodeAt(i);return AVC[s%AVC.length];}
  function av(id,name,sz){sz=sz||24;var ini=(name||id||'?').charAt(0).toUpperCase();return '<span class="av" style="width:'+sz+'px;height:'+sz+'px;font-size:'+(sz*0.46)+'px;background:'+avColor(id)+'">'+esc(ini)+'</span>';}
  function guessPhase(l){l=l.toLowerCase();if(l.indexOf('code')>=0&&l.indexOf('progress')>=0)return 'dev';if(l.indexOf('review')>=0)return 'review';if(l.indexOf('backlog')>=0)return 'qawait';if(l.indexOf('qa')>=0&&l.indexOf('progress')>=0)return 'qa';if(l.indexOf('to fix')>=0)return 'tofix';if(l.indexOf('validation')>=0||/\bpo\b/.test(l))return 'po';if(l.indexOf('ui/ux')>=0)return 'uiux';return 'none';}

  // ---- i18n (FR/EN) ----
  var SLANG='__SLANG__';
  var OAUTHOK=__OAUTH__; // OAuth GitLab configuré côté serveur (Auth.ClientId/ClientSecret)
  var CANNYOK=__CANNY_OK__; // connexion Canny déjà configurée côté serveur (ExternalConnections.Canny)
__I18N__
  // Fallback i18n PAR CLÉ vers l'anglais : une clé absente dans la langue courante retombe sur EN
  // (au lieu d'« undefined »). Permet de n'ajouter les nouvelles clés qu'en en+fr.
  var T=Object.assign({},I18N.en,I18N[SLANG]||{});
  var STEP_META=[[T.stepConnexion,'link'],[T.stepProjets,'box'],[T.stepPhases,'layers'],[T.stepEquipes,'users'],[T.stepConnexions,'server'],[T.stepVerif,'rocket']];
  var LANG_SWITCH='<div class="lang-switch"><select class="lang-sel" data-setlang>__LANG_OPTIONS__</select></div>';

  var ST={step:0,baseUrl:'__DEFAULT_INSTANCE__',token:'',timeout:'60',selfSigned:false,showTok:false,
    test:'idle',projects:[],groups:[],importIds:[],projSearch:'',labels:[],labelsDiag:[],labelsLoaded:false,labelPhase:{},
    phases:DEFAULT_PHASES.map(function(p){return {id:p.id,name:p.name,color:p.color};}),openColor:null,acc:'phases',
    phaseScope:'all',phaseProj:null,phasesByProject:{},labelPhaseByProject:{},
    acc1:[],teamOpen:[],adminState:'idle',adminUser:null,oauthClientId:'',oauthSecret:'',oauthEdit:false,adminErr:'',
    openDd:'',exportMilestone:{},
    cannyApiKey:'',cannyConnected:CANNYOK,cannyState:'idle',cannyErr:'',
    teams:[],memberships:[],saving:false,saveErr:'',launching:false,progress:null};
  var app=document.getElementById('app');
  // Répertoire username→nom (reconstruit depuis les groupes renvoyés par /api/setup/test).
  var PEOPLE={};

  // Persistance localStorage : l'identification admin se fait par OAuth (aller-retour PLEINE PAGE) → on
  // restaure l'état du wizard au retour. Effacé après une mise en service réussie. (clé unique, même origine)
  // NB : 'acc1'/'acc'/'teamOpen' (états d'accordéon, UI) NON persistés → l'état par défaut s'applique
  // toujours (étape 1 : accordéons fermés, aperçu d'état « Non testé / Non identifié » dans l'en-tête).
  // NB : 'baseUrl' VOLONTAIREMENT non persisté → le champ instance est TOUJOURS vide par défaut (placeholder
  // gitlab.com), même si une instance est déjà configurée. Le SSO passant en popup, le wizard ne se recharge
  // plus → pas besoin de le restaurer. L'instance configurée n'est exposée NULLE PART dans la page par défaut.
  // ⚠️ SÉCURITÉ : 'token' (group token GitLab) N'EST JAMAIS persisté — un secret en clair dans
  // localStorage survivrait à la session et serait lisible par tout code de la même origine.
  // Au rechargement du wizard, le token doit être resaisi (le SSO passe en popup : cas rare).
  var PKEYS=['step','timeout','selfSigned','test','projects','groups','importIds','labels',
    'labelsDiag','labelsLoaded','labelPhase','phases','phaseScope','phaseProj','phasesByProject',
    'labelPhaseByProject','teams','memberships','exportMilestone'];
  function persistST(){try{var o={};PKEYS.forEach(function(k){o[k]=ST[k];});localStorage.setItem('kpi-setup',JSON.stringify(o));}catch(e){}}
  function clearST(){try{localStorage.removeItem('kpi-setup');}catch(e){}}
  (function(){try{var v=localStorage.getItem('kpi-setup');if(!v)return;var o=JSON.parse(v);PKEYS.forEach(function(k){if(o[k]!==undefined)ST[k]=o[k];});
    (ST.groups||[]).forEach(function(g){(g.members||[]).forEach(function(mb){PEOPLE[mb.username]=mb.name||mb.username;});});}catch(e){}})();

  // ---- portée des phases : globale ('all') ou par projet ('per', projet actif) ----
  function activeProj(){return (ST.phaseProj!=null&&ST.importIds.indexOf(ST.phaseProj)>=0)?ST.phaseProj:(ST.importIds.length?ST.importIds[0]:null);}
  function ensurePer(){if(ST.phaseScope!=='per')return;var ap=activeProj();if(ap==null)return;
    if(!ST.phasesByProject[ap])ST.phasesByProject[ap]=ST.phases.map(function(p){return {id:p.id,name:p.name,color:p.color};});
    if(!ST.labelPhaseByProject[ap])ST.labelPhaseByProject[ap]=Object.assign({},ST.labelPhase);}
  function curPhases(){if(ST.phaseScope==='all')return ST.phases;var ap=activeProj();return (ap!=null&&ST.phasesByProject[ap])||ST.phases;}
  function curMap(){if(ST.phaseScope==='all')return ST.labelPhase;var ap=activeProj();return (ap!=null&&ST.labelPhaseByProject[ap])||ST.labelPhase;}
  function setMapVal(label,val){ensurePer();if(ST.phaseScope==='all'){ST.labelPhase[label]=val;return;}ST.labelPhaseByProject[activeProj()][label]=val;}
  function switchScope(sc){if(sc==='per'&&ST.phaseScope!=='per'){ST.phaseScope='per';if(ST.phaseProj==null||ST.importIds.indexOf(ST.phaseProj)<0)ST.phaseProj=(ST.importIds.length?ST.importIds[0]:null);ST.importIds.forEach(function(id){if(!ST.phasesByProject[id])ST.phasesByProject[id]=ST.phases.map(function(p){return {id:p.id,name:p.name,color:p.color};});if(!ST.labelPhaseByProject[id])ST.labelPhaseByProject[id]=Object.assign({},ST.labelPhase);});}else ST.phaseScope=sc;render();}

  function canNext(){return ST.step!==0||(ST.test==='ok'&&ST.adminState==='connected');}

  // Identité admin : OAuth (le compte connecté DEVIENT admin). On lit /api/me au chargement et au retour OAuth.
  function loadMe(){fetch('/api/me').then(function(r){return r.json();}).then(function(me){
    if(me&&me.authenticated){ST.adminState='connected';ST.adminUser={name:me.displayName||me.login||'',handle:'@'+(me.login||''),role:(me.role==='admin'?'Admin':(me.role||'GitLab'))};}
    else if(ST.adminState==='connecting'){ST.adminState='idle';} // SSO échoué/annulé → réafficher le bouton
    render();
  }).catch(function(){if(ST.adminState==='connecting'){ST.adminState='idle';render();}});}

  function render(){
    if(ST.launching){app.innerHTML=launchHtml();return;}
    persistST();
    var _scEl=document.getElementById('body');var _scPos=_scEl?_scEl.scrollTop:0; // préserve le scroll : un clic ne doit pas remonter la vue
    var h='<div class="suA"><div class="suA-top"><div class="brand"><div class="mark">'+MARK+'</div><div><div class="bn">KPI</div><div class="bs">'+T.bs+'</div></div></div><div class="topright"><div class="count">'+T.stepOf.replace('{n}',ST.step+1).replace('{t}',STEP_META.length)+'</div>'+LANG_SWITCH+'</div></div>';
    h+='<div class="step"><div class="stepper">';
    for(var i=0;i<STEP_META.length;i++){var st=i<ST.step?'done':i===ST.step?'cur':'';h+='<div class="node '+st+'" data-act="goto:'+i+'"><div class="dot">'+(i<ST.step?ic('check',16):(i+1))+'</div><div class="nl">'+STEP_META[i][0]+'</div></div>';if(i<STEP_META.length-1)h+='<div class="line'+(i<ST.step?' done':'')+'"></div>';}
    h+='</div></div><div class="body" id="body"><div class="bodyinner"><div class="card" style="max-width:'+[560,600,640,780,560,600][ST.step]+'px">'+head()+stepBody()+'</div></div></div>';
    h+='<div class="foot"><button class="btn ghost" data-act="back">'+ic('chevL',16)+T.back+'</button>';
    if(ST.step===5)h+='<button class="btn primary'+(ST.saving?' disabled':'')+'" data-act="launch">'+(ST.saving?'<span class="spin"></span>'+T.saving:ic('rocket',16)+T.launch)+'</button>';
    else h+='<button class="btn primary'+(canNext()?'':' disabled')+'" data-act="next">'+T.continue+' '+ic('chevR',16)+'</button>';
    h+='</div></div>';
    app.innerHTML=h;
    var _scNew=document.getElementById('body');if(_scNew)_scNew.scrollTop=_scPos; // restaure la position (go() remet à 0 lors d'un changement d'étape)
  }
  function head(){
    var meta=[[T.eb1,T.ht1,'link'],[T.eb2,T.ht2,'box'],[T.eb3,T.ht3,'layers'],[T.eb4,T.ht4,'users'],[T.ebC,T.htC,'server'],[T.eb5,T.ht5,'rocket']][ST.step];
    var subs=[T.hs1,T.hs2,T.hs3,T.hs4,T.hsC,T.hs5];
    // Numéro d'eyebrow dynamique : le « N » codé dans ebN est corrigé selon la position réelle de l'étape
    // (une étape insérée ne casse pas la numérotation des suivantes).
    var eyebrow=meta[0].replace(/\d+/,String(ST.step+1));
    return '<div class="cardhead"><div class="hero">'+ic(meta[2],28)+'</div><div><div class="eyebrow">'+eyebrow+'</div><h2 class="h">'+meta[1]+'</h2></div></div><p class="sub">'+subs[ST.step]+'</p>';
  }

  function stepBody(){
    if(ST.step===0)return s0();
    if(ST.step===1)return s1();
    if(ST.step===2)return s2();
    if(ST.step===3)return s3();
    if(ST.step===4)return sCanny();
    return s4();
  }
  // Étape « Connexions externes » (Canny). Facultative : la clé API est validée + chiffrée côté serveur
  // (POST /api/setup/canny), jamais persistée dans la page. On peut la sauter et la configurer plus tard (Options).
  function sCanny(){
    var connected=ST.cannyConnected;
    var chip=ST.cannyState==='saving'?'<span class="chip neutral"><span class="spin"></span>'+T.saving+'</span>'
      :connected?'<span class="chip ok">'+ic('check',14)+T.cannyDone+'</span>'
      :ST.cannyState==='err'?'<span class="chip err">'+esc(ST.cannyErr||T.cannyFail)+'</span>':'';
    var h='<div class="suA-oauthsetup">';
    h+='<div class="suA-oauthhint">'+T.cannyIntro+'</div>';
    h+='<div class="field"><div class="flabel">'+T.cannyKey+' '+info(T.cannyKeyHint)+'</div><div class="box">'+sic('key')+'<input data-field="cannyApiKey" type="password"'+(connected?' placeholder="••••••••"':'')+' value="'+esc(ST.cannyApiKey||'')+'" autocomplete="off"></div></div>';
    if(ST.cannyState==='err'&&ST.cannyErr)h+='<div class="note" style="background:var(--bad-soft);border-left-color:var(--bad)"><span class="ic" style="color:var(--bad)">'+ic('info',16)+'</span><div>'+esc(ST.cannyErr)+'</div></div>';
    h+='<button class="btn primary'+(ST.cannyState==='saving'?' disabled':'')+'" data-act="cannysave">'+(ST.cannyState==='saving'?'<span class="spin"></span>'+T.saving:T.cannyConnect)+'</button> '+chip;
    h+='<p class="sub" style="margin-top:14px">'+T.cannyOptional+'</p></div>';
    return h;
  }
  function s0(){
    var gOpen=ST.acc1.indexOf('group')>=0,aOpen=ST.acc1.indexOf('admin')>=0;
    var chip=ST.test==='testing'?'<span class="chip neutral"><span class="spin"></span>'+T.testing+'</span>':ST.test==='ok'?'<span class="chip ok">'+ic('check',14)+T.connected+ST.projects.length+T.accessibleProjects+'</span>':ST.test==='err'?'<span class="chip err">'+T.testFailed+'</span>':'<span class="chip neutral">'+T.notTested+'</span>';
    var gPrev=ST.test==='ok'?'<span class="suA-acccount full">'+ic('check',12)+' '+T.connectedShort+'</span>':'<span class="suA-acccount">'+(ST.test==='err'?T.testFailed:T.notTested)+'</span>';
    var aConn=ST.adminState==='connected',au=ST.adminUser||{name:'',handle:'',role:''};
    var aPrev=aConn?'<span class="suA-acccount full">'+ic('check',12)+' '+esc(au.name)+'</span>':'<span class="suA-acccount">'+T.adminNone+'</span>';
    var h='<div class="suA-acc'+(gOpen?' open':'')+'"><button class="suA-acchead" data-act="acc1:group"><span class="ic">'+ic('server',16)+'</span><span class="suA-acct">'+T.groupConn+'</span><span class="suA-accprev">'+gPrev+'</span><span class="suA-accchev">'+ic('chevR',16)+'</span></button>';
    if(gOpen){
      h+='<div class="suA-accbody">'
        +'<div class="field"><div class="flabel">'+T.baseUrl+' <span class="req">*</span></div><div class="box">'+sic('server')+'<input data-field="baseUrl" placeholder="https://gitlab.com" value="'+esc(ST.baseUrl)+'"></div></div>'
        +'<div class="field"><div class="flabel">'+T.groupToken+' <span class="req">*</span>'+info(T.tokenHint)+'</div><div class="box">'+sic('key')+'<input data-field="token" type="'+(ST.showTok?'text':'password')+'" placeholder="glpat-xxxxxxxxxxxxxxxxxxxx" value="'+esc(ST.token)+'"><button class="eye" data-act="eye">'+ic('eye',17)+'</button></div></div>'
        +'<div class="suA-testrow"><button class="btn outline sm" data-act="test"'+(ST.test==='testing'?' disabled':'')+'>'+ic('zap',16)+T.testConn+'</button>'+chip+'</div>'
        +'<details class="suA-adv"><summary>'+T.advanced+info(T.advancedHint)+'</summary><div class="suA-advgrid">'
          +'<label class="suA-advitem"><span class="suA-advlabel">'+T.timeout+' <span class="suA-advunit">(s)</span>'+info(T.timeoutHint)+'</span><input class="suA-advinput" data-field="timeout" inputmode="numeric" value="'+esc(ST.timeout)+'"></label>'
          +'<div class="suA-advitem"><button class="tog'+(ST.selfSigned?' on':'')+'" data-act="self"><b></b></button><span class="suA-advlabel">'+T.selfSigned+info(T.selfSignedSub)+'</span></div>'
        +'</div></details></div>';
    }
    h+='</div>';
    h+='<div class="suA-acc'+(aOpen?' open':'')+'" style="margin-top:10px"><button class="suA-acchead" data-act="acc1:admin"><span class="ic">'+ic('users',16)+'</span><span class="suA-acct">'+T.adminSec+'</span><span class="suA-accprev">'+aPrev+'</span><span class="suA-accchev">'+ic('chevR',16)+'</span></button>';
    if(aOpen){
      h+='<div class="suA-accbody">';
      if(aConn){
        h+='<div class="suA-admincard"><span class="suA-adminav" style="background:'+avColor(au.handle||au.name||'a')+'">'+esc((au.name||'?').charAt(0).toUpperCase())+'</span>'
          +'<div class="suA-adminmeta"><div class="suA-adminname">'+esc(au.name)+(au.role?' <span class="suA-adminrole">'+esc(au.role)+'</span>':'')+'</div><div class="suA-adminhandle">'+esc(au.handle)+'</div></div>'
          +'<span class="suA-adminok">'+ic('check',14)+' '+T.adminOk+'</span><button class="suA-adminchange" data-act="adminchange">'+T.edit+'</button></div>';
      } else if(!OAUTHOK || ST.oauthEdit){
        var bu=(ST.baseUrl||'').replace(/\/+$/,'');
        var appsUrl=bu+'/-/profile/applications';
        var reconf=OAUTHOK&&ST.oauthEdit; // reconfiguration : secret optionnel (conservé si vide)
        h+='<div class="suA-oauthsetup">'
          // L'instance OAuth est CELLE de « Connexion à GitLab » (ST.baseUrl partagé) — pas de champ dupliqué ici.
          // Si l'instance n'est pas encore renseignée, on invite à le faire dans l'accordéon du dessus.
          +'<div class="suA-oauthstep">'+(bu?'<a class="suA-oauthlink" href="'+esc(appsUrl)+'" target="_blank" rel="noopener">'+T.oauthOpenApps+' '+ic('arrow',13)+'</a>':'<span class="suA-oauthlink" style="opacity:.6;cursor:default">'+T.oauthFillInstance+'</span>')+'</div>'
          +'<div class="suA-oauthhint">'+T.oauthScopeHint+'</div>'
          +'<div class="field"><div class="flabel">'+T.appId+' <span class="req">*</span></div><div class="box">'+sic('key')+'<input data-field="oauthClientId" value="'+esc(ST.oauthClientId)+'"></div></div>'
          +'<div class="field"><div class="flabel">'+T.secret+' '+(reconf?'':'<span class="req">*</span>')+'</div><div class="box">'+sic('key')+'<input data-field="oauthSecret" type="password"'+(reconf?' placeholder="••••••••"':'')+' value="'+esc(ST.oauthSecret)+'"></div></div>';
        if(ST.adminErr)h+='<div class="note" style="background:var(--bad-soft);border-left-color:var(--bad)"><span class="ic" style="color:var(--bad)">'+ic('info',16)+'</span><div>'+esc(ST.adminErr)+'</div></div>';
        h+='<button class="suA-glbtn'+(ST.adminState==='connecting'?' busy':'')+'" data-act="oauthsave"'+(ST.adminState==='connecting'?' disabled':'')+'>'+(ST.adminState==='connecting'?'<span class="spin"></span>'+T.connecting:'<span class="suA-glmark">'+GITLAB_MARK+'</span>'+T.oauthSaveConnect)+'</button>';
        if(reconf)h+='<button class="btn ghost sm" data-act="oauthcancel" style="margin-top:6px">'+T.cancel+'</button>';
        h+='</div>';
      } else {
        // Indicateur = UNIQUEMENT l'instance saisie (ST.baseUrl), vide → « — ». #curInst est mis à jour en direct
        // quand on tape dans le champ base url (cf. handler 'input'). Aucune instance configurée affichée par défaut.
        h+='<div class="suA-adminrow"><button class="suA-glbtn'+(ST.adminState==='connecting'?' busy':'')+'" data-act="oauth"'+(ST.adminState==='connecting'?' disabled':'')+'>'+(ST.adminState==='connecting'?'<span class="spin"></span>'+T.connecting:'<span class="suA-glmark">'+GITLAB_MARK+'</span>'+T.withGitlab)+'</button></div>';
        h+='<div class="suA-oauthcur">'+T.oauthCurrent+' <code id="curInst">'+esc(oauthInst()||'—')+'</code></div>';
        h+='<button class="btn outline sm suA-reconf" data-act="oauthedit">'+ic('key',15)+' '+T.oauthReconfigure+'</button>';
      }
      h+='</div>';
    }
    h+='</div><div class="req" style="margin-top:8px"><span class="r">*</span> '+T.requiredBoth+'</div>';
    return h;
  }
  // Liste des projets (filtrée par la recherche) — extraite de s1() pour une mise à jour CIBLÉE
  // au fil de la frappe (#checklist), car un render() global ferait perdre le focus du champ.
  function checklistHtml(){
    var q=(ST.projSearch||'').trim().toLowerCase();
    var shown=q?ST.projects.filter(function(p){return (p.name||'').toLowerCase().indexOf(q)>=0||String(p.id).indexOf(q)>=0;}):ST.projects;
    if(!shown.length)return '<div class="empty" style="padding:8px 2px">'+T.noProjMatch.replace('{q}',esc(ST.projSearch))+'</div>';
    var h='<div class="checklist">';
    for(var i=0;i<shown.length;i++){var p=shown[i];var on=ST.importIds.indexOf(p.id)>=0;h+='<button class="chk'+(on?' on':'')+'" data-act="proj:'+p.id+'"><span class="chkbox">'+(on?ic('check',13):'')+'</span><span class="chkl">'+esc(p.name)+'<b>#'+p.id+'</b></span><span class="grp">'+esc(p.group||'')+'</span></button>';}
    return h+'</div>';
  }
  function s1(){
    var allOn=ST.projects.length>0&&ST.importIds.length===ST.projects.length;
    var h='<div class="note">'+sic('info')+'<div><b>'+ST.importIds.length+'</b>'+T.projSelectedOf+ST.projects.length+T.accessible+'</div></div>';
    if(ST.projects.length)h+='<div class="suA-selall"><button class="suA-selallbtn" data-act="toggleall">'+ic(allOn?'x':'check',14)+(allOn?T.deselectAll:T.selectAll)+'</button><span class="suA-selcount">'+ST.importIds.length+' / '+ST.projects.length+'</span></div>';
    if(ST.projects.length)h+='<div class="search'+(ST.projSearch?' filled':'')+'">'+sic('search')
      +'<input data-field="projSearch" value="'+esc(ST.projSearch)+'" placeholder="'+esc(T.searchProjects)+'">'
      +(ST.projSearch?'<button class="searchx" data-act="clrsearch">'+ic('x',14)+'</button>':'')+'</div>';
    return h+'<div id="checklist">'+checklistHtml()+'</div>';
  }
  function s2(){
    if(!ST.labelsLoaded)return '<div class="note">'+sic('info')+'<div>'+T.loadingLabels+'</div></div>';
    var ph=curPhases(),mp=curMap();
    var prod=ST.labels.filter(function(l){return l.toLowerCase().indexOf('prod::')===0;});
    var mapped=prod.filter(function(l){return (mp[l]||'none')!=='none';}).length;
    var h='<div class="note">'+sic('info')+'<div><span class="prereq">'+T.prereq+'</span> '+T.prereqText+'</div></div>';
    // Portée : phases/associations globales ('all') ou distinctes par projet ('per').
    h+='<div class="suA-scope"><div class="suA-seg"><button class="'+(ST.phaseScope==='all'?'on':'')+'" data-act="scope:all">'+T.scopeAll+'</button><button class="'+(ST.phaseScope==='per'?'on':'')+'" data-act="scope:per">'+T.scopePer+'</button></div><span class="suA-scopehint">'+(ST.phaseScope==='all'?T.scopeHintAll:T.scopeHintPer)+'</span></div>';
    if(ST.phaseScope==='per'){
      h+='<div class="suA-projtabs">';
      for(var pi=0;pi<ST.importIds.length;pi++){var ptid=ST.importIds[pi];var pp=ST.projects.filter(function(x){return x.id===ptid;})[0];if(pp)h+='<button class="suA-projtab'+(activeProj()===ptid?' on':'')+'" data-act="projtab:'+ptid+'">'+esc(pp.name)+'</button>';}
      h+='</div>';
    }
    // Accordéon 1 — Phases (éditeur). Aperçu = pastilles de couleur.
    var pOpen=ST.acc==='phases';
    h+='<div class="suA-acc'+(pOpen?' open':'')+'"><button class="suA-acchead" data-act="acc:phases"><span class="ic">'+ic('layers',16)+'</span><span class="suA-acct">'+T.phases+' <span class="subc">'+ph.length+'</span></span><span class="suA-accprev">';
    for(var pv=0;pv<Math.min(9,ph.length);pv++)h+='<i style="background:'+esc(ph[pv].color)+'" title="'+esc(ph[pv].name)+'"></i>';
    h+='</span><span class="suA-accchev">'+ic('chevR',16)+'</span></button>';
    if(pOpen){
      h+='<div class="suA-accbody"><div class="phases">';
      for(var i=0;i<ph.length;i++){var p=ph[i];
        h+='<div class="phrow"><div class="swatchwrap"><button class="swatch" style="background:'+esc(p.color)+'" data-act="phcol:'+esc(p.id)+'" title="'+esc(T.changeColor)+'"></button>';
        if(ST.openColor===p.id){h+='<div class="pop">';for(var c=0;c<PALETTE.length;c++)h+='<button class="pc'+(PALETTE[c]===p.color?' on':'')+'" style="background:'+PALETTE[c]+'" data-act="phpick:'+esc(p.id)+'~'+PALETTE[c]+'"></button>';h+='</div>';}
        h+='</div><input class="phname" data-phname="'+esc(p.id)+'" value="'+esc(p.name)+'"><button class="phx" data-act="phrm:'+esc(p.id)+'" title="'+esc(T.deletePhase)+'">×</button></div>';
      }
      h+='<button class="phadd" data-act="phadd">'+ic('plus',16)+T.addPhase+'</button></div></div>';
    }
    h+='</div>';
    // Accordéon 2 — Association des labels (scope Prod::). Aperçu = compteur N/M liés.
    var lOpen=ST.acc==='labels';
    h+='<div class="suA-acc'+(lOpen?' open':'')+'"><button class="suA-acchead" data-act="acc:labels"><span class="ic">'+ic('link',16)+'</span><span class="suA-acct">'+T.labelMapping+'</span><span class="suA-accprev"><span class="suA-acccount'+(prod.length&&mapped===prod.length?' full':'')+'">'+mapped+' / '+prod.length+' '+T.linked+'</span></span><span class="suA-accchev">'+ic('chevR',16)+'</span></button>';
    if(lOpen){
      var phOpts=[['none',T.notTracked]].concat(ph.map(function(p){return [p.id,p.name];}));
      h+='<div class="suA-accbody">';
      if(!ST.importIds.length){
        h+='<div class="empty">'+T.noProjForLabels+'</div><button class="btn outline sm" style="align-self:flex-start" data-act="goto:1">'+ic('chevL',15)+T.stepProjets+'</button>';
      } else if(!prod.length){
        if(!ST.labels.length){
          var failed=(ST.labelsDiag||[]).filter(function(d){return !d.ok;}).map(function(d){return '#'+d.id;});
          h+='<div class="empty">'+T.noLabelsFetched+(failed.length?(' (✗ '+failed.join(', ')+')'):'')+'</div>';
        } else {
          h+='<div class="empty">'+ST.labels.length+' '+T.labelsNonProd+'</div><div style="display:flex;flex-wrap:wrap;gap:6px">';
          for(var li=0;li<ST.labels.length;li++)h+='<span class="grp" style="text-transform:none;letter-spacing:0;font-family:var(--mono)">'+esc(ST.labels[li])+'</span>';
          h+='</div>';
        }
      } else {
        h+='<div class="map">';
        for(var j=0;j<prod.length;j++){var ll=prod[j];var phv=mp[ll]||'none';
          h+='<div class="maprow'+(phv==='none'?' unset':'')+'"><span class="dot2" style="background:'+phaseColor(phv)+'"></span><span class="mlabel">'+esc(ll)+'</span><span class="arrow">'+ic('arrow',15)+'</span>'+menuSel('phase:'+ST.labels.indexOf(ll),phv,phOpts,phaseColor)+'</div>';}
        h+='</div>';
      }
      if(ST.importIds.length)h+='<button class="btn outline sm" style="align-self:flex-start" data-act="reloadlabels">'+ic('zap',15)+T.refreshLabels+'</button>';
      h+='</div>';
    }
    h+='</div>';
    return h;
  }
  function s3(){
    var roleOpts=[['lead',T.roleLead],['member',T.roleMember]];
    var h='<div class="note">'+sic('info')+'<div>'+T.teamsIntro+'</div></div><div class="teamcol">';
    var vt=visibleTeams();
    if(!vt.length)h+='<div class="empty" style="padding:14px 0">'+T.noTeamsForProj+'</div>';
    for(var t=0;t<vt.length;t++){var tm=vt[t];var mem=ST.memberships.filter(function(m){return m.teamId===tm.id;});var open=ST.teamOpen.indexOf(tm.id)>=0;
      h+='<div class="team suA-teamacc'+(open?' open':'')+'"><div class="teamh" data-act="team:'+tm.id+'"><span class="suA-teamchev">'+ic('chevR',15)+'</span><input class="teamname" data-team="'+tm.id+'" value="'+esc(tm.name)+'">'+(tm.gitlab?'<span class="glbadge">'+T.glGroup+'</span>':'<span class="newbadge">'+T.newTeam+'</span>')+'<span class="suA-teamcount">'+mem.length+' '+(mem.length>1?T.members:T.member)+'</span><span class="suA-teampreview">';
      for(var pv=0;pv<Math.min(5,mem.length);pv++)h+=av(mem[pv].pid,POOLname(mem[pv].pid),20);
      h+='</span><button class="teamx" data-act="rmteam:'+tm.id+'">×</button></div>';
      if(open){h+='<div class="mlist">';
        if(mem.length===0)h+='<div class="empty">'+T.noMembers+'</div>';
        for(var j=0;j<mem.length;j++){var m=mem[j];var nm=POOLname(m.pid);h+='<div class="mrow">'+av(m.pid,nm,24)+'<span class="mname">'+esc(nm)+'</span>'+menuSel('role:'+m.pid+'~'+tm.id,m.role,roleOpts,null)+'<button class="mx" data-act="rmmem:'+m.pid+'~'+tm.id+'">×</button></div>';}
        var avail=allPeople().filter(function(pid){return !mem.some(function(m){return m.pid===pid;});});
        if(avail.length){h+='<div class="addsel">'+ic('plus',14)+'<select data-add="'+tm.id+'"><option value="">'+T.addMember+'</option>';for(var a=0;a<avail.length;a++)h+='<option value="'+esc(avail[a])+'">'+esc(POOLname(avail[a]))+'</option>';h+='</select></div>';}
        h+='</div>';
      }
      h+='</div>';
    }
    return h+'</div><button class="btn outline sm" style="align-self:flex-start;margin-top:12px" data-act="addteam">'+ic('plus',16)+T.newTeamName+'</button>';
  }
  // Milestone à IMPORTER pour un projet (périmètre initial, pas une borne) : choix explicite
  // (y compris '' = tout l'historique et '__skip__' = ne pas extraire), sinon défaut = la plus récente.
  function msFor(p){var v=ST.exportMilestone[p.id];if(v!==undefined&&v!==null)return v;return (p.milestones&&p.milestones[0])||'';}
  function s4(){
    var imp=ST.projects.filter(function(p){return ST.importIds.indexOf(p.id)>=0;}).map(function(p){return p.name;});
    var mapped=0;for(var k in ST.labelPhase)if(ST.labelPhase[k]&&ST.labelPhase[k]!=='none')mapped++;
    var _vt=visibleTeams();var _vid={};_vt.forEach(function(t){_vid[t.id]=1;});
    var ppl={};ST.memberships.forEach(function(m){if(_vid[m.teamId])ppl[m.pid]=1;});
    var phVal=ST.phaseScope==='per'?T.perProjectRecap:(mapped+T.labelsLinked);
    var rows=[['link',T.stepConnexion,ST.baseUrl.replace(/^https?:\/\//,''),0],['box',T.stepProjets,imp.length?imp.join(', '):T.none,1],['layers',T.stepPhases,phVal,2],['users',T.stepEquipes,_vt.length+T.teamsCount+Object.keys(ppl).length+T.peopleCount,3],['server',T.stepConnexions,ST.cannyConnected?T.cannyDone:'—',4]];
    var h='<div class="recap">';
    for(var i=0;i<rows.length;i++)h+='<div class="rrow"><span class="ric">'+ic(rows[i][0],15)+'</span><div class="rk">'+rows[i][1]+'</div><div class="rv">'+esc(rows[i][2])+'</div><button class="redit" data-act="goto:'+rows[i][3]+'">'+T.edit+'</button></div>';
    h+='</div>';
    // Milestone à importer, PAR PROJET sélectionné (la 1re extraction n'importe qu'elle ; les autres
    // milestones restent importables à tout moment via Options → Régénération).
    var imported=ST.projects.filter(function(p){return ST.importIds.indexOf(p.id)>=0;});
    h+='<div class="mstones"><div class="msecthead"><span class="msectlabel">'+T.msStart+'</span></div>';
    if(!imported.length)h+='<div class="empty" style="padding:10px 16px">'+T.noProjSelected+'</div>';
    else for(var mi=0;mi<imported.length;mi++){var mp2=imported[mi];
      // « Aucune » = ne rien importer pour ce projet ; « Tout l'historique » = tout le projet.
      var opts=[['__skip__',T.msNone],['',T.msAll]].concat((mp2.milestones||[]).map(function(m){return [m,m];}));
      h+='<div class="msrow"><span class="msdot">'+ic('flag',14)+'</span><span class="msname">'+esc(mp2.name)+'</span>'+menuSel('ms:'+mp2.id,msFor(mp2),opts,null)+'</div>';}
    h+='</div>';
    if(ST.saveErr)h+='<div class="note" style="background:var(--bad-soft);border-left-color:var(--bad)"><span class="ic" style="color:var(--bad)">'+ic('info',16)+'</span><div>'+esc(ST.saveErr)+'</div></div>';
    return h;
  }
  function sic(n){return '<span class="ic">'+ic(n,16)+'</span>';}
  // Bulle d'aide « i » (au survol/focus) — remplace les sous-textes permanents.
  function info(t){return '<span class="su-info" tabindex="0"><span class="su-info-i">i</span><span class="su-info-pop">'+t+'</span></span>';}
  // Positionne le popup « i » (en position:fixed) près de l'icône, au-dessus si possible sinon en dessous,
  // centré horizontalement et clampé dans le viewport. Évite que le popup soit coupé par un conteneur.
  function positionInfo(host){var pop=host.querySelector('.su-info-pop');if(!pop)return;
    var r=host.getBoundingClientRect(),vw=window.innerWidth,vh=window.innerHeight;
    var cx=Math.min(Math.max(r.left+r.width/2,124),Math.max(124,vw-124));pop.style.left=cx+'px';
    if(r.top>180){pop.style.top='auto';pop.style.bottom=(vh-r.top+8)+'px';}else{pop.style.bottom='auto';pop.style.top=(r.bottom+8)+'px';}}
  app.addEventListener('mouseover',function(e){var h=e.target.closest&&e.target.closest('.su-info');if(h)positionInfo(h);});
  app.addEventListener('focusin',function(e){var h=e.target.closest&&e.target.closest('.su-info');if(h)positionInfo(h);});
  // Un tooltip focalisé (clavier) reste visible : le repositionner si la vue défile/redimensionne, sinon le popup
  // (position:fixed, figé au viewport) se détacherait de son icône. Capture → attrape le scroll de #body.
  function repositionActiveInfo(){var a=document.activeElement;if(a&&a.closest){var h=a.closest('.su-info');if(h)positionInfo(h);}}
  app.addEventListener('scroll',repositionActiveInfo,true);
  window.addEventListener('resize',repositionActiveInfo);
  // Dropdown CUSTOM (popover) — remplace le <select> natif (options stylées par l'OS, hors DA).
  // act : identifiant d'action (ex "phase:3", "role:brice~g-qa", "ms:4"). dotFn(valeur)→couleur ou null.
  // Un seul ouvert à la fois (ST.openDd) ; sélection via [data-opt], fermeture au clic extérieur.
  function menuSel(act,val,opts,dotFn){
    var sel=null;for(var i=0;i<opts.length;i++)if(opts[i][0]===val)sel=opts[i];
    var open=ST.openDd===act;
    var dot=function(v){return dotFn?('<span class="dddot" style="background:'+dotFn(v)+'"></span>'):'';};
    var h='<div class="dd"><button class="ddbtn'+(open?' open':'')+'" data-dd="'+esc(act)+'">'
      +dot(val)+'<span class="ddlabel">'+esc(sel?sel[1]:'')+'</span><span class="ic">'+ic('chevD',13)+'</span></button>';
    if(open){h+='<div class="ddmenu right">';
      for(var j=0;j<opts.length;j++){var o=opts[j];h+='<button class="ddopt'+(o[0]===val?' on':'')+'" data-opt="'+esc(act)+'~~'+esc(o[0])+'">'
        +dot(o[0])+'<span class="ddlabel">'+esc(o[1])+'</span>'+(o[0]===val?'<span class="ddcheck">'+ic('check',13)+'</span>':'')+'</button>';}
      h+='</div>';}
    return h+'</div>';
  }
  // Affectation d'une sélection de dropdown (phase d'un label, rôle d'un membre, milestone de départ).
  function applySel(act,val){
    if(act.indexOf('phase:')===0){var i=+act.slice(6);setMapVal(ST.labels[i],val);}
    else if(act.indexOf('role:')===0){var pr=act.slice(5).split('~');ST.memberships.forEach(function(m){if(m.pid===pr[0]&&m.teamId===pr[1])m.role=val;});}
    else if(act.indexOf('ms:')===0){ST.exportMilestone[act.slice(3)]=val;}
  }

  // PEOPLE (username→nom) déclaré plus haut + reconstruit dans doTest / au restore localStorage.
  function POOLname(id){return PEOPLE[id]||id;}
  function allPeople(){return Object.keys(PEOPLE);}

  // Équipes visibles = équipes manuelles (toujours) + groupes GitLab rattachés à un projet SÉLECTIONNÉ
  // (le groupe possède le namespace du projet, ou en est un ancêtre). Évite d'afficher tous les groupes.
  function selNamespaces(){var s={};ST.projects.forEach(function(p){if(ST.importIds.indexOf(p.id)>=0&&p.groupFull)s[p.groupFull]=1;});return Object.keys(s);}
  // Garde-fou migration : un localStorage écrit par un build ANTÉRIEUR a des projets SANS groupFull. Dans ce cas
  // on ne peut pas filtrer de façon fiable → on n'exclut PAS les équipes GitLab (sinon elles disparaîtraient de
  // l'affichage ET du payload de save). Le filtrage normal reprend dès un nouveau « Tester la connexion ».
  function nsDataMissing(){var sel=ST.projects.filter(function(p){return ST.importIds.indexOf(p.id)>=0;});return sel.length>0&&!sel.some(function(p){return p.groupFull;});}
  function teamVisible(tm){if(!tm.gitlab)return true;if(nsDataMissing())return true;var gp=tm.groupPath||tm.name;return selNamespaces().some(function(n){return n===gp||n.indexOf(gp+'/')===0;});}
  function visibleTeams(){return ST.teams.filter(teamVisible);}

  // ---- actions ----
  function conn(){return {baseUrl:ST.baseUrl.trim().replace(/\/+$/,''),token:ST.token.trim(),selfSigned:ST.selfSigned,timeout:parseInt(ST.timeout,10)||60};}
  // Hôte affiché dans « Instance : … » : UNIQUEMENT ce qui est saisi dans le champ base url (live). Vide → « — ».
  // Aucune trace de l'instance configurée par défaut (demande explicite).
  function oauthInst(){return (ST.baseUrl||'').replace(/^https?:\/\//,'').replace(/\/+$/,'');}
  function doTest(){
    ST.test='testing';render();
    fetch('/api/setup/test',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(conn())})
      .then(function(r){return r.json();}).then(function(j){
        if(!j.ok){ST.test='err';render();return;}
        ST.projects=j.projects||[];ST.groups=j.groups||[];
        // Par défaut : AUCUN projet coché. L'admin sélectionne explicitement (ou « Tout cocher »).
        // build teams + memberships + people from groups
        PEOPLE={};ST.teams=[];ST.memberships=[];
        (ST.groups||[]).forEach(function(g,gi){var id='g'+gi;ST.teams.push({id:id,name:g.name,gitlab:true,groupPath:g.name});
          (g.members||[]).forEach(function(mb){PEOPLE[mb.username]=mb.name||mb.username;ST.memberships.push({pid:mb.username,teamId:id,role:mb.role==='lead'?'lead':'member'});});});
        ST.test='ok';render();
      }).catch(function(){ST.test='err';render();});
  }
  // Enregistre la config OAuth (in-app) puis lance le SSO. Reconfiguration serveur À CHAUD → pas de redémarrage.
  function doOauthSave(){
    // Garde AVANT d'ouvrir la popup : sans instance (champ « Connexion à GitLab » vide), le serveur rejetterait
    // → on éviterait juste une popup vide qui s'ouvre puis se referme. On guide l'utilisateur à la place.
    if(!conn().baseUrl){ST.adminState='idle';ST.adminErr=T.oauthFillInstance;render();return;}
    var p=ssoOpen(); // ouvrir la popup DANS le geste de clic (sinon bloquée) — AVANT le fetch async
    ST.adminState='connecting';ST.adminErr='';render();
    fetch('/api/setup/oauth',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({clientId:(ST.oauthClientId||'').trim(),clientSecret:(ST.oauthSecret||'').trim(),authority:conn().baseUrl,selfSigned:ST.selfSigned})})
      .then(function(r){return r.json();}).then(function(j){
        if(j.ok){ if(p){ssoTrack(p);} else {window.location.href='/auth/oauth?return=/setup';} }
        else{ if(p){try{p.close();}catch(_){}} ST.adminState='idle';ST.adminErr=j.error||T.saveImpossible;render(); }
      }).catch(function(){ if(p){try{p.close();}catch(_){}} ST.adminState='idle';ST.adminErr=T.serverUnreachable;render(); });
  }
  // Connexion Canny (étape facultative) : POST /api/setup/canny — la clé est validée + chiffrée côté serveur.
  function doCannySave(){
    var key=(ST.cannyApiKey||'').trim();
    if(!key){ST.cannyState='err';ST.cannyErr=T.cannyKeyRequired;render();return;}
    ST.cannyState='saving';ST.cannyErr='';render();
    fetch('/api/setup/canny',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({apiKey:key})})
      .then(function(r){return r.json();}).then(function(j){
        if(j&&j.ok){ST.cannyConnected=true;ST.cannyState='ok';ST.cannyApiKey='';}
        else{ST.cannyState='err';ST.cannyErr=(j&&j.error)||T.cannyFail;}
        render();
      }).catch(function(){ST.cannyState='err';ST.cannyErr=T.serverUnreachable;render();});
  }
  // SSO en POPUP : on ne quitte plus le wizard (fini la page blanche qui clignote). La popup est ouverte dans le
  // geste de clic (ssoOpen) puis dirigée vers /auth/oauth (ssoTrack). Au retour, /auth/popup-done poste un message
  // et se ferme → on relit /api/me (loadMe). Repli plein écran si la popup est bloquée.
  function ssoOpen(){var w=520,h=680,sw=(typeof screen!=='undefined'&&screen.width)||1200,sh=(typeof screen!=='undefined'&&screen.height)||800;try{var p=window.open('about:blank','kpi_sso','width='+w+',height='+h+',left='+Math.max(0,(sw-w)/2)+',top='+Math.max(0,(sh-h)/2));
    // Peint la popup en SOMBRE immédiatement — sinon about:blank flashe en blanc avant la navigation.
    try{p.document.write('<!doctype html><html><head><meta charset="utf-8"><title>GitLab</title></head><body style="margin:0;background:#0a0e13"></body></html>');p.document.close();}catch(e){}
    return p;}catch(e){return null;}}
  function ssoTrack(p){
    try{p.location.replace('/auth/oauth?return=/auth/popup-done');}catch(e){window.location.href='/auth/oauth?return=/setup';return;}
    ST.adminState='connecting';render();
    var done=false;
    function finish(){if(done)return;done=true;window.removeEventListener('message',onMsg);clearInterval(iv);loadMe();}
    function onMsg(e){if(e.origin===location.origin&&e.data&&e.data.kpiOauth){try{p.close();}catch(_){}finish();}}
    window.addEventListener('message',onMsg);
    var iv=setInterval(function(){if(p.closed)finish();},500);
  }
  function loadLabels(cb){
    ST.labelsLoaded=false;
    fetch('/api/setup/labels',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(Object.assign(conn(),{projectIds:ST.importIds}))})
      .then(function(r){return r.json();}).then(function(j){
        ST.labels=(j.ok&&j.labels)?j.labels:[];
        ST.labelsDiag=(j.ok&&j.perProject)?j.perProject:[];
        ST.labels.forEach(function(l){if(!(l in ST.labelPhase))ST.labelPhase[l]=guessPhase(l);});
        ST.labelsLoaded=true;cb&&cb();
      }).catch(function(){ST.labels=[];ST.labelsDiag=[];ST.labelsLoaded=true;cb&&cb();});
  }
  function save(){
    ST.saving=true;ST.saveErr='';render();
    function perPayload(arr){return arr.map(function(p){return {key:p.id,name:p.name,color:p.color,role:p.id==='uiux'?'nogc':'active',timed:p.id!=='uiux'};});}
    var payload={baseUrl:conn().baseUrl,token:conn().token,selfSigned:ST.selfSigned,timeout:conn().timeout,
      admins:[],
      projectIds:ST.importIds,labelPhases:ST.labelPhase,periods:perPayload(ST.phases),
      // Milestone à IMPORTER par projet (périmètre de la 1re extraction — pas une borne).
      startMilestones:ST.importIds.reduce(function(o,id){var p=ST.projects.filter(function(x){return x.id===id;})[0];var m=p?msFor(p):'';if(m)o[id]=m;return o;},{}),
      // Projets importés AVEC nom + namespace (pour l'onglet Options du dashboard, qui ne peut pas dériver les noms).
      projects:ST.projects.filter(function(p){return ST.importIds.indexOf(p.id)>=0;}).map(function(p){return {id:p.id,name:p.name,group:p.groupFull||''};}),
      teams:visibleTeams().map(function(t){return {name:t.name,groupPath:t.groupPath||'',members:ST.memberships.filter(function(m){return m.teamId===t.id;}).map(function(m){return {username:m.pid,role:m.role};})};})};
    // Mode « Par projet » : on transmet aussi les phases + associations distinctes par projet (Stage 2 côté dashboard).
    if(ST.phaseScope==='per'){var pbp={},lbp={};ST.importIds.forEach(function(id){pbp[id]=perPayload(ST.phasesByProject[id]||ST.phases);lbp[id]=ST.labelPhaseByProject[id]||ST.labelPhase;});payload.periodsByProject=pbp;payload.labelPhasesByProject=lbp;}
    fetch('/api/setup',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)})
      .then(function(r){return r.json();}).then(function(j){
        if(j.ok){
          // L'admin est authentifié via OAuth → on enchaîne directement sur le loader (plus de détour /login).
          clearST(); ST.saving=false; ST.launching=true; ST.progress={status:'running',percent:0,message:T.starting}; render(); setTimeout(pollProgress,500);
        }
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
    var factors=[62,90,72,98,68],bars='';
    for(var i=0;i<factors.length;i++)bars+='<i><b style="height:'+Math.round(pct*factors[i]/100)+'%"></b></i>';
    var eta=(pr.etaSeconds!=null&&!done&&!err)?fmtClock(pr.etaSeconds)+T.left:'';
    var status=done?('<span class="okic">'+ic('check',17)+'</span> '+T.done)
      :err?(T.failed+esc(pr.error||pr.message||''))
      :('<span class="spin"></span> '+esc(pr.message||T.extractingShort)+'<span class="ld-dots"><b></b><b></b><b></b></span>');
    // ld-sep (classe dédiée) : « dot » entrait en collision avec le .dot du STEPPER (rond de 34 px)
    // et rendait le séparateur illisible au milieu du texte.
    var meta=err?'':(T.extracting+(eta?(' <span class="ld-sep">·</span> '+eta):''));
    return '<div class="suA"><div class="suA-top"><div class="brand"><div class="mark">'+MARK+'</div><div><div class="bn">KPI</div><div class="bs">'+T.bs+'</div></div></div><div class="topright">'+LANG_SWITCH+'</div></div>'
      +'<div class="ld-launch"><div class="ld-logo">'+MARK+'</div>'
      +'<div class="ld-bars">'+bars+'</div>'
      +'<div class="ld-pct">'+pct+'<span>%</span></div>'
      +'<div class="ld-status'+(err?' err':'')+'">'+status+'</div>'
      +'<div class="ld-meta">'+meta+'</div>'
      +(done?'':'<button class="ld-cancel" data-act="cancelLaunch">'+(err?T.backToConfig:T.cancel)+'</button>')
      +'</div></div>';
  }
  function toTop(){var b=document.getElementById('body');if(b)b.scrollTop=0;}
  function go(n){ if(n>ST.step && !canNext())return; if(n===2 && !ST.labelsLoaded){ST.step=2;render();toTop();loadLabels(function(){render();toTop();});return;} ST.step=n; render(); toTop(); }

  app.addEventListener('click',function(e){
    // Dropdowns custom : toggle ([data-dd]), sélection ([data-opt]), fermeture au clic extérieur.
    var ddBtn=e.target.closest('[data-dd]');
    if(ddBtn){var kdd=ddBtn.dataset.dd;ST.openDd=(ST.openDd===kdd?'':kdd);render();return;}
    var opt=e.target.closest('[data-opt]');
    if(opt){var parts=opt.dataset.opt.split('~~');applySel(parts[0],parts[1]);ST.openDd='';render();return;}
    if(ST.openDd){ST.openDd='';render();}
    var b=e.target.closest('[data-act]');if(!b)return;var a=b.dataset.act;
    if(a==='eye'){ST.showTok=!ST.showTok;render();}
    else if(a==='self'){ST.selfSigned=!ST.selfSigned;render();}
    else if(a==='test'){doTest();}
    else if(a==='back'){if(ST.step===0){window.location.href='/login';}else{go(ST.step-1);}}
    else if(a==='next'){go(ST.step+1);}
    else if(a==='launch'){save();}
    else if(a.indexOf('goto:')===0){var n=+a.slice(5);if(n<ST.step||n===ST.step)go(n);}
    else if(a.indexOf('proj:')===0){var id=+a.slice(5);var k=ST.importIds.indexOf(id);if(k>=0)ST.importIds.splice(k,1);else ST.importIds.push(id);ST.labelsLoaded=false;render();}
    else if(a==='toggleall'){ST.importIds=(ST.importIds.length===ST.projects.length)?[]:ST.projects.map(function(p){return p.id;});ST.labelsLoaded=false;render();}
    else if(a.indexOf('rmteam:')===0){var tid=a.slice(7);ST.memberships=ST.memberships.filter(function(m){return m.teamId!==tid;});ST.teams=ST.teams.filter(function(t){return t.id!==tid;});render();}
    else if(a==='addteam'){ST.teams.push({id:'t'+Date.now(),name:T.newTeamName,gitlab:false});render();}
    else if(a.indexOf('rmmem:')===0){var pr=a.slice(6).split('~');ST.memberships=ST.memberships.filter(function(m){return !(m.pid===pr[0]&&m.teamId===pr[1]);});render();}
    else if(a==='cancelLaunch'){cancelLaunch();}
    else if(a.indexOf('phcol:')===0){var pid=a.slice(6);ST.openColor=(ST.openColor===pid?null:pid);render();}
    else if(a.indexOf('phpick:')===0){var pp=a.slice(7).split('~');ensurePer();curPhases().forEach(function(p){if(p.id===pp[0])p.color=pp[1];});ST.openColor=null;render();}
    else if(a.indexOf('phrm:')===0){var rid=a.slice(5);ensurePer();
      if(ST.phaseScope==='all'){ST.phases=ST.phases.filter(function(p){return p.id!==rid;});Object.keys(ST.labelPhase).forEach(function(k){if(ST.labelPhase[k]===rid)ST.labelPhase[k]='none';});}
      else{var ap=activeProj();ST.phasesByProject[ap]=(ST.phasesByProject[ap]||[]).filter(function(p){return p.id!==rid;});var mm=ST.labelPhaseByProject[ap]||{};Object.keys(mm).forEach(function(k){if(mm[k]===rid)mm[k]='none';});}
      render();}
    else if(a==='phadd'){ensurePer();var np={id:'ph-'+Date.now(),name:T.newPhase,color:PALETTE[curPhases().length%PALETTE.length]};if(ST.phaseScope==='all')ST.phases.push(np);else ST.phasesByProject[activeProj()].push(np);render();}
    else if(a.indexOf('acc:')===0){var ak=a.slice(4);ST.acc=(ST.acc===ak?'':ak);render();}
    else if(a==='reloadlabels'){ST.labelsLoaded=false;render();loadLabels(render);}
    else if(a==='clrsearch'){ST.projSearch='';render();}
    else if(a.indexOf('acc1:')===0){var k1=a.slice(5);var i1=ST.acc1.indexOf(k1);if(i1>=0)ST.acc1.splice(i1,1);else ST.acc1.push(k1);render();}
    else if(a.indexOf('scope:')===0){switchScope(a.slice(6));}
    else if(a.indexOf('projtab:')===0){ST.phaseProj=+a.slice(8);render();}
    else if(a==='oauth'){var pw=ssoOpen();if(pw){ssoTrack(pw);}else{window.location.href='/auth/oauth?return=/setup';}}
    else if(a==='oauthsave'){doOauthSave();}
    else if(a==='cannysave'){doCannySave();}
    else if(a==='oauthedit'){ST.oauthEdit=true;ST.adminErr='';render();}
    else if(a==='oauthcancel'){ST.oauthEdit=false;ST.adminErr='';render();}
    else if(a==='adminchange'){persistST();window.location.href='/logout?return=/setup';} // déconnecte la session app → réaffiche « Se connecter avec GitLab » (sinon GitLab ré-approuve le même compte)
    else if(a.indexOf('team:')===0){if(e.target.closest('input,select'))return;var tid2=a.slice(5);var k2=ST.teamOpen.indexOf(tid2);if(k2>=0)ST.teamOpen.splice(k2,1);else ST.teamOpen.push(tid2);render();}
  });
  app.addEventListener('input',function(e){
    var f=e.target.closest('[data-field]');if(f){ST[f.dataset.field]=f.value;if(f.dataset.field==='baseUrl'||f.dataset.field==='token')ST.test='idle';
      if(f.dataset.field==='baseUrl'){var ci=document.getElementById('curInst');if(ci)ci.textContent=oauthInst()||'—';} // « Instance : … » suit la base url en direct
      if(f.dataset.field==='projSearch'){var cl=document.getElementById('checklist');if(cl)cl.innerHTML=checklistHtml();} // MAJ ciblée : un render() global ferait perdre le focus
      return;}
    var tn=e.target.closest('[data-team]');if(tn){var _t=ST.teams.filter(function(x){return x.id===tn.dataset.team;})[0];if(_t)_t.name=tn.value;return;}
    var pn=e.target.closest('[data-phname]');if(pn){var pid=pn.dataset.phname;ensurePer();curPhases().forEach(function(p){if(p.id===pid)p.name=pn.value;});}
  });
  app.addEventListener('change',function(e){
    var sl=e.target.closest('[data-setlang]');if(sl){location.href='/set-lang?lang='+encodeURIComponent(sl.value)+'&return=/setup';return;}
    // (phase:/role: passés au dropdown custom → gérés au clic via applySel ; il ne reste que l'ajout de membre.)
    var ad=e.target.closest('[data-add]');if(ad&&ad.value){var tid=ad.dataset.add;if(!ST.memberships.some(function(m){return m.pid===ad.value&&m.teamId===tid;}))ST.memberships.push({pid:ad.value,teamId:tid,role:'member'});render();}
  });
  render();
  loadMe(); // identité admin (et détection du retour OAuth)
})();
</script>
</body>
</html>
""";
}
