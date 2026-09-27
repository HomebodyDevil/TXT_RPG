using System;
using TxTRPG.Gameplay.Exploration;
using UnityEngine;
namespace TxTRPG.UI.Exploration
{
    [CreateAssetMenu(menuName="TxTRPG/UI/Exploration Tree Style")]
    public sealed class ExplorationTreeStyle : ScriptableObject
    {
        [Serializable] public sealed class TypeImage { public string typeId; public string displayName; public Sprite sprite; }
        public TypeImage[] types=Array.Empty<TypeImage>();
        public Sprite fallbackIcon;
        public Sprite lineSprite;
        public Vector2 nodeSize=new(92,124);
        public Vector2 spacing=new(12,40);
        [Min(0)] public float padding=12;
        [Min(1)] public float lineWidth=2;
        [Min(1)] public float visitedLineWidth=4;
        public Color lineColor=new(.48f,.56f,.66f,1);
        public Color visitedLineColor=new(.95f,.78f,.38f,1);
        public Color available=new(.28f,.48f,.66f,1), active=new(.68f,.49f,.18f,1), completed=new(.25f,.49f,.38f,1), failed=new(.59f,.25f,.30f,1), unchosen=new(.25f,.28f,.34f,1);
        public void Resolve(string typeId,out string title,out Sprite icon)
        {
            foreach(var type in types) if(type!=null && string.Equals(type.typeId,typeId,StringComparison.Ordinal))
            { title=string.IsNullOrWhiteSpace(type.displayName)?"알 수 없는 노드":type.displayName; icon=type.sprite!=null?type.sprite:fallbackIcon; return; }
            title="알 수 없는 노드"; icon=fallbackIcon;
        }
        public Color ColorFor(ExplorationNodeStatus status) => status switch { ExplorationNodeStatus.Available=>available, ExplorationNodeStatus.Active=>active, ExplorationNodeStatus.Completed=>completed, ExplorationNodeStatus.Failed=>failed, _=>unchosen };
        public static string Label(ExplorationNodeStatus status) => status switch { ExplorationNodeStatus.Available=>"현재 후보", ExplorationNodeStatus.Active=>"진행 중", ExplorationNodeStatus.Completed=>"완료", ExplorationNodeStatus.Failed=>"실패", ExplorationNodeStatus.Unchosen=>"미선택", _=>"상태 미상" };
    }
}
