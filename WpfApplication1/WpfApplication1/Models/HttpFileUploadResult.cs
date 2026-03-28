namespace WpfApplication1.Models
{
    public class HttpFileUploadResult
    {
        public bool IsSuccess { get; set; }

        public int StatusCode { get; set; }

        public string ResponseText { get; set; }

        public string FilePath { get; set; }

        public string Url { get; set; }
    }
}