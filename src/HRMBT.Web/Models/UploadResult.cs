using System.Collections.Generic;

namespace HRMBT.Web.Models
{
    public class UploadResult
    {
        public int TotalRows { get; set; }
        public int SuccessfulRows { get; set; }
        public int FailedRows { get; set; }
        public List<string> ErrorMessages { get; set; } = new List<string>();
        public int UploadLogId { get; set; }
    }
}

