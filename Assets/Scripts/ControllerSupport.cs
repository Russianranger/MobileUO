using System;
using System.Linq;
using ClassicUO;
using ClassicUO.Game.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using Time = UnityEngine.Time;

public sealed class ControllerSupport : MonoBehaviour
{
    public static ControllerSupport Instance { get; private set; }
    public ControllerProfile Profile { get; private set; }
    public readonly bool[] Buttons = new bool[20];
    public readonly float[] Axes = new float[16];
    public readonly ControllerOutput Output = new ControllerOutput();
    public Vector2 Movement { get; private set; }
    public Vector2 Pointer { get; private set; }
    public bool PointerActive { get; private set; }
    public string DeviceName { get; private set; } = "No controller detected";
    public bool Connected { get; private set; }
    public bool Suspended => !Application.isFocused || MenuPresenter.IsMenuOpened || MobileSettingsUI.BlocksGameInput;
    private const string PreferencesKey = "mobileuo.controller.v1";
    private int sampleFrame = -1;
    private float nextDeviceCheck;
    private bool controlsHeld, keyboardHeld, neutralRequired = true;
    private Vector3 previousMouse;
    private string deviceIdentity = "";
    private EventSystem navigationSystem;
    private bool previousNavigation, navigationOverridden;
    private ControllerSettingsPresenter presenter;

    private void Awake()
    {
        Instance = this;
        try { Profile = JsonUtility.FromJson<ControllerProfile>(PlayerPrefs.GetString(PreferencesKey, "")); }
        catch (Exception e) { Debug.LogWarning("Could not load controller bindings: " + e.Message); }
        if (Profile == null || Profile.Version != 1) Profile = ControllerProfile.Default();
        Profile.Validate();
        Pointer = new Vector2(Screen.width / 2f, Screen.height / 2f);
        previousMouse = Input.mousePosition;
        presenter = gameObject.AddComponent<ControllerSettingsPresenter>();
    }

    public void Save()
    {
        Profile.Validate();
        PlayerPrefs.SetString(PreferencesKey, JsonUtility.ToJson(Profile)); PlayerPrefs.Save();
        RequireNeutral();
    }
    public void ResetDefaults() { Profile = ControllerProfile.Default(); Save(); }
    public void OpenSettings() { presenter.Open(); }

    private void Update()
    {
        Sample();
        // Legacy uGUI otherwise submits the selected button when gamepad A is used in the client.
        bool suppress = MobileSettingsUI.BlocksGameInput || (Connected && Profile.Enabled && Client.Game != null);
        if (suppress && !navigationOverridden && EventSystem.current != null)
        {
            navigationSystem = EventSystem.current; previousNavigation = navigationSystem.sendNavigationEvents;
            navigationSystem.sendNavigationEvents = false; navigationOverridden = true;
        }
        else if (!suppress) RestoreNavigation();
    }

