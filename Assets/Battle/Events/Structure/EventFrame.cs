using DBUS.Battle.Components.Events;

public struct EventFrame
{
    public BattleEvent Event;
    public BattleEventPhase Phase;
    public bool PhaseStarted;
}