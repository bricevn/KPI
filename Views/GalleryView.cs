using System.Text;

namespace Kpi.Views;

/// <summary>
/// Banc d'isolation des composants (route /gallery, DEV uniquement) : monte chaque
/// composant enregistré (Assets/components/*.jsx) seul, avec bascule thème/accent/densité.
/// Aucune donnée réelle : uniquement les fixtures déclarées par chaque composant.
/// Même modèle que DashboardView (React/Babel inline, sans build), mais SANS window.APP/__DATA__.
/// </summary>
public static class GalleryView
{
    // Fichiers de composants chargés (ordre libre — chacun s'auto-enregistre). Ajouter ici
    // tout nouveau composant. Chargés en text/babel après le registre.
    private static readonly string[] ComponentFiles = {
        "Button.jsx", "StatusBadge.jsx", "Avatar.jsx",
        "Chip.jsx", "DeltaBadge.jsx", "ProgressBar.jsx", "Sparkline.jsx", "Donut.jsx",
        "KpiCard.jsx", "DataTable.jsx", "PhaseBars.jsx",
    };

    public static string Page()
    {
        var baseDir = AppContext.BaseDirectory;
        string A(string sub, string f) => File.ReadAllText(Path.Combine(baseDir, "Assets", sub, f));
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"fr\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("  <title>Composants · KPI</title>");
        sb.AppendLine("  <link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.AppendLine("  <link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>");
        sb.AppendLine("  <link href=\"https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&family=IBM+Plex+Mono:wght@400;500;600&display=swap\" rel=\"stylesheet\">");
        sb.AppendLine("  <style>");
        sb.AppendLine(A("design", "charte-tokens.css"));   // source de vérité des tokens
        sb.AppendLine(A("design", "shared.css"));           // couleurs data (--c-*, --p-*)
        sb.AppendLine(A("design", "studio.css"));           // socle (avatars --av-*, .btn de base, etc.)
        sb.AppendLine(A("design", "charte-buttons.css"));   // boutons 3 niveaux (charte)
        sb.AppendLine(A("components", "components.css"));    // styles des composants
        sb.AppendLine(GalleryChromeCss);                    // chrome de la galerie
        sb.AppendLine("  html, body { margin: 0; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div id=\"root\"></div>");
        sb.AppendLine("  <script>" + A("vendor", "react.js") + "</script>");
        sb.AppendLine("  <script>" + A("vendor", "react-dom.js") + "</script>");
        sb.AppendLine("  <script>" + A("vendor", "babel.js") + "</script>");
        // Registre + utilitaires (plain JS, exécutés avant les scripts babel).
        sb.AppendLine("  <script>" + A("components", "registry.js") + "</script>");
        sb.AppendLine("  <script>" + A("components", "charte-complement.js") + "</script>");
        // Composants (s'auto-enregistrent), puis la galerie qui les monte.
        foreach (var f in ComponentFiles)
            sb.AppendLine("  <script type=\"text/babel\" data-presets=\"react\">" + A("components", f) + "</script>");
        sb.AppendLine("  <script type=\"text/babel\" data-presets=\"react\">" + A("components", "gallery.jsx") + "</script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    // Chrome de la galerie (sidebar + toolbar + canevas). Tout via les tokens de la charte.
    private const string GalleryChromeCss = """
    .gx.app { display: flex; min-height: 100vh; background: var(--color-bg); color: var(--color-ink-1); }
    .gx-sb { width: 240px; flex: none; background: var(--color-sidebar); border-right: 1px solid var(--color-line-1);
      padding: var(--space-5); height: 100vh; position: sticky; top: 0; overflow: auto; }
    .gx-brand { font-family: var(--font-display); font-weight: 700; font-size: var(--text-h3); margin-bottom: var(--space-5); }
    .gx-cat { margin-bottom: var(--space-4); }
    .gx-cat-h { font-size: var(--text-eyebrow); text-transform: uppercase; letter-spacing: .06em; font-weight: 700;
      color: var(--color-ink-3); margin-bottom: var(--space-2); }
    .gx-item { display: block; width: 100%; text-align: left; border: 0; background: transparent; color: var(--color-ink-2);
      font: 500 var(--text-sm)/1.4 var(--font-body); padding: var(--space-3); border-radius: var(--radius-md); cursor: pointer; }
    .gx-item:hover { background: var(--color-surface-2); color: var(--color-ink-1); }
    .gx-item.on { background: var(--color-accent-soft); color: var(--color-accent); font-weight: 600; }
    .gx-main { flex: 1; min-width: 0; padding: var(--space-6) var(--space-7); }
    .gx-toolbar { display: flex; align-items: center; gap: var(--space-5); margin-bottom: var(--space-7); flex-wrap: wrap; }
    .gx-seg { display: flex; gap: 3px; background: var(--color-surface-2); border-radius: var(--radius-md); padding: 3px; }
    .gx-seg button { border: 0; background: transparent; color: var(--color-ink-2); font: 600 var(--text-meta) var(--font-body);
      padding: 6px 13px; border-radius: var(--radius-sm); cursor: pointer; }
    .gx-seg button.on { background: var(--color-accent); color: #fff; }
    .gx-swatches { display: flex; gap: var(--space-3); }
    .gx-sw { width: 26px; height: 26px; border-radius: var(--radius-md); border: 2px solid transparent; cursor: pointer; padding: 0; }
    .gx-sw.on { border-color: var(--color-ink-1); box-shadow: 0 0 0 3px var(--color-accent-soft); }
    .gx-toggle { display: inline-flex; align-items: center; gap: 7px; font-size: var(--text-meta); color: var(--color-ink-2); cursor: pointer; }
    .gx-title { font-family: var(--font-display); font-size: var(--text-h1); font-weight: 700; margin: 0 0 6px; }
    .gx-notes { font-size: var(--text-sm); color: var(--color-ink-3); margin: 0 0 var(--space-6); max-width: 64ch; line-height: 1.5; }
    .gx-variants { display: flex; flex-wrap: wrap; gap: var(--space-5); }
    .gx-variant { border: 1px solid var(--color-line-1); border-radius: var(--radius-xl); background: var(--color-surface-1);
      overflow: hidden; min-width: 220px; }
    .gx-variant-h { font-size: var(--text-eyebrow); text-transform: uppercase; letter-spacing: .05em; font-weight: 700;
      color: var(--color-ink-3); padding: 10px 14px; border-bottom: 1px solid var(--color-line-2); }
    .gx-stage { padding: var(--space-6); display: flex; align-items: center; justify-content: center; gap: var(--space-4);
      min-height: 84px; background: var(--color-bg); }
    .gx-empty { color: var(--color-ink-3); padding: 40px; }
    """;
}
