using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using WpfApplication1.Commands;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;

namespace WpfApplication1.ViewModels
{
    public partial class MainWindowViewModel
    {
        private readonly ISchedulerStateStore _schedulerStateStore = new XmlSchedulerStateStore();
        private readonly string _schedulerStateFilePath = GetDefaultSchedulerStateFilePath();
        private IIntegratedSchedulerService _integratedSchedulerService;
        private CancellationTokenSource _schedulerCancellationTokenSource;
        private ObservableCollection<NavigationItem> _navigationItems;
        private NavigationItem _selectedNavigationItem;
        private ShellPage _selectedShellPage;
        private SchedulerSettings _schedulerSettings;
        private RuntimeStateSnapshot _runtimeState;
        private TaskSummarySnapshot _taskSummary;
        private ObservableCollection<RunHistoryItem> _runHistoryItems;
        private string _nodeLibrarySearchText;
        private ICollectionView _availableStepsView;
        private bool _isSchedulerRunning;
        private double _designerLeftPaneWidth = 280;
        private double _designerRightPaneWidth = 360;
        private double _logDrawerHeight = 240;
        private bool _isLogDrawerExpanded = true;
        private double _designerCanvasViewportOffsetX;
        private double _designerCanvasViewportOffsetY;

        public ObservableCollection<NavigationItem> NavigationItems
        {
            get { return _navigationItems; }
            private set { SetProperty(ref _navigationItems, value); }
        }

        public NavigationItem SelectedNavigationItem
        {
            get { return _selectedNavigationItem; }
            set
            {
                if (SetProperty(ref _selectedNavigationItem, value) && value != null)
                {
                    SelectedShellPage = value.Page;
                }
            }
        }

        public ShellPage SelectedShellPage
        {
            get { return _selectedShellPage; }
            set
            {
                if (SetProperty(ref _selectedShellPage, value))
                {
                    OnPropertyChanged("IsWorkbenchPage");
                    OnPropertyChanged("IsWorkflowManagementPage");
                    OnPropertyChanged("IsDesignerPage");
                    OnPropertyChanged("IsScheduleCenterPage");
                    OnPropertyChanged("IsOperationCenterPage");
                    OnPropertyChanged("IsLogCenterPage");
                    OnPropertyChanged("IsLocalTasksPage");
                    OnPropertyChanged("WindowTitle");
                    SyncSelectedNavigationItem();
                    ScheduleStateSave();
                }
            }
        }

        public bool IsWorkbenchPage
        {
            get { return SelectedShellPage == ShellPage.Workbench; }
        }

        public bool IsDesignerPage
        {
            get { return SelectedShellPage == ShellPage.Designer; }
        }

        public bool IsLocalTasksPage
        {
            get { return SelectedShellPage == ShellPage.LocalTasks; }
        }

        public SchedulerSettings SchedulerSettings
        {
            get { return _schedulerSettings; }
            private set
            {
                if (SetProperty(ref _schedulerSettings, value ?? new SchedulerSettings()))
                {
                    OnPropertyChanged("ApplyWorkflowIdValue");
                    OnPropertyChanged("QueryWorkflowIdValue");
                    OnPropertyChanged("ApprovalWorkflowIdValue");
                    OnPropertyChanged("ApplyPriorityValue");
                    OnPropertyChanged("MaxContinuousApplyCountValue");
                    OnPropertyChanged("MainLoopIntervalMsValue");
                    OnPropertyChanged("QueryIntervalWhenNoApplyMsValue");
                    OnPropertyChanged("ResumePromptOnStartupValue");
                    OnPropertyChanged("HasValidSchedulerSelection");
            OnPropertyChanged("SchedulerSelectionStatusText");
            if (StartSchedulerCommand != null) StartSchedulerCommand.RaiseCanExecuteChanged();
            if (RunSchedulerRoundCommand != null) RunSchedulerRoundCommand.RaiseCanExecuteChanged();
            ScheduleStateSave();
                }
            }
        }

        public RuntimeStateSnapshot RuntimeState
        {
            get { return _runtimeState; }
            private set { SetProperty(ref _runtimeState, value ?? new RuntimeStateSnapshot()); }
        }

