using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
public class DSNode : Node
{
    public string DialogueName { get; set; }
    public List<string> Choices { get; set; }
    public string Text { get; set; }
    public DSDialogueType DialogueType { get; set; }
    public Group Group { get; set; }

    protected DSGraphView graphView;
    private Color defaultBackgroundColor;

    public virtual void Initialize(DSGraphView dsGraphView, Vector2 position)
    {
        DialogueName = "Dialog name";
        Choices = new List<string>();
        Text = "Dialoge text.";

        graphView = dsGraphView;

        defaultBackgroundColor = new Color(29f / 255f, 29 / 255f, 30 / 255f);

        SetPosition(new Rect(position, Vector2.zero));

        mainContainer.AddToClassList("ds-node__main-container");
        extensionContainer.AddToClassList("ds-node__extension-container");
    }

    public virtual void Draw()
    {
        TextField dialogueNameTextField = DSElementUtility.CreateTextField(DialogueName, callback =>
        {
            if (Group == null)
            {
                graphView.RemoveUngroupedNode(this);

                DialogueName = callback.newValue;

                graphView.AddUngroupedNode(this);

                return;
            }
            DSGroup currentGroup = (DSGroup)Group;

            graphView.RemoveGroupedNode(this, Group);

            DialogueName = callback.newValue;

            graphView.AddGroupedNode(this, currentGroup);
        }
        );

        dialogueNameTextField.AddClasses("ds-node__text-field", "ds-node__text-field__hidden", "ds-node__filename-text-field");

        titleContainer.Insert(0, dialogueNameTextField);

        Port inputPort = this.CreatePort("Dialogue Connection", Orientation.Horizontal, Direction.Input, Port.Capacity.Multi);
        inputContainer.Add(inputPort);

        VisualElement customDataContainer = new VisualElement();

        customDataContainer.AddToClassList("ds-node__custom-data-container");

        Foldout textFoldout = DSElementUtility.CreateFoldout("Dialogue text");
        TextField textTextField = DSElementUtility.CreateTextArea(Text);

        textTextField.AddClasses("ds-node__text-field", "ds-node__quote-text-field");

        textFoldout.Add(textTextField);
        customDataContainer.Add(textFoldout);
        extensionContainer.Add(customDataContainer);
    }

    #region Utility Methods
    private void DisconnectPorts(VisualElement container)
    {
        foreach (Port port in container.Children())
        {
            if (!port.connected)
            {
                continue;
            }

            graphView.DeleteElements(port.connections);
        }
    }

    public void SetErrorStyle(Color color)
    {
        mainContainer.style.backgroundColor = color;
    }

    public void ResetStyle()
    {
        mainContainer.style.backgroundColor = defaultBackgroundColor;
    }
    #endregion
}
