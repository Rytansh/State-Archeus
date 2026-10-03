using Archeus.Battle.Presentation.Recipes;
using UnityEngine;

namespace Archeus.Battle.Presentation
{
    public sealed class PresentationTemporaryRecipeHolder : MonoBehaviour
    {
        [SerializeField]
        private PresentationRecipe recipe;

        public PresentationRecipe Recipe => recipe;
    }
}
