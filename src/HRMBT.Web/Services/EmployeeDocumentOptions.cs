namespace HRMBT.Web.Services;

public class EmployeeDocumentOptions
{
    public const string SectionName = "EmployeeDocuments";

    public string UploadRoot { get; set; } = "uploads/employees";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    public int MaxFilesPerUpload { get; set; } = 10;

    public string[] AllowedExtensions { get; set; } =
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg"
    };
}
