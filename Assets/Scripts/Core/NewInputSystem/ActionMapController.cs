using System.Collections.Generic;
using ZLinq;
using UnityEngine.InputSystem;
using R3;

public class ActionMapController
{
    private readonly InputActionAsset _actionAsset;
    private readonly Dictionary<string, InputActionMap> _actionMaps;
    private readonly ReactiveProperty<string> _currentMapName;
    
    public ReadOnlyReactiveProperty<string> CurrentMapName { get; }
    public InputActionMap CurrentMap => _actionMaps.GetValueOrDefault(_currentMapName.Value);
    public IReadOnlyDictionary<string, InputActionMap> AvailableMaps => _actionMaps;

    private const string gamePlay = "Gameplay", inventory = "Inventory", textInput = "TextInput";
    
    private IRavineLogger logger;

    public ActionMapController(InputActionAsset actionAsset, IRavineLogger logger)
    {
        this.logger = logger;
        _actionAsset = actionAsset;
        _actionMaps = _actionAsset.actionMaps.AsValueEnumerable().ToDictionary(map => map.name, map => map);
        _currentMapName = new ReactiveProperty<string>(string.Empty);
        CurrentMapName = _currentMapName.ToReadOnlyReactiveProperty();
    }

    public void SwitchToInventory() => SwitchTo(inventory);
    public void SwitchToGameplay() => SwitchTo(gamePlay);
    public void SwitchToTextInput() => SwitchTo(textInput);

    public void EnableUI()
    {
        if (!_actionMaps.TryGetValue("UI", out var targetMap))
        {
            logger.LogWarning($"UI not found in asset");
            return;
        }

        targetMap.Enable();
    }

    public void DisableUI()
    {
        if (!_actionMaps.TryGetValue("UI", out var targetMap))
        {
            logger.LogWarning($"UI not found in asset");
            return;
        }

        targetMap.Disable();
    }

    public bool SwitchTo(string mapName)
    {
        if (!_actionMaps.TryGetValue(mapName, out var targetMap))
        {
            logger.LogWarning($"ActionMap '{mapName}' not found in asset");
            return false;
        }

        CurrentMap?.Disable();
        targetMap.Enable();
        _currentMapName.Value = mapName;
        
        return true;
    }

    public void DisableAll()
    {
        foreach (var map in _actionMaps.Values)
        {
            map.Disable();
        }
        _currentMapName.Value = string.Empty;
    }

    public bool IsActive(string mapName) => _currentMapName.Value == mapName;

    public bool IsGamePlayActive() => _currentMapName.Value == gamePlay;

    public InputAction GetAction(string actionName)
    {
        return CurrentMap?.FindAction(actionName);
    }

    public InputAction GetActionFrom(string mapName, string actionName)
    {
        return _actionMaps.TryGetValue(mapName, out var map) 
            ? map.FindAction(actionName) 
            : null;
    }

    public Observable<string> OnMapChanged() => _currentMapName.Where(name => !string.IsNullOrEmpty(name));

    public void Dispose()
    {
        DisableAll();
        _currentMapName?.Dispose();
    }
}