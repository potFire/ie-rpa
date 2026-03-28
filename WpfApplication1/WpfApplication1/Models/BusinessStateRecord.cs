using System;
using WpfApplication1.Enums;

namespace WpfApplication1.Models
{
    public class BusinessStateRecord
    {
        public string Name { get; set; }

        public string IdCardNumber { get; set; }

        public string Reason { get; set; }

        public string SourceUrl { get; set; }

        public DateTime? FetchedAt { get; set; }

        public BusinessStateStage Stage { get; set; }

        public string MachineRole { get; set; }

        public string LastStepName { get; set; }

        public DateTime? LastUpdatedAt { get; set; }

        public string ErrorMessage { get; set; }

        public int RetryCount { get; set; }

        public string HtmlFilePath { get; set; }

        public string UploadResult { get; set; }

        public DateTime? UploadedAt { get; set; }

        public bool IsCompleted { get; set; }

        public BusinessStateRecord Clone()
        {
            return (BusinessStateRecord)MemberwiseClone();
        }
    }
}