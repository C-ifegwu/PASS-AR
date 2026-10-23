namespace PassAR
{
    /// <summary>What the centre-screen reticle is pointing at this frame.</summary>
    public struct AimInfo
    {
        public FireZone Zone;
        public float Distance;
        public bool DistanceOk;

        public bool HitsZone => Zone != null;

        /// <summary>On a fire zone base and inside the safe distance range (FR-08).</summary>
        public bool Valid => HitsZone && DistanceOk;
    }
}
