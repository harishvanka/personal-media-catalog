namespace MediaCatalog.Api.Dtos
{
    public record MediaFileDto(int Id, string DriveLabel, string RelativePath, long SizeBytes, string Extension, string ContentHash, string Category, DateTime? CreatedAtFs, DateTime? ModifiedAtFs);

    public record DuplicateSummaryDto(int TotalGroups, int TotalDuplicateFiles, long WastedBytes);

    public record DuplicateGroupDto(string ContentHash, long SizeBytes, List<MediaFileDto> Files);
}