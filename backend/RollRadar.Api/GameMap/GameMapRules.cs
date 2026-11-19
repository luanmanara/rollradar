using RollRadar.Api.Data;
using RollRadar.Api.Training;

namespace RollRadar.Api.GameMap;

/// <summary>Which catalog techniques an arrow of each kind can carry. Messages in Portuguese, the site shows them.</summary>
public static class GameMapRules
{
    /// <summary>Catalog categories by kind; a transition takes any technique.</summary>
    private static readonly Dictionary<EdgeKind, IReadOnlySet<string>> CategoriesByKind = new()
    {
        [EdgeKind.Raspagem] = new HashSet<string> { "Raspagens" },
        [EdgeKind.Passagem] = new HashSet<string> { "Passagens" },
        [EdgeKind.Fuga] = new HashSet<string> { "Fugas" },
        [EdgeKind.Queda] = new HashSet<string> { "Quedas" },
        [EdgeKind.Finalizacao] = TrainingValidation.FinishCategories,
    };

    /// <summary>Kinds a roll's transition (detailed mode) can have: every kind but submissions.</summary>
    public static readonly IReadOnlySet<EdgeKind> TransitionKinds =
        new HashSet<EdgeKind> { EdgeKind.Raspagem, EdgeKind.Passagem, EdgeKind.Fuga, EdgeKind.Queda, EdgeKind.Transicao };

    public const int MaxLabelLength = 80;

    /// <summary>Null when the technique (optional) suits the kind; otherwise the problem.</summary>
    public static string? CheckTechnique(EdgeKind kind, string? techniqueId, IReadOnlyDictionary<string, string> categoryById)
    {
        if (string.IsNullOrEmpty(techniqueId)) return null;
        if (!categoryById.TryGetValue(techniqueId, out var category)) return "Técnica desconhecida.";
        if (!CategoriesByKind.TryGetValue(kind, out var allowed) || allowed.Contains(category)) return null;
        return kind switch
        {
            EdgeKind.Raspagem => "Uma raspagem precisa de uma técnica de Raspagens.",
            EdgeKind.Passagem => "Uma passagem precisa de uma técnica de Passagens.",
            EdgeKind.Fuga => "Uma fuga precisa de uma técnica de Fugas.",
            EdgeKind.Queda => "Uma queda precisa de uma técnica de Quedas.",
            _ => "A finalização deve ser um estrangulamento, uma chave de braço ou uma chave de perna.",
        };
    }
}
