using System.Threading;
using System.Threading.Tasks;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public interface IIntegratedSchedulerService
    {
        bool IsRunning { get; }

        LocalSchedulerState CurrentState { get; }

        Task StartAsync(SchedulerSettings settings, SchedulerExecutionContext executionContext, CancellationToken cancellationToken);

        Task ExecuteSingleRoundAsync(SchedulerSettings settings, SchedulerExecutionContext executionContext, CancellationToken cancellationToken);

        void RequestStop();
    }
}
