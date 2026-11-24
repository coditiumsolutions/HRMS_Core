using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HRMBT.Web.Controllers;

[Route("employee-documents")]
public class EmployeeDocumentsController : Controller
{
    private readonly IEmployeeDocumentService _documentService;

    public EmployeeDocumentsController(IEmployeeDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpPost("upload")]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
    [RequestSizeLimit(104857600)]
    public async Task<IActionResult> Upload(
        [FromForm] int employeeId,
        [FromForm] List<IFormFile>? files,
        [FromForm] string? returnTo,
        CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
        {
            TempData["DocumentError"] = "Invalid employee reference.";
            return RedirectToAction("Index", "Employee");
        }

        var fileList = files?.Where(f => f.Length > 0).ToList() ?? new List<IFormFile>();
        var result = await _documentService.UploadAsync(employeeId, fileList, cancellationToken);

        if (result.UploadedCount > 0)
        {
            TempData["DocumentSuccess"] = result.UploadedCount == 1
                ? "1 document uploaded successfully."
                : $"{result.UploadedCount} documents uploaded successfully.";
        }

        if (result.Errors.Count > 0)
        {
            TempData["DocumentError"] = string.Join(" ", result.Errors);
        }
        else if (result.UploadedCount == 0)
        {
            TempData["DocumentError"] = "No files were uploaded. Choose one or more allowed file types.";
        }

        return BackToEmployee(employeeId, returnTo);
    }

    private IActionResult BackToEmployee(int employeeId, string? returnTo) =>
        string.Equals(returnTo, "details", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction("Details", "Employee", new { id = employeeId, tab = "attachments" })
            : RedirectToAction("Edit", "Employee", new { id = employeeId, tab = "attachments" });

    [HttpPost("photo")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20971520)]
    public async Task<IActionResult> UploadPhoto([FromForm] int employeeId, IFormFile? photo, [FromForm] string? returnTo, CancellationToken cancellationToken)
    {
        if (employeeId <= 0 || !await _documentService.EmployeeExistsAsync(employeeId, cancellationToken))
        {
            TempData["DocumentError"] = "Invalid employee reference.";
            return RedirectToAction("Index", "Employee");
        }

        var result = await _documentService.SetProfilePhotoAsync(employeeId, photo!, cancellationToken);
        if (result.UploadedCount > 0)
            TempData["DocumentSuccess"] = "Profile photo updated.";
        else
            TempData["DocumentError"] = result.Errors.Count > 0 ? string.Join(" ", result.Errors) : "The photo could not be saved.";

        return BackToEmployee(employeeId, returnTo);
    }

    [HttpPost("photo/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePhoto([FromForm] int employeeId, [FromForm] string? returnTo, CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
        {
            TempData["DocumentError"] = "Invalid employee reference.";
            return RedirectToAction("Index", "Employee");
        }

        TempData["DocumentSuccess"] = await _documentService.RemoveProfilePhotoAsync(employeeId, cancellationToken)
            ? "Profile photo removed."
            : "There was no profile photo to remove.";
        return BackToEmployee(employeeId, returnTo);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var file = await _documentService.GetDownloadAsync(id, cancellationToken);
        if (file == null)
            return NotFound();

        return File(file.Stream, file.ContentType, file.DownloadFileName);
    }

    [HttpPost("{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int employeeId, CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
        {
            TempData["DocumentError"] = "Invalid employee reference.";
            return RedirectToAction("Index", "Employee");
        }

        var deleted = await _documentService.DeleteAsync(id, cancellationToken);
        TempData[deleted ? "DocumentSuccess" : "DocumentError"] = deleted
            ? "Document deleted successfully."
            : "Document could not be deleted.";

        var referer = Request.Headers.Referer.ToString();
        if (!string.IsNullOrEmpty(referer) && referer.Contains("/employees/", StringComparison.OrdinalIgnoreCase) && !referer.Contains("/edit", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("Details", "Employee", new { id = employeeId, tab = "attachments" });
        }

        return RedirectToAction("Edit", "Employee", new { id = employeeId, tab = "attachments" });
    }
}
