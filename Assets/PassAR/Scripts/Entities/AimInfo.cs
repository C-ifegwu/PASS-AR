namespace PassAR
{
    /// <summary>What the circle in the middle of the screen is pointing at right now.</summary>
    public struct AimInfo
    {
        public FireZone Zone;
        public float Distance;
        public bool DistanceOk;

        public bool HitsZone => Zone != null;

        /// <summary>Pointing at the base of a fire zone and standing at a safe distance.</summary>
        public bool Valid => HitsZone && DistanceOk;
    }
}
