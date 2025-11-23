using System.Collections.Generic;
using UnityEngine;

public class DialogSystem
{
    private const float CELL_SIZE = 10f;
    private static Dictionary<Vector2Int, List<IDialogListener>> grid;
    private static Dictionary<IDialogListener, Vector2Int> listenerCells;

    private static DialogSystem _instance;
    public static DialogSystem Instance 
    {
        get
        {
            if (_instance == null) 
            {
                _instance = new DialogSystem();
                grid = new Dictionary<Vector2Int, List<IDialogListener>>();
                listenerCells = new Dictionary<IDialogListener, Vector2Int>();
            }
            return _instance;
        }
    }

    private Vector2Int PositionToCell(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / CELL_SIZE),
            Mathf.FloorToInt(position.z / CELL_SIZE)
        );
    }

    public void AddDialogListener(IDialogListener listener)
    {
        Vector2Int cell = PositionToCell(listener.GetCurrentPosition());
        
        if (!grid.ContainsKey(cell))
            grid[cell] = new List<IDialogListener>();
        
        grid[cell].Add(listener);
        listenerCells[listener] = cell;
    }

    public void RemoveDialogListener(IDialogListener listener)
    {
        if (listenerCells.TryGetValue(listener, out var cell))
        {
            if (grid.ContainsKey(cell))
                grid[cell].Remove(listener);
            
            listenerCells.Remove(listener);
        }
    }

    public void UpdateListenerPosition(IDialogListener listener)
    {
        Vector2Int newCell = PositionToCell(listener.GetCurrentPosition());
        
        if (listenerCells.TryGetValue(listener, out var oldCell) && oldCell != newCell)
        {
            if (grid.ContainsKey(oldCell))
                grid[oldCell].Remove(listener);
            
            if (!grid.ContainsKey(newCell))
                grid[newCell] = new List<IDialogListener>();
            
            grid[newCell].Add(listener);
            listenerCells[listener] = newCell;
        }
    }

    public void OnSpeechSend(IDialogSender sender, string message)
    {
        Vector3 senderPosition = sender.GetCurrentPosition();
        float distance = sender.GetDialogDistance();
        
        int cellsRadius = Mathf.CeilToInt(distance / CELL_SIZE);
        Vector2Int centerCell = PositionToCell(senderPosition);
        
        for (int x = -cellsRadius; x <= cellsRadius; x++)
        {
            for (int y = -cellsRadius; y <= cellsRadius; y++)
            {
                Vector2Int cell = new Vector2Int(centerCell.x + x, centerCell.y + y);
                
                if (grid.TryGetValue(cell, out var cellListeners))
                {
                    foreach (var listener in cellListeners)
                    {
                        if (Vector3.Distance(senderPosition, listener.GetCurrentPosition()) <= distance && listener.GetCurrentPosition() != sender.GetCurrentPosition())
                        {
                            listener.OnSpeechGet(sender, message);
                        }
                    }
                }
            }
        }
    }
}