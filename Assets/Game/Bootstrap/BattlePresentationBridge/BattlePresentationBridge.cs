using System;
using System.Collections.Generic;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Presentation.State;

namespace Archeus.Game.Bootstrap
{
    public sealed class BattlePresentationBridge
    {
        // ============================================================
        // FACT STREAM
        // ============================================================

        private readonly Queue<PresentationFact> pendingFacts = new();

        public int PendingFactCount => pendingFacts.Count;

        public void Publish(in PresentationFact fact)
        {
            pendingFacts.Enqueue(fact);
        }

        public bool TryConsume(out PresentationFact fact)
        {
            if (pendingFacts.Count > 0)
            {
                fact = pendingFacts.Dequeue();
                return true;
            }

            fact = default;
            return false;
        }

        // ============================================================
        // FULL STATE SNAPSHOT
        // ============================================================

        private PresentationStateSnapshot latestStateSnapshot;

        public void PublishStateSnapshot(PresentationStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            latestStateSnapshot = snapshot;
        }

        public bool TryTakeLatestStateSnapshot(out PresentationStateSnapshot snapshot)
        {
            snapshot = latestStateSnapshot;

            if (snapshot == null)
            {
                return false;
            }

            latestStateSnapshot = null;

            return true;
        }

        // ============================================================
        // ORDERED STATE UPDATE STREAM
        // ============================================================

        private readonly Queue<PresentationStateUpdate> pendingStateUpdates = new();

        public int PendingStateUpdateCount => pendingStateUpdates.Count;

        public bool TryConsumeStateUpdate(out PresentationStateUpdate update)
        {
            if (pendingStateUpdates.Count > 0)
            {
                update = pendingStateUpdates.Dequeue();

                return true;
            }

            update = null;
            return false;
        }

        // ============================================================
        // STATE RECOVERY REQUESTS
        // ============================================================

        private readonly Queue<ulong> pendingStateRecoveryRequests = new();
        private readonly HashSet<ulong> queuedStateRecoveryRequests = new();

        public int PendingStateRecoveryRequestCount => pendingStateRecoveryRequests.Count;

        public void RequestStateRecovery(ulong battleRuntimeID)
        {
            if (!queuedStateRecoveryRequests.Add(battleRuntimeID))
            {
                return;
            }

            pendingStateRecoveryRequests.Enqueue(battleRuntimeID);
        }

        public bool TryTakeStateRecoveryRequest(out ulong battleRuntimeID)
        {
            if (pendingStateRecoveryRequests.Count == 0)
            {
                battleRuntimeID = default;
                return false;
            }

            battleRuntimeID = pendingStateRecoveryRequests.Dequeue();

            queuedStateRecoveryRequests.Remove(battleRuntimeID);

            return true;
        }

        public void RequeueStateRecoveryRequest(ulong battleRuntimeID)
        {
            RequestStateRecovery(battleRuntimeID);
        }

        public void PublishRecoveryStateSnapshot(PresentationStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            /*
             * The recovery snapshot completely supersedes
             * previously queued state deltas.
             */
            pendingStateUpdates.Clear();

            latestStateSnapshot = snapshot;
        }

        // ============================================================
        // TYPED PUBLICATION API
        // ============================================================

        public void PublishBattleStateUpdate(
            ulong battleRuntimeID,
            ulong revision,
            in BattlePresentationState state
        )
        {
            pendingStateUpdates.Enqueue(
                new BattlePresentationStateUpdate(battleRuntimeID, revision, in state)
            );
        }

        public void PublishCharacterStateUpdate(
            ulong battleRuntimeID,
            ulong revision,
            in CharacterPresentationState state
        )
        {
            pendingStateUpdates.Enqueue(
                new CharacterPresentationStateUpdate(battleRuntimeID, revision, in state)
            );
        }

        public void PublishCardStateUpdate(
            ulong battleRuntimeID,
            ulong revision,
            in CardPresentationState state
        )
        {
            pendingStateUpdates.Enqueue(
                new CardPresentationStateUpdate(battleRuntimeID, revision, in state)
            );
        }

        public void PublishHandStateUpdate(
            ulong battleRuntimeID,
            ulong revision,
            in HandPresentationState state
        )
        {
            pendingStateUpdates.Enqueue(
                new HandPresentationStateUpdate(battleRuntimeID, revision, in state)
            );
        }

        public void PublishPlayerStateUpdate(
            ulong battleRuntimeID,
            ulong revision,
            in PlayerPresentationState state
        )
        {
            pendingStateUpdates.Enqueue(
                new PlayerPresentationStateUpdate(battleRuntimeID, revision, in state)
            );
        }

        // ============================================================
        // LIFECYCLE
        // ============================================================

        public void Clear()
        {
            pendingFacts.Clear();

            latestStateSnapshot = null;

            pendingStateUpdates.Clear();

            pendingStateRecoveryRequests.Clear();
            queuedStateRecoveryRequests.Clear();
        }
    }
}
