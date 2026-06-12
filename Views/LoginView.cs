// LoginView.cs — designed login + welcome pages for the KPI dashboard.
//
// Drop this file in Kpi/Views/ (namespace Kpi.Views).
// It returns self-contained HTML strings (inline CSS+JS, CSP-safe):
//   • no external JS (only Google Fonts, already allowed by the dashboard CSP);
//   • the page talks ONLY to same-origin endpoints (connect-src 'self').
//
// Wire it up in WebDashboard.cs — see WebDashboard.auth.patch.md.
//
// Security model (matches the "rien stocké" requirement):
//   • OAuth button  → GET /auth/oauth  → existing AddOAuth("gitlab") challenge.
//   • Token path    → POST /api/auth/token → server validates the PAT against
//     {instance}/api/v4/user, then signs a cookie with ONLY the username.
//     The personal access token is used once and never persisted.
//
using System.Text;
using Kpi.Config;

namespace Kpi.Views;

public static class LoginView
{
    /// <summary>Designed login page. OAuth button shown only when OAuth is configured. Bilingue FR/EN.</summary>
    public static string Page(AuthConfig auth, string culture = "fr")
    {
        var defaultInstance = !string.IsNullOrWhiteSpace(auth.Authority) ? auth.Authority.TrimEnd('/') : "https://gitlab.com";
        var en = culture == "en";
        string T(string k) => Kpi.Localization.Loc.T(en ? "en" : "fr", k);
        // Chaînes utilisées par le <script> inline (échappées en JSON valide, sûres en <script>).
        var jsI18n = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["encryptedTo"] = T("login.encryptedTo"), ["encrypted"] = T("login.encrypted"),
            ["errNoInstance"] = T("login.errNoInstance"), ["errNoToken"] = T("login.errNoToken"),
            ["verifying"] = T("login.verifying"), ["signin"] = T("login.signin"),
            ["cantConnect"] = T("login.cantConnect"), ["unreachable"] = T("login.unreachable"),
        });
        return Html
            .Replace("__OAUTH__", auth.OAuthConfigured ? "true" : "false")
            .Replace("__DEFAULT_INSTANCE__", HtmlAttr(defaultInstance))
            .Replace("__LANG__", en ? "en" : "fr")
            .Replace("__JS_I18N__", jsI18n)
            .Replace("__SW_FR__", en ? "" : "on").Replace("__SW_EN__", en ? "on" : "")
            .Replace("__T_TAG1__", T("login.tag1")).Replace("__T_TAG2__", T("login.tag2"))
            .Replace("__T_DESC__", T("login.desc")).Replace("__T_ENCRYPTED__", T("login.encrypted"))
            .Replace("__T_WELCOME__", T("login.welcome")).Replace("__T_WELCOMESUB__", T("login.welcomeSub"))
            .Replace("__T_INSTANCE__", T("login.instance")).Replace("__T_WITHGITLAB__", T("login.withGitlab"))
            .Replace("__T_USETOKEN__", T("login.useToken")).Replace("__T_TOKENPERSONAL__", T("login.tokenPersonal"))
            .Replace("__T_SIGNIN__", T("login.signin")).Replace("__T_TOKENNOTE__", T("login.tokenNote"));
    }

    /// <summary>Blank success page with a logout button.</summary>
    public static string Welcome(string? username)
        => WelcomeHtml.Replace("__USER__", HtmlAttr(username ?? ""));

    private static string HtmlAttr(string s) =>
        (s ?? "").Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

    // ---------------------------------------------------------------- login
    private const string Html = """
<!DOCTYPE html>
<html lang="__LANG__">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Connexion · KPI</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&family=IBM+Plex+Mono:wght@400;500;600&display=swap" rel="stylesheet">
<style>
:root{
  --bg:#0a0e13; --panel:#141a22; --panel-2:#1b232d; --panel-3:#222c37;
  --ink:#e9eef4; --ink-dim:#9aa6b6; --ink-faint:#5f6b7a;
  --line:#222c37; --line-2:#1b232d;
  --accent:#2b7fff; --accent-soft:rgba(43,127,255,.16); --accent-2:#4d97ff;
  --c-good:#1f9d6b; --c-bad:#ff6f63; --c-bad-soft:rgba(255,111,99,.14); --c-done:#0072B2;
  --gl-orange:#fc6d26; --gl-orange-2:#e24329;
  --disp:'Space Grotesk',system-ui,sans-serif; --sans:system-ui,'Segoe UI',sans-serif;
  --mono:'IBM Plex Mono',ui-monospace,monospace; --ease-out:cubic-bezier(.2,0,0,1);
}
*{box-sizing:border-box;}
html,body{margin:0;height:100%;}
body{background:var(--bg);color:var(--ink);font-family:var(--sans);}
.auth{position:fixed;inset:0;display:flex;}
.split{display:flex;width:100%;}
.split-brand{flex:1;background:linear-gradient(155deg,#10151c,#0a0e13);position:relative;padding:48px 44px;display:flex;flex-direction:column;justify-content:space-between;overflow:hidden;}
.bgrid{position:absolute;inset:0;opacity:.5;background:linear-gradient(var(--line) 1px,transparent 1px),linear-gradient(90deg,var(--line) 1px,transparent 1px);background-size:34px 34px;mask-image:radial-gradient(circle at 30% 30%,#000,transparent 75%);}
.split-form{flex:none;width:480px;background:var(--panel);display:flex;align-items:center;justify-content:center;padding:40px;border-left:1px solid var(--line);}
.form-inner{width:100%;max-width:360px;}
.lang-switch{display:flex;justify-content:flex-end;gap:6px;align-items:center;font-size:12px;margin-bottom:12px;color:var(--ink-faint);}
.lang-switch a{color:var(--ink-faint);text-decoration:none;padding:2px 7px;border-radius:7px;}
.lang-switch a.on{color:var(--ink);background:var(--panel-2);font-weight:600;}
.lang-switch a:hover{color:var(--ink);}
.brand{display:flex;align-items:center;gap:11px;position:relative;}
.brand-mark{width:38px;height:38px;border-radius:11px;background:var(--accent);display:flex;align-items:center;justify-content:center;flex:none;}
.brand-name{font-family:var(--disp);font-weight:700;font-size:17px;letter-spacing:-.01em;line-height:1;}
.brand-sub{font-size:11px;color:var(--ink-faint);margin-top:3px;}
.split-tag{font-family:var(--disp);font-weight:700;font-size:32px;line-height:1.18;letter-spacing:-.02em;position:relative;max-width:420px;}
.split-tag em{color:var(--accent);font-style:normal;}
.split-desc{position:relative;font-size:14px;color:var(--ink-dim);line-height:1.6;max-width:380px;margin-top:16px;}
.bars{position:relative;display:flex;align-items:flex-end;gap:10px;height:120px;margin-top:22px;max-width:360px;}
.bars i{flex:1;height:100%;border-radius:6px 6px 0 0;background:var(--panel-2);position:relative;overflow:hidden;}
.bars i b{position:absolute;left:0;right:0;bottom:0;display:block;border-radius:6px 6px 0 0;transform-origin:bottom;animation:barGrow .55s var(--ease-out) both,barWave 2.8s ease-in-out infinite;}
.bars i:nth-child(1) b{animation-delay:0s,.55s;}.bars i:nth-child(2) b{animation-delay:.06s,.71s;}
.bars i:nth-child(3) b{animation-delay:.12s,.87s;}.bars i:nth-child(4) b{animation-delay:.18s,1.03s;}
.bars i:nth-child(5) b{animation-delay:.24s,1.19s;}.bars i:nth-child(6) b{animation-delay:.30s,1.35s;}
.bars i:nth-child(7) b{animation-delay:.36s,1.51s;}
@keyframes barGrow{from{transform:scaleY(0);}to{transform:scaleY(1);}}
@keyframes barWave{0%,100%{transform:scaleY(1);}45%{transform:scaleY(.8);}}
@media (prefers-reduced-motion: reduce){.bars i b{animation:none;}}
.brand-foot{position:relative;font-size:11.5px;color:var(--ink-faint);display:flex;align-items:center;gap:7px;}
.a-title{font-family:var(--disp);font-weight:700;font-size:25px;letter-spacing:-.02em;margin:0 0 7px;}
.a-sub{font-size:13.5px;line-height:1.5;color:var(--ink-dim);margin:0 0 22px;}
.gl-btn{display:flex;align-items:center;justify-content:center;gap:11px;width:100%;height:52px;border:0;border-radius:13px;cursor:pointer;font-family:var(--disp);font-weight:600;font-size:15.5px;color:#fff;background:linear-gradient(180deg,#fc6d26,#e24329);box-shadow:0 6px 18px rgba(226,67,41,.32);transition:transform .12s var(--ease-out),box-shadow .12s,filter .12s;}
.gl-btn:hover{filter:brightness(1.05);transform:translateY(-1px);}
.gl-btn:disabled{cursor:default;filter:saturate(.4) brightness(.8);transform:none;box-shadow:none;}
.reveal-link{display:block;width:100%;text-align:center;border:0;background:none;cursor:pointer;color:var(--ink-dim);font-size:13px;padding:14px;margin-top:2px;}
.reveal-link:hover{color:var(--ink);}
.collapse{overflow:hidden;transition:max-height .3s var(--ease-out),opacity .25s;}
.a-or{display:flex;align-items:center;gap:12px;margin:18px 0;color:var(--ink-faint);font-size:10.5px;font-weight:600;letter-spacing:.1em;text-transform:uppercase;}
.a-or::before,.a-or::after{content:'';flex:1;height:1px;background:var(--line);}
.fld{display:flex;flex-direction:column;gap:7px;margin-bottom:16px;}
.fld-l{font-size:12px;font-weight:600;color:var(--ink-dim);}
.fld-box{display:flex;align-items:center;gap:10px;height:48px;padding:0 13px;background:var(--panel-2);border:1.5px solid var(--line);border-radius:12px;transition:border-color .14s,box-shadow .14s,background .14s;}
.fld-box .ico{color:var(--ink-faint);flex:none;display:flex;}
.fld-box input{flex:1;min-width:0;border:0;background:none;outline:none;color:var(--ink);font-family:var(--mono);font-size:13.5px;letter-spacing:.02em;}
.fld-box input::placeholder{color:var(--ink-faint);font-family:var(--sans);letter-spacing:0;}
.fld-box .eye{border:0;background:none;cursor:pointer;color:var(--ink-faint);display:flex;padding:4px;border-radius:6px;}
.fld-box .eye:hover{color:var(--ink-dim);}
.fld-box:focus-within{border-color:var(--accent);background:var(--panel);box-shadow:0 0 0 4px var(--accent-soft);}
.fld-box:focus-within .ico{color:var(--accent);}
.fld-box.err{border-color:var(--c-bad);box-shadow:0 0 0 4px var(--c-bad-soft);}
.sub-btn{display:flex;align-items:center;justify-content:center;gap:9px;width:100%;height:50px;border:0;border-radius:13px;cursor:pointer;font-family:var(--disp);font-weight:600;font-size:15px;color:var(--ink);background:var(--panel-2);border:1px solid var(--line);transition:background .12s,border-color .12s,transform .12s;}
.sub-btn:hover{background:var(--panel-3);border-color:var(--accent);transform:translateY(-1px);}
.sub-btn:disabled{cursor:default;filter:saturate(.5) brightness(.85);transform:none;}
.a-err{display:flex;align-items:flex-start;gap:9px;padding:11px 13px;border-radius:11px;background:var(--c-bad-soft);border-left:3px solid var(--c-bad);color:#ffd9d4;font-size:12.5px;line-height:1.45;margin-bottom:16px;}
.a-err svg{flex:none;margin-top:1px;color:var(--c-bad);}
.hidden{display:none !important;}
.spin{width:18px;height:18px;border:2.5px solid rgba(255,255,255,.35);border-top-color:#fff;border-radius:50%;animation:spin .7s linear infinite;}
.sub-btn .spin{border-color:var(--line-2);border-top-color:var(--ink);}
@keyframes spin{to{transform:rotate(360deg);}}
.a-foot{margin-top:22px;text-align:center;font-size:11.5px;color:var(--ink-faint);line-height:1.5;display:flex;align-items:center;justify-content:center;gap:6px;}
@media (max-width: 860px){
  .split{flex-direction:column;}
  .split-brand{flex:none;padding:34px 24px 24px;}
  .split-brand .split-desc{display:none;}
  .bars{height:74px;margin-top:18px;}
  .split-form{width:100%;flex:1;border-left:0;border-top:1px solid var(--line);align-items:flex-start;padding:28px 24px 36px;}
  .form-inner{max-width:460px;margin:0 auto;}
  .split-tag{font-size:26px;}
}
</style>
</head>
<body>
<div class="auth"><div class="split">
  <div class="split-brand">
    <div class="bgrid"></div>
    <div class="brand">
      <div class="brand-mark">
        <svg width="22" height="22" viewBox="0 0 24 24" aria-hidden="true">
          <rect x="3" y="13" width="4.2" height="7" rx="1.4" fill="#fff" opacity="0.82"></rect>
          <rect x="9.9" y="9" width="4.2" height="11" rx="1.4" fill="#fff" opacity="0.92"></rect>
          <rect x="16.8" y="4.5" width="4.2" height="15.5" rx="1.4" fill="#fff"></rect>
          <path d="M4 8.5 L11 6 L19 2.5" fill="none" stroke="#fff" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" opacity="0.9"></path>
          <circle cx="19" cy="2.5" r="1.7" fill="#fff"></circle>
        </svg>
      </div>
      <div><div class="brand-name">KPI</div><div class="brand-sub">Knowledge, Progress &amp; Impact</div></div>
    </div>
    <div>
      <div class="split-tag">__T_TAG1__<br><em>__T_TAG2__</em></div>
      <div class="bars" aria-hidden="true">
        <i><b style="height:42%;background:var(--accent)"></b></i>
        <i><b style="height:68%;background:var(--c-done)"></b></i>
        <i><b style="height:55%;background:var(--gl-orange)"></b></i>
        <i><b style="height:88%;background:var(--accent)"></b></i>
        <i><b style="height:73%;background:var(--c-done)"></b></i>
        <i><b style="height:96%;background:var(--gl-orange)"></b></i>
        <i><b style="height:61%;background:var(--accent)"></b></i>
      </div>
      <div class="split-desc">__T_DESC__</div>
    </div>
    <div class="brand-foot">
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="11" width="16" height="9" rx="2"></rect><path d="M8 11V7a4 4 0 0 1 8 0v4"></path></svg>
      <span id="footInstance">__T_ENCRYPTED__</span>
    </div>
  </div>
  <div class="split-form"><div class="form-inner">
    <div class="lang-switch"><a href="/set-lang?lang=fr&amp;return=/login" class="__SW_FR__">FR</a><span>·</span><a href="/set-lang?lang=en&amp;return=/login" class="__SW_EN__">EN</a></div>
    <h1 class="a-title">__T_WELCOME__</h1>
    <p class="a-sub">__T_WELCOMESUB__</p>
    <div class="fld">
      <label class="fld-l" for="instance">__T_INSTANCE__</label>
      <div class="fld-box">
        <span class="ico"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="7" rx="2"></rect><rect x="3" y="13" width="18" height="7" rx="2"></rect><path d="M7 7.5h.01M7 16.5h.01"></path></svg></span>
        <input id="instance" type="text" inputmode="url" autocomplete="url" spellcheck="false" placeholder="https://gitlab.exemple.com" value="__DEFAULT_INSTANCE__">
      </div>
    </div>
    <button class="gl-btn" id="oauthBtn" type="button">
      <span id="oauthIcon"><svg width="22" height="22" viewBox="0 0 380 380" aria-hidden="true"><path fill="#fff" opacity="0.95" d="M190 366 L259 154 H121 Z"></path><path fill="#fff" opacity="0.7" d="M190 366 L121 154 H24 Z"></path><path fill="#fff" opacity="0.5" d="M24 154 L3 219 a14 14 0 0 0 5 16 l182 131 Z"></path><path fill="#fff" opacity="0.7" d="M24 154 L53 64 a7 7 0 0 1 13 0 l55 90 Z"></path><path fill="#fff" opacity="0.95" d="M190 366 L259 154 h97 Z"></path><path fill="#fff" opacity="0.5" d="M356 154 l21 65 a14 14 0 0 1 -5 16 L190 366 Z"></path><path fill="#fff" opacity="0.7" d="M356 154 L327 64 a7 7 0 0 0 -13 0 l-55 90 Z"></path></svg></span>
      <span id="oauthLabel">__T_WITHGITLAB__</span>
    </button>
    <button class="reveal-link" id="revealBtn" type="button">__T_USETOKEN__</button>
    <div class="collapse" id="tokenWrap" style="max-height:0;opacity:0">
      <div class="a-or">__T_TOKENPERSONAL__</div>
      <div class="a-err hidden" id="errBox">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"></circle><path d="M12 8v5M12 16h.01"></path></svg>
        <span id="errMsg"></span>
      </div>
      <div class="fld"><div class="fld-box" id="tokenBox">
        <span class="ico"><svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="7.5" cy="15.5" r="3.5"></circle><path d="M10 13 21 2M18 5l2.5 2.5M15.5 7.5L18 10"></path></svg></span>
        <input id="token" type="password" spellcheck="false" placeholder="glpat-xxxxxxxxxxxxxxxxxxxx" autocomplete="off" aria-label="Token d'accès personnel GitLab">
        <button class="eye" id="eyeBtn" type="button" title="Afficher / masquer" aria-label="Afficher ou masquer le token"><svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z"></path><circle cx="12" cy="12" r="3"></circle></svg></button>
      </div></div>
      <button class="sub-btn" id="tokenBtn" type="button">__T_SIGNIN__</button>
      <div class="a-foot">__T_TOKENNOTE__</div>
    </div>
  </div></div>
</div></div>
<script>
(function(){
  var OAUTH_CONFIGURED = __OAUTH__;
  var I18N = __JS_I18N__;
  var $ = function(id){return document.getElementById(id);};
  var instance=$('instance'),token=$('token'),tokenWrap=$('tokenWrap'),oauthBtn=$('oauthBtn'),revealBtn=$('revealBtn'),tokenBtn=$('tokenBtn'),errBox=$('errBox'),errMsg=$('errMsg'),tokenBox=$('tokenBox'),footInstance=$('footInstance');
  function normInstance(){var v=(instance.value||'').trim().replace(/\/+$/,'');if(v&&!/^https?:\/\//i.test(v))v='https://'+v;return v;}
  function syncFoot(){var v=normInstance();footInstance.textContent=v?(I18N.encryptedTo+v.replace(/^https?:\/\//,'')):I18N.encrypted;}
  instance.addEventListener('input',syncFoot);syncFoot();
  function openToken(){tokenWrap.style.maxHeight='360px';tokenWrap.style.opacity='1';revealBtn.classList.add('hidden');setTimeout(function(){token.focus();},120);}
  revealBtn.addEventListener('click',openToken);
  if(!OAUTH_CONFIGURED){oauthBtn.classList.add('hidden');revealBtn.classList.add('hidden');openToken();}
  $('eyeBtn').addEventListener('click',function(){token.type=token.type==='password'?'text':'password';});
  function showError(m){errMsg.textContent=m;errBox.classList.remove('hidden');tokenBox.classList.add('err');}
  function clearError(){errBox.classList.add('hidden');tokenBox.classList.remove('err');}
  token.addEventListener('input',clearError);
  oauthBtn.addEventListener('click',function(){window.location.href='/auth/oauth';});
  function resetTokenBtn(){tokenBtn.disabled=false;tokenBtn.textContent=I18N.signin;}
  tokenBtn.addEventListener('click',function(){
    clearError();
    var inst=normInstance(),tok=(token.value||'').trim();
    if(!inst){showError(I18N.errNoInstance);return;}
    if(!tok){showError(I18N.errNoToken);return;}
    tokenBtn.disabled=true;tokenBtn.innerHTML='<span class="spin"></span> '+I18N.verifying;
    fetch('/api/auth/token',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({instance:inst,token:tok})})
      .then(function(r){return r.json().then(function(j){return {ok:r.ok,j:j};});})
      .then(function(res){if(res.ok&&res.j&&res.j.ok){window.location.href='/';}else{resetTokenBtn();showError((res.j&&res.j.error)||I18N.cantConnect);}})
      .catch(function(){resetTokenBtn();showError(I18N.unreachable);});
  });
  token.addEventListener('keydown',function(e){if(e.key==='Enter')tokenBtn.click();});
})();
</script>
</body>
</html>
""";

    // -------------------------------------------------------------- welcome
    private const string WelcomeHtml = """
<!DOCTYPE html>
<html lang="fr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Connecté · KPI</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&display=swap" rel="stylesheet">
<style>
:root{--bg:#0a0e13;--panel:#141a22;--line:#222c37;--ink:#e9eef4;--ink-dim:#9aa6b6;--ink-faint:#5f6b7a;--accent:#2b7fff;--c-good:#1f9d6b;--c-good-soft:rgba(31,157,107,.16);--disp:'Space Grotesk',system-ui,sans-serif;--sans:system-ui,'Segoe UI',sans-serif;--mono:ui-monospace,monospace;}
*{box-sizing:border-box;}
html,body{margin:0;height:100%;}
body{background:var(--bg);color:var(--ink);font-family:var(--sans);display:flex;align-items:center;justify-content:center;}
.card{width:min(92vw,420px);text-align:center;padding:8px;}
.ring{width:78px;height:78px;border-radius:50%;background:var(--c-good-soft);color:var(--c-good);display:flex;align-items:center;justify-content:center;margin:0 auto 22px;}
h1{font-family:var(--disp);font-weight:700;font-size:24px;letter-spacing:-.02em;margin:0 0 9px;}
p{font-size:14px;line-height:1.55;color:var(--ink-dim);margin:0 0 4px;}
.who{font-family:var(--mono);color:var(--ink);}
.logout{display:inline-flex;align-items:center;gap:9px;margin-top:28px;height:46px;padding:0 22px;border:1px solid var(--line);background:var(--panel);color:var(--ink);border-radius:12px;font-family:var(--disp);font-weight:600;font-size:14px;text-decoration:none;cursor:pointer;transition:border-color .12s,background .12s,transform .12s;}
.logout:hover{border-color:var(--accent);background:#1b232d;transform:translateY(-1px);}
</style>
</head>
<body>
<div class="card">
  <div class="ring"><svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6 9 17l-5-5"></path></svg></div>
  <h1>Connexion réussie</h1>
  <p>Vous êtes authentifié<span id="asWho"></span>.</p>
  <p style="color:var(--ink-faint);font-size:12.5px;margin-top:10px">Cette page est volontairement vide. Le tableau de bord viendra ici.</p>
  <a class="logout" href="/logout"><svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path><path d="M16 17l5-5-5-5M21 12H9"></path></svg> Se déconnecter</a>
</div>
<script>
(function(){var n="__USER__";if(n)document.getElementById('asWho').textContent=' en tant que @'+n;})();
</script>
</body>
</html>
""";
}
