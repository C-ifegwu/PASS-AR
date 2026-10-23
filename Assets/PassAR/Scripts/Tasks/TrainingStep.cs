namespace PassAR
{
    /// <summary>
    /// One step of the PASS procedure. Concrete steps decide for themselves when
    /// they are complete; TaskManager only talks to this base class.
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

        /// <summary>Text shown at the top of the screen while this step is active (FR-05).</summary>
        public abstract string Instruction { get; }

        /// <summary>Shown as a warning when the user presses Squeeze during this step too early (FR-12).</summary>
        public virtual string WrongOrderMessage => "Not yet - follow the steps in order";

        /// <summary>True for steps in which spraying is allowed.</summary>
        public virtual bool AllowsSpray => false;

        /// <summary>A step can ask TaskManager to go back one step (aim lost while squeezing).</summary>
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
