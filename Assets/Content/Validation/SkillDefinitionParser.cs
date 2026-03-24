using UnityEngine;
using System.Collections.Generic;
public static class SkillDefinitionParser
{
    public static List<SkillDefinition> FilterValidSkillDefinitions(SkillDefinition[] skillDefs)
    {
        if (skillDefs == null || skillDefs.Length == 0) {return new List<SkillDefinition>(0);}
        List<SkillDefinition> validDefs = new List<SkillDefinition>(skillDefs.Length);

        foreach (SkillDefinition def in skillDefs)
        {
            if (def == null)
            {
                Logging.Error(def + " is null and will not be baked or considered.");
                continue;
            }

            var ctx = new ValidationContext(def);
            SkillDefinitionValidator.Validate(def, ctx);

            if (ctx.HasErrors)
            {
                Logging.Error(def + " contains errors and will not be baked or considered.");
                continue;
            }

            validDefs.Add(def);
        }

        return validDefs;
    }
}
