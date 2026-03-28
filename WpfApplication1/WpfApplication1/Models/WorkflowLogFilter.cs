using WpfApplication1.Enums;

namespace WpfApplication1.Models
{
    public class WorkflowLogFilter
    {
        public string SearchText { get; set; }

        public WorkflowType? WorkflowType { get; set; }
    }
}
