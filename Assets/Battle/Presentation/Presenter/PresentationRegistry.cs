using System.Collections.Generic;
using Archeus.Battle.Presentation.Presenters;

namespace Archeus.Battle.Presentation
{
    public sealed class PresentationRegistry
    {
        private readonly Dictionary<uint, CharacterPresenter> presenters = new();

        public bool Register(CharacterPresenter presenter)
        {
            if (presenter == null)
                return false;

            return presenters.TryAdd(presenter.RuntimeID, presenter);
        }

        public void Unregister(CharacterPresenter presenter)
        {
            if (presenter == null)
                return;

            if (
                presenters.TryGetValue(presenter.RuntimeID, out CharacterPresenter existing)
                && existing == presenter
            )
            {
                presenters.Remove(presenter.RuntimeID);
            }
        }

        public bool TryGet(uint runtimeID, out CharacterPresenter presenter)
        {
            return presenters.TryGetValue(runtimeID, out presenter);
        }

        public void Clear()
        {
            presenters.Clear();
        }
    }
}
