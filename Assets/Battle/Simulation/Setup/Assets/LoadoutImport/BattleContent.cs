using Archeus.Content.Registries;
using Unity.Entities;

namespace Archeus.Battle.Components.Core
{
    public struct BattleContentRegistry: IComponentData
    {
        public BlobAssetReference<ContentBlobRegistry> BattleRegistryReference;
    }
}
