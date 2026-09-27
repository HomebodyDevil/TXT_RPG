using System;
using System.Linq;
using NUnit.Framework;
using TxTRPG.UI.Exploration;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace TxTRPG.UI.Tests
{
    [ExecuteAlways]
    public sealed class ExplorationTestEventSystem : EventSystem { }

    public sealed class ExplorationInitialSelectionTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ReplacementClearsOnlyOwnedFocusAndHideClearsSelection(int index)
        {
            var previous=EventSystem.current;
            var eventsObject=new GameObject("Events",typeof(ExplorationTestEventSystem));
            var root=new GameObject("List",typeof(RectTransform),typeof(ScrollRect),typeof(ExplorationNodeChoiceCardList));
            var content=new GameObject("Content",typeof(RectTransform),typeof(ExplorationNodeChoiceLayoutGroup)); content.transform.SetParent(root.transform,false);
            var other=new GameObject("Other",typeof(RectTransform),typeof(Button));
            try
            {
                var events=eventsObject.GetComponent<EventSystem>(); EventSystem.current=events; var list=root.GetComponent<ExplorationNodeChoiceCardList>();
                var scroll=root.GetComponent<ScrollRect>(); scroll.content=(RectTransform)content.transform; scroll.viewport=(RectTransform)root.transform;
                list.ConfigureForEditor(scroll,scroll.content,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/ExplorationNodeChoiceCard.prefab").GetComponent<ExplorationNodeChoiceCardView>(),content.GetComponent<ExplorationNodeChoiceLayoutGroup>());
                var data=Enumerable.Range(0,3).Select(i=>new ExplorationNodeChoiceCardData(new ExplorationNodeChoiceRequest("r","s",i.ToString()),"A","B","",null,true)).ToArray();
                list.Show(data,_=>{}); Assert.That(events.currentSelectedGameObject,Is.Null);
                var reused=list.Cards[index]; events.SetSelectedGameObject(reused.gameObject); list.Show(data,_=>{});
                Assert.That(list.Cards[index],Is.SameAs(reused)); Assert.That(events.currentSelectedGameObject,Is.Null);
                events.SetSelectedGameObject(other); list.Show(data,_=>{}); Assert.That(events.currentSelectedGameObject,Is.SameAs(other));
                events.SetSelectedGameObject(reused.gameObject); list.Hide(); Assert.That(events.currentSelectedGameObject,Is.Null);
                list.Show(Array.Empty<ExplorationNodeChoiceCardData>(),_=>{}); Assert.That(list.Cards.All(c=>!c.gameObject.activeSelf),Is.True);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(other); Object.DestroyImmediate(eventsObject); if(previous!=null) EventSystem.current=previous; }
        }
    }
}
