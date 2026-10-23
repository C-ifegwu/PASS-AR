namespace PassAR
{
    /// <summary>
    /// One step of PASS. Each step decides for itself when it is finished.
    /// TaskManager only needs to know this common outline.
    /// </summary>
    public abstract class TrainingStep
    {
        protected readonly string stepName;
        protected float elapsed;

        protected TrainingStep(string stepName)
        {
            this.stepName = stepName;
        }

        public string Name => stepName;
        public float ElapsedInStep => elapsed;

        /// <summary>Text shown at the top of the screen while this step is active.</summary>
        public abstract string Instruction { get; }

        /// <summary>Warning shown when the user presses Squeeze too early during this step.</summary>
        public virtual string WrongOrderMessage => "Not yet - follow the steps in order";

        /// <summary>True for steps in which spraying is allowed.</summary>
        public virtual bool AllowsSpray => false;

        /// <summary>A step can ask to go back one step (for example when aim is lost while squeezing).</summary>
        public virtual bool ShouldRevert => false;

        public virtual void Enter()
        {
            elapsed = 0f;
        }

        public virtual void Tick(float dt)
        {
            elapsed += dt;
        }

        public abstract bool IsComplete();

        public virtual void Exit()
        {
        }
    }
}
