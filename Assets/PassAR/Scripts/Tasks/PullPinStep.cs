namespace PassAR
{
    /// <summary>P - Pull the pin.</summary>
    public class PullPinStep : TrainingStep
    {
        private readonly Extinguisher extinguisher;

        public PullPinStep(Extinguisher extinguisher) : base("Pull")
        {
            this.extinguisher = extinguisher;
        }

        public override string Instruction => "P - PULL the safety pin. Press PULL PIN.";

        public override string WrongOrderMessage => "Wrong order - pull the pin first";

        public override bool IsComplete()
        {
            return extinguisher.PinIsPulled;
        }
    }
}
