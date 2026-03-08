namespace MediaCatalog.Api.Dtos
{
    public record DriveDto(int Id, string Label, string RootPath, string? Serial, DateTime? LastScannedAt);

    // Includes aggregated file stats — returned by GET /api/drives
    public record DriveStatsDto(
        int Id,
        string Label,
        string RootPath,
        string? Serial,
        DateTime? LastScannedAt,
        int FileCount,
        long TotalBytes);

    public record CreateDriveDto(string Label, string Path, string? Serial);
}