using System;

namespace WpfApplication1.Models
{
    public class SchedulerExecutionContext
    {
        public string EmployeeId { get; set; }

        public string BusinessStatePath { get; set; }

        public string SchedulerStatePath { get; set; }

        public BusinessStateRecord PendingBusinessState { get; set; }

        public Action<BusinessStateRecord> BusinessStateChanged { get; set; }

        public Action<RuntimeStateSnapshot> RuntimeStateChanged { get; set; }

        public Action<TaskSummarySnapshot> TaskSummaryChanged { get; set; }

        public Action<RunHistoryItem> RunHistoryAdded { get; set; }
    }
}
