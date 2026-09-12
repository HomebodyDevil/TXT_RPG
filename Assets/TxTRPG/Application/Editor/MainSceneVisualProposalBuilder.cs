using System;
using System.Linq;
using TxTRPG.Application.Presentation;
using TxTRPG.UI;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    [InitializeOnLoad]
    public static class MainSceneVisualProposalBuilder
    {
        public const string SourceScenePath = "Assets/Scenes/TMP_MainScene.unity";
        public const string ProposalScenePath = "Assets/Scenes/MainScene_VisualProposal.unity";
        public const string AssetFolder = "Assets/TxTRPG/UI/VisualProposals/MainScene";
        public const string MainStylePath = AssetFolder + "/MainSceneProposalBackground.asset";
        public const string StoryStylePath = AssetFolder + "/StoryColumnProposalBackground.asset";
        public const string CharacterStylePath = AssetFolder + "/CharacterColumnProposalBackground.asset";
        private const string DefaultProfilePath = AssetFolder + "/MainSceneProposalDefaults.asset";
        private const string PreviewProfilePath = AssetFolder + "/MainSceneProposalPreview.asset";
        private const string PreviewSessionKey = "TxTRPG.MainSceneVisualProposal.Play";
        private const string StoryDemoDataPath = "Assets/TxTRPG/UI/DEMO/StoryTextPanelDemoData.asset";
        private const string EnemyDemoDataPath = "Assets/TxTRPG/UI/DEMO/EnemyDisplayPanel/EnemyDisplayPanelDemoData.asset";
        private const int SchemaVersion = 1;
        private static bool transitionRequested;

        static MainSceneVisualProposalBuilder()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/TxT RPG/UI/Visual Proposals/Rebuild Main Scene Visual Proposal")]
        public static void CreateOrUpdate()
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath))
                throw new InvalidOperationException($"Source scene was not found: {SourceScenePath}");

            EnsureFolder(AssetFolder);
            var created = AssetDatabase.LoadAssetAtPath<SceneAsset>(ProposalScenePath) == null;
            if (created && !AssetDatabase.CopyAsset(SourceScenePath, ProposalScenePath))
                throw new InvalidOperationException("Could not create the visual proposal scene copy.");

            var setup = EditorSceneManager.GetSceneManagerSetup();
            var scene = SceneManager.GetSceneByPath(ProposalScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(ProposalScenePath, OpenSceneMode.Additive);
                ApplyProposal(scene);
                if (!EditorSceneManager.SaveScene(scene, ProposalScenePath))
                    throw new InvalidOperationException("Could not save the visual proposal scene.");
                EnsureBuildSettingsEntry();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (setup.Any(entry => entry.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            Debug.Log(created
                ? $"Created {ProposalScenePath} without modifying {SourceScenePath}."
                : $"Updated owned visual settings in {ProposalScenePath}.");
        }

        [MenuItem("Tools/TxT RPG/UI/Visual Proposals/Play Main Scene Visual Proposal")]
        public static void PlayThroughAppScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ProposalScenePath) == null) CreateOrUpdate();
            SessionState.SetBool(PreviewSessionKey, true);
            transitionRequested = false;
            EditorApplication.isPlaying = true;
        }

        public static void ApplyProposal(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var main = FindNamed<FlexibleLayoutPanel>(roots, "Main_FlexibleLayoutPanel");
            var text = FindNamed<FlexibleLayoutPanel>(roots, "Text_FlexibleLayoutPanel");
            var character = FindNamed<FlexibleLayoutPanel>(roots, "Character_FlexibleLayoutPanel");
            var placeholder = FindNamed<FlexibleLayoutPanel>(roots, "TMP_FlexibleLayoutPanel");

            ConfigureLayout(main, FlexibleLayoutAxis.Horizontal,
                FlexibleLayoutAxisPolicy.VerticalWhenNarrow, 920f, 24f,
                new RectOffset(30, 30, 26, 26), TextAnchor.MiddleCenter);
            main.SetContentMargins(0, 0, 0, 0);
            ConfigureLayout(text, FlexibleLayoutAxis.Vertical,
                FlexibleLayoutAxisPolicy.Fixed, 720f, 18f,
                new RectOffset(), TextAnchor.MiddleCenter);
            ConfigureLayout(character, FlexibleLayoutAxis.Vertical,
                FlexibleLayoutAxisPolicy.Fixed, 720f, 14f,
                new RectOffset(), TextAnchor.MiddleCenter);

            placeholder.gameObject.SetActive(false);
            ConfigureItem(text, 1.55f, 560f);
            ConfigureItem(character, 0.95f, 360f);
            ConfigureItem(FindNamed<StoryTextPanel>(roots, "StoryTextPanel"), 1.5f, 340f);
            ConfigureItem(FindNamed<EnemyDisplayPanel>(roots, "EnemyDisplayPanel").GetComponentInParent<FlexibleLayoutItem>(), 0.62f, 190f);
            ConfigureItem(FindNamed<CharacterDisplayPanel>(roots, "CharacterDisplayPanel"), 1.5f, 260f);
            ConfigureItem(FindNamed<HealthBarPanel>(roots, "HealthBarPanel"), 0f, 76f, true);
            ConfigureItem(FindNamed<ActionGridPanel>(roots, "ActionGridPanel"), 0.8f, 190f);
            ConfigureItem(FindNamed<GameMenuPanel>(roots, "GameMenuPanel"), 0f, 92f, true);

            var mainStyle = CreateStyle(MainStylePath, new Color(0.035f, 0.061f, 0.095f, 1f), 1f);
            var storyStyle = CreateStyle(StoryStylePath, new Color(0.055f, 0.090f, 0.135f, 1f), 0.96f);
            var characterStyle = CreateStyle(CharacterStylePath, new Color(0.075f, 0.078f, 0.105f, 1f), 0.96f);
            AssignStyle(main, mainStyle);
            AssignStyle(text, storyStyle);
            AssignStyle(character, characterStyle);
            ConfigureDecorativeBackdrop(main);
            ConfigureBorder(main, new Color(0.27f, 0.52f, 0.72f, 0.34f));
            ConfigureBorder(text, new Color(0.30f, 0.60f, 0.83f, 0.45f));
            ConfigureBorder(character, new Color(0.66f, 0.48f, 0.36f, 0.40f));

            var storyPanel = FindNamed<StoryTextPanel>(roots, "StoryTextPanel");
            var actionGrid = FindNamed<ActionGridPanel>(roots, "ActionGridPanel");
            var enemyPanel = FindNamed<EnemyDisplayPanel>(roots, "EnemyDisplayPanel");
            var healthPanel = FindNamed<HealthBarPanel>(roots, "HealthBarPanel");
            var demoRoot = enemyPanel.GetComponentInParent<EnemyDisplayPanelDemoLoader>()?.gameObject;
            if (demoRoot != null)
            {
                foreach (var loader in demoRoot.GetComponents<EnemyDisplayPanelDemoLoader>()) Object.DestroyImmediate(loader);
                foreach (var startup in demoRoot.GetComponents<PanelStartupController>()) Object.DestroyImmediate(startup);
                foreach (var reveal in demoRoot.GetComponents<FadePanelRevealTransition>()) Object.DestroyImmediate(reveal);
                if (demoRoot.name == "EnemyDisplayPanelDemo") demoRoot.name = "EnemyDisplayPanelContainer";
            }

            var defaults = LoadOrCreateDefaults();
            var controller = main.GetComponent<MainScenePresentationController>()
                ?? main.gameObject.AddComponent<MainScenePresentationController>();
            var preview = LoadOrRepairPreviewProfile();
            controller.Configure(defaults, storyPanel, actionGrid, enemyPanel, healthPanel, true, preview);
            EditorUtility.SetDirty(controller);

            var marker = main.GetComponent<MainSceneVisualProposalMarker>()
                ?? main.gameObject.AddComponent<MainSceneVisualProposalMarker>();
            marker.ConfigureForEditor(SchemaVersion, SourceScenePath);
            EditorUtility.SetDirty(marker);
            Canvas.ForceUpdateCanvases();
            main.Rebuild(); text.Rebuild(); character.Rebuild();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ConfigureLayout(FlexibleLayoutPanel panel, FlexibleLayoutAxis axis,
            FlexibleLayoutAxisPolicy policy, float breakpoint, float spacing,
            RectOffset padding, TextAnchor alignment)
        {
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("fixedAxis").enumValueIndex = (int)axis;
            serialized.FindProperty("axisPolicy").enumValueIndex = (int)policy;
            serialized.FindProperty("breakpoint").floatValue = breakpoint;
            serialized.FindProperty("spacing").floatValue = spacing;
            serialized.FindProperty("m_ChildAlignment").enumValueIndex = (int)alignment;
            SetRectOffset(serialized.FindProperty("m_Padding"), padding);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);
        }

        private static void ConfigureItem(Component component, float weight, float size, bool fixedSize = false)
        {
            var item = component as FlexibleLayoutItem ?? component.GetComponent<FlexibleLayoutItem>()
                ?? throw new InvalidOperationException($"{component.name} has no FlexibleLayoutItem.");
            item.Configure(fixedSize ? FlexibleLayoutSizeMode.Fixed : FlexibleLayoutSizeMode.Weighted,
                weight, fixedSize ? size : 100f, size);
            EditorUtility.SetDirty(item);
        }

        private static MainSceneDefaultPresentationProfile LoadOrCreateDefaults()
        {
            var defaults = AssetDatabase.LoadAssetAtPath<MainSceneDefaultPresentationProfile>(DefaultProfilePath);
            if (defaults == null)
            {
                defaults = ScriptableObject.CreateInstance<MainSceneDefaultPresentationProfile>();
                AssetDatabase.CreateAsset(defaults, DefaultProfilePath);
            }
            defaults.ConfigureForEditor("The story is not available yet.",
                "The story could not be loaded.", "HP", "Unknown", null, null);
            EditorUtility.SetDirty(defaults);
            return defaults;
        }
        private static MainScenePreviewProfile LoadOrRepairPreviewProfile()
        {
            var preview = AssetDatabase.LoadAssetAtPath<MainScenePreviewProfile>(PreviewProfilePath);
            if (preview != null) return preview;
            if (AssetDatabase.LoadMainAssetAtPath(PreviewProfilePath) != null)
                AssetDatabase.DeleteAsset(PreviewProfilePath);
            preview = ScriptableObject.CreateInstance<MainScenePreviewProfile>();
            preview.ConfigureForEditor(MainScenePreviewScenario.Normal,
                AssetDatabase.LoadAssetAtPath<StoryTextPanelDemoData>(StoryDemoDataPath),
                AssetDatabase.LoadAssetAtPath<EnemyDisplayPanelDemoData>(EnemyDemoDataPath), 0f);
            AssetDatabase.CreateAsset(preview, PreviewProfilePath);
            return preview;
        }
        private static PanelBackgroundStyle CreateStyle(string path, Color tint, float opacity)
        {
            var style = AssetDatabase.LoadAssetAtPath<FlexibleLayoutBackgroundStyle>(path);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<FlexibleLayoutBackgroundStyle>();
                AssetDatabase.CreateAsset(style, path);
            }
            style.Configure(null, tint, FlexibleLayoutBackgroundScaleMode.Stretch, opacity,
                FlexibleLayoutBackgroundOverflowMode.ClipToPanel);
            EditorUtility.SetDirty(style);
            return style;
        }

        private static void AssignStyle(FlexibleLayoutPanel panel, PanelBackgroundStyle style)
        {
            var renderer = panel.GetComponentInChildren<FlexibleLayoutBackground>(true)
                ?? throw new InvalidOperationException($"{panel.name} has no FlexibleLayoutBackground.");
            var serialized = new SerializedObject(renderer);
            serialized.FindProperty("initialStyle").objectReferenceValue = style;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.ApplyStyle(style);
            EditorUtility.SetDirty(renderer);
        }

        private static void ConfigureDecorativeBackdrop(FlexibleLayoutPanel main)
        {
            var visualRoot = main.transform.Find("BackgroundLayer/BackgroundVisualRoot") as RectTransform
                ?? throw new InvalidOperationException("Main background visual root was not found.");
            ReplaceDecorativeImage(visualRoot, "VisualProposalStoryGlow",
                new Vector2(0f, 0f), new Vector2(0.68f, 1f),
                new Color(0.08f, 0.28f, 0.48f, 0.18f));
            ReplaceDecorativeImage(visualRoot, "VisualProposalCharacterWash",
                new Vector2(0.62f, 0f), Vector2.one,
                new Color(0.38f, 0.20f, 0.15f, 0.14f));
        }

        private static void ReplaceDecorativeImage(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            rect.SetAsLastSibling();
        }

        private static void ConfigureBorder(FlexibleLayoutPanel panel, Color color)
        {
            var border = panel.transform.Find("ForegroundLayer/Border")?.GetComponent<Image>();
            if (border == null) return;
            border.color = color;
            border.raycastTarget = false;
            EditorUtility.SetDirty(border);
        }

        private static T FindNamed<T>(GameObject[] roots, string name) where T : Component
        {
            return roots.SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault(component => component.name == name)
                ?? throw new InvalidOperationException($"{name} ({typeof(T).Name}) was not found.");
        }

        private static void SetRectOffset(SerializedProperty property, RectOffset value)
        {
            if (property == null) return;
            property.FindPropertyRelative("m_Left").intValue = value.left;
            property.FindPropertyRelative("m_Right").intValue = value.right;
            property.FindPropertyRelative("m_Top").intValue = value.top;
            property.FindPropertyRelative("m_Bottom").intValue = value.bottom;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/'); var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void EnsureBuildSettingsEntry()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var index = scenes.FindIndex(entry => entry.path == ProposalScenePath);
            if (index < 0) scenes.Add(new EditorBuildSettingsScene(ProposalScenePath, true));
            else if (!scenes[index].enabled) scenes[index] = new EditorBuildSettingsScene(ProposalScenePath, true);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PreviewSessionKey, false))
            {
                transitionRequested = false;
                EditorApplication.update -= TryOpenProposal;
                EditorApplication.update += TryOpenProposal;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.EraseBool(PreviewSessionKey);
                EditorApplication.update -= TryOpenProposal;
                transitionRequested = false;
            }
        }

        private static async void TryOpenProposal()
        {
            if (transitionRequested || !EditorApplication.isPlaying) return;
            var rootType = Type.GetType("TxTRPG.SceneTransition.AppSceneRoot, TxTRPG.SceneTransition");
            var root = rootType?.GetProperty("Instance")?.GetValue(null) as Component;
            var service = rootType?.GetProperty("SceneTransitions")?.GetValue(root);
            if (root == null || service == null) return;
            var serviceType = service.GetType();
            if ((bool)serviceType.GetProperty("IsTransitioning").GetValue(service)) return;
            var currentScene = (Scene)serviceType.GetProperty("CurrentContentScene").GetValue(service);
            if (!currentScene.IsValid()) return;
            if (currentScene.path == ProposalScenePath)
            {
                transitionRequested = true;
                EditorApplication.update -= TryOpenProposal;
                return;
            }

            transitionRequested = true;
            EditorApplication.update -= TryOpenProposal;
            try
            {
                var profileType = Type.GetType("TxTRPG.SceneTransition.SceneTransitionProfile, TxTRPG.SceneTransition");
                var method = serviceType.GetMethod("LoadContentSceneAsync", new[]
                {
                    typeof(string), profileType, typeof(System.Threading.CancellationToken)
                });
                var task = method?.Invoke(service, new object[]
                {
                    ProposalScenePath, null, default(System.Threading.CancellationToken)
                }) as System.Threading.Tasks.Task;
                if (task == null) throw new MissingMethodException("SceneTransitionService.LoadContentSceneAsync");
                await task;
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}










