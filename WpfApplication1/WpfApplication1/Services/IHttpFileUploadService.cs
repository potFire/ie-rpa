using System.Threading;
using System.Threading.Tasks;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public interface IHttpFileUploadService
    {
        Task<HttpFileUploadResult> UploadAsync(string url, string filePath, int timeoutMs, CancellationToken cancellationToken);
    }
}