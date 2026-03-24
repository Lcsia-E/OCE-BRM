using SpatialSys.UnitySDK;   // Spatial.io SDK namespace: gives access to Avatar, GUI, Data Store, etc.
using System.Collections;    // Required for IEnumerator and coroutines
using UnityEngine;           // Core Unity namespace: GameObject, MonoBehaviour, Vector3, etc.

public class PlayerRestrictions : MonoBehaviour, IAvatarInputActionsListener
{
    // ==========================================
    // CONFIGURATION & TOGGLE STATES
    // ==========================================

    [Header("INITIAL SETTINGS")]
    public bool activateAtStart = true;    // If true, applies locks as soon as the scene loads

    [Header("RESTRICTION TOGGLES")]
    public bool lockMovement = false;      // Prevents walking/running
    public bool lockJump = true;           // Prevents jumping (essential for the Skinner Box)
    public bool lockSprint = false;        // Prevents sprinting
    public bool disableEmotes = true;      // Prevents opening the emote wheel

    [Header("VR CAMERA SETTINGS")]
    public XRCameraMode vrCameraMode = XRCameraMode.FirstPerson; // Set the POV for VR headset users
    public bool allowXRSwitch = false;     // Set to false to prevent XR players from switching modes

    // =======================
    // UNITY LIFECYCLE: START
    // =======================

    void Start()
    {
        // 1) Apply locks and camera settings automatically if the toggle is active
        if (activateAtStart)
        {
            ApplyRestrictions();
        }
    }

    // =====================================
    // PUBLIC METHODS FOR TOGGLE BUTTONS
    // =====================================

    public void ApplyRestrictions()
    {
        // a) StartAvatarInputCapture overrides Spatial's default behavior
        SpatialBridge.inputService.StartAvatarInputCapture(lockMovement, lockJump, lockSprint, false, this);

        // b) Emotes control
        SpatialBridge.inputService.SetEmoteBindingsEnabled(!disableEmotes);

        // c) Set the XR Camera Mode for VR users
        SpatialBridge.cameraService.xrCameraMode = vrCameraMode;

        // d) OFFICIAL FIX: Prevents switching between 1st and 3rd person in XR
        SpatialBridge.cameraService.allowPlayerToSwitchXRCameraMode = allowXRSwitch;

        SpatialBridge.coreGUIService.DisplayToastMessage("Experimental Restrictions Applied");
    }

    public void ReleaseAllControls()
    {
        lockMovement = false;
        lockJump = false;
        lockSprint = false;
        disableEmotes = false;
        allowXRSwitch = true;
        vrCameraMode = XRCameraMode.Default;

        SpatialBridge.inputService.ReleaseInputCapture(this);
        SpatialBridge.inputService.SetEmoteBindingsEnabled(true);
        
        SpatialBridge.cameraService.xrCameraMode = XRCameraMode.Default;
        SpatialBridge.cameraService.allowPlayerToSwitchXRCameraMode = true;

        SpatialBridge.coreGUIService.DisplayToastMessage("Controls Restored");
    }

    // =======================================================
    // IAvatarInputActionsListener MANDATORY IMPLEMENTATION
    // =======================================================

    public void OnAvatarMoveInput(InputPhase phase, Vector2 move) { }
    public void OnAvatarJumpInput(InputPhase phase) { }
    public void OnAvatarSprintInput(InputPhase phase) { }
    public void OnAvatarActionInput(InputPhase phase) { }
    public void OnAvatarAutoSprintToggled(bool on) { }
    public void OnInputCaptureStarted(InputCaptureType type) { }
    public void OnInputCaptureStopped(InputCaptureType type) { }
}