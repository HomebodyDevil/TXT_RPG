#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI
{
    // Enqueue is managed-only: OnValidate may run on a loading thread.
    internal interface IEditorValidationRefresh
    {
        void ApplyDeferredValidation();
    }

    internal static class EditorValidationRefreshPump
    {
        private static readonly object Gate = new();
        private static readonly List<WeakReference<IEditorValidationRefresh>> Pending = new();

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.update -= Flush;
            EditorApplication.update += Flush;
            AssemblyReloadEvents.beforeAssemblyReload -= Clear;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
        }

        public static void Enqueue(IEditorValidationRefresh target)
        {
            lock (Gate)
            {
                foreach (var weak in Pending)
                    if (weak.TryGetTarget(out var queued) && ReferenceEquals(queued, target)) return;
                Pending.Add(new WeakReference<IEditorValidationRefresh>(target));
            }
        }

        private static void Clear()
        {
            lock (Gate) Pending.Clear();
        }

        private static void Flush()
        {
            if (EditorApplication.isCompiling) return;
            List<WeakReference<IEditorValidationRefresh>> batch;
            lock (Gate)
            {
                if (Pending.Count == 0) return;
                batch = new List<WeakReference<IEditorValidationRefresh>>(Pending);
                Pending.Clear();
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var weak in batch)
            {
                if (!weak.TryGetTarget(out var target) || target is not Behaviour component ||
                    component == null || !component.isActiveAndEnabled ||
                    EditorUtility.IsPersistent(component) || !component.gameObject.scene.IsValid()) continue;
                target.ApplyDeferredValidation();
            }
        }
    }
}
#endif
