using UnityEngine;
using System.Collections.Generic;
public static class AbilityProgramDefinitionParser
{
    public static List<AbilityProgramDefinition> FilterValidAbilityProgramDefinitions(AbilityProgramDefinition[] AbilityProgramDefs)
    {
        if (AbilityProgramDefs == null || AbilityProgramDefs.Length == 0) {return new List<AbilityProgramDefinition>(0);}
        List<AbilityProgramDefinition> validDefs = new List<AbilityProgramDefinition>(AbilityProgramDefs.Length);

        foreach (AbilityProgramDefinition def in AbilityProgramDefs)
        {
            if (def == null)
            {
                Logging.Warning("Asset is null and will not be baked or considered.");
                continue;
            }

            if (validDefs.Contains(def))
            {
                Logging.Warning(def + " already exists in the definition list and will not be baked or considered.");
                continue;
            }

            validDefs.Add(def);
        }

        return validDefs;
    }
}
