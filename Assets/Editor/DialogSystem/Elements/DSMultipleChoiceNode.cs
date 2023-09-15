using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
public class DSMultipleChoiceNode : DSNode
{
    public override void Initialize(DSGraphView dsGraphView, Vector2 position)
    {
        base.Initialize(dsGraphView, position);
        DialogueType = DSDialogueType.MultipleChoice;

        Choices.Add("New Chioce");
    }

    public override void Draw()
    {
        base.Draw();
        Button addChoiceButton = DSElementUtility.CreateButton("Add Choice", () =>
        {
            Port choicePort = CreateChoicePort("New Choice");
            Choices.Add("New Choice");
            outputContainer.Add(choicePort);
        });

        addChoiceButton.AddToClassList("ds-node__button");
        mainContainer.Insert(1, addChoiceButton);

        foreach (string choice in Choices)
        {
            Port choicePort = CreateChoicePort(choice);
            outputContainer.Add(choicePort);
        }
        RefreshExpandedState();
    }

    #region Element Creation
    private Port CreateChoicePort(string choice)
    {
        Port choicePort = this.CreatePort();
        Button deleteChoiceButton = DSElementUtility.CreateButton("X");
        deleteChoiceButton.AddToClassList("ds-node__button");
        TextField choiceTextField = DSElementUtility.CreateTextField(choice);
        choiceTextField.AddClasses("ds-node__text-field", "ds-node__text-field__hidden", "ds-node__choice-text-field");
        choicePort.Add(choiceTextField);
        choicePort.Add(deleteChoiceButton);
        return choicePort;
    }
    #endregion
}
