namespace PassAR
{
    /// <summary>
    /// Top-level states of the simulation (Figure 3 in the design document).
    /// The four PASS steps are not listed here, they live in TrainingStep.
    /// </summary>
    public enum SimState
    {
        Welcome,
        Scanning,
        Running,
        Paused,
        Summary
    }
}
