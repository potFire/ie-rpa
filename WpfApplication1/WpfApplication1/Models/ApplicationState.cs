using WpfApplication1.Enums;

namespace WpfApplication1.Models
{
    public class ApplicationState
    {
        public ApplicationState()
        {
            Workflow = new WorkflowDefinition();
            SchedulerSettings = new SchedulerSettings();
            SelectedShellPage = ShellPage.Workbench;
            DesignerLeftPaneWidth = 280;
            DesignerRightPaneWidth = 360;
            LogDrawerHeight = 240;
            IsLogDrawerExpanded = true;
            DesignerZoom = 1.0;
        }

        public string EmployeeId { get; set; }

        public string SelectedStepId { get; set; }

        public WorkflowDefinition Workflow { get; set; }

        public string ActiveWorkflowPath { get; set; }

        public string ActiveWorkflowId { get; set; }

        public ShellPage SelectedShellPage { get; set; }

        public SchedulerSettings SchedulerSettings { get; set; }

        public double DesignerLeftPaneWidth { get; set; }

        public double DesignerRightPaneWidth { get; set; }

        public double LogDrawerHeight { get; set; }

        public bool IsLogDrawerExpanded { get; set; }

        public WorkflowType? WorkflowManagementFilterType { get; set; }

        public double DesignerZoom { get; set; }

        public double CanvasViewportOffsetX { get; set; }

        public double CanvasViewportOffsetY { get; set; }

        public string SelectedLogWorkflowId { get; set; }

        public string SelectedLogRunId { get; set; }

        public WorkflowType? LogCenterFilterType { get; set; }

        public string LogCenterSearchText { get; set; }
    }
}
