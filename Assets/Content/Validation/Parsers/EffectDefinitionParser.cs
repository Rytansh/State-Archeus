using System.Collections.Generic;
using Archeus.Core.Debugging;
public static class EffectDefinitionParser
{
    public static List<EffectDefinition> FilterValidEffectDefinitions(EffectDefinition[] effectDefs)
    {
        if (effectDefs == null || effectDefs.Length == 0) {return new List<EffectDefinition>(0);}
        List<EffectDefinition> validDefs = new List<EffectDefinition>(effectDefs.Length);

        foreach (EffectDefinition def in effectDefs)
        {
            if (def == null)
            {
                Logging.Warn(LogCategory.System, "Asset is null and will not be baked or considered.");
                continue;
            }

            if (validDefs.Contains(def))
            {
                Logging.Warn(LogCategory.System, def + " already exists in the definition list and will not be baked or considered.");
                continue;
            }

            validDefs.Add(def);
        }

        return validDefs;
    }
}
