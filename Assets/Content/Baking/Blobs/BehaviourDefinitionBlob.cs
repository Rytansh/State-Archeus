using Unity.Entities;
using Archeus.Battle.Events.Definitions;
using Archeus.Content.Lookup;

namespace Archeus.Content.Blobs
{
    public struct BehaviourDefinitionBlob : IHasID
    {
        public uint ID;
        public uint GetID() => ID;
        public BlobArray<BehaviourTriggerBlob> Triggers;
    }

    public struct BehaviourTriggerBlob
    {
        public BattleEventType EventType;
        public BattleEventPhase Phase;
        public int VMProgramIndex;
        public BlobArray<EventConditionBlob> Conditions;
        public int Priority;
    }

    public struct EventConditionBlob
    {
        public ConditionType Type;
        public ConditionTarget Target;
        public float Value;
    }
}
