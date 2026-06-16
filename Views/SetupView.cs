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
        // Pas de défaut gitlab.com : champ VIDE au bootstrap (placeholder) pour ne pas piéger les instances
        // self-hosted (sinon l'Authority OAuth retombe silencieusement sur gitlab.com). Une fois configuré,
        // pré-rempli avec l'Authority enregistrée pour permettre la reconfiguration.
        var defaultInstance = !string.IsNullOrWhiteSpace(auth.Authority) ? auth.Authority.TrimEnd('/') : "";
        var lc = Kpi.Localization.Loc.Normalize(culture);
        var langOptions = string.Join("", Kpi.Localization.Loc.List().Select(l =>
            $"<option value=\"{l[0]}\"{(l[0] == lc ? " selected" : "")}>{HtmlAttr(l[1])}</option>"));
        return Html
            .Replace("__OAUTH__", auth.OAuthConfigured ? "true" : "false")
            .Replace("__OAUTH_CLIENTID__", HtmlAttr(auth.ClientId ?? ""))
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
/* étape Phases : accordéons (Phases / Association des labels), un seul ouvert à la fois */
.suA-acc{border:1px solid var(--line);border-radius:14px;overflow:hidden;background:var(--panel-2);}
.suA-acc+.suA-acc{margin-top:10px;}
.suA-acchead{display:flex;align-items:center;gap:10px;width:100%;padding:13px 15px;background:none;border:0;cursor:pointer;color:var(--ink);font:inherit;text-align:left;}
.suA-acchead:hover{background:var(--panel-3);}
.suA-acchead .ic{color:var(--accent);display:flex;flex:none;}
.suA-acct{font-weight:700;font-size:14px;}
.suA-accchev{margin-left:auto;display:flex;color:var(--ink-faint);transition:transform .2s var(--ease);}
.suA-acc.open .suA-accchev{transform:rotate(90deg);}
.suA-accbody{padding:4px 15px 16px;display:flex;flex-direction:column;gap:12px;}
.suA-accbody .map{border:0;border-radius:0;background:none;}
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
.su-info-pop{position:absolute;bottom:calc(100% + 8px);left:50%;transform:translateX(-50%);width:230px;z-index:40;background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:9px 11px;font:400 11.5px/1.45 system-ui,sans-serif;color:var(--ink-dim);text-transform:none;letter-spacing:0;box-shadow:0 10px 30px rgba(0,0,0,.45);opacity:0;visibility:hidden;transition:opacity .12s;}
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
/* étape 1 — formulaire de configuration OAuth (in-app, sans édition d'appsettings) */
.suA-oauthsetup{display:flex;flex-direction:column;gap:12px;}
.suA-oauthstep{display:flex;gap:10px;align-items:flex-start;font-size:13px;color:var(--ink-dim);line-height:1.45;}
.suA-oauthnum{width:22px;height:22px;border-radius:7px;flex:none;background:var(--panel-3);border:1px solid var(--line);display:flex;align-items:center;justify-content:center;font-weight:700;font-size:12px;color:var(--ink);}
.suA-oauthlink{color:var(--accent);text-decoration:none;font-weight:600;display:inline-flex;align-items:center;gap:4px;}
.suA-oauthlink:hover{text-decoration:underline;}
.suA-oauthredir{font-size:11.5px;color:var(--ink-faint);margin-top:5px;}
.suA-oauthredir code{font-family:var(--mono);background:var(--panel-3);padding:1px 5px;border-radius:5px;color:var(--ink-dim);}
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
/* écran de chargement post-setup (loader temps réel) */
.ld-dots{display:inline-flex;align-items:center;}
.ld-dots b{display:inline-block;width:5px;height:5px;border-radius:50%;background:var(--accent);margin-left:4px;animation:lddot 1.2s ease-in-out infinite;}
.ld-dots b:nth-child(2){animation-delay:.18s;}.ld-dots b:nth-child(3){animation-delay:.36s;}
@keyframes lddot{0%,60%,100%{opacity:.25;transform:translateY(0);}30%{opacity:1;transform:translateY(-3px);}}
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
  var PALETTE=['#2188ff','#0ea5e9','#06b6d4','#2dd4bf','#0f9e8e','#22c55e','#84cc16','#eab308','#c79a06','#e0792e','#f97316','#ef4444','#d6336c','#ec4899','#d946ef','#a855f7','#8957e5','#6366f1','#64748b','#94a3b8'];
  var NONE_COLOR='#5f6b7a';
  // Couleur d'une phase par sa clé, depuis la liste ÉDITABLE (ST.phases). 'none' = gris.
  var phaseColor=function(k){if(k==='none')return NONE_COLOR;var ps=curPhases();for(var i=0;i<ps.length;i++)if(ps[i].id===k)return ps[i].color;return NONE_COLOR;};
  var P={link:'<path d="M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1.5 1.5"/><path d="M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1.5-1.5"/>',server:'<rect x="3" y="4" width="18" height="7" rx="2"/><rect x="3" y="13" width="18" height="7" rx="2"/><path d="M7 7.5h.01M7 16.5h.01"/>',key:'<circle cx="7.5" cy="15.5" r="3.5"/><path d="M10 13 21 2M18 5l2.5 2.5M15.5 7.5L18 10"/>',eye:'<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/>',box:'<path d="M21 8 12 3 3 8v8l9 5 9-5Z"/><path d="m3 8 9 5 9-5M12 13v8"/>',layers:'<path d="m12 2 9 5-9 5-9-5z"/><path d="m21 12-9 5-9-5"/><path d="m21 17-9 5-9-5"/>',users:'<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/>',check:'<path d="M20 6 9 17l-5-5"/>',chevR:'<path d="m9 18 6-6-6-6"/>',chevL:'<path d="m15 18-6-6 6-6"/>',chevD:'<path d="m6 9 6 6 6-6"/>',arrow:'<path d="M5 12h14M13 6l6 6-6 6"/>',zap:'<path d="M13 2 3 14h9l-1 8 10-12h-9l1-8Z"/>',info:'<circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/>',plus:'<path d="M12 5v14M5 12h14"/>',rocket:'<path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z"/><path d="m12 15-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z"/><path d="M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0"/><path d="M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5"/>',x:'<path d="M18 6 6 18M6 6l12 12"/>'};
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
      tokenHint:"Token de groupe, de projet ou personnel (scope read_api). Stocké côté serveur, jamais affiché aux utilisateurs.",
      adminLabel:"Compte(s) administrateur", adminHint:"Username(s) GitLab admin — 1re mise en service. Plusieurs : séparés par des virgules.",
      timeout:"Timeout (s)", timeoutHint:"Délai max d'une requête à l'API.",
      selfSigned:"Certificats auto-signés", selfSignedSub:"Pour les instances internes",
      testConn:"Tester la connexion", testing:"Test en cours…",
      connected:"Connecté · ", accessibleProjects:" projets accessibles",
      testFailed:"Échec · vérifiez l'URL et le token", notTested:"Non testé",
      requiredField:"champ obligatoire · une connexion réussie est requise pour continuer.",
      projSelectedOf:" projet(s) sélectionné(s) sur ", accessible:" accessibles.",
      loadingLabels:"Chargement des labels des projets sélectionnés…",
      selectAll:"Tout cocher", deselectAll:"Tout décocher",
      noProdLabels:"Aucun label Prod:: trouvé sur les projets sélectionnés.",
      refreshLabels:"Rafraîchir les labels",
      noProjForLabels:"Sélectionnez au moins un projet (étape Projets) pour charger ses labels.",
      noLabelsFetched:"Aucun label récupéré pour ce(s) projet(s). Vérifiez que le token a le scope read_api et accès au projet.",
      labelsNonProd:"labels récupérés, mais aucun dans le scope « Prod:: ». Les phases se basent sur des labels « Prod::Xxx ». Vos labels :",
      groupConn:"Connexion au groupe", groupToken:"Token", advanced:"Options avancées",
      advancedHint:"À ajuster uniquement pour les instances GitLab auto-hébergées. Les valeurs par défaut conviennent à gitlab.com.",
      adminSec:"Administrateur", adminNone:"Non identifié", adminOk:"Administrateur", connecting:"Connexion à GitLab…",
      withGitlab:"Se connecter avec GitLab", connectedShort:"Connecté",
      requiredBoth:"champs obligatoires · une connexion au groupe réussie et un administrateur identifié sont requis pour continuer.",
      scopeAll:"Tous les projets", scopePer:"Par projet",
      scopeHintAll:"Mêmes phases et associations pour tous les projets importés.",
      scopeHintPer:"Phases et associations distinctes pour chaque projet.",
      member:"membre", members:"membres", perProjectRecap:"par projet · phases & labels distincts",
      oauthStep1:"Créez une application OAuth sur GitLab (Redirect URI ci-dessous, scope read_user), puis collez les identifiants.",
      oauthOpenApps:"Ouvrir les Applications GitLab", oauthSaveConnect:"Enregistrer & se connecter", oauthReconfigure:"Reconfigurer l'OAuth", oauthCurrent:"Instance :",
      prereq:"Prérequis", prereqText:"Seuls les labels Prod:: sont pris en compte. Personnalisez vos phases (nom, couleur, ajout/suppression), puis reliez-y vos labels.",
      phases:"Phases", changeColor:"Changer la couleur", deletePhase:"Supprimer la phase", addPhase:"Ajouter une phase",
      labelMapping:"Association des labels", notTracked:"Non suivi",
      teamsIntro:"Équipes importées depuis les <b>groupes GitLab</b>. <b>Lead</b> = Maintainer · <b>Membre</b> = Developer. Un membre peut appartenir à <b>plusieurs équipes</b>.", noTeamsForProj:"Aucune équipe GitLab pour les projets sélectionnés. Ajoutez-en une manuellement ci-dessous.",
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
      tokenHint:"Group, project, or personal token (read_api scope). Stored server-side, never shown to users.",
      adminLabel:"Admin account(s)", adminHint:"GitLab admin username(s) — first setup only. Comma-separated for several.",
      timeout:"Timeout (s)", timeoutHint:"Max delay for an API request.",
      selfSigned:"Self-signed certificates", selfSignedSub:"For internal instances",
      testConn:"Test connection", testing:"Testing…",
      connected:"Connected · ", accessibleProjects:" accessible projects",
      testFailed:"Failed · check URL and token", notTested:"Not tested",
      requiredField:"required field · a successful connection is required to continue.",
      projSelectedOf:" project(s) selected of ", accessible:" accessible.",
      loadingLabels:"Loading labels…",
      selectAll:"Select all", deselectAll:"Deselect all",
      noProdLabels:"No Prod:: labels found on the selected projects.",
      refreshLabels:"Refresh labels",
      noProjForLabels:"Select at least one project (Projects step) to load its labels.",
      noLabelsFetched:"No label fetched for this project. Check that the token has the read_api scope and access to the project.",
      labelsNonProd:"labels fetched, but none in the « Prod:: » scope. Phases rely on « Prod::Xxx » labels. Your labels:",
      groupConn:"Group connection", groupToken:"Token", advanced:"Advanced options",
      advancedHint:"Adjust only for self-hosted GitLab instances. The defaults work for gitlab.com.",
      adminSec:"Administrator", adminNone:"Not identified", adminOk:"Administrator", connecting:"Connecting to GitLab…",
      withGitlab:"Sign in with GitLab", connectedShort:"Connected",
      requiredBoth:"required fields · a successful group connection and an identified administrator are required to continue.",
      scopeAll:"All projects", scopePer:"Per project",
      scopeHintAll:"Same phases and mappings for all imported projects.",
      scopeHintPer:"Separate phases and mappings for each project.",
      member:"member", members:"members", perProjectRecap:"per project · separate phases &amp; labels",
      oauthStep1:"Create an OAuth application on GitLab (Redirect URI below, scope read_user), then paste the credentials.",
      oauthOpenApps:"Open GitLab Applications", oauthSaveConnect:"Save & sign in", oauthReconfigure:"Reconfigure OAuth", oauthCurrent:"Instance:",
      prereq:"Prerequisite", prereqText:"Only Prod:: labels are taken into account. Customize your phases (name, color, add/remove), then link your labels to them.",
      phases:"Phases", changeColor:"Change color", deletePhase:"Delete phase", addPhase:"Add a phase",
      labelMapping:"Label mapping", notTracked:"Not tracked",
      teamsIntro:"Teams imported from <b>GitLab groups</b>. <b>Lead</b> = Maintainer · <b>Member</b> = Developer. A member can belong to <b>several teams</b>.", noTeamsForProj:"No GitLab team for the selected projects. Add one manually below.",
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
      "tokenHint": "Token de grupo, de proyecto o personal (alcance read_api). Almacenado en el servidor, nunca se muestra a los usuarios.",
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
      "selectAll": "Seleccionar todo",
      "deselectAll": "Deseleccionar todo",
      "noProdLabels": "No se encontraron etiquetas Prod:: en los proyectos seleccionados.",
      "refreshLabels": "Actualizar etiquetas",
      "noProjForLabels": "Selecciona al menos un proyecto (paso Proyectos) para cargar sus etiquetas.",
      "noLabelsFetched": "No se recuperó ninguna etiqueta para este proyecto. Verifica que el token tenga el alcance read_api y acceso al proyecto.",
      "labelsNonProd": "etiquetas recuperadas, pero ninguna en el ámbito « Prod:: ». Las fases se basan en etiquetas « Prod::Xxx ». Tus etiquetas:",
      "groupConn": "Conexión al grupo", "groupToken": "Token", "advanced": "Opciones avanzadas",
      "advancedHint": "Ajústalo solo en instancias de GitLab autoalojadas. Los valores predeterminados son adecuados para gitlab.com.",
      "adminSec": "Administrador", "adminNone": "Sin identificar", "adminOk": "Administrador", "connecting": "Conectando con GitLab…",
      "withGitlab": "Conectarse con GitLab", "connectedShort": "Conectado",
      "requiredBoth": "campos obligatorios · para continuar se requiere una conexión al grupo correcta y un administrador identificado.",
      "scopeAll": "Todos los proyectos", "scopePer": "Por proyecto",
      "scopeHintAll": "Mismas fases y asociaciones para todos los proyectos importados.",
      "scopeHintPer": "Fases y asociaciones distintas para cada proyecto.",
      "member": "miembro", "members": "miembros", "perProjectRecap": "por proyecto · fases y etiquetas distintas",
      "oauthStep1": "Crea una aplicación OAuth en GitLab (Redirect URI abajo, scope read_user), luego pega las credenciales.",
      "oauthOpenApps": "Abrir Aplicaciones de GitLab", "oauthSaveConnect": "Guardar y conectarse", "oauthReconfigure": "Reconfigurar OAuth", "oauthCurrent": "Instancia:",
      "prereq": "Requisito previo",
      "prereqText": "Solo se tienen en cuenta las etiquetas Prod::. Personaliza tus fases (nombre, color, añadir/quitar), luego vincula tus etiquetas a ellas.",
      "phases": "Fases",
      "changeColor": "Cambiar color",
      "deletePhase": "Eliminar fase",
      "addPhase": "Añadir una fase",
      "labelMapping": "Mapeo de etiquetas",
      "notTracked": "No rastreada",
      "teamsIntro": "Equipos importados desde <b>grupos de GitLab</b>. <b>Lead</b> = Maintainer · <b>Miembro</b> = Developer. Un miembro puede pertenecer a <b>varios equipos</b>.", "noTeamsForProj": "Ningún equipo GitLab para los proyectos seleccionados. Añade uno manualmente abajo.",
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
      "tokenHint": "Gruppen-, Projekt- oder persönlicher Token (Bereich read_api). Serverseitig gespeichert, wird Benutzern niemals angezeigt.",
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
      "selectAll": "Alle auswählen",
      "deselectAll": "Alle abwählen",
      "noProdLabels": "Keine Prod::-Label in den ausgewählten Projekten gefunden.",
      "refreshLabels": "Labels aktualisieren",
      "noProjForLabels": "Wählen Sie mindestens ein Projekt (Schritt Projekte), um dessen Label zu laden.",
      "noLabelsFetched": "Kein Label für dieses Projekt abgerufen. Prüfen Sie, ob das Token den Bereich read_api und Zugriff auf das Projekt hat.",
      "labelsNonProd": "Labels abgerufen, aber keines im « Prod:: »-Bereich. Phasen basieren auf « Prod::Xxx »-Labels. Ihre Labels:",
      "groupConn": "Verbindung zur Gruppe", "groupToken": "Token", "advanced": "Erweiterte Optionen",
      "advancedHint": "Nur für selbst gehostete GitLab-Instanzen anzupassen. Die Standardwerte eignen sich für gitlab.com.",
      "adminSec": "Administrator", "adminNone": "Nicht identifiziert", "adminOk": "Administrator", "connecting": "Verbindung zu GitLab…",
      "withGitlab": "Mit GitLab anmelden", "connectedShort": "Verbunden",
      "requiredBoth": "Pflichtfelder · Eine erfolgreiche Verbindung zur Gruppe und ein identifizierter Administrator sind erforderlich, um fortzufahren.",
      "scopeAll": "Alle Projekte", "scopePer": "Pro Projekt",
      "scopeHintAll": "Gleiche Phasen und Zuordnungen für alle importierten Projekte.",
      "scopeHintPer": "Eigene Phasen und Zuordnungen für jedes Projekt.",
      "member": "Mitglied", "members": "Mitglieder", "perProjectRecap": "pro Projekt · eigene Phasen &amp; Labels",
      "oauthStep1": "Erstellen Sie eine OAuth-Anwendung in GitLab (Redirect URI unten, scope read_user) und fügen Sie dann die Zugangsdaten ein.",
      "oauthOpenApps": "GitLab-Anwendungen öffnen", "oauthSaveConnect": "Speichern & anmelden", "oauthReconfigure": "OAuth neu konfigurieren", "oauthCurrent": "Instanz:",
      "prereq": "Voraussetzung",
      "prereqText": "Nur Label Prod:: werden berücksichtigt. Passen Sie Ihre Phasen an (Name, Farbe, Hinzufügen/Entfernen), verknüpfen Sie dann Ihre Label damit.",
      "phases": "Phasen",
      "changeColor": "Farbe ändern",
      "deletePhase": "Phase löschen",
      "addPhase": "Phase hinzufügen",
      "labelMapping": "Label-Zuordnung",
      "notTracked": "Nicht verfolgt",
      "teamsIntro": "Teams importiert aus <b>GitLab-Gruppen</b>. <b>Lead</b> = Maintainer · <b>Mitglied</b> = Developer. Ein Mitglied kann zu <b>mehreren Teams</b> gehören.", "noTeamsForProj": "Kein GitLab-Team für die ausgewählten Projekte. Fügen Sie unten manuell eines hinzu.",
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
      "tokenHint": "Token di gruppo, di progetto o personale (scope read_api). Archiviato lato server, mai mostrato agli utenti.",
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
      "selectAll": "Seleziona tutto",
      "deselectAll": "Deseleziona tutto",
      "noProdLabels": "Nessuna etichetta Prod:: trovata nei progetti selezionati.",
      "refreshLabels": "Aggiorna etichette",
      "noProjForLabels": "Seleziona almeno un progetto (passaggio Progetti) per caricarne le etichette.",
      "noLabelsFetched": "Nessuna etichetta recuperata per questo progetto. Verifica che il token abbia lo scope read_api e accesso al progetto.",
      "labelsNonProd": "etichette recuperate, ma nessuna nello scope « Prod:: ». Le fasi si basano su etichette « Prod::Xxx ». Le tue etichette:",
      "groupConn": "Connessione al gruppo", "groupToken": "Token", "advanced": "Opzioni avanzate",
      "advancedHint": "Da regolare solo per le istanze GitLab self-hosted. I valori predefiniti vanno bene per gitlab.com.",
      "adminSec": "Amministratore", "adminNone": "Non identificato", "adminOk": "Amministratore", "connecting": "Connessione a GitLab…",
      "withGitlab": "Accedi con GitLab", "connectedShort": "Connesso",
      "requiredBoth": "campi obbligatori · per continuare sono richiesti una connessione al gruppo riuscita e un amministratore identificato.",
      "scopeAll": "Tutti i progetti", "scopePer": "Per progetto",
      "scopeHintAll": "Stesse fasi e associazioni per tutti i progetti importati.",
      "scopeHintPer": "Fasi e associazioni distinte per ogni progetto.",
      "member": "membro", "members": "membri", "perProjectRecap": "per progetto · fasi &amp; etichette distinte",
      "oauthStep1": "Crea un'applicazione OAuth su GitLab (Redirect URI sotto, scope read_user), poi incolla le credenziali.",
      "oauthOpenApps": "Apri Applicazioni GitLab", "oauthSaveConnect": "Salva e accedi", "oauthReconfigure": "Riconfigura OAuth", "oauthCurrent": "Istanza:",
      "prereq": "Prerequisito",
      "prereqText": "Solo le etichette Prod:: vengono prese in considerazione. Personalizza le tue fasi (nome, colore, aggiungi/rimuovi), quindi collega le tue etichette ad esse.",
      "phases": "Fasi",
      "changeColor": "Cambia colore",
      "deletePhase": "Elimina fase",
      "addPhase": "Aggiungi una fase",
      "labelMapping": "Mapping etichette",
      "notTracked": "Non tracciato",
      "teamsIntro": "Team importati da <b>gruppi GitLab</b>. <b>Lead</b> = Maintainer · <b>Membro</b> = Developer. Un membro può appartenere a <b>più team</b>.", "noTeamsForProj": "Nessun team GitLab per i progetti selezionati. Aggiungine uno manualmente qui sotto.",
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
      "stepProjets": "Projetos",
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
      "tokenHint": "Token de grupo, de projeto ou pessoal (scope read_api). Armazenado no servidor, nunca mostrado aos utilizadores.",
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
      "selectAll": "Selecionar tudo",
      "deselectAll": "Desmarcar tudo",
      "noProdLabels": "Nenhum rótulo Prod:: encontrado nos projetos selecionados.",
      "refreshLabels": "Atualizar rótulos",
      "noProjForLabels": "Selecione pelo menos um projeto (passo Projetos) para carregar os seus rótulos.",
      "noLabelsFetched": "Nenhum rótulo obtido para este projeto. Verifique se o token tem o scope read_api e acesso ao projeto.",
      "labelsNonProd": "rótulos obtidos, mas nenhum no scope « Prod:: ». As fases baseiam-se em rótulos « Prod::Xxx ». Os seus rótulos:",
      "groupConn": "Ligação ao grupo", "groupToken": "Token", "advanced": "Opções avançadas",
      "advancedHint": "A ajustar apenas para instâncias GitLab auto-alojadas. Os valores predefinidos são adequados para gitlab.com.",
      "adminSec": "Administrador", "adminNone": "Não identificado", "adminOk": "Administrador", "connecting": "A ligar ao GitLab…",
      "withGitlab": "Iniciar sessão com o GitLab", "connectedShort": "Ligado",
      "requiredBoth": "campos obrigatórios · para continuar, é necessária uma ligação ao grupo bem-sucedida e um administrador identificado.",
      "scopeAll": "Todos os projetos", "scopePer": "Por projeto",
      "scopeHintAll": "Mesmas fases e associações para todos os projetos importados.",
      "scopeHintPer": "Fases e associações distintas para cada projeto.",
      "member": "membro", "members": "membros", "perProjectRecap": "por projeto · fases e rótulos distintos",
      "oauthStep1": "Crie uma aplicação OAuth no GitLab (Redirect URI abaixo, scope read_user) e cole as credenciais.",
      "oauthOpenApps": "Abrir Aplicações GitLab", "oauthSaveConnect": "Guardar e iniciar sessão", "oauthReconfigure": "Reconfigurar OAuth", "oauthCurrent": "Instância:",
      "prereq": "Pré-requisito",
      "prereqText": "Apenas rótulos Prod:: são considerados. Personalize as suas fases (nome, cor, adicionar/remover), depois associe os seus rótulos a elas.",
      "phases": "Fases",
      "changeColor": "Alterar cor",
      "deletePhase": "Eliminar fase",
      "addPhase": "Adicionar uma fase",
      "labelMapping": "Mapeamento de rótulos",
      "notTracked": "Não rastreado",
      "teamsIntro": "Equipes importadas a partir de <b>grupos GitLab</b>. <b>Lead</b> = Maintainer · <b>Membro</b> = Developer. Um membro pode pertencer a <b>várias equipes</b>.", "noTeamsForProj": "Nenhuma equipa GitLab para os projetos selecionados. Adicione uma manualmente abaixo.",
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
      "tokenHint": "Токен группы, проекта или персональный (scope read_api). Хранится на сервере, не показывается пользователям.",
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
      "selectAll": "Выбрать все",
      "deselectAll": "Снять выбор",
      "noProdLabels": "Метки Prod:: не найдены в выбранных проектах.",
      "refreshLabels": "Обновить метки",
      "noProjForLabels": "Выберите хотя бы один проект (шаг «Проекты»), чтобы загрузить его метки.",
      "noLabelsFetched": "Для этого проекта не получено ни одной метки. Убедитесь, что у токена есть scope read_api и доступ к проекту.",
      "labelsNonProd": "меток получено, но ни одной в области « Prod:: ». Этапы основаны на метках « Prod::Xxx ». Ваши метки:",
      "groupConn": "Подключение к группе", "groupToken": "Токен", "advanced": "Дополнительные параметры",
      "advancedHint": "Изменяйте только для самостоятельно размещённых экземпляров GitLab. Значения по умолчанию подходят для gitlab.com.",
      "adminSec": "Администратор", "adminNone": "Не определён", "adminOk": "Администратор", "connecting": "Подключение к GitLab…",
      "withGitlab": "Войти через GitLab", "connectedShort": "Подключено",
      "requiredBoth": "обязательные поля · для продолжения необходимы успешное подключение к группе и определённый администратор.",
      "scopeAll": "Все проекты", "scopePer": "По проекту",
      "scopeHintAll": "Одинаковые фазы и связи для всех импортированных проектов.",
      "scopeHintPer": "Отдельные фазы и связи для каждого проекта.",
      "member": "участник", "members": "участники", "perProjectRecap": "по проекту · отдельные фазы и метки",
      "oauthStep1": "Создайте OAuth-приложение в GitLab (Redirect URI ниже, scope read_user), затем вставьте учётные данные.",
      "oauthOpenApps": "Открыть приложения GitLab", "oauthSaveConnect": "Сохранить и войти", "oauthReconfigure": "Перенастроить OAuth", "oauthCurrent": "Экземпляр:",
      "prereq": "Предварительное условие",
      "prereqText": "Учитываются только метки Prod::. Настройте ваши фазы (название, цвет, добавление/удаление), затем свяжите с ними ваши метки.",
      "phases": "Фазы",
      "changeColor": "Изменить цвет",
      "deletePhase": "Удалить фазу",
      "addPhase": "Добавить фазу",
      "labelMapping": "Сопоставление меток",
      "notTracked": "Не отслеживается",
      "teamsIntro": "Команды, импортированные из <b>групп GitLab</b>. <b>Lead</b> = Maintainer · <b>Member</b> = Developer. Участник может принадлежать <b>нескольким командам</b>.", "noTeamsForProj": "Нет команды GitLab для выбранных проектов. Добавьте её вручную ниже.",
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
      "tokenHint": "رمز المجموعة أو المشروع أو شخصي (نطاق read_api). مخزن على الخادم، لا يُعرض أبداً للمستخدمين.",
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
      "selectAll": "تحديد الكل",
      "deselectAll": "إلغاء تحديد الكل",
      "noProdLabels": "لم يتم العثور على تسميات Prod:: في المشاريع المحددة.",
      "refreshLabels": "تحديث التسميات",
      "noProjForLabels": "اختر مشروعاً واحداً على الأقل (خطوة المشاريع) لتحميل تسمياته.",
      "noLabelsFetched": "لم يتم جلب أي تسمية لهذا المشروع. تأكد من أن الرمز يملك نطاق read_api والوصول إلى المشروع.",
      "labelsNonProd": "تسمية تم جلبها، لكن لا شيء في نطاق « Prod:: ». تعتمد المراحل على تسميات « Prod::Xxx ». تسمياتك:",
      "groupConn": "الاتصال بالمجموعة", "groupToken": "الرمز", "advanced": "خيارات متقدمة",
      "advancedHint": "للتعديل فقط في حالة مثيلات GitLab المستضافة ذاتيًا. القيم الافتراضية مناسبة لـ gitlab.com.",
      "adminSec": "مسؤول", "adminNone": "غير محدَّد", "adminOk": "مسؤول محدَّد", "connecting": "جارٍ الاتصال بـ GitLab…",
      "withGitlab": "الاتصال عبر GitLab", "connectedShort": "متصل",
      "requiredBoth": "حقول مطلوبة · يلزم اتصال ناجح بالمجموعة ومسؤول محدَّد للمتابعة.",
      "scopeAll": "جميع المشاريع", "scopePer": "حسب المشروع",
      "scopeHintAll": "نفس المراحل والارتباطات لجميع المشاريع المستوردة.",
      "scopeHintPer": "مراحل وارتباطات مستقلة لكل مشروع.",
      "member": "عضو", "members": "أعضاء", "perProjectRecap": "حسب المشروع · مراحل وتسميات مستقلة",
      "oauthStep1": "أنشئ تطبيق OAuth في GitLab (Redirect URI أدناه، scope read_user)، ثم الصق بيانات الاعتماد.",
      "oauthOpenApps": "فتح تطبيقات GitLab", "oauthSaveConnect": "حفظ وتسجيل الدخول", "oauthReconfigure": "إعادة تكوين OAuth", "oauthCurrent": "المثيل:",
      "prereq": "المتطلب الأساسي",
      "prereqText": "فقط تسميات Prod:: يتم أخذها بعين الاعتبار. قم بتخصيص مراحلك (الاسم واللون والإضافة/الحذف)، ثم ربط تسمياتك بها.",
      "phases": "المراحل",
      "changeColor": "تغيير اللون",
      "deletePhase": "حذف المرحلة",
      "addPhase": "إضافة مرحلة",
      "labelMapping": "تعيين التسميات",
      "notTracked": "غير مُتابعة",
      "teamsIntro": "فرق مستوردة من <b>مجموعات GitLab</b>. <b>Lead</b> = المسؤول · <b>العضو</b> = المطور. يمكن لعضو الانتماء إلى <b>عدة فرق</b>.", "noTeamsForProj": "لا يوجد فريق GitLab للمشاريع المحددة. أضف واحداً يدوياً أدناه.",
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
      "tokenHint": "群组、项目或个人令牌（read_api 范围）。存储在服务器端，绝不向用户显示。",
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
      "selectAll": "全选",
      "deselectAll": "取消全选",
      "noProdLabels": "在所选项目中未找到 Prod:: 标签。",
      "refreshLabels": "刷新标签",
      "noProjForLabels": "请至少选择一个项目（项目步骤）以加载其标签。",
      "noLabelsFetched": "未获取到该项目的任何标签。请检查令牌是否具有 read_api 范围及项目访问权限。",
      "labelsNonProd": "个标签已获取，但没有任何属于 « Prod:: » 范围。阶段基于 « Prod::Xxx » 标签。您的标签：",
      "groupConn": "连接到群组", "groupToken": "令牌", "advanced": "高级选项",
      "advancedHint": "仅需为自托管的 GitLab 实例调整。默认值适用于 gitlab.com。",
      "adminSec": "管理员", "adminNone": "未识别", "adminOk": "管理员", "connecting": "正在连接到 GitLab…",
      "withGitlab": "使用 GitLab 登录", "connectedShort": "已连接",
      "requiredBoth": "必填字段 · 需成功连接到群组并识别出管理员后方可继续。",
      "scopeAll": "所有项目", "scopePer": "按项目",
      "scopeHintAll": "所有导入的项目使用相同的阶段和关联。",
      "scopeHintPer": "每个项目使用各自不同的阶段和关联。",
      "member": "名成员", "members": "名成员", "perProjectRecap": "按项目 · 各自独立的阶段和标签",
      "oauthStep1": "在 GitLab 上创建一个 OAuth 应用（下方的 Redirect URI，scope read_user），然后粘贴凭据。",
      "oauthOpenApps": "打开 GitLab 应用", "oauthSaveConnect": "保存并登录", "oauthReconfigure": "重新配置 OAuth", "oauthCurrent": "实例：",
      "prereq": "先决条件",
      "prereqText": "仅考虑Prod:: 标签。自定义您的阶段（名称、颜色、添加/移除），然后将您的标签链接到它们。",
      "phases": "阶段",
      "changeColor": "更改颜色",
      "deletePhase": "删除阶段",
      "addPhase": "添加阶段",
      "labelMapping": "标签映射",
      "notTracked": "未跟踪",
      "teamsIntro": "从 <b>GitLab组</b>导入的团队。<b>Lead</b> = 维护者 · <b>成员</b> = 开发者。一个成员可以属于 <b>多个团队</b>。", "noTeamsForProj": "所选项目没有 GitLab 团队。请在下方手动添加。",
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
      "tokenHint": "グループ、プロジェクト、または個人トークン（read_api スコープ）。サーバー側に保存され、ユーザーには表示されません。",
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
      "selectAll": "すべて選択",
      "deselectAll": "すべて選択解除",
      "noProdLabels": "選択したプロジェクトに Prod:: ラベルが見つかりません。",
      "refreshLabels": "ラベルを更新",
      "noProjForLabels": "ラベルを読み込むには、少なくとも1つのプロジェクトを選択してください（プロジェクト手順）。",
      "noLabelsFetched": "このプロジェクトのラベルを取得できませんでした。トークンに read_api スコープとプロジェクトへのアクセス権があるか確認してください。",
      "labelsNonProd": "個のラベルを取得しましたが、« Prod:: » スコープのものはありません。フェーズは « Prod::Xxx » ラベルに基づきます。あなたのラベル：",
      "groupConn": "グループへの接続", "groupToken": "トークン", "advanced": "詳細オプション",
      "advancedHint": "セルフホスト型のGitLabインスタンスの場合のみ調整してください。デフォルト値は gitlab.com に適しています。",
      "adminSec": "管理者", "adminNone": "未確認", "adminOk": "管理者", "connecting": "GitLabに接続中…",
      "withGitlab": "GitLabで接続", "connectedShort": "接続済み",
      "requiredBoth": "必須項目 · 続行するには、グループへの接続成功と管理者の確認が必要です。",
      "scopeAll": "すべてのプロジェクト", "scopePer": "プロジェクトごと",
      "scopeHintAll": "インポートしたすべてのプロジェクトに同じフェーズと関連付けを適用します。",
      "scopeHintPer": "プロジェクトごとに個別のフェーズと関連付け。",
      "member": "メンバー", "members": "メンバー", "perProjectRecap": "プロジェクトごと · フェーズ &amp; ラベルを個別に",
      "oauthStep1": "GitLab で OAuth アプリを作成し（下記の Redirect URI、scope read_user）、認証情報を貼り付けてください。",
      "oauthOpenApps": "GitLab アプリを開く", "oauthSaveConnect": "保存してサインイン", "oauthReconfigure": "OAuthを再設定", "oauthCurrent": "インスタンス：",
      "prereq": "前提条件",
      "prereqText": "Prod::ラベルのみが対象。フェーズをカスタマイズ（名前、色、追加/削除）してからラベルをリンク。",
      "phases": "フェーズ",
      "changeColor": "色を変更",
      "deletePhase": "フェーズを削除",
      "addPhase": "フェーズを追加",
      "labelMapping": "ラベルマッピング",
      "notTracked": "未追跡",
      "teamsIntro": "<b>GitLabグループ</b>からインポート済みチーム。<b>リード</b> = メンテナー · <b>メンバー</b> = 開発者。メンバーは<b>複数チーム</b>に属せます。", "noTeamsForProj": "選択したプロジェクトに対応する GitLab チームがありません。下から手動で追加してください。",
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
    test:'idle',projects:[],groups:[],importIds:[],labels:[],labelsDiag:[],labelsLoaded:false,labelPhase:{},
    phases:DEFAULT_PHASES.map(function(p){return {id:p.id,name:p.name,color:p.color};}),openColor:null,acc:'phases',
    phaseScope:'all',phaseProj:null,phasesByProject:{},labelPhaseByProject:{},
    acc1:['group','admin'],teamOpen:[],adminState:'idle',adminUser:null,oauthClientId:'__OAUTH_CLIENTID__',oauthSecret:'',oauthEdit:false,adminErr:'',
    teams:[],memberships:[],saving:false,saveErr:'',launching:false,progress:null};
  var app=document.getElementById('app');
  // Répertoire username→nom (reconstruit depuis les groupes renvoyés par /api/setup/test).
  var PEOPLE={};

  // Persistance localStorage : l'identification admin se fait par OAuth (aller-retour PLEINE PAGE) → on
  // restaure l'état du wizard au retour. Effacé après une mise en service réussie. (clé unique, même origine)
  // NB : 'acc1'/'acc'/'teamOpen' (états d'accordéon, UI) NON persistés → l'ouverture par défaut s'applique
  // toujours (sinon un ancien localStorage rouvrirait l'ancien état et masquerait le bouton admin).
  var PKEYS=['step','baseUrl','token','timeout','selfSigned','test','projects','groups','importIds','labels',
    'labelsDiag','labelsLoaded','labelPhase','phases','phaseScope','phaseProj','phasesByProject',
    'labelPhaseByProject','teams','memberships'];
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
    if(me&&me.authenticated){ST.adminState='connected';ST.adminUser={name:me.displayName||me.login||'',handle:'@'+(me.login||''),role:(me.role==='admin'?'Admin':(me.role||'GitLab'))};render();}
  }).catch(function(){});}

  function render(){
    if(ST.launching){app.innerHTML=launchHtml();return;}
    persistST();
    var _scEl=document.getElementById('body');var _scPos=_scEl?_scEl.scrollTop:0; // préserve le scroll : un clic ne doit pas remonter la vue
    var h='<div class="suA"><div class="suA-top"><div class="brand"><div class="mark">'+MARK+'</div><div><div class="bn">KPI</div><div class="bs">'+T.bs+'</div></div></div><div class="topright"><div class="count">'+T.stepOf.replace('{n}',ST.step+1)+'</div>'+LANG_SWITCH+'</div></div>';
    h+='<div class="step"><div class="stepper">';
    for(var i=0;i<STEP_META.length;i++){var st=i<ST.step?'done':i===ST.step?'cur':'';h+='<div class="node '+st+'" data-act="goto:'+i+'"><div class="dot">'+(i<ST.step?ic('check',16):(i+1))+'</div><div class="nl">'+STEP_META[i][0]+'</div></div>';if(i<STEP_META.length-1)h+='<div class="line'+(i<ST.step?' done':'')+'"></div>';}
    h+='</div></div><div class="body" id="body"><div class="bodyinner"><div class="card" style="max-width:'+[560,600,640,780,600][ST.step]+'px">'+head()+stepBody()+'</div></div></div>';
    h+='<div class="foot"><button class="btn ghost'+(ST.step===0?' disabled':'')+'" data-act="back">'+ic('chevL',16)+T.back+'</button>';
    if(ST.step===4)h+='<button class="btn primary'+(ST.saving?' disabled':'')+'" data-act="launch">'+(ST.saving?'<span class="spin"></span>'+T.saving:ic('rocket',16)+T.launch)+'</button>';
    else h+='<button class="btn primary'+(canNext()?'':' disabled')+'" data-act="next">'+T.continue+' '+ic('chevR',16)+'</button>';
    h+='</div></div>';
    app.innerHTML=h;
    var _scNew=document.getElementById('body');if(_scNew)_scNew.scrollTop=_scPos; // restaure la position (go() remet à 0 lors d'un changement d'étape)
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
    var gOpen=ST.acc1.indexOf('group')>=0,aOpen=ST.acc1.indexOf('admin')>=0;
    var chip=ST.test==='testing'?'<span class="chip neutral"><span class="spin"></span>'+T.testing+'</span>':ST.test==='ok'?'<span class="chip ok">'+ic('check',14)+T.connected+ST.projects.length+T.accessibleProjects+'</span>':ST.test==='err'?'<span class="chip err">'+T.testFailed+'</span>':'<span class="chip neutral">'+T.notTested+'</span>';
    var gPrev=ST.test==='ok'?'<span class="suA-acccount full">'+ic('check',12)+' '+T.connectedShort+'</span>':'<span class="suA-acccount">'+(ST.test==='err'?T.testFailed:T.notTested)+'</span>';
    var aConn=ST.adminState==='connected',au=ST.adminUser||{name:'',handle:'',role:''};
    var aPrev=aConn?'<span class="suA-acccount full">'+ic('check',12)+' '+esc(au.name)+'</span>':'<span class="suA-acccount">'+T.adminNone+'</span>';
    var h='<div class="suA-acc'+(gOpen?' open':'')+'"><button class="suA-acchead" data-act="acc1:group"><span class="ic">'+ic('server',16)+'</span><span class="suA-acct">'+T.groupConn+'</span><span class="suA-accprev">'+gPrev+'</span><span class="suA-accchev">'+ic('chevR',16)+'</span></button>';
    if(gOpen){
      h+='<div class="suA-accbody">'
        +'<div class="field"><div class="flabel">'+T.baseUrl+' <span class="req">*</span></div><div class="box">'+sic('server')+'<input data-field="baseUrl" placeholder="https://gitlab.exemple.com" value="'+esc(ST.baseUrl)+'"></div></div>'
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
        var redir=location.origin+'/signin-gitlab';
        var bu=(ST.baseUrl||'').replace(/\/+$/,'');
        var appsUrl=bu+'/-/profile/applications';
        var reconf=OAUTHOK&&ST.oauthEdit; // reconfiguration : secret optionnel (conservé si vide)
        h+='<div class="suA-oauthsetup">'
          // Champ instance EXPLICITE : l'Authority OAuth ne peut plus retomber silencieusement sur gitlab.com.
          +'<div class="field"><div class="flabel">'+T.baseUrl+' <span class="req">*</span>'+info(T.advancedHint)+'</div><div class="box">'+sic('server')+'<input data-field="baseUrl" placeholder="https://gitlab.exemple.com" value="'+esc(ST.baseUrl)+'"></div></div>'
          +'<div class="suA-oauthstep"><span class="suA-oauthnum">1</span><div>'+T.oauthStep1+'<div class="suA-oauthredir">Redirect URI : <code>'+esc(redir)+'</code> · scope <code>read_user</code></div>'+(bu?'<a class="suA-oauthlink" href="'+esc(appsUrl)+'" target="_blank" rel="noopener">'+T.oauthOpenApps+' '+ic('arrow',13)+'</a>':'')+'</div></div>'
          +'<div class="field"><div class="flabel">Application ID <span class="req">*</span></div><div class="box">'+sic('key')+'<input data-field="oauthClientId" value="'+esc(ST.oauthClientId)+'"></div></div>'
          +'<div class="field"><div class="flabel">Secret '+(reconf?'':'<span class="req">*</span>')+'</div><div class="box">'+sic('key')+'<input data-field="oauthSecret" type="password"'+(reconf?' placeholder="••••••••"':'')+' value="'+esc(ST.oauthSecret)+'"></div></div>';
        if(ST.adminErr)h+='<div class="note" style="background:var(--bad-soft);border-left-color:var(--bad)"><span class="ic" style="color:var(--bad)">'+ic('info',16)+'</span><div>'+esc(ST.adminErr)+'</div></div>';
        h+='<button class="suA-glbtn'+(ST.adminState==='connecting'?' busy':'')+'" data-act="oauthsave"'+(ST.adminState==='connecting'?' disabled':'')+'>'+(ST.adminState==='connecting'?'<span class="spin"></span>'+T.connecting:'<span class="suA-glmark">'+GITLAB_MARK+'</span>'+T.oauthSaveConnect)+'</button>';
        if(reconf)h+='<button class="btn ghost sm" data-act="oauthcancel" style="margin-top:6px">'+T.cancel+'</button>';
        h+='</div>';
      } else {
        var curInst=(ST.baseUrl||'').replace(/^https?:\/\//,'').replace(/\/+$/,'');
        h+='<div class="suA-adminrow"><button class="suA-glbtn'+(ST.adminState==='connecting'?' busy':'')+'" data-act="oauth"'+(ST.adminState==='connecting'?' disabled':'')+'>'+(ST.adminState==='connecting'?'<span class="spin"></span>'+T.connecting:'<span class="suA-glmark">'+GITLAB_MARK+'</span>'+T.withGitlab)+'</button></div>';
        // Instance courante visible + reconfiguration accessible (corriger une Authority erronée sans redémarrer).
        h+='<div class="suA-oauthredir" style="margin-top:8px;display:flex;align-items:center;gap:8px;flex-wrap:wrap">'+T.oauthCurrent+' <code>'+esc(curInst||'—')+'</code><button class="suA-adminchange" data-act="oauthedit">'+T.oauthReconfigure+'</button></div>';
      }
      h+='</div>';
    }
    h+='</div><div class="req" style="margin-top:8px"><span class="r">*</span> '+T.requiredBoth+'</div>';
    return h;
  }
  function s1(){
    var allOn=ST.projects.length>0&&ST.importIds.length===ST.projects.length;
    var h='<div class="note">'+sic('info')+'<div><b>'+ST.importIds.length+'</b>'+T.projSelectedOf+ST.projects.length+T.accessible+'</div></div>';
    if(ST.projects.length)h+='<div class="suA-selall"><button class="suA-selallbtn" data-act="toggleall">'+ic(allOn?'x':'check',14)+(allOn?T.deselectAll:T.selectAll)+'</button><span class="suA-selcount">'+ST.importIds.length+' / '+ST.projects.length+'</span></div>';
    h+='<div class="checklist">';
    for(var i=0;i<ST.projects.length;i++){var p=ST.projects[i];var on=ST.importIds.indexOf(p.id)>=0;h+='<button class="chk'+(on?' on':'')+'" data-act="proj:'+p.id+'"><span class="chkbox">'+(on?ic('check',13):'')+'</span><span class="chkl">'+esc(p.name)+'<b>#'+p.id+'</b></span><span class="grp">'+esc(p.group||'')+'</span></button>';}
    return h+'</div>';
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
      h+='</div><button class="btn outline sm" style="align-self:flex-start" data-act="phadd">'+ic('plus',16)+T.addPhase+'</button></div>';
    }
    h+='</div>';
    // Accordéon 2 — Association des labels (scope Prod::). Aperçu = compteur N/M liés.
    var lOpen=ST.acc==='labels';
    h+='<div class="suA-acc'+(lOpen?' open':'')+'"><button class="suA-acchead" data-act="acc:labels"><span class="ic">'+ic('link',16)+'</span><span class="suA-acct">'+T.labelMapping+'</span><span class="suA-accprev"><span class="suA-acccount'+(prod.length&&mapped===prod.length?' full':'')+'">'+mapped+' / '+prod.length+'</span></span><span class="suA-accchev">'+ic('chevR',16)+'</span></button>';
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
          h+='<div class="maprow'+(phv==='none'?' unset':'')+'"><span class="dot2" style="background:'+phaseColor(phv)+'"></span><span class="mlabel">'+esc(ll)+'</span><span class="arrow">'+ic('arrow',15)+'</span>'+miniSel('phase:'+ST.labels.indexOf(ll),phv,phOpts)+'</div>';}
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
        for(var j=0;j<mem.length;j++){var m=mem[j];var nm=POOLname(m.pid);h+='<div class="mrow">'+av(m.pid,nm,24)+'<span class="mname">'+esc(nm)+'</span>'+miniSel('role:'+m.pid+'~'+tm.id,m.role,roleOpts)+'<button class="mx" data-act="rmmem:'+m.pid+'~'+tm.id+'">×</button></div>';}
        var avail=allPeople().filter(function(pid){return !mem.some(function(m){return m.pid===pid;});});
        if(avail.length){h+='<div class="addsel">'+ic('plus',14)+'<select data-add="'+tm.id+'"><option value="">'+T.addMember+'</option>';for(var a=0;a<avail.length;a++)h+='<option value="'+esc(avail[a])+'">'+esc(POOLname(avail[a]))+'</option>';h+='</select></div>';}
        h+='</div>';
      }
      h+='</div>';
    }
    return h+'</div><button class="btn outline sm" style="align-self:flex-start;margin-top:12px" data-act="addteam">'+ic('plus',16)+T.newTeamName+'</button>';
  }
  function s4(){
    var imp=ST.projects.filter(function(p){return ST.importIds.indexOf(p.id)>=0;}).map(function(p){return p.name;});
    var mapped=0;for(var k in ST.labelPhase)if(ST.labelPhase[k]&&ST.labelPhase[k]!=='none')mapped++;
    var _vt=visibleTeams();var _vid={};_vt.forEach(function(t){_vid[t.id]=1;});
    var ppl={};ST.memberships.forEach(function(m){if(_vid[m.teamId])ppl[m.pid]=1;});
    var phVal=ST.phaseScope==='per'?T.perProjectRecap:(mapped+T.labelsLinked);
    var rows=[['link',T.stepConnexion,ST.baseUrl.replace(/^https?:\/\//,''),0],['box',T.stepProjets,imp.length?imp.join(', '):T.none,1],['layers',T.stepPhases,phVal,2],['users',T.stepEquipes,_vt.length+T.teamsCount+Object.keys(ppl).length+T.peopleCount,3]];
    var h='<div class="recap">';
    for(var i=0;i<rows.length;i++)h+='<div class="rrow"><span class="ric">'+ic(rows[i][0],15)+'</span><div class="rk">'+rows[i][1]+'</div><div class="rv">'+esc(rows[i][2])+'</div><button class="redit" data-act="goto:'+rows[i][3]+'">'+T.edit+'</button></div>';
    h+='</div>';
    if(ST.saveErr)h+='<div class="note" style="background:var(--bad-soft);border-left-color:var(--bad)"><span class="ic" style="color:var(--bad)">'+ic('info',16)+'</span><div>'+esc(ST.saveErr)+'</div></div>';
    return h;
  }
  function sic(n){return '<span class="ic">'+ic(n,16)+'</span>';}
  // Bulle d'aide « i » (au survol/focus) — remplace les sous-textes permanents.
  function info(t){return '<span class="su-info" tabindex="0"><span class="su-info-i">i</span><span class="su-info-pop">'+t+'</span></span>';}
  function miniSel(act,val,opts){var h='<div class="mini"><select data-sel="'+act+'">';for(var i=0;i<opts.length;i++)h+='<option value="'+esc(opts[i][0])+'"'+(opts[i][0]===val?' selected':'')+'>'+esc(opts[i][1])+'</option>';return h+'</select><span class="ic">'+ic('chevD',13)+'</span></div>';}

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
    ST.adminState='connecting';ST.adminErr='';render();
    fetch('/api/setup/oauth',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({clientId:(ST.oauthClientId||'').trim(),clientSecret:(ST.oauthSecret||'').trim(),authority:conn().baseUrl,selfSigned:ST.selfSigned})})
      .then(function(r){return r.json();}).then(function(j){
        if(j.ok){persistST();window.location.href='/auth/oauth?return=/setup';}
        else{ST.adminState='idle';ST.adminErr=j.error||T.saveImpossible;render();}
      }).catch(function(){ST.adminState='idle';ST.adminErr=T.serverUnreachable;render();});
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
    function perPayload(arr){return arr.map(function(p){return {key:p.id,name:p.name,color:p.color,timed:p.id!=='uiux'};});}
    var payload={baseUrl:conn().baseUrl,token:conn().token,selfSigned:ST.selfSigned,timeout:conn().timeout,
      admins:[],
      projectIds:ST.importIds,labelPhases:ST.labelPhase,periods:perPayload(ST.phases),
      teams:visibleTeams().map(function(t){return {name:t.name,members:ST.memberships.filter(function(m){return m.teamId===t.id;}).map(function(m){return {username:m.pid,role:m.role};})};})};
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
    var heights=[40,62,52,86,66,96],cols=['#2b7fff','#1f9d6b','#fc6d26','#4d97ff','#1f9d6b','#2b7fff'],bars='';
    for(var i=0;i<heights.length;i++)bars+='<i><b style="height:'+Math.round(pct*heights[i]/100)+'%;background:'+cols[i]+'"></b></i>';
    var eta=(pr.etaSeconds!=null&&!done&&!err)?' · ~ '+fmtClock(pr.etaSeconds)+T.left:'';
    var status=done?('<span class="okic">'+ic('check',17)+'</span> '+T.done)
      :err?(T.failed+esc(pr.error||pr.message||''))
      :('<span class="spin"></span> '+esc(pr.message||T.extractingShort)+'<span class="ld-dots"><b></b><b></b><b></b></span>');
    return '<div class="suA"><div class="suA-top"><div class="brand"><div class="mark">'+MARK+'</div><div><div class="bn">KPI</div><div class="bs">'+T.bs+'</div></div></div><div class="topright">'+LANG_SWITCH+'</div></div>'
      +'<div class="ld-wrap"><div class="ld-bars">'+bars+'</div>'
      +'<div class="ld-pct">'+pct+'<span>%</span></div>'
      +'<div class="ld-status'+(err?' err':'')+'">'+status+'</div>'
      +'<div class="ld-meta">'+(err?'':T.extracting+eta)+'</div>'
      +(done?'':'<button class="btn '+(err?'outline':'ghost')+' sm" data-act="cancelLaunch">'+(err?T.backToConfig:ic('chevL',15)+T.cancel)+'</button>')
      +'</div></div>';
  }
  function toTop(){var b=document.getElementById('body');if(b)b.scrollTop=0;}
  function go(n){ if(n>ST.step && !canNext())return; if(n===2 && !ST.labelsLoaded){ST.step=2;render();toTop();loadLabels(function(){render();toTop();});return;} ST.step=n; render(); toTop(); }

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
    else if(a.indexOf('acc1:')===0){var k1=a.slice(5);var i1=ST.acc1.indexOf(k1);if(i1>=0)ST.acc1.splice(i1,1);else ST.acc1.push(k1);render();}
    else if(a.indexOf('scope:')===0){switchScope(a.slice(6));}
    else if(a.indexOf('projtab:')===0){ST.phaseProj=+a.slice(8);render();}
    else if(a==='oauth'){persistST();ST.adminState='connecting';render();window.location.href='/auth/oauth?return=/setup';}
    else if(a==='oauthsave'){doOauthSave();}
    else if(a==='oauthedit'){ST.oauthEdit=true;ST.adminErr='';render();}
    else if(a==='oauthcancel'){ST.oauthEdit=false;ST.adminErr='';render();}
    else if(a==='adminchange'){persistST();window.location.href='/logout?return=/setup';} // déconnecte la session app → réaffiche « Se connecter avec GitLab » (sinon GitLab ré-approuve le même compte)
    else if(a.indexOf('team:')===0){if(e.target.closest('input,select'))return;var tid2=a.slice(5);var k2=ST.teamOpen.indexOf(tid2);if(k2>=0)ST.teamOpen.splice(k2,1);else ST.teamOpen.push(tid2);render();}
  });
  app.addEventListener('input',function(e){
    var f=e.target.closest('[data-field]');if(f){ST[f.dataset.field]=f.value;if(f.dataset.field==='baseUrl'||f.dataset.field==='token')ST.test='idle';return;}
    var tn=e.target.closest('[data-team]');if(tn){var _t=ST.teams.filter(function(x){return x.id===tn.dataset.team;})[0];if(_t)_t.name=tn.value;return;}
    var pn=e.target.closest('[data-phname]');if(pn){var pid=pn.dataset.phname;ensurePer();curPhases().forEach(function(p){if(p.id===pid)p.name=pn.value;});}
  });
  app.addEventListener('change',function(e){
    var sl=e.target.closest('[data-setlang]');if(sl){location.href='/set-lang?lang='+encodeURIComponent(sl.value)+'&return=/setup';return;}
    var s=e.target.closest('[data-sel]');if(s){var a=s.dataset.sel;
      if(a.indexOf('phase:')===0){var i=+a.slice(6);setMapVal(ST.labels[i],s.value);render();}
      else if(a.indexOf('role:')===0){var pr=a.slice(5).split('~');ST.memberships.forEach(function(m){if(m.pid===pr[0]&&m.teamId===pr[1])m.role=s.value;});render();}
      return;}
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
