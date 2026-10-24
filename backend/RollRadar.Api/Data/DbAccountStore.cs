using Microsoft.EntityFrameworkCore;
using Npgsql;
using RollRadar.Api.Accounts;

namespace RollRadar.Api.Data;

/// <summary><see cref="IAccountStore"/> on the users and user_logins tables.</summary>
public class DbAccountStore(RollRadarDbContext db) : IAccountStore
{
    public async Task<Guid> SignInAsync(ExternalLogin login, CancellationToken cancellationToken = default)
    {
        try
        {
            return await SignInOnceAsync(login, cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two first sign-ins of the same account at once: the other one created the user, so use it.
            db.ChangeTracker.Clear();
            return await SignInOnceAsync(login, cancellationToken);
        }
    }

    private async Task<Guid> SignInOnceAsync(ExternalLogin login, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var user = await db.UserLogins
            .Where(l => l.Provider == login.Provider && l.ProviderKey == login.ProviderKey)
            .Select(l => l.User)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            user = new UserEntity { Id = Guid.NewGuid(), CreatedAt = now };
            user.Logins.Add(new UserLoginEntity { Provider = login.Provider, ProviderKey = login.ProviderKey });
            db.Users.Add(user);
        }

        user.Email = login.Email;
        user.DisplayName = login.DisplayName;
        user.LastLoginAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    public Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new Account(u.Id, u.Email, u.DisplayName))
            .FirstOrDefaultAsync(cancellationToken);
}
