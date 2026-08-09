using Asp.Versioning;
using CommunityOS.Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Identity.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/.well-known")]
[ApiVersion(1.0)]
public sealed class DiscoveryController(
    RsaSigningKeyProvider signingKeyProvider,
    OidcDiscoveryDocument oidcDiscoveryDocument) : ControllerBase
{
    [HttpGet("openid-configuration")]
    [AllowAnonymous]
    [Produces("application/json")]
    public IActionResult OpenIdConfiguration() => Content(
        oidcDiscoveryDocument.BuildJson(), "application/json");

    [HttpGet("jwks")]
    [AllowAnonymous]
    [Produces("application/json")]
    public IActionResult Jwks() => Content(
        signingKeyProvider.GenerateJwksJson(), "application/json");
}
