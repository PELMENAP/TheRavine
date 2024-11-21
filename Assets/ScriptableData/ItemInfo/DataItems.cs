using UnityEngine;
using System.Collections.Generic;
[CreateAssetMenu(fileName = "DataItems", menuName = "Gameplay/Create New DataItems")]
public class DataItems : ScriptableObject
{
    [SerializeField] private List<InventoryItemInfo> _data;
    public List<InventoryItemInfo> data => _data;
}

