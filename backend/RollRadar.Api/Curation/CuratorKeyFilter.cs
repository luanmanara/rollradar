using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace RollRadar.Api.Curation;

/// <summary>Lets the request through only with the curator key from <see cref="CurationOptions.AdminKey"/>.</summary>
public class CuratorKeyFilter(IOptions<CurationOptions> options) : IAuthorizationFilter
{
    public const string Header = "X-Curator-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = options.Value.AdminKey;
        if (string.IsNullOrWhiteSpace(expected))
        {
            context.Result = new ObjectResult("Curation is closed: set Curation:AdminKey.") { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        var given = context.HttpContext.Request.Headers[Header].ToString();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected)))
            context.Result = new UnauthorizedResult();
    }
}
