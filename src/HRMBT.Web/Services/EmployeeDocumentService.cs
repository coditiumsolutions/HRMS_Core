using System.Globalization;
using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRMBT.Web.Services;

public class EmployeeDocumentService : IEmployeeDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly EmployeeDocumentOptions _options;
    private readonly ILogger<EmployeeDocumentService> _logger;

    public EmployeeDocumentService(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        IOptions<EmployeeDocumentOptions> options,
        ILogger<EmployeeDocumentService> logger)
    {
        _context = context;
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public Task<bool> EmployeeExistsAsync(int employeeUid, CancellationToken cancellationToken = default) =>
        _context.Employees.AsNoTracking().AnyAsync(e => e.uid == employeeUid, cancellationToken);

    public async Task<EmployeeDocumentsViewModel> BuildViewModelAsync(
        int employeeUid,
        bool canUpload,
        CancellationToken cancellationToken = default)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.uid == employeeUid, cancellationToken);

        var documents = await _context.EmployeeDocuments
            .AsNoTracking()
            .Where(d => d.EmployeeId == employeeUid && !d.IsDeleted)
            .OrderByDescending(d => d.UploadedOn)
            .Select(d => new EmployeeDocumentItemViewModel
            {
                DocumentId = d.Id,
                EmployeeId = d.EmployeeId,
                FileName = d.FileName,
                OriginalFileName = d.OriginalFileName ?? d.FileName,
                FileExtension = d.FileExtension ?? string.Empty,
                FileSize = d.FileSize ?? 0,
                FileSizeDisplay = string.Empty,
                UploadedOn = d.UploadedOn ?? DateTime.MinValue,
                UploadedOnDisplay = string.Empty
            })
            .ToListAsync(cancellationToken);

        foreach (var doc in documents)
        {
            doc.FileSizeDisplay = FormatFileSize(doc.FileSize);
            doc.UploadedOnDisplay = doc.UploadedOn == DateTime.MinValue
                ? "—"
                : doc.UploadedOn.ToString("dd MMM yyyy HH:mm", CultureInfo.CurrentCulture);
        }

        return new EmployeeDocumentsViewModel
        {
            EmployeeId = employeeUid,
            EmployeeDisplayName = employee?.EmployeeName,
            CanUpload = canUpload,
            MaxFileSizeBytes = _options.MaxFileSizeBytes,
            MaxFilesPerUpload = _options.MaxFilesPerUpload,
            AllowedExtensionsDisplay = string.Join(", ", _options.AllowedExtensions),
            Documents = documents,
            Upload = new EmployeeDocumentUploadViewModel { EmployeeId = employeeUid }
        };
    }

    public async Task<EmployeeDocumentUploadResult> UploadAsync(
        int employeeUid,
        IEnumerable<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!await EmployeeExistsAsync(employeeUid, cancellationToken))
        {
            return new EmployeeDocumentUploadResult
            {
                Success = false,
                Errors = new[] { "Employee record was not found." }
            };
        }

        var fileList = files?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
        if (fileList.Count == 0)
        {
            return new EmployeeDocumentUploadResult
            {
                Success = false,
                Errors = new[] { "Select at least one file to upload." }
            };
        }

        if (fileList.Count > _options.MaxFilesPerUpload)
        {
            return new EmployeeDocumentUploadResult
            {
                Success = false,
                Errors = new[] { $"You can upload at most {_options.MaxFilesPerUpload} file(s) at a time." }
            };
        }

        var allowed = new HashSet<string>(
            _options.AllowedExtensions.Select(NormalizeExtension),
            StringComparer.OrdinalIgnoreCase);

        var employeeFolder = GetEmployeePhysicalDirectory(employeeUid);
        Directory.CreateDirectory(employeeFolder);

        var entities = new List<EmployeeDocument>();
        var uploaded = 0;

        foreach (var file in fileList)
        {
            var validationError = ValidateFile(file, allowed);
            if (validationError != null)
            {
                errors.Add($"{file.FileName}: {validationError}");
                continue;
            }

            var originalName = Path.GetFileName(file.FileName);
            var extension = NormalizeExtension(Path.GetExtension(originalName));
            var storedName = $"{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(employeeFolder, storedName);
            var relativePath = CombineRelativePath(employeeUid, storedName);

            try
            {
                await using (var stream = new FileStream(
                    physicalPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                entities.Add(new EmployeeDocument
                {
                    EmployeeId = employeeUid,
                    FileName = storedName,
                    OriginalFileName = originalName,
                    FilePath = relativePath,
                    FileExtension = string.IsNullOrEmpty(extension) ? null : extension,
                    FileSize = file.Length,
                    UploadedOn = DateTime.Now,
                    IsDeleted = false
                });
                uploaded++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save file {FileName} for employee {EmployeeId}", originalName, employeeUid);
                errors.Add($"{originalName}: could not be saved.");
                if (System.IO.File.Exists(physicalPath))
                {
                    try { System.IO.File.Delete(physicalPath); } catch { /* ignore */ }
                }
            }
        }

        if (entities.Count > 0)
        {
            _context.EmployeeDocuments.AddRange(entities);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new EmployeeDocumentUploadResult
        {
            Success = uploaded > 0 && errors.Count == 0,
            UploadedCount = uploaded,
            Errors = errors
        };
    }

    public async Task<EmployeeDocumentDownloadResult?> GetDownloadAsync(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _context.EmployeeDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, cancellationToken);

        if (document == null)
            return null;

        var physicalPath = GetPhysicalPath(document.FilePath);
        if (!System.IO.File.Exists(physicalPath))
            return null;

        var stream = new FileStream(
            physicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return new EmployeeDocumentDownloadResult
        {
            Stream = stream,
            ContentType = GetContentType(document.FileExtension),
            DownloadFileName = document.OriginalFileName ?? document.FileName
        };
    }

    public async Task<bool> DeleteAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var document = await _context.EmployeeDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, cancellationToken);

        if (document == null)
            return false;

        document.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private string? ValidateFile(IFormFile file, HashSet<string> allowed)
    {
        if (file.Length <= 0)
            return "File is empty.";

        if (file.Length > _options.MaxFileSizeBytes)
            return $"File exceeds maximum size of {FormatFileSize(_options.MaxFileSizeBytes)}.";

        var extension = NormalizeExtension(Path.GetExtension(file.FileName));
        if (string.IsNullOrEmpty(extension) || !allowed.Contains(extension))
            return $"File type is not allowed. Allowed: {string.Join(", ", allowed)}.";

        return null;
    }

    private string GetEmployeePhysicalDirectory(int employeeUid) =>
        Path.Combine(_environment.WebRootPath, _options.UploadRoot.Replace('/', Path.DirectorySeparatorChar), employeeUid.ToString(CultureInfo.InvariantCulture));

    private string CombineRelativePath(int employeeUid, string storedName) =>
        $"{_options.UploadRoot.TrimEnd('/')}/{employeeUid}/{storedName}".Replace('\\', '/');

    private string GetPhysicalPath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_environment.WebRootPath, normalized);
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;
        extension = extension.Trim().ToLowerInvariant();
        return extension.StartsWith('.') ? extension : "." + extension;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.##} KB";
        return $"{bytes / (1024.0 * 1024.0):0.##} MB";
    }

    private static string GetContentType(string? extension) => (extension ?? string.Empty).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };
}
