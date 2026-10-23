namespace PassAR
{
    /// <summary>
    /// The main stages the app can be in.
    /// The four PASS steps are not listed here. They are handled by the step scripts.
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
