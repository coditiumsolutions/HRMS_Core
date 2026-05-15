using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HRMBT.Web.Controllers;

public class EmployeeDocumentsController : Controller
{
    private readonly IEmployeeDocumentService _documentService;

    public EmployeeDocumentsController(IEmployeeDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
    [RequestSizeLimit(104857600)]
    public async Task<IActionResult> Upload(
        [FromForm] int employeeId,
        [FromForm] List<IFormFile>? files,
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

        return RedirectToAction("Edit", "Employee", new { id = employeeId, tab = "attachments" });
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var file = await _documentService.GetDownloadAsync(id, cancellationToken);
        if (file == null)
            return NotFound();

        return File(file.Stream, file.ContentType, file.DownloadFileName);
    }

    [HttpPost]
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
        if (!string.IsNullOrEmpty(referer) && referer.Contains("/Employee/Details/", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("Details", "Employee", new { id = employeeId, tab = "attachments" });
        }

        return RedirectToAction("Edit", "Employee", new { id = employeeId, tab = "attachments" });
    }
}
