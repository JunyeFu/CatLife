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
        private bool interventionEnabled;
        private float origin;
        private float contactPointer;
        private double prepareStarted;

        public void Begin(float pointer, bool enabled, bool alreadyShown)
        {
            origin = pointer;
            interventionEnabled = enabled;
            InterventionShown = alreadyShown;
            Progress = 0;
            Phase = CatLifeExitGesturePhase.Dragging;
        }

        public void Step(float pointer, double now)
        {
            if (Phase == CatLifeExitGesturePhase.Idle) return;
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
                Phase = CatLifeExitGesturePhase.Released;
                return; // Rebase at the current finger position; never jump on release.
            }
            Progress = Math.Max(0, Math.Min(1, pointer - origin));
            if (!interventionEnabled || InterventionShown) return;
            if (Progress >= .35f)
            {
                Progress = .35f;
                InterventionShown = true;
                prepareStarted = now;
                Phase = CatLifeExitGesturePhase.Preparing;
            }
            else Phase = Progress >= .15f ? CatLifeExitGesturePhase.Reaching : CatLifeExitGesturePhase.Dragging;
        }

        public bool End()
        {
            bool confirm = Phase != CatLifeExitGesturePhase.Idle && Progress >= .65f;
            Cancel();
            return confirm;
        }

        public void Cancel()
        {
            Progress = 0;
            Phase = CatLifeExitGesturePhase.Idle;
        }
    }
}
