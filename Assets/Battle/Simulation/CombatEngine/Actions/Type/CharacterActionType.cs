namespace Archeus.Battle.Data.Actions
{
    public enum CharacterActionType : byte
    {
        None = 0,

        NormalAttack,
        SuperchargedAttack,
        FinalTrumpSkill,
        TrichicSkill,

        Transformation,

        AdditionalAttack,
        CounterAttack,
        FollowUpAttack,
    }
}
