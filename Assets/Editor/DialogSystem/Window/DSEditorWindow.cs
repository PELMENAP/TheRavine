using UnityEditor;
using UnityEngine.UIElements;


public class DSEditorWindow : EditorWindow
{
    [MenuItem("Window/DS/Dialog graph")]
    public static void Open()
    {
        GetWindow<DSEditorWindow>("Dialog graph");
    }

    private void OnEnable()
    {
        AddGraphView();
        AddStyles();
    }
    #region Element Addition
    private void AddGraphView()
    {
        DSGraphView graphView = new DSGraphView(this);
        graphView.StretchToParentSize();
        rootVisualElement.Add(graphView);
    }
    private void AddStyles()
    {
        rootVisualElement.AddStyleSheets("Assets/Editor/Editor Default Resources/DialogSystem/DSVariables.uss");
    }
    #endregion
}