        public TaskSummarySnapshot TaskSummary
        {
            get { return _taskSummary; }
            private set { SetProperty(ref _taskSummary, value ?? new TaskSummarySnapshot()); }
        }

        public ObservableCollection<RunHistoryItem> RunHistoryItems
        {
            get { return _runHistoryItems; }
            private set { SetProperty(ref _runHistoryItems, value); }
        }

        public ICollectionView AvailableStepsView
        {
            get { return _availableStepsView; }
            private set { SetProperty(ref _availableStepsView, value); }
        }

        public string NodeLibrarySearchText
        {
            get { return _nodeLibrarySearchText; }
            set
            {
                if (SetProperty(ref _nodeLibrarySearchText, value) && AvailableStepsView != null)
                {
                    AvailableStepsView.Refresh();
                }
            }
        }

        public bool IsSchedulerRunning
        {
            get { return _isSchedulerRunning; }
            private set
            {
                if (SetProperty(ref _isSchedulerRunning, value))
                {
                    OnPropertyChanged("SchedulerStatusText");
                    if (StartSchedulerCommand != null) StartSchedulerCommand.RaiseCanExecuteChanged();
                    if (RunSchedulerRoundCommand != null) RunSchedulerRoundCommand.RaiseCanExecuteChanged();
                    if (StopSchedulerLoopCommand != null) StopSchedulerLoopCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public double DesignerLeftPaneWidth
        {
            get { return _designerLeftPaneWidth; }
            set
            {
                if (SetProperty(ref _designerLeftPaneWidth, Math.Max(220, value)))
                {
                    ScheduleStateSave();
                }
            }
        }

        public double DesignerRightPaneWidth
        {
            get { return _designerRightPaneWidth; }
            set
            {
                if (SetProperty(ref _designerRightPaneWidth, Math.Max(300, value)))
                {
                    ScheduleStateSave();
                }
            }
        }

        public double LogDrawerHeight
        {
            get { return _logDrawerHeight; }
            set
            {
                if (SetProperty(ref _logDrawerHeight, Math.Max(140, value)))
                {
                    ScheduleStateSave();
                }
            }
        }

        public bool IsLogDrawerExpanded
        {
            get { return _isLogDrawerExpanded; }
            set
            {
                if (SetProperty(ref _isLogDrawerExpanded, value))
                {
                    OnPropertyChanged("LogToggleText");
                    ScheduleStateSave();
                }
            }
        }

        public double DesignerCanvasViewportOffsetX
        {
            get { return _designerCanvasViewportOffsetX; }
            set
            {
                if (SetProperty(ref _designerCanvasViewportOffsetX, Math.Max(0, value)))
                {
                    ScheduleStateSave();
                }
            }
        }

        public double DesignerCanvasViewportOffsetY
        {
            get { return _designerCanvasViewportOffsetY; }
            set
            {
                if (SetProperty(ref _designerCanvasViewportOffsetY, Math.Max(0, value)))
                {
                    ScheduleStateSave();
                }
            }
        }
        public string SchedulerStatusText
        {
            get
            {
                if (IsSchedulerRunning)
                {
                    return "统一调度运行中";
                }

                if (_integratedSchedulerService != null && _integratedSchedulerService.CurrentState != null && !string.IsNullOrWhiteSpace(_integratedSchedulerService.CurrentState.LastMode))
                {
                    return "最近模式：" + _integratedSchedulerService.CurrentState.LastMode;
                }

                return "统一调度空闲中";
            }
        }

        public string LogToggleText
        {
            get { return IsLogDrawerExpanded ? "收起日志" : "展开日志"; }
        }

        public string BusinessStateFilePath
        {
            get { return _businessStateFilePath; }
        }

        public string SchedulerStateFilePath
        {
            get { return _schedulerStateFilePath; }
        }

        public string SuggestedActionText
        {
            get { return TaskSummary != null && !string.IsNullOrWhiteSpace(TaskSummary.SuggestedAction) ? TaskSummary.SuggestedAction : "-"; }
        }

        public string LastRefreshDisplayText
        {
            get { return TaskSummary != null && TaskSummary.LastRefreshAt.HasValue ? TaskSummary.LastRefreshAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-"; }
        }

        public bool ApplyPriorityValue
        {
            get { return SchedulerSettings != null && SchedulerSettings.ApplyPriority; }
            set { UpdateSchedulerSettings(settings => settings.ApplyPriority = value); }
        }

        public int MaxContinuousApplyCountValue
        {
            get { return SchedulerSettings != null ? SchedulerSettings.MaxContinuousApplyCount : 3; }
            set { UpdateSchedulerSettings(settings => settings.MaxContinuousApplyCount = Math.Max(1, value)); }
        }

        public int MainLoopIntervalMsValue
        {
            get { return SchedulerSettings != null ? SchedulerSettings.MainLoopIntervalMs : 2000; }
            set { UpdateSchedulerSettings(settings => settings.MainLoopIntervalMs = Math.Max(500, value)); }
        }

        public int QueryIntervalWhenNoApplyMsValue
        {
            get { return SchedulerSettings != null ? SchedulerSettings.QueryIntervalWhenNoApplyMs : 5000; }
            set { UpdateSchedulerSettings(settings => settings.QueryIntervalWhenNoApplyMs = Math.Max(1000, value)); }
        }

        public bool ResumePromptOnStartupValue
        {
            get { return SchedulerSettings == null || SchedulerSettings.ResumePromptOnStartup; }
            set { UpdateSchedulerSettings(settings => settings.ResumePromptOnStartup = value); }
        }

        public string CurrentModeDisplayText
        {
            get { return RuntimeState != null && !string.IsNullOrWhiteSpace(RuntimeState.CurrentMode) ? RuntimeState.CurrentMode : "-"; }
        }

        public string CurrentStepDisplayText
        {
            get { return RuntimeState != null && !string.IsNullOrWhiteSpace(RuntimeState.CurrentStepName) ? RuntimeState.CurrentStepName : "-"; }
        }

        public string CurrentWindowDisplayText
        {
            get { return RuntimeState != null && !string.IsNullOrWhiteSpace(RuntimeState.CurrentWindowTitle) ? RuntimeState.CurrentWindowTitle : "-"; }
        }

        public string CurrentFrameDisplayText
        {
            get { return RuntimeState != null && !string.IsNullOrWhiteSpace(RuntimeState.FramePathDisplay) ? RuntimeState.FramePathDisplay : "root"; }
        }

        public string CurrentObjectDisplayText
        {
            get { return RuntimeState != null && !string.IsNullOrWhiteSpace(RuntimeState.CurrentObject) ? RuntimeState.CurrentObject : "-"; }
        }

        public string RecentErrorDisplayText
        {
            get { return RuntimeState != null && !string.IsNullOrWhiteSpace(RuntimeState.RecentErrorSummary) ? RuntimeState.RecentErrorSummary : "-"; }
        }

        public string PendingApplyText
        {
            get { return Convert.ToString(TaskSummary != null ? TaskSummary.PendingApplyCount : 0); }
        }

        public string PendingApprovalText
        {
            get { return Convert.ToString(TaskSummary != null ? TaskSummary.PendingApprovalCount : 0); }
        }

        public string PendingQueryText
        {
            get { return Convert.ToString(TaskSummary != null ? TaskSummary.PendingQueryCount : 0); }
        }

        public string PendingResumeTextValue
        {
            get { return Convert.ToString(TaskSummary != null ? TaskSummary.PendingResumeCount : 0); }
        }

        public string CompletedTodayText
        {
            get { return Convert.ToString(TaskSummary != null ? TaskSummary.CompletedTodayCount : 0); }
        }

        public string FailedTodayText
        {
            get { return Convert.ToString(TaskSummary != null ? TaskSummary.FailedTodayCount : 0); }
        }

        public RelayCommand SelectShellPageCommand { get; private set; }
        public AsyncRelayCommand StartSchedulerCommand { get; private set; }
        public RelayCommand StopSchedulerLoopCommand { get; private set; }
        public AsyncRelayCommand RunSchedulerRoundCommand { get; private set; }
        public RelayCommand ToggleLogDrawerCommand { get; private set; }

        private void InitializeShellAndScheduler(ApplicationState initialState)
        {
            _integratedSchedulerService = new IntegratedSchedulerService(_workflowRunner, _workflowFileService, _businessStateStore, _schedulerStateStore, _publishedWorkflowStore, _logService);
            SchedulerSettings = initialState != null && initialState.SchedulerSettings != null
                ? initialState.SchedulerSettings.Clone()
                : BuildDefaultSchedulerSettings();
            SchedulerSettings.Normalize();

            DesignerLeftPaneWidth = initialState != null && initialState.DesignerLeftPaneWidth > 0 ? initialState.DesignerLeftPaneWidth : 280;
            DesignerRightPaneWidth = initialState != null && initialState.DesignerRightPaneWidth > 0 ? initialState.DesignerRightPaneWidth : 360;
            LogDrawerHeight = initialState != null && initialState.LogDrawerHeight > 0 ? initialState.LogDrawerHeight : 240;
            IsLogDrawerExpanded = initialState == null || initialState.IsLogDrawerExpanded;
            DesignerCanvasViewportOffsetX = initialState != null ? initialState.CanvasViewportOffsetX : 0;
            DesignerCanvasViewportOffsetY = initialState != null ? initialState.CanvasViewportOffsetY : 0;

            RuntimeState = new RuntimeStateSnapshot
            {
                FramePathDisplay = "root",
                CurrentWorkflowName = CurrentWorkflowName,
                CurrentMode = "manual"
            };
            TaskSummary = new TaskSummarySnapshot
            {
                SuggestedAction = "继续在流程管理中维护草稿，或在调度中心启动已发布流程。",
                LastRefreshAt = DateTime.Now
            };
            RunHistoryItems = new ObservableCollection<RunHistoryItem>();

            NavigationItems = new ObservableCollection<NavigationItem>(new[]
            {
                new NavigationItem { Page = ShellPage.Workbench, Title = "工作台", Description = "任务摘要与快速入口" },
                new NavigationItem { Page = ShellPage.WorkflowManagement, Title = "流程管理", Description = "新建、筛选、发布和进入设计器" },
                new NavigationItem { Page = ShellPage.Designer, Title = "流程设计器", Description = "纯画布设计、属性与调试" },
                new NavigationItem { Page = ShellPage.ScheduleCenter, Title = "调度中心", Description = "已发布流程的统一调度控制台" },
                new NavigationItem { Page = ShellPage.OperationCenter, Title = "运行中心", Description = "运行态、统计和最近执行" },
                new NavigationItem { Page = ShellPage.LogCenter, Title = "日志中心", Description = "按流程和执行批次查看诊断日志" },
                new NavigationItem { Page = ShellPage.LocalTasks, Title = "本机任务", Description = "恢复、本机 XML 与报告上传" }
            });

            foreach (var step in AvailableSteps)
            {
                step.Category = ResolveToolboxCategory(step.StepType);
            }

            AvailableStepsView = CollectionViewSource.GetDefaultView(AvailableSteps);
            if (AvailableStepsView != null)
            {
                AvailableStepsView.Filter = FilterAvailableStep;
                var grouping = AvailableStepsView as ListCollectionView;
                if (grouping != null)
                {
                    grouping.GroupDescriptions.Clear();
                    grouping.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
                }
            }

            SelectShellPageCommand = new RelayCommand(ExecuteSelectShellPage, CanExecuteSelectShellPage);
            StartSchedulerCommand = new AsyncRelayCommand(StartSchedulerAsync, CanStartScheduler);
            StopSchedulerLoopCommand = new RelayCommand(StopSchedulerLoop, CanStopScheduler);
            RunSchedulerRoundCommand = new AsyncRelayCommand(RunSchedulerRoundAsync, CanRunSchedulerRound);
            ToggleLogDrawerCommand = new RelayCommand(ToggleLogDrawer);

            SelectedShellPage = initialState != null ? initialState.SelectedShellPage : ShellPage.Workbench;
            SyncSelectedNavigationItem();
            RefreshTaskSummaryFromState();
            RefreshLogCenterDataSafe();
        }

        private static SchedulerSettings BuildDefaultSchedulerSettings()
        {
            return new SchedulerSettings();
        }

        private static string GetDefaultSchedulerStateFilePath()
        {
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "State");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "scheduler-state.xml");
        }

        private void UpdateSchedulerSettings(Action<SchedulerSettings> update)
        {
            if (SchedulerSettings == null)
            {
                SchedulerSettings = BuildDefaultSchedulerSettings();
            }

            update(SchedulerSettings);
            SchedulerSettings.Normalize();
            OnPropertyChanged("ApplyWorkflowIdValue");
            OnPropertyChanged("QueryWorkflowIdValue");
            OnPropertyChanged("ApprovalWorkflowIdValue");
            OnPropertyChanged("ApplyPriorityValue");
            OnPropertyChanged("MaxContinuousApplyCountValue");
            OnPropertyChanged("MainLoopIntervalMsValue");
            OnPropertyChanged("QueryIntervalWhenNoApplyMsValue");
            OnPropertyChanged("ResumePromptOnStartupValue");
            OnPropertyChanged("HasValidSchedulerSelection");
            OnPropertyChanged("SchedulerSelectionStatusText");
            if (StartSchedulerCommand != null) StartSchedulerCommand.RaiseCanExecuteChanged();
            if (RunSchedulerRoundCommand != null) RunSchedulerRoundCommand.RaiseCanExecuteChanged();
            ScheduleStateSave();
        }

        private bool FilterAvailableStep(object item)
        {
            var step = item as ToolboxStepDefinition;
            if (step == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(NodeLibrarySearchText))
            {
                return true;
            }

            var search = NodeLibrarySearchText.Trim();
            return (step.Name ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                   || (step.Description ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                   || (step.Category ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool CanExecuteSelectShellPage(object parameter)
        {
            return parameter != null;
        }

        private void ExecuteSelectShellPage(object parameter)
        {
            ShellPage page;
            if (parameter is ShellPage)
            {
                page = (ShellPage)parameter;
            }
            else if (!Enum.TryParse(Convert.ToString(parameter), true, out page))
            {
                return;
            }

            SelectedShellPage = page;
        }

        private void SyncSelectedNavigationItem()
        {
            if (NavigationItems == null)
            {
                return;
            }

            var item = NavigationItems.FirstOrDefault(entry => entry.Page == SelectedShellPage);
            if (!ReferenceEquals(_selectedNavigationItem, item))
            {
                _selectedNavigationItem = item;
                OnPropertyChanged("SelectedNavigationItem");
            }
        }

        private bool CanStartScheduler()
        {
            return !IsSchedulerRunning && ExecutionStatus != ExecutionStatus.Running && HasValidSchedulerSelection;
        }

        private bool CanRunSchedulerRound()
        {
            return !IsSchedulerRunning && ExecutionStatus != ExecutionStatus.Running && HasValidSchedulerSelection;
        }

        private bool CanStopScheduler()
        {
            return IsSchedulerRunning;
        }

        private async Task StartSchedulerAsync()
        {
            _schedulerCancellationTokenSource = new CancellationTokenSource();
            IsSchedulerRunning = true;
            ExecutionStatus = ExecutionStatus.Running;
            try
            {
                _logService.Log(LogLevel.Info, "Integrated scheduler started.");
                await _integratedSchedulerService.StartAsync(SchedulerSettings.Clone(), BuildSchedulerExecutionContext(), _schedulerCancellationTokenSource.Token);
                ExecutionStatus = ExecutionStatus.Completed;
            }
            catch (OperationCanceledException)
            {
                ExecutionStatus = ExecutionStatus.Cancelled;
                _logService.Log(LogLevel.Warning, "Integrated scheduler stopped.");
            }
            catch (Exception ex)
            {
                ExecutionStatus = ExecutionStatus.Failed;
                _logService.Log(LogLevel.Error, "Integrated scheduler failed: " + ex.Message);
                UpdateRuntimeStateSnapshot(new RuntimeStateSnapshot
                {
                    CurrentWorkflowName = CurrentWorkflowName,
                    RecentErrorSummary = ex.Message,
                    FramePathDisplay = RuntimeState != null ? RuntimeState.FramePathDisplay : "root"
                });
            }
            finally
            {
                IsSchedulerRunning = false;
                if (_schedulerCancellationTokenSource != null)
                {
                    _schedulerCancellationTokenSource.Dispose();
                    _schedulerCancellationTokenSource = null;
                }
            }
        }

        private async Task RunSchedulerRoundAsync()
        {
            ExecutionStatus = ExecutionStatus.Running;
            try
            {
                await _integratedSchedulerService.ExecuteSingleRoundAsync(SchedulerSettings.Clone(), BuildSchedulerExecutionContext(), CancellationToken.None);
                ExecutionStatus = ExecutionStatus.Completed;
            }
            catch (Exception ex)
            {
                ExecutionStatus = ExecutionStatus.Failed;
                _logService.Log(LogLevel.Error, "Single scheduler round failed: " + ex.Message);
                throw;
            }
        }

        private void StopSchedulerLoop()
        {
            if (_schedulerCancellationTokenSource != null && !_schedulerCancellationTokenSource.IsCancellationRequested)
            {
                _integratedSchedulerService.RequestStop();
                _schedulerCancellationTokenSource.Cancel();
            }
        }

        private void ToggleLogDrawer()
        {
            IsLogDrawerExpanded = !IsLogDrawerExpanded;
        }

        private SchedulerExecutionContext BuildSchedulerExecutionContext()
        {
            return new SchedulerExecutionContext
            {
                EmployeeId = EmployeeId,
                BusinessStatePath = _businessStateFilePath,
                SchedulerStatePath = _schedulerStateFilePath,
                PendingBusinessState = _pendingBusinessResumeRecord != null ? _pendingBusinessResumeRecord.Clone() : null,
                BusinessStateChanged = state =>
                {
                    ExecuteOnUi(() =>
                    {
                        _pendingBusinessResumeRecord = state != null ? state.Clone() : null;
                        RefreshPendingBusinessResumeBindings();
                        RefreshTaskSummaryFromState();
            RefreshLogCenterDataSafe();
        });
                },
                RuntimeStateChanged = snapshot => ExecuteOnUi(() => UpdateRuntimeStateSnapshot(snapshot)),
                TaskSummaryChanged = summary => ExecuteOnUi(() => UpdateTaskSummarySnapshot(summary)),
                RunHistoryAdded = item => ExecuteOnUi(() => AddRunHistoryItem(item))
            };
        }

        private void ExecuteOnUi(Action action)
        {
            if (Application.Current == null)
            {
                action();
                return;
            }

            Application.Current.Dispatcher.Invoke(action);
        }

        private void UpdateRuntimeStateSnapshot(RuntimeStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            RuntimeState = snapshot.Clone();
            OnPropertyChanged("CurrentModeDisplayText");
            OnPropertyChanged("CurrentStepDisplayText");
            OnPropertyChanged("CurrentWindowDisplayText");
            OnPropertyChanged("CurrentFrameDisplayText");
            OnPropertyChanged("CurrentObjectDisplayText");
            OnPropertyChanged("RecentErrorDisplayText");
            OnPropertyChanged("DesignerDebugHintText");
        }

        private void UpdateTaskSummarySnapshot(TaskSummarySnapshot summary)
        {
            TaskSummary = summary ?? new TaskSummarySnapshot();
            TaskSummary.CompletedTodayCount = RunHistoryItems != null ? RunHistoryItems.Count(item => string.Equals(item.Result, "Success", StringComparison.OrdinalIgnoreCase) && item.StartedAt.Date == DateTime.Today) : 0;
            TaskSummary.FailedTodayCount = RunHistoryItems != null ? RunHistoryItems.Count(item => string.Equals(item.Result, "Failed", StringComparison.OrdinalIgnoreCase) && item.StartedAt.Date == DateTime.Today) : 0;
            RaiseTaskSummaryBindings();
        }

        private void RefreshTaskSummaryFromState()
        {
            if (TaskSummary == null)
            {
                TaskSummary = new TaskSummarySnapshot();
            }

            TaskSummary.PendingResumeCount = HasPendingBusinessResume ? 1 : 0;
            TaskSummary.PendingApprovalCount = _pendingBusinessResumeRecord != null && _pendingBusinessResumeRecord.Stage == BusinessStateStage.PendingApproval ? 1 : 0;
            TaskSummary.PendingQueryCount = _pendingBusinessResumeRecord != null && (_pendingBusinessResumeRecord.Stage == BusinessStateStage.Queryable || _pendingBusinessResumeRecord.Stage == BusinessStateStage.ReportSaved) ? 1 : 0;
            TaskSummary.PendingApplyCount = _pendingBusinessResumeRecord != null && (_pendingBusinessResumeRecord.Stage == BusinessStateStage.Fetched || _pendingBusinessResumeRecord.Stage == BusinessStateStage.Applied || _pendingBusinessResumeRecord.Stage == BusinessStateStage.Failed) ? 1 : 0;
            TaskSummary.CompletedTodayCount = RunHistoryItems != null ? RunHistoryItems.Count(item => string.Equals(item.Result, "Success", StringComparison.OrdinalIgnoreCase) && item.StartedAt.Date == DateTime.Today) : 0;
            TaskSummary.FailedTodayCount = RunHistoryItems != null ? RunHistoryItems.Count(item => string.Equals(item.Result, "Failed", StringComparison.OrdinalIgnoreCase) && item.StartedAt.Date == DateTime.Today) : 0;
            TaskSummary.HasPendingTask = TaskSummary.PendingApplyCount > 0 || TaskSummary.PendingQueryCount > 0 || TaskSummary.PendingResumeCount > 0 || TaskSummary.PendingApprovalCount > 0;
            if (string.IsNullOrWhiteSpace(TaskSummary.SuggestedAction))
            {
                TaskSummary.SuggestedAction = HasPendingBusinessResume ? "前往本机任务页处理恢复。" : "前往流程管理或调度中心继续操作。";
            }
            TaskSummary.LastRefreshAt = DateTime.Now;
            RaiseTaskSummaryBindings();
        }

        private void AddRunHistoryItem(RunHistoryItem item)
        {
            if (item == null)
            {
                return;
            }

            if (RunHistoryItems == null)
            {
                RunHistoryItems = new ObservableCollection<RunHistoryItem>();
            }

            RunHistoryItems.Insert(0, item);
            while (RunHistoryItems.Count > 30)
            {
                RunHistoryItems.RemoveAt(RunHistoryItems.Count - 1);
            }

            RefreshTaskSummaryFromState();
            RefreshLogCenterDataSafe();
        }

        private static string ResolveToolboxCategory(StepType stepType)
        {
            switch (stepType)
            {
                case StepType.LaunchIe:
                case StepType.AttachIe:
                case StepType.Navigate:
                case StepType.WaitPageReady:
                    return "浏览器与页面";
                case StepType.ClickElement:
                case StepType.InputText:
                case StepType.ReadText:
                case StepType.SelectOption:
                case StepType.ExecuteScript:
                case StepType.HandleAlert:
                case StepType.UploadFile:
                case StepType.WaitDownload:
                case StepType.Screenshot:
                case StepType.WaitForElement:
                    return "页面操作";
                case StepType.SwitchWindow:
                case StepType.SwitchFrame:
                case StepType.ClickAndSwitchWindow:
                    return "窗口与 iframe";
                case StepType.HttpGetData:
                case StepType.HttpUploadFile:
                case StepType.SetVariable:
                    return "数据与接口";
                case StepType.PageListLoop:
                case StepType.QueryAndExportReport:
                case StepType.UpdateBusinessState:
                    return "列表与报告";
                case StepType.Condition:
                case StepType.Loop:
                case StepType.LoopStart:
                case StepType.LoopEnd:
                case StepType.Delay:
                    return "调度与流程控制";
                default:
                    return "开始与结束";
            }
        }

        private static string GetShellPageTitle(ShellPage page)
        {
            switch (page)
            {
                case ShellPage.WorkflowManagement:
                    return "流程管理";
                case ShellPage.Designer:
                    return "流程设计器";
                case ShellPage.ScheduleCenter:
                    return "调度中心";
                case ShellPage.OperationCenter:
                    return "运行中心";
                case ShellPage.LocalTasks:
                    return "本机任务";
                case ShellPage.LogCenter:
                    return "日志中心";
                default:
                    return "工作台";
            }
        }
        private void RaiseTaskSummaryBindings()
        {
            OnPropertyChanged("TaskSummary");
            OnPropertyChanged("PendingApplyText");
            OnPropertyChanged("PendingApprovalText");
            OnPropertyChanged("PendingQueryText");
            OnPropertyChanged("PendingResumeTextValue");
            OnPropertyChanged("CompletedTodayText");
            OnPropertyChanged("FailedTodayText");
            OnPropertyChanged("SuggestedActionText");
            OnPropertyChanged("LastRefreshDisplayText");
        }
    }
}











