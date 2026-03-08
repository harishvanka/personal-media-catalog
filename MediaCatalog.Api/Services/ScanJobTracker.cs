using System.Collections.Concurrent;
using MediaCatalog.Api.Models;

namespace MediaCatalog.Api.Services
{
    /// <summary>
    /// Singleton store for active/completed scan jobs.
    /// Jobs survive the request lifetime but are cleared on app restart.
    /// </summary>
    public sealed class ScanJobTracker
    {
        private readonly ConcurrentDictionary<Guid, ScanJob> _jobs = new();

        public ScanJob Create(int driveId)
        {
            var job = new ScanJob { DriveId = driveId };
            _jobs[job.Id] = job;
            return job;
        }

        public ScanJob? Get(Guid id) => _jobs.GetValueOrDefault(id);

        public IReadOnlyList<ScanJob> GetForDrive(int driveId) =>
            _jobs.Values.Where(j => j.DriveId == driveId).ToList();
    }
}
