using UnityEngine;

[CreateAssetMenu(fileName = "CasinoSlotPref", menuName = "Gameplay/Create New CasinoSlotPref")]
public class CasinoSlotPref : ScriptableObject
{
    public SlotCell[] slotCells;
}

[System.Serializable]

public struct SlotCell
{
    public Sprite sprite;
    public int[] costs;
}
