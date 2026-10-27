using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    // Only the list needs a vertical layout. Each row has two explicit columns,
    // independent of the preferred/native size of its button's sprite.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SettingsRowLayout : UIBehaviour
    {
        public TMP_Text label;
        public Button button;
        [Min(100)] public float buttonWidth=300;
        [Min(20)] public float buttonHeight=45;
        [Min(0)] public float gap=24;
        [Min(24)] public float rowHeight=56;
        protected override void OnEnable(){base.OnEnable();Apply();}
        protected override void OnRectTransformDimensionsChange(){base.OnRectTransformDimensionsChange();Apply();}
        public void Apply()
        {
            if(!label || !button || !(transform is RectTransform row))return;
            var layout=GetComponent<HorizontalLayoutGroup>();if(layout)layout.enabled=false;
            var element=GetComponent<LayoutElement>()??gameObject.AddComponent<LayoutElement>();
            element.minHeight=element.preferredHeight=rowHeight;element.flexibleHeight=0;
            float width=row.rect.width>0?Mathf.Min(buttonWidth,row.rect.width*.5f):buttonWidth;
            var key=(RectTransform)button.transform;
            key.anchorMin=key.anchorMax=key.pivot=new Vector2(1,.5f);
            key.sizeDelta=new Vector2(width,buttonHeight);key.anchoredPosition=Vector2.zero;
            var text=label.rectTransform;text.anchorMin=Vector2.zero;text.anchorMax=Vector2.one;
            text.offsetMin=Vector2.zero;text.offsetMax=new Vector2(-width-gap,0);
            label.alignment=TextAlignmentOptions.MidlineLeft;label.color=Color.white;label.raycastTarget=false;
            label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=24;
            label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;
        }
        public static void Configure(Transform row,TMP_Text label,Button button)
        {
            if(!row || !label || !button)return;
            var layout=row.GetComponent<SettingsRowLayout>()??row.gameObject.AddComponent<SettingsRowLayout>();
            layout.label=label;layout.button=button;layout.Apply();
        }
    }
}
