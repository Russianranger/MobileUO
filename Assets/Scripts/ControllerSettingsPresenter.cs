using System;
using System.Collections;
using System.Linq;
using SDL2;
using UnityEngine;
using UnityEngine.UI;

public sealed class ControllerSettingsPresenter : MonoBehaviour
{
    private GameObject panel, selectionPanel;
    private Text deviceLabel;
    private Text liveLabel;
    private Coroutine capture;
    private ControllerSupport Support => ControllerSupport.Instance;

    public void Open()
    {
        if (panel != null) return;
        Support.RequireNeutral();
        panel = MobileSettingsUI.Panel("Physical controller", out var content, Close);
        deviceLabel = MobileSettingsUI.Label(content, Support.DeviceName);
        liveLabel = MobileSettingsUI.Label(content, "");
        MobileSettingsUI.Label(content, "Changes save automatically. Bind triggers and the D-pad once. Use Bind for any stick or button that differs from the defaults.");
        var enabled = MobileSettingsUI.Button(content, "", null);
        void RefreshEnabled() { enabled.GetComponentInChildren<Text>().text = "Controller: " + (Support.Profile.Enabled ? "On" : "Off"); }
        enabled.onClick.AddListener(() => { Support.Profile.Enabled = !Support.Profile.Enabled; Support.Save(); RefreshEnabled(); });
        RefreshEnabled();
        AddAxis(content, "Movement horizontal (move right)", () => Support.Profile.MoveX, v => Support.Profile.MoveX = v,
            () => Support.Profile.InvertMoveX, v => Support.Profile.InvertMoveX = v);
        AddAxis(content, "Movement vertical (move up)", () => Support.Profile.MoveY, v => Support.Profile.MoveY = v,
            () => Support.Profile.InvertMoveY, v => Support.Profile.InvertMoveY = v);
        AddAxis(content, "Cursor horizontal (move right)", () => Support.Profile.CursorX, v => Support.Profile.CursorX = v,
            () => Support.Profile.InvertCursorX, v => Support.Profile.InvertCursorX = v);
        AddAxis(content, "Cursor vertical (move up)", () => Support.Profile.CursorY, v => Support.Profile.CursorY = v,
            () => Support.Profile.InvertCursorY, v => Support.Profile.InvertCursorY = v);
        AddNumber(content, "Dead zone", () => Support.Profile.DeadZone, v => Support.Profile.DeadZone = v, .05f, "P0");
        AddNumber(content, "Cursor speed (pixels/second)", () => Support.Profile.CursorSpeed, v => Support.Profile.CursorSpeed = v, 100, "F0");
        AddNumber(content, "Run threshold", () => Support.Profile.RunThreshold, v => Support.Profile.RunThreshold = v, .05f, "P0");
        foreach (var binding in Support.Profile.Bindings) AddBinding(content, binding);
        MobileSettingsUI.Button(content, "Restore default controls", () =>
        {
            ShowSelection("Restore defaults?", choices =>
                MobileSettingsUI.Button(choices, "Restore all default controls", () => { Close(); Support.ResetDefaults(); Open(); }));
        });
    }

    private void AddAxis(Transform content, string name, Func<int> get, Action<int> set, Func<bool> inverted, Action<bool> invert)
    {
        var row = MobileSettingsUI.Row(content);
        var label = MobileSettingsUI.Label(row, "");
        void Refresh() { label.text = name + ": " + (get() < 0 ? "Off" : "Axis " + (get() + 1)) + (inverted() ? " (inverted)" : ""); }
        MobileSettingsUI.Button(row, "Bind", () => StartCapture(true, source =>
        {
            set(source.Axis); invert(source.Pressed < source.Rest); Support.Save(); Refresh();
        }), 110);
        MobileSettingsUI.Button(row, "Invert", () => { invert(!inverted()); Support.Save(); Refresh(); }, 110);
        MobileSettingsUI.Button(row, "Off", () => { set(-1); Support.Save(); Refresh(); }, 80);
        Refresh();
    }

    private void AddNumber(Transform content, string name, Func<float> get, Action<float> set, float step, string format)
    {
        var row = MobileSettingsUI.Row(content);
        var label = MobileSettingsUI.Label(row, name + ": " + get().ToString(format));
        void Change(float delta) { set(get() + delta); Support.Save(); label.text = name + ": " + get().ToString(format); }
        MobileSettingsUI.Button(row, "−", () => Change(-step), 80);
        MobileSettingsUI.Button(row, "+", () => Change(step), 80);
    }

    private void AddBinding(Transform content, ControllerBinding binding)
    {
        var row = MobileSettingsUI.Row(content);
        var sourceLabel = MobileSettingsUI.Label(row, binding.Name + " — " + binding.Source);
        MobileSettingsUI.Button(row, "Bind input", () => StartCapture(false, source =>
        {
            binding.Source = source; Support.Save(); sourceLabel.text = binding.Name + " — " + binding.Source;
        }), 150);
        MobileSettingsUI.Button(row, "Unbind", () => { binding.Source = new ControllerSource(); Support.Save(); sourceLabel.text = binding.Name + " — Unbound"; }, 110);
        var actionRow = MobileSettingsUI.Row(content);
        var actionLabel = MobileSettingsUI.Label(actionRow, "Action: " + ActionName(binding));
        MobileSettingsUI.Button(actionRow, "Choose action", () => ChooseAction(binding, () => actionLabel.text = "Action: " + ActionName(binding)), 180);
        var mods = MobileSettingsUI.Button(actionRow, "", null, 220);
        void RefreshModifiers() { mods.GetComponentInChildren<Text>().text = "Modifiers: " + ModifierName(binding.Modifiers); }
        mods.onClick.AddListener(() => { binding.Modifiers = (binding.Modifiers + 1) & 7; Support.Save(); RefreshModifiers(); });
        RefreshModifiers();
    }

