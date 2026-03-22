using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
    public class MainWindowViewModel : BindableBase
    {
        private static readonly string[] AlertActionOptionsSource = { "accept", "dismiss" };
        private static readonly string[] MatchModeOptionsSource = { "text", "value" };
        private readonly IWorkflowFileService _workflowFileService;
        private readonly ILogService _logService;
        private readonly IWorkflowRunner _workflowRunner;
        private readonly string _defaultWorkflowDirectory;
        private WorkflowDefinition _currentWorkflow;
        private WorkflowStep _selectedWorkflowStep;
        private ToolboxStepDefinition _selectedToolboxStep;
        private ExecutionStatus _executionStatus;
        private CancellationTokenSource _cancellationTokenSource;

        public MainWindowViewModel()
        {
            _workflowFileService = new WorkflowFileService();
            _logService = new InMemoryLogService();
            _workflowRunner = BuildWorkflowRunner(_logService);
            _defaultWorkflowDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Workflows");

            AvailableSteps = new ObservableCollection<ToolboxStepDefinition>(BuildToolboxSteps());
            Logs = new ObservableCollection<ExecutionLogEntry>();

            NewWorkflowCommand = new RelayCommand(CreateNewWorkflow);
            AddStepCommand = new RelayCommand(AddSelectedToolboxStep, () => SelectedToolboxStep != null);
            RemoveSelectedStepCommand = new RelayCommand(RemoveSelectedStep, () => SelectedWorkflowStep != null);
            MoveStepUpCommand = new RelayCommand(MoveSelectedStepUp, CanMoveSelectedStepUp);
            MoveStepDownCommand = new RelayCommand(MoveSelectedStepDown, CanMoveSelectedStepDown);
            SaveWorkflowCommand = new AsyncRelayCommand(SaveWorkflowAsync, () => CurrentWorkflow != null);
            LoadWorkflowCommand = new AsyncRelayCommand(LoadWorkflowAsync);
            OpenElementPickerCommand = new RelayCommand(OpenElementPicker, CanPickElementForSelectedStep);
            RunWorkflowCommand = new AsyncRelayCommand(RunWorkflowAsync, CanRunWorkflow);
            RunFromSelectedStepCommand = new AsyncRelayCommand(RunFromSelectedStepAsync, CanRunFromSelectedStep);
            RunSelectedStepCommand = new AsyncRelayCommand(RunSelectedStepAsync, CanRunSelectedStep);
            StopWorkflowCommand = new RelayCommand(StopWorkflow, () => ExecutionStatus == ExecutionStatus.Running);

            _logService.EntryAdded += OnLogEntryAdded;
            CreateNewWorkflow();
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
                    if (OpenElementPickerCommand != null)
                    {
                        OpenElementPickerCommand.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        public ToolboxStepDefinition SelectedToolboxStep
        {
            get { return _selectedToolboxStep; }
            set { SetProperty(ref _selectedToolboxStep, value); }
        }

        public ExecutionStatus ExecutionStatus
        {
            get { return _executionStatus; }
            private set
            {
                if (SetProperty(ref _executionStatus, value))
                {
                    OnPropertyChanged("StatusText");
                }
            }
        }

        public string CurrentWorkflowName
        {
            get { return CurrentWorkflow != null ? CurrentWorkflow.Name : "未命名流程"; }
        }

        public string WorkflowSummary
        {
            get
            {
                var stepCount = CurrentWorkflow != null ? CurrentWorkflow.Steps.Count : 0;
                return string.Format("共 {0} 个步骤，可在左侧选择步骤后添加到当前流程。", stepCount);
            }
        }

        public string SelectedStepParametersText
        {
            get
            {
                if (SelectedWorkflowStep == null || SelectedWorkflowStep.Parameters.Count == 0)
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
                       || ShowInputTextEditor
                       || ShowReadTextEditor
                       || ShowSelectOptionEditor
                       || ShowHandleAlertEditor
                       || ShowSetVariableEditor
                       || ShowWriteLogEditor
                       || ShowDelayEditor;
            }
        }

        public bool ShowUrlEditor
        {
            get
            {
                return SelectedWorkflowStep != null
                       && (SelectedWorkflowStep.StepType == StepType.LaunchIe
                           || SelectedWorkflowStep.StepType == StepType.Navigate);
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
                    case StepType.ClickElement:
                    case StepType.InputText:
                    case StepType.ReadText:
                    case StepType.SelectOption:
                    case StepType.UploadFile:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool ShowInputTextEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.InputText; }
        }

        public bool ShowReadTextEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.ReadText; }
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

        public string UrlEditorLabel
        {
            get
            {
                if (SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.LaunchIe)
                {
                    return "启动地址";
                }

                return "导航地址";
            }
        }

        public string SelectorEditorLabel
        {
            get { return "元素选择器（推荐使用 XPath）"; }
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

        public IEnumerable<string> AlertActionOptions
        {
            get { return AlertActionOptionsSource; }
        }

        public IEnumerable<string> MatchModeOptions
        {
            get { return MatchModeOptionsSource; }
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
            get { return "IE RPA 设计器 - " + CurrentWorkflowName; }
        }

        public RelayCommand NewWorkflowCommand { get; private set; }

        public RelayCommand AddStepCommand { get; private set; }

        public RelayCommand RemoveSelectedStepCommand { get; private set; }

        public RelayCommand MoveStepUpCommand { get; private set; }

        public RelayCommand MoveStepDownCommand { get; private set; }

        public AsyncRelayCommand SaveWorkflowCommand { get; private set; }

        public AsyncRelayCommand LoadWorkflowCommand { get; private set; }

        public RelayCommand OpenElementPickerCommand { get; private set; }

        public AsyncRelayCommand RunWorkflowCommand { get; private set; }

        public AsyncRelayCommand RunFromSelectedStepCommand { get; private set; }

        public AsyncRelayCommand RunSelectedStepCommand { get; private set; }

        public RelayCommand StopWorkflowCommand { get; private set; }

        private static IWorkflowRunner BuildWorkflowRunner(ILogService logService)
        {
            var browserService = new IeBrowserService();
            var variableResolver = new VariableResolver();
            var desktopInteractionService = new DesktopInteractionService();
            var executors = new List<IStepExecutor>
            {
                new LaunchIeStepExecutor(browserService, variableResolver),
                new AttachIeStepExecutor(browserService),
                new NavigateStepExecutor(variableResolver),
                new WaitPageReadyStepExecutor(),
                new ClickElementStepExecutor(variableResolver),
                new InputTextStepExecutor(variableResolver),
                new ReadTextStepExecutor(variableResolver),
                new SelectOptionStepExecutor(variableResolver),
                new SwitchFrameStepExecutor(variableResolver),
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
                new LoopStepExecutor(variableResolver)
            };

            return new WorkflowRunner(new StepExecutorFactory(executors), logService, desktopInteractionService);
        }

        private void CreateNewWorkflow()
        {
            _logService.Clear();
            Logs.Clear();
            ExecutionStatus = ExecutionStatus.Idle;
            CurrentWorkflow = new WorkflowDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = "IE 表单提交流程",
                Steps = new ObservableCollection<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = "设置目标系统地址",
                        StepType = StepType.SetVariable,
                        Parameters = new StepParameterBag
                        {
                            { "name", "TargetUrl" },
                            { "value", "http://intranet.example.local" }
                        }
                    },
                    new WorkflowStep
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = "启动 IE 并打开页面",
                        StepType = StepType.LaunchIe,
                        Parameters = new StepParameterBag
                        {
                            { "url", "${TargetUrl}" }
                        }
                    },
                    new WorkflowStep
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = "等待页面稳定",
                        StepType = StepType.WaitPageReady
                    },
                    new WorkflowStep
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = "记录启动日志",
                        StepType = StepType.WriteLog,
                        Parameters = new StepParameterBag
                        {
                            { "message", "IE 自动化主链已经接入，可以继续配置输入、点击和读取步骤。" }
                        }
                    }
                }
            };

            SelectedWorkflowStep = CurrentWorkflow.Steps.FirstOrDefault();
            _logService.Log(LogLevel.Info, "已创建新的流程骨架。");
        }

        private void AddSelectedToolboxStep()
        {
            if (SelectedToolboxStep == null || CurrentWorkflow == null)
            {
                return;
            }

            var step = CreateStepFromToolbox(SelectedToolboxStep);
            CurrentWorkflow.Steps.Add(step);
            SelectedWorkflowStep = step;
            OnPropertyChanged("WorkflowSummary");
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
            OnPropertyChanged("WorkflowSummary");
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
            var path = GetDefaultWorkflowPath();
            await _workflowFileService.SaveAsync(path, CurrentWorkflow);
            _logService.Log(LogLevel.Info, "流程已保存到 " + path + "。");
        }

        private async Task LoadWorkflowAsync()
        {
            var path = GetDefaultWorkflowPath();
            if (!File.Exists(path))
            {
                _logService.Log(LogLevel.Warning, "默认流程文件不存在，已保留当前设计。");
                return;
            }

            CurrentWorkflow = await _workflowFileService.LoadAsync(path);
            SelectedWorkflowStep = CurrentWorkflow.Steps.FirstOrDefault();
            OnPropertyChanged("WorkflowSummary");
            _logService.Log(LogLevel.Info, "已从 " + path + " 加载流程。");
        }

        private void OpenElementPicker()
        {
            if (!CanPickElementForSelectedStep())
            {
                MessageBox.Show("当前步骤不需要页面元素选择器。", "元素拾取器", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var window = new ElementPickerWindow(ApplySelectorToSelectedStep);
            if (Application.Current != null && Application.Current.MainWindow != null)
            {
                window.Owner = Application.Current.MainWindow;
            }

            window.ShowDialog();
        }

        private void ApplySelectorToSelectedStep(string selector)
        {
            if (SelectedWorkflowStep == null || string.IsNullOrWhiteSpace(selector))
            {
                return;
            }

            var parameterName = ResolveSelectorParameterName(SelectedWorkflowStep.StepType);
            SelectedWorkflowStep.Parameters[parameterName] = selector;
            OnPropertyChanged("SelectedStepParametersText");
            OnPropertyChanged("SelectedStepParametersEditor");
            NotifySelectedStepEditorStateChanged();
            _logService.Log(LogLevel.Info, "已把元素选择器写入当前步骤：" + parameterName + " = " + selector, SelectedWorkflowStep);
        }

        private static string ResolveSelectorParameterName(StepType? stepType)
        {
            switch (stepType)
            {
                case StepType.ClickElement:
                case StepType.InputText:
                case StepType.ReadText:
                case StepType.SelectOption:
                case StepType.UploadFile:
                    return "selector";
                default:
                    return "selector";
            }
        }

        private bool CanPickElementForSelectedStep()
        {
            return ShowSelectorEditor;
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
            await RunWorkflowInternalAsync(0, null, "流程执行完成。");
        }

        private async Task RunFromSelectedStepAsync()
        {
            if (SelectedWorkflowStep == null || CurrentWorkflow == null)
            {
                return;
            }

            var startIndex = CurrentWorkflow.Steps.IndexOf(SelectedWorkflowStep);
            await RunWorkflowInternalAsync(startIndex, null, "已从选中步骤开始执行到流程结束。");
        }

        private async Task RunSelectedStepAsync()
        {
            if (SelectedWorkflowStep == null || CurrentWorkflow == null)
            {
                return;
            }

            var startIndex = CurrentWorkflow.Steps.IndexOf(SelectedWorkflowStep);
            await RunWorkflowInternalAsync(startIndex, 1, "单步执行完成。");
        }

        private async Task RunWorkflowInternalAsync(int startStepIndex, int? maxSteps, string completionMessage)
        {
            _logService.Clear();
            Logs.Clear();
            _cancellationTokenSource = new CancellationTokenSource();
            ExecutionStatus = ExecutionStatus.Running;

            try
            {
                // 这里把“全量运行 / 从选中步骤运行 / 单步执行”统一走同一条执行链，
                // 这样日志、失败截图、变量上下文和重试策略都能保持一致。
                var context = new WpfApplication1.Workflow.ExecutionContext(_cancellationTokenSource.Token);
                await _workflowRunner.RunAsync(CurrentWorkflow, context, startStepIndex, maxSteps);
                ExecutionStatus = ExecutionStatus.Completed;
                _logService.Log(LogLevel.Info, completionMessage);
            }
            catch (OperationCanceledException)
            {
                ExecutionStatus = ExecutionStatus.Cancelled;
                _logService.Log(LogLevel.Warning, "流程已停止。");
            }
            catch (Exception ex)
            {
                ExecutionStatus = ExecutionStatus.Failed;
                _logService.Log(LogLevel.Error, "流程执行失败：" + ex.Message);
            }
            finally
            {
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }

        private void StopWorkflow()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
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

        private string GetDefaultWorkflowPath()
        {
            Directory.CreateDirectory(_defaultWorkflowDirectory);
            return Path.Combine(_defaultWorkflowDirectory, "default.ierpa.json");
        }

        private static IEnumerable<ToolboxStepDefinition> BuildToolboxSteps()
        {
            return new[]
            {
                new ToolboxStepDefinition { StepType = StepType.LaunchIe, Name = "启动 IE", Description = "启动一个新的 IE11 浏览器实例。" },
                new ToolboxStepDefinition { StepType = StepType.AttachIe, Name = "附加 IE", Description = "连接到当前已打开的 IE11 窗口。" },
                new ToolboxStepDefinition { StepType = StepType.Navigate, Name = "打开网页", Description = "导航到指定地址并等待页面跳转。" },
                new ToolboxStepDefinition { StepType = StepType.WaitPageReady, Name = "等待页面完成", Description = "等待页面或脚本加载稳定。" },
                new ToolboxStepDefinition { StepType = StepType.ClickElement, Name = "点击元素", Description = "点击按钮、链接或其他可交互元素。" },
                new ToolboxStepDefinition { StepType = StepType.InputText, Name = "输入文本", Description = "向输入框写入文字内容。" },
                new ToolboxStepDefinition { StepType = StepType.ReadText, Name = "读取文本", Description = "读取页面文本并保存到变量。" },
                new ToolboxStepDefinition { StepType = StepType.SelectOption, Name = "选择下拉项", Description = "选择 select 控件中的目标选项。" },
                new ToolboxStepDefinition { StepType = StepType.SwitchFrame, Name = "切换 Frame", Description = "进入或退出页面中的 frame/iframe。" },
                new ToolboxStepDefinition { StepType = StepType.SwitchWindow, Name = "切换窗口", Description = "切换到新打开的业务窗口。" },
                new ToolboxStepDefinition { StepType = StepType.ExecuteScript, Name = "执行脚本", Description = "运行一段页面 JavaScript。" },
                new ToolboxStepDefinition { StepType = StepType.HandleAlert, Name = "处理弹窗", Description = "接受或关闭 alert/confirm 对话框。" },
                new ToolboxStepDefinition { StepType = StepType.UploadFile, Name = "上传文件", Description = "处理文件选择框并上传本地文件。" },
                new ToolboxStepDefinition { StepType = StepType.WaitDownload, Name = "等待下载", Description = "检查文件下载完成状态。" },
                new ToolboxStepDefinition { StepType = StepType.SetVariable, Name = "设置变量", Description = "给流程变量赋值，供后续步骤引用。" },
                new ToolboxStepDefinition { StepType = StepType.Condition, Name = "条件判断", Description = "根据变量或结果决定后续路径。" },
                new ToolboxStepDefinition { StepType = StepType.Loop, Name = "循环", Description = "按集合或次数重复执行步骤。" },
                new ToolboxStepDefinition { StepType = StepType.Delay, Name = "延时等待", Description = "在步骤之间插入固定等待时间。" },
                new ToolboxStepDefinition { StepType = StepType.Screenshot, Name = "截图", Description = "保存当前页面或桌面的截图。" },
                new ToolboxStepDefinition { StepType = StepType.WriteLog, Name = "写日志", Description = "向执行日志写入业务提示信息。" }
            };
        }

        private static WorkflowStep CreateStepFromToolbox(ToolboxStepDefinition toolboxStep)
        {
            var step = new WorkflowStep
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = toolboxStep.Name,
                StepType = toolboxStep.StepType
            };

            switch (toolboxStep.StepType)
            {
                case StepType.LaunchIe:
                    step.Parameters.Add("url", "${TargetUrl}");
                    break;
                case StepType.Navigate:
                    step.Parameters.Add("url", "http://intranet.example.local");
                    break;
                case StepType.InputText:
                    step.Parameters.Add("selector", "xpath=/html[1]/body[1]/input[1]");
                    step.Parameters.Add("text", "${UserName}");
                    break;
                case StepType.ClickElement:
                    step.Parameters.Add("selector", "xpath=/html[1]/body[1]/button[1]");
                    break;
                case StepType.ReadText:
                    step.Parameters.Add("selector", "xpath=/html[1]/body[1]/div[1]");
                    step.Parameters.Add("variableName", "ResultText");
                    break;
                case StepType.SelectOption:
                    step.Parameters.Add("selector", "xpath=/html[1]/body[1]/select[1]");
                    step.Parameters.Add("option", "Shanghai");
                    step.Parameters.Add("matchMode", "text");
                    break;
                case StepType.SwitchFrame:
                    step.Parameters.Add("framePath", "0");
                    break;
                case StepType.ExecuteScript:
                    step.Parameters.Add("script", "window.__ieRpaResult = document.title;");
                    step.Parameters.Add("resultExpression", "window.__ieRpaResult");
                    step.Parameters.Add("resultVariableName", "ScriptResult");
                    break;
                case StepType.SwitchWindow:
                    step.Parameters.Add("mode", "last");
                    step.Parameters.Add("waitForNewWindow", "true");
                    step.Parameters.Add("excludeCurrent", "true");
                    break;
                case StepType.UploadFile:
                    step.Parameters.Add("selector", "xpath=/html[1]/body[1]/input[1]");
                    step.Parameters.Add("clickSelector", "xpath=/html[1]/body[1]/button[1]");
                    step.Parameters.Add("filePath", @"C:\Temp\example.txt");
                    step.Parameters.Add("dialogDelayMs", "800");
                    break;
                case StepType.HandleAlert:
                    step.Parameters.Add("action", "accept");
                    step.Parameters.Add("buttonText", "确定");
                    step.Parameters.Add("titleContains", "");
                    break;
                case StepType.WaitDownload:
                    step.Parameters.Add("downloadDirectory", @"C:\Users\Public\Downloads");
                    step.Parameters.Add("fileName", "");
                    step.Parameters.Add("filePattern", "");
                    step.Parameters.Add("stableMs", "1200");
                    step.Parameters.Add("outputVariableName", "DownloadedFilePath");
                    break;
                case StepType.Screenshot:
                    step.Parameters.Add("directory", @"C:\Temp\IeRpaScreenshots");
                    step.Parameters.Add("fileNamePrefix", "ie_rpa");
                    step.Parameters.Add("outputVariableName", "LastScreenshotPath");
                    break;
                case StepType.Condition:
                    step.Parameters.Add("left", "${ResultText}");
                    step.Parameters.Add("operator", "contains");
                    step.Parameters.Add("right", "success");
                    step.Parameters.Add("resultVariableName", "ConditionResult");
                    step.Parameters.Add("whenTrueStepIndex", "");
                    step.Parameters.Add("whenFalseStepIndex", "");
                    break;
                case StepType.Loop:
                    step.Parameters.Add("loopKey", "mainLoop");
                    step.Parameters.Add("repeatFromStepIndex", "0");
                    step.Parameters.Add("times", "3");
                    step.Parameters.Add("currentIterationVariable", "LoopIteration");
                    break;
                case StepType.SetVariable:
                    step.Parameters.Add("name", "VarName");
                    step.Parameters.Add("value", "Value");
                    break;
                case StepType.Delay:
                    step.Parameters.Add("durationMs", "1000");
                    break;
                case StepType.WriteLog:
                    step.Parameters.Add("message", "业务日志内容");
                    break;
            }

            return step;
        }

        private string GetStepParameter(string key)
        {
            if (SelectedWorkflowStep == null || string.IsNullOrWhiteSpace(key))
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

            SelectedWorkflowStep.Parameters[key] = value ?? string.Empty;
            OnPropertyChanged("SelectedStepParametersText");
            OnPropertyChanged("SelectedStepParametersEditor");
            NotifySelectedStepEditorStateChanged();
        }

        private StepType? GetSelectedStepType()
        {
            return SelectedWorkflowStep != null ? (StepType?)SelectedWorkflowStep.StepType : null;
        }

        private void NotifySelectedStepEditorStateChanged()
        {
            OnPropertyChanged("HasStructuredStepEditor");
            OnPropertyChanged("ShowUrlEditor");
            OnPropertyChanged("ShowSelectorEditor");
            OnPropertyChanged("ShowInputTextEditor");
            OnPropertyChanged("ShowReadTextEditor");
            OnPropertyChanged("ShowSelectOptionEditor");
            OnPropertyChanged("ShowHandleAlertEditor");
            OnPropertyChanged("ShowSetVariableEditor");
            OnPropertyChanged("ShowWriteLogEditor");
            OnPropertyChanged("ShowDelayEditor");
            OnPropertyChanged("UrlEditorLabel");
            OnPropertyChanged("SelectorEditorLabel");
            OnPropertyChanged("UrlParameterValue");
            OnPropertyChanged("SelectorParameterValue");
            OnPropertyChanged("InputTextParameterValue");
            OnPropertyChanged("ReadVariableNameParameterValue");
            OnPropertyChanged("OptionParameterValue");
            OnPropertyChanged("MatchModeParameterValue");
            OnPropertyChanged("AlertActionParameterValue");
            OnPropertyChanged("AlertButtonTextParameterValue");
            OnPropertyChanged("AlertTitleContainsParameterValue");
            OnPropertyChanged("SetVariableNameParameterValue");
            OnPropertyChanged("SetVariableValueParameterValue");
            OnPropertyChanged("WriteLogMessageParameterValue");
            OnPropertyChanged("DelayDurationParameterValue");
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

