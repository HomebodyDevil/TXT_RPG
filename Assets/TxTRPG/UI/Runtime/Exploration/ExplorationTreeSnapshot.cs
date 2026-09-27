using System;
using System.Collections.Generic;
using System.Linq;
using TxTRPG.Gameplay.Exploration;
using UnityEngine;
namespace TxTRPG.UI.Exploration
{
    public readonly struct ExplorationTreeNode
    {
        public ExplorationTreeNode(string id,string parentId,int depth,int sibling,string typeId,ExplorationNodeStatus status,bool visited)
        { Id=id; ParentId=parentId; Depth=depth; Sibling=sibling; TypeId=typeId; Status=status; Visited=visited; }
        public string Id { get; }
        public string ParentId { get; }
        public int Depth { get; }
        public int Sibling { get; }
        public string TypeId { get; }
        public ExplorationNodeStatus Status { get; }
        public bool Visited { get; }
        public bool IsRoot => TypeId==ExplorationNodeTypeIds.Root && Depth==-1;
    }
    public sealed class ExplorationTreeSnapshot
    {
        public string RunId { get; }
        public IReadOnlyList<ExplorationTreeNode> Nodes { get; }
        public ExplorationTreeSnapshot(string runId,IEnumerable<ExplorationTreeNode> nodes)
        {
            if(string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("Run ID is required.");
            RunId=runId;
            var ordered=(nodes??throw new ArgumentNullException(nameof(nodes))).OrderBy(n=>n.Depth).ThenBy(n=>n.Sibling).ThenBy(n=>n.Id,StringComparer.Ordinal).ToArray();
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var node in ordered)
                if(string.IsNullOrWhiteSpace(node.Id)||!ids.Add(node.Id)||node.Depth< -1)throw new ArgumentException("Invalid or duplicate tree node identity/depth.");
            Nodes=Array.AsReadOnly(ordered);
        }
    }
    public readonly struct ExplorationTreePlacement
    {
        public ExplorationTreePlacement(ExplorationTreeNode node,Vector2 center,Vector2 size,int parentIndex)
        { Node=node; Center=center; Size=size; ParentIndex=parentIndex; }
        public ExplorationTreeNode Node { get; }
        public Vector2 Center { get; }
        public Vector2 Size { get; }
        public int ParentIndex { get; }
        public Vector2 Incoming => Center+Vector2.up*Size.y*.5f;
        public Vector2 Outgoing => Center-Vector2.up*Size.y*.5f;
    }
    public sealed class ExplorationTreeLayout
    {
        public ExplorationTreePlacement[] Nodes { get; private set; }
        public Vector2 ContentSize { get; private set; }
        public int InvalidEdges { get; private set; }
        public static ExplorationTreeLayout Calculate(ExplorationTreeSnapshot snapshot,float viewportWidth,Vector2 nodeSize,Vector2 gap,float padding)
        {
            nodeSize=new Vector2(Mathf.Max(72,nodeSize.x),Mathf.Max(100,nodeSize.y));
            gap=new Vector2(Mathf.Max(4,gap.x),Mathf.Max(16,gap.y)); padding=Mathf.Max(0,padding);
            var nodes=snapshot.Nodes; var counts=new Dictionary<int,int>();
            foreach(var n in nodes) counts[n.Depth]=counts.TryGetValue(n.Depth,out var count)?count+1:1;
            var maxCount=counts.Count==0?0:counts.Values.Max();
            var width=Mathf.Max(Mathf.Max(1,viewportWidth),padding*2+maxCount*nodeSize.x+Mathf.Max(0,maxCount-1)*gap.x);
            var result=new ExplorationTreeLayout { Nodes=new ExplorationTreePlacement[nodes.Count] };
            var indices=new Dictionary<string,int>(StringComparer.Ordinal); for(var i=0;i<nodes.Count;i++)indices.Add(nodes[i].Id,i);
            var rowTop=padding; var start=0;
            while(start<nodes.Count)
            {
                var end=start+1; while(end<nodes.Count&&nodes[end].Depth==nodes[start].Depth)end++;
                var root=nodes[start].IsRoot&&end==start+1; var size=root?new Vector2(64,38):nodeSize;
                var rowWidth=(end-start)*size.x+(end-start-1)*gap.x; var x=(width-rowWidth)*.5f;
                for(var i=start;i<end;i++)
                {
                    var n=nodes[i]; var parent=-1;
                    if(!n.IsRoot)
                    {
                        // Strict depth descent rules out cycles without recursive traversal.
                        if(!string.IsNullOrEmpty(n.ParentId)&&indices.TryGetValue(n.ParentId,out var candidate)&&nodes[candidate].Depth==n.Depth-1)parent=candidate;
                        else result.InvalidEdges++;
                    }
                    result.Nodes[i]=new ExplorationTreePlacement(n,new Vector2(x+size.x*.5f,-rowTop-size.y*.5f),size,parent);
                    x+=size.x+gap.x;
                }
                rowTop+=size.y+gap.y; start=end;
            }
            result.ContentSize=new Vector2(width,Mathf.Max(1,rowTop-gap.y+padding)); return result;
        }
    }
}
