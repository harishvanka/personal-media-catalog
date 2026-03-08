using MediaCatalog.Api.Models;

namespace MediaCatalog.Api.Services
{
    public interface IDriveScanner
    {
        /// <summary>
        /// Enqueues a scan of <paramref name="driveId"/>.
        /// The provided <paramref name="job"/> is updated in-place as the scan runs.
        /// Returns immediately; actual work happens on the background service.
        /// </summary>
        Task EnqueueAsync(int driveId, ScanJob job, CancellationToken cancellationToken = default);
    }
}
