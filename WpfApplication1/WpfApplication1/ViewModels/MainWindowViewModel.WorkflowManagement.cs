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
        private readonly IWorkflowCatalogService _workflowCatalogService = new WorkflowCatalogService();
        private readonly IPublishedWorkflowStore _publishedWorkflowStore = new XmlPublishedWorkflowStore();
        private readonly IWorkflowTemplateFactory _workflowTemplateFactory = new WorkflowTemplateFactory();
        private ObservableCollection<WorkflowListItem> _workflowCatalogItems;
        private ObservableCollection<PublishedWorkflowRecord> _publishedWorkflows;
        private ObservableCollection<PublishedWorkflowRecord> _applyWorkflowOptions;
        private ObservableCollection<PublishedWorkflowRecord> _queryWorkflowOptions;
        private ObservableCollection<PublishedWorkflowRecord> _approvalWorkflowOptions;
        private WorkflowListItem _selectedWorkflowListItem;
        private ICollectionView _workflowCatalogView;
        private string _workflowFilterText;
        private WorkflowType? _selectedWorkflowFilterType;
        private string _currentWorkflowPath;
        private ObservableCollection<DesignerCanvasNodeViewModel> _designerNodes;
        private ObservableCollection<DesignerCanvasConnectionViewModel> _designerConnections;
        private double _designerZoom = 1.0;

        public ObservableCollection<WorkflowListItem> WorkflowCatalogItems
        {
            get { return _workflowCatalogItems; }
            private set { SetProperty(ref _workflowCatalogItems, value); }
        }

        public ICollectionView WorkflowCatalogView
        {
            get { return _workflowCatalogView; }
            private set { SetProperty(ref _workflowCatalogView, value); }
        }

        public WorkflowListItem SelectedWorkflowListItem
        {
            get { return _selectedWorkflowListItem; }
            set
            {
                if (SetProperty(ref _selectedWorkflowListItem, value))
                {
                    if (OpenSelectedWorkflowCommand != null) OpenSelectedWorkflowCommand.RaiseCanExecuteChanged();
                    if (DuplicateWorkflowCommand != null) DuplicateWorkflowCommand.RaiseCanExecuteChanged();
                    if (DeleteWorkflowCommand != null) DeleteWorkflowCommand.RaiseCanExecuteChanged();
                    if (ExportCatalogWorkflowCommand != null) ExportCatalogWorkflowCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string WorkflowFilterText
        {
            get { return _workflowFilterText; }
            set
            {
                if (SetProperty(ref _workflowFilterText, value) && WorkflowCatalogView != null)
                {
                    WorkflowCatalogView.Refresh();
                }
            }
        }

        public WorkflowType? SelectedWorkflowFilterType
        {
            get { return _selectedWorkflowFilterType; }
            set
            {
                if (SetProperty(ref _selectedWorkflowFilterType, value))
                {
                    if (WorkflowCatalogView != null)
                    {
                        WorkflowCatalogView.Refresh();
                    }

                    ScheduleStateSave();
                }
            }
        }

        public string CurrentWorkflowPath
        {
            get { return _currentWorkflowPath; }
            private set
            {
                if (SetProperty(ref _currentWorkflowPath, value))
                {
                    OnPropertyChanged("CurrentWorkflowPathText");
                    OnPropertyChanged("CurrentWorkflowMetaText");
                    OnPropertyChanged("HasCurrentWorkflowPath");
                    OnPropertyChanged("WindowTitle");
                    ScheduleStateSave();
                }
            }
        }

        public string CurrentWorkflowPathText
        {
            get { return string.IsNullOrWhiteSpace(CurrentWorkflowPath) ? "当前为未保存草稿" : CurrentWorkflowPath; }
        }

        public bool HasCurrentWorkflowPath
        {
            get { return !string.IsNullOrWhiteSpace(CurrentWorkflowPath); }
        }

        public ObservableCollection<PublishedWorkflowRecord> PublishedWorkflows
        {
            get { return _publishedWorkflows; }
            private set { SetProperty(ref _publishedWorkflows, value); }
        }

        public ObservableCollection<PublishedWorkflowRecord> ApplyWorkflowOptions
        {
            get { return _applyWorkflowOptions; }
            private set { SetProperty(ref _applyWorkflowOptions, value); }
        }

        public ObservableCollection<PublishedWorkflowRecord> QueryWorkflowOptions
        {
            get { return _queryWorkflowOptions; }
            private set { SetProperty(ref _queryWorkflowOptions, value); }
        }

        public ObservableCollection<PublishedWorkflowRecord> ApprovalWorkflowOptions
        {
            get { return _approvalWorkflowOptions; }
            private set { SetProperty(ref _approvalWorkflowOptions, value); }
        }

        public ObservableCollection<DesignerCanvasNodeViewModel> DesignerNodes
        {
            get { return _designerNodes; }
            private set { SetProperty(ref _designerNodes, value); }
        }

        public ObservableCollection<DesignerCanvasConnectionViewModel> DesignerConnections
        {
            get { return _designerConnections; }
            private set { SetProperty(ref _designerConnections, value); }
        }

        public double DesignerZoom
        {
            get { return _designerZoom; }
            set
            {
                var normalized = Math.Max(0.5, Math.Min(1.8, value));
                if (SetProperty(ref _designerZoom, normalized))
                {
                    if (CurrentWorkflow != null)
                    {
                        CurrentWorkflow.EnsureCanvasLayout();
                        CurrentWorkflow.CanvasLayout.Zoom = normalized;
                    }

                    OnPropertyChanged("DesignerZoomPercentText");
                    ScheduleStateSave();
                }
            }
        }

        public string DesignerZoomPercentText
        {
            get { return string.Format("{0:0}%", DesignerZoom * 100); }
        }

        public string CurrentWorkflowMetaText
        {
            get
            {
                if (CurrentWorkflow == null)
                {
                    return "未加载流程";
                }

                return string.Format("{0} | 角色：{1} | 版本：v{2}",
                    GetWorkflowTypeDisplay(CurrentWorkflow.WorkflowType),
                    string.IsNullOrWhiteSpace(CurrentWorkflow.ApplicableRole) ? "未设置" : CurrentWorkflow.ApplicableRole,
                    string.IsNullOrWhiteSpace(CurrentWorkflow.Version) ? "0.1.0" : CurrentWorkflow.Version);
            }
        }

        public string SelectedStepDescriptionText
        {
            get
            {
                var step = SelectedWorkflowStep;
                if (step == null)
                {
                    return "选择画布节点后，这里会显示节点用途、适用场景和配置提示。";
                }

                var toolbox = AvailableSteps != null ? AvailableSteps.FirstOrDefault(item => item.StepType == step.StepType) : null;
                return toolbox != null ? toolbox.Description : step.StepType.ToString();
            }
        }

        public string DesignerDebugHintText
        {
            get
            {
                if (SelectedWorkflowStep == null)
                {
                    return "调试页支持元素拾取、变量预览和单步测试入口。请先从画布选择一个节点。";
                }

                return string.Format("当前节点：{0}\n步骤类型：{1}\n当前对象：{2}\n最近错误：{3}",
                    SelectedWorkflowStep.Name,
                    SelectedWorkflowStep.StepType,
                    CurrentObjectDisplayText,
                    RecentErrorDisplayText);
            }
        }

        public string SchedulerSelectionStatusText
        {
            get
            {
                if (!HasValidSchedulerSelection)
                {
                    return "当前发布流程选择不完整或已失效，调度启动已被禁用。";
                }

                return "调度中心当前仅读取已发布流程快照。";
            }
        }

        public bool HasValidSchedulerSelection
        {
            get
            {
                return ResolvePublishedWorkflowById(SchedulerSettings != null ? SchedulerSettings.ApplyWorkflowId : null) != null
                    && ResolvePublishedWorkflowById(SchedulerSettings != null ? SchedulerSettings.QueryWorkflowId : null) != null;
            }
        }

        public bool HasCurrentWorkflow
        {
            get { return CurrentWorkflow != null; }
        }

        public bool IsWorkflowManagementPage
        {
            get { return SelectedShellPage == ShellPage.WorkflowManagement; }
        }

        public bool IsScheduleCenterPage
        {
            get { return SelectedShellPage == ShellPage.ScheduleCenter; }
        }

        public bool IsOperationCenterPage
        {
            get { return SelectedShellPage == ShellPage.OperationCenter; }
        }

        public string ApplyWorkflowIdValue
        {
            get { return SchedulerSettings != null ? SchedulerSettings.ApplyWorkflowId : string.Empty; }
            set { UpdateSchedulerSettings(settings => settings.ApplyWorkflowId = value); OnPropertyChanged("HasValidSchedulerSelection"); OnPropertyChanged("SchedulerSelectionStatusText"); }
        }

        public string QueryWorkflowIdValue
        {
            get { return SchedulerSettings != null ? SchedulerSettings.QueryWorkflowId : string.Empty; }
            set { UpdateSchedulerSettings(settings => settings.QueryWorkflowId = value); OnPropertyChanged("HasValidSchedulerSelection"); OnPropertyChanged("SchedulerSelectionStatusText"); }
        }

        public string ApprovalWorkflowIdValue
        {
            get { return SchedulerSettings != null ? SchedulerSettings.ApprovalWorkflowId : string.Empty; }
            set { UpdateSchedulerSettings(settings => settings.ApprovalWorkflowId = value); }
        }

        public RelayCommand CreateWorkflowCommand { get; private set; }
        public AsyncRelayCommand RefreshWorkflowCatalogCommand { get; private set; }
        public AsyncRelayCommand OpenSelectedWorkflowCommand { get; private set; }
        public AsyncRelayCommand DuplicateWorkflowCommand { get; private set; }
        public AsyncRelayCommand DeleteWorkflowCommand { get; private set; }
        public AsyncRelayCommand ImportCatalogWorkflowCommand { get; private set; }
        public AsyncRelayCommand ExportCatalogWorkflowCommand { get; private set; }
        public AsyncRelayCommand PublishCurrentWorkflowCommand { get; private set; }
        public AsyncRelayCommand SaveDraftWorkflowCommand { get; private set; }
        public RelayCommand CanvasZoomInCommand { get; private set; }
        public RelayCommand CanvasZoomOutCommand { get; private set; }
        public RelayCommand AutoArrangeCanvasCommand { get; private set; }
        public RelayCommand SelectCanvasNodeCommand { get; private set; }
        private void InitializeWorkflowManagementSurface(ApplicationState initialState)
        {
            DesignerNodes = new ObservableCollection<DesignerCanvasNodeViewModel>();
            DesignerConnections = new ObservableCollection<DesignerCanvasConnectionViewModel>();
            WorkflowCatalogItems = new ObservableCollection<WorkflowListItem>();
            PublishedWorkflows = new ObservableCollection<PublishedWorkflowRecord>();
            ApplyWorkflowOptions = new ObservableCollection<PublishedWorkflowRecord>();
            QueryWorkflowOptions = new ObservableCollection<PublishedWorkflowRecord>();
            ApprovalWorkflowOptions = new ObservableCollection<PublishedWorkflowRecord>();

            WorkflowCatalogView = CollectionViewSource.GetDefaultView(WorkflowCatalogItems);
            if (WorkflowCatalogView != null)
            {
                WorkflowCatalogView.Filter = FilterWorkflowCatalogItem;
            }

            SelectedWorkflowFilterType = initialState != null ? (WorkflowType?)initialState.WorkflowManagementFilterType : null;
            DesignerZoom = initialState != null && initialState.DesignerZoom > 0 ? initialState.DesignerZoom : 1.0;
            CurrentWorkflowPath = initialState != null ? initialState.ActiveWorkflowPath : string.Empty;

            CreateWorkflowCommand = new RelayCommand(CreateWorkflowFromDialog);
            RefreshWorkflowCatalogCommand = new AsyncRelayCommand(RefreshWorkflowCatalogAsync);
            OpenSelectedWorkflowCommand = new AsyncRelayCommand(OpenSelectedWorkflowAsync, () => SelectedWorkflowListItem != null);
            DuplicateWorkflowCommand = new AsyncRelayCommand(DuplicateSelectedWorkflowAsync, () => SelectedWorkflowListItem != null);
            DeleteWorkflowCommand = new AsyncRelayCommand(DeleteSelectedWorkflowAsync, () => SelectedWorkflowListItem != null);
            ImportCatalogWorkflowCommand = new AsyncRelayCommand(ImportWorkflowIntoCatalogAsync);
            ExportCatalogWorkflowCommand = new AsyncRelayCommand(ExportSelectedWorkflowAsync, () => SelectedWorkflowListItem != null);
            PublishCurrentWorkflowCommand = new AsyncRelayCommand(PublishCurrentWorkflowAsync, () => CurrentWorkflow != null);
            SaveDraftWorkflowCommand = new AsyncRelayCommand(SaveCurrentWorkflowAsync, () => CurrentWorkflow != null);
            CanvasZoomInCommand = new RelayCommand(() => DesignerZoom += 0.1, () => CurrentWorkflow != null);
            CanvasZoomOutCommand = new RelayCommand(() => DesignerZoom -= 0.1, () => CurrentWorkflow != null);
            AutoArrangeCanvasCommand = new RelayCommand(AutoArrangeCanvas, () => CurrentWorkflow != null && CurrentWorkflow.Steps.Count > 0);
            SelectCanvasNodeCommand = new RelayCommand(parameter => SelectCanvasNode(Convert.ToString(parameter)), parameter => parameter != null);

            EnsureCatalogSeedAsync().GetAwaiter().GetResult();
            RefreshWorkflowCatalogAsync().GetAwaiter().GetResult();
            RefreshDesignerCanvas();
        }

        private bool FilterWorkflowCatalogItem(object item)
        {
            var workflow = item as WorkflowListItem;
            if (workflow == null)
            {
                return false;
            }

            if (SelectedWorkflowFilterType.HasValue && workflow.WorkflowType != SelectedWorkflowFilterType.Value)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(WorkflowFilterText))
            {
                return true;
            }

            var search = WorkflowFilterText.Trim();
            return (workflow.Name ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                   || (workflow.Description ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                   || (workflow.ApplicableRole ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task EnsureCatalogSeedAsync()
        {
            var catalogItems = await _workflowCatalogService.GetWorkflowsAsync();
            if (catalogItems.Count > 0)
            {
                return;
            }

            var examplesDirectory = Path.Combine(GetWorkflowFilesDirectory(), "Examples");
            if (!Directory.Exists(examplesDirectory))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(examplesDirectory, "*.ierpa.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    await _workflowCatalogService.ImportWorkflowAsync(file);
                }
                catch
                {
                }
            }
        }

        private async Task RefreshWorkflowCatalogAsync()
        {
            var catalogItems = await _workflowCatalogService.GetWorkflowsAsync();
            var publishedItems = await _publishedWorkflowStore.LoadAllAsync();

            foreach (var item in catalogItems)
            {
                var published = publishedItems.FirstOrDefault(entry => string.Equals(entry.WorkflowId, item.WorkflowId, StringComparison.OrdinalIgnoreCase));
                if (published != null)
                {
                    item.IsPublished = true;
                    item.PublishedAt = published.PublishedAt;
                    item.PublishedVersion = published.Version;
                }
            }

            WorkflowCatalogItems.Clear();
            foreach (var item in catalogItems.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                WorkflowCatalogItems.Add(item);
            }

            PublishedWorkflows.Clear();
            foreach (var published in publishedItems.OrderByDescending(item => item.PublishedAt))
            {
                PublishedWorkflows.Add(published);
            }

            ResetPublishedOptions();
            TrySetDefaultSchedulerSelections();

            if (WorkflowCatalogView != null)
            {
                WorkflowCatalogView.Refresh();
            }

            if (!string.IsNullOrWhiteSpace(CurrentWorkflowPath))
            {
                SelectedWorkflowListItem = WorkflowCatalogItems.FirstOrDefault(item => string.Equals(item.SourcePath, CurrentWorkflowPath, StringComparison.OrdinalIgnoreCase));
            }
            else if (CurrentWorkflow != null && !string.IsNullOrWhiteSpace(CurrentWorkflow.Id))
            {
                SelectedWorkflowListItem = WorkflowCatalogItems.FirstOrDefault(item => string.Equals(item.WorkflowId, CurrentWorkflow.Id, StringComparison.OrdinalIgnoreCase));
            }

            OnPropertyChanged("HasValidSchedulerSelection");
            OnPropertyChanged("SchedulerSelectionStatusText");
            if (StartSchedulerCommand != null) StartSchedulerCommand.RaiseCanExecuteChanged();
            if (RunSchedulerRoundCommand != null) RunSchedulerRoundCommand.RaiseCanExecuteChanged();
        }

        private void ResetPublishedOptions()
        {
            ApplyWorkflowOptions.Clear();
            QueryWorkflowOptions.Clear();
            ApprovalWorkflowOptions.Clear();

            foreach (var item in PublishedWorkflows.Where(item => item.WorkflowType == WorkflowType.Apply || item.WorkflowType == WorkflowType.General))
            {
                ApplyWorkflowOptions.Add(item);
            }

            foreach (var item in PublishedWorkflows.Where(item => item.WorkflowType == WorkflowType.Query || item.WorkflowType == WorkflowType.General))
            {
                QueryWorkflowOptions.Add(item);
            }

            foreach (var item in PublishedWorkflows.Where(item => item.WorkflowType == WorkflowType.Approval || item.WorkflowType == WorkflowType.General))
            {
                ApprovalWorkflowOptions.Add(item);
            }
        }

        private void TrySetDefaultSchedulerSelections()
        {
            if (SchedulerSettings == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SchedulerSettings.ApplyWorkflowId) && ApplyWorkflowOptions.Count > 0)
            {
                SchedulerSettings.ApplyWorkflowId = ApplyWorkflowOptions[0].WorkflowId;
            }

            if (string.IsNullOrWhiteSpace(SchedulerSettings.QueryWorkflowId) && QueryWorkflowOptions.Count > 0)
            {
                SchedulerSettings.QueryWorkflowId = QueryWorkflowOptions[0].WorkflowId;
            }

            if (string.IsNullOrWhiteSpace(SchedulerSettings.ApprovalWorkflowId) && ApprovalWorkflowOptions.Count > 0)
            {
                SchedulerSettings.ApprovalWorkflowId = ApprovalWorkflowOptions[0].WorkflowId;
            }

            OnPropertyChanged("ApplyWorkflowIdValue");
            OnPropertyChanged("QueryWorkflowIdValue");
            OnPropertyChanged("ApprovalWorkflowIdValue");
            OnPropertyChanged("HasValidSchedulerSelection");
            OnPropertyChanged("SchedulerSelectionStatusText");
            if (StartSchedulerCommand != null) StartSchedulerCommand.RaiseCanExecuteChanged();
            if (RunSchedulerRoundCommand != null) RunSchedulerRoundCommand.RaiseCanExecuteChanged();
        }

        private void CreateWorkflowFromDialog()
        {
            var dialog = new WorkflowCreateDialog
            {
                Owner = Application.Current != null ? Application.Current.MainWindow : null
            };
            if (dialog.ShowDialog() != true || dialog.Request == null)
            {
                return;
            }

            var workflow = _workflowTemplateFactory.CreateWorkflow(dialog.Request);
            var path = _workflowCatalogService.CreateWorkflowAsync(dialog.Request, workflow).GetAwaiter().GetResult();
            LoadWorkflowIntoDesignerAsync(path, true).GetAwaiter().GetResult();
            RefreshWorkflowCatalogAsync().GetAwaiter().GetResult();
            _logService.Log(LogLevel.Info, "已创建新流程并进入设计器。");
        }

        private async Task OpenSelectedWorkflowAsync()
        {
            if (SelectedWorkflowListItem == null)
            {
                return;
            }

            await LoadWorkflowIntoDesignerAsync(SelectedWorkflowListItem.SourcePath, true);
        }

        private async Task DuplicateSelectedWorkflowAsync()
        {
            if (SelectedWorkflowListItem == null)
            {
                return;
            }

            var duplicatePath = await _workflowCatalogService.DuplicateWorkflowAsync(SelectedWorkflowListItem.SourcePath);
            await RefreshWorkflowCatalogAsync();
            await LoadWorkflowIntoDesignerAsync(duplicatePath, true);
            _logService.Log(LogLevel.Info, "已复制流程草稿。");
        }

        private async Task DeleteSelectedWorkflowAsync()
        {
            if (SelectedWorkflowListItem == null)
            {
                return;
            }

            var result = MessageBox.Show("确认删除所选流程吗？该操作会删除本机草稿文件。", "流程管理", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            var workflowId = SelectedWorkflowListItem.WorkflowId;
            var sourcePath = SelectedWorkflowListItem.SourcePath;
            await _workflowCatalogService.DeleteWorkflowAsync(sourcePath);
            await _publishedWorkflowStore.RemoveAsync(workflowId);
            if (string.Equals(CurrentWorkflowPath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                var fallback = BuildDefaultWorkflow();
                ReplaceWorkflow(fallback);
                CurrentWorkflowPath = string.Empty;
                SelectedWorkflowStep = CurrentWorkflow.Steps.FirstOrDefault();
            }

            await RefreshWorkflowCatalogAsync();
        }
        private async Task ImportWorkflowIntoCatalogAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "导入到流程目录",
                Filter = "IE RPA 流程文件 (*.ierpa.json)|*.ierpa.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                InitialDirectory = GetWorkflowFilesDirectory(),
                CheckFileExists = true
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var importedPath = await _workflowCatalogService.ImportWorkflowAsync(dialog.FileName);
            await RefreshWorkflowCatalogAsync();
            await LoadWorkflowIntoDesignerAsync(importedPath, true);
            _logService.Log(LogLevel.Info, "流程已导入到本地目录。");
        }

        private async Task ExportSelectedWorkflowAsync()
        {
            if (SelectedWorkflowListItem == null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "导出流程副本",
                Filter = "IE RPA 流程文件 (*.ierpa.json)|*.ierpa.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                FileName = SanitizeWorkflowFileName(SelectedWorkflowListItem.Name) + ".ierpa.json"
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            await _workflowCatalogService.ExportWorkflowAsync(SelectedWorkflowListItem.SourcePath, dialog.FileName);
            _logService.Log(LogLevel.Info, "流程已导出到 " + dialog.FileName + "。");
        }

        private async Task SaveCurrentWorkflowAsync()
        {
            if (CurrentWorkflow == null)
            {
                return;
            }

            var savePath = CurrentWorkflowPath;
            if (string.IsNullOrWhiteSpace(savePath))
            {
                savePath = await _workflowCatalogService.CreateWorkflowAsync(new WorkflowCreateRequest
                {
                    WorkflowType = CurrentWorkflow.WorkflowType,
                    Name = CurrentWorkflow.Name,
                    Description = CurrentWorkflow.Description,
                    ApplicableRole = CurrentWorkflow.ApplicableRole
                }, CurrentWorkflow);
            }
            else
            {
                savePath = await _workflowCatalogService.SaveWorkflowAsync(savePath, CurrentWorkflow);
            }

            CurrentWorkflowPath = savePath;
            await RefreshWorkflowCatalogAsync();
            _logService.Log(LogLevel.Info, "流程草稿已保存。");
        }

        private async Task PublishCurrentWorkflowAsync()
        {
            if (CurrentWorkflow == null)
            {
                return;
            }

            await SaveCurrentWorkflowAsync();
            var nextVersion = IncrementWorkflowVersion(CurrentWorkflow.Version);
            CurrentWorkflow.Version = nextVersion;
            CurrentWorkflow.IsPublished = true;
            CurrentWorkflow.LastModifiedAt = DateTime.Now;
            var snapshotPath = Path.Combine(_publishedWorkflowStore.PublishedDirectory, CurrentWorkflow.Id + ".ierpa.json");
            await _workflowFileService.SaveAsync(snapshotPath, CurrentWorkflow);
            await _publishedWorkflowStore.SaveAsync(new PublishedWorkflowRecord
            {
                WorkflowId = CurrentWorkflow.Id,
                WorkflowName = CurrentWorkflow.Name,
                WorkflowType = CurrentWorkflow.WorkflowType,
                ApplicableRole = CurrentWorkflow.ApplicableRole,
                SourcePath = CurrentWorkflowPath,
                PublishedSnapshotPath = snapshotPath,
                PublishedAt = DateTime.Now,
                Version = nextVersion
            });
            await _workflowCatalogService.SaveWorkflowAsync(CurrentWorkflowPath, CurrentWorkflow);
            await RefreshWorkflowCatalogAsync();
            _logService.Log(LogLevel.Info, "流程已发布到本机发布清单。");
        }

        private static string IncrementWorkflowVersion(string version)
        {
            Version parsed;
            if (!Version.TryParse(string.IsNullOrWhiteSpace(version) ? "0.1.0" : version, out parsed))
            {
                parsed = new Version(0, 1, 0);
            }

            return new Version(parsed.Major, parsed.Minor, parsed.Build + 1).ToString();
        }

        private async Task LoadWorkflowIntoDesignerAsync(string path, bool switchToDesigner)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            var workflow = EnsureWorkflow(await _workflowFileService.LoadAsync(path));
            workflow.EnsureCanvasLayout();
            ReplaceWorkflow(workflow);
            CurrentWorkflowPath = path;
            SelectedWorkflowStep = CurrentWorkflow != null ? CurrentWorkflow.Steps.FirstOrDefault() : null;
            DesignerZoom = CurrentWorkflow != null && CurrentWorkflow.CanvasLayout != null && CurrentWorkflow.CanvasLayout.Zoom > 0
                ? CurrentWorkflow.CanvasLayout.Zoom
                : 1.0;
            RefreshDesignerCanvas();
            NotifySelectedStepEditorStateChanged();
            if (switchToDesigner)
            {
                SelectedShellPage = ShellPage.Designer;
            }
        }

        public void RefreshDesignerCanvas()
        {
            if (DesignerNodes == null)
            {
                DesignerNodes = new ObservableCollection<DesignerCanvasNodeViewModel>();
            }

            if (DesignerConnections == null)
            {
                DesignerConnections = new ObservableCollection<DesignerCanvasConnectionViewModel>();
            }

            if (CurrentWorkflow == null)
            {
                DesignerNodes.Clear();
                DesignerConnections.Clear();
                return;
            }

            CurrentWorkflow.EnsureCanvasLayout();
            DesignerNodes.Clear();
            foreach (var step in CurrentWorkflow.Steps)
            {
                var layout = CurrentWorkflow.CanvasLayout.Nodes.FirstOrDefault(node => string.Equals(node.StepId, step.Id, StringComparison.OrdinalIgnoreCase));
                if (layout == null)
                {
                    continue;
                }

                DesignerNodes.Add(new DesignerCanvasNodeViewModel(step, layout)
                {
                    IsSelected = ReferenceEquals(step, SelectedWorkflowStep)
                });
            }

            RefreshDesignerConnections();
            OnPropertyChanged("SelectedStepDescriptionText");
            OnPropertyChanged("DesignerDebugHintText");
            OnPropertyChanged("CurrentWorkflowMetaText");
        }

        private void RefreshDesignerConnections()
        {
            DesignerConnections.Clear();
            if (CurrentWorkflow == null || CurrentWorkflow.CanvasLayout == null)
            {
                return;
            }

            foreach (var connection in CurrentWorkflow.CanvasLayout.Connections)
            {
                var fromNode = CurrentWorkflow.CanvasLayout.Nodes.FirstOrDefault(node => string.Equals(node.StepId, connection.FromStepId, StringComparison.OrdinalIgnoreCase));
                var toNode = CurrentWorkflow.CanvasLayout.Nodes.FirstOrDefault(node => string.Equals(node.StepId, connection.ToStepId, StringComparison.OrdinalIgnoreCase));
                if (fromNode == null || toNode == null)
                {
                    continue;
                }

                DesignerConnections.Add(new DesignerCanvasConnectionViewModel
                {
                    FromStepId = connection.FromStepId,
                    ToStepId = connection.ToStepId,
                    X1 = fromNode.X + fromNode.Width,
                    Y1 = fromNode.Y + fromNode.Height / 2,
                    X2 = toNode.X,
                    Y2 = toNode.Y + toNode.Height / 2
                });
            }
        }

        public void MoveCanvasNode(string stepId, double x, double y)
        {
            if (CurrentWorkflow == null || CurrentWorkflow.CanvasLayout == null)
            {
                return;
            }

            var node = CurrentWorkflow.CanvasLayout.Nodes.FirstOrDefault(item => string.Equals(item.StepId, stepId, StringComparison.OrdinalIgnoreCase));
            if (node == null)
            {
                return;
            }

            node.X = Math.Max(24, x);
            node.Y = Math.Max(24, y);
            RefreshDesignerCanvas();
            ScheduleStateSave();
        }

        public void HandleCanvasDrop(ToolboxStepDefinition toolboxStep, Point position)
        {
            if (toolboxStep == null || CurrentWorkflow == null)
            {
                return;
            }

            var step = CreateStepFromToolbox(toolboxStep);
            var insertIndex = SelectedWorkflowStep != null ? CurrentWorkflow.Steps.IndexOf(SelectedWorkflowStep) + 1 : CurrentWorkflow.Steps.Count;
            if (insertIndex < 0 || insertIndex > CurrentWorkflow.Steps.Count)
            {
                insertIndex = CurrentWorkflow.Steps.Count;
            }

            CurrentWorkflow.Steps.Insert(insertIndex, step);
            CurrentWorkflow.EnsureCanvasLayout();
            var node = CurrentWorkflow.CanvasLayout.Nodes.FirstOrDefault(item => string.Equals(item.StepId, step.Id, StringComparison.OrdinalIgnoreCase));
            if (node != null)
            {
                node.X = Math.Max(24, position.X - node.Width / 2);
                node.Y = Math.Max(24, position.Y - node.Height / 2);
            }

            SelectedWorkflowStep = step;
            RefreshDesignerCanvas();
        }

        public void SelectCanvasNode(string stepId)
        {
            if (CurrentWorkflow == null || string.IsNullOrWhiteSpace(stepId))
            {
                return;
            }

            SelectedWorkflowStep = CurrentWorkflow.Steps.FirstOrDefault(step => string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase));
            foreach (var node in DesignerNodes)
            {
                node.IsSelected = string.Equals(node.StepId, stepId, StringComparison.OrdinalIgnoreCase);
            }

            OnPropertyChanged("SelectedStepDescriptionText");
            OnPropertyChanged("DesignerDebugHintText");
        }

        private void AutoArrangeCanvas()
        {
            if (CurrentWorkflow == null)
            {
                return;
            }

            CurrentWorkflow.EnsureCanvasLayout();
            for (var index = 0; index < CurrentWorkflow.Steps.Count; index++)
            {
                var step = CurrentWorkflow.Steps[index];
                var node = CurrentWorkflow.CanvasLayout.Nodes.FirstOrDefault(item => string.Equals(item.StepId, step.Id, StringComparison.OrdinalIgnoreCase));
                if (node == null)
                {
                    continue;
                }

                node.X = 120 + (index % 4) * 260;
                node.Y = 120 + (index / 4) * 160;
            }

            RefreshDesignerCanvas();
        }

        private PublishedWorkflowRecord ResolvePublishedWorkflowById(string workflowId)
        {
            return PublishedWorkflows != null
                ? PublishedWorkflows.FirstOrDefault(item => string.Equals(item.WorkflowId, workflowId, StringComparison.OrdinalIgnoreCase)
                                                            && !string.IsNullOrWhiteSpace(item.PublishedSnapshotPath)
                                                            && File.Exists(item.PublishedSnapshotPath))
                : null;
        }

        private static string GetWorkflowTypeDisplay(WorkflowType workflowType)
        {
            switch (workflowType)
            {
                case WorkflowType.Apply:
                    return "申请流程";
                case WorkflowType.Approval:
                    return "审批流程";
                case WorkflowType.Query:
                    return "查询流程";
                case WorkflowType.IntegratedScheduler:
                    return "调度编排模板";
                case WorkflowType.Subflow:
                    return "子流程";
                default:
                    return "通用流程";
            }
        }
    }
}



