using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Exploration;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.UI.Exploration;
using UnityEngine;
namespace TxTRPG.Application.Tests
{
    public sealed class ExplorationTreeProjectionTests
    {
        private sealed class RandomSource:IExplorationRandomSource {public int Next(int max)=>0;}
        private static ExplorationRunState Run(int choices=3)=>new("test-run",new HealthState(100),new ExplorationNodeGenerator(choices,new[]{new ExplorationNodeWeight("combat",1)},new RandomSource()));
        [TestCase(1)] [TestCase(3)] [TestCase(8)]
        public void ProjectionRetainsAllCandidatesAndOnlyActualParents(int count)
        {
            var run=Run(count);
            for(var step=0;step<4;step++)
            {
                var selected=run.CurrentChoices[count/2];run.Select(run.CurrentChoiceSetId,selected.Id);
                var active=ExplorationTreeProjection.Create(run);Assert.That(active.Nodes.Single(n=>n.Id==selected.Id).Status,Is.EqualTo(ExplorationNodeStatus.Active));
                run.CompleteActive(selected.Id,"test");
            }
            var snapshot=ExplorationTreeProjection.Create(run);var layout=ExplorationTreeLayout.Calculate(snapshot,360,new Vector2(92,124),new Vector2(12,40),12);
            Assert.That(snapshot.Nodes.Count,Is.EqualTo(1+count*5));Assert.That(snapshot.Nodes.Count(n=>n.Visited),Is.EqualTo(4));
            foreach(var item in layout.Nodes)
            {
                if(item.Node.IsRoot){Assert.That(item.ParentIndex,Is.EqualTo(-1));continue;}
                Assert.That(layout.Nodes[item.ParentIndex].Node.Id,Is.EqualTo(item.Node.ParentId));
                Assert.That(layout.Nodes[item.ParentIndex].Node.Status,Is.Not.EqualTo(ExplorationNodeStatus.Unchosen));
            }
            Assert.That(layout.InvalidEdges,Is.Zero);
        }
        [Test]
        public void FailedAndUnknownTypeRemainVisibleWithoutDomainMutation()
        {
            var run=Run();var chosen=run.CurrentChoices[0];run.Select(run.CurrentChoiceSetId,chosen.Id);run.FailActive(chosen.Id,"test-failure");
            var snapshot=ExplorationTreeProjection.Create(run);Assert.That(snapshot.Nodes.Single(n=>n.Id==chosen.Id).Status,Is.EqualTo(ExplorationNodeStatus.Failed));
            var style=ScriptableObject.CreateInstance<ExplorationTreeStyle>();
            try{style.Resolve("unregistered",out var name,out var icon);Assert.That(name,Is.EqualTo("알 수 없는 노드"));Assert.That(run.Nodes.Count,Is.EqualTo(4));}finally{UnityEngine.Object.DestroyImmediate(style);}
        }
        [Test]
        public void DuplicateIdsAreRejectedAndCyclesOrMissingParentsNeverConnect()
        {
            ExplorationTreeNode N(string id,string parent,int depth)=>new(id,parent,depth,0,"unknown",ExplorationNodeStatus.Available,false);
            Assert.Throws<ArgumentException>(()=>new ExplorationTreeSnapshot("r",new[]{N("same","",0),N("same","",1)}));
            var snapshot=new ExplorationTreeSnapshot("r",new[]{N("a","b",0),N("b","a",0),N("c","missing",1)});
            var layout=ExplorationTreeLayout.Calculate(snapshot,320,new Vector2(92,124),new Vector2(12,40),12);
            Assert.That(layout.InvalidEdges,Is.EqualTo(3));Assert.That(layout.Nodes.All(n=>n.ParentIndex<0),Is.True);
        }
        [TestCase(180)] [TestCase(360)] [TestCase(800)]
        public void RowsRemainStableAndHorizontalOverflowPreservesReadableSize(float width)
        {
            var run=Run();var before=ExplorationTreeLayout.Calculate(ExplorationTreeProjection.Create(run),width,new Vector2(92,124),new Vector2(12,40),12);
            var node=run.CurrentChoices[1];run.Select(run.CurrentChoiceSetId,node.Id);run.CompleteActive(node.Id,"test");
            var after=ExplorationTreeLayout.Calculate(ExplorationTreeProjection.Create(run),width,new Vector2(92,124),new Vector2(12,40),12);
            foreach(var p in before.Nodes)Assert.That(after.Nodes.Single(n=>n.Node.Id==p.Node.Id).Center,Is.EqualTo(p.Center));
            Assert.That(after.ContentSize.x,Is.GreaterThanOrEqualTo(324));Assert.That(after.Nodes[1].Size.x,Is.EqualTo(92));
            Assert.That(after.Nodes[1].Center.y,Is.EqualTo(after.Nodes[3].Center.y));
        }
        [Test]
        public void InputOrderDoesNotAffectStableLayout()
        {
            var snapshot=ExplorationTreeProjection.Create(Run());var reverse=new ExplorationTreeSnapshot(snapshot.RunId,snapshot.Nodes.Reverse());
            Assert.That(reverse.Nodes.Select(n=>n.Id),Is.EqualTo(snapshot.Nodes.Select(n=>n.Id)));
        }
        [TestCase(100)] [TestCase(500)]
        public void LongRecordProjectionAndLayoutMeasurement(int stages)
        {
            var run=Run();for(var i=1;i<stages;i++){var n=run.CurrentChoices[0];run.Select(run.CurrentChoiceSetId,n.Id);run.CompleteActive(n.Id,"test");}
            var allocated=GC.GetAllocatedBytesForCurrentThread();var clock=Stopwatch.StartNew();
            var snapshot=ExplorationTreeProjection.Create(run);var layout=ExplorationTreeLayout.Calculate(snapshot,360,new Vector2(92,124),new Vector2(12,40),12);
            clock.Stop();var bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Assert.That(layout.Nodes.Length,Is.EqualTo(1+3*stages));Assert.That(layout.Nodes.Count(n=>n.ParentIndex>=0),Is.EqualTo(3*stages));
            UnityEngine.Debug.Log($"TREE_PROJECTION_MEASUREMENT stages={stages} nodes={layout.Nodes.Length} edges={3*stages} ms={clock.Elapsed.TotalMilliseconds:F3} allocatedBytes={bytes}");
        }
    }
}
