using System;

namespace CatLife.Mobile
{
    public enum CatLifeExitGesturePhase { Idle, Dragging, Reaching, Preparing, Holding, Released }

    // Pointer positions are measured in effective track lengths. The view owns pointer identity.
    public sealed class CatLifeExitGesture
    {
        public CatLifeExitGesturePhase Phase { get; private set; }
        public float Progress { get; private set; }
        public bool InterventionShown { get; private set; }
        public bool IsSettling { get; private set; }
        private bool interventionEnabled;
        private readonly double slowRoot, fastRoot;
        private double velocity, lastStep;
        private float origin;
        private float contactPointer;
        private double prepareStarted;

        public CatLifeExitGesture(double stiffness, double damping)
        {
            double discriminant = Math.Sqrt(damping * damping - 4 * stiffness);
            slowRoot = (-damping + discriminant) / 2;
            fastRoot = (-damping - discriminant) / 2;
        }

        public void Begin(float pointer, double now, bool enabled, bool alreadyShown)
        {
            origin = pointer - Progress;
            interventionEnabled = enabled;
            InterventionShown = alreadyShown;
            velocity = 0; lastStep = now; IsSettling = false;
            Phase = CatLifeExitGesturePhase.Dragging;
        }

        public void Step(float pointer, double now)
        {
            double dt = now - lastStep;
            lastStep = now;
            if (Phase == CatLifeExitGesturePhase.Idle)
            {
                if (!IsSettling) return;
                AdvanceSpring(0, dt);
                if (Progress < .0001f && Math.Abs(velocity) < .001) Cancel();
                return;
            }
            if (Phase == CatLifeExitGesturePhase.Preparing)
            {
                if (now - prepareStarted < .4) return;
                contactPointer = pointer;
                Phase = CatLifeExitGesturePhase.Holding;
            }
            if (Phase == CatLifeExitGesturePhase.Holding)
            {
                if (now - prepareStarted < 1.2 && pointer - contactPointer < .1f) return;
                origin = pointer - .35f;
                velocity = 0;
                Phase = CatLifeExitGesturePhase.Released;
                return; // Rebase at the current finger position; never jump on release.
            }
            AdvanceSpring(Math.Max(0, Math.Min(1, pointer - origin)), dt);
            if (!interventionEnabled || InterventionShown) return;
            if (Progress >= .35f)
            {
                Progress = .35f;
                velocity = 0;
                InterventionShown = true;
                prepareStarted = now;
                Phase = CatLifeExitGesturePhase.Preparing;
            }
            else Phase = Progress >= .15f ? CatLifeExitGesturePhase.Reaching : CatLifeExitGesturePhase.Dragging;
        }

        public bool End()
        {
            bool confirm = Phase != CatLifeExitGesturePhase.Idle && Progress >= .65f;
            Phase = CatLifeExitGesturePhase.Idle;
            velocity = 0;
            IsSettling = Progress > 0;
            return confirm;
        }

        private void AdvanceSpring(double target, double dt)
        {
            // Exact overdamped solution (mass = 1). Stable even after a late frame.
            double displacement = Progress - target;
            double a = (velocity - fastRoot * displacement) / (slowRoot - fastRoot);
            double b = displacement - a;
            double slow = a * Math.Exp(slowRoot * dt), fast = b * Math.Exp(fastRoot * dt);
            double position = target + slow + fast;
            velocity = slowRoot * slow + fastRoot * fast;
            Progress = (float)Math.Max(0, Math.Min(1, position));
            if (position <= 0 || position >= 1) velocity = 0;
        }

        public void Cancel()
        {
            Progress = 0;
            velocity = 0; IsSettling = false;
            Phase = CatLifeExitGesturePhase.Idle;
        }
    }
}
