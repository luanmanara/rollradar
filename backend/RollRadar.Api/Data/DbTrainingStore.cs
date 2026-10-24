using Microsoft.EntityFrameworkCore;
using Npgsql;
using RollRadar.Api.Common;
using RollRadar.Api.Training;

namespace RollRadar.Api.Data;

/// <summary><see cref="ITrainingStore"/> on the diary tables. Every query starts from the user's id.</summary>
public class DbTrainingStore(RollRadarDbContext db) : ITrainingStore
{
    public const int MaxPageSize = 100;
    private const int MaxPartnerSuggestions = 10;

    public async Task<TrainingPage> ListAsync(Guid userId, TrainingFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.TrainingSessions.Where(s => s.UserId == userId);
        if (filter.From is { } from) query = query.Where(s => s.Date >= from);
        if (filter.To is { } to) query = query.Where(s => s.Date <= to);
        if (filter.PartnerId is { } partnerId) query = query.Where(s => s.Rolls.Any(r => r.PartnerId == partnerId));
        if (SnakeCaseEnum.TryParse<TrainingFormat>(filter.Format, out var format)) query = query.Where(s => s.Format == format);
        if (SnakeCaseEnum.TryParse<Attire>(filter.Attire, out var attire)) query = query.Where(s => s.Attire == attire);
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var pattern = $"%{EscapeLike(filter.Query.Trim())}%";
            query = query.Where(s => EF.Functions.ILike(s.TechniqueNotes!, pattern, @"\")
                || s.Rolls.Any(r => EF.Functions.ILike(r.Notes!, pattern, @"\")));
        }

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);
        var total = await query.CountAsync(cancellationToken);
        var sessions = await WithDetails(query
                .OrderByDescending(s => s.Date).ThenByDescending(s => s.CreatedAt).ThenBy(s => s.Id)
                .Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);
        return new TrainingPage(await ToModelsAsync(sessions, cancellationToken), total, page, pageSize);
    }

    public async Task<TrainingSession?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var session = await WithDetails(db.TrainingSessions.Where(s => s.Id == id && s.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);
        return session is null ? null : (await ToModelsAsync([session], cancellationToken))[0];
    }

    public Task<SaveResult> CreateAsync(Guid userId, TrainingSessionInput input, CancellationToken cancellationToken = default) =>
        RetryOnceOnUniqueViolation(async () =>
        {
            var owner = await db.TrainingSessions.Where(s => s.Id == input.Id).Select(s => (Guid?)s.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            if (owner is not null)
                return owner == userId
                    ? new SaveResult(SaveStatus.AlreadyExists, await GetAsync(userId, input.Id, cancellationToken))
                    : new SaveResult(SaveStatus.Conflict);

            var errors = await ValidateAsync(input, cancellationToken);
            if (errors.Count > 0) return SaveResult.Invalid(errors);

            var session = new TrainingSessionEntity { Id = input.Id, UserId = userId, CreatedAt = DateTime.UtcNow };
            db.TrainingSessions.Add(session);
            await ApplyAsync(userId, session, input, null, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return new SaveResult(SaveStatus.Created, await GetAsync(userId, input.Id, cancellationToken));
        });

    public Task<SaveResult> UpdateAsync(Guid userId, Guid id, TrainingSessionInput input, CancellationToken cancellationToken = default) =>
        RetryOnceOnUniqueViolation(async () =>
        {
            input = input with { Id = id };
            var session = await db.TrainingSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
            if (session is null) return new SaveResult(SaveStatus.NotFound);

            var errors = await ValidateAsync(input, cancellationToken);
            if (errors.Count > 0) return SaveResult.Invalid(errors);

            // Rolls and tags are replaced as a whole, in one transaction with the new ones.
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Rolls.Where(r => r.SessionId == id).ExecuteDeleteAsync(cancellationToken);
            await db.SessionTechniques.Where(t => t.SessionId == id).ExecuteDeleteAsync(cancellationToken);
            var focus = await db.FocusPoints.FirstOrDefaultAsync(f => f.OriginSessionId == id, cancellationToken);
            await ApplyAsync(userId, session, input, focus, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new SaveResult(SaveStatus.Updated, await GetAsync(userId, id, cancellationToken));
        });

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        await db.TrainingSessions.Where(s => s.Id == id && s.UserId == userId).ExecuteDeleteAsync(cancellationToken) > 0;

    public Task<FocusPoint?> GetOpenFocusAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.FocusPoints.AsNoTracking()
            .Where(f => f.UserId == userId && f.Review == null)
            .OrderByDescending(f => f.OriginSession!.Date).ThenByDescending(f => f.CreatedAt)
            .Select(f => new FocusPoint(f.Id, f.Text, f.OriginSessionId, f.OriginSession!.Date, f.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<SaveResult> ReviewFocusAsync(Guid userId, long id, FocusReviewInput input, CancellationToken cancellationToken = default)
    {
        var focus = await db.FocusPoints.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, cancellationToken);
        if (focus is null) return new SaveResult(SaveStatus.NotFound);

        var errors = new Dictionary<string, string[]>();
        if (!SnakeCaseEnum.TryParse<FocusReview>(input.Review, out var review))
            errors["review"] = ["Responda sim, parcial ou não."];
        if (input.SessionId is { } sessionId
            && !await db.TrainingSessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken))
            errors["sessionId"] = ["Treino não encontrado."];
        if (errors.Count > 0) return SaveResult.Invalid(errors);

        focus.Review = review;
        focus.ReviewedSessionId = input.SessionId;
        await db.SaveChangesAsync(cancellationToken);
        return new SaveResult(SaveStatus.Updated);
    }

    public async Task<IReadOnlyList<Partner>> FindPartnersAsync(Guid userId, string? query, CancellationToken cancellationToken = default)
    {
        var normalized = TextNormalizer.Normalize(query);
        var partners = await db.Partners.AsNoTracking()
            .Where(p => p.UserId == userId && (normalized == "" || p.NormalizedName.Contains(normalized)))
            .OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Name)
            .Take(MaxPartnerSuggestions)
            .ToListAsync(cancellationToken);
        return partners.Select(ToModel).ToList();
    }

    public async Task<RecentPositions> GetRecentPositionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var nodes = await GameNodesAsync(userId, cancellationToken);
        var categories = TrainingValidation.PositionCategories.ToList();
        var catalog = await db.Techniques.AsNoTracking()
            .Where(t => categories.Contains(t.Category))
            .OrderBy(t => t.Position)
            .Select(t => new CatalogPosition(t.Id, t.English, t.Portuguese, t.Category))
            .ToListAsync(cancellationToken);
        return new RecentPositions(nodes.Where(n => !n.Hidden).ToList(), catalog);
    }

    public async Task<AccountExport?> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return null;

        var sessions = await WithDetails(db.TrainingSessions.Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Date).ThenByDescending(s => s.CreatedAt).ThenBy(s => s.Id)).ToListAsync(cancellationToken);
        var partners = await db.Partners.AsNoTracking().Where(p => p.UserId == userId).OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
        var focusPoints = await db.FocusPoints.AsNoTracking().Where(f => f.UserId == userId).OrderBy(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
        return new AccountExport(
            DateTime.UtcNow, user.Id, user.Email, user.DisplayName,
            await ToModelsAsync(sessions, cancellationToken),
            partners.Select(ToModel).ToList(),
            await GameNodesAsync(userId, cancellationToken),
            focusPoints.Select(f => new ExportedFocusPoint(f.Id, f.Text, f.OriginSessionId, f.CreatedAt, SnakeCaseEnum.Name(f.Review), f.ReviewedSessionId)).ToList());
    }

    /// <summary>The user's positions, the most recently used first, then the newest never used.</summary>
    private async Task<List<GameNode>> GameNodesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var nodes = await db.GameNodes.AsNoTracking()
            .Where(n => n.UserId == userId)
            .Select(n => new
            {
                Node = n,
                LastUsed = db.RollSteps.Where(s => s.NodeId == n.Id).Max(s => (DateOnly?)s.Roll!.Session!.Date),
                Uses = db.RollSteps.Count(s => s.NodeId == n.Id),
            })
            .ToListAsync(cancellationToken);
        return nodes
            .OrderByDescending(n => n.LastUsed.HasValue).ThenByDescending(n => n.LastUsed).ThenByDescending(n => n.Node.CreatedAt)
            .Select(n => new GameNode(n.Node.Id, n.Node.TechniqueId, n.Node.Label, SnakeCaseEnum.Name(n.Node.Side), n.Node.Hidden, n.LastUsed, n.Uses))
            .ToList();
    }

    private async Task<Dictionary<string, string[]>> ValidateAsync(TrainingSessionInput input, CancellationToken cancellationToken)
    {
        var categories = await db.Techniques.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Category, cancellationToken);
        return TrainingValidation.Validate(input, categories, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    /// <summary>Copies a valid input onto the tracked session, with new rolls and tags, the partners and positions they use and the point to fix.</summary>
    private async Task ApplyAsync(Guid userId, TrainingSessionEntity session, TrainingSessionInput input, FocusPointEntity? focus, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        session.Date = input.Date;
        SnakeCaseEnum.TryParse<Attire>(input.Attire, out var attire);
        SnakeCaseEnum.TryParse<TrainingFormat>(input.Format, out var format);
        session.Attire = attire;
        session.Format = format;
        session.DurationMinutes = input.DurationMinutes;
        session.TechniqueNotes = Clean(input.TechniqueNotes);
        session.Sleep = input.Sleep;
        session.Pain = input.Pain;
        session.Energy = input.Energy;
        session.UpdatedAt = now;

        var techniques = (input.Techniques ?? []).Distinct().ToList();
        session.Techniques = techniques
            .Select((id, i) => new SessionTechniqueEntity { SessionId = session.Id, TechniqueId = id, Position = i })
            .ToList();

        var rolls = input.Rolls ?? [];
        var partners = await PartnersForAsync(userId, rolls, now, cancellationToken);
        var nodes = await NodesForAsync(userId, rolls, cancellationToken);
        session.Rolls = rolls.Select((roll, i) => new RollEntity
        {
            SessionId = session.Id,
            Order = i,
            Partner = Clean(roll.PartnerName) is { } name ? partners[TextNormalizer.Normalize(name)] : null,
            PartnerBelt = ParseOrNull<Belt>(roll.PartnerBelt),
            DurationMinutes = roll.DurationMinutes,
            Notes = Clean(roll.Notes),
            Steps = (roll.Steps ?? []).Select((step, j) => new RollStepEntity
            {
                Order = j,
                Node = nodes[NodeKey(new PositionRef(step.TechniqueId, step.Side))],
                Outcome = ParseOrNull<StepOutcome>(step.Outcome)!.Value,
            }).ToList(),
            Finishes = (roll.Finishes ?? []).Select((finish, j) => new RollFinishEntity
            {
                Order = j,
                ByMe = finish.ByMe,
                TechniqueId = Clean(finish.TechniqueId),
                Node = finish.Position is { } position ? nodes[NodeKey(position)] : null,
            }).ToList(),
        }).ToList();

        var focusText = Clean(input.FocusPoint);
        if (focusText is null)
        {
            if (focus is not null) db.FocusPoints.Remove(focus);
        }
        else if (focus is null)
        {
            db.FocusPoints.Add(new FocusPointEntity { UserId = userId, OriginSession = session, Text = focusText, CreatedAt = now });
        }
        else
        {
            focus.Text = focusText;
        }
    }

    /// <summary>The partners named in the rolls by normalized name, creating the new ones and remembering name and belt.</summary>
    private async Task<Dictionary<string, PartnerEntity>> PartnersForAsync(
        Guid userId, IReadOnlyList<RollInput> rolls, DateTime now, CancellationToken cancellationToken)
    {
        var named = rolls
            .Select(r => (Name: Clean(r.PartnerName), Belt: ParseOrNull<Belt>(r.PartnerBelt)))
            .Where(r => r.Name is not null)
            .ToList();
        var keys = named.Select(r => TextNormalizer.Normalize(r.Name)).Distinct().ToList();
        var partners = await db.Partners.Where(p => p.UserId == userId && keys.Contains(p.NormalizedName))
            .ToDictionaryAsync(p => p.NormalizedName, cancellationToken);

        foreach (var (name, belt) in named)
        {
            var key = TextNormalizer.Normalize(name);
            if (!partners.TryGetValue(key, out var partner))
            {
                partner = new PartnerEntity { UserId = userId, Name = name!, NormalizedName = key };
                db.Partners.Add(partner);
                partners[key] = partner;
            }
            partner.Name = name!;
            if (belt is not null) partner.Belt = belt;
            partner.UpdatedAt = now;
        }
        return partners;
    }

    /// <summary>
    /// The user's game nodes for the positions marked in the rolls or that submissions came from, creating the ones
    /// used for the first time.
    /// </summary>
    private async Task<Dictionary<(string TechniqueId, NodeSide Side), GameNodeEntity>> NodesForAsync(
        Guid userId, IReadOnlyList<RollInput> rolls, CancellationToken cancellationToken)
    {
        var wanted = rolls
            .SelectMany(r => (r.Steps ?? []).Select(s => new PositionRef(s.TechniqueId, s.Side))
                .Concat((r.Finishes ?? []).Where(f => f.Position is not null).Select(f => f.Position!)))
            .Select(NodeKey)
            .Distinct()
            .ToList();
        var techniqueIds = wanted.Select(w => w.TechniqueId).Distinct().ToList();
        var nodes = (await db.GameNodes.Where(n => n.UserId == userId && techniqueIds.Contains(n.TechniqueId)).ToListAsync(cancellationToken))
            .ToDictionary(n => (n.TechniqueId, n.Side));

        foreach (var key in wanted)
        {
            if (nodes.TryGetValue(key, out var node))
            {
                node.Hidden = false;
                continue;
            }
            node = new GameNodeEntity { UserId = userId, TechniqueId = key.TechniqueId, Side = key.Side, CreatedAt = DateTime.UtcNow };
            db.GameNodes.Add(node);
            nodes[key] = node;
        }
        return nodes;
    }

    /// <summary>
    /// The same session sent twice at once (the client retries when the answer is slow), or the same partner or
    /// position created by two requests: the second one fails on a unique index, then runs again and finds the rows.
    /// </summary>
    private async Task<SaveResult> RetryOnceOnUniqueViolation(Func<Task<SaveResult>> save)
    {
        try
        {
            return await save();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return await save();
        }
    }

    private static IQueryable<TrainingSessionEntity> WithDetails(IQueryable<TrainingSessionEntity> query) =>
        query.AsNoTracking()
            .Include(s => s.Techniques)
            .Include(s => s.Rolls).ThenInclude(r => r.Partner)
            .Include(s => s.Rolls).ThenInclude(r => r.Steps).ThenInclude(st => st.Node)
            .Include(s => s.Rolls).ThenInclude(r => r.Finishes).ThenInclude(f => f.Node)
            .AsSplitQuery();

    private async Task<List<TrainingSession>> ToModelsAsync(List<TrainingSessionEntity> sessions, CancellationToken cancellationToken)
    {
        var ids = sessions.Select(s => s.Id).ToList();
        var focus = await db.FocusPoints.AsNoTracking().Where(f => ids.Contains(f.OriginSessionId))
            .ToDictionaryAsync(f => f.OriginSessionId, f => f.Text, cancellationToken);
        return sessions.Select(s => new TrainingSession(
            s.Id, s.Date, SnakeCaseEnum.Name(s.Attire), SnakeCaseEnum.Name(s.Format), s.DurationMinutes, s.TechniqueNotes,
            s.Techniques.OrderBy(t => t.Position).Select(t => t.TechniqueId).ToList(),
            s.Sleep, s.Pain, s.Energy,
            focus.GetValueOrDefault(s.Id),
            s.Rolls.OrderBy(r => r.Order).Select(r => new Roll(
                r.Order, r.PartnerId, r.Partner?.Name, SnakeCaseEnum.Name(r.PartnerBelt), r.DurationMinutes, r.Notes,
                r.Steps.OrderBy(st => st.Order)
                    .Select(st => new RollStep(st.NodeId, st.Node!.TechniqueId, SnakeCaseEnum.Name(st.Node.Side), SnakeCaseEnum.Name(st.Outcome)))
                    .ToList(),
                r.Finishes.OrderBy(f => f.Order)
                    .Select(f => new RollFinish(f.ByMe, f.TechniqueId, f.NodeId,
                        f.Node is null ? null : new PositionRef(f.Node.TechniqueId, SnakeCaseEnum.Name(f.Node.Side))))
                    .ToList()))
                .ToList(),
            s.CreatedAt, s.UpdatedAt)).ToList();
    }

    private static (string TechniqueId, NodeSide Side) NodeKey(PositionRef position) =>
        (position.TechniqueId, ParseOrNull<NodeSide>(position.Side)!.Value);

    private static Partner ToModel(PartnerEntity p) => new(p.Id, p.Name, SnakeCaseEnum.Name(p.Belt));

    private static TEnum? ParseOrNull<TEnum>(string? name) where TEnum : struct, Enum =>
        SnakeCaseEnum.TryParse<TEnum>(name, out var value) ? value : null;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
