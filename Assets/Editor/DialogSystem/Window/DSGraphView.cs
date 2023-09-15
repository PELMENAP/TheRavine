using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using System;
using System.Collections.Generic;
public class DSGraphView : GraphView
{
    private DSEditorWindow editorWindow;
    private DSSearchWindow searchWindow;
    private SerializableDictionary<string, DSNodeErrorData> ungroupedNodes;
    private SerializableDictionary<string, DSGroupErrorData> groups;
    private SerializableDictionary<Group, SerializableDictionary<string, DSNodeErrorData>> groupedNodes;
    public DSGraphView(DSEditorWindow dseditorWindow)
    {
        editorWindow = dseditorWindow;
        ungroupedNodes = new SerializableDictionary<string, DSNodeErrorData>();
        groups = new SerializableDictionary<string, DSGroupErrorData>();
        groupedNodes = new SerializableDictionary<Group, SerializableDictionary<string, DSNodeErrorData>>();
        AddManipulators();
        AddSearchWindow();
        AddGridBackGround();

        OnElementDeleted();
        OnGroupElementsAdded();
        OnGroupElementsRemoved();
        OnGroupRenamed();

        AddStyles();
    }

    #region Overrided Methods
    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        List<Port> compatiblePorts = new List<Port>();
        ports.ForEach(port =>
        {
            if ((startPort == port) || (startPort.node == port.node) || (startPort.direction == port.direction))
                return;
            compatiblePorts.Add(port);
        });
        return compatiblePorts;
    }
    #endregion

    #region Manipulators
    private void AddManipulators()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());

        this.AddManipulator(new ContentZoomer());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        this.AddManipulator(CreateNodeContextualMenu("Add Node (Single Choice)", DSDialogueType.SingleChoice));
        this.AddManipulator(CreateNodeContextualMenu("Add Node (Multiple Choice)", DSDialogueType.MultipleChoice));
        this.AddManipulator(CreateGroupContextualMenu());
    }

    private IManipulator CreateGroupContextualMenu()
    {
        ContextualMenuManipulator contextualMenuManipulator = new ContextualMenuManipulator(
            menuEvent => menuEvent.menu.AppendAction("Add group", actionEvent => CreateGroup("DialogueGroup", GetLocalMousePosition(actionEvent.eventInfo.localMousePosition)))
        );
        return contextualMenuManipulator;
    }
    private IManipulator CreateNodeContextualMenu(string actionTitle, DSDialogueType dialogueType)
    {
        ContextualMenuManipulator contextualMenuManipulator = new ContextualMenuManipulator(
            menuEvent => menuEvent.menu.AppendAction(actionTitle, actionEvent => AddElement(CreateNode(dialogueType, GetLocalMousePosition(actionEvent.eventInfo.localMousePosition))))
        );
        return contextualMenuManipulator;
    }
    #endregion
    #region Element Creation
    public DSGroup CreateGroup(string title, Vector2 localMousePosition)
    {
        DSGroup group = new DSGroup(title, localMousePosition);

        AddGroup(group);

        AddElement(group);

        foreach (GraphElement selectedElement in selection)
        {
            if (!(selectedElement is DSNode))
            {
                continue;
            }
            DSNode node = (DSNode)selectedElement;
            group.AddElement(node);
        }

        return group;
    }
    public DSNode CreateNode(DSDialogueType dialogueType, Vector2 position)
    {
        Type nodeType = Type.GetType($"DS{dialogueType}Node");
        DSNode node = (DSNode)Activator.CreateInstance(nodeType);
        node.Initialize(this, position);
        node.Draw();
        AddUngroupedNode(node);
        return node;
    }
    #endregion

    #region Callbacks
    private void OnElementDeleted()
    {
        deleteSelection = (operationName, askUser) =>
        {
            Type groupType = typeof(DSGroup);
            Type edgeType = typeof(Edge);

            List<DSGroup> groupsToDelete = new List<DSGroup>();
            List<Edge> edgesToDelete = new List<Edge>();
            List<DSNode> nodesToDelete = new List<DSNode>();

            foreach (GraphElement element in selection)
            {
                if (element is DSNode)
                {
                    nodesToDelete.Add((DSNode)element);
                    continue;
                }

                if (element.GetType() == edgeType)
                {
                    Edge edge = (Edge)element;
                    edgesToDelete.Add(edge);
                    continue;
                }

                if (element.GetType() != groupType)
                {
                    continue;
                }

                DSGroup group = (DSGroup)element;

                groupsToDelete.Add(group);
            }

            foreach (DSGroup group in groupsToDelete)
            {
                List<DSNode> groupNodes = new List<DSNode>();

                foreach (GraphElement groupElement in group.containedElements)
                {
                    if (!(groupElement is DSNode))
                    {
                        continue;
                    }

                    DSNode groupNode = (DSNode)groupElement;

                    groupNodes.Add(groupNode);
                }

                group.RemoveElements(groupNodes);

                RemoveGroup(group);

                RemoveElement(group);
            }

            DeleteElements(edgesToDelete);

            foreach (DSNode node in nodesToDelete)
            {
                if (node.Group != null)
                {
                    node.Group.RemoveElement(node);
                }
                RemoveUngroupedNode(node);

                RemoveElement(node);
            }
        };
    }
    private void OnGroupElementsAdded()
    {
        elementsAddedToGroup = (group, elements) =>
        {
            foreach (GraphElement element in elements)
            {
                if (!(element is DSNode))
                {
                    continue;
                }

                DSGroup dsGroup = (DSGroup)group;
                DSNode node = (DSNode)element;

                RemoveUngroupedNode(node);
                AddGroupedNode(node, dsGroup);
            }
        };
    }
    private void OnGroupElementsRemoved()
    {
        elementsRemovedFromGroup = (group, elements) =>
        {
            foreach (GraphElement element in elements)
            {
                if (!(element is DSNode))
                {
                    continue;
                }

                DSNode node = (DSNode)element;

                RemoveGroupedNode(node, group);
                AddUngroupedNode(node);
            }
        };
    }
    private void OnGroupRenamed()
    {
        groupTitleChanged = (group, newTitle) =>
        {
            DSGroup dsGroup = (DSGroup)group;

            RemoveGroup(dsGroup);

            dsGroup.OldTitle = dsGroup.title;

            AddGroup(dsGroup);
        };
    }
    #endregion

    #region Repeated Elements
    public void AddUngroupedNode(DSNode node)
    {
        string nodeName = node.DialogueName;
        if (!ungroupedNodes.ContainsKey(nodeName))
        {
            DSNodeErrorData nodeErrorData = new DSNodeErrorData();
            nodeErrorData.Nodes.Add(node);
            ungroupedNodes.Add(nodeName, nodeErrorData);
            return;
        }
        List<DSNode> ungroupedNodesList = ungroupedNodes[nodeName].Nodes;

        ungroupedNodesList.Add(node);

        Color errorColor = ungroupedNodes[nodeName].ErrorData.Color;
        node.SetErrorStyle(errorColor);

        if (ungroupedNodesList.Count == 2)
        {
            ungroupedNodesList[0].SetErrorStyle(errorColor);
        }
    }

    private void AddGroup(DSGroup group)
    {
        string groupName = group.title;

        if (!groups.ContainsKey(groupName))
        {
            DSGroupErrorData groupErrorData = new DSGroupErrorData();

            groupErrorData.Groups.Add(group);

            groups.Add(groupName, groupErrorData);

            return;
        }

        List<DSGroup> groupsList = groups[groupName].Groups;

        groupsList.Add(group);

        Color errorColor = groups[groupName].ErrorData.Color;

        group.SetErrorStyle(errorColor);

        if (groupsList.Count == 2)
        {
            groupsList[0].SetErrorStyle(errorColor);
        }
    }

    private void RemoveGroup(DSGroup group)
    {
        string oldGroupName = group.OldTitle;

        List<DSGroup> groupsList = groups[oldGroupName].Groups;

        groupsList.Remove(group);

        group.ResetStyle();

        if (groupsList.Count == 1)
        {
            groupsList[0].ResetStyle();

            return;
        }

        if (groupsList.Count == 0)
        {
            groups.Remove(oldGroupName);
        }
    }
    public void RemoveUngroupedNode(DSNode node)
    {
        string nodeName = node.DialogueName;

        List<DSNode> ungroupedNodesList = ungroupedNodes[nodeName].Nodes;

        ungroupedNodesList.Remove(node);

        node.ResetStyle();

        if (ungroupedNodesList.Count == 1)
        {
            ungroupedNodesList[0].ResetStyle();
            return;
        }

        if (ungroupedNodesList.Count == 0)
        {
            ungroupedNodes.Remove(nodeName);
        }
    }

    public void AddGroupedNode(DSNode node, DSGroup group)
    {
        string nodeName = node.DialogueName;

        node.Group = group;

        if (!groupedNodes.ContainsKey(group))
        {
            groupedNodes.Add(group, new SerializableDictionary<string, DSNodeErrorData>());
        }

        if (!groupedNodes[group].ContainsKey(nodeName))
        {
            DSNodeErrorData nodeErrorData = new DSNodeErrorData();

            nodeErrorData.Nodes.Add(node);

            groupedNodes[group].Add(nodeName, nodeErrorData);
            return;
        }

        List<DSNode> groupedNodesList = groupedNodes[group][nodeName].Nodes;

        groupedNodesList.Add(node);

        Color errorColor = groupedNodes[group][nodeName].ErrorData.Color;

        node.SetErrorStyle(errorColor);

        if (groupedNodesList.Count == 2)
        {
            groupedNodesList[0].SetErrorStyle(errorColor);
        }
    }
    public void RemoveGroupedNode(DSNode node, Group group)
    {
        string nodeName = node.DialogueName;

        node.Group = null;

        List<DSNode> groupedNodesList = groupedNodes[group][nodeName].Nodes;

        groupedNodesList.Remove(node);

        node.ResetStyle();

        if (groupedNodesList.Count == 1)
        {
            groupedNodesList[0].ResetStyle();

            return;
        }

        if (groupedNodesList.Count == 0)
        {
            groupedNodes[group].Remove(nodeName);

            if (groupedNodes[group].Count == 0)
            {
                groupedNodes.Remove(group);
            }
        }
    }


    #endregion

    #region Element Addition

    private void AddSearchWindow()
    {
        if (searchWindow == null)
        {
            searchWindow = ScriptableObject.CreateInstance<DSSearchWindow>();
            searchWindow.Initialize(this);
        }
        nodeCreationRequest = context => SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), searchWindow);
    }
    private void AddGridBackGround()
    {
        GridBackground gridBackground = new GridBackground();
        gridBackground.StretchToParentSize();
        Insert(0, gridBackground);
    }
    private void AddStyles()
    {
        this.AddStyleSheets("Assets/Editor/Editor Default Resources/DialogSystem/DSGraphViewStyles.uss", "Assets/Editor/Editor Default Resources/DialogSystem/DSNodeStyle.uss");
    }
    #endregion

    #region Utilities
    public Vector2 GetLocalMousePosition(Vector2 mousePosition, bool isSearchWindow = false)
    {
        Vector2 worldMousePosition = mousePosition;
        if (isSearchWindow)
            worldMousePosition -= editorWindow.position.position;
        Vector2 localMousePosition = contentViewContainer.WorldToLocal(worldMousePosition);
        return localMousePosition;
    }
    #endregion
}
