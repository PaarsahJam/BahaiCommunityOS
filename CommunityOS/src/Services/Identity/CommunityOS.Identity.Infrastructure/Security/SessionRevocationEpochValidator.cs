using System.Globalization;
using System.Security.Claims;

namespace CommunityOS.Identity.Infrastructure.Security;

/// <summary>
/// ADR-036 D1/Q1: reads the signed <c>sre</c> claim and applies the
/// account-epoch validity rule shared by every Identity JWT validation seam.
///
/// <list type="bullet">
/// <item>Serialization: the token epoch is a JSON numeric claim
/// (<see cref="ClaimValueTypes.Integer64"/> at issuance) corresponding to the
/// CLR <see langword="long"/>.</item>
/// <item>Absent <c>sre</c> reads as epoch <c>0</c> for legacy compatibility.</item>
/// <item>Non-numeric or otherwise malformed <c>sre</c> fails closed.</item>
/// <item>A token is accepted when <c>tokenEpoch &gt;= accountEpoch</c> and
/// rejected when <c>tokenEpoch &lt; accountEpoch</c>.</item>
/// </list>
/// </summary>
public static class SessionRevocationEpochValidator
{
    /// <summary>The JWT claim name carrying the account's session-revocation epoch.</summary>
    public const string ClaimType = "sre";

    // The XML Schema value types emitted for integral JSON number claims by
    // both JwtSecurityTokenHandler and JsonWebTokenHandler during inbound
    // validation. Strings, doubles (incl. precision-losing overflow), booleans
    // and JSON null are deliberately excluded so those representations fail closed.
    private const string Integer32Type = "http://www.w3.org/2001/XMLSchema#integer32";
    private const string Integer64Type = "http://www.w3.org/2001/XMLSchema#integer64";

    /// <summary>
    /// Reads the token epoch from a validated principal. Returns
    /// <see langword="true"/> with <paramref name="tokenEpoch"/> set to <c>0</c>
    /// when the claim is absent (legacy compatibility). Returns
    /// <see langword="false"/> when the claim is present but not an integral
    /// numeric epoch (fail closed).
    /// </summary>
    public static bool TryReadTokenEpoch(ClaimsPrincipal? principal, out long tokenEpoch)
    {
        tokenEpoch = 0;

        var claim = principal?.FindFirst(ClaimType);
        if (claim is null)
            return true;

        if (claim.ValueType is not (Integer32Type or Integer64Type))
            return false;

        return long.TryParse(claim.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out tokenEpoch);
    }

    /// <summary>
    /// Applies the ADR-036 validity rule against the locally known account
    /// epoch: rejected when the token is older than the account epoch, otherwise
    /// accepted (equal, or newer per the agreed ADR-036 model).
    /// </summary>
    public static TokenEpochValidity Evaluate(long tokenEpoch, long accountEpoch) =>
        tokenEpoch < accountEpoch ? TokenEpochValidity.Revoked : TokenEpochValidity.Accepted;
}

public enum TokenEpochValidity
{
    Accepted,
    Revoked
}