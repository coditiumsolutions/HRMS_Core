using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HRMBT.Web.Models.ViewModels;

public class EmployeeDocumentsViewModel
{
    public int EmployeeId { get; set; }
    public string? EmployeeDisplayName { get; set; }
    public bool CanUpload { get; set; }
    public long MaxFileSizeBytes { get; set; }
    public int MaxFilesPerUpload { get; set; }
    public string AllowedExtensionsDisplay { get; set; } = string.Empty;
    public IReadOnlyList<EmployeeDocumentItemViewModel> Documents { get; set; } = Array.Empty<EmployeeDocumentItemViewModel>();
    public EmployeeDocumentUploadViewModel Upload { get; set; } = new();
}

public class EmployeeDocumentItemViewModel
{
    public int DocumentId { get; set; }
    public int EmployeeId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileSizeDisplay { get; set; } = string.Empty;
    public DateTime UploadedOn { get; set; }
    public string UploadedOnDisplay { get; set; } = string.Empty;
}

public class EmployeeDocumentUploadViewModel
{
    public int EmployeeId { get; set; }

    [Display(Name = "Documents")]
    public List<IFormFile>? Files { get; set; }
}

public class EmployeeDocumentUploadResult
{
    public bool Success { get; init; }
    public int UploadedCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public class EmployeeDocumentDownloadResult
{
    public required Stream Stream { get; init; }
    public required string ContentType { get; init; }
    public required string DownloadFileName { get; init; }
}
