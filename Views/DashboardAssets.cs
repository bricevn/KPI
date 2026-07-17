namespace Kpi.Views;

/// <summary>
/// Liste PARTAGÉE des fichiers de composants isolés (Assets/components/*.jsx), chargés à la fois
/// par la galerie (GalleryView) et le dashboard live (DashboardView) — source unique anti-divergence.
/// Ordre libre : chaque composant s'auto-enregistre via window.KPIGallery.register().
/// </summary>
public static class DashboardAssets
{
    public static readonly string[] ComponentFiles =
    {
        "Button.jsx", "StatusBadge.jsx", "Avatar.jsx",
        "Chip.jsx", "DeltaBadge.jsx", "ProgressBar.jsx", "Sparkline.jsx", "Donut.jsx",
        "KpiCard.jsx", "DataTable.jsx", "PhaseBars.jsx", "GanttChart.jsx",
    };
}
