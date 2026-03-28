using System;
using WpfApplication1.Enums;

namespace WpfApplication1.Models
{
    public class WorkflowCreateRequest
    {
        public WorkflowType WorkflowType { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string ApplicableRole { get; set; }
    }
}
