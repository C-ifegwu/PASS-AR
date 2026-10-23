using System;
using System.Collections.Generic;

namespace PassAR
{
    /// <summary>
    /// Runs the PASS steps one after another, counts mistakes, measures time and builds
    /// the result. It is a plain class (not attached to an object) so it is easy to test.
    /// </summary>
    public class TaskManager
    {
        private readonly int errorPenalty;
        private List<TrainingStep> steps = new List<TrainingStep>();
        private float[] stepTimes = new float[0];
        private int currentIndex;
        private int errors;
        private float elapsed;
        private bool completed;
        private DateTime startedAt;

        public event Action<TrainingStep> StepChanged;
        public event Action<string> ErrorReported;
        public event Action Completed;

        public TaskManager(int errorPenalty)
        {
            this.errorPenalty = errorPenalty;
        }

        public TrainingStep CurrentStep =>
            (!completed && currentIndex < steps.Count) ? steps[currentIndex] : null;

        public int CurrentIndex => currentIndex;
        public int Errors => errors;
        public float ElapsedSeconds => elapsed;
        public bool IsCompleted => completed;
        public bool SprayAllowed => CurrentStep != null && CurrentStep.AllowsSpray;

        public void Begin(List<TrainingStep> newSteps)
        {
            steps = newSteps;
            stepTimes = new float[steps.Count];
            currentIndex = 0;
            errors = 0;
            elapsed = 0f;
            completed = false;
            startedAt = DateTime.Now;

            steps[0].Enter();
            StepChanged?.Invoke(steps[0]);
        }

        public void Tick(float dt)
        {
            if (completed || steps.Count == 0) return;

            elapsed += dt;
            stepTimes[currentIndex] += dt;

            TrainingStep step = steps[currentIndex];
            step.Tick(dt);

            if (step.ShouldRevert && currentIndex > 0)
                Revert();
            else if (step.IsComplete())
                Advance();
        }

        public void Advance()
        {
            if (completed) return;

            steps[currentIndex].Exit();
            currentIndex++;

            if (currentIndex >= steps.Count)
            {
                completed = true;
                Completed?.Invoke();
                return;
            }

            steps[currentIndex].Enter();
            StepChanged?.Invoke(steps[currentIndex]);
        }

        private void Revert()
        {
            steps[currentIndex].Exit();
            currentIndex--;
            steps[currentIndex].Enter();
            StepChanged?.Invoke(steps[currentIndex]);
        }

        public void ReportError(string reason)
        {
            errors++;
            ErrorReported?.Invoke(reason);
        }

        /// <summary>
        /// Called when the user presses Squeeze. Returns true if spraying is allowed now.
        /// If not, the press is ignored and counts as one mistake.
        /// </summary>
        public bool TrySqueeze()
        {
            if (SprayAllowed) return true;

            string message = CurrentStep != null ? CurrentStep.WrongOrderMessage : "Not allowed now";
            ReportError(message);
            return false;
        }

        public SessionRecord BuildRecord()
        {
            return new SessionRecord
            {
                sessionId = startedAt.ToString("yyyyMMdd-HHmmss"),
                startTime = startedAt.ToString("s"),
                durationSec = elapsed,
                errors = errors,
                score = CalculateScore(errors, errorPenalty),
                stepTimes = (float[])stepTimes.Clone(),
                completed = completed
            };
        }

        /// <summary>100 minus the penalty for each mistake. It never goes below 0.</summary>
        public static int CalculateScore(int errors, int penaltyPerError)
        {
            int score = 100 - errors * penaltyPerError;
            return score < 0 ? 0 : score;
        }
    }
}
