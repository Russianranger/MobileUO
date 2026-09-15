using System;
using UnityEngine;
using UnityEngine.UI;

// Runtime-built uGUI controls keep these additions independent of serialized scene references.
public static class MobileSettingsUI
{
    public static int OpenPanels { get; internal set; }
    public static bool BlocksGameInput => OpenPanels > 0;
    private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static GameObject Panel(string title, out Transform content, Action close)
    {
        var root = new GameObject(title, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MobileSettingsPanelLifetime));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1500 + OpenPanels;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(root.transform, false);
        Stretch(background.GetComponent<RectTransform>());
        background.GetComponent<Image>().color = new Color(.055f, .065f, .085f, .99f);
        var layout = background.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 20, 20);
        layout.spacing = 12;
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        var header = Row(background.transform);
        Label(header, title, 30);
        if (close != null) Button(header, "Close", close, 130);

        var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect), typeof(LayoutElement));
        scroll.transform.SetParent(background.transform, false);
        scroll.GetComponent<Image>().color = new Color(.1f, .11f, .14f);
        scroll.GetComponent<Mask>().showMaskGraphic = true;
        scroll.GetComponent<LayoutElement>().flexibleHeight = 1;
        var body = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        body.transform.SetParent(scroll.transform, false);
        var rect = body.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = Vector2.zero;
        var vertical = body.GetComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(16, 16, 12, 12); vertical.spacing = 10;
        vertical.childControlHeight = vertical.childControlWidth = true; vertical.childForceExpandHeight = false;
        body.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = scroll.GetComponent<ScrollRect>();
        sr.content = rect; sr.viewport = scroll.GetComponent<RectTransform>(); sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        content = body.transform;
        return root;
    }

    public static Transform Row(Transform parent)
    {
        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().minHeight = 58;
        var group = row.GetComponent<HorizontalLayoutGroup>();
        group.spacing = 12; group.childControlHeight = group.childControlWidth = true;
        group.childForceExpandWidth = false;
        return row.transform;
    }

    public static Text Label(Transform parent, string text, int size = 23)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<Text>(); label.font = Font; label.fontSize = size;
        label.color = Color.white; label.text = text; label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap; label.raycastTarget = false;
        var element = go.GetComponent<LayoutElement>(); element.flexibleWidth = 1; element.minHeight = 48;
        return label;
    }

    public static Button Button(Transform parent, string text, Action clicked, float width = -1)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(.19f, .25f, .33f);
        var element = go.GetComponent<LayoutElement>(); element.minHeight = 56;
        if (width > 0) element.preferredWidth = width; else element.flexibleWidth = 1;
        var label = Label(go.transform, text, 22); label.alignment = TextAnchor.MiddleCenter;
        Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(8, 0); label.rectTransform.offsetMax = new Vector2(-8, 0);
        var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => clicked?.Invoke());
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

}
