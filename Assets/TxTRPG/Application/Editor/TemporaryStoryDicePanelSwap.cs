using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TxTRPG.UI;
using TxTRPG.UI.Dice;
using TxTRPG.UI.Exploration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY one-off application menus (main-scene-story-dice-panel-swap). They adapt TMP_MainScene after the user swapped
    // StoryTextPanel and OwnedDicePanel. Apply is safe to rerun: it only sets the listed values and saves the Scene.
    // Measure only reads Play Mode layout. Retire both once every working copy has been migrated.
    public static class TemporaryStoryDicePanelSwap
    {
        public const string ApplyMenuPath = "Tools/TxT RPG/Application/Temporary/Apply Story Dice Panel Swap Settings";
        public const string MeasureMenuPath = "Tools/TxT RPG/Application/Temporary/Measure Story Dice Panel Swap Layout (Play Mode)";

        // Decided from the Measure menu (see DOCS/architecture/story-text-panel.md#너비-기반-글자-크기).
        public const float StoryReferenceWidth = 942f;
        public const float StoryMinimumBodyFontSize = 18f;
        public const float StoryMinimumSpeakerFontSize = 14f;
        public static readonly float[] NarrowHeights = { 760f, 520f, 640f };

        private static readonly (string Name, Vector2 Size)[] Viewports =
        {
            ("desktop 1920x1080", new Vector2(1920f, 1080f)),
            ("ultrawide", new Vector2(2560f, 1080f)),
            ("narrow desktop", new Vector2(1024f, 640f)),
            ("tablet", new Vector2(768f, 1024f)),
            ("phone portrait", new Vector2(390f, 844f)),
            ("phone landscape", new Vector2(844f, 390f))
        };

        [MenuItem(ApplyMenuPath)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            TemporarySlotMachineIntegration.ThrowIfAppliedToMainScene(ApplyMenuPath);
            var scene = SceneManager.GetActiveScene();
            if (scene.path != TemporaryDiceMenuBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved TMP_MainScene in Edit Mode without unsaved changes.");
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();

            var story = components.OfType<StoryTextPanel>().Single();
            var storySettings = new SerializedObject(story);
            storySettings.FindProperty("scaleTextWithWidth").boolValue = true;
            storySettings.FindProperty("textScaleReferenceWidth").floatValue = StoryReferenceWidth;
            storySettings.FindProperty("minimumBodyFontSize").floatValue = StoryMinimumBodyFontSize;
            storySettings.FindProperty("minimumSpeakerFontSize").floatValue = StoryMinimumSpeakerFontSize;
            storySettings.ApplyModifiedPropertiesWithoutUndo();
            Record(story);

            var dice = components.OfType<OwnedDicePanel>().Single();
            var enemy = components.OfType<EnemyDisplayPanel>().Single();
            var exploration = components.OfType<Transform>().Single(item => item.name == TemporaryDiceMenuBuilder.ExplorationPanelName);
            var column = dice.transform.parent;
            var enemyRoot = enemy.transform;
            while (enemyRoot != null && enemyRoot.parent != column) enemyRoot = enemyRoot.parent;
            if (enemyRoot == null || exploration.parent != column)
                throw new InvalidOperationException("EnemyDisplayPanel, OwnedDicePanel, and ExplorationNodePanel must share one ContentLayer.");
            var blocker = exploration.GetComponent<CoveredPanelInteractionBlocker>() ?? exploration.gameObject.AddComponent<CoveredPanelInteractionBlocker>();
            var covered = new[] { EnsureCanvasGroup(enemyRoot.gameObject), EnsureCanvasGroup(dice.gameObject) };
            blocker.ConfigureForEditor(covered);
            EditorUtility.SetDirty(blocker);
            Record(blocker);

            var responsive = components.OfType<ResponsiveMainScreen>().Single();
            var responsiveSettings = new SerializedObject(responsive);
            var heights = responsiveSettings.FindProperty("narrowHeights");
            if (heights.arraySize != NarrowHeights.Length) throw new InvalidOperationException("Unexpected narrowHeights length.");
            for (var i = 0; i < NarrowHeights.Length; i++) heights.GetArrayElementAtIndex(i).floatValue = NarrowHeights[i];
            responsiveSettings.ApplyModifiedPropertiesWithoutUndo();
            Record(responsive);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save TMP_MainScene.");
            Debug.Log($"STORY_DICE_SWAP_APPLY: Story width text size (reference {StoryReferenceWidth}, min {StoryMinimumBodyFontSize}/{StoryMinimumSpeakerFontSize}), covered-panel blocker on {exploration.name} ({enemyRoot.name}, {dice.name}), narrowHeights [{string.Join(", ", NarrowHeights)}]. Scene saved. Safe to rerun.");
        }

        [MenuItem(MeasureMenuPath)]
        public static async void Measure()
        {
            var log = new StringBuilder("STORY_DICE_SWAP_MEASURE:");
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
                var story = UnityEngine.Object.FindAnyObjectByType<StoryTextPanel>();
                var dice = UnityEngine.Object.FindAnyObjectByType<OwnedDicePanel>();
                var enemy = UnityEngine.Object.FindAnyObjectByType<EnemyDisplayPanel>();
                var screen = UnityEngine.Object.FindAnyObjectByType<ResponsiveMainScreen>();
                var tree = UnityEngine.Object.FindAnyObjectByType<ExplorationNodeTreePanel>();
                if (story == null || dice == null || enemy == null || screen == null)
                    throw new InvalidOperationException("Main scene panels are missing.");
                var storyContent = (RectTransform)new SerializedObject(story).FindProperty("content").objectReferenceValue;
                var storyViewport = (RectTransform)new SerializedObject(story).FindProperty("viewport").objectReferenceValue;
                var body = story.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault(text => text.name == "Body");
                var viewport = screen.PageScroll.viewport;
                var saved = (viewport.anchorMin, viewport.anchorMax, viewport.sizeDelta, viewport.anchoredPosition);
                try
                {
                    foreach (var (name, size) in Viewports)
                    {
                        viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
                        viewport.sizeDelta = size; viewport.anchoredPosition = Vector2.zero;
                        await Task.Delay(250);
                        Canvas.ForceUpdateCanvases();
                        var lineHeight = 0f;
                        if (body != null && body.font != null)
                        {
                            var face = body.font.faceInfo;
                            lineHeight = face.lineHeight / face.pointSize * story.EffectiveBodyFontSize;
                        }
                        var lines = lineHeight > 0f ? storyViewport.rect.height / lineHeight : 0f;
                        var column = (RectTransform)dice.transform.parent.parent;
                        var results = dice.ResultsButton != null ? (RectTransform)dice.ResultsButton.transform : null;
                        log.AppendLine().Append($" {name}: narrow={screen.IsNarrow} storyPanel={((RectTransform)story.transform).rect.size} storyMessageWidth={storyContent.rect.width:0.#} storyViewportHeight={storyViewport.rect.height:0.#} ")
                            .Append($"font={story.EffectiveBodyFontSize:0.##}/{story.EffectiveSpeakerFontSize:0.##} separator={story.EffectiveSeparatorWidth:0.#} bodyLines~{lines:0.#} ")
                            .Append($"tree={(tree != null ? ((RectTransform)tree.transform).rect.size.ToString() : "-")} enemy={EnemyRoot(enemy, dice).rect.size} dice={((RectTransform)dice.transform).rect.size} middle={column.rect.size} left={((RectTransform)story.transform.parent.parent).rect.size}")
                            .Append(results != null ? $" results={results.rect.size}" : string.Empty);
                    }
                }
                finally
                {
                    viewport.anchorMin = saved.anchorMin; viewport.anchorMax = saved.anchorMax;
                    viewport.sizeDelta = saved.sizeDelta; viewport.anchoredPosition = saved.anchoredPosition;
                }
                Debug.Log(log.AppendLine().Append(" DONE").ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError(log.Append(" FAIL: ").Append(exception.Message).ToString());
            }
        }

        public const string ValidateMenuPath = "Tools/TxT RPG/Application/Temporary/Validate Story Dice Panel Swap (Play Mode)";

        // Requires a fresh awaiting-choice run (enter Play Mode from AppScene and wait for TMP_MainScene).
        [MenuItem(ValidateMenuPath)]
        public static async void ValidatePlay()
        {
            var log = new StringBuilder("STORY_DICE_SWAP_VALIDATE:");
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
                var run = UnityEngine.Object.FindAnyObjectByType<TxTRPG.Application.Exploration.ExplorationRunController>() ?? throw new InvalidOperationException("ExplorationRunController is missing.");
                var blocker = UnityEngine.Object.FindAnyObjectByType<CoveredPanelInteractionBlocker>(FindObjectsInactive.Include) ?? throw new InvalidOperationException("CoveredPanelInteractionBlocker is missing.");
                var dice = UnityEngine.Object.FindAnyObjectByType<OwnedDicePanel>();
                var story = UnityEngine.Object.FindAnyObjectByType<StoryTextPanel>();
                var screen = UnityEngine.Object.FindAnyObjectByType<ResponsiveMainScreen>();
                if (run.Run == null || run.Run.Phase != TxTRPG.Gameplay.Exploration.ExplorationPhase.AwaitingChoice)
                    throw new InvalidOperationException("Validation requires a fresh awaiting-choice exploration run.");

                // S9: while choosing a node, no reachable selectable may sit inside the covered panels.
                await Frames(2);
                Require(blocker.gameObject.activeInHierarchy && blocker.IsBlocking, "blocker active while choosing");
                var groups = CoveredGroups(blocker);
                Require(groups.Length == 2 && groups.All(group => !group.interactable), "covered panels are non-interactable while choosing");
                var reachable = Selectable.allSelectablesArray.Where(item => item.IsInteractable() && item.isActiveAndEnabled).ToArray();
                Require(!reachable.Any(item => groups.Any(group => item.transform.IsChildOf(group.transform))), "no navigable control behind the node panel");
                Require(dice.ResultsButton != null && !dice.ResultsButton.IsInteractable(), "ResultsButton is not navigable while choosing");
                var cards = reachable.Where(item => item.transform.IsChildOf(blocker.transform)).ToArray();
                foreach (var card in cards)
                {
                    foreach (var next in new[] { card.FindSelectableOnUp(), card.FindSelectableOnDown(), card.FindSelectableOnLeft(), card.FindSelectableOnRight() })
                        Require(next == null || !groups.Any(group => next.transform.IsChildOf(group.transform)), $"{card.name} does not navigate into a covered panel ({next?.name})");
                }
                log.AppendLine($" choosing: blocker on, {cards.Length} node controls, none navigate into enemy/dice panels.");

                // Leaving the choice restores the original interactable state.
                var select = typeof(TxTRPG.Application.Exploration.ExplorationRunController).GetMethod("SelectChoice", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string), typeof(string) }, null)
                    ?? throw new InvalidOperationException("The exploration choice handler is missing.");
                var choice = run.Run.CurrentChoices.FirstOrDefault(item => item.TypeId == TxTRPG.Gameplay.Exploration.ExplorationNodeTypeIds.Combat) ?? run.Run.CurrentChoices.First();
                select.Invoke(run, new object[] { run.Run.CurrentChoiceSetId, choice.Id });
                await Frames(2);
                if (!blocker.gameObject.activeInHierarchy)
                {
                    Require(!blocker.IsBlocking && groups.All(group => group.interactable), "covered panels restored after the node panel hides");
                    log.AppendLine($" after choice ({choice.TypeId}): blocker off, panels interactable, ResultsButton interactable={dice.ResultsButton.interactable}.");
                }
                else log.AppendLine($" after choice ({choice.TypeId}): node panel still shown (continue step); restore not checked here.");

                // S7: text size follows width; scroll position survives the size change.
                for (var i = 0; i < 30; i++) story.AddMessage($"검증 메시지 {i}: 패널 너비에 따라 글자 크기가 달라지는지 확인하는 긴 문장입니다.", i % 3 == 0 ? "검증" : null);
                await Frames(3);
                var viewport = screen.PageScroll.viewport;
                var saved = (viewport.anchorMin, viewport.anchorMax, viewport.sizeDelta, viewport.anchoredPosition);
                var scroll = story.GetComponent<ScrollRect>();
                try
                {
                    viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f); viewport.anchoredPosition = Vector2.zero;
                    viewport.sizeDelta = new Vector2(2560f, 1080f); await Frames(4);
                    var wideFont = story.EffectiveBodyFontSize;
                    Require(Mathf.Abs(scroll.verticalNormalizedPosition) < .01f, $"follows the latest message ({scroll.verticalNormalizedPosition})");
                    viewport.sizeDelta = new Vector2(1024f, 640f); await Frames(4);
                    Require(story.EffectiveBodyFontSize >= 18f - .01f && story.EffectiveBodyFontSize <= 24f + .01f, "font within 18..24");
                    Require(Mathf.Abs(scroll.verticalNormalizedPosition) < .01f, $"still at latest after resize ({scroll.verticalNormalizedPosition})");
                    var body = story.GetComponentsInChildren<TMPro.TMP_Text>().Where(text => text.name == "Body").ToArray();
                    Require(body.Length > 0 && body.All(text => Mathf.Abs(text.fontSize - story.EffectiveBodyFontSize) < .01f), "existing messages use the new size");
                    story.ScrollToOldest(); await Frames(2);
                    viewport.sizeDelta = new Vector2(1920f, 1080f); await Frames(4);
                    Require(scroll.verticalNormalizedPosition > .95f, $"user scroll position kept near the top ({scroll.verticalNormalizedPosition})");
                    log.AppendLine($" story: wide font={wideFont:0.##}, narrow desktop font={story.EffectiveBodyFontSize:0.##}, separator={story.EffectiveSeparatorWidth:0.#}, scroll kept.");
                }
                finally
                {
                    viewport.anchorMin = saved.anchorMin; viewport.anchorMax = saved.anchorMax;
                    viewport.sizeDelta = saved.sizeDelta; viewport.anchoredPosition = saved.anchoredPosition;
                }
                Debug.Log(log.Append(" PASS").ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError(log.Append(" FAIL: ").Append(exception.Message).ToString());
            }
        }

        private static CanvasGroup[] CoveredGroups(CoveredPanelInteractionBlocker blocker) =>
            new SerializedObject(blocker).FindProperty("coveredPanels") is { } property
                ? Enumerable.Range(0, property.arraySize).Select(i => property.GetArrayElementAtIndex(i).objectReferenceValue as CanvasGroup).Where(group => group != null).ToArray()
                : Array.Empty<CanvasGroup>();

        private static async Task Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var frame = Time.frameCount;
                while (Time.frameCount == frame) await Task.Yield();
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static RectTransform EnemyRoot(EnemyDisplayPanel enemy, OwnedDicePanel dice)
        {
            var current = enemy.transform;
            while (current.parent != null && current.parent != dice.transform.parent) current = current.parent;
            return (RectTransform)current;
        }

        // Unity allows one CanvasGroup per object, so an existing group is shared (the enemy panel's group is also driven
        // by PanelStartupController; CoveredPanelInteractionBlocker re-blocks it if that enables input while covered).
        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            var group = target.GetComponent<CanvasGroup>();
            if (group != null) return group;
            group = target.AddComponent<CanvasGroup>();
            Record(group);
            return group;
        }

        private static void Record(UnityEngine.Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
