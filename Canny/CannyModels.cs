using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kpi.Canny;

// ============================================================================
// Modèles BRUTS de l'API Canny v1 (https://canny.io/api/v1/<resource>/list).
// Champs volontairement RÉDUITS à ce que consomme la consolidation (CannyDatasetBuilder,
// port de build-dataset.js) : System.Text.Json ignore les propriétés surnuméraires.
// ============================================================================

/// <summary>Référence dénormalisée (id + nom éventuel) : board/category/tag imbriqués dans un post.</summary>
public sealed class CannyRef
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
}

public sealed class CannyAuthor
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public bool IsAdmin { get; set; }
}

public sealed class CannyRoadmapRaw
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Url { get; set; }
    public bool Archived { get; set; }
    public string? Created { get; set; }
    public int PostCount { get; set; }
}

/// <summary>Champ personnalisé Canny. <see cref="Value"/> peut être une string OU un tableau → JsonElement.</summary>
public sealed class CannyCustomField
{
    public string Name { get; set; } = "";
    public JsonElement Value { get; set; }
}

public sealed class CannyPostRaw
{
    public string Id { get; set; } = "";
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? Status { get; set; }
    public int Score { get; set; }
    public int CommentCount { get; set; }
    public string? Created { get; set; }
    /// <summary>Corps texte du post — contient les liens GitLab en clair (parsés pour le rapprochement).</summary>
    public string? Details { get; set; }
    public CannyRef? Board { get; set; }
    public CannyAuthor? Author { get; set; }
    public CannyRef? Category { get; set; }
    public List<CannyRef> Tags { get; set; } = new();
    public List<CannyRoadmapRaw> Roadmaps { get; set; } = new();
    public List<CannyCustomField> CustomFields { get; set; } = new();
}

public sealed class CannyCommentRaw
{
    public string Id { get; set; } = "";
    public string? Value { get; set; }
    public bool Internal { get; set; }
    public string? Status { get; set; }
    public string? Created { get; set; }
    public CannyRef? Post { get; set; }
    public CannyAuthor? Author { get; set; }
}

public sealed class CannyStatusChangeRaw
{
    public string Id { get; set; } = "";
    public string? Status { get; set; }
    public string? Created { get; set; }
    public CannyRef? Post { get; set; }
}

public sealed class CannyUserRaw
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? Email { get; set; }
    public bool IsAdmin { get; set; }
}

public sealed class CannyBoardRaw
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public bool IsPrivate { get; set; }
    public int PostCount { get; set; }
    public string? Created { get; set; }
}

public sealed class CannyCategoryRaw
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public CannyRef? Board { get; set; }
    public string? ParentID { get; set; }
    public int PostCount { get; set; }
}

public sealed class CannyTagRaw
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public CannyRef? Board { get; set; }
    public int PostCount { get; set; }
}

/// <summary>Résultat d'une extraction Canny (comptes par entité, pour le retour setup/options).</summary>
public sealed class CannyExtractResult
{
    public int Posts { get; set; }
    public int Comments { get; set; }
    public int Votes { get; set; }
    public int Users { get; set; }
    public int StatusChanges { get; set; }
    public int Boards { get; set; }
    public int Categories { get; set; }
    public int Tags { get; set; }
    public int Roadmaps { get; set; }
    public string ExtractedAt { get; set; } = "";
}
