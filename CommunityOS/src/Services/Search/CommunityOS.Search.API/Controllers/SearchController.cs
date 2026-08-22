using Asp.Versioning;
using CommunityOS.Search.API.Extensions;
using CommunityOS.Search.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CommunityOS.Search.API.Controllers;
[ApiController, ApiVersion("1.0"), Route("api/v{version:apiVersion}/search"), Authorize]
public sealed class SearchController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SearchResultPageDto>(200), ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(403)]
    public Task<SearchResultPageDto> Search([FromQuery] string q, [FromQuery] string[]? types = null, [FromQuery] Guid? organizationUnitId = null, [FromQuery] string[]? status = null, [FromQuery] bool sensitive = false, [FromQuery] int? limit = null, [FromQuery] int offset = 0, CancellationToken ct = default) => Send(null, q, types, organizationUnitId, status, sensitive, limit, offset, ct);

    [HttpGet("admin/health")]
    [ProducesResponseType<SearchIndexHealthDto>(200), ProducesResponseType(401), ProducesResponseType(403)]
    public async Task<IActionResult> Health(CancellationToken ct) => Ok(await sender.Send(new HealthQuery(User.SubjectId()), ct));

    [HttpPost("admin/reindex")]
    [ProducesResponseType(204), ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(403)]
    public async Task<IActionResult> Reindex(CancellationToken ct) { await sender.Send(new ReindexCommand(User.SubjectId()), ct); return NoContent(); }

    [HttpPost("admin/reindex/{sourceType}")]
    [ProducesResponseType(204), ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(403)]
    public async Task<IActionResult> ReindexSource(string sourceType, CancellationToken ct) { await sender.Send(new ReindexCommand(User.SubjectId(), sourceType), ct); return NoContent(); }

    [HttpGet("{sourceType}")]
    [ProducesResponseType<SearchResultPageDto>(200), ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(403)]
    public Task<SearchResultPageDto> SearchSource(string sourceType, [FromQuery] string q, [FromQuery] Guid? organizationUnitId = null, [FromQuery] string[]? status = null, [FromQuery] bool sensitive = false, [FromQuery] int? limit = null, [FromQuery] int offset = 0, CancellationToken ct = default) => Send(sourceType, q, null, organizationUnitId, status, sensitive, limit, offset, ct);

    private Task<SearchResultPageDto> Send(string? sourceType, string q, string[]? types, Guid? unit, string[]? status, bool sensitive, int? limit, int offset, CancellationToken ct) => sender.Send(new SearchQuery(User.SubjectId(), q ?? string.Empty, SearchQueryFilterParser.Normalize(types), sourceType, unit, SearchQueryFilterParser.Normalize(status), sensitive, limit, offset), ct);
}
