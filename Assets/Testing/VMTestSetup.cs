using Unity.Collections;
using Unity.Entities;
using DBUS.Battle.VM.Data;
using DBUS.Battle.Components.Events;
using DBUS.Battle.VM.Systems;

public static class VMTestSetup
{
    public static void Init()
    {
        using var builder = new BlobBuilder(Allocator.Temp);

        ref var program = ref builder.ConstructRoot<AbilityProgram>();

        var instructions = builder.Allocate(ref program.Instructions, 2);

        instructions[0] = new AbilityInstruction
        {
            Opcode = AbilityOpcode.PushConst,
            A = System.BitConverter.SingleToInt32Bits(1.0f)
        };

        instructions[1] = new AbilityInstruction
        {
            Opcode = AbilityOpcode.DealDamage
        };

        var blob = builder.CreateBlobAssetReference<AbilityProgram>(Allocator.Persistent);

        // Register to BehaviourIndex 0, TriggerIndex 0
        AbilityProgramRegistry.Register(0, 0, blob);
        Logging.System("registered");
    }
}
