namespace MediaCatalog.Api.Dtos
{
    public record MediaFileDto(int Id, string DriveLabel, string RelativePath, long SizeBytes, string Extension, string ContentHash, string Category, DateTime? CreatedAtFs, DateTime? ModifiedAtFs);

    public record DuplicateSummaryDto(int TotalGroups, int TotalDuplicateFiles, long WastedBytes);

    public record DuplicateGroupDto(string ContentHash, long SizeBytes, List<MediaFileDto> Files);

    // Module 4 — File Organization Engine
    public record OrganizeSuggestionDto(int FileId, string CurrentPath, string SuggestedPath, bool AlreadyOrganized);

    public record MoveResultDto(int FileId, string SourcePath, string TargetPath, bool DryRun, bool Success, string? Message);
}