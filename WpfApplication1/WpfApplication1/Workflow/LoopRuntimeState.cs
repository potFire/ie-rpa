namespace WpfApplication1.Workflow
{
    public class LoopRuntimeState
    {
        public LoopRuntimeState()
        {
            NextIterationNumber = 1;
        }

        public int CompletedIterations { get; set; }

        public int NextIterationNumber { get; set; }

        public bool SkipCurrentCycle { get; set; }
    }
}
