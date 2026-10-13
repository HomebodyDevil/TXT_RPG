using System;
using System.Collections.Generic;
using System.Linq;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay.SlotMachine;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Editor
{
    // Inspector of TemporaryFortuneCriticalConfiguration: a weight function picker with named parameter fields, and a
    // read-only preview per rule computed with the runtime types (FortuneCriticalRule, FortuneMultiplierTable), so it
    // shows exactly what the game draws. The preview never changes the asset.
    [CustomEditor(typeof(TemporaryFortuneCriticalConfiguration))]
    public sealed class TemporaryFortuneCriticalConfigurationEditor : UnityEditor.Editor
    {
        private static readonly Color BarColor = new(1f, .78f, .24f, .85f);
        private static readonly Color BarBackground = new(0f, 0f, 0f, .15f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var registry = FortuneWeightFunctionRegistry.Default;
            EditorGUILayout.HelpBox("기본값은 확정되지 않은 임시 밸런스 값입니다. 실행 중 변경은 TemporaryFortuneCriticalState API로 하며 이 자산은 바뀌지 않습니다.", MessageType.None);

            var defaultRule = serializedObject.FindProperty("defaultReelRule");
            var reelRules = serializedObject.FindProperty("reelRules");
            var totalRule = serializedObject.FindProperty("totalRule");
            var cap = serializedObject.FindProperty("maximumEffectiveMultiplier");

            EditorGUILayout.PropertyField(cap, new GUIContent("유효 배수 상한 C", cap.tooltip));
            EditorGUILayout.Space();
            DrawRule("Reel 기본 규칙", defaultRule, registry);
            EditorGUILayout.Space();
            DrawRule("합계 규칙", totalRule, registry);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reel별 규칙", EditorStyles.boldLabel);
            for (var i = 0; i < reelRules.arraySize; i++)
            {
                var entry = reelRules.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("reelId"), new GUIContent($"Reel ID {i + 1}"));
                    if (GUILayout.Button("삭제", GUILayout.Width(48f))) { reelRules.DeleteArrayElementAtIndex(i); break; }
                }
                DrawRule($"'{entry.FindPropertyRelative("reelId").stringValue}' 규칙", entry.FindPropertyRelative("rule"), registry);
                EditorGUILayout.Space();
            }
            if (GUILayout.Button("Reel별 규칙 추가")) reelRules.InsertArrayElementAtIndex(reelRules.arraySize);

            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            DrawOverallPreview((TemporaryFortuneCriticalConfiguration)target, registry);
        }

        // ---------- Rule fields ----------

        private static void DrawRule(string title, SerializedProperty rule, FortuneWeightFunctionRegistry registry)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                var chance = rule.FindPropertyRelative("chance");
                EditorGUILayout.PropertyField(chance, new GUIContent("발생 확률 p", chance.tooltip));
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("minimumMultiplier"), new GUIContent("최소 배수"));
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("maximumMultiplier"), new GUIContent("최대 배수"));
                var source = rule.FindPropertyRelative("distributionSource");
                EditorGUILayout.PropertyField(source, new GUIContent("분포 종류"));
                if (source.enumValueIndex == (int)FortuneDistributionSource.Table)
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("tableWeights"), new GUIContent("배수별 가중치 (최소 배수부터)"), true);
                else DrawFunction(rule, registry);
            }
            DrawRulePreview(rule, registry);
        }

        private static void DrawFunction(SerializedProperty rule, FortuneWeightFunctionRegistry registry)
        {
            var functionId = rule.FindPropertyRelative("functionId");
            var functions = registry.Functions;
            var current = -1;
            for (var i = 0; i < functions.Count; i++) if (functions[i].Id == functionId.stringValue.Trim()) current = i;
            var labels = functions.Select(f => $"{f.DisplayName} ({f.Id})").ToList();
            if (current < 0) labels.Add($"등록되지 않은 함수 '{functionId.stringValue}'");
            var selected = EditorGUILayout.Popup("가중치 함수", current < 0 ? labels.Count - 1 : current, labels.ToArray());
            if (selected < functions.Count && selected != current) functionId.stringValue = functions[selected].Id;
            if (current < 0 && selected >= functions.Count) return;
            var function = functions[selected];

            var parameters = rule.FindPropertyRelative("parameters");
            foreach (var parameter in function.Parameters)
            {
                var index = IndexOf(parameters, parameter.Name);
                var label = new GUIContent($"{parameter.Name} {parameter.RangeText}", parameter.Description);
                if (index < 0)
                {
                    // Not stored: the function default is used. Editing stores the value by name.
                    EditorGUI.BeginChangeCheck();
                    var value = EditorGUILayout.DoubleField(label, parameter.DefaultValue);
                    if (!EditorGUI.EndChangeCheck()) continue;
                    parameters.InsertArrayElementAtIndex(parameters.arraySize);
                    var added = parameters.GetArrayElementAtIndex(parameters.arraySize - 1);
                    added.FindPropertyRelative("name").stringValue = parameter.Name;
                    added.FindPropertyRelative("value").doubleValue = value;
                }
                else EditorGUILayout.PropertyField(parameters.GetArrayElementAtIndex(index).FindPropertyRelative("value"), label);
                EditorGUILayout.LabelField(" ", parameter.Description, EditorStyles.miniLabel);
            }
            for (var i = 0; i < parameters.arraySize; i++)
            {
                var name = parameters.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (function.Parameters.Any(p => p.Name == name?.Trim())) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox($"'{function.Id}'가 모르는 매개변수 '{name}'는 무시됩니다.", MessageType.Warning);
                    if (GUILayout.Button("제거", GUILayout.Width(48f))) { parameters.DeleteArrayElementAtIndex(i); break; }
                }
            }
        }

        private static int IndexOf(SerializedProperty parameters, string name)
        {
            for (var i = 0; i < parameters.arraySize; i++)
                if (string.Equals(parameters.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue?.Trim(), name, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        // ---------- Preview ----------

        private static void DrawRulePreview(SerializedProperty property, FortuneWeightFunctionRegistry registry)
        {
            var definition = property.boxedValue as TemporaryFortuneCriticalRuleDefinition;
            if (definition == null) return;
            if (!definition.TryCreateRule(registry, out var rule, out var reason))
            {
                EditorGUILayout.HelpBox($"이 규칙은 올바르지 않습니다: {reason}\n실행하면 Fortune Critical 전체가 꺼집니다.", MessageType.Error);
                return;
            }
            using (new EditorGUI.IndentLevelScope())
            {
                for (var n = rule.MinimumMultiplier; n <= rule.MaximumMultiplier; n++)
                {
                    var probability = rule.Table.Probability(n);
                    var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * .9f);
                    rect = EditorGUI.IndentedRect(rect);
                    var labelRect = new Rect(rect.x, rect.y, 110f, rect.height);
                    var barRect = new Rect(labelRect.xMax, rect.y + 2f, Mathf.Max(0f, rect.width - labelRect.width), rect.height - 4f);
                    GUI.Label(labelRect, $"×{n}   {probability * 100d:F2}%", EditorStyles.miniLabel);
                    EditorGUI.DrawRect(barRect, BarBackground);
                    EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * (float)probability, barRect.height), BarColor);
                }
                EditorGUILayout.LabelField("발생 시 평균 배수 E[n]", rule.ExpectedMultiplierWhenTriggered.ToString("F3"));
                EditorGUILayout.LabelField("기대 배수 1 + p(E[n] - 1)", rule.ExpectedMultiplier.ToString("F3"));
                if (rule.Table.UnreachableMultipliers.Count > 0)
                    EditorGUILayout.HelpBox($"확률이 백만분율 0으로 반올림되어 나오지 않는 배수: {string.Join(", ", rule.Table.UnreachableMultipliers.Select(m => $"×{m}"))}", MessageType.Warning);
                if (rule.Chance > 0d && rule.ChancePartsPerMillion == 0)
                    EditorGUILayout.HelpBox("발생 확률이 백만분율 0으로 반올림되어 실제로는 발생하지 않습니다.", MessageType.Warning);
            }
        }

        private static void DrawOverallPreview(TemporaryFortuneCriticalConfiguration configuration, FortuneWeightFunctionRegistry registry)
        {
            EditorGUILayout.LabelField("전체 미리보기", EditorStyles.boldLabel);
            if (!configuration.TryCreateRules(registry, out var rules, out var reason))
            {
                EditorGUILayout.HelpBox($"설정이 올바르지 않습니다: {reason}\n실행하면 Fortune Critical 없이 전투가 진행되고 오류가 기록됩니다.", MessageType.Error);
                return;
            }
            var reel = rules.DefaultReelRule.ExpectedMultiplier;
            var total = rules.TotalRule.ExpectedMultiplier;
            EditorGUILayout.LabelField("대략적 기대 배수 E[M_reel] × E[M_total]", (reel * total).ToString("F3"));
            EditorGUILayout.LabelField(" ", "Reel 기본 규칙과 합계 규칙 기준이며 유효 배수 상한은 무시한 값입니다.", EditorStyles.miniLabel);

            var largest = Math.Max(rules.DefaultReelRule.MaximumMultiplier, rules.TotalRule.MaximumMultiplier);
            foreach (var entry in rules.ReelRules) largest = Math.Max(largest, entry.Rule.MaximumMultiplier);
            if (rules.MaximumEffectiveMultiplier < largest)
                EditorGUILayout.HelpBox($"유효 배수 상한 ×{rules.MaximumEffectiveMultiplier}이 규칙의 최대 배수 ×{largest}보다 작습니다. 상한을 넘는 배수는 잘립니다.", MessageType.Warning);

            var slots = AssetDatabase.LoadAssetAtPath<TemporarySlotMachineConfiguration>(TemporarySlotMachineIntegration.SlotConfigurationPath);
            if (slots == null) return;
            var known = new HashSet<string>(slots.Reels.Where(r => r != null).Select(r => r.Id), StringComparer.Ordinal);
            var missing = rules.ReelRules.Where(r => !known.Contains(r.ReelId)).Select(r => r.ReelId).ToList();
            if (missing.Count > 0)
                EditorGUILayout.HelpBox($"현재 슬롯머신 설정에 없는 Reel ID: {string.Join(", ", missing)}. 실행 중 그 ID의 Reel이 추가되면 적용됩니다.", MessageType.Info);
        }
    }
}
