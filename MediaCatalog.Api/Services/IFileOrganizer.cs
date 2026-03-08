using MediaCatalog.Api.Dtos;
using MediaCatalog.Api.Models;

namespace MediaCatalog.Api.Services
{
    public interface IFileOrganizer
    {
        /// <summary>Returns the suggested relative path for a file based on its category and name.</summary>
        string SuggestRelativePath(MediaFile file);

        /// <summary>Moves the file on disk and updates the DB record. If dryRun is true, nothing is written.</summary>
        Task<MoveResultDto> MoveAsync(int fileId, bool dryRun, CancellationToken ct = default);
    }
}
