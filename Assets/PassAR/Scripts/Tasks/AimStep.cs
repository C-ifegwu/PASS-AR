namespace PassAR
{
    /// <summary>A - Aim at the base of the fire from a safe distance.</summary>
    public class AimStep : TrainingStep
    {
        private readonly Extinguisher extinguisher;
        private readonly ScenarioConfig config;
        private float validTime;

        public AimStep(Extinguisher extinguisher, ScenarioConfig config) : base("Aim")
        {
            this.extinguisher = extinguisher;
            this.config = config;
        }

        public override string Instruction =>
            "A - AIM at the BASE of the fire from " + config.minDist.ToString("0") + " to " + config.maxDist.ToString("0") + " m.";

        public override string WrongOrderMessage => "Wrong order - aim at the base of the fire first";

        public override void Enter()
        {
            base.Enter();
            validTime = 0f;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            validTime = extinguisher.CurrentAim.Valid ? validTime + dt : 0f;
        }

        public override bool IsComplete()
        {
            return validTime >= config.aimHoldSeconds;
        }
    }
}
