namespace PassAR
{
    /// <summary>S - Sweep side to side until every fire zone is out.</summary>
    public class SweepStep : TrainingStep
    {
        private readonly FireHazard hazard;

        public SweepStep(FireHazard hazard) : base("Sweep")
        {
            this.hazard = hazard;
        }

        public override string Instruction => "S - SWEEP side to side until all 3 fire zones are out.";

        public override bool AllowsSpray => true;

        public override bool IsComplete()
        {
            return hazard.IsNeutralised();
        }
    }
}
