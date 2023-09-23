using UnityEngine;
using UnityEditor;

namespace Modern2D
{
    //  flag necressary for opengl builds because for some reason
    //  Unity includes scripts from editor folder in builds
#if UNITY_EDITOR

    public class Layers
    {//  original script created by user13214375 on stackoverflow.com
     //	extended for sorting layers and tags support
     //	https://stackoverflow.com/questions/61085096/how-can-i-create-a-new-layer-using-a-script-in-unity

        private static int maxTags = 10000;
        private static int maxSortingLayers = 31;
        private static int maxLayers = 31;

        /////////////////////////////////////////////////////////////////////

        public void AddNewLayer(string name)
        {
            CreateLayer(name);
        }

        public void DeleteLayer(string name)
        {
            RemoveLayer(name);
        }


        ////////////////////////////////////////////////////////////////////


        /// <summary>
        /// Adds the layer.
        /// </summary>
        /// <returns><c>true</c>, if layer was added, <c>false</c> otherwise.</returns>
        /// <param name="layerName">Layer name.</param>
        public static bool CreateLayer(string layerName)
        {
            // Open tag manager
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            // Layers Property
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (!PropertyExists(layersProp, 0, maxLayers, layerName))
            {
                SerializedProperty sp;
                // Start at layer 9th index -> 8 (zero based) => first 8 reserved for unity / greyed out
                for (int i = 8, j = maxLayers; i < j; i++)
                {
                    sp = layersProp.GetArrayElementAtIndex(i);
                    if (sp.stringValue == "")
                    {
                        // Assign string value to layer
                        sp.stringValue = layerName;
                        Debug.Log("Layer: " + layerName + " has been added");
                        // Save settings
                        tagManager.ApplyModifiedProperties();
                        return true;
                    }
                    if (i == j)
                        Debug.Log("All allowed layers have been filled");
                }
            }
            else
            {
                //Debug.Log ("Layer: " + layerName + " already exists");
            }
            return false;
        }

        public static int FindLayerIndex(string layerName)
        {
            if (!LayerExists(layerName)) throw new System.Exception("FindLayerIndex : layer -> " + layerName + " doesn't exist");

            // Open tag manager
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            // Layers Property
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            SerializedProperty sp;

            for (int i = 0, j = maxLayers; i < j; i++)
            {
                sp = layersProp.GetArrayElementAtIndex(i);
                if (sp.stringValue == layerName) return i;
            }
            throw new System.Exception("Layer not found");
        }


        public static bool CreateTag(string tag)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            if (asset != null)
            { // sanity checking
                var so = new SerializedObject(asset);
                var tags = so.FindProperty("tags");

                var numTags = tags.arraySize;
                // do not create duplicates
                int i;
                for (i = 0; i < numTags; i++)
                {
                    var existingTag = tags.GetArrayElementAtIndex(i);
                    if (existingTag.stringValue == tag) return true;
                }

                if (i == maxTags)
                {
                    Debug.Log("All allowed tags have been filled");
                    return false;
                }

                tags.InsertArrayElementAtIndex(numTags);
                tags.GetArrayElementAtIndex(numTags).stringValue = tag;
                so.ApplyModifiedProperties();
                so.Update();
                return true;
            }
            return false;
        }

        public static bool CreateSortingLayer(string sortingLayer)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            if (asset != null)
            { // sanity checking
                var so = new SerializedObject(asset);
                var sortingLayers = so.FindProperty("m_SortingLayers");

                var numTags = sortingLayers.arraySize;
                // do not create duplicates
                int i;
                for (i = 0; i < numTags; i++)
                {
                    var existingTag = sortingLayers.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                    if (existingTag.stringValue == sortingLayer) return true;
                }

                if (i == maxSortingLayers)
                {
                    Debug.Log("All allowed sorting layers have been filled");
                    return false;
                }

                sortingLayers.InsertArrayElementAtIndex(sortingLayers.arraySize);
                var newLayer = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);
                newLayer.FindPropertyRelative("name").stringValue = sortingLayer;
                newLayer.FindPropertyRelative("uniqueID").intValue = (int)System.DateTime.Now.Ticks;

