using HRMBT.Web.Models.ViewModels;

namespace HRMBT.Web.Services;

public interface IEmployeeDocumentService
{
    Task<EmployeeDocumentsViewModel> BuildViewModelAsync(int employeeUid, bool canUpload, CancellationToken cancellationToken = default);

    Task<EmployeeDocumentUploadResult> UploadAsync(int employeeUid, IEnumerable<IFormFile> files, CancellationToken cancellationToken = default, string? documentType = null);

    /// <summary>Stores one image as the employee profile photo and retires the previous one.</summary>
    Task<EmployeeDocumentUploadResult> SetProfilePhotoAsync(int employeeUid, IFormFile photo, CancellationToken cancellationToken = default);

    Task<bool> RemoveProfilePhotoAsync(int employeeUid, CancellationToken cancellationToken = default);

    Task<EmployeeDocumentDownloadResult?> GetDownloadAsync(int documentId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int documentId, CancellationToken cancellationToken = default);

    Task<bool> EmployeeExistsAsync(int employeeUid, CancellationToken cancellationToken = default);
}
