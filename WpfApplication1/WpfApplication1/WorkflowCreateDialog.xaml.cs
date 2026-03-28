using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1
{
    public partial class WorkflowCreateDialog : Window
    {
        public WorkflowCreateDialog()
        {
            InitializeComponent();
            WorkflowTypeComboBox.ItemsSource = new[]
            {
                new WorkflowTypeOption(WorkflowType.Apply, "申请流程"),
                new WorkflowTypeOption(WorkflowType.Approval, "审批流程"),
                new WorkflowTypeOption(WorkflowType.Query, "查询流程"),
                new WorkflowTypeOption(WorkflowType.IntegratedScheduler, "调度编排模板"),
                new WorkflowTypeOption(WorkflowType.General, "通用流程"),
                new WorkflowTypeOption(WorkflowType.Subflow, "子流程")
            };
            WorkflowTypeComboBox.SelectedIndex = 0;
            Loaded += WorkflowCreateDialog_Loaded;
        }

        public WorkflowCreateRequest Request { get; private set; }

        private void WorkflowCreateDialog_Loaded(object sender, RoutedEventArgs e)
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        }

        private void CreateButton_OnClick(object sender, RoutedEventArgs e)
        {
            var workflowType = WorkflowTypeComboBox.SelectedValue is WorkflowType
                ? (WorkflowType)WorkflowTypeComboBox.SelectedValue
                : WorkflowType.General;
            var workflowName = (NameTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(workflowName))
            {
                MessageBox.Show(this, "请输入流程名称。", "新建流程", MessageBoxButton.OK, MessageBoxImage.Information);
                NameTextBox.Focus();
                return;
            }

            Request = new WorkflowCreateRequest
            {
                WorkflowType = workflowType,
                Name = workflowName,
                Description = (DescriptionTextBox.Text ?? string.Empty).Trim(),
                ApplicableRole = (RoleTextBox.Text ?? string.Empty).Trim()
            };
            DialogResult = true;
            Close();
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private class WorkflowTypeOption
        {
            public WorkflowTypeOption(WorkflowType value, string displayName)
            {
                Value = value;
                DisplayName = displayName;
            }

            public WorkflowType Value { get; private set; }

            public string DisplayName { get; private set; }
        }
    }
}
