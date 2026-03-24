using UnityEngine;
using System.Collections.Generic;
public static class BehaviourDefinitionParser
{
    public static List<BehaviourDefinition> FilterValidBehaviourDefinitions(BehaviourDefinition[] BehaviourDefs)
    {
        if (BehaviourDefs == null || BehaviourDefs.Length == 0) {return new List<BehaviourDefinition>(0);}
        List<BehaviourDefinition> validDefs = new List<BehaviourDefinition>(BehaviourDefs.Length);

        foreach (BehaviourDefinition def in BehaviourDefs)
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
