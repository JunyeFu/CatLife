using System;

namespace CatLife.Mobile
{
    // UTC supplies the persisted deadline; elapsed time supplies progress in this process.
    public sealed class CatLifeSessionClock
    {
        private long utcAnchor;
        private double elapsedAnchor;
        public void Anchor(long utc, double elapsed) { utcAnchor = utc; elapsedAnchor = elapsed; }
        public long Now(double elapsed) => utcAnchor + (long)Math.Max(0, elapsed - elapsedAnchor);
    }
}
