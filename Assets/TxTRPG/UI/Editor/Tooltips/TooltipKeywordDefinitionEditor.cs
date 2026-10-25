using System;
using System.Collections.Generic;
using System.Linq;
using TxTRPG.UI.Runes;
using TxTRPG.UI.Tooltips;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Editor.Tooltips
{
    // Where a keyword is used: the tooltip contents and other keywords whose Text blocks write {kw:id}, and the catalog
    // entries (rune definition IDs) that show those contents. Computed by TooltipKeywordUsageFinder.
    public sealed class TooltipKeywordUsage
    {
        public TooltipKeywordUsage(UnityEngine.Object asset, TooltipSubjectCategory category, string subjectId, string description)
        {
            Asset = asset;
            Category = category;
            SubjectId = subjectId ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public UnityEngine.Object Asset { get; }
        // Rune / Item / Skill for a catalog entry, Other for a content or keyword that is not (yet) shown by a catalog.
        public TooltipSubjectCategory Category { get; }
        public string SubjectId { get; }
        public string Description { get; }
    }

    // Read-only search over the project's tooltip assets. Extension point: when item or skill tooltip catalogs exist,
    // add their entries next to the RuneTooltipCatalog search (category Item or Skill).
    public static class TooltipKeywordUsageFinder
    {
        public static IReadOnlyList<TooltipKeywordUsage> Find(string keywordId)
        {
            var result = new List<TooltipKeywordUsage>();
            if (string.IsNullOrWhiteSpace(keywordId)) return result;
            keywordId = keywordId.Trim();
            var usingContents = new HashSet<TooltipContent>();
            foreach (var content in LoadAll<TooltipContent>())
            {
                if (!Uses(content.Blocks, keywordId)) continue;
                usingContents.Add(content);
                result.Add(new TooltipKeywordUsage(content, TooltipSubjectCategory.Other, string.Empty, $"내용 {content.name}"));
            }
            foreach (var keyword in LoadAll<TooltipKeywordDefinition>())
                if (keyword.Id != keywordId && Uses(keyword.Blocks, keywordId))
                    result.Add(new TooltipKeywordUsage(keyword, TooltipSubjectCategory.Other, string.Empty, $"키워드 {keyword.Id}"));
            foreach (var catalog in LoadAll<RuneTooltipCatalog>())
                foreach (var entry in catalog.Entries)
                    if (entry?.Content != null && usingContents.Contains(entry.Content))
                        result.Add(new TooltipKeywordUsage(catalog, TooltipSubjectCategory.Rune, entry.DefinitionId,
                            $"룬 {entry.DefinitionId} ({catalog.name} → {entry.Content.name})"));
            return result;
        }

        public static bool Uses(IReadOnlyList<TooltipBlock> blocks, string keywordId)
        {
            if (blocks == null) return false;
            foreach (var block in blocks)
                if (block != null && block.Kind == TooltipBlockKind.Text &&
                    TooltipMarkup.FindKeywordIds(block.Content).Contains(keywordId, StringComparer.Ordinal)) return true;
            return false;
        }

        private static IEnumerable<T> LoadAll<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null);
    }

    // Keyword Inspector: the default fields, then (read-only) the related entries grouped by category and the uses found
    // by 사용처 새로고침 (never computed while drawing). A related entry that is not used, or a use that is not a
    // related entry, is reported as information; nothing is changed or saved automatically.
    [CustomEditor(typeof(TooltipKeywordDefinition))]
    public sealed class TooltipKeywordDefinitionEditor : UnityEditor.Editor
    {
        private IReadOnlyList<TooltipKeywordUsage> usages;
        private string usagesFor;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var keyword = (TooltipKeywordDefinition)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("연관 항목", EditorStyles.boldLabel);
            var related = keyword.RelatedEntries.Where(entry => entry != null && entry.SubjectId.Length > 0).ToArray();
            if (related.Length == 0) EditorGUILayout.LabelField("없음");
            foreach (var group in related.GroupBy(entry => entry.Category).OrderBy(group => group.Key))
                EditorGUILayout.LabelField(CategoryName(group.Key), string.Join(", ", group.Select(entry => entry.SubjectId)));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("사용처", EditorStyles.boldLabel);
            if (GUILayout.Button("사용처 새로고침"))
            {
                usages = TooltipKeywordUsageFinder.Find(keyword.Id);
                usagesFor = keyword.Id;
            }
            if (usages == null || usagesFor != keyword.Id)
            {
                EditorGUILayout.HelpBox("사용처 새로고침을 누르면 이 키워드를 쓰는 내용·키워드·카탈로그 항목을 찾습니다.", MessageType.None);
                return;
            }
            if (usages.Count == 0) EditorGUILayout.LabelField("없음");
            foreach (var usage in usages)
            {
                if (GUILayout.Button($"{CategoryName(usage.Category)}: {usage.Description}", EditorStyles.linkLabel))
                {
                    EditorGUIUtility.PingObject(usage.Asset);
                    Selection.activeObject = usage.Asset;
                }
            }
            var used = new HashSet<(TooltipSubjectCategory, string)>(usages.Where(usage => usage.SubjectId.Length > 0).Select(usage => (usage.Category, usage.SubjectId)));
            var declared = new HashSet<(TooltipSubjectCategory, string)>(related.Select(entry => (entry.Category, entry.SubjectId)));
            var unused = declared.Where(entry => !used.Contains(entry)).Select(entry => $"{CategoryName(entry.Item1)} {entry.Item2}").ToArray();
            var undeclared = used.Where(entry => !declared.Contains(entry)).Select(entry => $"{CategoryName(entry.Item1)} {entry.Item2}").ToArray();
            if (unused.Length > 0) EditorGUILayout.HelpBox($"연관 항목이지만 사용하지 않음: {string.Join(", ", unused)}", MessageType.Info);
            if (undeclared.Length > 0) EditorGUILayout.HelpBox($"사용하지만 연관 항목에 없음: {string.Join(", ", undeclared)}", MessageType.Info);
        }

        private static string CategoryName(TooltipSubjectCategory category) => category switch
        {
            TooltipSubjectCategory.Rune => "룬",
            TooltipSubjectCategory.Item => "아이템",
            TooltipSubjectCategory.Skill => "스킬",
            TooltipSubjectCategory.Other => "기타",
            _ => "알 수 없음"
        };
    }
}
