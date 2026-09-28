using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Identity;
using AspNetHasher = Microsoft.AspNetCore.Identity.PasswordHasher<ELearning.Domain.Identity.User>;
using AspNetResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace ELearning.Infrastructure.Security;

/// <summary>PBKDF2 qua PasswordHasher của ASP.NET Core Identity (docs/07-bao-mat.md mục 2).</summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private readonly AspNetHasher _inner = new();

    public string Hash(User user, string password) => _inner.HashPassword(user, password);

    public PasswordCheck Verify(User user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return PasswordCheck.Failed;
        }

        return _inner.VerifyHashedPassword(user, user.PasswordHash, password) switch
        {
            AspNetResult.Success => PasswordCheck.Success,
            AspNetResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed,
        };
    }
}
