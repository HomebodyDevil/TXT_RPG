using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using TxTRPG.Application.Players;
using TxTRPG.Application.Runes;
using TxTRPG.UI.DebugPanels;
using TxTRPG.UI.Encounters;
using TxTRPG.UI.Runes;
using TxTRPG.UI.Shapes;
using TxTRPG.UI.Tooltips;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY application menu (rune-tooltip-and-test-runes). Creates what is missing and edits stored assets in place:
    // 1. Assets: test rune icon (test.png, added to TemporaryRuneIconCatalog for rune.test.tooltip and rune.test.long),
    //    three test keywords and their catalog, rune tooltip contents (rune.attack and rune.heal are temporary texts) and
    //    TemporaryRuneTooltipCatalog, TemporaryTestRuneConfiguration.
    // 2. TooltipView.prefab (new). RuneGridCard.prefab: CardArea/HitArea, BadgeArea/HitArea and RuneGridCardTooltipTrigger.
    //    EncounterDebugPanel.prefab: a third tab 룬 with Pages/RunePage (RuneDebugTabView). AppRoot.prefab:
    //    PlayerSessionHost.temporaryTestRuneConfiguration. RuneGridPanel.prefab keeps its new fields empty.
    // 3. Candidate_MainScene: OverlayLayer/TooltipLayer (TooltipPresenter, last child), the RuneGrid instance's tooltip
    //    overrides and Canvas/TemporaryTestRuneDebugController.
    // Existing assets, objects and filled fields are kept ("kept"), so rerunning is safe and never overwrites user edits or
    // overrides; nothing is regenerated. It stops without changes in Play Mode, while compiling, in Prefab Stage or with
    // unsaved open Scenes. Not part of the Prefab rebuild batch (the Prefabs are developer-owned). Retire it once the user
    // has applied and verified the feature.
    public static class TemporaryRuneTooltipApply
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Apply Rune Tooltip And Test Runes";
        public const string LogPrefix = "RUNE_TOOLTIP_APPLY";
        public const string ScenePath = "Assets/Scenes/Candidate_MainScene.unity";
        public const string TooltipFolder = "Assets/TxTRPG/UI/Tooltips";
        public const string KeywordFolder = TooltipFolder + "/Keywords";
        public const string KeywordCatalogPath = TooltipFolder + "/TooltipKeywordCatalog.asset";
        public const string RuneContentFolder = TooltipFolder + "/Runes/Contents";
        public const string RuneTooltipCatalogPath = TooltipFolder + "/Runes/TemporaryRuneTooltipCatalog.asset";
        public const string TooltipViewPrefabPath = "Assets/TxTRPG/UI/Prefabs/Tooltips/TooltipView.prefab";
        public const string TestIconPath = TemporaryRuneGridApply.IconFolder + "/test.png";
        public const string TestRuneConfigurationPath = "Assets/TxTRPG/Application/Configuration/TemporaryTestRuneConfiguration.asset";
        public const string AppRootPrefabPath = "Assets/TxTRPG/SceneTransition/Prefabs/AppRoot.prefab";
        public const string AlphaKeywordId = "keyword.test.alpha";
        public const string BetaKeywordId = "keyword.test.beta";
        public const string GammaKeywordId = "keyword.test.gamma";
        public const string TooltipLayerName = "TooltipLayer";
        public const string ControllerName = "TemporaryTestRuneDebugController";
        public const string RuneTabLabel = "룬";
        public const float TitleFontSize = 26f;
        public const float BodyFontSize = 22f;

        private static readonly Color BackgroundColor = new Color32(0x14, 0x1C, 0x33, 0xF7);
        private static readonly Color BorderColor = new Color32(0x8F, 0xA6, 0xD9, 0xFF);
        private static readonly Color SeparatorColor = new Color32(0x8F, 0xA6, 0xD9, 0x80);
        private static readonly Color TestIconColor = new Color32(0xB4, 0x8C, 0xFF, 0xFF);

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (!CanApply(out var reason)) { Debug.LogWarning($"{LogPrefix}: stopped without changes. {reason}"); return; }
            var summary = new StringBuilder($"{LogPrefix}:\n");
            try
            {
                var icon = EnsureTestIcon(summary);
                EnsureIconCatalogEntries(icon, summary);
                var keywords = EnsureKeywords(icon, summary);
                EnsureRuneTooltips(icon, summary);
                EnsureTestRuneConfiguration(summary);
                AssetDatabase.SaveAssets();
                var view = EnsureTooltipViewPrefab(summary);
                ApplyCardPrefab(summary);
                ApplyDebugPanelPrefab(summary);
                ApplyAppRoot(summary);
                ApplyScene(view, keywords, summary);
                summary.Append(" done");
                Debug.Log(summary.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError($"{summary}{LogPrefix}: failed. {exception}");
            }
        }

        private static bool CanApply(out string reason)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { reason = "Leave Play Mode first."; return false; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { reason = "Wait for compilation to finish."; return false; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) { reason = "Close the Prefab Stage first."; return false; }
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty) { reason = $"Save or revert the open Scene '{scene.path}' first."; return false; }
            }
            reason = string.Empty;
            return true;
        }

        // ── Assets ──

        private static Sprite EnsureTestIcon(StringBuilder summary)
        {
            if (!File.Exists(TestIconPath))
            {
                TemporaryRuneGridApply.WriteIcon(TestIconPath, TestIconColor, Flask);
                summary.AppendLine($" created {TestIconPath} (temporary test rune icon)");
            }
            else summary.AppendLine($" kept {TestIconPath}");
            return AssetDatabase.LoadAssetAtPath<Sprite>(TestIconPath) ?? throw new InvalidOperationException($"{TestIconPath} is not imported as a Sprite.");
        }

        // Round-bottomed flask: neck, rim and bulb.
        private static bool Flask(float x, float y)
        {
            if (Mathf.Abs(x) <= 11f && y >= 4f && y <= 42f) return true;
            if (Mathf.Abs(x) <= 19f && y > 42f && y <= 50f) return true;
            return x * x + (y + 18f) * (y + 18f) <= 38f * 38f;
        }

        private static void EnsureIconCatalogEntries(Sprite icon, StringBuilder summary)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RuneIconCatalog>(TemporaryRuneGridApply.CatalogPath)
                ?? throw new InvalidOperationException($"{TemporaryRuneGridApply.CatalogPath} is missing (run Apply Candidate Rune Grid first).");
            var entries = catalog.Entries.ToList();
            var added = new[] { TemporaryTestRuneConfiguration.TooltipRuneId, TemporaryTestRuneConfiguration.LongRuneId }
                .Where(id => entries.All(entry => entry?.DefinitionId != id)).ToArray();
            if (added.Length == 0) { summary.AppendLine($" kept {TemporaryRuneGridApply.CatalogPath}"); return; }
            entries.AddRange(added.Select(id => new RuneIconCatalog.Entry(id, icon)));
            catalog.SetEntries(entries);
            EditorUtility.SetDirty(catalog);
            summary.AppendLine($" updated {TemporaryRuneGridApply.CatalogPath} (+{string.Join(", ", added)})");
        }

        private static TooltipKeywordCatalog EnsureKeywords(Sprite icon, StringBuilder summary)
        {
            var alpha = EnsureKeyword("TestKeywordAlpha", AlphaKeywordId, "알파", icon, new[]
            {
                TooltipBlock.Text($"테스트용 첫 번째 키워드입니다. 다음 단계는 {{kw:{BetaKeywordId}}}입니다."),
                TooltipBlock.Separator(),
                TooltipBlock.Text("이 키워드에만 아이콘이 있습니다.")
            }, summary);
            var beta = EnsureKeyword("TestKeywordBeta", BetaKeywordId, "베타", null, new[]
            {
                TooltipBlock.Text($"테스트용 두 번째 키워드입니다. 이어서 {{kw:{GammaKeywordId}}}에 마우스를 올려 보세요.")
            }, summary);
            var gamma = EnsureKeyword("TestKeywordGamma", GammaKeywordId, "감마", null, new[]
            {
                TooltipBlock.Text($"테스트용 세 번째 키워드입니다. {{kw:{AlphaKeywordId}}}는 이미 열려 있으므로 다시 열리지 않습니다."),
                TooltipBlock.Image(icon, 64f)
            }, summary);
            var catalog = AssetDatabase.LoadAssetAtPath<TooltipKeywordCatalog>(KeywordCatalogPath);
            if (catalog != null) { summary.AppendLine($" kept {KeywordCatalogPath}"); return catalog; }
            catalog = ScriptableObject.CreateInstance<TooltipKeywordCatalog>();
            catalog.SetKeywords(new[] { alpha, beta, gamma });
            AssetDatabase.CreateAsset(catalog, KeywordCatalogPath);
            summary.AppendLine($" created {KeywordCatalogPath}");
            return catalog;
        }

        private static TooltipKeywordDefinition EnsureKeyword(string assetName, string id, string displayName, Sprite icon,
            TooltipBlock[] blocks, StringBuilder summary)
        {
            var path = $"{KeywordFolder}/{assetName}.asset";
            var keyword = AssetDatabase.LoadAssetAtPath<TooltipKeywordDefinition>(path);
            if (keyword != null) { summary.AppendLine($" kept {path}"); return keyword; }
            EnsureFolder(KeywordFolder);
            keyword = ScriptableObject.CreateInstance<TooltipKeywordDefinition>();
            keyword.Configure(id, displayName, icon, blocks, new[] { new TooltipRelatedEntry(TooltipSubjectCategory.Rune, TemporaryTestRuneConfiguration.TooltipRuneId) });
            AssetDatabase.CreateAsset(keyword, path);
            summary.AppendLine($" created {path}");
            return keyword;
        }

        private static void EnsureRuneTooltips(Sprite icon, StringBuilder summary)
        {
            var attack = EnsureContent("TemporaryAttackRuneTooltip", summary, TooltipBlock.Title("공격 룬"),
                TooltipBlock.Text("슬롯 총합에서 받은 몫만큼 적에게 피해를 줍니다. (임시 문구)"));
            var heal = EnsureContent("TemporaryHealRuneTooltip", summary, TooltipBlock.Title("회복 룬"),
                TooltipBlock.Text("슬롯 총합에서 받은 몫만큼 체력을 회복합니다. (임시 문구)"));
            var test = EnsureContent("TestRuneTooltip", summary, TooltipBlock.Title("테스트 룬"), TooltipBlock.Image(icon, 96f),
                TooltipBlock.Text($"분배와 전투에 참여하지 않는 개발용 룬입니다. {{kw:{AlphaKeywordId}}}에 마우스를 올려 키워드 툴팁을 확인하세요."),
                TooltipBlock.Separator(),
                TooltipBlock.Text($"표시 텍스트 지정: {{kw:{BetaKeywordId}|두 번째 키워드}}, 중괄호 표기: {{{{예시}}}}"));
            var paragraphs = Enumerable.Range(1, 14).Select(i =>
                $"{i}번째 문단입니다. 긴 본문이 화면 프레임의 최대 높이를 넘으면 툴팁 안에서 잘리고, 아래쪽이 넘치면 기준점을 뒤집거나 프레임 안으로 밀어 넣습니다.");
            var longContent = EnsureContent("LongTestRuneTooltip", summary, TooltipBlock.Title("긴 테스트 룬"),
                TooltipBlock.Text(string.Join("\n\n", paragraphs)), TooltipBlock.Separator(),
                TooltipBlock.Text($"마지막 문단의 키워드: {{kw:{GammaKeywordId}}}"));
            var catalog = AssetDatabase.LoadAssetAtPath<RuneTooltipCatalog>(RuneTooltipCatalogPath);
            if (catalog != null) { summary.AppendLine($" kept {RuneTooltipCatalogPath}"); return; }
            catalog = ScriptableObject.CreateInstance<RuneTooltipCatalog>();
            catalog.SetEntries(new[]
            {
                new RuneTooltipCatalog.Entry(TemporaryRuneConfiguration.AttackRuneId, attack),
                new RuneTooltipCatalog.Entry(TemporaryRuneConfiguration.HealRuneId, heal),
                new RuneTooltipCatalog.Entry(TemporaryTestRuneConfiguration.TooltipRuneId, test),
                new RuneTooltipCatalog.Entry(TemporaryTestRuneConfiguration.LongRuneId, longContent)
            });
            AssetDatabase.CreateAsset(catalog, RuneTooltipCatalogPath);
            summary.AppendLine($" created {RuneTooltipCatalogPath} (rune.test.missing has no entry: fallback tooltip)");
        }

        private static TooltipContent EnsureContent(string assetName, StringBuilder summary, params TooltipBlock[] blocks)
        {
            var path = $"{RuneContentFolder}/{assetName}.asset";
            var content = AssetDatabase.LoadAssetAtPath<TooltipContent>(path);
            if (content != null) { summary.AppendLine($" kept {path}"); return content; }
            EnsureFolder(RuneContentFolder);
            content = ScriptableObject.CreateInstance<TooltipContent>();
            content.SetBlocks(blocks);
            AssetDatabase.CreateAsset(content, path);
            summary.AppendLine($" created {path}");
            return content;
        }

        private static void EnsureTestRuneConfiguration(StringBuilder summary)
        {
            if (AssetDatabase.LoadAssetAtPath<TemporaryTestRuneConfiguration>(TestRuneConfigurationPath) != null)
            { summary.AppendLine($" kept {TestRuneConfigurationPath}"); return; }
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TemporaryTestRuneConfiguration>(), TestRuneConfigurationPath);
            summary.AppendLine($" created {TestRuneConfigurationPath} (rune.test.tooltip, rune.test.long, rune.test.missing)");
        }

        // ── TooltipView.prefab ──

        private static TooltipView EnsureTooltipViewPrefab(StringBuilder summary)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TooltipViewPrefabPath);
            if (existing != null)
            {
                summary.AppendLine($" kept {TooltipViewPrefabPath}");
                return existing.GetComponent<TooltipView>() ?? throw new InvalidOperationException($"{TooltipViewPrefabPath} has no TooltipView.");
            }
            EnsureFolder(Path.GetDirectoryName(TooltipViewPrefabPath).Replace('\\', '/'));
            var root = TemporaryDebugPanelTabsApply.NewUi("TooltipView", null);
            try
            {
                root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
                root.pivot = new Vector2(0f, 1f);
                root.sizeDelta = new Vector2(TooltipView.DefaultMaximumWidth, 200f);
                root.gameObject.AddComponent<CanvasGroup>();
                var visual = TemporaryDebugPanelTabsApply.Stretch(TemporaryDebugPanelTabsApply.NewUi("VisualRoot", root));
                var background = TemporaryDebugPanelTabsApply.Stretch(TemporaryDebugPanelTabsApply.NewUi("Background", visual)).gameObject.AddComponent<UiShapeGraphic>();
                background.Configure(UiShape.RoundedRectangle, 10f, UiShapeDrawMode.Fill, 0f);
                background.color = BackgroundColor;
                background.raycastTarget = true;
                var border = TemporaryDebugPanelTabsApply.Stretch(TemporaryDebugPanelTabsApply.NewUi("Border", visual)).gameObject.AddComponent<UiShapeGraphic>();
                border.Configure(UiShape.RoundedRectangle, 10f, UiShapeDrawMode.InnerBorder, 2f);
                border.color = BorderColor;
                border.raycastTarget = false;

                var content = TemporaryDebugPanelTabsApply.Stretch(TemporaryDebugPanelTabsApply.NewUi("Content", visual));
                content.gameObject.AddComponent<RectMask2D>();
                var blocks = TemporaryDebugPanelTabsApply.NewUi("Blocks", content);
                blocks.anchorMin = new Vector2(0f, 1f);
                blocks.anchorMax = Vector2.one;
                blocks.pivot = new Vector2(.5f, 1f);
                blocks.sizeDelta = Vector2.zero;
                var layout = blocks.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(16, 16, 16, 16);
                layout.spacing = 8f;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                blocks.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var title = BlockText(TemporaryDebugPanelTabsApply.NewUi("TitleTemplate", blocks), TitleFontSize, FontStyles.Bold, false);
                var textRect = TemporaryDebugPanelTabsApply.NewUi("TextTemplate", blocks);
                var text = BlockText(textRect, BodyFontSize, FontStyles.Normal, true);
                var detector = textRect.gameObject.AddComponent<TooltipKeywordLinkDetector>();
                detector.ConfigureForEditor(text);
                var image = TemporaryDebugPanelTabsApply.NewUi("ImageTemplate", blocks).gameObject.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.gameObject.AddComponent<LayoutElement>().preferredHeight = TooltipBlock.DefaultImageHeight;
                var separator = TemporaryDebugPanelTabsApply.NewUi("SeparatorTemplate", blocks).gameObject.AddComponent<Image>();
                separator.color = SeparatorColor;
                separator.raycastTarget = false;
                var separatorLayout = separator.gameObject.AddComponent<LayoutElement>();
                separatorLayout.minHeight = separatorLayout.preferredHeight = 2f;
                foreach (Transform template in blocks) template.gameObject.SetActive(false);

                var view = root.gameObject.AddComponent<TooltipView>();
                view.ConfigureForEditor(visual, background, layout, title, detector, image, separator);
                var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, TooltipViewPrefabPath, out var success);
                if (!success) throw new InvalidOperationException($"Could not save {TooltipViewPrefabPath}.");
                summary.AppendLine($" created {TooltipViewPrefabPath}");
                return saved.GetComponent<TooltipView>();
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        private static TextMeshProUGUI BlockText(RectTransform rect, float size, FontStyles style, bool raycast)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = true;
            text.raycastTarget = raycast;
            return text;
        }

        // ── RuneGridCard.prefab ──

        private static void ApplyCardPrefab(StringBuilder summary)
        {
            var root = PrefabUtility.LoadPrefabContents(TemporaryRuneGridApply.CardPrefabPath);
            try
            {
                var card = root.GetComponent<RuneGridCardView>() ?? throw new InvalidOperationException("RuneGridCardView is missing.");
                if (!card.IsValid(out var missing)) throw new InvalidOperationException($"RuneGridCardView is incomplete: {missing}.");
                var changes = new StringBuilder();
                EnsureHitArea(card.CardArea, changes);
                EnsureHitArea(card.BadgeArea, changes);
                var trigger = root.GetComponent<RuneGridCardTooltipTrigger>();
                if (trigger == null)
                {
                    trigger = root.AddComponent<RuneGridCardTooltipTrigger>();
                    trigger.ConfigureForEditor(card);
                    changes.Append(" RuneGridCardTooltipTrigger");
                }
                if (card.TooltipTrigger != trigger)
                {
                    card.SetTooltipTriggerForEditor(trigger);
                    changes.Append(" tooltipTrigger");
                }
                if (changes.Length == 0) { summary.AppendLine($" kept {TemporaryRuneGridApply.CardPrefabPath}"); return; }
                PrefabUtility.SaveAsPrefabAsset(root, TemporaryRuneGridApply.CardPrefabPath, out var saved);
                if (!saved) throw new InvalidOperationException($"Could not save {TemporaryRuneGridApply.CardPrefabPath}.");
                summary.AppendLine($" updated {TemporaryRuneGridApply.CardPrefabPath} ({changes.ToString().Trim()})");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void EnsureHitArea(RectTransform area, StringBuilder changes)
        {
            if (area.Find(TemporaryRuneGridApply.HitAreaName) != null) return;
            var hit = TemporaryDebugPanelTabsApply.Stretch(TemporaryDebugPanelTabsApply.NewUi(TemporaryRuneGridApply.HitAreaName, area));
            hit.SetAsLastSibling();
            var image = hit.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            changes.Append($" {area.name}/{TemporaryRuneGridApply.HitAreaName}");
        }

        // ── EncounterDebugPanel.prefab ──

        private static void ApplyDebugPanelPrefab(StringBuilder summary)
        {
            var path = TemporaryDebugPanelTabsApply.DebugPanelPrefabPath;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var panel = root.GetComponent<EncounterDebugPanel>() ?? throw new InvalidOperationException("EncounterDebugPanel is missing.");
                var group = root.GetComponent<DebugPanelTabGroup>() ?? throw new InvalidOperationException("DebugPanelTabGroup is missing (apply the debug panel tabs first).");
                var body = panel.Body != null ? panel.Body.transform : throw new InvalidOperationException("EncounterDebugPanel has no Body.");
                if (body.Find("Pages/RunePage") != null) { summary.AppendLine($" kept {path}"); return; }
                var tabBar = body.Find("TabBar") ?? throw new InvalidOperationException("Body/TabBar is missing.");
                var pages = body.Find("Pages") ?? throw new InvalidOperationException("Body/Pages is missing.");
                var encounterPage = pages.Find("EncounterPage") as RectTransform ?? throw new InvalidOperationException("Pages/EncounterPage is missing.");

                var runeTab = TemporaryDebugPanelTabsApply.NewTab(tabBar, "RuneTab", RuneTabLabel, out var label, out var indicator);
                indicator.SetActive(false);
                var view = BuildRunePage(pages, encounterPage, runeTab);
                view.gameObject.SetActive(false);
                var tabs = group.Tabs.ToList();
                tabs.Add(new DebugPanelTab(runeTab, label, indicator, view.gameObject, view.RemoveAllButton));
                var initial = new SerializedObject(group).FindProperty("initialIndex").intValue;
                group.ConfigureForEditor(tabs, initial);
                EditorUtility.SetDirty(group);

                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved) throw new InvalidOperationException($"Could not save {path}.");
                summary.AppendLine($" updated {path} (TabBar/RuneTab, Pages/RunePage)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static RuneDebugTabView BuildRunePage(Transform pages, RectTransform encounterPage, Selectable navigationUp)
        {
            var page = TemporaryDebugPanelTabsApply.NewUi("RunePage", pages);
            TemporaryDebugPanelTabsApply.Column(page.gameObject, 0, 6f);
            var pageLayout = page.gameObject.AddComponent<LayoutElement>();
            var removeAll = TemporaryDebugPanelTabsApply.NewButton(page, "RemoveAllButton", "모두 제거", -1f);

            var rows = TemporaryDebugPanelTabsApply.NewUi("Rows", page);
            TemporaryDebugPanelTabsApply.Column(rows.gameObject, 0, 6f);
            var template = TemporaryDebugPanelTabsApply.NewUi("RowTemplate", rows);
            TemporaryDebugPanelTabsApply.Row(template.gameObject, 48f);
            var labels = TemporaryDebugPanelTabsApply.NewUi("Labels", template);
            TemporaryDebugPanelTabsApply.Column(labels.gameObject, 0, 0f);
            TemporaryDebugPanelTabsApply.Flexible(labels.gameObject, 1f);
            var nameText = TemporaryDebugPanelTabsApply.Text(TemporaryDebugPanelTabsApply.NewUi("Name", labels), "테스트 룬", 12f, 16f);
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            var idText = TemporaryDebugPanelTabsApply.Text(TemporaryDebugPanelTabsApply.NewUi("Id", labels), "rune.test.", 10f, 13f);
            idText.alignment = TextAlignmentOptions.MidlineLeft;
            idText.color = new Color(1f, 1f, 1f, .7f);
            idText.textWrappingMode = TextWrappingModes.NoWrap;
            idText.overflowMode = TextOverflowModes.Ellipsis;
            var minus = TemporaryDebugPanelTabsApply.NewButton(template, "MinusButton", "−", 56f);
            var count = TemporaryDebugPanelTabsApply.Text(TemporaryDebugPanelTabsApply.NewUi("CountText", template), "0", 12f, 16f);
            TemporaryDebugPanelTabsApply.Fixed(count.gameObject, 40f);
            var plus = TemporaryDebugPanelTabsApply.NewButton(template, "PlusButton", "+", 56f);
            var row = template.gameObject.AddComponent<RuneDebugRow>();
            row.ConfigureForEditor(nameText, idText, count, minus, plus);
            template.gameObject.SetActive(false);

            var result = TemporaryDebugPanelTabsApply.Text(TemporaryDebugPanelTabsApply.NewUi("ResultText", page), string.Empty, 14f, 14f);
            result.enableAutoSizing = false;
            result.fontSize = 15f;
            result.alignment = TextAlignmentOptions.TopLeft;
            result.textWrappingMode = TextWrappingModes.Normal;
            result.gameObject.AddComponent<LayoutElement>().minHeight = 40f;

            var view = page.gameObject.AddComponent<RuneDebugTabView>();
            view.ConfigureForEditor(removeAll, rows, row, result, pageLayout, encounterPage, navigationUp);
            return view;
        }

        // ── AppRoot.prefab ──

        private static void ApplyAppRoot(StringBuilder summary)
        {
            var configuration = AssetDatabase.LoadAssetAtPath<TemporaryTestRuneConfiguration>(TestRuneConfigurationPath)
                ?? throw new InvalidOperationException($"{TestRuneConfigurationPath} is missing.");
            var root = PrefabUtility.LoadPrefabContents(AppRootPrefabPath);
            try
            {
                var host = root.GetComponentInChildren<PlayerSessionHost>(true) ?? throw new InvalidOperationException("AppRoot has no PlayerSessionHost.");
                var serialized = new SerializedObject(host);
                var property = serialized.FindProperty("temporaryTestRuneConfiguration");
                if (property.objectReferenceValue != null) { summary.AppendLine($" kept {AppRootPrefabPath}"); return; }
                property.objectReferenceValue = configuration;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, AppRootPrefabPath, out var saved);
                if (!saved) throw new InvalidOperationException($"Could not save {AppRootPrefabPath}.");
                summary.AppendLine($" updated {AppRootPrefabPath} (temporaryTestRuneConfiguration)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ── Candidate_MainScene ──

        private static void ApplyScene(TooltipView viewPrefab, TooltipKeywordCatalog keywords, StringBuilder summary)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var panel = FindInScene<EncounterDebugPanel>(scene) ?? throw new InvalidOperationException("Candidate_MainScene has no EncounterDebugPanel.");
            var overlay = panel.transform.parent as RectTransform ?? throw new InvalidOperationException("EncounterDebugPanel has no RectTransform parent.");
            var frame = overlay.parent as RectTransform;
            if (frame == null || frame.name != "ScreenFrame") throw new InvalidOperationException($"Expected ScreenFrame/{overlay.name}, found {frame?.name}/{overlay.name}.");
            var changed = false;

            var presenter = overlay.Find(TooltipLayerName)?.GetComponent<TooltipPresenter>();
            if (presenter != null) summary.AppendLine($" kept {overlay.name}/{TooltipLayerName}");
            else
            {
                if (overlay.Find(TooltipLayerName) != null) throw new InvalidOperationException($"{overlay.name}/{TooltipLayerName} exists without TooltipPresenter.");
                var layer = TemporaryDebugPanelTabsApply.Stretch(TemporaryDebugPanelTabsApply.NewUi(TooltipLayerName, overlay));
                layer.SetAsLastSibling();
                presenter = layer.gameObject.AddComponent<TooltipPresenter>();
                presenter.ConfigureForEditor(viewPrefab, keywords, frame);
                summary.AppendLine($" added {frame.name}/{overlay.name}/{TooltipLayerName}");
                changed = true;
            }

            var grid = FindInScene<RuneGridPanel>(scene) ?? throw new InvalidOperationException("Candidate_MainScene has no RuneGridPanel.");
            var gridObject = new SerializedObject(grid);
            var gridChanges = string.Empty;
            var presenterProperty = gridObject.FindProperty("tooltipPresenter");
            if (presenterProperty.objectReferenceValue == null) { presenterProperty.objectReferenceValue = presenter; gridChanges += " tooltipPresenter"; }
            var catalogProperty = gridObject.FindProperty("tooltipCatalog");
            if (catalogProperty.objectReferenceValue == null)
            {
                catalogProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<RuneTooltipCatalog>(RuneTooltipCatalogPath);
                gridChanges += " tooltipCatalog";
            }
            if (gridChanges.Length > 0)
            {
                gridObject.ApplyModifiedPropertiesWithoutUndo();
                summary.AppendLine($" set {grid.name} overrides:{gridChanges}");
                changed = true;
            }
            else summary.AppendLine($" kept {grid.name} tooltip overrides");

            var controller = FindInScene<TemporaryTestRuneDebugController>(scene);
            if (controller != null) summary.AppendLine($" kept {ControllerName}");
            else
            {
                var runeTab = panel.GetComponentInChildren<RuneDebugTabView>(true) ?? throw new InvalidOperationException("The debug panel has no RuneDebugTabView (apply the Prefab first).");
                var canvas = panel.GetComponentInParent<Canvas>()?.rootCanvas ?? throw new InvalidOperationException("EncounterDebugPanel is not under a Canvas.");
                var gameObject = new GameObject(ControllerName);
                gameObject.transform.SetParent(canvas.transform, false);
                gameObject.AddComponent<TemporaryTestRuneDebugController>().Configure(runeTab);
                summary.AppendLine($" added Canvas/{ControllerName}");
                changed = true;
            }

            if (!changed) return;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException($"Could not save {ScenePath}.");
            summary.AppendLine($" saved {ScenePath}");
        }

        private static T FindInScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
