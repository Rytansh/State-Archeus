using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Baking;
using UnityEngine;
using System.Collections.Generic;

public class ContentDefinitionBaker : Baker<ContentDefinitionAuthoring>
{
    public override void Bake(ContentDefinitionAuthoring authoring)
    {
        List<CharacterDefinition> validCharacterDefinitions = CharacterDefinitionParser.FilterValidCharacterDefinitions(authoring.CharacterDefinitions);
        List<SkillDefinition> validSkillDefinitions = SkillDefinitionParser.FilterValidSkillDefinitions(authoring.SkillDefinitions);
        List<BehaviourDefinition> validBehaviourDefinitions = BehaviourDefinitionParser.FilterValidBehaviourDefinitions(authoring.BehaviourDefinitions);
        Entity ContentRegistryEntity = GetEntity(TransformUsageFlags.None);

        using (BlobBuilder builder = new BlobBuilder(Allocator.Temp))
        {
            ref ContentBlobRegistry root = ref builder.ConstructRoot<ContentBlobRegistry>();
            BlobBuilderArray<CharacterDefinitionBlob> charactersToBake = builder.Allocate(ref root.Characters, validCharacterDefinitions.Count);
            BlobBuilderArray<SkillDefinitionBlob> skillsToBake = builder.Allocate(ref root.Skills, validSkillDefinitions.Count);
            BlobBuilderArray<BehaviourDefinitionBlob> behavioursToBake = builder.Allocate(ref root.Behaviours, validBehaviourDefinitions.Count);

            BakeAllCharacters(validCharacterDefinitions, ref charactersToBake, builder);
            BakeAllSkills(validSkillDefinitions, ref skillsToBake, builder);
            BakeAllBehaviours(validBehaviourDefinitions, ref behavioursToBake, builder);

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
            Logging.System("Baked character " + characterDefs[i].Name + " successfully.");
        }
    }

    private static void BakeAllSkills(List<SkillDefinition> skillDefs, ref BlobBuilderArray<SkillDefinitionBlob> outputArray, BlobBuilder builder)
    {
        for (int i = 0; i < skillDefs.Count; i++)
        {
            WriteSkill(ref outputArray[i], skillDefs[i], ref builder);
            Logging.System("Baked skill " + skillDefs[i].Name + " successfully.");
        }
    }

    private static void BakeAllBehaviours(List<BehaviourDefinition> behaviourDefs, ref BlobBuilderArray<BehaviourDefinitionBlob> outputArray, BlobBuilder builder) 
    { 
        for (int i = 0; i < behaviourDefs.Count; i++) 
        { 
            WriteBehaviour(ref outputArray[i], behaviourDefs[i], ref builder); 
            Logging.System("Baked behaviour " + behaviourDefs[i].name + " successfully."); 
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

    private static void WriteBehaviour(ref BehaviourDefinitionBlob blob, BehaviourDefinition def, ref BlobBuilder builder)
    { 
        blob.ID = StableHash32.HashFromString(def.ID); 
        var triggers = builder.Allocate(ref blob.Triggers, def.Triggers.Count); 
        for (int i = 0; i < def.Triggers.Count; i++) 
        { 
            var trigger = def.Triggers[i]; 
            ref var blobListener = ref triggers[i]; 
            blobListener.EventType = trigger.EventType; 
            blobListener.VMProgramID = StableHash32.HashFromString(trigger.VMProgramID); 
            blobListener.Priority = trigger.Priority; 

            var conditions = builder.Allocate( ref blobListener.Conditions, trigger.Conditions.Count ); 
            for (int j = 0; j < trigger.Conditions.Count; j++) 
            { 
                var defCondition = trigger.Conditions[j]; 
                conditions[j] = new EventConditionBlob 
                { 
                    Type = defCondition.Type, 
                    Value = defCondition.Value 
                }; 
            } 
        } 
    }

}
