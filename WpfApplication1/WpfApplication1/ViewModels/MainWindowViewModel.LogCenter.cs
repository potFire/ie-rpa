using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using WpfApplication1.Commands;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;

namespace WpfApplication1.ViewModels
{
    public partial class MainWindowViewModel
    {
        private readonly IWorkflowLogService _workflowLogService = new WorkflowLogService();
        private ObservableCollection<WorkflowLogWorkflowItem> _loggedWorkflows;
        private ICollectionView _loggedWorkflowsView;
        private ObservableCollection<WorkflowLogRunItem> _workflowLogRuns;
        private WorkflowLogWorkflowItem _selectedLogWorkflow;
        private WorkflowLogRunItem _selectedWorkflowLogRun;
        private string _selectedWorkflowLogText;
        private string _selectedWorkflowLogSummaryText;
        private string _logCenterSearchText;
        private WorkflowType? _selectedLogFilterType;
        private ObservableCollection<StepParameterOption> _logCenterFilterOptions;

        public ObservableCollection<WorkflowLogWorkflowItem> LoggedWorkflows
        {
            get { return _loggedWorkflows; }
            private set { SetProperty(ref _loggedWorkflows, value); }
        }

        public ICollectionView LoggedWorkflowsView
        {
            get { return _loggedWorkflowsView; }
            private set { SetProperty(ref _loggedWorkflowsView, value); }
        }

        public ObservableCollection<WorkflowLogRunItem> WorkflowLogRuns
        {
            get { return _workflowLogRuns; }
            private set { SetProperty(ref _workflowLogRuns, value); }
        }

        public WorkflowLogWorkflowItem SelectedLogWorkflow
        {
            get { return _selectedLogWorkflow; }
            set
            {
                if (SetProperty(ref _selectedLogWorkflow, value))
                {
                    if (ExportSelectedLogCommand != null) ExportSelectedLogCommand.RaiseCanExecuteChanged();
                    if (CopySelectedLogSummaryCommand != null) CopySelectedLogSummaryCommand.RaiseCanExecuteChanged();
                    LoadRunsForSelectedWorkflowAsync().GetAwaiter().GetResult();
                    ScheduleStateSave();
                }
            }
        }

        public WorkflowLogRunItem SelectedWorkflowLogRun
        {
            get { return _selectedWorkflowLogRun; }
            set
            {
                if (SetProperty(ref _selectedWorkflowLogRun, value))
                {
                    if (ExportSelectedLogCommand != null) ExportSelectedLogCommand.RaiseCanExecuteChanged();
                    if (CopySelectedLogSummaryCommand != null) CopySelectedLogSummaryCommand.RaiseCanExecuteChanged();
                    LoadSelectedRunTextAsync().GetAwaiter().GetResult();
                    ScheduleStateSave();
                }
            }
        }

        public string SelectedWorkflowLogText
        {
            get { return _selectedWorkflowLogText; }
            private set { SetProperty(ref _selectedWorkflowLogText, value); }
        }

        public string SelectedWorkflowLogSummaryText
        {
            get { return _selectedWorkflowLogSummaryText; }
            private set { SetProperty(ref _selectedWorkflowLogSummaryText, value); }
        }

        public string LogCenterSearchText
        {
            get { return _logCenterSearchText; }
            set
            {
                if (SetProperty(ref _logCenterSearchText, value) && LoggedWorkflowsView != null)
                {
                    LoggedWorkflowsView.Refresh();
                    ScheduleStateSave();
                }
            }
        }

        public ObservableCollection<StepParameterOption> LogCenterFilterOptions
        {
            get { return _logCenterFilterOptions; }
            private set { SetProperty(ref _logCenterFilterOptions, value); }
        }

        public WorkflowType? SelectedLogFilterType
        {
            get { return _selectedLogFilterType; }
            set
            {
                if (SetProperty(ref _selectedLogFilterType, value))
                {
                    OnPropertyChanged("SelectedLogFilterTypeValue");
                    if (LoggedWorkflowsView != null)
                    {
                        LoggedWorkflowsView.Refresh();
                    }

                    ScheduleStateSave();
                }
            }
        }

        public string SelectedLogFilterTypeValue
        {
            get { return SelectedLogFilterType.HasValue ? SelectedLogFilterType.Value.ToString() : string.Empty; }
            set
            {
                WorkflowType workflowType;
                SelectedLogFilterType = !string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out workflowType)
                    ? (WorkflowType?)workflowType
                    : null;
            }
        }

