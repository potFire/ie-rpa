using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WpfApplication1.Automation.IE;
using WpfApplication1.Commands;
using WpfApplication1.Common;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.StepExecutors;
using WpfApplication1.Workflow;

namespace WpfApplication1.ViewModels
{
    public partial class MainWindowViewModel : BindableBase
    {
        private static readonly string[] AlertActionOptionsSource = { "accept", "dismiss" };
        private static readonly string[] MatchModeOptionsSource = { "text", "value" };
        private static readonly string[] WindowTitleMatchModeOptionsSource = { "contains", "exact", "startswith", "endswith" };
        private static readonly string[] FrameActionOptionsSource = { "enter", "parent", "root" };
        private static readonly string[] LoopModeOptionsSource = { "infinite", "counted" };
        private static readonly string[] PageListModeOptionsSource = { "approve", "queryReport", "click" };
        private static readonly string[] ReturnModeOptionsSource = { "switchOriginal", "closeCurrentWindow", "clickSelector", "alertConfirm", "clickSelectorAndAlert" };
        private readonly IApplicationStateStore _applicationStateStore;
        private readonly IBusinessStateStore _businessStateStore;
        private readonly IWorkflowFileService _workflowFileService;
        private readonly ILogService _logService;
        private readonly IWorkflowRunner _workflowRunner;
        private readonly DispatcherTimer _autoSaveTimer;
        private readonly string _stateFilePath;
        private readonly string _businessStateFilePath;
        private WorkflowDefinition _currentWorkflow;
        private WorkflowStep _selectedWorkflowStep;
        private ToolboxStepDefinition _selectedToolboxStep;
        private ExecutionStatus _executionStatus;
        private CancellationTokenSource _cancellationTokenSource;
        private string _employeeId;
        private bool _isPreparingElementPicker;
        private bool _suspendAutoSave;
        private BusinessStateRecord _pendingBusinessResumeRecord;

        public MainWindowViewModel()
            : this(new XmlApplicationStateStore(), GetDefaultStateFilePath(), null, new XmlBusinessStateStore(), GetDefaultBusinessStateFilePath(), null)
        {
        }

        public MainWindowViewModel(
            IApplicationStateStore applicationStateStore,
            string stateFilePath,
            ApplicationState initialState,
            IBusinessStateStore businessStateStore,
            string businessStateFilePath,
            BusinessStateRecord pendingBusinessResumeRecord)
        {
            _applicationStateStore = applicationStateStore ?? new XmlApplicationStateStore();
            _businessStateStore = businessStateStore ?? new XmlBusinessStateStore();
            _workflowFileService = new WorkflowFileService();
            _stateFilePath = string.IsNullOrWhiteSpace(stateFilePath) ? GetDefaultStateFilePath() : stateFilePath;
            _businessStateFilePath = string.IsNullOrWhiteSpace(businessStateFilePath) ? GetDefaultBusinessStateFilePath() : businessStateFilePath;
            _pendingBusinessResumeRecord = pendingBusinessResumeRecord != null ? pendingBusinessResumeRecord.Clone() : null;
            _logService = new InMemoryLogService();
            _workflowRunner = BuildWorkflowRunner(_logService);

            AvailableSteps = new ObservableCollection<ToolboxStepDefinition>(BuildToolboxSteps());
            Logs = new ObservableCollection<ExecutionLogEntry>();

            NewWorkflowCommand = new RelayCommand(CreateNewWorkflow);
            AddStepCommand = new RelayCommand(AddSelectedToolboxStep, () => SelectedToolboxStep != null);
            RemoveSelectedStepCommand = new RelayCommand(RemoveSelectedStep, () => SelectedWorkflowStep != null);
            MoveStepUpCommand = new RelayCommand(MoveSelectedStepUp, CanMoveSelectedStepUp);
            MoveStepDownCommand = new RelayCommand(MoveSelectedStepDown, CanMoveSelectedStepDown);
            SaveWorkflowCommand = new AsyncRelayCommand(SaveWorkflowAsync, () => CurrentWorkflow != null);
            LoadWorkflowCommand = new AsyncRelayCommand(LoadWorkflowAsync);
            OpenElementPickerCommand = new AsyncRelayCommand(OpenElementPickerAsync, CanPickElementForSelectedStep);
            OpenParameterElementPickerCommand = new AsyncRelayCommand(OpenParameterElementPickerAsync, CanPickElementForParameter);
            RunWorkflowCommand = new AsyncRelayCommand(RunWorkflowAsync, CanRunWorkflow);
            RunFromSelectedStepCommand = new AsyncRelayCommand(RunFromSelectedStepAsync, CanRunFromSelectedStep);
            RunSelectedStepCommand = new AsyncRelayCommand(RunSelectedStepAsync, CanRunSelectedStep);
            StopWorkflowCommand = new RelayCommand(StopWorkflow, () => ExecutionStatus == ExecutionStatus.Running);
            EditEmployeeIdCommand = new RelayCommand(EditEmployeeId);
            ApplyPendingBusinessResumeCommand = new RelayCommand(ApplyPendingBusinessResume, () => HasPendingBusinessResume);
            ClearPendingBusinessResumeCommand = new RelayCommand(ClearPendingBusinessResume, () => HasPendingBusinessResume);

            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(500);
            _autoSaveTimer.Tick += AutoSaveTimer_OnTick;

            _logService.EntryAdded += OnLogEntryAdded;
            ExecutionStatus = ExecutionStatus.Idle;

            if (initialState != null)
            {
                ApplyApplicationState(initialState);
            }
            else
            {
                CreateNewWorkflow();
            }

            if (CurrentWorkflow == null)
            {
                CreateNewWorkflow();
            }
            else if (SelectedWorkflowStep == null)
            {
                SelectedWorkflowStep = CurrentWorkflow.Steps.FirstOrDefault();
            }

            InitializeShellAndScheduler(initialState);
            InitializeWorkflowManagementSurface(initialState);
            InitializeLogCenter(initialState);
            RefreshPendingBusinessResumeBindings();
            if (HasPendingBusinessResume)
            {
                ApplyPendingBusinessResume();
            }
        }

        public ObservableCollection<ToolboxStepDefinition> AvailableSteps { get; private set; }

        public ObservableCollection<ExecutionLogEntry> Logs { get; private set; }

