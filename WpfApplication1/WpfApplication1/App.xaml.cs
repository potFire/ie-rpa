using System;
using System.IO;
using System.Windows;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.ViewModels;

namespace WpfApplication1
{
    public partial class App : Application
    {
        private readonly IApplicationStateStore _applicationStateStore = new XmlApplicationStateStore();
        private readonly IBusinessStateStore _businessStateStore = new XmlBusinessStateStore();

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var designerStatePath = GetDesignerStateFilePath();
            var businessStatePath = GetBusinessStateFilePath();
            var state = new ApplicationState();
            if (File.Exists(designerStatePath))
            {
                try
                {
                    state = await _applicationStateStore.LoadAsync(designerStatePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("鍔犺浇鏈湴 XML 鐘舵€佸け璐ワ紝灏嗕娇鐢ㄩ粯璁ら厤缃户缁惎鍔ㄣ€俓r\n" + ex.Message,
                        "鍚姩鎻愮ず",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            BusinessStateRecord pendingBusinessState = null;
            var shouldPromptResume = state != null && state.SchedulerSettings != null
                ? state.SchedulerSettings.ResumePromptOnStartup
                : true;
            if (shouldPromptResume && File.Exists(businessStatePath))
            {
                try
                {
                    var businessState = await _businessStateStore.LoadAsync(businessStatePath);
                    if (businessState != null && !businessState.IsCompleted)
                    {
                        var message = string.Format(
                            "检测到本机存在未完成业务记录。\r\n姓名：{0}\r\n证件号：{1}\r\n阶段：{2}\r\n最后步骤：{3}\r\n\r\n是否定位到可恢复步骤并保留恢复信息？",
                            string.IsNullOrWhiteSpace(businessState.Name) ? "-" : businessState.Name,
                            string.IsNullOrWhiteSpace(businessState.IdCardNumber) ? "-" : businessState.IdCardNumber,
                            businessState.Stage,
                            string.IsNullOrWhiteSpace(businessState.LastStepName) ? "-" : businessState.LastStepName);
                        if (MessageBox.Show(message, "业务恢复提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            pendingBusinessState = businessState;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("加载业务恢复 XML 失败，将忽略恢复记录继续启动。\r\n" + ex.Message,
                        "启动提示",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            if (string.IsNullOrWhiteSpace(state.EmployeeId))
            {
                var dialog = new EmployeeIdDialog(null, true);
                var result = dialog.ShowDialog();
                if (result != true)
                {
                    Shutdown();
                    return;
                }

                state.EmployeeId = dialog.EmployeeId;
            }

            var viewModel = new MainWindowViewModel(_applicationStateStore, designerStatePath, state, _businessStateStore, businessStatePath, pendingBusinessState);
            var window = new MainWindow(viewModel);
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
        }

        private static string GetDesignerStateFilePath()
        {
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "State");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "designer-state.xml");
        }

        private static string GetBusinessStateFilePath()
        {
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "State");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "business-state.xml");
        }
    }
}
