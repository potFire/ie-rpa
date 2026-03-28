using System.Threading.Tasks;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public interface ISchedulerStateStore
    {
        Task SaveAsync(string path, LocalSchedulerState state);

        Task<LocalSchedulerState> LoadAsync(string path);
    }
}
