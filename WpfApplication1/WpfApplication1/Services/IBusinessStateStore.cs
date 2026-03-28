using System.Threading.Tasks;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public interface IBusinessStateStore
    {
        Task SaveAsync(string path, BusinessStateRecord state);

        Task<BusinessStateRecord> LoadAsync(string path);
    }
}