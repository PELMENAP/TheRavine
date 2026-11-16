using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SkillGraphMenu : VisualElement
{
    private readonly Label _title = new();
    private readonly Label _info = new();
    private readonly VisualElement _options = new();

    public int OptionCount => _options.childCount;
    public bool IsShown => style.display.value != DisplayStyle.None;

    public SkillGraphMenu()
    {
        AddToClassList("skill-menu");
        style.position = Position.Absolute;
        style.display = DisplayStyle.None;
        _title.AddToClassList("skill-menu__title");
        _info.AddToClassList("skill-menu__info");
        _options.AddToClassList("skill-menu__options");
        Add(_title);
        Add(_info);
        Add(_options);
    }

    public void Begin(string title, string info)
    {
        _title.text = title;
        _info.text = info;
        _options.Clear();
    }

    public void AddOption(string label, bool enabled, Action action)
    {
        var button = new Button(() =>
        {
            Hide();
            action?.Invoke();
        }) { text = label };
        button.AddToClassList("skill-button");
        button.AddToClassList("skill-menu__option");
        button.SetEnabled(enabled);
        _options.Add(button);
    }

    public void AddNote(string text)
    {
        var note = new Label(text);
        note.AddToClassList("skill-menu__note");
        _options.Add(note);
    }

    public void Present(Vector2 position, Vector2 bounds)
    {
        style.display = DisplayStyle.Flex;
        style.left = Mathf.Clamp(position.x, 0f, Mathf.Max(bounds.x - SkillGraphPalette.MenuWidth, 0f));
        style.top = Mathf.Clamp(position.y, 0f, Mathf.Max(bounds.y - SkillGraphPalette.MenuHeightGuess, 0f));
    }

    public void Hide() => style.display = DisplayStyle.None;
}
