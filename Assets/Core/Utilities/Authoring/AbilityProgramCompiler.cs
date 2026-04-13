using System;
using System.Collections.Generic;
using Archeus.Battle.VM.Programs;
using Archeus.Battle.Events.Definitions;

public static class AbilityProgramCompiler
{
    public static List<InstructionDefinition> Compile(string source)
    {
        var lines = source.Split('\n');

        Dictionary<string, int> labels = new();
        List<string> instructionLines = new();

        // PASS 1: collect labels
        int instructionIndex = 0;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (string.IsNullOrEmpty(line))
                continue;

            if (line.EndsWith(":"))
            {
                string label = line.TrimEnd(':');
                labels[label] = instructionIndex;
            }
            else
            {
                instructionLines.Add(line);
                instructionIndex++;
            }
        }

        // PASS 2: build instructions
        List<InstructionDefinition> instructions = new();

        foreach (var line in instructionLines)
        {
            var parts = line.Split(' ');

            var opcode = Enum.Parse<AbilityOpcode>(parts[0]);

            int A = 0;

            if (parts.Length > 1)
            {
                A = ParseArgument(opcode, parts[1], labels);
            }
            instructions.Add(new InstructionDefinition
            {
                Opcode = opcode,
                A = A
            });
        }

        return instructions;
    }

    private static int ParseArgument(AbilityOpcode opcode, string arg, Dictionary<string, int> labels)
    {
        // labels always win
        if (labels.ContainsKey(arg))
            return labels[arg];

        switch (opcode)
        {
            // 🔵 FLOAT OPCODES
            case AbilityOpcode.PushConst:
            {
                if (!float.TryParse(arg, out var f))
                    throw new Exception($"PUSH_CONST requires float: {arg}");

                return VMEncoding.EncodeFloat(f);
            }

            // 🔵 ENUM OPCODES
            case AbilityOpcode.PushEventValue:
            case AbilityOpcode.ModifyEventValue:
            {
                if (!Enum.TryParse<EventValueType>(arg, out var ev))
                    throw new Exception($"Invalid EventValueType: {arg}");

                return (int)ev;
            }

            // 🔵 INT OPCODES
            case AbilityOpcode.LoadState:
            case AbilityOpcode.StoreState:
            case AbilityOpcode.Jump:
            case AbilityOpcode.JumpIfTrue:
            case AbilityOpcode.JumpIfFalse:
            {
                if (!int.TryParse(arg, out var i))
                    throw new Exception($"Opcode {opcode} requires int: {arg}");

                return i;
            }

            // 🔵 DEFAULT (no arg or unused)
            default:
                return 0;
        }
    }
}