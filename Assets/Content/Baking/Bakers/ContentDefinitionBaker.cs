using Unity.Collections;
using Unity.Entities;
using System.Collections.Generic;
using Archeus.Content.Blobs;
using Archeus.Content.Registries;
using Archeus.Battle.VM.Programs;
using Archeus.Core.Debugging;

public class ContentDefinitionBaker : Baker<ContentDefinitionAuthoring>
{
    public override void Bake(ContentDefinitionAuthoring authoring)
    {
        List<CharacterDefinition> validCharacterDefinitions = CharacterDefinitionParser.FilterValidCharacterDefinitions(authoring.CharacterDefinitions);
        List<SkillDefinition> validSkillDefinitions = SkillDefinitionParser.FilterValidSkillDefinitions(authoring.SkillDefinitions);
        List<BehaviourDefinition> validBehaviourDefinitions = BehaviourDefinitionParser.FilterValidBehaviourDefinitions(authoring.BehaviourDefinitions);
        List<AbilityProgramDefinition> validAbilityProgramDefinitions = AbilityProgramDefinitionParser.FilterValidAbilityProgramDefinitions(authoring.AbilityProgramDefinitions);
        Entity ContentRegistryEntity = GetEntity(TransformUsageFlags.None);

        var programIDToIndex = new Dictionary<uint, int>();

        for (int i = 0; i < validAbilityProgramDefinitions.Count; i++)
        {
            uint id = StableHash32.HashFromString(validAbilityProgramDefinitions[i].ID);
            programIDToIndex[id] = i;
        }

        using (BlobBuilder builder = new BlobBuilder(Allocator.Temp))
        {
            ref ContentBlobRegistry root = ref builder.ConstructRoot<ContentBlobRegistry>();
            BlobBuilderArray<CharacterDefinitionBlob> charactersToBake = builder.Allocate(ref root.Characters, validCharacterDefinitions.Count);
            BlobBuilderArray<SkillDefinitionBlob> skillsToBake = builder.Allocate(ref root.Skills, validSkillDefinitions.Count);
            BlobBuilderArray<BehaviourDefinitionBlob> behavioursToBake = builder.Allocate(ref root.Behaviours, validBehaviourDefinitions.Count);
            BlobBuilderArray<AbilityProgram> programsToBake = builder.Allocate(ref root.AbilityPrograms, validAbilityProgramDefinitions.Count);

            BakeAllCharacters(validCharacterDefinitions, ref charactersToBake, builder);
            BakeAllSkills(validSkillDefinitions, ref skillsToBake, builder);
            BakeAllBehaviours(validBehaviourDefinitions, ref behavioursToBake, builder, programIDToIndex);
            BakeAllPrograms(validAbilityProgramDefinitions, ref programsToBake, builder);

            BlobAssetReference<ContentBlobRegistry> registryReference = builder.CreateBlobAssetReference<ContentBlobRegistry>(Allocator.Persistent);
            AddBlobAsset(ref registryReference, out Unity.Entities.Hash128 blobHash);

            AddComponent(ContentRegistryEntity, new ContentBlobRegistryComponent{BlobRegistryReference = registryReference});
        }
    }
    private static void BakeAllCharacters(List<CharacterDefinition> characterDefs, ref BlobBuilderArray<CharacterDefinitionBlob> outputArray, BlobBuilder builder)
    {
        for (int i = 0; i < characterDefs.Count; i++)
        {
            WriteCharacter(ref outputArray[i], characterDefs[i], ref builder);
            Logging.Info(LogCategory.System, "Baked character " + characterDefs[i].Name + " successfully.");
        }
    }

    private static void BakeAllSkills(List<SkillDefinition> skillDefs, ref BlobBuilderArray<SkillDefinitionBlob> outputArray, BlobBuilder builder)
    {
        for (int i = 0; i < skillDefs.Count; i++)
        {
            WriteSkill(ref outputArray[i], skillDefs[i], ref builder);
            Logging.Info(LogCategory.System, "Baked skill " + skillDefs[i].Name + " successfully.");
        }
    }

    private static void BakeAllBehaviours(List<BehaviourDefinition> behaviourDefs,ref BlobBuilderArray<BehaviourDefinitionBlob> outputArray,BlobBuilder builder,Dictionary<uint, int> programMap)
    { 
        for (int i = 0; i < behaviourDefs.Count; i++) 
        { 
            WriteBehaviour(ref outputArray[i], behaviourDefs[i], ref builder, programMap); 
            Logging.Info(LogCategory.System,"Baked behaviour " + behaviourDefs[i].name + " successfully."); 
        } 
    }

    private static void BakeAllPrograms(List<AbilityProgramDefinition> programDefs, ref BlobBuilderArray<AbilityProgram> outputArray, BlobBuilder builder)
    {
        for (int i = 0; i < programDefs.Count; i++)
        {
            WriteProgram(ref outputArray[i], programDefs[i], ref builder);
            Logging.Info(LogCategory.System,"Baked behaviour " + programDefs[i].name + " successfully."); 
        }
    }

