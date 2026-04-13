using UnityEngine;

public static class CharacterDefinitionValidator
{
    public static void Validate(CharacterDefinition definition, ValidationContext validator)
    {
        ValidationTools.RequireNotNullOrEmpty(validator, definition.ID, "ID");
        ValidationTools.RequireNotNullOrEmpty(validator, definition.Name, "Name");

        ValidationTools.RequireEnumDefined(validator, definition.Rarity, "Rarity");
        ValidationTools.RequireEnumDefined(validator, definition.CharacterType, "Character Type");
        ValidationTools.RequireEnumDefined(validator, definition.BattleType, "Battle Type");
        ValidationTools.RequireEnumDefined(validator, definition.Speciality, "Speciality");
        
        ValidationTools.RequirePositive(validator, definition.CharacterBaseStats.MaxHealth, "Character HP");
        ValidationTools.RequireNonNegative(validator, definition.CharacterBaseStats.Attack, "Character ATK");
        ValidationTools.RequireNonNegative(validator, definition.CharacterBaseStats.Defense, "Character DEF");
        ValidationTools.RequireInRange(validator, definition.CharacterBaseStats.CritRATE, 0, 100, "Crit Rate");
        if(definition.CharacterBaseStats.CritRATE < 0) { validator.Warning("Crit Rate out of acceptable bounds.");}
        ValidationTools.RequireNonNegative(validator, definition.CharacterBaseStats.CritDMG, "Crit DMG");
        ValidationTools.RequireNonNegative(validator, definition.CharacterBaseStats.Reactivity, "Reactivity");
        ValidationTools.RequireNonNegative(validator, definition.CharacterBaseStats.SustainPOWER, "Sustain Power");
        ValidationTools.RequireNonNegative(validator, definition.CharacterBaseStats.MagicalDMGBonus, "Magical DMG Bonus");
        ValidationTools.RequireNonNegative(validator, definition.CharacterBaseStats.FlexibleDMGBonus, "Flexible DMG Bonus");
        ValidationTools.RequireNonNegative(validator, definition.CharacterBaseStats.TacticalDMGBonus, "Tactical DMG Bonus");
        ValidationTools.RequireNonNegative(validator, definition.CharacterBaseStats.PowerfulDMGBonus, "Powerful DMG Bonus");
        ValidationTools.RequireNonNegative(validator, definition.CharacterBaseStats.ALLDMGBonus, "All DMG Bonus");
        ValidationTools.RequireNonNegative(validator, definition.CharacterBaseStats.DOTDMGBonus, "DOT DMG Bonus");
    }
}
