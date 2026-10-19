using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TxTRPG.UI.ScreenFrame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY application menu (DOCS/instructions/screen-frame-letterbox-resolution-policy.md). Creates, only when
    // missing, DefaultLetterboxPresentation.prefab, DefaultScreenFrameProfile.asset and ScreenFrameCanvas.prefab; inserts
    // ModalWindowHost/FrameArea (ScreenFrameAlignedRect) above WindowFrame in EncounterResultModal.prefab when missing;
    // then restructures Candidate_MainScene's Canvas into ScreenFrame (MainArea, OverlayLayer) / LetterboxLayer /
    // ScreenOverlayLayer (EncounterResultModal), replaces its CanvasScaler with ScreenFrameCanvasScaler, adds
    // ScreenFrameController and stores the modal FrameArea controller as an instance override. Finally it sets the Player
    // orientation (landscape left/right only), the 1280x720 default window and a resizable window.
    // Existing Prefabs and the profile are never overwritten, and a Canvas that already has ScreenFrame leaves the Scene
    // untouched, so rerunning is safe. It stops without changes in Play Mode, while compiling, in Prefab Stage, with
    // unsaved open Scenes, or when Canvas's children differ from MainArea, OverlayLayer and EncounterRollController.
    // Saves only what it changed. Not part of the Prefab rebuild batch (one-time application; the assets are
    // developer-owned afterwards). Retire it once the user has applied and verified the screen frame.
    public static class TemporaryScreenFrameApply
    {
        // TxTRPGEditorMenuPaths.Application + "Temporary/Apply Candidate Screen Frame" (this assembly does not reference
        // TxTRPG.Editor.Common).
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Apply Candidate Screen Frame";
        public const string ScenePath = "Assets/Scenes/Candidate_MainScene.unity";
        public const string PrefabFolder = "Assets/TxTRPG/UI/Prefabs/ScreenFrame";
        public const string StyleFolder = "Assets/TxTRPG/UI/Styles/ScreenFrame";
        public const string PresentationPrefabPath = PrefabFolder + "/DefaultLetterboxPresentation.prefab";
        public const string CanvasPrefabPath = PrefabFolder + "/ScreenFrameCanvas.prefab";
        public const string ProfilePath = StyleFolder + "/DefaultScreenFrameProfile.asset";
        public const string ResultModalPrefabPath = "Assets/TxTRPG/UI/Prefabs/Encounters/EncounterResultModal.prefab";
        private const string Log = "SCREEN_FRAME_APPLY: ";
        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        [MenuItem(MenuPath, false, 3030)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Log + "exit Play Mode first. Nothing was changed."); return; }
            if (EditorApplication.isCompiling) { Debug.LogError(Log + "wait for script compilation to finish. Nothing was changed."); return; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) { Debug.LogError(Log + "save and close Prefab Stage first. Nothing was changed."); return; }
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            {
                Debug.LogError(Log + "save or revert the unsaved open Scenes first. Nothing was changed.");
                return;
            }

            // Check the Scene before creating anything, so a mismatching hierarchy changes nothing.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Canvas");
            if (canvas == null) { Debug.LogError(Log + "Candidate_MainScene has no root Canvas. Nothing was changed."); return; }
            var sceneAlreadyApplied = canvas.transform.Find("ScreenFrame") != null;
            if (!sceneAlreadyApplied && !CheckHierarchy(canvas.transform)) return;

            EnsureFolder(PrefabFolder);
            EnsureFolder(StyleFolder);
            var presentation = EnsurePresentationPrefab();
            var profile = EnsureProfile(presentation);
            EnsureCanvasPrefab(profile);
            EnsureModalFrameArea();

            if (sceneAlreadyApplied) Debug.Log(Log + "Candidate_MainScene already has Canvas/ScreenFrame; the Scene was left unchanged.");
            else ApplyScene(scene, canvas, profile);

            ApplyPlayerSettings();
            Debug.Log(Log + "done.");
        }

        private static bool CheckHierarchy(Transform canvas)
        {
            var names = Enumerable.Range(0, canvas.childCount).Select(i => canvas.GetChild(i).name).ToArray();
            var expected = new[] { "MainArea", "OverlayLayer", "EncounterRollController" };
            if (!names.SequenceEqual(expected))
            {
                Debug.LogError(Log + $"Canvas children are [{string.Join(", ", names)}], expected [{string.Join(", ", expected)}]. Nothing was changed.");
                return false;
            }
            if (canvas.Find("OverlayLayer/EncounterResultModal") == null)
            {
                Debug.LogError(Log + "OverlayLayer/EncounterResultModal is missing. Nothing was changed.");
                return false;
            }
            if (canvas.GetComponents<CanvasScaler>().Length != 1)
            {
                Debug.LogError(Log + "Canvas must have exactly one CanvasScaler. Nothing was changed.");
                return false;
            }
            return true;
        }

        private static GameObject EnsurePresentationPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPrefabPath);
            if (existing != null) { Debug.Log(Log + "kept existing " + PresentationPrefabPath); return existing; }
            var bars = LetterboxBarsPresentation.Create(null, Color.black);
            bars.gameObject.name = "DefaultLetterboxPresentation";
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(bars.gameObject, PresentationPrefabPath);
                Debug.Log(Log + "created " + PresentationPrefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(bars.gameObject);
            }
        }

        private static ScreenFrameProfile EnsureProfile(GameObject presentation)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ScreenFrameProfile>(ProfilePath);
            if (existing != null) { Debug.Log(Log + "kept existing " + ProfilePath); return existing; }
            var profile = ScriptableObject.CreateInstance<ScreenFrameProfile>();
            profile.Configure(Reference, true, presentation, Color.black);
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssetIfDirty(profile);
            Debug.Log(Log + "created " + ProfilePath);
            return profile;
        }

        private static void EnsureCanvasPrefab(ScreenFrameProfile profile)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath) != null) { Debug.Log(Log + "kept existing " + CanvasPrefabPath); return; }
            var root = new GameObject("ScreenFrameCanvas", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.AddComponent<ScreenFrameCanvasScaler>();
                scaler.SetProfile(profile);
                root.AddComponent<GraphicRaycaster>();
                var controller = root.AddComponent<ScreenFrameController>();
                var frame = CreateRect("ScreenFrame", root.transform);
                CreateRect("OverlayLayer", frame);
                var letterbox = CreateRect("LetterboxLayer", root.transform).gameObject.AddComponent<LetterboxView>();
                letterbox.SetProfile(profile);
                CreateRect("ScreenOverlayLayer", root.transform);
                controller.Configure(scaler, frame, letterbox);
                root.SetActive(true);
                SetReferenceFrame(frame);
                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
                Debug.Log(Log + "created " + CanvasPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureModalFrameArea()
        {
            using (var scope = new PrefabUtility.EditPrefabContentsScope(ResultModalPrefabPath))
            {
                var host = scope.prefabContentsRoot.transform.Find("ModalWindowHost");
                if (host == null) { Debug.LogError(Log + "EncounterResultModal has no ModalWindowHost; the modal was left unchanged."); return; }
                if (host.Find("FrameArea") != null) { Debug.Log(Log + "EncounterResultModal already has FrameArea."); return; }
                var windowFrame = host.Find("WindowFrame");
                if (windowFrame == null) { Debug.LogError(Log + "EncounterResultModal has no ModalWindowHost/WindowFrame; the modal was left unchanged."); return; }
                var frameArea = CreateRect("FrameArea", host);
                frameArea.SetSiblingIndex(windowFrame.GetSiblingIndex());
                frameArea.gameObject.AddComponent<ScreenFrameAlignedRect>();
                windowFrame.SetParent(frameArea, false);
                Debug.Log(Log + "inserted ModalWindowHost/FrameArea above WindowFrame in " + ResultModalPrefabPath);
            }
        }

        private static void ApplyScene(Scene scene, GameObject canvasObject, ScreenFrameProfile profile)
        {
            var canvas = canvasObject.transform;
            var mainArea = canvas.Find("MainArea");
            var overlay = canvas.Find("OverlayLayer");
            var modal = overlay.Find("EncounterResultModal");
            var rollController = canvas.Find("EncounterRollController");

            var oldScaler = canvasObject.GetComponent<CanvasScaler>();
            Undo.DestroyObjectImmediate(oldScaler);
            var scaler = Undo.AddComponent<ScreenFrameCanvasScaler>(canvasObject);
            scaler.SetProfile(profile);

            var frame = CreateRect("ScreenFrame", canvas);
            Undo.RegisterCreatedObjectUndo(frame.gameObject, "Screen frame");
            mainArea.SetParent(frame, false);
            overlay.SetParent(frame, false);

            var letterboxLayer = CreateRect("LetterboxLayer", canvas);
            Undo.RegisterCreatedObjectUndo(letterboxLayer.gameObject, "Screen frame");
            var letterbox = letterboxLayer.gameObject.AddComponent<LetterboxView>();
            letterbox.SetProfile(profile);

            var screenOverlay = CreateRect("ScreenOverlayLayer", canvas);
            Undo.RegisterCreatedObjectUndo(screenOverlay.gameObject, "Screen frame");
            modal.SetParent(screenOverlay, false);

            frame.SetSiblingIndex(0);
            letterboxLayer.SetSiblingIndex(1);
            screenOverlay.SetSiblingIndex(2);
            rollController.SetSiblingIndex(3);

            var controller = canvasObject.AddComponent<ScreenFrameController>();
            controller.Configure(scaler, frame, letterbox);
            SetReferenceFrame(frame);

            var frameArea = modal.Find("ModalWindowHost/FrameArea");
            var aligned = frameArea != null ? frameArea.GetComponent<ScreenFrameAlignedRect>() : null;
            if (aligned != null)
            {
                var serialized = new SerializedObject(aligned);
                serialized.FindProperty("controller").objectReferenceValue = controller;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(aligned);
            }
            else
            {
                Debug.LogError(Log + "EncounterResultModal instance has no ModalWindowHost/FrameArea; its controller reference was not set.");
            }

            foreach (var rect in new[] { (RectTransform)mainArea, (RectTransform)overlay })
            {
                if (rect.anchorMin != Vector2.zero || rect.anchorMax != Vector2.one || rect.offsetMin != Vector2.zero || rect.offsetMax != Vector2.zero)
                    Debug.LogWarning(Log + $"{rect.name} is not stretched (0, 0, 0, 0) after the move: {rect.anchorMin}..{rect.anchorMax} {rect.offsetMin}/{rect.offsetMax}");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(Log + "restructured and saved " + ScenePath);
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            WriteProjectSettingsKeys();
            Debug.Log(Log + $"Player settings: orientation {PlayerSettings.defaultInterfaceOrientation}, landscape only, " +
                $"{PlayerSettings.defaultScreenWidth}x{PlayerSettings.defaultScreenHeight}, resizable {PlayerSettings.resizableWindow}, fullscreen {PlayerSettings.fullScreenMode}");
        }

        // SaveAssetIfDirty does not cover ProjectSettings and AssetDatabase.SaveAssets would also write unrelated dirty
        // assets, so only the changed keys are written to ProjectSettings.asset (the in-memory values above match them).
        private static void WriteProjectSettingsKeys()
        {
            const string path = "ProjectSettings/ProjectSettings.asset";
            var values = new Dictionary<string, string>
            {
                { "defaultScreenOrientation", "4" },
                { "allowedAutorotateToPortrait", "0" },
                { "allowedAutorotateToPortraitUpsideDown", "0" },
                { "allowedAutorotateToLandscapeRight", "1" },
                { "allowedAutorotateToLandscapeLeft", "1" },
                { "defaultScreenWidth", "1280" },
                { "defaultScreenHeight", "720" },
                { "resizableWindow", "1" },
            };
            var text = File.ReadAllText(path);
            var updated = text;
            foreach (var pair in values)
            {
                var pattern = new Regex("^(  " + Regex.Escape(pair.Key) + ": ).*$", RegexOptions.Multiline);
                if (!pattern.IsMatch(updated)) { Debug.LogError(Log + $"ProjectSettings.asset has no top-level '{pair.Key}'; it was not written."); return; }
                updated = pattern.Replace(updated, "${1}" + pair.Value, 1);
            }
            if (updated == text) { Debug.Log(Log + "ProjectSettings.asset already has the screen settings."); return; }
            File.WriteAllText(path, updated);
            Debug.Log(Log + "wrote the screen settings to ProjectSettings.asset");
        }

        // Stores the reference frame (bottom left, 1920x1080) so saved values do not depend on the Game View size.
        private static void SetReferenceFrame(RectTransform frame)
        {
            frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.zero;
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = Reference;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
