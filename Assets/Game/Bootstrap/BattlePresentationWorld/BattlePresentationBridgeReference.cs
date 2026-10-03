using Archeus.Game.Bootstrap;
using Unity.Entities;

public sealed class BattlePresentationBridgeReference : IComponentData
{
    public BattlePresentationBridge Bridge;

    public BattlePresentationBridgeReference() { }
}
