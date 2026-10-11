using System.Collections.Generic;
using System.Linq;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Editor
{
    // Lets the developer pick the selected skin from the registered skins; the selection is still stored as the ID
    // string. Shows the catalog validation result.
    [CustomEditor(typeof(SlotMachinePanelSkinCatalog))]
    public sealed class SlotMachinePanelSkinCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var skins = serializedObject.FindProperty("skins");
            var selected = serializedObject.FindProperty("selectedSkinId");
            EditorGUILayout.PropertyField(skins, true);

            var registered = new List<SlotMachinePanelSkin>();
            for (var i = 0; i < skins.arraySize; i++)
                if (skins.GetArrayElementAtIndex(i).objectReferenceValue is SlotMachinePanelSkin skin && skin.SkinId.Length > 0)
                    registered.Add(skin);
            var ids = registered.Select(s => s.SkinId).Distinct().ToList();
            var labels = ids.Select(id => $"{registered.First(s => s.SkinId == id).DisplayName} ({id})").ToList();
            var current = ids.IndexOf(selected.stringValue.Trim());
            // The current value stays selectable as is, so viewing the Inspector never changes the selection.
            if (current < 0)
            {
                ids.Insert(0, selected.stringValue);
                labels.Insert(0, selected.stringValue.Length > 0 ? $"(등록되지 않음) {selected.stringValue}" : "(선택 안 함)");
                current = 0;
            }
            if (ids.Count == 1 && registered.Count == 0) EditorGUILayout.PropertyField(selected, new GUIContent("Selected Skin Id"));
            else
            {
                var chosen = EditorGUILayout.Popup("Selected Skin", current, labels.ToArray());
                if (chosen != current) selected.stringValue = ids[chosen];
            }
            serializedObject.ApplyModifiedProperties();

            var catalog = (SlotMachinePanelSkinCatalog)target;
            if (!catalog.Validate(out var reason))
                EditorGUILayout.HelpBox($"카탈로그 검증 실패: {reason}. 게임은 시작되지만 패널은 기본 외형으로 표시됩니다.", MessageType.Warning);
        }
    }
}
