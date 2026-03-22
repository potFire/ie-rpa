using System.Text;
using System.Windows;
using System.Windows.Input;
using WpfApplication1.Models;
using WpfApplication1.ViewModels;

namespace WpfApplication1
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private void LogsDataGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var entry = LogsDataGrid.SelectedItem as ExecutionLogEntry;
            if (entry == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("时间: " + entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("级别: " + entry.Level);
            builder.AppendLine("步骤: " + (string.IsNullOrWhiteSpace(entry.StepName) ? "-" : entry.StepName));
            builder.AppendLine("消息: " + (entry.Message ?? string.Empty));

            if (!string.IsNullOrWhiteSpace(entry.ScreenshotPath))
            {
                builder.AppendLine("截图路径: " + entry.ScreenshotPath);
            }

            Clipboard.SetText(builder.ToString());
        }
    }
}