    private static string ModifierName(int mods)
    {
        string value = ((mods & 2) != 0 ? "Ctrl " : "") + ((mods & 1) != 0 ? "Shift " : "") + ((mods & 4) != 0 ? "Alt" : "");
        return value.Length == 0 ? "None" : value.Trim();
    }

    private static string ActionName(ControllerBinding binding)
    {
        if (binding.Action != ControllerAction.Key) return binding.Action.ToString();
        return binding.Key == 0 ? "Modifier only" : ((SDL.SDL_Keycode)binding.Key).ToString().Replace("SDLK_", "");
    }

    private void ChooseAction(ControllerBinding binding, Action changed)
    {
        ShowSelection("Choose action for " + binding.Name, content =>
        {
            foreach (ControllerAction action in Enum.GetValues(typeof(ControllerAction)))
            {
                var choice = action;
                MobileSettingsUI.Button(content, choice == ControllerAction.Key ? "Modifier only (Ctrl / Shift / Alt)" : choice.ToString(), () =>
                { binding.Action = choice; binding.Key = 0; Support.Save(); changed(); CloseSelection(); });
            }
            // Use SDL key values directly; Unity's special-key numeric values are different.
            foreach (SDL.SDL_Keycode key in Enum.GetValues(typeof(SDL.SDL_Keycode)))
            {
                int code = (int)key;
                if (!((code >= 8 && code <= 127) || (code >= 1073741881 && code <= 1073741927))) continue;
                var choice = key;
                MobileSettingsUI.Button(content, choice.ToString().Replace("SDLK_", ""), () =>
                { binding.Action = ControllerAction.Key; binding.Key = (int)choice; Support.Save(); changed(); CloseSelection(); });
            }
        });
    }

    private void StartCapture(bool axisOnly, Action<ControllerSource> selected)
    {
        ShowSelection("Bind controller input", content =>
        {
            var prompt = MobileSettingsUI.Label(content, "Release the controls, then move the requested stick or press the desired button.");
            capture = StartCoroutine(Capture(axisOnly, selected, prompt));
        });
    }

    private IEnumerator Capture(bool axisOnly, Action<ControllerSource> selected, Text prompt)
    {
        yield return new WaitForSecondsRealtime(.5f);
        var baseline = (float[])Support.Axes.Clone();
        var buttonsAtStart = (bool[])Support.Buttons.Clone();
        float expires = Time.unscaledTime + 15;
        while (Time.unscaledTime < expires)
        {
            for (int i = 0; i < Support.Buttons.Length; i++)
            {
                if (!Support.Buttons[i]) buttonsAtStart[i] = false;
                if (!axisOnly && Support.Buttons[i] && !buttonsAtStart[i])
                {
                    selected(new ControllerSource { Button = i }); capture = null; CloseSelection(); yield break;
                }
            }
            for (int i = 0; i < Support.Axes.Length; i++)
            {
                if (Mathf.Abs(Support.Axes[i] - baseline[i]) < .65f) continue;
                selected(new ControllerSource { Axis = i, Rest = baseline[i], Pressed = Support.Axes[i] });
                capture = null; CloseSelection(); yield break;
            }
            prompt.text = "Move the requested stick or press a button/trigger. " + Mathf.CeilToInt(expires - Time.unscaledTime) + " seconds remaining.";
            yield return null;
        }
        prompt.text = "No input detected. Close and try again; check that the Thor is in gamepad mode.";
        capture = null;
    }

    private void ShowSelection(string title, Action<Transform> populate)
    {
        CloseSelection();
        selectionPanel = MobileSettingsUI.Panel(title, out var content, CloseSelection);
        populate(content);
    }
    private void CloseSelection()
    {
        if (capture != null) { StopCoroutine(capture); capture = null; }
        if (selectionPanel != null) { Destroy(selectionPanel); selectionPanel = null; }
    }
    public void Close()
    {
        CloseSelection();
        if (panel != null) { Destroy(panel); panel = null; }
        Support?.RequireNeutral();
    }
    private void Update()
    {
        if (panel == null) return;
        deviceLabel.text = Support.DeviceName;
        var buttons = Enumerable.Range(0, Support.Buttons.Length).Where(i => Support.Buttons[i]).Select(i => "Button " + i);
        var axes = Enumerable.Range(0, Support.Axes.Length).Where(i => Mathf.Abs(Support.Axes[i]) > .1f).Select(i => "Axis " + (i + 1) + ": " + Support.Axes[i].ToString("F2"));
        liveLabel.text = "Live input: " + string.Join("  ", buttons.Concat(axes));
        if (Input.GetKeyDown(KeyCode.Escape)) { if (selectionPanel != null) CloseSelection(); else Close(); }
    }
    private void OnDisable() { Close(); }
}
