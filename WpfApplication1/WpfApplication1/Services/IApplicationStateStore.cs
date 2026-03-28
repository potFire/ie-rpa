using System.Threading.Tasks;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public interface IApplicationStateStore
    {
        Task SaveAsync(string path, ApplicationState state);

        Task<ApplicationState> LoadAsync(string path);
    }
}
