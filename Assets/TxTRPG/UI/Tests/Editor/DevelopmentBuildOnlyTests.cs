using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    public sealed class DevelopmentBuildOnlyTests
    {
        [Test]
        public void ReleaseBuild_Disables_DebugBuild_Keeps()
        {
            Assert.That(DevelopmentBuildOnly.ShouldDisable(false), Is.True);
            Assert.That(DevelopmentBuildOnly.ShouldDisable(true), Is.False);
        }

        [Test]
        public void InTheEditor_TheObjectStaysActive()
        {
            var target = new GameObject("DevelopmentBuildOnlyTarget");
            try
            {
                var component = target.AddComponent<DevelopmentBuildOnly>();
                typeof(DevelopmentBuildOnly).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
                Assert.That(Debug.isDebugBuild, Is.True, "the Editor counts as a debug build");
                Assert.That(target.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(target); }
        }
    }
}
