using System.Windows;

namespace WpfApplication1
{
    public partial class EmployeeIdDialog : Window
    {
        private readonly bool _isRequired;

        public EmployeeIdDialog(string currentEmployeeId, bool isRequired)
        {
            InitializeComponent();
            _isRequired = isRequired;
            EmployeeIdTextBox.Text = currentEmployeeId ?? string.Empty;
            CancelButton.Visibility = _isRequired ? Visibility.Collapsed : Visibility.Visible;
            Loaded += EmployeeIdDialog_Loaded;
        }

        public string EmployeeId { get; private set; }

        private void EmployeeIdDialog_Loaded(object sender, RoutedEventArgs e)
        {
            EmployeeIdTextBox.Focus();
            EmployeeIdTextBox.SelectAll();
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            var employeeId = (EmployeeIdTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                MessageBox.Show(this, "请输入当前工号。", "用户中心", MessageBoxButton.OK, MessageBoxImage.Information);
                EmployeeIdTextBox.Focus();
                return;
            }

            EmployeeId = employeeId;
            DialogResult = true;
            Close();
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isRequired)
            {
                return;
            }

            DialogResult = false;
            Close();
        }
    }
}
