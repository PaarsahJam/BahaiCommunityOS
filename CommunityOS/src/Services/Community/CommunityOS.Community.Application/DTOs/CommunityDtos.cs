namespace CommunityOS.Community.Application.DTOs;

public sealed record CommunityDto(
    Guid Id,
    string Name,
    string Country,
    string? Region,
    string? City,
    double? Latitude,
    double? Longitude,
    string HierarchyLevel,
    Guid? ParentId,
    bool IsActive,
    IReadOnlyList<LocalUnitDto> LocalUnits);

public sealed record LocalUnitDto(
    Guid Id,
    string Name,
    string Country,
    string? Region,
    string? City,
    Guid ClusterId,
    bool IsActive);

public sealed record ClusterDto(
    Guid Id,
    string Name,
    string Country,
    string? Region,
    string? City,
    Guid RegionId);
