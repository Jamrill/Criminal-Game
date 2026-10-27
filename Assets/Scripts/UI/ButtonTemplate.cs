using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    public static class ButtonTemplate
    {
        public static Button Create(Button template,Transform parent,string name,out TMP_Text label)
        {
            Button button;
            if(template)button=Object.Instantiate(template,parent,false);
            else
            {
                var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));
                go.transform.SetParent(parent,false);button=go.GetComponent<Button>();
                button.targetGraphic=go.GetComponent<Image>();
            }
            button.name=name;
            var rect=(RectTransform)button.transform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition3D=Vector3.zero;rect.localRotation=Quaternion.identity;rect.localScale=Vector3.one;
            rect.sizeDelta=new Vector2(100,100);
            label=button.GetComponentInChildren<TMP_Text>(true);
            if(!label)
            {
                var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));
                go.transform.SetParent(button.transform,false);label=go.GetComponent<TextMeshProUGUI>();
                label.font=TMP_Settings.defaultFontAsset;
            }
            label.raycastTarget=false;
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
            return button;
        }
    }
}