        public WorkflowDefinition CurrentWorkflow
        {
            get { return _currentWorkflow; }
            private set
            {
                if (SetProperty(ref _currentWorkflow, value))
                {
                    OnPropertyChanged("CurrentWorkflowName");
                    OnPropertyChanged("WorkflowSummary");
                    OnPropertyChanged("WindowTitle");
                    OnPropertyChanged("CurrentWorkflowMetaText");
                    OnPropertyChanged("HasCurrentWorkflow");
                    SaveWorkflowCommand.RaiseCanExecuteChanged();
                    if (SaveDraftWorkflowCommand != null) SaveDraftWorkflowCommand.RaiseCanExecuteChanged();
                    if (PublishCurrentWorkflowCommand != null) PublishCurrentWorkflowCommand.RaiseCanExecuteChanged();
                    if (CanvasZoomInCommand != null) CanvasZoomInCommand.RaiseCanExecuteChanged();
                    if (CanvasZoomOutCommand != null) CanvasZoomOutCommand.RaiseCanExecuteChanged();
                    if (AutoArrangeCanvasCommand != null) AutoArrangeCanvasCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public WorkflowStep SelectedWorkflowStep
        {
            get { return _selectedWorkflowStep; }
            set
            {
                if (SetProperty(ref _selectedWorkflowStep, value))
                {
                    OnPropertyChanged("HasSelectedWorkflowStep");
                    OnPropertyChanged("SelectedStepParametersText");
                    OnPropertyChanged("SelectedStepParametersEditor");
                    NotifySelectedStepEditorStateChanged();
                    RemoveSelectedStepCommand.RaiseCanExecuteChanged();
                    MoveStepUpCommand.RaiseCanExecuteChanged();
                    MoveStepDownCommand.RaiseCanExecuteChanged();
                    OpenElementPickerCommand.RaiseCanExecuteChanged();
                    if (OpenParameterElementPickerCommand != null) OpenParameterElementPickerCommand.RaiseCanExecuteChanged();
                    RunFromSelectedStepCommand.RaiseCanExecuteChanged();
                    RunSelectedStepCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged("SelectedStepDescriptionText");
                    OnPropertyChanged("DesignerDebugHintText");
                    RefreshDesignerCanvas();
                    ScheduleStateSave();
                }
            }
        }

        public ToolboxStepDefinition SelectedToolboxStep
        {
            get { return _selectedToolboxStep; }
            set
            {
                if (SetProperty(ref _selectedToolboxStep, value))
                {
                    AddStepCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ExecutionStatus ExecutionStatus
        {
            get { return _executionStatus; }
            private set
            {
                if (SetProperty(ref _executionStatus, value))
                {
                    OnPropertyChanged("StatusText");
                    StopWorkflowCommand.RaiseCanExecuteChanged();
                    RunWorkflowCommand.RaiseCanExecuteChanged();
                    RunFromSelectedStepCommand.RaiseCanExecuteChanged();
                    RunSelectedStepCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string EmployeeId
        {
            get { return _employeeId; }
            private set
            {
                if (SetProperty(ref _employeeId, value))
                {
                    OnPropertyChanged("EmployeeDisplayText");
                }
            }
        }

        public bool IsPreparingElementPicker
        {
            get { return _isPreparingElementPicker; }
            private set
            {
                if (SetProperty(ref _isPreparingElementPicker, value))
                {
                    OnPropertyChanged("ElementPickerButtonText");
                    OpenElementPickerCommand.RaiseCanExecuteChanged();
                    if (OpenParameterElementPickerCommand != null) OpenParameterElementPickerCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string EmployeeDisplayText
        {
            get
            {
                return string.IsNullOrWhiteSpace(EmployeeId)
                    ? "工号未设置"
                    : "工号：" + EmployeeId;
            }
        }

        public string CurrentWorkflowName
        {
            get { return CurrentWorkflow != null && !string.IsNullOrWhiteSpace(CurrentWorkflow.Name) ? CurrentWorkflow.Name : "未命名流程"; }
        }

        public bool HasPendingBusinessResume
        {
            get { return _pendingBusinessResumeRecord != null; }
        }

        public string PendingBusinessResumeText
        {
            get
            {
                if (!HasPendingBusinessResume)
                {
                    return "当前没有待恢复业务。";
                }

                var state = _pendingBusinessResumeRecord;
                return string.Format("未完成业务：{0} / {1}，阶段：{2}，最后步骤：{3}",
                    string.IsNullOrWhiteSpace(state.Name) ? "-" : state.Name,
                    string.IsNullOrWhiteSpace(state.IdCardNumber) ? "-" : state.IdCardNumber,
                    state.Stage,
                    string.IsNullOrWhiteSpace(state.LastStepName) ? "-" : state.LastStepName);
            }
        }

        public string WorkflowSummary
        {
            get
            {
                var stepCount = CurrentWorkflow != null && CurrentWorkflow.Steps != null ? CurrentWorkflow.Steps.Count : 0;
                return string.Format("共 {0} 个步骤，可在左侧选择节点后加入到当前流程。", stepCount);
            }
        }

        public string SelectedStepParametersText
        {
            get
            {
                if (SelectedWorkflowStep == null || SelectedWorkflowStep.Parameters == null || SelectedWorkflowStep.Parameters.Count == 0)
                {
                    return "当前步骤还没有配置参数。";
                }

                return string.Join(Environment.NewLine,
                    SelectedWorkflowStep.Parameters.Select(item => item.Key + " = " + item.Value));
            }
        }

        public string SelectedStepParametersEditor
        {
            get { return SelectedStepParametersText; }
            set
            {
                if (SelectedWorkflowStep == null)
                {
                    return;
                }

                SelectedWorkflowStep.Parameters = ParseParameters(value);
                OnPropertyChanged("SelectedStepParametersText");
                OnPropertyChanged("SelectedStepParametersEditor");
                NotifySelectedStepEditorStateChanged();
                RefreshDesignerCanvas();
                    ScheduleStateSave();
            }
        }

        public bool HasSelectedWorkflowStep
        {
            get { return SelectedWorkflowStep != null; }
        }

        public bool HasStructuredStepEditor
        {
            get
            {
                return ShowUrlEditor
                       || ShowSelectorEditor
                       || ShowWaitForElementEditor
                       || ShowInputTextEditor
                       || ShowReadTextEditor
                       || ShowDataVariableEditor
                       || ShowSelectOptionEditor
                       || ShowHandleAlertEditor
                       || ShowSetVariableEditor
                       || ShowWriteLogEditor
                       || ShowDelayEditor
                       || ShowSwitchFrameEditor
                       || ShowClickAndSwitchWindowEditor
                       || ShowSwitchWindowEditor
                       || ShowLoopStartEditor
                       || ShowLoopEndEditor
                       || ShowHttpUploadFileEditor
                       || ShowPageListLoopEditor
                       || ShowQueryAndExportReportEditor;
            }
        }

        public bool ShowUrlEditor
        {
            get
            {
                return SelectedWorkflowStep != null
                       && (SelectedWorkflowStep.StepType == StepType.LaunchIe
                           || SelectedWorkflowStep.StepType == StepType.Navigate
                           || SelectedWorkflowStep.StepType == StepType.HttpGetData);
            }
        }

        public bool ShowSelectorEditor
        {
            get
            {
                if (SelectedWorkflowStep == null)
                {
                    return false;
                }

                switch (SelectedWorkflowStep.StepType)
                {
                    case StepType.WaitForElement:
                    case StepType.ClickElement:
                    case StepType.ClickAndSwitchWindow:
                    case StepType.InputText:
                    case StepType.ReadText:
                    case StepType.SelectOption:
                    case StepType.SwitchFrame:
                    case StepType.UploadFile:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool ShowWaitForElementEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.WaitForElement; }
        }

        public bool ShowInputTextEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.InputText; }
        }

        public bool ShowReadTextEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.ReadText; }
        }

        public bool ShowDataVariableEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.HttpGetData; }
        }

        public bool ShowSelectOptionEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.SelectOption; }
        }

        public bool ShowHandleAlertEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.HandleAlert; }
        }

        public bool ShowSetVariableEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.SetVariable; }
        }

        public bool ShowWriteLogEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.WriteLog; }
        }

