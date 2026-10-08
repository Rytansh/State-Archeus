using System;
using UnityEngine;

public sealed class FieldCardView : MonoBehaviour
{
    [SerializeField]
    private FieldPosition position;

    public FieldPosition Position => position;

    public event Action<FieldPosition> SlotClicked;
    public event Action<uint> CardClicked;
}
