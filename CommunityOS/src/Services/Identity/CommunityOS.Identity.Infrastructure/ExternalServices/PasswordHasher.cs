using CommunityOS.Identity.Application.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace CommunityOS.Identity.Infrastructure.ExternalServices;

internal sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<string> _inner = new();

    public string Hash(string plainText) =>
        _inner.HashPassword(string.Empty, plainText);

    public bool Verify(string plainText, string hash) =>
        _inner.VerifyHashedPassword(string.Empty, hash, plainText)
            != PasswordVerificationResult.Failed;
}
