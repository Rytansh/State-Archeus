using Unity.Entities;
using System;
using DBUS.Battle.VM.Data;

namespace DBUS.Battle.VM.Systems
{
    public static class AbilityInterpreter
    {
        private const int MAX_VM_STEPS = 256;
        public static void Execute(ref AbilityExecutionFrame frame, ref AbilityExecutionContext context)
        {
            int safetyCounter = 0;
            ref var program = ref context.ContentRegistry.Value.AbilityPrograms[frame.ProgramIndex];

            while (frame.InstructionPointer < program.Instructions.Length)
            {
                if (++safetyCounter > MAX_VM_STEPS)
                {
                    Logging.Warning("VM exceeded maximum instruction count.");
                    return;
                }

                ref var instruction = ref program.Instructions[frame.InstructionPointer];

                switch (instruction.Opcode)
                {
                    // VALUE INTRODUCTION OPCODES //
                    case AbilityOpcode.PushConst:
                    {
                        float value = BitConverter.Int32BitsToSingle(instruction.A);
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

                        Push(ref frame, a >= b ? 1f : 0f);
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

                        Logging.System($"VM emitted DamageRequested {frame.Source.Index} → {frame.Target.Index} with AttackMultiplier " + multiplier);
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
                        break;
                    }

                    // DEFAULT
                    default:
                    {
                        Logging.Warning($"Unknown opcode {instruction.Opcode}. Cancelling execution.");
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
                Logging.Warning("VM stack overflow.");
                return;
            }
            frame.Stack.Add(value);
        }

        private static float Pop(ref AbilityExecutionFrame frame)
        {
            if (frame.Stack.Length == 0)
            {
                Logging.Warning("VM stack underflow.");
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
                Logging.Warning("VM stack underflow.");
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
