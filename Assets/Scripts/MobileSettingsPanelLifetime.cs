using UnityEngine;

public sealed class MobileSettingsPanelLifetime : MonoBehaviour
{
    private void OnEnable() { MobileSettingsUI.OpenPanels++; }
    private void OnDisable() { MobileSettingsUI.OpenPanels = System.Math.Max(0, MobileSettingsUI.OpenPanels - 1); }
}