        public bool ShowDelayEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.Delay; }
        }

        public bool ShowSwitchFrameEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.SwitchFrame; }
        }

        public bool ShowClickAndSwitchWindowEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.ClickAndSwitchWindow; }
        }

        public bool ShowSwitchWindowEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.SwitchWindow; }
        }

        public bool ShowLoopStartEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.LoopStart; }
        }

        public bool ShowLoopEndEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.LoopEnd; }
        }

        public string UrlEditorLabel
        {
            get
            {
                if (SelectedWorkflowStep == null)
                {
                    return "鍦板潃";
                }

                switch (SelectedWorkflowStep.StepType)
                {
                    case StepType.LaunchIe:
                        return "鍚姩鍦板潃";
                    case StepType.HttpGetData:
                        return "璇锋眰鍦板潃";
                    default:
                        return "瀵艰埅鍦板潃";
                }
            }
        }

        public string SelectorEditorLabel
        {
            get
            {
                if (SelectedWorkflowStep == null)
                {
                    return "元素选择器（推荐使用 XPath）";
                }

                switch (SelectedWorkflowStep.StepType)
                {
                    case StepType.WaitForElement:
                        return "等待目标选择器（推荐使用 XPath）";
                    case StepType.ClickAndSwitchWindow:
                        return "点击目标选择器（推荐使用 XPath）";
                    case StepType.SwitchFrame:
                        return "iframe 选择器（推荐使用 XPath）";
                    default:
                        return "元素选择器（推荐使用 XPath）";
                }
            }
        }

        public string ElementPickerButtonText
        {
            get { return IsPreparingElementPicker ? "准备拾取中..." : "拾取页面元素"; }
        }

        public string UrlParameterValue
        {
            get { return GetStepParameter("url"); }
            set { SetStepParameter("url", value); }
        }

        public string SelectorParameterValue
        {
            get { return GetStepParameter(ResolveSelectorParameterName(GetSelectedStepType())); }
            set { SetStepParameter(ResolveSelectorParameterName(GetSelectedStepType()), value); }
        }

        public string WaitPollIntervalParameterValue
        {
            get { return GetStepParameter("pollIntervalMs"); }
            set { SetStepParameter("pollIntervalMs", value); }
        }

        public string InputTextParameterValue
        {
            get { return GetStepParameter("text"); }
            set { SetStepParameter("text", value); }
        }

        public string ReadVariableNameParameterValue
        {
            get { return GetStepParameter("variableName"); }
            set { SetStepParameter("variableName", value); }
        }

        public string DataVariableNameParameterValue
        {
            get { return GetStepParameter("dataVariableName"); }
            set { SetStepParameter("dataVariableName", value); }
        }

        public string OptionParameterValue
        {
            get { return GetStepParameter("option"); }
            set { SetStepParameter("option", value); }
        }

        public string MatchModeParameterValue
        {
            get
            {
                var value = GetStepParameter("matchMode");
                return string.IsNullOrWhiteSpace(value) ? "text" : value;
            }
            set { SetStepParameter("matchMode", value); }
        }

        public string AlertActionParameterValue
        {
            get
            {
                var value = GetStepParameter("action");
                return string.IsNullOrWhiteSpace(value) ? "accept" : value;
            }
            set { SetStepParameter("action", value); }
        }

        public string AlertButtonTextParameterValue
        {
            get { return GetStepParameter("buttonText"); }
            set { SetStepParameter("buttonText", value); }
        }

        public string AlertTitleContainsParameterValue
        {
            get { return GetStepParameter("titleContains"); }
            set { SetStepParameter("titleContains", value); }
        }

        public string SetVariableNameParameterValue
        {
            get { return GetStepParameter("name"); }
            set { SetStepParameter("name", value); }
        }

        public string SetVariableValueParameterValue
        {
            get { return GetStepParameter("value"); }
            set { SetStepParameter("value", value); }
        }

        public string WriteLogMessageParameterValue
        {
            get { return GetStepParameter("message"); }
            set { SetStepParameter("message", value); }
        }

        public string DelayDurationParameterValue
        {
            get { return GetStepParameter("durationMs"); }
            set { SetStepParameter("durationMs", value); }
        }

        public string SwitchFrameActionParameterValue
        {
            get
            {
                var value = GetStepParameter("action");
                return string.IsNullOrWhiteSpace(value) ? "enter" : value;
            }
            set { SetStepParameter("action", value); }
        }

        public string ClickAndSwitchWindowTitleParameterValue
        {
            get { return GetStepParameter("targetWindowTitle"); }
            set { SetStepParameter("targetWindowTitle", value); }
        }

        public string ClickAndSwitchWindowMatchModeParameterValue
        {
            get
            {
                var value = GetStepParameter("matchMode");
                return string.IsNullOrWhiteSpace(value) ? "contains" : value;
            }
            set { SetStepParameter("matchMode", value); }
        }

        public string ClickAndSwitchWindowPollIntervalParameterValue
        {
            get { return GetStepParameter("pollIntervalMs"); }
            set { SetStepParameter("pollIntervalMs", value); }
        }

        public bool ClickAndSwitchWindowExcludeCurrentParameterValue
        {
            get
            {
                var value = GetStepParameter("excludeCurrent");
                bool parsed;
                return !string.IsNullOrWhiteSpace(value) ? (bool.TryParse(value, out parsed) ? parsed : true) : true;
            }
            set { SetStepParameter("excludeCurrent", value ? "true" : "false"); }
        }

        public string SwitchWindowTitleParameterValue
        {
            get { return GetStepParameter("titleContains"); }
            set { SetStepParameter("titleContains", value); }
        }

        public string SwitchWindowModeParameterValue
        {
            get
            {
                var value = GetStepParameter("mode");
                return string.IsNullOrWhiteSpace(value) ? "last" : value;
            }
            set { SetStepParameter("mode", value); }
        }

        public string SwitchWindowUrlParameterValue
        {
            get { return GetStepParameter("urlContains"); }
            set { SetStepParameter("urlContains", value); }
        }

        public string LoopStartKeyParameterValue
        {
            get { return GetStepParameter("loopKey"); }
            set { SetStepParameter("loopKey", value); }
        }

        public string RequiredVariablesParameterValue
        {
            get { return GetStepParameter("requiredVariables"); }
            set { SetStepParameter("requiredVariables", value); }
        }

        public string IterationVariableParameterValue
        {
            get { return GetStepParameter("iterationVariable"); }
            set { SetStepParameter("iterationVariable", value); }
        }

        public string LoopEndKeyParameterValue
        {
            get { return GetStepParameter("loopKey"); }
            set { SetStepParameter("loopKey", value); }
        }

        public string LoopModeParameterValue
        {
            get
            {
                var value = GetStepParameter("mode");
                return string.IsNullOrWhiteSpace(value) ? "infinite" : value;
            }
            set { SetStepParameter("mode", value); }
        }

        public string LoopTimesParameterValue
        {
            get { return GetStepParameter("times"); }
            set { SetStepParameter("times", value); }
        }

        public string LoopIntervalParameterValue
        {
            get { return GetStepParameter("intervalMs"); }
            set { SetStepParameter("intervalMs", value); }
        }

        public IEnumerable<string> AlertActionOptions
        {
            get { return AlertActionOptionsSource; }
        }

        public IEnumerable<string> MatchModeOptions
        {
            get { return MatchModeOptionsSource; }
        }

        public IEnumerable<string> WindowTitleMatchModeOptions
        {
            get { return WindowTitleMatchModeOptionsSource; }
        }

        public IEnumerable<string> FrameActionOptions
        {
            get { return FrameActionOptionsSource; }
        }

        public IEnumerable<string> LoopModeOptions
        {
            get { return LoopModeOptionsSource; }
        }

        public string StatusText
        {
            get
            {
                switch (ExecutionStatus)
                {
                    case ExecutionStatus.Running:
                        return "运行中";
                    case ExecutionStatus.Completed:
                        return "已完成";
                    case ExecutionStatus.Failed:
                        return "执行失败";
                    case ExecutionStatus.Cancelled:
                        return "已停止";
                    default:
                        return "空闲";
                }
            }
        }

        public string WindowTitle
        {
            get { return string.Format("IE RPA - {0} - {1}", GetShellPageTitle(SelectedShellPage), CurrentWorkflowName); }
        }

        public RelayCommand NewWorkflowCommand { get; private set; }
        public RelayCommand AddStepCommand { get; private set; }
        public RelayCommand RemoveSelectedStepCommand { get; private set; }
        public RelayCommand MoveStepUpCommand { get; private set; }
        public RelayCommand MoveStepDownCommand { get; private set; }
        public AsyncRelayCommand SaveWorkflowCommand { get; private set; }
        public AsyncRelayCommand LoadWorkflowCommand { get; private set; }
        public AsyncRelayCommand OpenElementPickerCommand { get; private set; }
        public AsyncRelayCommand OpenParameterElementPickerCommand { get; private set; }
        public AsyncRelayCommand RunWorkflowCommand { get; private set; }
        public AsyncRelayCommand RunFromSelectedStepCommand { get; private set; }
        public AsyncRelayCommand RunSelectedStepCommand { get; private set; }
        public RelayCommand StopWorkflowCommand { get; private set; }
        public RelayCommand EditEmployeeIdCommand { get; private set; }
        public RelayCommand ApplyPendingBusinessResumeCommand { get; private set; }
        public RelayCommand ClearPendingBusinessResumeCommand { get; private set; }

        private static IWorkflowRunner BuildWorkflowRunner(ILogService logService)
        {
            var browserService = new IeBrowserService();
            var variableResolver = new VariableResolver();
            var desktopInteractionService = new DesktopInteractionService();
            var httpFileUploadService = new HttpFileUploadService();
            var executors = new List<IStepExecutor>
            {
                new LaunchIeStepExecutor(browserService, variableResolver),
                new AttachIeStepExecutor(browserService),
                new NavigateStepExecutor(variableResolver),
                new HttpGetDataStepExecutor(variableResolver),
                new WaitForElementStepExecutor(variableResolver),
                new WaitPageReadyStepExecutor(),
                new ClickElementStepExecutor(variableResolver),
                new ClickAndSwitchWindowStepExecutor(browserService, variableResolver),
                new InputTextStepExecutor(variableResolver),
                new ReadTextStepExecutor(variableResolver),
                new SelectOptionStepExecutor(variableResolver),
                new SwitchFrameStepExecutor(variableResolver),
                new PageListLoopStepExecutor(browserService, desktopInteractionService, variableResolver, httpFileUploadService),
                new QueryAndExportReportStepExecutor(variableResolver, httpFileUploadService),
                new HttpUploadFileStepExecutor(variableResolver, httpFileUploadService),
                new UpdateBusinessStateStepExecutor(variableResolver),
                new ExecuteScriptStepExecutor(variableResolver),
                new SwitchWindowStepExecutor(browserService, variableResolver),
                new UploadFileStepExecutor(variableResolver),
                new HandleAlertStepExecutor(desktopInteractionService, variableResolver),
                new WaitDownloadStepExecutor(variableResolver),
                new ScreenshotStepExecutor(desktopInteractionService, variableResolver),
                new DelayStepExecutor(),
                new SetVariableStepExecutor(),
                new WriteLogStepExecutor(variableResolver),
                new ConditionStepExecutor(variableResolver),
                new LoopStartStepExecutor(),
                new LoopEndStepExecutor(),
                new LoopStepExecutor(variableResolver)
            };

            return new WorkflowRunner(new StepExecutorFactory(executors), logService, desktopInteractionService);
        }

        private static string GetDefaultStateFilePath()
        {
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "State");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "designer-state.xml");
        }

        private static string GetDefaultBusinessStateFilePath()
        {
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "State");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "business-state.xml");
        }

        private void ApplyApplicationState(ApplicationState state)
        {
            _suspendAutoSave = true;
            try
            {
                _logService.Clear();
                Logs.Clear();
                ExecutionStatus = ExecutionStatus.Idle;
                EmployeeId = state != null ? state.EmployeeId : string.Empty;

                var workflow = EnsureWorkflow(state != null ? state.Workflow : null);
                ReplaceWorkflow(workflow);

                var selectedStep = CurrentWorkflow != null && CurrentWorkflow.Steps != null
                    ? CurrentWorkflow.Steps.FirstOrDefault(item => string.Equals(item.Id, state != null ? state.SelectedStepId : null, StringComparison.OrdinalIgnoreCase))
                    : null;
                SelectedWorkflowStep = selectedStep ?? (CurrentWorkflow != null ? CurrentWorkflow.Steps.FirstOrDefault() : null);
            }
            finally
            {
                _suspendAutoSave = false;
                NotifySelectedStepEditorStateChanged();
            }
        }

        private void ApplyPendingBusinessResume()
        {
            if (!HasPendingBusinessResume || CurrentWorkflow == null || CurrentWorkflow.Steps == null || CurrentWorkflow.Steps.Count == 0)
            {
                RefreshPendingBusinessResumeBindings();
                return;
            }

            var stepIndex = ResolveResumeStepIndex(CurrentWorkflow, _pendingBusinessResumeRecord);
            if (stepIndex >= 0 && stepIndex < CurrentWorkflow.Steps.Count)
            {
                SelectedWorkflowStep = CurrentWorkflow.Steps[stepIndex];
                _logService.Log(LogLevel.Info, "已定位到业务恢复步骤：" + SelectedWorkflowStep.Name);
            }
            else
            {
                _logService.Log(LogLevel.Warning, "未找到与当前业务阶段匹配的恢复步骤，请检查步骤参数中的 resumeStages 配置。");
            }

            RefreshPendingBusinessResumeBindings();
        }

        private void ClearPendingBusinessResume()
        {
            _pendingBusinessResumeRecord = null;
            RefreshPendingBusinessResumeBindings();
        }

        private int ResolveResumeStepIndex(WorkflowDefinition workflow, BusinessStateRecord state)
        {
            if (workflow == null || workflow.Steps == null || state == null)
            {
                return -1;
            }

            var stageToken = state.Stage.ToString();
            for (var index = 0; index < workflow.Steps.Count; index++)
            {
                var step = workflow.Steps[index];
                if (step == null || step.Parameters == null)
                {
                    continue;
                }

                string resumeStages;
                if (!step.Parameters.TryGetValue("resumeStages", out resumeStages) && !step.Parameters.TryGetValue("resumeStage", out resumeStages))
                {
                    continue;
                }

                var tokens = (resumeStages ?? string.Empty)
                    .Split(new[] { ',', ';', '|'}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim());
                if (tokens.Any(item => string.Equals(item, stageToken, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item, "Any", StringComparison.OrdinalIgnoreCase)))
                {
                    return index;
                }
            }

            return -1;
        }

        private void RefreshPendingBusinessResumeBindings()
        {
            OnPropertyChanged("HasPendingBusinessResume");
            OnPropertyChanged("PendingBusinessResumeText");
            if (ApplyPendingBusinessResumeCommand != null)
            {
                ApplyPendingBusinessResumeCommand.RaiseCanExecuteChanged();
            }

            if (ClearPendingBusinessResumeCommand != null)
            {
                ClearPendingBusinessResumeCommand.RaiseCanExecuteChanged();
            }
        }

        private void InjectBusinessStateVariables(WpfApplication1.Workflow.ExecutionContext context)
        {
            if (context == null)
            {
                return;
            }

            context.Variables["BusinessStateFilePath"] = _businessStateFilePath;
            BusinessStateSupport.SyncVariables(context);
            if (context.CurrentBusinessState != null && context.CurrentBusinessState.FetchedAt.HasValue)
            {
                context.Variables["BusinessState.FetchedAt"] = context.CurrentBusinessState.FetchedAt.Value.ToString("o");
            }
        }

        private async Task PersistFailedBusinessStateAsync(WpfApplication1.Workflow.ExecutionContext context, string errorMessage)
        {
            if (context == null || context.CurrentBusinessState == null)
            {
                return;
            }

            context.CurrentBusinessState.Stage = BusinessStateStage.Failed;
            context.CurrentBusinessState.ErrorMessage = errorMessage;
            context.CurrentBusinessState.IsCompleted = false;
            context.CurrentBusinessState.RetryCount++;
            BusinessStateSupport.SyncVariables(context);
            await BusinessStateSupport.PersistAsync(context);
            _pendingBusinessResumeRecord = context.CurrentBusinessState.Clone();
            RefreshPendingBusinessResumeBindings();
        }
        private WorkflowDefinition EnsureWorkflow(WorkflowDefinition workflow)
        {
            if (workflow == null)
            {
                return BuildDefaultWorkflow();
            }

            if (string.IsNullOrWhiteSpace(workflow.Id))
            {
                workflow.Id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(workflow.Name))
            {
                workflow.Name = "未命名流程";
            }

            if (string.IsNullOrWhiteSpace(workflow.Version))
            {
                workflow.Version = "0.1.0";
            }

            if (workflow.Description == null)
            {
                workflow.Description = string.Empty;
            }

            if (workflow.ApplicableRole == null)
            {
                workflow.ApplicableRole = string.Empty;
            }

            if (workflow.Steps == null)
            {
                workflow.Steps = new ObservableCollection<WorkflowStep>();
            }

            foreach (var step in workflow.Steps)
            {
                EnsureStep(step);
            }

            workflow.EnsureCanvasLayout();
            return workflow;
        }

        private static void EnsureStep(WorkflowStep step)
        {
            if (step == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(step.Id))
            {
                step.Id = Guid.NewGuid().ToString("N");
            }

            if (step.Parameters == null)
            {
                step.Parameters = new StepParameterBag();
            }
        }

        private void ReplaceWorkflow(WorkflowDefinition workflow)
        {
            DetachWorkflowSubscriptions(CurrentWorkflow);
            CurrentWorkflow = workflow;
            AttachWorkflowSubscriptions(CurrentWorkflow);
        }

        private void AttachWorkflowSubscriptions(WorkflowDefinition workflow)
        {
            if (workflow == null)
            {
                return;
            }

            workflow.PropertyChanged += Workflow_OnPropertyChanged;
            if (workflow.Steps != null)
            {
                workflow.Steps.CollectionChanged += WorkflowSteps_OnCollectionChanged;
                foreach (var step in workflow.Steps)
                {
                    AttachStepSubscriptions(step);
                }
            }
        }

        private void DetachWorkflowSubscriptions(WorkflowDefinition workflow)
        {
            if (workflow == null)
            {
                return;
            }

            workflow.PropertyChanged -= Workflow_OnPropertyChanged;
            if (workflow.Steps != null)
            {
                workflow.Steps.CollectionChanged -= WorkflowSteps_OnCollectionChanged;
                foreach (var step in workflow.Steps)
                {
                    DetachStepSubscriptions(step);
                }
            }
        }

        private void AttachStepSubscriptions(WorkflowStep step)
        {
            if (step != null)
            {
                step.PropertyChanged += WorkflowStep_OnPropertyChanged;
            }
        }

        private void DetachStepSubscriptions(WorkflowStep step)
        {
            if (step != null)
            {
                step.PropertyChanged -= WorkflowStep_OnPropertyChanged;
            }
        }

        private void Workflow_OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged("CurrentWorkflowName");
            OnPropertyChanged("WorkflowSummary");
            OnPropertyChanged("WindowTitle");
            RefreshDesignerCanvas();
                    ScheduleStateSave();
        }

        private void WorkflowSteps_OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (WorkflowStep step in e.OldItems)
                {
                    DetachStepSubscriptions(step);
                }
            }

            if (e.NewItems != null)
            {
                foreach (WorkflowStep step in e.NewItems)
                {
                    EnsureStep(step);
                    AttachStepSubscriptions(step);
                }
            }

            OnPropertyChanged("WorkflowSummary");
            MoveStepUpCommand.RaiseCanExecuteChanged();
            MoveStepDownCommand.RaiseCanExecuteChanged();
            RunWorkflowCommand.RaiseCanExecuteChanged();
            RefreshDesignerCanvas();
                    ScheduleStateSave();
        }

        private void WorkflowStep_OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (ReferenceEquals(sender, SelectedWorkflowStep))
            {
                OnPropertyChanged("SelectedStepParametersText");
                OnPropertyChanged("SelectedStepParametersEditor");
                if (string.Equals(e.PropertyName, "StepType", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(e.PropertyName, "Parameters", StringComparison.OrdinalIgnoreCase))
                {
                    NotifySelectedStepEditorStateChanged();
                }
            }

            RefreshDesignerCanvas();
                    ScheduleStateSave();
        }

        private WorkflowDefinition BuildDefaultWorkflow()
        {
            return _workflowTemplateFactory.CreateWorkflow(new WorkflowCreateRequest
            {
                WorkflowType = WorkflowType.General,
                Name = "未命名通用流程",
                Description = "默认草稿模板，可直接在纯画布设计器中继续完善。",
                ApplicableRole = string.Empty
            });
        }

        private void CreateNewWorkflow()
        {
            _logService.Clear();
            Logs.Clear();
            ExecutionStatus = ExecutionStatus.Idle;
            ReplaceWorkflow(BuildDefaultWorkflow());
            CurrentWorkflowPath = string.Empty;
            SelectedWorkflowStep = CurrentWorkflow.Steps.FirstOrDefault();
            _logService.Log(LogLevel.Info, "已创建新的通用草稿流程。");
            RefreshDesignerCanvas();
                    ScheduleStateSave();
        }

        private void AddSelectedToolboxStep()
        {
            if (SelectedToolboxStep == null || CurrentWorkflow == null)
            {
                return;
            }

            var step = CreateStepFromToolbox(SelectedToolboxStep);
            var insertIndex = SelectedWorkflowStep != null ? CurrentWorkflow.Steps.IndexOf(SelectedWorkflowStep) + 1 : CurrentWorkflow.Steps.Count;
            if (insertIndex < 0 || insertIndex > CurrentWorkflow.Steps.Count)
            {
                insertIndex = CurrentWorkflow.Steps.Count;
            }

            CurrentWorkflow.Steps.Insert(insertIndex, step);
            SelectedWorkflowStep = step;
            SelectedShellPage = ShellPage.Designer;
        }

        private void RemoveSelectedStep()
        {
            if (SelectedWorkflowStep == null || CurrentWorkflow == null)
            {
                return;
            }

            var index = CurrentWorkflow.Steps.IndexOf(SelectedWorkflowStep);
            CurrentWorkflow.Steps.Remove(SelectedWorkflowStep);
            SelectedWorkflowStep = CurrentWorkflow.Steps.Count == 0
                ? null
                : CurrentWorkflow.Steps[Math.Max(0, index - 1)];
        }

        private bool CanMoveSelectedStepUp()
        {
            return SelectedWorkflowStep != null
                   && CurrentWorkflow != null
                   && CurrentWorkflow.Steps.IndexOf(SelectedWorkflowStep) > 0;
        }

        private bool CanMoveSelectedStepDown()
        {
            return SelectedWorkflowStep != null
                   && CurrentWorkflow != null
                   && CurrentWorkflow.Steps.IndexOf(SelectedWorkflowStep) >= 0
                   && CurrentWorkflow.Steps.IndexOf(SelectedWorkflowStep) < CurrentWorkflow.Steps.Count - 1;
        }

        private void MoveSelectedStepUp()
        {
            if (!CanMoveSelectedStepUp())
            {
                return;
            }

            var index = CurrentWorkflow.Steps.IndexOf(SelectedWorkflowStep);
            CurrentWorkflow.Steps.Move(index, index - 1);
        }

        private void MoveSelectedStepDown()
        {
            if (!CanMoveSelectedStepDown())
            {
                return;
            }

            var index = CurrentWorkflow.Steps.IndexOf(SelectedWorkflowStep);
            CurrentWorkflow.Steps.Move(index, index + 1);
        }

        private async Task SaveWorkflowAsync()
        {
            if (CurrentWorkflow == null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "导出流程文件",
                Filter = "IE RPA 流程文件 (*.ierpa.json)|*.ierpa.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                InitialDirectory = GetWorkflowFilesDirectory(),
                FileName = SanitizeWorkflowFileName(CurrentWorkflowName) + ".ierpa.json"
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                await _workflowFileService.SaveAsync(dialog.FileName, CurrentWorkflow);
                _logService.Log(LogLevel.Info, "流程已导出到 " + dialog.FileName + "。");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, "导出流程失败：" + ex.Message);
            }
        }

        private async Task LoadWorkflowAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "导入流程文件",
                Filter = "IE RPA 流程文件 (*.ierpa.json)|*.ierpa.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                InitialDirectory = GetWorkflowFilesDirectory(),
                CheckFileExists = true
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var workflow = EnsureWorkflow(await _workflowFileService.LoadAsync(dialog.FileName));
                ReplaceWorkflow(workflow);
                CurrentWorkflowPath = string.Empty;
                SelectedWorkflowStep = CurrentWorkflow != null ? CurrentWorkflow.Steps.FirstOrDefault() : null;
                NotifySelectedStepEditorStateChanged();
                RefreshDesignerCanvas();
                    ScheduleStateSave();
                _logService.Log(LogLevel.Info, "已从 " + dialog.FileName + " 导入流程文件。");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, "导入流程失败：" + ex.Message);
            }
        }

        private async Task OpenElementPickerAsync()
        {
            if (!CanPickElementForSelectedStep())
            {
                MessageBox.Show("当前步骤不需要页面元素选择器。", "元素拾取器", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await OpenElementPickerCoreAsync(ResolveSelectorParameterName(SelectedWorkflowStep.StepType));
        }

        private async Task OpenParameterElementPickerAsync(object parameter)
        {
            var parameterName = parameter != null ? Convert.ToString(parameter) : string.Empty;
            if (!CanPickElementForParameter(parameter))
            {
                MessageBox.Show("当前字段暂不支持页面元素拾取。", "元素拾取器", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await OpenElementPickerCoreAsync(parameterName);
        }

        private async Task OpenElementPickerCoreAsync(string parameterName)
        {
            IsPreparingElementPicker = true;
            try
            {
                _logService.Log(LogLevel.Info, "开始准备页面元素拾取。", SelectedWorkflowStep);
                var owner = Application.Current != null ? Application.Current.MainWindow : null;
                var loadingWindow = new ElementPickerLoadingWindow();
                if (owner != null)
                {
                    loadingWindow.Owner = owner;
                }

                loadingWindow.Show();
                await loadingWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

                var pickerWindow = new ElementPickerWindow(selector => ApplySelectorToStep(parameterName, selector), false, true, LogElementPickerTrace);
                pickerWindow.Show();
                var started = await pickerWindow.BeginAutoPickAsync();
                loadingWindow.Close();

                if (started)
                {
                    _logService.Log(LogLevel.Info, "页面元素拾取已启动，请前往 IE 页面点击目标元素。", SelectedWorkflowStep);
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "页面元素拾取未能成功启动。", SelectedWorkflowStep);
                }
            }
            finally
            {
                IsPreparingElementPicker = false;
            }
        }

        private void LogElementPickerTrace(LogLevel level, string message)
        {
            _logService.Log(level, "[元素拾取] " + message, SelectedWorkflowStep);
        }

        private void ApplySelectorToSelectedStep(string selector)
        {
            if (SelectedWorkflowStep == null || string.IsNullOrWhiteSpace(selector))
            {
                return;
            }

            ApplySelectorToStep(ResolveSelectorParameterName(SelectedWorkflowStep.StepType), selector);
        }

        private void ApplySelectorToStep(string parameterName, string selector)
        {
            if (SelectedWorkflowStep == null || string.IsNullOrWhiteSpace(selector) || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            if (SelectedWorkflowStep.Parameters == null)
            {
                SelectedWorkflowStep.Parameters = new StepParameterBag();
            }

            SelectedWorkflowStep.Parameters[parameterName] = selector;
            OnPropertyChanged("SelectedStepParametersText");
            OnPropertyChanged("SelectedStepParametersEditor");
            NotifySelectedStepEditorStateChanged();
            _logService.Log(LogLevel.Info, "已把元素选择器写入当前步骤：" + parameterName + " = " + selector, SelectedWorkflowStep);
            RefreshDesignerCanvas();
                    ScheduleStateSave();
        }

        private static string ResolveSelectorParameterName(StepType? stepType)
        {
            switch (stepType)
            {
                case StepType.ClickAndSwitchWindow:
                    return "clickSelector";
                case StepType.WaitForElement:
                case StepType.ClickElement:
                case StepType.InputText:
                case StepType.ReadText:
                case StepType.SelectOption:
                case StepType.SwitchFrame:
                case StepType.UploadFile:
                    return "selector";
                default:
                    return "selector";
            }
        }

        private bool CanPickElementForSelectedStep()
        {
            return ShowSelectorEditor && !IsPreparingElementPicker;
        }

        private bool CanPickElementForParameter(object parameter)
        {
            return SelectedWorkflowStep != null
                   && !IsPreparingElementPicker
                   && !string.IsNullOrWhiteSpace(parameter != null ? Convert.ToString(parameter) : string.Empty);
        }

        private bool CanRunWorkflow()
        {
            return CurrentWorkflow != null
                   && CurrentWorkflow.Steps.Count > 0
                   && ExecutionStatus != ExecutionStatus.Running;
        }

        private bool CanRunFromSelectedStep()
        {
            return SelectedWorkflowStep != null
                   && CurrentWorkflow != null
                   && ExecutionStatus != ExecutionStatus.Running;
        }

        private bool CanRunSelectedStep()
        {
            return SelectedWorkflowStep != null
                   && CurrentWorkflow != null
                   && ExecutionStatus != ExecutionStatus.Running;
        }

        private async Task RunWorkflowAsync()
        {
            await RunWorkflowInternalAsync(0, null, "流程执行完成。", "手动执行");
        }

        private async Task RunFromSelectedStepAsync()
        {
            if (SelectedWorkflowStep == null || CurrentWorkflow == null)
            {
                return;
            }

            var startIndex = CurrentWorkflow.Steps.IndexOf(SelectedWorkflowStep);
            await RunWorkflowInternalAsync(startIndex, null, "已从选中步骤开始执行到流程结束。", "从当前节点继续执行");
        }

        private async Task RunSelectedStepAsync()
        {
            if (SelectedWorkflowStep == null || CurrentWorkflow == null)
            {
                return;
            }

            var startIndex = CurrentWorkflow.Steps.IndexOf(SelectedWorkflowStep);
            await RunWorkflowInternalAsync(startIndex, 1, "单步执行完成。", "单步调试");
        }

        private async Task RunWorkflowInternalAsync(int startStepIndex, int? maxSteps, string completionMessage, string runName)
        {
            _logService.Clear();
            Logs.Clear();
            _cancellationTokenSource = new CancellationTokenSource();
            ExecutionStatus = ExecutionStatus.Running;
            var history = new RunHistoryItem
            {
                WorkflowName = CurrentWorkflowName,
                Mode = "manual",
                StartedAt = DateTime.Now
            };
            _logService.BeginRun(CurrentWorkflow, CurrentWorkflowPath, "manual", runName);
            var context = new WpfApplication1.Workflow.ExecutionContext(_cancellationTokenSource.Token)
            {
                BusinessStateStore = _businessStateStore,
                BusinessStatePath = _businessStateFilePath,
                CurrentBusinessState = _pendingBusinessResumeRecord != null ? _pendingBusinessResumeRecord.Clone() : null,
                SchedulerMode = "manual",
                CurrentWorkflowPath = CurrentWorkflowPath,
                WorkflowId = CurrentWorkflow != null ? CurrentWorkflow.Id : string.Empty,
                WorkflowName = CurrentWorkflow != null ? CurrentWorkflow.Name : CurrentWorkflowName,
                WorkflowType = CurrentWorkflow != null ? CurrentWorkflow.WorkflowType : WorkflowType.General,
                RunId = _logService.CurrentRun != null ? _logService.CurrentRun.RunId : string.Empty,
                RunName = _logService.CurrentRun != null ? _logService.CurrentRun.RunName : runName
            };
            context.RuntimeStateChanged += snapshot => ExecuteOnUi(() => UpdateRuntimeStateSnapshot(snapshot));
            context.UpdateRuntimeState(state =>
            {
                state.CurrentWorkflowName = CurrentWorkflowName;
                state.CurrentWorkflowPath = CurrentWorkflowPath;
                state.CurrentMode = "manual";
                state.FramePathDisplay = "root";
            });

            string failureMessage = null;
            var finalResult = "Success";
            var finalSummary = completionMessage;

            try
            {
                context.Variables["EmployeeId"] = EmployeeId ?? string.Empty;
                context.Variables["JobNo"] = EmployeeId ?? string.Empty;
                InjectBusinessStateVariables(context);
                await _workflowRunner.RunAsync(CurrentWorkflow, context, startStepIndex, maxSteps);
                if (context.CurrentBusinessState != null)
                {
                    _pendingBusinessResumeRecord = context.CurrentBusinessState.Clone();
                    if (_pendingBusinessResumeRecord.IsCompleted)
                    {
                        _pendingBusinessResumeRecord = null;
                    }

                    RefreshPendingBusinessResumeBindings();
                }

                history.Result = "Success";
                ExecutionStatus = ExecutionStatus.Completed;
                _logService.Log(LogLevel.Info, completionMessage, null, null, context, "WorkflowCompleted");
            }
            catch (OperationCanceledException)
            {
                failureMessage = "流程执行已取消。";
                history.Result = "Cancelled";
                history.ErrorSummary = failureMessage;
                ExecutionStatus = ExecutionStatus.Cancelled;
                finalResult = "Cancelled";
                finalSummary = failureMessage;
                _logService.Log(LogLevel.Warning, failureMessage, null, null, context, "WorkflowCancelled");
            }
            catch (Exception ex)
            {
                failureMessage = ex.Message;
                history.Result = "Failed";
                history.ErrorSummary = ex.Message;
                ExecutionStatus = ExecutionStatus.Failed;
                finalResult = "Failed";
                finalSummary = ex.Message;
                _logService.Log(LogLevel.Error, "流程执行失败：" + ex.Message, null, null, context, "WorkflowFailed", 0, 0, ex);
            }
            if (!string.IsNullOrWhiteSpace(failureMessage) && context.CurrentBusinessState != null)
            {
                await PersistFailedBusinessStateAsync(context, failureMessage);
            }

            history.EndedAt = DateTime.Now;
            history.Duration = history.EndedAt.Value - history.StartedAt;
            AddRunHistoryItem(history);
            RefreshTaskSummaryFromState();
            _logService.EndRun(finalResult, finalSummary);
            await RefreshLogCenterDataAsync();

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }
        private void StopWorkflow()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        private void EditEmployeeId()
        {
            var dialog = new EmployeeIdDialog(EmployeeId, false);
            if (Application.Current != null && Application.Current.MainWindow != null)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            var result = dialog.ShowDialog();
            if (result != true)
            {
                return;
            }

            EmployeeId = dialog.EmployeeId;
            _logService.Log(LogLevel.Info, "当前工号已更新为：" + EmployeeId);
            RefreshDesignerCanvas();
                    ScheduleStateSave();
        }

        private void OnLogEntryAdded(object sender, ExecutionLogEntry entry)
        {
            if (Application.Current == null)
            {
                Logs.Add(entry);
                return;
            }

            Application.Current.Dispatcher.Invoke(() => Logs.Add(entry));
        }

        private void AutoSaveTimer_OnTick(object sender, EventArgs e)
        {
            _autoSaveTimer.Stop();
            var task = SaveStateCoreAsync();
            task.ContinueWith(_ => { });
        }

        private void ScheduleStateSave()
        {
            if (_suspendAutoSave)
            {
                return;
            }

            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private async Task<bool> SaveStateCoreAsync()
        {
            try
            {
                await _applicationStateStore.SaveAsync(_stateFilePath, BuildApplicationState());
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, "保存设计器状态失败：" + ex.Message);
                return false;
            }
        }

        private ApplicationState BuildApplicationState()
        {
            return new ApplicationState
            {
                EmployeeId = EmployeeId,
                SelectedStepId = SelectedWorkflowStep != null ? SelectedWorkflowStep.Id : string.Empty,
                Workflow = CurrentWorkflow,
                ActiveWorkflowPath = CurrentWorkflowPath,
                ActiveWorkflowId = CurrentWorkflow != null ? CurrentWorkflow.Id : string.Empty,
                SelectedShellPage = SelectedShellPage,
                SchedulerSettings = SchedulerSettings != null ? SchedulerSettings.Clone() : BuildDefaultSchedulerSettings(),
                DesignerLeftPaneWidth = DesignerLeftPaneWidth,
                DesignerRightPaneWidth = DesignerRightPaneWidth,
                LogDrawerHeight = LogDrawerHeight,
                IsLogDrawerExpanded = IsLogDrawerExpanded,
                WorkflowManagementFilterType = SelectedWorkflowFilterType,
                DesignerZoom = DesignerZoom,
                CanvasViewportOffsetX = DesignerCanvasViewportOffsetX,
                CanvasViewportOffsetY = DesignerCanvasViewportOffsetY,
                SelectedLogWorkflowId = SelectedLogWorkflow != null ? SelectedLogWorkflow.WorkflowId : string.Empty,
                SelectedLogRunId = SelectedWorkflowLogRun != null ? SelectedWorkflowLogRun.RunId : string.Empty,
                LogCenterFilterType = SelectedLogFilterType,
                LogCenterSearchText = LogCenterSearchText
            };
        }

        private string GetStepParameter(string key)
        {
            if (SelectedWorkflowStep == null || string.IsNullOrWhiteSpace(key) || SelectedWorkflowStep.Parameters == null)
            {
                return string.Empty;
            }

            string value;
            return SelectedWorkflowStep.Parameters.TryGetValue(key, out value) ? value : string.Empty;
        }

        private void SetStepParameter(string key, string value)
        {
            if (SelectedWorkflowStep == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (SelectedWorkflowStep.Parameters == null)
            {
                SelectedWorkflowStep.Parameters = new StepParameterBag();
            }

            SelectedWorkflowStep.Parameters[key] = value ?? string.Empty;
            OnPropertyChanged("SelectedStepParametersText");
            OnPropertyChanged("SelectedStepParametersEditor");
            NotifySelectedStepEditorStateChanged();
            RefreshDesignerCanvas();
                    ScheduleStateSave();
        }

        private StepType? GetSelectedStepType()
        {
            return SelectedWorkflowStep != null ? (StepType?)SelectedWorkflowStep.StepType : null;
        }

        private void NotifySelectedStepEditorStateChanged()
        {
            RefreshCurrentStepParameterGroups();
            OnPropertyChanged("HasStructuredStepEditor");
            OnPropertyChanged("ShowUrlEditor");
            OnPropertyChanged("ShowSelectorEditor");
            OnPropertyChanged("ShowWaitForElementEditor");
            OnPropertyChanged("ShowInputTextEditor");
            OnPropertyChanged("ShowReadTextEditor");
            OnPropertyChanged("ShowDataVariableEditor");
            OnPropertyChanged("ShowSelectOptionEditor");
            OnPropertyChanged("ShowHandleAlertEditor");
            OnPropertyChanged("ShowSetVariableEditor");
            OnPropertyChanged("ShowWriteLogEditor");
            OnPropertyChanged("ShowDelayEditor");
            OnPropertyChanged("ShowSwitchFrameEditor");
            OnPropertyChanged("ShowClickAndSwitchWindowEditor");
            OnPropertyChanged("ShowSwitchWindowEditor");
            OnPropertyChanged("ShowLoopStartEditor");
            OnPropertyChanged("ShowLoopEndEditor");
            OnPropertyChanged("ShowHttpUploadFileEditor");
            OnPropertyChanged("ShowPageListLoopEditor");
            OnPropertyChanged("ShowQueryAndExportReportEditor");
            OnPropertyChanged("UrlEditorLabel");
            OnPropertyChanged("SelectorEditorLabel");
            OnPropertyChanged("ElementPickerButtonText");
            OnPropertyChanged("UrlParameterValue");
            OnPropertyChanged("SelectorParameterValue");
            OnPropertyChanged("WaitPollIntervalParameterValue");
            OnPropertyChanged("InputTextParameterValue");
            OnPropertyChanged("ReadVariableNameParameterValue");
            OnPropertyChanged("DataVariableNameParameterValue");
            OnPropertyChanged("OptionParameterValue");
            OnPropertyChanged("MatchModeParameterValue");
            OnPropertyChanged("AlertActionParameterValue");
            OnPropertyChanged("AlertButtonTextParameterValue");
            OnPropertyChanged("AlertTitleContainsParameterValue");
            OnPropertyChanged("SetVariableNameParameterValue");
            OnPropertyChanged("SetVariableValueParameterValue");
            OnPropertyChanged("WriteLogMessageParameterValue");
            OnPropertyChanged("DelayDurationParameterValue");
            OnPropertyChanged("SwitchFrameActionParameterValue");
            OnPropertyChanged("ClickAndSwitchWindowTitleParameterValue");
            OnPropertyChanged("ClickAndSwitchWindowMatchModeParameterValue");
            OnPropertyChanged("ClickAndSwitchWindowPollIntervalParameterValue");
            OnPropertyChanged("ClickAndSwitchWindowExcludeCurrentParameterValue");
            OnPropertyChanged("SwitchWindowTitleParameterValue");
            OnPropertyChanged("SwitchWindowModeParameterValue");
            OnPropertyChanged("SwitchWindowUrlParameterValue");
            OnPropertyChanged("LoopStartKeyParameterValue");
            OnPropertyChanged("RequiredVariablesParameterValue");
            OnPropertyChanged("IterationVariableParameterValue");
            OnPropertyChanged("LoopEndKeyParameterValue");
            OnPropertyChanged("LoopModeParameterValue");
            OnPropertyChanged("LoopTimesParameterValue");
            OnPropertyChanged("LoopIntervalParameterValue");
            OnPropertyChanged("HttpUploadUrlParameterValue");
            OnPropertyChanged("HttpUploadFilePathParameterValue");
            OnPropertyChanged("HttpUploadResponseVariableParameterValue");
            OnPropertyChanged("PageListModeParameterValue");
            OnPropertyChanged("FilterSelectorParameterValue");
            OnPropertyChanged("FilterValueParameterValue");
            OnPropertyChanged("QueryButtonSelectorParameterValue");
            OnPropertyChanged("ListReadySelectorParameterValue");
            OnPropertyChanged("RowSelectorTemplateParameterValue");
            OnPropertyChanged("RowActionSelectorTemplateParameterValue");
            OnPropertyChanged("PageListMaxRowsParameterValue");
            OnPropertyChanged("PageListMaxRoundsParameterValue");
            OnPropertyChanged("PageListPollIntervalParameterValue");
            OnPropertyChanged("PageListTargetWindowTitleParameterValue");
            OnPropertyChanged("PageListWindowMatchModeParameterValue");
            OnPropertyChanged("DetailReadySelectorParameterValue");
            OnPropertyChanged("DetailActionSelectorParameterValue");
            OnPropertyChanged("PageListReturnModeParameterValue");
            OnPropertyChanged("PageListReturnSelectorParameterValue");
            OnPropertyChanged("PageListReturnButtonTextParameterValue");
            OnPropertyChanged("PageListReturnTitleContainsParameterValue");
            OnPropertyChanged("PopupReadySelectorParameterValue");
            OnPropertyChanged("PopupPollIntervalParameterValue");
            OnPropertyChanged("ReportIframeSelectorParameterValue");
            OnPropertyChanged("SaveDirectoryParameterValue");
            OnPropertyChanged("FileNameTemplateParameterValue");
            OnPropertyChanged("ReportUploadUrlParameterValue");
            OnPropertyChanged("ClosePopupSelectorParameterValue");
            OnPropertyChanged("OutputFileVariableNameParameterValue");
            OnPropertyChanged("UploadResponseVariableNameParameterValue");
            OnPropertyChanged("HasPendingBusinessResume");
            OnPropertyChanged("PendingBusinessResumeText");
        }

        private static IEnumerable<ToolboxStepDefinition> BuildToolboxSteps()
        {
            return new[]
            {
                CreateToolboxStep(StepType.LaunchIe, "启动 IE", "启动一个新的 IE11 浏览器实例。"),
                CreateToolboxStep(StepType.AttachIe, "附加 IE", "连接到当前已打开的 IE11 窗口。"),
                CreateToolboxStep(StepType.Navigate, "打开网页", "导航到指定地址并等待页面跳转。"),
                CreateToolboxStep(StepType.HttpGetData, "GET 请求数据", "发起 GET 请求并把结果写入流程变量。"),
                CreateToolboxStep(StepType.WaitForElement, "等待元素出现", "轮询当前页面，直到目标元素出现或超时。"),
                CreateToolboxStep(StepType.WaitPageReady, "等待页面完成", "等待页面或脚本加载稳定。"),
                CreateToolboxStep(StepType.ClickElement, "点击元素", "点击按钮、链接或其他可交互元素。"),
                CreateToolboxStep(StepType.ClickAndSwitchWindow, "点击并切换新窗口", "点击页面元素后，按标题匹配并切换到目标窗口。"),
                CreateToolboxStep(StepType.InputText, "输入文本", "向输入框写入文本内容。"),
                CreateToolboxStep(StepType.ReadText, "读取文本", "读取页面文本并保存到变量。"),
                CreateToolboxStep(StepType.SelectOption, "选择下拉项", "选择 select 控件中的目标选项。"),
                CreateToolboxStep(StepType.SwitchFrame, "切换 Frame", "进入、返回父级或回到根页面的 frame/iframe。"),
                CreateToolboxStep(StepType.PageListLoop, "页面列表循环", "按查询刷新和从头检查模式逐条处理列表记录。"),
                CreateToolboxStep(StepType.QueryAndExportReport, "查询并导出报告", "抓取报告 iframe HTML，保存为本地文件并上传。"),
                CreateToolboxStep(StepType.HttpUploadFile, "HTTP 上传文件", "将本地文件以 multipart 方式上传到接口。"),
                CreateToolboxStep(StepType.UpdateBusinessState, "更新业务状态", "写入或更新本机 XML 业务状态。"),
                CreateToolboxStep(StepType.SwitchWindow, "切换窗口", "切换到新打开的业务窗口。"),
                CreateToolboxStep(StepType.ExecuteScript, "执行脚本", "运行一段页面 JavaScript。"),
                CreateToolboxStep(StepType.HandleAlert, "处理弹窗", "接受或关闭 alert/confirm 对话框。"),
                CreateToolboxStep(StepType.UploadFile, "上传文件", "处理文件选择框并上传本地文件。"),
                CreateToolboxStep(StepType.WaitDownload, "等待下载", "检查文件下载完成状态。"),
                CreateToolboxStep(StepType.SetVariable, "设置变量", "给流程变量赋值，供后续步骤引用。"),
                CreateToolboxStep(StepType.Condition, "条件判断", "根据变量或结果决定后续路径。"),
                CreateToolboxStep(StepType.LoopStart, "开始循环", "定义循环块入口，并检查循环变量。"),
                CreateToolboxStep(StepType.LoopEnd, "结束循环", "定义循环块出口，支持无限或固定次数循环。"),
                CreateToolboxStep(StepType.Loop, "循环（兼容旧流程）", "兼容旧版尾部 Loop 回跳逻辑。"),
                CreateToolboxStep(StepType.Delay, "延时等待", "在步骤之间插入固定等待时间。"),
                CreateToolboxStep(StepType.Screenshot, "截图", "保存当前页面或桌面的截图。"),
                CreateToolboxStep(StepType.WriteLog, "写日志", "向执行日志写入业务提示信息。")
            };
        }

        private static ToolboxStepDefinition CreateToolboxStep(StepType stepType, string name, string description)
        {
            return new ToolboxStepDefinition
            {
                StepType = stepType,
                Name = name,
                Description = description,
                Category = GetStepCategory(stepType)
            };
        }

        private static WorkflowStep CreateStepFromToolbox(ToolboxStepDefinition toolboxStep)
        {
            var step = new WorkflowStep
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = toolboxStep.Name,
                StepType = toolboxStep.StepType,
                Parameters = new StepParameterBag()
            };

            foreach (var definition in StepParameterDefinitionProvider.GetDefinitions(toolboxStep.StepType))
            {
                if (string.IsNullOrWhiteSpace(definition.Key) || string.IsNullOrWhiteSpace(definition.DefaultValue))
                {
                    continue;
                }

                step.Parameters[definition.Key] = definition.DefaultValue;
            }

            return step;
        }

        private static string GetWorkflowFilesDirectory()
        {
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Workflows");
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string SanitizeWorkflowFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "workflow";
            }

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(invalidChar, '_');
            }

            return raw;
        }

        private static StepParameterBag ParseParameters(string raw)
        {
            var parameters = new StepParameterBag();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return parameters;
            }

            var lines = raw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, separatorIndex).Trim();
                var value = line.Substring(separatorIndex + 1).Trim();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    parameters[key] = value;
                }
            }

            return parameters;
        }
    }
}











