    public void Sample()
    {
        if (sampleFrame == Time.frameCount) return;
        sampleFrame = Time.frameCount;
        if (Time.unscaledTime >= nextDeviceCheck)
        {
            string[] names = Input.GetJoystickNames();
            string identity = string.Join("|", names);
            if (identity != deviceIdentity) { deviceIdentity = identity; RequireNeutral(); }
            Connected = names.Any(name => !string.IsNullOrEmpty(name));
            DeviceName = Connected ? string.Join(", ", names.Where(name => !string.IsNullOrEmpty(name))) : "No controller detected";
            nextDeviceCheck = Time.unscaledTime + .25f;
        }
        for (int i = 0; i < Buttons.Length; i++) Buttons[i] = Connected && Input.GetKey(KeyCode.JoystickButton0 + i);
        for (int i = 0; i < Axes.Length; i++) Axes[i] = Connected ? Input.GetAxisRaw("MobileUOAxis" + (i + 1)) : 0;

        bool blocked = Suspended || !Connected || !Profile.Enabled || Client.Game == null;
        if (blocked) neutralRequired = true;
        if (neutralRequired && !blocked)
        {
            // Ignore actions held through reconnects, focus changes or the binding editor.
            bool held = Profile.Bindings.Any(binding => binding.Source.Read(Buttons, Axes));
            held |= Mathf.Abs(Axis(Profile.MoveX, false)) + Mathf.Abs(Axis(Profile.MoveY, false)) > 0;
            if (!held) neutralRequired = false;
        }
        blocked |= neutralRequired;
        Output.Read(Profile, Buttons, Axes, blocked);
        Movement = blocked ? Vector2.zero : Vector2.ClampMagnitude(new Vector2(Axis(Profile.MoveX, Profile.InvertMoveX), Axis(Profile.MoveY, Profile.InvertMoveY)), 1);
        Vector2 cursor = blocked ? Vector2.zero : new Vector2(Axis(Profile.CursorX, Profile.InvertCursorX), Axis(Profile.CursorY, Profile.InvertCursorY));
        bool physicalPointer = Input.touchCount > 0 || (Input.mousePosition - previousMouse).sqrMagnitude > 1
            || Input.GetMouseButton(0) || Input.GetMouseButton(1);
        if (physicalPointer && PointerActive && (Output.Left || Output.Right))
        {
            neutralRequired = true; Movement = Vector2.zero; Output.Read(Profile, Buttons, Axes, true);
        }
        previousMouse = Input.mousePosition;
        if (physicalPointer || blocked) PointerActive = false;
        else if (cursor.sqrMagnitude > 0 || Output.Left || Output.Right || Output.ScrollUp || Output.ScrollDown)
            PointerActive = true;
        if (PointerActive)
        {
            Pointer += cursor * Profile.CursorSpeed * Mathf.Min(.05f, Time.unscaledDeltaTime);
            Pointer = new Vector2(Mathf.Clamp(Pointer.x, 0, Screen.width - 1), Mathf.Clamp(Pointer.y, 0, Screen.height - 1));
        }
        else if (physicalPointer)
            Pointer = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;

        if (Output.Controls && !controlsHeld) presenter.Open();
        if (Output.Keyboard && !keyboardHeld) ToggleKeyboard();
        controlsHeld = Output.Controls; keyboardHeld = Output.Keyboard;
        if (MobileSettingsUI.BlocksGameInput) { Movement = Vector2.zero; Output.Read(Profile, Buttons, Axes, true); PointerActive = false; }
    }

    private float Axis(int index, bool invert) => ControllerProfile.ReadAxis(Axes, index, invert, Profile.DeadZone);

    public void RequireNeutral()
    {
        neutralRequired = true; Movement = Vector2.zero; PointerActive = false;
        Output.Read(Profile ?? ControllerProfile.Default(), Buttons, Axes, true);
        if (Client.Game?.Scene is GameScene scene) scene.JoystickInput = Microsoft.Xna.Framework.Vector2.Zero;
        Client.Game?.ReleaseControllerInput();
    }

    private void ToggleKeyboard()
    {
        if (ClassicUO.GameController.TouchScreenKeyboard != null && ClassicUO.GameController.TouchScreenKeyboard.active)
        {
            ClassicUO.GameController.TouchScreenKeyboard.active = false;
            ClassicUO.GameController.TouchScreenKeyboard = null;
            return;
        }
        // Focus the existing chat textbox so MobileUO owns text entry and its Send handling.
        var chat = ClassicUO.Game.Managers.UIManager.SystemChat;
        if (chat != null)
        {
            chat.SetFocus();
            ClassicUO.GameController.TouchScreenKeyboard = TouchScreenKeyboard.Open(chat.TextBoxControl.Text,
                TouchScreenKeyboardType.Default, false, false);
        }
    }

    private void OnApplicationFocus(bool focus) { if (!focus) RequireNeutral(); }
    private void OnApplicationPause(bool paused) { if (paused) RequireNeutral(); }
    private void OnDisable() { RequireNeutral(); RestoreNavigation(); }
    private void OnDestroy() { RestoreNavigation(); if (Instance == this) Instance = null; }
    private void RestoreNavigation()
    {
        if (navigationOverridden && navigationSystem != null) navigationSystem.sendNavigationEvents = previousNavigation;
        navigationOverridden = false;
    }
}