    //########### Writers ###########//
    private static void WriteCharacter(ref CharacterDefinitionBlob blob, CharacterDefinition def, ref BlobBuilder builder)
    {
        blob.ID = StableHash32.HashFromString(def.ID);
        blob.Rarity = (byte)def.Rarity;
        blob.BattleType = (byte)def.BattleType;
        blob.CharacterType = (byte)def.CharacterType;
        blob.Speciality = (byte)def.Speciality;

        ref var stats = ref blob.CharacterBlobBaseStats;

        stats.MaxHealth = def.CharacterBaseStats.MaxHealth;
        stats.Attack = def.CharacterBaseStats.Attack;
        stats.Defense = def.CharacterBaseStats.Defense;
        stats.CritDMG = def.CharacterBaseStats.CritDMG;
        stats.CritRATE = def.CharacterBaseStats.CritRATE;
        stats.Reactivity = def.CharacterBaseStats.Reactivity;
        stats.SustainPOWER = def.CharacterBaseStats.SustainPOWER;
        stats.MagicalDMGBonus = def.CharacterBaseStats.MagicalDMGBonus;
        stats.PowerfulDMGBonus = def.CharacterBaseStats.PowerfulDMGBonus;
        stats.TacticalDMGBonus = def.CharacterBaseStats.TacticalDMGBonus;
        stats.FlexibleDMGBonus = def.CharacterBaseStats.FlexibleDMGBonus;
        stats.DOTDMGBonus = def.CharacterBaseStats.DOTDMGBonus;
        stats.ALLDMGBonus = def.CharacterBaseStats.ALLDMGBonus;

        blob.NormalAttackID = StableHash32.HashFromString(def.NormalAttackID);
        blob.SuperchargedAttackID = StableHash32.HashFromString(def.SuperchargedAttackID);
        blob.FinalTrumpSkillID = StableHash32.HashFromString(def.FinalTrumpSkillID);

        var behaviourIds = builder.Allocate(ref blob.BehaviourIDs, def.BehaviourIDs.Count);
        for (int i = 0; i < def.BehaviourIDs.Count; i++){behaviourIds[i] = StableHash32.HashFromString(def.BehaviourIDs[i]);}
        
    }

    private static void WriteSkill(ref SkillDefinitionBlob blob, SkillDefinition def, ref BlobBuilder builder)
    {
        blob.ID = StableHash32.HashFromString(def.ID);
        blob.Rarity = (byte)def.Rarity;
        blob.Speciality = (byte)def.Speciality;

        blob.Duration = def.Duration;

        blob.SkillBlobBaseStats.Attack = def.SkillBaseStats.Attack;
        blob.SkillBlobBaseStats.Defense = def.SkillBaseStats.Defense;
        blob.SkillBlobBaseStats.Health  = def.SkillBaseStats.Health;

        blob.NormalAbilityID = StableHash32.HashFromString(def.NormalAbilityID);
        blob.DelayAndImprovementAbilityID = StableHash32.HashFromString(def.DelayAndImprovementAbilityID);
        var behaviourIds = builder.Allocate(ref blob.BehaviourIDs, def.BehaviourIDs.Count);
        for (int i = 0; i < def.BehaviourIDs.Count; i++){behaviourIds[i] = StableHash32.HashFromString(def.BehaviourIDs[i]);}
    }

    private static void WriteBehaviour(ref BehaviourDefinitionBlob blob, BehaviourDefinition def, ref BlobBuilder builder, Dictionary<uint, int> programMap)
    { 
        blob.ID = StableHash32.HashFromString(def.ID); 
        var triggers = builder.Allocate(ref blob.Triggers, def.Triggers.Count); 
        for (int i = 0; i < def.Triggers.Count; i++) 
        { 
            var trigger = def.Triggers[i]; 
            ref var blobListener = ref triggers[i]; 
            blobListener.EventType = trigger.EventType; 
            blobListener.Phase = trigger.Phase;
            uint programID = StableHash32.HashFromString(trigger.VMProgramID);
            if (!programMap.TryGetValue(programID, out int programIndex))
            {
                Logging.Error(LogCategory.System, $"Program not found for ID {trigger.VMProgramID}");
                programIndex = -1;
            }
            blobListener.VMProgramIndex = programIndex;
            blobListener.Priority = trigger.Priority; 

            var conditions = builder.Allocate( ref blobListener.Conditions, trigger.Conditions.Count ); 
            for (int j = 0; j < trigger.Conditions.Count; j++) 
            { 
                var defCondition = trigger.Conditions[j]; 
                conditions[j] = new EventConditionBlob 
                { 
                    Type = defCondition.Type, 
                    Target = defCondition.Target,
                    Value = defCondition.Value 
                }; 
            } 
        } 
    }

    private static void WriteProgram(ref AbilityProgram blob, AbilityProgramDefinition def, ref BlobBuilder builder)
    {
        blob.ID = StableHash32.HashFromString(def.ID);

        var instructions = builder.Allocate(ref blob.Instructions, def.Instructions.Count);

        for (int i = 0; i < def.Instructions.Count; i++)
        {
            var defInstr = def.Instructions[i];

            int a = defInstr.A;
            int b = defInstr.B;


            instructions[i] = new AbilityInstruction
            {
                Opcode = defInstr.Opcode,
                A = a,
                B = b
            };
        }
    }

}
