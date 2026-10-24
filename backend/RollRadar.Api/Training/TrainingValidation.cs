using RollRadar.Api.Data;

namespace RollRadar.Api.Training;

/// <summary>Checks a session before it is stored. Messages are in Portuguese: the site shows them as they come.</summary>
public static class TrainingValidation
{
    /// <summary>Catalog categories a position of the game can come from.</summary>
    public static readonly IReadOnlySet<string> PositionCategories = new HashSet<string> { "Posições", "Guardas" };

    /// <summary>Catalog categories a roll can end with.</summary>
    public static readonly IReadOnlySet<string> FinishCategories =
        new HashSet<string> { "Estrangulamentos", "Chaves de braço", "Chaves de perna" };

    public const int MaxDurationMinutes = 600;
    public const int MaxTechniques = 20;
    public const int MaxRolls = 40;
    public const int MaxSteps = 30;
    public const int MaxFinishes = 30;
    public const int MaxRollMinutes = 60;
    public const int MaxNotesLength = 4000;
    public const int MaxRollNotesLength = 1000;
    public const int MaxFocusLength = 300;
    public const int MaxPartnerNameLength = 80;

    /// <summary>Field (as the client names it, e.g. "rolls[0].finishTechniqueId") -> problems; empty when valid.</summary>
    /// <param name="categoryById">Category of every catalog technique, by id.</param>
    /// <param name="today">Today's date; a day ahead is accepted for time zones.</param>
    public static Dictionary<string, string[]> Validate(
        TrainingSessionInput input, IReadOnlyDictionary<string, string> categoryById, DateOnly today)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
            list.Add(message);
        }

        if (input.Id == Guid.Empty) Add("id", "Informe o id do treino.");
        if (input.Date > today.AddDays(1)) Add("date", "A data do treino não pode ser no futuro.");
        if (input.Date < new DateOnly(1990, 1, 1)) Add("date", "Data inválida.");
        if (!SnakeCaseEnum.TryParse<Attire>(input.Attire, out _)) Add("attire", "Escolha com ou sem kimono.");
        if (!SnakeCaseEnum.TryParse<TrainingFormat>(input.Format, out _)) Add("format", "Escolha aula, open ou competição.");
        if (input.DurationMinutes is <= 0 or > MaxDurationMinutes)
            Add("durationMinutes", $"A duração deve ser de 1 a {MaxDurationMinutes} minutos.");
        CheckScale(input.Sleep, "sleep", Add);
        CheckScale(input.Pain, "pain", Add);
        CheckScale(input.Energy, "energy", Add);
        if (input.TechniqueNotes?.Length > MaxNotesLength) Add("techniqueNotes", $"Use no máximo {MaxNotesLength} caracteres.");
        if (input.FocusPoint?.Trim().Length > MaxFocusLength) Add("focusPoint", $"Use no máximo {MaxFocusLength} caracteres.");

        var techniques = input.Techniques ?? [];
        if (techniques.Count > MaxTechniques) Add("techniques", $"Marque no máximo {MaxTechniques} técnicas.");
        foreach (var id in techniques.Where(id => !categoryById.ContainsKey(id)))
            Add("techniques", $"Técnica desconhecida: {id}.");

        var rolls = input.Rolls ?? [];
        if (rolls.Count > MaxRolls) Add("rolls", $"Registre no máximo {MaxRolls} rolas.");
        for (var i = 0; i < rolls.Count; i++)
        {
            var roll = rolls[i];
            var prefix = $"rolls[{i}]";
            if (roll.PartnerName?.Trim().Length > MaxPartnerNameLength)
                Add($"{prefix}.partnerName", $"Use no máximo {MaxPartnerNameLength} caracteres.");
            if (roll.PartnerBelt is not null && !SnakeCaseEnum.TryParse<Belt>(roll.PartnerBelt, out _))
                Add($"{prefix}.partnerBelt", "Faixa inválida.");
            if (roll.Notes?.Length > MaxRollNotesLength)
                Add($"{prefix}.notes", $"Use no máximo {MaxRollNotesLength} caracteres.");
            if (roll.DurationMinutes is <= 0 or > MaxRollMinutes)
                Add($"{prefix}.durationMinutes", $"A duração do rola deve ser de 1 a {MaxRollMinutes} minutos.");

            var steps = roll.Steps ?? [];
            if (steps.Count > MaxSteps) Add($"{prefix}.steps", $"Marque no máximo {MaxSteps} posições.");
            for (var j = 0; j < steps.Count; j++)
            {
                var step = steps[j];
                CheckPosition(new PositionRef(step.TechniqueId, step.Side), $"{prefix}.steps[{j}]");
                if (!SnakeCaseEnum.TryParse<StepOutcome>(step.Outcome, out _))
                    Add($"{prefix}.steps[{j}].outcome", "Escolha estive, fui bem ou sofri.");
            }

            var finishes = roll.Finishes ?? [];
            if (finishes.Count > MaxFinishes) Add($"{prefix}.finishes", $"Registre no máximo {MaxFinishes} finalizações por rola.");
            for (var j = 0; j < finishes.Count; j++)
            {
                var finish = finishes[j];
                if (!string.IsNullOrEmpty(finish.TechniqueId)
                    && (!categoryById.TryGetValue(finish.TechniqueId, out var category) || !FinishCategories.Contains(category)))
                    Add($"{prefix}.finishes[{j}].techniqueId", "A finalização deve ser um estrangulamento, uma chave de braço ou uma chave de perna.");
                if (finish.Position is { } position) CheckPosition(position, $"{prefix}.finishes[{j}].position");
            }
        }

        void CheckPosition(PositionRef position, string field)
        {
            if (!categoryById.TryGetValue(position.TechniqueId ?? "", out var category) || !PositionCategories.Contains(category))
                Add($"{field}.techniqueId", "A posição deve ser do catálogo de posições ou guardas.");
            if (!SnakeCaseEnum.TryParse<NodeSide>(position.Side, out _))
                Add($"{field}.side", "Escolha por cima, por baixo ou neutro.");
        }

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }

    private static void CheckScale(int? value, string field, Action<string, string> add)
    {
        if (value is < 1 or > 5) add(field, "Use uma nota de 1 a 5.");
    }
}
