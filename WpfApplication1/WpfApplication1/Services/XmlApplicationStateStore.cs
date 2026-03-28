using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public class XmlApplicationStateStore : IApplicationStateStore
    {
        public Task SaveAsync(string path, ApplicationState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = state.SchedulerSettings ?? new SchedulerSettings();
            var document = new XDocument(
                new XElement("designerState",
                    new XElement("employeeId", state.EmployeeId ?? string.Empty),
                    new XElement("selectedStepId", state.SelectedStepId ?? string.Empty),
                    new XElement("activeWorkflowPath", state.ActiveWorkflowPath ?? string.Empty),
                    new XElement("activeWorkflowId", state.ActiveWorkflowId ?? string.Empty),
                    new XElement("selectedShellPage", state.SelectedShellPage),
                    new XElement("designerLeftPaneWidth", state.DesignerLeftPaneWidth.ToString(CultureInfo.InvariantCulture)),
                    new XElement("designerRightPaneWidth", state.DesignerRightPaneWidth.ToString(CultureInfo.InvariantCulture)),
                    new XElement("logDrawerHeight", state.LogDrawerHeight.ToString(CultureInfo.InvariantCulture)),
                    new XElement("isLogDrawerExpanded", state.IsLogDrawerExpanded),
                    new XElement("workflowManagementFilterType", state.WorkflowManagementFilterType.HasValue ? state.WorkflowManagementFilterType.Value.ToString() : string.Empty),
                    new XElement("designerZoom", state.DesignerZoom.ToString(CultureInfo.InvariantCulture)),
                    new XElement("canvasViewportOffsetX", state.CanvasViewportOffsetX.ToString(CultureInfo.InvariantCulture)),
                    new XElement("canvasViewportOffsetY", state.CanvasViewportOffsetY.ToString(CultureInfo.InvariantCulture)),
                    SerializeSchedulerSettings(settings),
                    SerializeWorkflow(state.Workflow)));

            document.Save(path);
            return Task.FromResult(0);
        }

        public Task<ApplicationState> LoadAsync(string path)
        {
            var document = XDocument.Load(path);
            var root = document.Element("designerState");
            var filterTypeRaw = GetElementValue(root, "workflowManagementFilterType");
            var state = new ApplicationState
            {
                EmployeeId = GetElementValue(root, "employeeId"),
                SelectedStepId = GetElementValue(root, "selectedStepId"),
                ActiveWorkflowPath = GetElementValue(root, "activeWorkflowPath"),
                ActiveWorkflowId = GetElementValue(root, "activeWorkflowId"),
                SelectedShellPage = ParseEnum(GetElementValue(root, "selectedShellPage"), ShellPage.Workbench),
                DesignerLeftPaneWidth = ParseDouble(GetElementValue(root, "designerLeftPaneWidth"), 280),
                DesignerRightPaneWidth = ParseDouble(GetElementValue(root, "designerRightPaneWidth"), 360),
                LogDrawerHeight = ParseDouble(GetElementValue(root, "logDrawerHeight"), 240),
                IsLogDrawerExpanded = ParseBool(GetElementValue(root, "isLogDrawerExpanded"), true),
                WorkflowManagementFilterType = string.IsNullOrWhiteSpace(filterTypeRaw) ? (WorkflowType?)null : ParseEnum(filterTypeRaw, WorkflowType.General),
                DesignerZoom = ParseDouble(GetElementValue(root, "designerZoom"), 1.0),
                CanvasViewportOffsetX = ParseDouble(GetElementValue(root, "canvasViewportOffsetX"), 0),
                CanvasViewportOffsetY = ParseDouble(GetElementValue(root, "canvasViewportOffsetY"), 0),
                SchedulerSettings = DeserializeSchedulerSettings(root != null ? root.Element("schedulerSettings") : null),
                Workflow = DeserializeWorkflow(root != null ? root.Element("workflow") : null)
            };

            if (state.SchedulerSettings == null)
            {
                state.SchedulerSettings = new SchedulerSettings();
            }

            state.SchedulerSettings.Normalize();
            if (state.Workflow == null)
            {
                state.Workflow = new WorkflowDefinition();
            }

            state.Workflow.EnsureCanvasLayout();
            return Task.FromResult(state);
        }

        private static XElement SerializeSchedulerSettings(SchedulerSettings settings)
        {
            return new XElement("schedulerSettings",
                new XElement("applyWorkflowId", settings.ApplyWorkflowId ?? string.Empty),
                new XElement("queryWorkflowId", settings.QueryWorkflowId ?? string.Empty),
                new XElement("approvalWorkflowId", settings.ApprovalWorkflowId ?? string.Empty),
                new XElement("applyPriority", settings.ApplyPriority),
                new XElement("maxContinuousApplyCount", settings.MaxContinuousApplyCount),
                new XElement("mainLoopIntervalMs", settings.MainLoopIntervalMs),
                new XElement("queryIntervalWhenNoApplyMs", settings.QueryIntervalWhenNoApplyMs),
                new XElement("resumePromptOnStartup", settings.ResumePromptOnStartup));
        }

        private static SchedulerSettings DeserializeSchedulerSettings(XElement settingsElement)
        {
            if (settingsElement == null)
            {
                return new SchedulerSettings();
            }

            var settings = new SchedulerSettings
            {
                ApplyWorkflowId = GetElementValue(settingsElement, "applyWorkflowId"),
                QueryWorkflowId = GetElementValue(settingsElement, "queryWorkflowId"),
                ApprovalWorkflowId = GetElementValue(settingsElement, "approvalWorkflowId"),
                ApplyPriority = ParseBool(GetElementValue(settingsElement, "applyPriority"), true),
                MaxContinuousApplyCount = ParseInt(GetElementValue(settingsElement, "maxContinuousApplyCount"), 3),
                MainLoopIntervalMs = ParseInt(GetElementValue(settingsElement, "mainLoopIntervalMs"), 2000),
                QueryIntervalWhenNoApplyMs = ParseInt(GetElementValue(settingsElement, "queryIntervalWhenNoApplyMs"), 5000),
                ResumePromptOnStartup = ParseBool(GetElementValue(settingsElement, "resumePromptOnStartup"), true)
            };
            settings.Normalize();
            return settings;
        }

        private static XElement SerializeWorkflow(WorkflowDefinition workflow)
        {
            var safeWorkflow = workflow ?? new WorkflowDefinition();
            safeWorkflow.EnsureCanvasLayout();
            return new XElement("workflow",
                new XAttribute("id", safeWorkflow.Id ?? string.Empty),
                new XAttribute("name", safeWorkflow.Name ?? string.Empty),
                new XAttribute("version", safeWorkflow.Version ?? string.Empty),
                new XAttribute("workflowType", safeWorkflow.WorkflowType),
                new XAttribute("description", safeWorkflow.Description ?? string.Empty),
                new XAttribute("applicableRole", safeWorkflow.ApplicableRole ?? string.Empty),
                new XAttribute("lastModifiedAt", safeWorkflow.LastModifiedAt.HasValue ? safeWorkflow.LastModifiedAt.Value.ToString("o") : string.Empty),
                new XAttribute("isPublished", safeWorkflow.IsPublished),
                SerializeCanvasLayout(safeWorkflow.CanvasLayout),
                new XElement("steps",
                    safeWorkflow.Steps.Select(SerializeStep)));
        }

        private static XElement SerializeCanvasLayout(CanvasLayout layout)
        {
            var safeLayout = layout ?? new CanvasLayout();
            return new XElement("canvasLayout",
                new XAttribute("zoom", safeLayout.Zoom.ToString(CultureInfo.InvariantCulture)),
                new XElement("nodes", safeLayout.Nodes.Select(node => new XElement("node",
                    new XAttribute("stepId", node.StepId ?? string.Empty),
                    new XAttribute("x", node.X.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("y", node.Y.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("width", node.Width.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("height", node.Height.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("visualGroup", node.VisualGroup ?? string.Empty)))),
                new XElement("connections", safeLayout.Connections.Select(connection => new XElement("connection",
                    new XAttribute("from", connection.FromStepId ?? string.Empty),
                    new XAttribute("to", connection.ToStepId ?? string.Empty)))));
        }

        private static XElement SerializeStep(WorkflowStep step)
        {
            var safeStep = step ?? new WorkflowStep();
            return new XElement("step",
                new XAttribute("id", safeStep.Id ?? string.Empty),
                new XAttribute("name", safeStep.Name ?? string.Empty),
                new XAttribute("stepType", safeStep.StepType),
                new XAttribute("timeoutMs", safeStep.TimeoutMs),
                new XAttribute("retryCount", safeStep.RetryCount),
                new XAttribute("continueOnError", safeStep.ContinueOnError),
                new XElement("parameters",
                    safeStep.Parameters.Select(pair =>
                        new XElement("parameter",
                            new XAttribute("key", pair.Key ?? string.Empty),
                            new XAttribute("value", pair.Value ?? string.Empty)))));
        }

        private static WorkflowDefinition DeserializeWorkflow(XElement workflowElement)
        {
            if (workflowElement == null)
            {
                return new WorkflowDefinition();
            }

            DateTime lastModifiedAt;
            DateTime.TryParse(GetAttributeValue(workflowElement, "lastModifiedAt"), out lastModifiedAt);
            var workflow = new WorkflowDefinition
            {
                Id = GetAttributeValue(workflowElement, "id"),
                Name = GetAttributeValue(workflowElement, "name"),
                Version = GetAttributeValue(workflowElement, "version"),
                WorkflowType = ParseEnum(GetAttributeValue(workflowElement, "workflowType"), WorkflowType.General),
                Description = GetAttributeValue(workflowElement, "description"),
                ApplicableRole = GetAttributeValue(workflowElement, "applicableRole"),
                LastModifiedAt = lastModifiedAt == default(DateTime) ? (DateTime?)null : lastModifiedAt,
                IsPublished = ParseBool(GetAttributeValue(workflowElement, "isPublished"), false),
                Steps = new ObservableCollection<WorkflowStep>(
                    workflowElement.Element("steps") != null
                        ? workflowElement.Element("steps").Elements("step").Select(DeserializeStep)
                        : Enumerable.Empty<WorkflowStep>()),
                CanvasLayout = DeserializeCanvasLayout(workflowElement.Element("canvasLayout"))
            };
            workflow.EnsureCanvasLayout();
            return workflow;
        }

        private static CanvasLayout DeserializeCanvasLayout(XElement canvasElement)
        {
            var layout = new CanvasLayout
            {
                Zoom = ParseDouble(GetAttributeValue(canvasElement, "zoom"), 1.0),
                Nodes = new ObservableCollection<CanvasNodeLayout>(),
                Connections = new ObservableCollection<CanvasConnection>()
            };

            var nodesElement = canvasElement != null ? canvasElement.Element("nodes") : null;
            if (nodesElement != null)
            {
                foreach (var nodeElement in nodesElement.Elements("node"))
                {
                    layout.Nodes.Add(new CanvasNodeLayout
                    {
                        StepId = GetAttributeValue(nodeElement, "stepId"),
                        X = ParseDouble(GetAttributeValue(nodeElement, "x"), 0),
                        Y = ParseDouble(GetAttributeValue(nodeElement, "y"), 0),
                        Width = ParseDouble(GetAttributeValue(nodeElement, "width"), 200),
                        Height = ParseDouble(GetAttributeValue(nodeElement, "height"), 88),
                        VisualGroup = GetAttributeValue(nodeElement, "visualGroup")
                    });
                }
            }

            var connectionsElement = canvasElement != null ? canvasElement.Element("connections") : null;
            if (connectionsElement != null)
            {
                foreach (var connectionElement in connectionsElement.Elements("connection"))
                {
                    layout.Connections.Add(new CanvasConnection
                    {
                        FromStepId = GetAttributeValue(connectionElement, "from"),
                        ToStepId = GetAttributeValue(connectionElement, "to")
                    });
                }
            }

            return layout;
        }

        private static WorkflowStep DeserializeStep(XElement stepElement)
        {
            var step = new WorkflowStep
            {
                Id = GetAttributeValue(stepElement, "id"),
                Name = GetAttributeValue(stepElement, "name"),
                StepType = ParseEnum(stepElement, "stepType", StepType.SetVariable),
                TimeoutMs = ParseInt(stepElement, "timeoutMs", 10000),
                RetryCount = ParseInt(stepElement, "retryCount", 0),
                ContinueOnError = ParseBool(stepElement, "continueOnError", false),
                Parameters = new StepParameterBag()
            };

            var parametersElement = stepElement.Element("parameters");
            if (parametersElement != null)
            {
                foreach (var parameterElement in parametersElement.Elements("parameter"))
                {
                    var key = GetAttributeValue(parameterElement, "key");
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    step.Parameters[key] = GetAttributeValue(parameterElement, "value");
                }
            }

            return step;
        }

        private static string GetElementValue(XElement parent, string name)
        {
            var element = parent != null ? parent.Element(name) : null;
            return element != null ? element.Value : string.Empty;
        }

        private static string GetAttributeValue(XElement element, string name)
        {
            var attribute = element != null ? element.Attribute(name) : null;
            return attribute != null ? attribute.Value : string.Empty;
        }

        private static int ParseInt(XElement element, string attributeName, int defaultValue)
        {
            return ParseInt(GetAttributeValue(element, attributeName), defaultValue);
        }

        private static int ParseInt(string raw, int defaultValue)
        {
            int value;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : defaultValue;
        }

        private static double ParseDouble(string raw, double defaultValue)
        {
            double value;
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : defaultValue;
        }

        private static bool ParseBool(XElement element, string attributeName, bool defaultValue)
        {
            return ParseBool(GetAttributeValue(element, attributeName), defaultValue);
        }

        private static bool ParseBool(string raw, bool defaultValue)
        {
            bool value;
            return bool.TryParse(raw, out value) ? value : defaultValue;
        }

        private static TEnum ParseEnum<TEnum>(XElement element, string attributeName, TEnum defaultValue) where TEnum : struct
        {
            return ParseEnum(GetAttributeValue(element, attributeName), defaultValue);
        }

        private static TEnum ParseEnum<TEnum>(string raw, TEnum defaultValue) where TEnum : struct
        {
            TEnum value;
            return Enum.TryParse(raw, true, out value) ? value : defaultValue;
        }
    }
}

