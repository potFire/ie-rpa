using System.Collections.Generic;
using System.Threading.Tasks;

namespace WpfApplication1.Automation.IE
{
    public interface IIeBrowserService
    {
        Task<IIePage> LaunchAsync(string url, int timeoutMs);

        Task<IIePage> AttachAsync(int timeoutMs);

        IList<IIePage> GetAllPages();
    }
}