        public bool IsLogCenterPage
        {
            get { return SelectedShellPage == ShellPage.LogCenter; }
        }

        public string EmptyWorkflowLogsText
        {
            get { return LoggedWorkflows != null && LoggedWorkflows.Count > 0 ? string.Empty : "当前还没有可查看的流程日志。"; }
        }

        public string EmptyRunLogsText
        {
            get
            {
                if (SelectedLogWorkflow == null)
                {
                    return "请先在左侧选择一个流程。";
                }

                return WorkflowLogRuns != null && WorkflowLogRuns.Count > 0 ? string.Empty : "该流程还没有执行批次日志。";
            }
        }

        public AsyncRelayCommand RefreshLogCenterCommand { get; private set; }
        public AsyncRelayCommand ExportSelectedLogCommand { get; private set; }
        public RelayCommand CopySelectedLogSummaryCommand { get; private set; }

        private void InitializeLogCenter(ApplicationState initialState)
        {
            LoggedWorkflows = new ObservableCollection<WorkflowLogWorkflowItem>();
            WorkflowLogRuns = new ObservableCollection<WorkflowLogRunItem>();
            LogCenterFilterOptions = BuildLogCenterFilterOptions();
            LoggedWorkflowsView = CollectionViewSource.GetDefaultView(LoggedWorkflows);
            if (LoggedWorkflowsView != null)
            {
                LoggedWorkflowsView.Filter = FilterLoggedWorkflow;
            }

            LogCenterSearchText = initialState != null ? initialState.LogCenterSearchText : string.Empty;
            SelectedLogFilterType = initialState != null ? initialState.LogCenterFilterType : null;
            RefreshLogCenterCommand = new AsyncRelayCommand(RefreshLogCenterAsync);
            ExportSelectedLogCommand = new AsyncRelayCommand(ExportSelectedLogAsync, () => SelectedWorkflowLogRun != null);
            CopySelectedLogSummaryCommand = new RelayCommand(CopySelectedLogSummary, () => SelectedWorkflowLogRun != null);

            RefreshLogCenterAsync().GetAwaiter().GetResult();
            RestoreLogCenterSelection(initialState);
        }

        private static ObservableCollection<StepParameterOption> BuildLogCenterFilterOptions()
        {
            return new ObservableCollection<StepParameterOption>(new[]
            {
                new StepParameterOption { Value = string.Empty, DisplayName = "全部流程" },
                new StepParameterOption { Value = WorkflowType.Apply.ToString(), DisplayName = "申请流程" },
                new StepParameterOption { Value = WorkflowType.Approval.ToString(), DisplayName = "审批流程" },
                new StepParameterOption { Value = WorkflowType.Query.ToString(), DisplayName = "查询流程" },
                new StepParameterOption { Value = WorkflowType.IntegratedScheduler.ToString(), DisplayName = "调度编排模板" },
                new StepParameterOption { Value = WorkflowType.General.ToString(), DisplayName = "通用流程" },
                new StepParameterOption { Value = WorkflowType.Subflow.ToString(), DisplayName = "子流程" }
            });
        }

        private bool FilterLoggedWorkflow(object item)
        {
            var workflow = item as WorkflowLogWorkflowItem;
            if (workflow == null)
            {
                return false;
            }

            if (SelectedLogFilterType.HasValue && workflow.WorkflowType != SelectedLogFilterType.Value)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(LogCenterSearchText))
            {
                return true;
            }

