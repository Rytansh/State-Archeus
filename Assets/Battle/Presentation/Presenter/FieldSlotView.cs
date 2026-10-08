using System;
using UnityEngine;

public sealed class FieldSlotView : MonoBehaviour
{
    [SerializeField]
    private FieldPosition position;

    [SerializeField]
    private FieldCardView cardView;

    private bool occupiedThisRefresh;

    public FieldPosition Position => position;

    public event Action<FieldPosition> SlotClicked;
    public event Action<uint> CardClicked;
}
