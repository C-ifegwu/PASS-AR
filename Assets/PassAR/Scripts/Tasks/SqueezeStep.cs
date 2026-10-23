namespace PassAR
{
    /// <summary>S - Squeeze the handle. Complete once the first fire zone is out.</summary>
    public class SqueezeStep : TrainingStep
    {
        private readonly Extinguisher extinguisher;
        private readonly FireHazard hazard;
        private readonly ScenarioConfig config;
        private float aimLostTime;

        public SqueezeStep(Extinguisher extinguisher, FireHazard hazard, ScenarioConfig config) : base("Squeeze")
        {
            this.extinguisher = extinguisher;
            this.hazard = hazard;
            this.config = config;
        }

        public override string Instruction => "S - SQUEEZE: hold SQUEEZE while aiming at the base of the fire.";

        public override bool AllowsSpray => true;

        public override bool ShouldRevert => aimLostTime > config.aimLostGrace;

        public override void Enter()
        {
            base.Enter();
            aimLostTime = 0f;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            aimLostTime = extinguisher.CurrentAim.Valid ? 0f : aimLostTime + dt;
        }

        public override bool IsComplete()
        {
            return hazard.ZonesOut >= 1;
        }
    }
}
