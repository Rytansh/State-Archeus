using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Content.Blobs;
using Unity.Entities;

public static class CardEntityBuilder
{
    public static Entity CreateCharacterEntity(
        EntityCommandBuffer ecb,
        in CardEntityComponents components,
        ref CharacterDefinitionBlob definition
    )
    {
        Entity character = ecb.CreateEntity();

        AddCommonComponentsToCardEntity(ecb, character, components);

        ecb.AddComponent<CharacterTag>(character);

        ecb.AddComponent(
            character,
            new CharacterRuntimeStatBaseline
            {
                Attack = definition.CharacterBlobBaseStats.Attack,
                Defense = definition.CharacterBlobBaseStats.Defense,
                MaxHealth = definition.CharacterBlobBaseStats.MaxHealth,

                CritRATE = definition.CharacterBlobBaseStats.CritRATE,
                CritDMG = definition.CharacterBlobBaseStats.CritDMG,

                DOTDMGBonus = definition.CharacterBlobBaseStats.DOTDMGBonus,
                Authority = definition.CharacterBlobBaseStats.Authority,
                Initiative = definition.CharacterBlobBaseStats.Initiative,
                Stability = definition.CharacterBlobBaseStats.Stability,
                Observation = definition.CharacterBlobBaseStats.Observation,
                Reactivity = definition.CharacterBlobBaseStats.Reactivity,

                ALLDMGBonus = definition.CharacterBlobBaseStats.ALLDMGBonus,
            }
        );

        ecb.AddComponent(
            character,
            new CurrentHealth { Value = definition.CharacterBlobBaseStats.MaxHealth }
        );

        DynamicBuffer<BehaviourReference> behaviours = ecb.AddBuffer<BehaviourReference>(character);

        DynamicBuffer<BehaviourRuntimeState> runtimeStates = ecb.AddBuffer<BehaviourRuntimeState>(
            character
        );

        ecb.AddBuffer<ActiveEffect>(character);

        for (int i = 0; i < definition.BehaviourIndices.Length; i++)
        {
            behaviours.Add(
                new BehaviourReference { BehaviourIndex = definition.BehaviourIndices[i] }
            );

            runtimeStates.Add(new BehaviourRuntimeState { Memory = default });
        }

        return character;
    }

    public static Entity CreateSkillEntity(
        EntityCommandBuffer ecb,
        in CardEntityComponents components,
        ref SkillDefinitionBlob definition
    )
    {
        Entity skill = ecb.CreateEntity();

        AddCommonComponentsToCardEntity(ecb, skill, components);

        ecb.AddComponent<SkillTag>(skill);

        ecb.AddComponent(
            skill,
            new SkillRuntimeStatBaseline
            {
                Attack = definition.SkillBlobBaseStats.Attack,
                Defense = definition.SkillBlobBaseStats.Defense,
                Health = definition.SkillBlobBaseStats.Health,
            }
        );

        return skill;
    }

    private static void AddCommonComponentsToCardEntity(
        EntityCommandBuffer ecb,
        Entity card,
        in CardEntityComponents components
    )
    {
        ecb.AddComponent(card, new CardDefinitionID { Value = components.DefinitionID });
        ecb.AddComponent(card, new CardRuntimeID { Value = components.RuntimeID });
        ecb.AddComponent(card, new CardOwner { Player = components.Owner });
        ecb.AddComponent(card, new OwnedBattle { Battle = components.Battle });
        ecb.AddComponent(card, components.SideAssignment);
    }
}
