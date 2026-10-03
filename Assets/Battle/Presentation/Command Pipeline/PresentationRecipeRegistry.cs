using System.Collections.Generic;
using Archeus.Battle.Presentation.Recipes;

namespace Archeus.Battle.Presentation
{
    public sealed class PresentationRecipeRegistry
    {
        private readonly Dictionary<uint, PresentationRecipe> recipes = new();

        public bool Register(uint actionDefinitionID, PresentationRecipe recipe)
        {
            if (recipe == null)
                return false;

            return recipes.TryAdd(actionDefinitionID, recipe);
        }

        public bool TryGet(uint actionDefinitionID, out PresentationRecipe recipe)
        {
            return recipes.TryGetValue(actionDefinitionID, out recipe);
        }

        public void Clear()
        {
            recipes.Clear();
        }
    }
}
