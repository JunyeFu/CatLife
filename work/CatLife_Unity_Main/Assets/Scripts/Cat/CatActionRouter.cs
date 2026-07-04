using System.Collections.Generic;
using UnityEngine;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    public sealed class CatActionRouter : MonoBehaviour
    {
        [Header("Default Cooldowns")]
        [SerializeField] private float ambientCooldownSeconds = 6f;
        [SerializeField] private float userCooldownSeconds = 8f;
        [SerializeField] private float uiCooldownSeconds = 10f;
        [SerializeField] private float sessionCooldownSeconds = 8f;
        [SerializeField] private float recognitionCooldownSeconds = 12f;

        [Header("Queue")]
        [SerializeField] private float defaultMaxDelaySeconds = 2f;

        private readonly Dictionary<CatBehaviorState, float> lastStartedAt = new Dictionary<CatBehaviorState, float>();
        private CatActionRequest pendingRequest;
        private bool hasPendingRequest;

        public int QueuedActionCount { get { return hasPendingRequest ? 1 : 0; } }
        public CatBehaviorState PendingAction { get { return hasPendingRequest ? pendingRequest.state : CatBehaviorState.None; } }
        public string PendingReason { get { return hasPendingRequest ? pendingRequest.reason : ""; } }
        public CatBehaviorState LastAcceptedAction { get; private set; }
        public CatActionSource LastAcceptedSource { get; private set; }
        public string LastAcceptedReason { get; private set; }
        public string LastDecision { get; private set; }

        public bool TryRoute(
            CatActionRequest request,
            bool isMoving,
            bool isBusy,
            out CatActionRequest playableRequest)
        {
            request = Normalize(request);
            playableRequest = default;

            if (!IsRequestValid(request))
            {
                LastDecision = "drop_invalid";
                return false;
            }

            if (IsOnCooldown(request))
            {
                LastDecision = "drop_cooldown_" + request.state;
                return false;
            }

            if (isBusy && request.interruptPolicy != CatActionInterruptPolicy.PlayNow)
            {
                if (ShouldQueue(request))
                {
                    QueueOrReplace(request);
                }
                else
                {
                    LastDecision = "drop_busy_" + request.state;
                }

                return false;
            }

            if (isMoving && request.interruptPolicy == CatActionInterruptPolicy.QueueIfMoving)
            {
                QueueOrReplace(request);
                return false;
            }

            if (isMoving && request.interruptPolicy == CatActionInterruptPolicy.DropIfBusy)
            {
                LastDecision = "drop_moving_" + request.state;
                return false;
            }

            playableRequest = request;
            MarkStarted(request);
            return true;
        }

        public bool TryPopReady(
            bool isMoving,
            bool isBusy,
            out CatActionRequest playableRequest)
        {
            playableRequest = default;
            if (!hasPendingRequest || isBusy)
            {
                return false;
            }

            CatActionRequest request = pendingRequest;
            float age = Time.time - request.createdAt;
            if (request.maxDelaySeconds > 0f && age > request.maxDelaySeconds)
            {
                if (request.interruptPolicy != CatActionInterruptPolicy.QueueIfMoving)
                {
                    hasPendingRequest = false;
                    LastDecision = "drop_expired_" + request.state;
                    return false;
                }
            }
            else if (isMoving && request.interruptPolicy == CatActionInterruptPolicy.QueueIfMoving)
            {
                return false;
            }

            hasPendingRequest = false;
            if (IsOnCooldown(request))
            {
                LastDecision = "drop_queued_cooldown_" + request.state;
                return false;
            }

            playableRequest = request;
            MarkStarted(request);
            return true;
        }

        public void ClearQueue()
        {
            hasPendingRequest = false;
            LastDecision = "clear_queue";
        }

        private CatActionRequest Normalize(CatActionRequest request)
        {
            if (request.createdAt <= 0f)
            {
                request.createdAt = Time.time;
            }

            if (request.priority <= 0)
            {
                request.priority = GetDefaultPriority(request.source);
            }

            if (request.cooldownSeconds <= 0f)
            {
                request.cooldownSeconds = GetDefaultCooldown(request.source);
            }

            if (request.maxDelaySeconds <= 0f)
            {
                request.maxDelaySeconds = defaultMaxDelaySeconds;
            }

            return request;
        }

        private static bool IsRequestValid(CatActionRequest request)
        {
            return request.state != CatBehaviorState.None &&
                request.state != CatBehaviorState.Roam &&
                request.state != CatBehaviorState.FocusedRoam;
        }

        private bool IsOnCooldown(CatActionRequest request)
        {
            float lastTime;
            if (!lastStartedAt.TryGetValue(request.state, out lastTime))
            {
                return false;
            }

            return Time.time - lastTime < request.cooldownSeconds;
        }

        private bool ShouldQueue(CatActionRequest request)
        {
            return request.interruptPolicy == CatActionInterruptPolicy.QueueIfMoving ||
                request.interruptPolicy == CatActionInterruptPolicy.ReplaceAmbient;
        }

        private void QueueOrReplace(CatActionRequest request)
        {
            if (!hasPendingRequest || request.priority >= pendingRequest.priority ||
                pendingRequest.source == CatActionSource.Ambient)
            {
                pendingRequest = request;
                hasPendingRequest = true;
                LastDecision = "queue_" + request.state;
                return;
            }

            LastDecision = "drop_lower_priority_" + request.state;
        }

        private void MarkStarted(CatActionRequest request)
        {
            lastStartedAt[request.state] = Time.time;
            LastAcceptedAction = request.state;
            LastAcceptedSource = request.source;
            LastAcceptedReason = request.reason;
            LastDecision = "play_" + request.state + "_from_" + request.source;
        }

        private float GetDefaultCooldown(CatActionSource source)
        {
            switch (source)
            {
                case CatActionSource.User:
                    return userCooldownSeconds;
                case CatActionSource.Ui:
                    return uiCooldownSeconds;
                case CatActionSource.Session:
                    return sessionCooldownSeconds;
                case CatActionSource.Recognition:
                    return recognitionCooldownSeconds;
                default:
                    return ambientCooldownSeconds;
            }
        }

        private static int GetDefaultPriority(CatActionSource source)
        {
            switch (source)
            {
                case CatActionSource.System:
                    return 100;
                case CatActionSource.Session:
                    return 90;
                case CatActionSource.User:
                    return 70;
                case CatActionSource.Ui:
                    return 60;
                case CatActionSource.Recognition:
                    return 50;
                case CatActionSource.Llm:
                    return 30;
                default:
                    return 10;
            }
        }
    }
}
