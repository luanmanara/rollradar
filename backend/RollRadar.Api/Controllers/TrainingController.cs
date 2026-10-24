using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RollRadar.Api.Accounts;
using RollRadar.Api.Data;
using RollRadar.Api.Training;

namespace RollRadar.Api.Controllers;

/// <summary>
/// The signed-in user's training diary. The user always comes from the session cookie: an id in the address only
/// picks one of the user's own sessions, someone else's answers 404.
/// </summary>
[ApiController]
[Route("api")]
[Authorize(Policy = UserAuthentication.Policy)]
public class TrainingController(ITrainingStore store) : ControllerBase
{
    private Guid UserId => User.FindUserId() ?? throw new InvalidOperationException("The User policy lets only signed-in users through.");

    /// <summary>The history, newest first, filtered by period, partner, format, attire and text (<paramref name="q"/>).</summary>
    [HttpGet("treinos")]
    public async Task<ActionResult<TrainingPage>> List(
        [FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, [FromQuery] long? parceiro, [FromQuery] string? formato,
        [FromQuery] string? roupa, [FromQuery] string? q, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 20,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(formato) && !SnakeCaseEnum.TryParse<TrainingFormat>(formato, out _))
            ModelState.AddModelError(nameof(formato), "Use aula, open ou competicao.");
        if (!string.IsNullOrEmpty(roupa) && !SnakeCaseEnum.TryParse<Attire>(roupa, out _))
            ModelState.AddModelError(nameof(roupa), "Use gi ou no_gi.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        return await store.ListAsync(UserId, new TrainingFilter(de, ate, parceiro, formato, roupa, q, pagina, tamanho), cancellationToken);
    }

    [HttpGet("treinos/{id:guid}")]
    public async Task<ActionResult<TrainingSession>> Get(Guid id, CancellationToken cancellationToken) =>
        await store.GetAsync(UserId, id, cancellationToken) is { } session ? session : NotFound();

    /// <summary>
    /// Records a session. The id comes from the client, so sending it again (after a timeout, say) answers 200 with
    /// the stored session instead of creating another; 201 the first time.
    /// </summary>
    [HttpPost("treinos")]
    public async Task<ActionResult<TrainingSession>> Create(TrainingSessionInput input, CancellationToken cancellationToken)
    {
        var result = await store.CreateAsync(UserId, input, cancellationToken);
        return result.Status switch
        {
            SaveStatus.Created => CreatedAtAction(nameof(Get), new { id = input.Id }, result.Session),
            SaveStatus.AlreadyExists => Ok(result.Session),
            SaveStatus.Conflict => Conflict(),
            _ => Invalid(result),
        };
    }

    [HttpPut("treinos/{id:guid}")]
    public async Task<ActionResult<TrainingSession>> Update(Guid id, TrainingSessionInput input, CancellationToken cancellationToken)
    {
        var result = await store.UpdateAsync(UserId, id, input, cancellationToken);
        return result.Status switch
        {
            SaveStatus.Updated => Ok(result.Session),
            SaveStatus.NotFound => NotFound(),
            _ => Invalid(result),
        };
    }

    [HttpDelete("treinos/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await store.DeleteAsync(UserId, id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>The most recent point to fix not answered yet; 204 when there is none.</summary>
    [HttpGet("treinos/foco")]
    public async Task<ActionResult<FocusPoint>> GetFocus(CancellationToken cancellationToken) =>
        await store.GetOpenFocusAsync(UserId, cancellationToken) is { } focus ? focus : NoContent();

    /// <summary>Answers the point to fix: "sim", "parcial" or "nao".</summary>
    [HttpPut("treinos/foco/{id:long}")]
    public async Task<IActionResult> ReviewFocus(long id, FocusReviewInput input, CancellationToken cancellationToken)
    {
        var result = await store.ReviewFocusAsync(UserId, id, input, cancellationToken);
        return result.Status switch
        {
            SaveStatus.Updated => NoContent(),
            SaveStatus.NotFound => NotFound(),
            _ => Invalid(result),
        };
    }

    /// <summary>The user's training partners matching <paramref name="q"/>, for the autocomplete.</summary>
    [HttpGet("parceiros")]
    public async Task<ActionResult<IReadOnlyList<Partner>>> Partners([FromQuery] string? q, CancellationToken cancellationToken) =>
        Ok(await store.FindPartnersAsync(UserId, q, cancellationToken));

    /// <summary>The positions of the user's game by recent use, and the catalog positions to add.</summary>
    [HttpGet("mapa/posicoes-recentes")]
    public async Task<ActionResult<RecentPositions>> RecentPositions(CancellationToken cancellationToken) =>
        await store.GetRecentPositionsAsync(UserId, cancellationToken);

    private ActionResult Invalid(SaveResult result) =>
        ValidationProblem(new ValidationProblemDetails(result.Errors ?? new Dictionary<string, string[]>())
        {
            Title = "Confira os dados do treino.",
        });
}
