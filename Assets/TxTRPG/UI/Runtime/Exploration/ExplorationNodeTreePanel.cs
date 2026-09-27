using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TxTRPG.UI.Windows;
namespace TxTRPG.UI.Exploration
{
    [DisallowMultipleComponent]
    public sealed class ExplorationNodeTreePanel : MonoBehaviour
    {
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform nodesLayer, edgesLayer;
        [SerializeField] private ExplorationTreeNodeView nodePrefab;
        [SerializeField] private ExplorationTreeEdgeView edgePrefab;
        [SerializeField] private ExplorationTreeStyle style;
        [SerializeField] private Button browseButton, latestButton;
        [SerializeField] private ExplorationTreeNavigation navigation;
        [SerializeField] private TMP_Text message;
        [SerializeField] private GameWindowService windows;
        private ExplorationTreeSnapshot snapshot;
        private ExplorationTreeLayout layout;
        private readonly Dictionary<string,ExplorationTreeNodeView> nodes=new(StringComparer.Ordinal);
        private readonly Dictionary<string,ExplorationTreeEdgeView> edges=new(StringComparer.Ordinal);
        private readonly Stack<ExplorationTreeNodeView> spareNodes=new();
        private readonly Stack<ExplorationTreeEdgeView> spareEdges=new();
        private readonly HashSet<string> visibleNodes=new(StringComparer.Ordinal), visibleEdges=new(StringComparer.Ordinal);
        private readonly List<string> removals=new();
        private Vector2 previousViewport;
        private bool rebuilding, pendingLayout;
        public ScrollRect Scroll=>scroll;
        public ExplorationTreeStyle Style=>style;
        public ExplorationTreeSnapshot Snapshot=>snapshot;
        public ExplorationTreeLayout Layout=>layout;
        public IReadOnlyDictionary<string,ExplorationTreeNodeView> VisibleNodes=>nodes;
        public IReadOnlyDictionary<string,ExplorationTreeEdgeView> VisibleEdges=>edges;
        public Button BrowseButton=>browseButton;
        public Button LatestButton=>latestButton;
        public ExplorationTreeNavigation Navigation=>navigation;
        private void OnEnable()
        {
            if(scroll!=null)scroll.onValueChanged.AddListener(OnScroll);
            if(browseButton!=null)browseButton.onClick.AddListener(EnterNavigation);
            if(latestButton!=null)latestButton.onClick.AddListener(GoToLatest);
            pendingLayout=true;
        }
        private void OnDisable()
        {
            if(scroll!=null)scroll.onValueChanged.RemoveListener(OnScroll);
            if(browseButton!=null)browseButton.onClick.RemoveListener(EnterNavigation);
            if(latestButton!=null)latestButton.onClick.RemoveListener(GoToLatest);
            foreach(var view in nodes.Values)view.ResetEffects();foreach(var view in edges.Values)view.ResetEffects();
        }
        private void OnRectTransformDimensionsChange()=>pendingLayout=true;
        private void LateUpdate()
        {
            if(scroll==null||scroll.viewport==null||snapshot==null)return;
            var size=scroll.viewport.rect.size;
            if(pendingLayout||size!=previousViewport){pendingLayout=false;Rebuild(false,false);}
        }
        public void Show(ExplorationTreeSnapshot value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            var newRun=snapshot==null||snapshot.RunId!=value.RunId;
            var newRows=snapshot==null||value.Nodes.Count>snapshot.Nodes.Count;
            var follow=newRun || newRows&&IsAtLatest();
            snapshot=value;
            if(newRun)ReleaseAll();
            Rebuild(follow,newRun);
        }
        public void ShowError(string text)
        {
            ReleaseAll();snapshot=null;layout=null;
            if(message!=null){message.text=text;message.gameObject.SetActive(true);}
        }
        private bool IsAtLatest()=>scroll==null||scroll.content==null||scroll.content.rect.height<=scroll.viewport.rect.height+2||scroll.content.rect.height-scroll.viewport.rect.height-scroll.content.anchoredPosition.y<=32f;
        public void RefreshStyle(){if(snapshot!=null)Rebuild(false,false);}
        private void Rebuild(bool follow,bool newRun)
        {
            if(snapshot==null||scroll==null||scroll.viewport==null||style==null||nodePrefab==null||edgePrefab==null)return;
            ApplyHeaderLayout();
            var old=scroll.content.anchoredPosition;previousViewport=scroll.viewport.rect.size;
            var displaySize=style.nodeSize;
            if(nodePrefab.Title!=null&&style.types!=null)
                foreach(var type in style.types)
                    if(type!=null&&!string.IsNullOrWhiteSpace(type.displayName))
                        displaySize.y=Mathf.Max(displaySize.y,92+nodePrefab.Title.GetPreferredValues(type.displayName,Mathf.Max(72,displaySize.x)-6,float.PositiveInfinity).y);
            layout=ExplorationTreeLayout.Calculate(snapshot,previousViewport.x,displaySize,style.spacing,style.padding);
            rebuilding=true;
            try
            {
                scroll.StopMovement();scroll.content.sizeDelta=new Vector2(layout.ContentSize.x,Mathf.Max(previousViewport.y,layout.ContentSize.y));
                if(newRun)old.x=-(scroll.content.rect.width-previousViewport.x)*.5f;
                scroll.content.anchoredPosition=old;
                if(follow)scroll.verticalNormalizedPosition=0;
                ClampPosition();
            }
            finally{rebuilding=false;}
            if(message!=null){message.text=layout.InvalidEdges>0?"일부 기록의 연결 정보가 올바르지 않습니다.":snapshot.Nodes.Count==0?"탐험 기록이 없습니다.":string.Empty;message.gameObject.SetActive(message.text.Length>0);}
            RenderVisible();
        }
        private void ApplyHeaderLayout()
        {
            if(browseButton==null||latestButton==null)return;
            var header=(RectTransform)browseButton.transform.parent;
            var browse=(RectTransform)browseButton.transform;var latest=(RectTransform)latestButton.transform;
            var stacked=header.rect.width<180f;var height=stacked?104f:48f;
            header.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            var scrollRoot=(RectTransform)scroll.transform;var inset=scrollRoot.offsetMax;inset.y=-height-8f;scrollRoot.offsetMax=inset;
            if(stacked)
            {
                browse.anchorMin=new Vector2(0,1);browse.anchorMax=Vector2.one;browse.pivot=new Vector2(.5f,1);browse.sizeDelta=new Vector2(0,48);browse.anchoredPosition=Vector2.zero;
                latest.anchorMin=Vector2.zero;latest.anchorMax=new Vector2(1,0);latest.pivot=new Vector2(.5f,0);latest.sizeDelta=new Vector2(0,48);latest.anchoredPosition=Vector2.zero;
            }
            else
            {
                browse.anchorMin=Vector2.zero;browse.anchorMax=new Vector2(.68f,1);browse.offsetMin=Vector2.zero;browse.offsetMax=new Vector2(-4,0);
                latest.anchorMin=new Vector2(.68f,0);latest.anchorMax=Vector2.one;latest.offsetMin=new Vector2(4,0);latest.offsetMax=Vector2.zero;
            }
        }
        public void GoToLatest()
        {
            if(windows!=null&&windows.BlocksGameplayInput||scroll==null)return;
            scroll.StopMovement();scroll.verticalNormalizedPosition=0;RenderVisible();
        }
        public void ScrollBy(Vector2 offset)
        {
            if(windows!=null&&windows.BlocksGameplayInput||scroll==null)return;
            scroll.StopMovement();scroll.content.anchoredPosition+=offset;ClampPosition();RenderVisible();
        }
        private void ClampPosition()
        {
            var position=scroll.content.anchoredPosition;
            position.x=Mathf.Clamp(position.x,-Mathf.Max(0,scroll.content.rect.width-scroll.viewport.rect.width),0);
            position.y=Mathf.Clamp(position.y,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height));
            scroll.content.anchoredPosition=position;
        }
        private void EnterNavigation()
        {
            if(windows!=null&&windows.BlocksGameplayInput)return;
            if(EventSystem.current!=null&&navigation!=null)EventSystem.current.SetSelectedGameObject(navigation.gameObject);
        }
        private void OnScroll(Vector2 _) {if(!rebuilding)RenderVisible();}
        // Stable layout owns all records; only intersecting rows acquire visual objects.
        private void RenderVisible()
        {
            if(layout==null||!isActiveAndEnabled)return;
            var min=scroll.content.anchoredPosition.y-150;var max=min+scroll.viewport.rect.height+300;
            visibleNodes.Clear();visibleEdges.Clear();
            var height=layout.Nodes.Length>1?layout.Nodes[1].Size.y:style.nodeSize.y;
            var first=LowerBound(min-height-style.spacing.y);var last=LowerBound(max+height+style.spacing.y);
            for(var i=first;i<last;i++)
            {
                var p=layout.Nodes[i];visibleNodes.Add(p.Node.Id);
                if(p.ParentIndex>=0)visibleEdges.Add(p.Node.Id);
            }
            removals.Clear();foreach(var pair in nodes)if(!visibleNodes.Contains(pair.Key))removals.Add(pair.Key);
            foreach(var id in removals){var view=nodes[id];nodes.Remove(id);view.gameObject.SetActive(false);spareNodes.Push(view);}
            removals.Clear();foreach(var pair in edges)if(!visibleEdges.Contains(pair.Key))removals.Add(pair.Key);
            foreach(var id in removals){var view=edges[id];edges.Remove(id);view.gameObject.SetActive(false);spareEdges.Push(view);}
            for(var i=first;i<last;i++)
            {
                var p=layout.Nodes[i];
                if(!nodes.TryGetValue(p.Node.Id,out var node)){node=spareNodes.Count>0?spareNodes.Pop():Instantiate(nodePrefab,nodesLayer);nodes.Add(p.Node.Id,node);}
                node.gameObject.SetActive(true);node.Bind(snapshot.RunId,p,style);
                if(p.ParentIndex<0)continue;
                if(!edges.TryGetValue(p.Node.Id,out var edge)){edge=spareEdges.Count>0?spareEdges.Pop():Instantiate(edgePrefab,edgesLayer);edges.Add(p.Node.Id,edge);}
                var parent=layout.Nodes[p.ParentIndex];edge.gameObject.SetActive(true);edge.Bind(snapshot.RunId,parent.Node.Id,p.Node.Id,parent.Outgoing,p.Incoming,p.Node.Visited,style);
            }
        }
        private int LowerBound(float depth)
        {
            var low=0;var high=layout.Nodes.Length;
            while(low<high){var mid=low+(high-low)/2;if(-layout.Nodes[mid].Center.y<depth)low=mid+1;else high=mid;}return low;
        }
        private void ReleaseAll()
        {
            foreach(var view in nodes.Values){view.gameObject.SetActive(false);spareNodes.Push(view);}nodes.Clear();
            foreach(var view in edges.Values){view.gameObject.SetActive(false);spareEdges.Push(view);}edges.Clear();
        }
#if UNITY_EDITOR
        public void Configure(ScrollRect targetScroll,RectTransform nodeLayer,RectTransform edgeLayer,ExplorationTreeNodeView node,ExplorationTreeEdgeView edge,ExplorationTreeStyle settings,Button browse,Button latest,ExplorationTreeNavigation navigator,TMP_Text notice,GameWindowService service)
        {scroll=targetScroll;nodesLayer=nodeLayer;edgesLayer=edgeLayer;nodePrefab=node;edgePrefab=edge;style=settings;browseButton=browse;latestButton=latest;navigation=navigator;message=notice;windows=service;}
#endif
    }
}
