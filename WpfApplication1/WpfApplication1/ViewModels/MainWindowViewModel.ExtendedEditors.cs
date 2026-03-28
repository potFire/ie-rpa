using System;
using System.Collections.Generic;
using WpfApplication1.Enums;

namespace WpfApplication1.ViewModels
{
    public partial class MainWindowViewModel
    {
        public bool ShowHttpUploadFileEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.HttpUploadFile; }
        }

        public bool ShowPageListLoopEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.PageListLoop; }
        }

        public bool ShowQueryAndExportReportEditor
        {
            get { return SelectedWorkflowStep != null && SelectedWorkflowStep.StepType == StepType.QueryAndExportReport; }
        }

        public string HttpUploadUrlParameterValue
        {
            get { return GetStepParameter("url"); }
            set { SetStepParameter("url", value); }
        }

        public string HttpUploadFilePathParameterValue
        {
            get { return GetStepParameter("filePath"); }
            set { SetStepParameter("filePath", value); }
        }

        public string HttpUploadResponseVariableParameterValue
        {
            get { return GetStepParameter("responseVariableName"); }
            set { SetStepParameter("responseVariableName", value); }
        }

        public string PageListModeParameterValue
        {
            get
            {
                var value = GetStepParameter("mode");
                return string.IsNullOrWhiteSpace(value) ? "approve" : value;
            }
            set { SetStepParameter("mode", value); }
        }

        public string FilterSelectorParameterValue
        {
            get { return GetStepParameter("filterSelector"); }
            set { SetStepParameter("filterSelector", value); }
        }

        public string FilterValueParameterValue
        {
            get { return GetStepParameter("filterValue"); }
            set { SetStepParameter("filterValue", value); }
        }

        public string QueryButtonSelectorParameterValue
        {
            get { return GetStepParameter("queryButtonSelector"); }
            set { SetStepParameter("queryButtonSelector", value); }
        }

        public string ListReadySelectorParameterValue
        {
            get { return GetStepParameter("listReadySelector"); }
            set { SetStepParameter("listReadySelector", value); }
        }

        public string RowSelectorTemplateParameterValue
        {
            get { return GetStepParameter("rowSelectorTemplate"); }
            set { SetStepParameter("rowSelectorTemplate", value); }
        }

        public string RowActionSelectorTemplateParameterValue
        {
            get { return GetStepParameter("rowActionSelectorTemplate"); }
            set { SetStepParameter("rowActionSelectorTemplate", value); }
        }

        public string PageListMaxRowsParameterValue
        {
            get { return GetStepParameter("maxRows"); }
            set { SetStepParameter("maxRows", value); }
        }

        public string PageListMaxRoundsParameterValue
        {
            get { return GetStepParameter("maxRounds"); }
            set { SetStepParameter("maxRounds", value); }
        }

        public string PageListPollIntervalParameterValue
        {
            get { return GetStepParameter("pollIntervalMs"); }
            set { SetStepParameter("pollIntervalMs", value); }
        }

        public string PageListTargetWindowTitleParameterValue
        {
            get { return GetStepParameter("targetWindowTitle"); }
            set { SetStepParameter("targetWindowTitle", value); }
        }

        public string PageListWindowMatchModeParameterValue
        {
            get
            {
                var value = GetStepParameter("windowMatchMode");
                return string.IsNullOrWhiteSpace(value) ? "contains" : value;
            }
            set { SetStepParameter("windowMatchMode", value); }
        }

        public string DetailReadySelectorParameterValue
        {
            get { return GetStepParameter("detailReadySelector"); }
            set { SetStepParameter("detailReadySelector", value); }
        }

        public string DetailActionSelectorParameterValue
        {
            get { return GetStepParameter("detailActionSelector"); }
            set { SetStepParameter("detailActionSelector", value); }
        }

        public string PageListReturnModeParameterValue
        {
            get
            {
                var value = GetStepParameter("returnMode");
                return string.IsNullOrWhiteSpace(value) ? "closeCurrentWindow" : value;
            }
            set { SetStepParameter("returnMode", value); }
        }

        public string PageListReturnSelectorParameterValue
        {
            get { return GetStepParameter("returnSelector"); }
            set { SetStepParameter("returnSelector", value); }
        }

        public string PageListReturnButtonTextParameterValue
        {
            get { return GetStepParameter("returnButtonText"); }
            set { SetStepParameter("returnButtonText", value); }
        }

        public string PageListReturnTitleContainsParameterValue
        {
            get { return GetStepParameter("returnTitleContains"); }
            set { SetStepParameter("returnTitleContains", value); }
        }

        public string PopupReadySelectorParameterValue
        {
            get { return GetStepParameter("popupReadySelector"); }
            set { SetStepParameter("popupReadySelector", value); }
        }

        public string PopupPollIntervalParameterValue
        {
            get { return GetStepParameter("popupPollIntervalMs"); }
            set { SetStepParameter("popupPollIntervalMs", value); }
        }

        public string ReportIframeSelectorParameterValue
        {
            get { return GetStepParameter("reportIframeSelector"); }
            set { SetStepParameter("reportIframeSelector", value); }
        }

        public string SaveDirectoryParameterValue
        {
            get { return GetStepParameter("saveDirectory"); }
            set { SetStepParameter("saveDirectory", value); }
        }

        public string FileNameTemplateParameterValue
        {
            get { return GetStepParameter("fileNameTemplate"); }
            set { SetStepParameter("fileNameTemplate", value); }
        }

        public string ReportUploadUrlParameterValue
        {
            get { return GetStepParameter("uploadUrl"); }
            set { SetStepParameter("uploadUrl", value); }
        }

        public string ClosePopupSelectorParameterValue
        {
            get { return GetStepParameter("closePopupSelector"); }
            set { SetStepParameter("closePopupSelector", value); }
        }

        public string OutputFileVariableNameParameterValue
        {
            get { return GetStepParameter("outputFileVariableName"); }
            set { SetStepParameter("outputFileVariableName", value); }
        }

        public string UploadResponseVariableNameParameterValue
        {
            get { return GetStepParameter("uploadResponseVariableName"); }
            set { SetStepParameter("uploadResponseVariableName", value); }
        }

        public IEnumerable<string> PageListModeOptions
        {
            get { return PageListModeOptionsSource; }
        }

        public IEnumerable<string> ReturnModeOptions
        {
            get { return ReturnModeOptionsSource; }
        }
    }
}