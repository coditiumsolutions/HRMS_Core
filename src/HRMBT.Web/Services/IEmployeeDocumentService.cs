using HRMBT.Web.Models.ViewModels;

namespace HRMBT.Web.Services;

public interface IEmployeeDocumentService
{
    Task<EmployeeDocumentsViewModel> BuildViewModelAsync(int employeeUid, bool canUpload, CancellationToken cancellationToken = default);

    Task<EmployeeDocumentUploadResult> UploadAsync(int employeeUid, IEnumerable<IFormFile> files, CancellationToken cancellationToken = default);

    Task<EmployeeDocumentDownloadResult?> GetDownloadAsync(int documentId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int documentId, CancellationToken cancellationToken = default);

    Task<bool> EmployeeExistsAsync(int employeeUid, CancellationToken cancellationToken = default);
}
