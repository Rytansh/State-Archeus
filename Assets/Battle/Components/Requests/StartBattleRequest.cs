using Unity.Entities;

namespace DBUS.Battle.Components.Requests
{
    public struct StartBattleRequest: IComponentData
    {
        public ulong BattleID;
        public ulong BattleSeed;
        public ulong BattleConfigID;
    }

}
