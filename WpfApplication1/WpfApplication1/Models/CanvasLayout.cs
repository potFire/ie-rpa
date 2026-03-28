using System;
using System.Collections.ObjectModel;
using System.Linq;
using WpfApplication1.Common;

namespace WpfApplication1.Models
{
    public class CanvasLayout : BindableBase
    {
        private ObservableCollection<CanvasNodeLayout> _nodes = new ObservableCollection<CanvasNodeLayout>();
        private ObservableCollection<CanvasConnection> _connections = new ObservableCollection<CanvasConnection>();
        private double _zoom = 1.0;

        public ObservableCollection<CanvasNodeLayout> Nodes
        {
            get { return _nodes; }
            set { SetProperty(ref _nodes, value); }
        }

        public ObservableCollection<CanvasConnection> Connections
        {
            get { return _connections; }
            set { SetProperty(ref _connections, value); }
        }

        public double Zoom
        {
            get { return _zoom; }
            set { SetProperty(ref _zoom, value); }
        }

        public void EnsureLinearLayout(System.Collections.Generic.IEnumerable<WorkflowStep> steps)
        {
            if (Nodes == null)
            {
                Nodes = new ObservableCollection<CanvasNodeLayout>();
            }

            if (Connections == null)
            {
                Connections = new ObservableCollection<CanvasConnection>();
            }

            var safeSteps = (steps ?? Enumerable.Empty<WorkflowStep>()).Where(step => step != null).ToList();
            for (var index = 0; index < safeSteps.Count; index++)
            {
                var step = safeSteps[index];
                if (Nodes.All(node => !string.Equals(node.StepId, step.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    Nodes.Add(new CanvasNodeLayout
                    {
                        StepId = step.Id,
                        X = 120 + index * 260,
                        Y = 120,
                        Width = 200,
                        Height = 88,
                        VisualGroup = "Main"
                    });
                }
            }

            for (var index = Nodes.Count - 1; index >= 0; index--)
            {
                var node = Nodes[index];
                if (safeSteps.All(step => !string.Equals(step.Id, node.StepId, StringComparison.OrdinalIgnoreCase)))
                {
                    Nodes.RemoveAt(index);
                }
            }

            Connections.Clear();
            for (var index = 0; index < safeSteps.Count - 1; index++)
            {
                Connections.Add(new CanvasConnection
                {
                    FromStepId = safeSteps[index].Id,
                    ToStepId = safeSteps[index + 1].Id
                });
            }
        }
    }
}
