using System.Collections.ObjectModel;
using System.Linq;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;

namespace WpfApplication1.ViewModels
{
    public partial class MainWindowViewModel
    {
        private static readonly IStepParameterDefinitionProvider StepParameterDefinitionProvider = new StepParameterDefinitionProvider();
        private ObservableCollection<StepParameterGroupViewModel> _currentStepParameterGroups;

        public ObservableCollection<StepParameterGroupViewModel> CurrentStepParameterGroups
        {
            get { return _currentStepParameterGroups ?? (_currentStepParameterGroups = new ObservableCollection<StepParameterGroupViewModel>()); }
        }

        public bool HasDynamicStepParameters
        {
            get { return CurrentStepParameterGroups.Count > 0; }
        }

        public string EmptyDynamicParametersText
        {
            get
            {
                if (!HasSelectedWorkflowStep)
                {
                    return "请先在流程中选择一个步骤。";
                }

                return "当前节点没有额外参数。";
            }
        }

        private void RefreshCurrentStepParameterGroups()
        {
            CurrentStepParameterGroups.Clear();
            if (SelectedWorkflowStep == null)
            {
                OnPropertyChanged("HasDynamicStepParameters");
                OnPropertyChanged("EmptyDynamicParametersText");
                return;
            }

            var definitions = StepParameterDefinitionProvider
                .GetDefinitions(SelectedWorkflowStep.StepType)
                .OrderBy(definition => definition.Section)
                .ThenBy(definition => definition.Order)
                .ToList();

            var orderedSections = new[]
            {
                StepParameterSection.Basic,
                StepParameterSection.Picker,
                StepParameterSection.Business,
                StepParameterSection.Advanced
            };

            foreach (var section in orderedSections)
            {
                var sectionDefinitions = definitions
                    .Where(definition => definition.Section == section)
                    .ToList();

                if (sectionDefinitions.Count == 0)
                {
                    continue;
                }

                CurrentStepParameterGroups.Add(new StepParameterGroupViewModel(
                    section,
                    GetSectionTitle(section),
                    new ObservableCollection<StepParameterViewModel>(sectionDefinitions.Select(definition => new StepParameterViewModel(SelectedWorkflowStep, definition, OnDynamicStepParameterValueChanged)))));
            }

            OnPropertyChanged("HasDynamicStepParameters");
            OnPropertyChanged("EmptyDynamicParametersText");
        }

        private void OnDynamicStepParameterValueChanged()
        {
            OnPropertyChanged("SelectedStepParametersText");
            OnPropertyChanged("SelectedStepParametersEditor");
            ScheduleStateSave();
        }

        private static string GetSectionTitle(StepParameterSection section)
        {
            switch (section)
            {
                case StepParameterSection.Picker:
                    return "拾取配置";
                case StepParameterSection.Business:
                    return "业务配置";
                case StepParameterSection.Advanced:
                    return "高级参数";
                default:
                    return "基础配置";
            }
        }

        public static string GetStepCategory(StepType stepType)
        {
            switch (stepType)
            {
                case StepType.LaunchIe:
                case StepType.AttachIe:
                case StepType.Navigate:
                case StepType.SwitchWindow:
                case StepType.SwitchFrame:
                    return "浏览器";
                case StepType.WaitForElement:
                case StepType.WaitPageReady:
                case StepType.ClickElement:
                case StepType.ClickAndSwitchWindow:
                case StepType.InputText:
                case StepType.ReadText:
                case StepType.SelectOption:
                case StepType.ExecuteScript:
                case StepType.HandleAlert:
                case StepType.UploadFile:
                case StepType.WaitDownload:
                case StepType.Screenshot:
                    return "页面交互";
                case StepType.HttpGetData:
                case StepType.HttpUploadFile:
                case StepType.SetVariable:
                    return "数据与接口";
                case StepType.PageListLoop:
                case StepType.QueryAndExportReport:
                case StepType.UpdateBusinessState:
                    return "组合业务";
                case StepType.Condition:
                case StepType.Loop:
                case StepType.LoopStart:
                case StepType.LoopEnd:
                case StepType.Delay:
                    return "控制流";
                case StepType.WriteLog:
                    return "调试与日志";
                default:
                    return "其他";
            }
        }
    }
}

