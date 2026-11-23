namespace HRMBT.Web.Services;

public class EmployeeDocumentOptions
{
    public const string SectionName = "EmployeeDocuments";

    public string UploadRoot { get; set; } = "uploads/employees";

    /// <summary>
    /// Folder that UploadRoot is relative to. Empty = the web root (old behaviour). Set it to a folder outside the
    /// site (absolute path, or relative to the content root) so uploads survive a deploy and are never web-served.
    /// </summary>
    public string StorageBasePath { get; set; } = string.Empty;

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    public int MaxFilesPerUpload { get; set; } = 10;

    public string[] AllowedExtensions { get; set; } =
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg"
    };
}
