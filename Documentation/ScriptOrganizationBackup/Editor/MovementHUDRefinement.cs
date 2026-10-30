using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.UI;

public static class MovementHUDRefinement
{
    public static void Run()
    {
        PlayerAnimationExpansion.ConfigureAndValidate();
        const string path = "Assets/Scenes/10_World_City.unity";
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var existing = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameplayHUD>(true)).FirstOrDefault();
        if (!existing)
        {
            var go = new GameObject("GameplayHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(GameplayHUD));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            TMP_Text Label(string name, string text, float y)
            {
                var child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); child.transform.SetParent(go.transform, false);
                var rect = child.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1,1);
                rect.anchoredPosition = new Vector2(-36, y); rect.sizeDelta = new Vector2(300, 48);
                var label = child.GetComponent<TextMeshProUGUI>(); label.font = TMP_Settings.defaultFontAsset;
                label.text = text; label.fontSize = 30; label.alignment = TextAlignmentOptions.Right; label.raycastTarget = false;
                return label;
            }
            go.GetComponent<GameplayHUD>().Configure(Label("TimeText", "00:00", -28), Label("MoneyText", "$ 0", -76));
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MOVEMENT_HUD_REFINEMENT_PASSED");
    }
}
