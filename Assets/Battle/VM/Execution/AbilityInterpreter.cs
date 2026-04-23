using Archeus.Battle.Buffers.Events;
using Archeus.Battle.VM.Programs;
using Archeus.Battle.Events.Definitions;
using Archeus.Battle.Events.Payloads;
using Archeus.Core.Debugging;

namespace Archeus.Battle.VM.Execution
{
    public static class AbilityInterpreter
    {
        private const int MAX_VM_STEPS = 256;
        public static void Execute(ref AbilityExecutionFrame frame, ref AbilityExecutionContext context, ref BattleEvent evt)
        {
            int safetyCounter = 0;
            ref var program = ref context.ContentRegistry.Value.AbilityPrograms[frame.ProgramIndex];

            while (frame.InstructionPointer < program.Instructions.Length)
            {
                if (++safetyCounter > MAX_VM_STEPS)
                {
                    Logging.Info(LogCategory.VM, "VM exceeded maximum instruction count.");
                    return;
                }

                ref var instruction = ref program.Instructions[frame.InstructionPointer];

                switch (instruction.Opcode)
                {
                    // VALUE INTRODUCTION OPCODES //
                    case AbilityOpcode.PushConst:
                    {
                        float value = VMEncoding.DecodeFloat(instruction.A);
                        Push(ref frame, value);
                        break;
                    }
                    case AbilityOpcode.PushStat:
                    {
                        var stats = context.CharacterStatsLookup[frame.Source];
                        float value = instruction.A switch
                        {
                            0 => stats.Attack,
                            1 => stats.Defense,
                            2 => stats.MaxHealth,
                            _ => 0
                        };
                        Push(ref frame, value);
                        break;
                    }
                    case AbilityOpcode.PushEventValue:
                    {
                        var type = (EventValueType)instruction.A;

                        float value = type switch
                        {
                            EventValueType.DamageBase => evt.Payload.Damage.BaseDamage,
                            EventValueType.DamageFinal => evt.Payload.Damage.FinalDamage,
                            EventValueType.DamageMultiplier => evt.Payload.Damage.AttackMultiplier,
                            _ => 0f
                        };

                        Push(ref frame, value);
                        break;
                    }

                    case AbilityOpcode.LoadState:
                    {
                        int index = instruction.A;

                        var state = context.StateBuffer[context.StateIndex];

                        float value = (index < state.Memory.Length) ? state.Memory[index] : 0f;

                        Push(ref frame, value);
                        break;
                    }

                    case AbilityOpcode.StoreState:
                    {
                        int index = instruction.A;
                        float value = Pop(ref frame);

                        var state = context.StateBuffer[context.StateIndex];

                        while (state.Memory.Length <= index)
                        {
                            state.Memory.Add(0f);
                        }

                        state.Memory[index] = value;
                        context.StateBuffer[context.StateIndex] = state;

                        break;
                    }

                    case AbilityOpcode.ModifyEventValue:
                    {
                        var type = (EventValueType)instruction.A;

                        float value = Pop(ref frame);

                        switch (type)
                        {
                            case EventValueType.DamageFinal:
                                evt.Payload.Damage.FinalDamage = value;
                                break;

                            case EventValueType.DamageBase:
                                evt.Payload.Damage.BaseDamage = value;
                                break;

                            case EventValueType.DamageMultiplier:
                                evt.Payload.Damage.AttackMultiplier = value;
                                break;
                        }

                        break;
                    }

                    // MATH OPERATION OPCODES //
                    case AbilityOpcode.Add:
                    {
                        float b = Pop(ref frame);
                        float a = Pop(ref frame);

                        Push(ref frame, a + b);
                        break;
                    }

                    case AbilityOpcode.Sub:
                    {
                        float b = Pop(ref frame);
                        float a = Pop(ref frame);

                        Push(ref frame, a - b);
                        break;
                    }

                    case AbilityOpcode.Mul:
                    {
                        float b = Pop(ref frame);
                        float a = Pop(ref frame);

                        Push(ref frame, a * b);
                        break;
                    }

                    case AbilityOpcode.Div:
                    {
                        float b = Pop(ref frame);
                        float a = Pop(ref frame);

                        Push(ref frame, a / b);
                        break;
                    }

                    // COMPARISON OPCODES //
                    case AbilityOpcode.Equal:
                    {
                        float b = Pop(ref frame);
                        float a = Pop(ref frame);

                        Push(ref frame, a == b ? 1f : 0f);
                        break;
                    }

                    case AbilityOpcode.Greater:
                    {
                        float b = Pop(ref frame);
                        float a = Pop(ref frame);

                        Push(ref frame, a > b ? 1f : 0f);
                        break;
                    }

                    case AbilityOpcode.GreaterEqual:
                    {
                        float b = Pop(ref frame);
                        float a = Pop(ref frame);

                        float result = a >= b ? 1f : 0f;

                        Push(ref frame, result);
                        break;
                    }

                    case AbilityOpcode.Less:
                    {
                        float b = Pop(ref frame);
                        float a = Pop(ref frame);

                        Push(ref frame, a < b ? 1f : 0f);
                        break;
                    }

                    case AbilityOpcode.LessEqual:
                    {
                        float b = Pop(ref frame);
                        float a = Pop(ref frame);

                        Push(ref frame, a <= b ? 1f : 0f);
                        break;
                    }

                    // GAMEPLAY OPCODES //

                    case AbilityOpcode.DealDamage:
                    {
                        float multiplier = Pop(ref frame);

                        EmitEvent(ref context, new BattleEvent
                        {
                            Type = BattleEventType.DamageRequested,
                            Scope = BattleEventScope.Targeted,
                            Source = frame.Source,
                            Target = frame.Target,
                            Payload = new EventPayload
                            {
                                Damage = new DamagePayload
                                {
                                    AttackMultiplier = multiplier
                                }
                            }
                        });
                        break;
                    }
                    
                    // VM FLOW OPCODES //
                    case AbilityOpcode.Jump:
                    {
                        frame.InstructionPointer = instruction.A;
                        continue;
                    }

                    case AbilityOpcode.JumpIfFalse:
                    {
                        float cond = Pop(ref frame);
                        if (cond == 0f)
                        {
                            frame.InstructionPointer = instruction.A;
                            continue;
                        }
                        break;
                    }

                    case AbilityOpcode.JumpIfTrue:
                    {
                        float cond = Pop(ref frame);

                        if (cond != 0f)
                        {
                            frame.InstructionPointer = instruction.A;
                            continue;
                        }

                        break;
                    }

                    case AbilityOpcode.End:
                    {
                        return;
                    }

                    // DEFAULT
                    default:
                    {
                        Logging.Warn(LogCategory.VM, $"Unknown opcode {instruction.Opcode}. Cancelling execution.");
                        return;
                    }
                }

                frame.InstructionPointer++;
            }
        }

        private static void Push(ref AbilityExecutionFrame frame, float value)
        {
            if (frame.Stack.Length >= 32)
            {
                Logging.Warn(LogCategory.VM, "VM stack overflow.");
                return;
            }
            frame.Stack.Add(value);
        }

        private static float Pop(ref AbilityExecutionFrame frame)
        {
            if (frame.Stack.Length == 0)
            {
                Logging.Warn(LogCategory.VM, "VM stack underflow.");
                return 0;
            }

            int last = frame.Stack.Length - 1;
            float value = frame.Stack[last];
            frame.Stack.RemoveAt(last);

            return value;
        }

        private static float Peek(ref AbilityExecutionFrame frame)
        {
            if (frame.Stack.Length == 0)
            {
                Logging.Warn(LogCategory.VM, "VM stack underflow.");
                return 0;
            }

            return frame.Stack[frame.Stack.Length - 1];
        }

        private static void EmitEvent(ref AbilityExecutionContext context, BattleEvent evt)
        {
            context.ChainedEventQueue.Add(new ChainedBattleEvent
            {
                Event = evt
            });
        }
    }
}
