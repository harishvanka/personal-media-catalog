using System.Text.RegularExpressions;
using MediaCatalog.Api.Data;
using MediaCatalog.Api.Dtos;
using MediaCatalog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaCatalog.Api.Services
{
    public class FileOrganizer : IFileOrganizer
    {
        private static readonly Regex YearRegex = new(@"\b(19\d{2}|20\d{2})\b", RegexOptions.Compiled);

        private static readonly string[] KnownPlatforms =
            ["Udemy", "Coursera", "Pluralsight", "PluralSight", "LinkedIn", "Tutorial", "Course"];

        private readonly MediaCatalogContext _db;

        public FileOrganizer(MediaCatalogContext db) => _db = db;

        public string SuggestRelativePath(MediaFile file)
        {
            var fileName = Path.GetFileName(file.RelativePath);
            var ext = file.Extension.TrimStart('.');

            return file.Category switch
            {
                "Movie"    => SuggestMoviePath(fileName),
                "Document" => $"Docs/{ext}/{fileName}",
                "Photo"    => SuggestPhotoPath(file, fileName),
                "Tutorial" => SuggestTutorialPath(file.RelativePath, fileName),
                _          => $"Other/{fileName}"
            };
        }

        // Movies/{Year}/{Clean Title}.ext  — or  Movies/Unknown/{Clean Title}.ext
        private static string SuggestMoviePath(string fileName)
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            var ext  = Path.GetExtension(fileName);
            var match = YearRegex.Match(stem);

            if (match.Success)
            {
                var year = match.Value;
                var title = stem[..match.Index]
                    .Replace('.', ' ')
                    .Replace('_', ' ')
                    .Replace('-', ' ')
                    .Trim();
                if (string.IsNullOrWhiteSpace(title)) title = stem;
                return $"Movies/{year}/{title}{ext}";
            }

            var cleanTitle = stem.Replace('.', ' ').Replace('_', ' ').Trim();
            return $"Movies/Unknown/{cleanTitle}{ext}";
        }

        // Photos/{Year}/{filename}  — year from filesystem timestamps
        private static string SuggestPhotoPath(MediaFile file, string fileName)
        {
            var year = (file.CreatedAtFs ?? file.ModifiedAtFs)?.Year.ToString() ?? "Unknown";
            return $"Photos/{year}/{fileName}";
        }

        // Tutorials/{Platform}/{rest-of-path}  preserving sub-folder structure under the platform
        private static string SuggestTutorialPath(string relativePath, string fileName)
        {
            var parts = relativePath.Replace('\\', '/').Split('/');

            foreach (var platform in KnownPlatforms)
            {
                for (var i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Contains(platform, StringComparison.OrdinalIgnoreCase))
                    {
                        // Normalise the platform name to one of our canonical names
                        var canonical = KnownPlatforms.First(p =>
                            parts[i].Contains(p, StringComparison.OrdinalIgnoreCase));
                        // Keep everything from the platform segment onwards
                        var rest = string.Join("/", parts[(i + 1)..]);
                        return string.IsNullOrEmpty(rest)
                            ? $"Tutorials/{canonical}/{fileName}"
                            : $"Tutorials/{canonical}/{rest}";
                    }
                }
            }

            // No known platform found — group under parent folder name
            var parent = parts.Length > 1 ? parts[^2] : "Unknown";
            return $"Tutorials/Other/{parent}/{fileName}";
        }

        public async Task<MoveResultDto> MoveAsync(int fileId, bool dryRun, CancellationToken ct = default)
        {
            var file = await _db.MediaFiles
                .Include(f => f.Drive)
                .FirstOrDefaultAsync(f => f.Id == fileId, ct);

            if (file == null)
                return new MoveResultDto(fileId, "", "", dryRun, false, "File not found.");

            var drive       = file.Drive;
            var sourcePath  = Path.Combine(drive.RootPath, file.RelativePath);
            var suggestedRel = SuggestRelativePath(file);
            var targetPath  = Path.Combine(drive.RootPath, suggestedRel);

            // Already in the right place
            if (string.Equals(
                    Path.GetFullPath(sourcePath),
                    Path.GetFullPath(targetPath),
                    StringComparison.OrdinalIgnoreCase))
                return new MoveResultDto(fileId, sourcePath, targetPath, dryRun, true,
                    "File is already in the correct location.");

            if (dryRun)
                return new MoveResultDto(fileId, sourcePath, targetPath, true, true, null);

            try
            {
                if (!File.Exists(sourcePath))
                    return new MoveResultDto(fileId, sourcePath, targetPath, false, false,
                        "Source file not found on disk.");

                if (File.Exists(targetPath))
                    return new MoveResultDto(fileId, sourcePath, targetPath, false, false,
                        "A file already exists at the target path.");

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Move(sourcePath, targetPath);

                file.RelativePath = suggestedRel;
                await _db.SaveChangesAsync(ct);

                return new MoveResultDto(fileId, sourcePath, targetPath, false, true, null);
            }
            catch (Exception ex)
            {
                return new MoveResultDto(fileId, sourcePath, targetPath, false, false, ex.Message);
            }
        }
    }
}
