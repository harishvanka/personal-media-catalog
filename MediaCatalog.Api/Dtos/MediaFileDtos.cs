namespace MediaCatalog.Api.Dtos
{
    public record MediaFileDto(int Id, string DriveLabel, string RelativePath, long SizeBytes, string Extension, string ContentHash, string Category, DateTime? CreatedAtFs, DateTime? ModifiedAtFs);
}