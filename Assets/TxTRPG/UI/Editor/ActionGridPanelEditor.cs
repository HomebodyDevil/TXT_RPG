using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Editor
{
    [CustomPropertyDrawer(typeof(ActionGridDisplaySettings))]
    public sealed class ActionGridDisplaySettingsDrawer : PropertyDrawer
    {
        private static SerializedProperty Preset(SerializedProperty property) => property.FindPropertyRelative("mode").enumValueIndex switch
        {
            1 => property.FindPropertyRelative("balanced"),
            2 => property.FindPropertyRelative("distributedSpacing"),
            3 => property.FindPropertyRelative("largeSlots"),
            _ => null
        };
        private static System.Collections.Generic.IEnumerable<SerializedProperty> Fields(SerializedProperty property)
        {
            yield return property.FindPropertyRelative("mode");
            yield return property.FindPropertyRelative("flow");
            if (property.FindPropertyRelative("flow").enumValueIndex == 1) yield return property.FindPropertyRelative("singleRow");
            var preset = Preset(property);
            if (preset == null) yield break;
            foreach (var name in new[] { "maximumColumns", "minimumCellSize", "maximumCellSize", "targetCellSize", "spacing", "padding", "alignment", "incompleteRowAlignment", "verticalPlacement", "minimumHorizontalGap", "maximumHorizontalGap" })
                yield return preset.FindPropertyRelative(name);
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight * 2 + 8;
            foreach (var field in Fields(property)) height += EditorGUI.GetPropertyHeight(field, true) + 3;
            return height;
        }
        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, property);
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(rect, label, EditorStyles.boldLabel); rect.y += rect.height + 4;
            var row = property.FindPropertyRelative("flow").enumValueIndex == 1;
            foreach (var field in Fields(property))
            {
                rect.height = EditorGUI.GetPropertyHeight(field, true);
                var ignored = row && (field.name == "maximumColumns" || field.name == "incompleteRowAlignment" || field.name == "alignment" || field.name == "verticalPlacement" || field.name == "minimumHorizontalGap" || field.name == "maximumHorizontalGap");
                using (new EditorGUI.DisabledScope(ignored)) EditorGUI.PropertyField(rect, field, true);
                rect.y += rect.height + 3;
            }
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(rect, row ? "Single Row ignores disabled grid fields; target size / gap / padding come from the preset." : "Canvas UI units. Each preset retains its settings.", EditorStyles.miniLabel);
            EditorGUI.EndProperty();
        }
    }

    [CustomEditor(typeof(ActionGridPanel)), CanEditMultipleObjects]
    public sealed class ActionGridPanelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true)) do
            {
                if (iterator.name == "m_Script") continue;
                var row = ((ActionGridPanel)target).IsSingleRow;
                var ignored = row && (iterator.name == "layoutMode" || iterator.name == "fixedColumns" || iterator.name == "minimumCellSize" || iterator.name == "maximumCellSize" || iterator.name == "gridAlignment" || iterator.name == "incompleteRowAlignment" || iterator.name == "verticalPlacement");
                using (new EditorGUI.DisabledScope(ignored)) EditorGUILayout.PropertyField(iterator, true);
            } while (iterator.NextVisible(false));
            if (serializedObject.ApplyModifiedProperties())
                foreach (var item in targets) ((ActionGridPanel)item).RefreshDisplayLayout();
            if (targets.Length != 1) return;
            var panel = (ActionGridPanel)target;
            var result = panel.CurrentLayout;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resolved Layout", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Columns / Cell", $"{result.Columns} / {result.CellSize}");
            EditorGUILayout.LabelField("Gap / Required height", $"{result.Spacing} / {result.RequiredHeight:0.##}");
            EditorGUILayout.LabelField("Required width", $"{result.RequiredWidth:0.##}");
            if (panel.SurfaceLayout != null)
            {
                var surface = panel.SurfaceLayout;
                var show = EditorGUILayout.Toggle("Show Header (GameObject)", surface.HeaderVisible);
                if (show != surface.HeaderVisible) { Undo.RecordObject(surface.transform.parent.Find("Header").gameObject, "Toggle Actions Header"); surface.SetHeaderVisible(show); }
                EditorGUILayout.LabelField("Header reservation", surface.HeaderReservation.ToString("0.##"));
                EditorGUILayout.HelpBox("Header height, gap and body margins: Surface Layout on Scroll View. Header activeSelf is the visibility source of truth.", MessageType.None);
            }
            if (panel.IsSingleRow) EditorGUILayout.HelpBox(!result.IsReady ? "Waiting for a positive viewport." : result.RequiredHeight > panel.ScrollRect.viewport.rect.height ? "Body is too short for the row. Increase body height or reduce target size / vertical padding." : "One row; horizontal overflow scrolls. Hidden scrollbars reserve no space.", MessageType.Info);
            else EditorGUILayout.HelpBox(!result.IsReady ? "Waiting for a positive viewport size." : result.HasInsufficientSpace ? "Viewport cannot fully fit one row/cell. Check parent allocation; vertical overflow uses scrolling." : "Viewport fits the cell size. Additional rows use vertical scrolling.", result.HasInsufficientSpace ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.HelpBox("Manual Layout fields are retained while presets are active. ConfigureLayout selects Manual. Bag: choose Presentation in the owning Inventory page's Grid Settings.", MessageType.None);
        }
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
    [CustomEditor(typeof(ActionGridSurfaceLayout))]
    public sealed class ActionGridSurfaceLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            if (serializedObject.ApplyModifiedProperties()) ((ActionGridSurfaceLayout)target).HeaderStateChanged();
        }
    }

}