            var search = LogCenterSearchText.Trim();
            return (workflow.WorkflowName ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                   || (workflow.WorkflowId ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                   || workflow.WorkflowTypeText.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task RefreshLogCenterAsync()
        {
            var workflows = await _workflowLogService.GetLoggedWorkflowsAsync();
            var previousWorkflowId = SelectedLogWorkflow != null ? SelectedLogWorkflow.WorkflowId : string.Empty;
            var previousRunId = SelectedWorkflowLogRun != null ? SelectedWorkflowLogRun.RunId : string.Empty;

            LoggedWorkflows.Clear();
            foreach (var workflow in workflows.OrderByDescending(item => item.LastRunAt))
            {
                LoggedWorkflows.Add(workflow);
            }

            if (LoggedWorkflowsView != null)
            {
                LoggedWorkflowsView.Refresh();
            }

            OnPropertyChanged("EmptyWorkflowLogsText");

            if (!string.IsNullOrWhiteSpace(previousWorkflowId))
            {
                SelectedLogWorkflow = LoggedWorkflows.FirstOrDefault(item => string.Equals(item.WorkflowId, previousWorkflowId, StringComparison.OrdinalIgnoreCase));
            }
            else if (SelectedLogWorkflow == null && LoggedWorkflows.Count > 0)
            {
                SelectedLogWorkflow = LoggedWorkflows[0];
            }

            if (!string.IsNullOrWhiteSpace(previousRunId) && WorkflowLogRuns != null)
            {
                SelectedWorkflowLogRun = WorkflowLogRuns.FirstOrDefault(item => string.Equals(item.RunId, previousRunId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void RestoreLogCenterSelection(ApplicationState state)
        {
            if (state == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(state.SelectedLogWorkflowId))
            {
                SelectedLogWorkflow = LoggedWorkflows.FirstOrDefault(item => string.Equals(item.WorkflowId, state.SelectedLogWorkflowId, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(state.SelectedLogRunId) && WorkflowLogRuns != null)
            {
                SelectedWorkflowLogRun = WorkflowLogRuns.FirstOrDefault(item => string.Equals(item.RunId, state.SelectedLogRunId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private async Task LoadRunsForSelectedWorkflowAsync()
        {
            WorkflowLogRuns.Clear();
            SelectedWorkflowLogText = string.Empty;
            SelectedWorkflowLogSummaryText = string.Empty;
            OnPropertyChanged("EmptyRunLogsText");

            if (SelectedLogWorkflow == null)
            {
                return;
            }

            var runs = await _workflowLogService.GetRunsAsync(SelectedLogWorkflow.WorkflowId);
            foreach (var run in runs.OrderByDescending(item => item.StartedAt))
            {
                WorkflowLogRuns.Add(run);
            }

            OnPropertyChanged("EmptyRunLogsText");
            SelectedWorkflowLogRun = WorkflowLogRuns.FirstOrDefault();
        }

        private async Task LoadSelectedRunTextAsync()
        {
            if (SelectedWorkflowLogRun == null)
            {
                SelectedWorkflowLogText = string.Empty;
                SelectedWorkflowLogSummaryText = string.Empty;
                return;
            }

            SelectedWorkflowLogText = await _workflowLogService.LoadRunTextAsync(SelectedWorkflowLogRun);
            SelectedWorkflowLogSummaryText = BuildLogSummaryText(SelectedWorkflowLogRun);
        }

        private async Task ExportSelectedLogAsync()
        {
            if (SelectedWorkflowLogRun == null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "导出执行日志",
                Filter = "文本日志 (*.log)|*.log|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                FileName = string.Format("{0}_{1}.log",
                    SanitizeWorkflowFileName(string.IsNullOrWhiteSpace(SelectedWorkflowLogRun.WorkflowName) ? SelectedWorkflowLogRun.WorkflowId : SelectedWorkflowLogRun.WorkflowName),
                    SelectedWorkflowLogRun.RunId)
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            await _workflowLogService.ExportRunAsync(SelectedWorkflowLogRun, dialog.FileName);
            _logService.Log(LogLevel.Info, "已导出执行日志到 " + dialog.FileName + "。");
        }

        private void CopySelectedLogSummary()
        {
            if (SelectedWorkflowLogRun == null)
            {
                return;
            }

            Clipboard.SetText(SelectedWorkflowLogSummaryText ?? string.Empty);
            _logService.Log(LogLevel.Info, "当前执行批次摘要已复制到剪贴板。");
        }

        private string BuildLogSummaryText(WorkflowLogRunItem run)
        {
            if (run == null)
            {
                return string.Empty;
            }

            return string.Format("流程：{0}\r\n类型：{1}\r\n批次：{2}\r\n模式：{3}\r\n结果：{4}\r\n开始：{5:yyyy-MM-dd HH:mm:ss}\r\n结束：{6}\r\n日志文件：{7}\r\n摘要：{8}",
                run.WorkflowName,
                run.WorkflowTypeText,
                run.RunId,
                run.RunMode,
                run.Result,
                run.StartedAt,
                run.EndedAt.HasValue ? run.EndedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-",
                run.LogFilePath,
                string.IsNullOrWhiteSpace(run.Summary) ? "-" : run.Summary);
        }

        private async Task RefreshLogCenterDataAsync()
        {
            await RefreshLogCenterAsync();
        }

        private void RefreshLogCenterDataSafe()
        {
            try
            {
                RefreshLogCenterAsync().GetAwaiter().GetResult();
            }
            catch
            {
            }
        }
    }
}
