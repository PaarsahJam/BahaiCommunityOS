namespace CommunityOS.Identity.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(Guid memberId, string email, IEnumerable<string> roles);
    string GenerateRefreshToken();
    Guid? ValidateRefreshToken(string refreshToken);
}

public interface IPasswordHasher
{
    string Hash(string plainText);
    bool Verify(string plainText, string hash);
}
