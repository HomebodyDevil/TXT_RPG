using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.SceneTransition.Editor
{
    [CustomPropertyDrawer(typeof(ScenePathAttribute))]
    public sealed class ScenePathPropertyDrawer : PropertyDrawer
    {
        private const float HelpBoxHeight = 38f;

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var scenePath = (ScenePathAttribute)attribute;
            return BuildScenePathUtility.TryValidate(
                property.stringValue,
                scenePath.ExcludeAppScene,
                out _)
                ? EditorGUIUtility.singleLineHeight
                : EditorGUIUtility.singleLineHeight +
                  EditorGUIUtility.standardVerticalSpacing +
                  HelpBoxHeight;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, "ScenePath can only be used with string fields.", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            var scenePath = (ScenePathAttribute)attribute;
            var options = BuildScenePathUtility.GetSelectableScenes(scenePath.ExcludeAppScene);
            var popupRect = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            DrawPopup(popupRect, property, label, options);

            if (!BuildScenePathUtility.TryValidate(
                    property.stringValue,
                    scenePath.ExcludeAppScene,
                    out var error))
            {
                var helpRect = new Rect(
                    position.x,
                    popupRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                    position.width,
                    HelpBoxHeight);
                EditorGUI.HelpBox(helpRect, error, MessageType.Error);
            }
            EditorGUI.EndProperty();
        }

        private static void DrawPopup(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            IReadOnlyList<BuildSceneOption> options)
        {
            var paths = options.Select(option => option.Path).ToList();
            var labels = options.Select(option => new GUIContent(option.DisplayName)).ToList();
            var currentPath = BuildScenePathUtility.Normalize(property.stringValue);
            var selectedIndex = paths.FindIndex(path =>
                string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase));

            if (selectedIndex < 0)
            {
                paths.Insert(0, null);
                labels.Insert(0, new GUIContent(
                    string.IsNullOrEmpty(currentPath)
                        ? "<Select an enabled Build Settings scene>"
                        : $"<Invalid> {currentPath}"));
                selectedIndex = 0;
            }

            using (new EditorGUI.DisabledScope(options.Count == 0))
            {
                EditorGUI.BeginChangeCheck();
                var nextIndex = EditorGUI.Popup(position, label, selectedIndex, labels.ToArray());
                if (EditorGUI.EndChangeCheck() &&
                    nextIndex >= 0 &&
                    nextIndex < paths.Count &&
                    !string.IsNullOrEmpty(paths[nextIndex]))
                {
                    property.stringValue = paths[nextIndex];
                }
            }
        }
    }
}

