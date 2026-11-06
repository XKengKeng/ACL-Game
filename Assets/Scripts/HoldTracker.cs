namespace RhythmMilestone
{
    // Shared by real input and autoplay; scoring happens only on terminal transitions.
    public sealed class HoldTracker
    {
        public enum Result { Waiting, Holding, Complete, Broken }
        public Result State { get; private set; }
        public readonly float EndTime;
        public const float ReleaseTolerance = 0.075f;

        public HoldTracker(float endTime) { EndTime = endTime; }
        public void Begin() { if (State == Result.Waiting) State = Result.Holding; }
        public Result Tick(float time, bool pressed)
        {
            if (State != Result.Holding) return State;
            if (!pressed) State = time >= EndTime - ReleaseTolerance ? Result.Complete : Result.Broken;
            else if (time >= EndTime) State = Result.Complete;
            return State;
        }
    }
}
