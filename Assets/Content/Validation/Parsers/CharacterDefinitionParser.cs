
using System.Collections.Generic;
using Archeus.Core.Debugging;
public static class CharacterDefinitionParser
{
    public static List<CharacterDefinition> FilterValidCharacterDefinitions(CharacterDefinition[] characterDefs)
    {
        if (characterDefs == null || characterDefs.Length == 0) {return new List<CharacterDefinition>(0);}
        List<CharacterDefinition> validDefs = new List<CharacterDefinition>(characterDefs.Length);

        foreach (CharacterDefinition def in characterDefs)
        {
            if (def == null)
            {
                Logging.Warn(LogCategory.System,"Asset is null and will not be baked or considered.");
                continue;
            }

            if (validDefs.Contains(def))
            {
                Logging.Warn(LogCategory.System,def + " already exists in the definition list and will not be baked or considered.");
                continue;
            }

            var ctx = new ValidationContext(def);
            CharacterDefinitionValidator.Validate(def, ctx);

            if (ctx.HasWarnings)
            {
                Logging.Warn(LogCategory.System,def + " contains errors and will not be baked or considered.");
                continue;
            }

            validDefs.Add(def);
        }

        return validDefs;
    }
}
