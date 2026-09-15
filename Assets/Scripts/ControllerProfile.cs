using System;
using System.Collections.Generic;

[Serializable]
public sealed class ControllerSource
{
    public int Button = -1;
    public int Axis = -1;
    public float Rest;
    public float Pressed = 1;
    public bool Read(bool[] buttons, float[] axes)
    {
        if (Button >= 0) return Button < buttons.Length && buttons[Button];
        return Axis >= 0 && Axis < axes.Length && Math.Abs(Pressed - Rest) > .2f
            && (axes[Axis] - Rest) / (Pressed - Rest) > .55f;
    }
    public override string ToString() => Button >= 0 ? "Button " + Button : Axis >= 0 ? "Axis " + (Axis + 1) + (Pressed > Rest ? " +" : " −") : "Unbound";
}

public enum ControllerAction { None, Key, LeftClick, RightClick, ScrollUp, ScrollDown, Controls, Keyboard }

[Serializable]
public sealed class ControllerBinding
{
    public string Name;
    public ControllerSource Source = new ControllerSource();
    public ControllerAction Action;
    public int Key;
    // 1 = Shift, 2 = Ctrl, 4 = Alt. These apply to the full held chord.
    public int Modifiers;
}

[Serializable]
public sealed class ControllerProfile
{
    public int Version = 1;
    public bool Enabled = true;
    public int MoveX = 0, MoveY = 1, CursorX = 3, CursorY = 4;
    public bool InvertMoveX, InvertMoveY = true, InvertCursorX, InvertCursorY = true;
    public float DeadZone = .2f;
    public float CursorSpeed = 650;
    public float RunThreshold = .75f;
    public List<ControllerBinding> Bindings = new List<ControllerBinding>();

    public static ControllerProfile Default()
    {
        var profile = new ControllerProfile();
        profile.Bindings.Add(Button("A / Cross", 0, ControllerAction.LeftClick));
        profile.Bindings.Add(Button("B / Circle", 1, ControllerAction.Key, 27));
        profile.Bindings.Add(Button("X / Square", 2, ControllerAction.Key, 9));
        profile.Bindings.Add(Button("Y / Triangle", 3, ControllerAction.Key, 105, 4)); // Alt+I
        profile.Bindings.Add(Button("Left shoulder", 4, ControllerAction.Key, 0, 2));
        profile.Bindings.Add(Button("Right shoulder", 5, ControllerAction.Key, 0, 1));
        profile.Bindings.Add(Button("Select / Back", 6, ControllerAction.Keyboard));
        profile.Bindings.Add(Button("Start", 7, ControllerAction.Controls));
        profile.Bindings.Add(Button("Left stick click", 8, ControllerAction.Key, 32));
        profile.Bindings.Add(Button("Right stick click", 9, ControllerAction.RightClick));
        // Android vendors expose these as different axes/buttons. Capture them once instead of
        // assigning an unrelated resting axis which could hold a click or macro indefinitely.
        profile.Bindings.Add(Axis("Left trigger", -1, 1, ControllerAction.RightClick));
        profile.Bindings.Add(Axis("Right trigger", -1, 1, ControllerAction.LeftClick));
        profile.Bindings.Add(Axis("D-pad up", -1, 1, ControllerAction.Key, 1073741882)); // F1
        profile.Bindings.Add(Axis("D-pad down", -1, -1, ControllerAction.Key, 1073741883));
        profile.Bindings.Add(Axis("D-pad left", -1, -1, ControllerAction.Key, 1073741884));
        profile.Bindings.Add(Axis("D-pad right", -1, 1, ControllerAction.Key, 1073741885));
        return profile;
    }

    private static ControllerBinding Button(string name, int button, ControllerAction action, int key = 0, int mods = 0)
        => new ControllerBinding { Name = name, Source = new ControllerSource { Button = button }, Action = action, Key = key, Modifiers = mods };
    private static ControllerBinding Axis(string name, int axis, float pressed, ControllerAction action, int key = 0)
        => new ControllerBinding { Name = name, Source = new ControllerSource { Axis = axis, Pressed = pressed }, Action = action, Key = key };

    public void Validate()
    {
        MoveX = ClampAxis(MoveX); MoveY = ClampAxis(MoveY); CursorX = ClampAxis(CursorX); CursorY = ClampAxis(CursorY);
        DeadZone = FiniteClamp(DeadZone, .05f, .6f, .2f);
        CursorSpeed = FiniteClamp(CursorSpeed, 100, 1800, 650);
        RunThreshold = FiniteClamp(RunThreshold, .2f, 1, .75f);
        if (Bindings == null || Bindings.Count > 64) Bindings = Default().Bindings;
        Bindings.RemoveAll(binding => binding == null);
        foreach (var binding in Bindings)
        {
            if (binding.Source == null) binding.Source = new ControllerSource();
            binding.Source.Button = Math.Max(-1, Math.Min(19, binding.Source.Button));
            binding.Source.Axis = ClampAxis(binding.Source.Axis);
            binding.Source.Rest = FiniteClamp(binding.Source.Rest, -1, 1, 0);
            binding.Source.Pressed = FiniteClamp(binding.Source.Pressed, -1, 1, 1);
            binding.Modifiers &= 7;
        }
    }
    private static int ClampAxis(int axis) => Math.Max(-1, Math.Min(15, axis));
    private static float FiniteClamp(float value, float min, float max, float fallback)
        => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));

    public static float ReadAxis(float[] axes, int index, bool invert, float deadZone)
    {
        if (index < 0 || index >= axes.Length) return 0;
        float value = axes[index];
        float magnitude = Math.Abs(value);
        if (magnitude <= deadZone) return 0;
        value = Math.Sign(value) * Math.Min(1, (magnitude - deadZone) / (1 - deadZone));
        return invert ? -value : value;
    }
}

// Reduce all bindings together: releasing one source must not release a key held by another.
public sealed class ControllerOutput
{
    public readonly HashSet<int> Keys = new HashSet<int>();
    public int Modifiers;
    public bool Left, Right, ScrollUp, ScrollDown, Controls, Keyboard;
    public void Read(ControllerProfile profile, bool[] buttons, float[] axes, bool blocked)
    {
        Keys.Clear(); Modifiers = 0;
        Left = Right = ScrollUp = ScrollDown = Controls = Keyboard = false;
        if (blocked || !profile.Enabled) return;
        foreach (var binding in profile.Bindings)
        {
            if (!binding.Source.Read(buttons, axes)) continue;
            switch (binding.Action)
            {
                case ControllerAction.Key: if (binding.Key != 0) Keys.Add(binding.Key); Modifiers |= binding.Modifiers; break;
                case ControllerAction.LeftClick: Left = true; break;
                case ControllerAction.RightClick: Right = true; break;
                case ControllerAction.ScrollUp: ScrollUp = true; break;
                case ControllerAction.ScrollDown: ScrollDown = true; break;
                case ControllerAction.Controls: Controls = true; break;
                case ControllerAction.Keyboard: Keyboard = true; break;
            }
        }
    }
}