                so.ApplyModifiedProperties();
                so.Update();
                return true;
            }
            return false;
        }

        public static string NewLayer(string name)
        {
            if (name != null || name != "")
            {
                CreateLayer(name);
            }

            return name;
        }

        /// <summary>
        /// Removes the layer.
        /// </summary>
        /// <returns><c>true</c>, if layer was removed, <c>false</c> otherwise.</returns>
        /// <param name="layerName">Layer name.</param>
        public static bool RemoveLayer(string layerName)
        {

            // Open tag manager
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            // Tags Property
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            if (PropertyExists(layersProp, 0, layersProp.arraySize, layerName))
            {
                SerializedProperty sp;

                for (int i = 0, j = layersProp.arraySize; i < j; i++)
                {

                    sp = layersProp.GetArrayElementAtIndex(i);

                    if (sp.stringValue == layerName)
                    {
                        sp.stringValue = "";
                        Debug.Log("Layer: " + layerName + " has been removed");
                        // Save settings
                        tagManager.ApplyModifiedProperties();
                        return true;
                    }

                }
            }

            return false;

        }
        /// <summary>
        /// Checks to see if layer exists.
        /// </summary>
        /// <returns><c>true</c>, if layer exists, <c>false</c> otherwise.</returns>
        /// <param name="layerName">Layer name.</param>
        public static bool LayerExists(string layerName)
        {
            // Open tag manager
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            // Layers Property
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            return PropertyExists(layersProp, 0, maxLayers, layerName);
        }

        /// <summary>
        /// Checks to see if a tag exists.
        /// </summary>
        /// <returns><c>true</c>, if layer exists, <c>false</c> otherwise.</returns>
        /// <param name="layerName">Layer name.</param>
        public static bool TagExists(string tagName)
        {
            // Open tag manager
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            // Layers Property
            SerializedProperty layersProp = tagManager.FindProperty("tags");
            return PropertyExists(layersProp, 0, maxTags, tagName);
        }

        /// <summary>
        /// Checks to see if a sorting layer exist
        /// </summary>
        /// <returns><c>true</c>, if layer exists, <c>false</c> otherwise.</returns>
        /// <param name="layerName">Layer name.</param>
        public static bool SortingLayerExists(string sortingLayerName)
        {
            // Open tag manager
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            // Layers Property
            SerializedProperty layersProp = tagManager.FindProperty("m_SortingLayers");
            return PropertyExistsSortingLayer(layersProp, 0, layersProp.arraySize, sortingLayerName);
        }

        /// <summary>
        /// Checks if the value exists in the property.
        /// </summary>
        /// <returns><c>true</c>, if exists was propertyed, <c>false</c> otherwise.</returns>
        /// <param name="property">Property.</param>
        /// <param name="start">Start.</param>
        /// <param name="end">End.</param>
        /// <param name="value">Value.</param>
        private static bool PropertyExistsSortingLayer(SerializedProperty property, int start, int end, string value)
        {
            for (int i = start; i < end; i++)
            {
                SerializedProperty t = property.GetArrayElementAtIndex(i);
                //	this one differs from the other ones, as we have to dive one layer deeper for the name property
                //	don't know why throught
                if (t.FindPropertyRelative("name").stringValue.Equals(value))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool PropertyExists(SerializedProperty property, int start, int end, string value)
        {
            for (int i = start; i < end; i++)
            {
                SerializedProperty t = property.GetArrayElementAtIndex(i);
                //	this one differs from the other ones, as we have to dive one layer deeper for the name property
                //	don't know why throught
                if (t.stringValue.Equals(value))
                {
                    return true;
                }
            }
            return false;
        }
    }

#endif

}