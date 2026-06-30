using Spotster.DTOs;
using Spotster.Entities;

namespace Spotster.Services;

public interface IParkingService
{
    Task<ParkingReportDto> CreateReportAsync(Guid userId, CreateParkingReportRequest request, IFormFile? photo, string? ipAddress, string? userAgent);
    Task<IReadOnlyList<ParkingReportDto>> GetActiveAsync();
    Task<IReadOnlyList<ParkingReportDto>> GetMyActiveAsync(Guid userId);
    Task<PagedResult<ParkingReportDto>> GetNearbyAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        int page,
        int pageSize,
        Guid? viewerUserId = null);
    Task<ParkingReportDto> VoteAsync(Guid userId, VoteRequest request);
    Task DeleteReportAsync(Guid userId, Guid reportId);
    Task ProcessExpiredReportsAsync();
}
