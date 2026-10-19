using System;
using System.Collections.Generic;
using System.Linq;
using TxTRPG.Application.Players;
using TxTRPG.Application.Runes;
using TxTRPG.Gameplay.Runes;
using TxTRPG.UI.Runes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY Play Mode check (candidate-main-scene-rune-grid). Enter Play Mode through AppScene so Candidate_MainScene
    // is the content scene, then run the menu. It changes only the session rune state (runes are session-lifetime and
    // never saved): stacks one rune on the first line, adds lines until the grid scrolls vertically, then restores the
    // original lines and removes every added rune. Added IDs start with "rune.check.". Results use the RUNE_GRID_CHECK
    // prefix. Nothing is saved; safe to rerun. Retire it with TemporaryRuneGridApply.
    public static class TemporaryRuneGridPlayCheck
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Rune Grid Play Check";
        private const string Prefix = "rune.check.";
        private const int MaximumExtraLines = 120;

        [MenuItem(MenuPath, false, 3002)]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogError("RUNE_GRID_CHECK: FAIL enter Play Mode through AppScene first."); return; }
            var scene = SceneManager.GetSceneByPath(TemporaryRuneGridApply.ScenePath);
            if (!scene.isLoaded) { Debug.LogError("RUNE_GRID_CHECK: FAIL Candidate_MainScene is not the loaded content scene."); return; }
            var runes = PlayerSessionHost.Instance != null ? PlayerSessionHost.Instance.TemporaryRunes : null;
            if (runes == null) { Debug.LogError("RUNE_GRID_CHECK: FAIL PlayerSessionHost.Instance.TemporaryRunes is not available."); return; }
            var binder = scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<TemporaryRuneGridBinder>(true)).FirstOrDefault(found => found != null);
            var panel = binder != null ? binder.Panel : null;
            if (panel == null) { Debug.LogError("RUNE_GRID_CHECK: FAIL Candidate_MainScene has no TemporaryRuneGridBinder with a RuneGridPanel."); return; }
            if (!ReferenceEquals(binder.Runes, runes)) { Debug.LogError("RUNE_GRID_CHECK: FAIL the binder is not connected to the session rune state."); return; }
            if (runes.Loadout.LineCount == 0) { Debug.LogError("RUNE_GRID_CHECK: FAIL the loadout has no line to stack on."); return; }

            var originalLines = runes.Loadout.Lines.ToArray();
            var added = new List<string>();
            var passed = true;
            try
            {
                passed &= Report(1, MatchesLoadout(panel, runes, out var detail), detail);

                var first = originalLines[0];
                var stackId = Prefix + "stack";
                var expected = first.Weight + 1;
                if (!runes.Inventory.TryAdd(new OwnedRune(stackId, runes.Loadout.DefinitionIdOf(first)), out var reason)) throw new InvalidOperationException(reason);
                added.Add(stackId);
                if (!runes.Loadout.TryAddRuneToLine(first.LineId, stackId, out reason)) throw new InvalidOperationException(reason);
                Canvas.ForceUpdateCanvases();
                var badge = panel.Cards[0].CountText.text;
                passed &= Report(2, badge == RuneGridCardView.FormatCount(expected) && MatchesLoadout(panel, runes, out detail),
                    $"first line weight {expected}, badge '{badge}'. {detail}");

                var definition = runes.Loadout.DefinitionIdOf(first);
                var extra = 0;
                while (extra < MaximumExtraLines && panel.Content.rect.height <= panel.Viewport.rect.height)
                {
                    var id = $"{Prefix}extra.{extra}";
                    if (!runes.Inventory.TryAdd(new OwnedRune(id, definition), out reason)) throw new InvalidOperationException(reason);
                    added.Add(id);
                    if (!runes.Loadout.TryAddLine(new RuneLoadoutLine($"{Prefix}line.{extra}", new[] { id }), out reason)) throw new InvalidOperationException(reason);
                    extra++;
                    Canvas.ForceUpdateCanvases();
                }
                var layout = panel.LayoutGroup.CurrentLayout;
                var contentWidth = panel.Content.rect.width;
                var viewportWidth = panel.Viewport.rect.width;
                var scrolls = panel.Content.rect.height > panel.Viewport.rect.height;
                var noHorizontal = contentWidth <= viewportWidth + .01f && layout.UsedWidth <= panel.LayoutGroup.AvailableWidth + .01f && !panel.ScrollRect.horizontal;
                passed &= Report(3, scrolls && noHorizontal && MatchesLoadout(panel, runes, out detail),
                    $"added {extra} lines, cols={layout.Columns} card={layout.CardSize:0.#} content={contentWidth:0.#}x{panel.Content.rect.height:0.#} viewport={viewportWidth:0.#}x{panel.Viewport.rect.height:0.#} verticalScroll={scrolls} horizontalOverflow={!noHorizontal}. {detail}");
            }
            catch (Exception exception)
            {
                passed = false;
                Debug.LogError($"RUNE_GRID_CHECK: FAIL {exception.Message}");
            }
            finally
            {
                passed &= Restore(runes, originalLines, added, panel);
            }
            Debug.Log(passed ? "RUNE_GRID_CHECK: PASS all steps" : "RUNE_GRID_CHECK: FAIL see the steps above");
        }

        private static bool Restore(TemporaryRuneState runes, RuneLoadoutLine[] originalLines, List<string> added, RuneGridPanel panel)
        {
            var ok = runes.Loadout.TryReplaceAll(originalLines, out var reason);
            foreach (var id in added)
                ok &= runes.Inventory.TryRemove(id, out reason);
            Canvas.ForceUpdateCanvases();
            var restored = ok && runes.Loadout.Lines.Select(line => line.LineId).SequenceEqual(originalLines.Select(line => line.LineId))
                && !runes.Inventory.Snapshot().Any(rune => rune.InstanceId.StartsWith(Prefix, StringComparison.Ordinal));
            var matches = MatchesLoadout(panel, runes, out var detail);
            return Report(4, restored && matches, $"{(ok ? string.Empty : reason + " ")}{detail}");
        }

        private static bool MatchesLoadout(RuneGridPanel panel, TemporaryRuneState runes, out string detail)
        {
            var lines = runes.Loadout.Lines;
            if (panel.ActiveCardCount != lines.Count) { detail = $"cards {panel.ActiveCardCount} != lines {lines.Count}"; return false; }
            for (var i = 0; i < lines.Count; i++)
            {
                var card = panel.Cards[i];
                if (!card.gameObject.activeSelf || card.Key != lines[i].LineId || card.CountText.text != RuneGridCardView.FormatCount(lines[i].Weight))
                { detail = $"card {i + 1} shows '{card.Key}' x'{card.CountText.text}', line is '{lines[i].LineId}' x{lines[i].Weight}"; return false; }
            }
            detail = $"cards={lines.Count} badges=[{string.Join(",", lines.Select(line => line.Weight))}]";
            return true;
        }

        private static bool Report(int step, bool ok, string detail)
        {
            var message = $"RUNE_GRID_CHECK: step {step} {(ok ? "PASS" : "FAIL")} {detail}";
            if (ok) Debug.Log(message); else Debug.LogError(message);
            return ok;
        }
    }
}
