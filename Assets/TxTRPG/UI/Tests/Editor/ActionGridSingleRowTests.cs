using System;
using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.UI.Tests
{
    public sealed class ActionGridSingleRowTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(5)]
        public void Fit_UsesThresholdAndNoGapCap(int count)
        {
            var options = new ActionGridSingleRowSettings();
            var r = ActionGridSingleRowCalculator.Calculate(new Vector2(1000,200), count, Vector2.one*64,16,new RectOffset(11,29,7,13),options);
            Assert.That(r.Overflow, Is.False);
            Assert.That(r.Layout.Columns,Is.EqualTo(Mathf.Max(1,count)));
            Assert.That(r.Alignment,Is.EqualTo(count<=4 ? ActionGridHorizontalAlignment.Center : ActionGridHorizontalAlignment.Left));
            Assert.That(r.Layout.Spacing.x,Is.EqualTo(count<=4 ? 16 : 160));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(6)] [TestCase(-2)]
        public void Threshold_IsIndependentOfOverflow(int threshold)
        {
            var options=new ActionGridSingleRowSettings { centerThreshold=threshold }; options.Normalize();
            var r=ActionGridSingleRowCalculator.Calculate(new Vector2(1000,100),2,Vector2.one*64,16,new RectOffset(),options);
            Assert.That(r.Alignment,Is.EqualTo(Mathf.Max(0,threshold)>=2 ? ActionGridHorizontalAlignment.Center : ActionGridHorizontalAlignment.Left));
            r=ActionGridSingleRowCalculator.Calculate(new Vector2(143,100),2,Vector2.one*64,16,new RectOffset(),options);
            Assert.That(r.Overflow,Is.True); Assert.That(r.Alignment,Is.EqualTo(ActionGridHorizontalAlignment.Left)); Assert.That(r.Layout.Spacing.x,Is.EqualTo(16));
        }
        [TestCase(144,false)] [TestCase(143,true)]
        public void ExactBoundary_DoesNotShrinkCells(float width,bool overflow)
        {
            var r=ActionGridSingleRowCalculator.Calculate(new Vector2(width,100),2,Vector2.one*64,16,new RectOffset(),new());
            Assert.That(r.Overflow,Is.EqualTo(overflow)); Assert.That(r.Layout.CellSize.x,Is.EqualTo(64));
        }
        [Test]
        public void InvalidSettings_AreFiniteAndCopiesAreIndependent()
        {
            var settings=new ActionGridDisplaySettings { flow=ActionGridFlow.SingleRow };
            settings.singleRow.targetCellSize=new Vector2(float.NaN,float.PositiveInfinity);
            settings.singleRow.scrollbarHeight=float.NaN; settings.singleRow.centerThreshold=-5; settings.Normalize();
            var copy=JsonUtility.FromJson<ActionGridDisplaySettings>(JsonUtility.ToJson(settings)).Copy();
            copy.singleRow.centerThreshold=9;
            Assert.That(settings.singleRow.centerThreshold,Is.Zero);
            Assert.That(copy.singleRow.targetCellSize,Is.EqualTo(Vector2.one*64));
            Assert.That(copy.flow,Is.EqualTo(ActionGridFlow.SingleRow));
        }
        [TestCase(ActionGridHorizontalAlignment.Left)] [TestCase(ActionGridHorizontalAlignment.Center)] [TestCase(ActionGridHorizontalAlignment.Right)]
        public void FixedGroup_UsesChosenAlignment(ActionGridHorizontalAlignment alignment)
        {
            var options=new ActionGridSingleRowSettings { alignment=ActionGridRowAlignment.FixedGapGroup,groupAlignment=alignment };
            var r=ActionGridSingleRowCalculator.Calculate(new Vector2(500,100),2,Vector2.one*64,16,new RectOffset(),options);
            Assert.That(r.Alignment,Is.EqualTo(alignment)); Assert.That(r.Layout.Spacing.x,Is.EqualTo(16));
        }
        [Test]
        public void LayoutSerializationCannotMutateOwnedPresetPadding()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab"));
            try
            {
                var panel=go.GetComponent<ActionGridPanel>();
                panel.ConfigureDisplay(new ActionGridDisplaySettings { mode=ActionGridDisplayMode.Balanced, flow=ActionGridFlow.SingleRow });
                panel.ScrollRect.content.GetComponent<GridLayoutGroup>().padding.left=3;
                Assert.That(panel.GetDisplaySettings().balanced.padding.left,Is.EqualTo(14));
                panel.RefreshDisplayLayout();
                Assert.That(panel.ScrollRect.content.GetComponent<GridLayoutGroup>().padding.left,Is.EqualTo(14));
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void Prefab_ModeTransitionsRestoreGeometryAndHeaderDoesNotReactivate()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab"));
            try
            {
                var panel=go.GetComponent<ActionGridPanel>(); var surface=ActionGridSurfaceAuthoring.Ensure(panel);
                var content=panel.ScrollRect.content; var anchors=content.anchorMax; var pivot=content.pivot;
                var settings=panel.GetDisplaySettings(); settings.flow=ActionGridFlow.SingleRow;
                panel.ConfigureDisplay(settings); surface.ConfigureHeader(false,58,8,new RectOffset(18,18,18,18));
                Assert.That(panel.ScrollRect.horizontal,Is.True); Assert.That(panel.ScrollRect.vertical,Is.False);
                Assert.That(panel.ScrollRect.viewport.offsetMin.x,Is.Zero); Assert.That(panel.ScrollRect.viewport.offsetMax.x,Is.Zero);
                Assert.That(surface.HeaderReservation,Is.Zero); Assert.That(((RectTransform)panel.ScrollRect.transform).offsetMax.y,Is.EqualTo(-18));
                go.transform.Find("Header").gameObject.SetActive(true);
                Assert.That(surface.HeaderReservation,Is.EqualTo(66)); Assert.That(((RectTransform)panel.ScrollRect.transform).offsetMax.y,Is.EqualTo(-84));
                go.transform.Find("Header").gameObject.SetActive(false); go.SetActive(false); go.SetActive(true);
                Assert.That(surface.HeaderVisible,Is.False);
                settings.flow=ActionGridFlow.Grid; panel.ConfigureDisplay(settings);
                Assert.That(content.anchorMax,Is.EqualTo(anchors)); Assert.That(content.pivot,Is.EqualTo(pivot));
                Assert.That(panel.ScrollRect.horizontal,Is.False); Assert.That(panel.ScrollRect.vertical,Is.True);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
