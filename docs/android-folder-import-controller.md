# Android folder import and physical controllers

This change adds both features to the existing Unity/ClassicUO client. It does not add an offline server runtime.

## Import a client folder

1. Add or edit a server configuration.
2. Tap **Import client folder** at the bottom left. On Android this replaces the old manual **Mark files as downloaded** action.
3. In Android's file browser, select the folder containing the client assets themselves: `anim.mul` or `animationFrame1.uop`, art files, and `tiledata.mul`.
4. Wait for **Client ready — Save**, then save the configuration. Enter the shard address, port, and matching client version as usual. A file-download server is not required after importing.

The importer copies asset formats (`mul`, `uop`, `idx`, `def`, `enu`, `rle`, `txt`, `cfg`) and music (`mp3`, `ogg`, `wav`, `mid`, `midi`), preserving subfolders. It skips executables, DLLs, hidden directories, and root-level `Data`, `Profiles`, and `Screenshots`. MobileUO's existing `Data` folder, including character profiles and assistant settings, is retained when replacing a client.

Android restricts access to storage roots, the Downloads root, and other apps' protected `Android/data` and `Android/obb` folders. Put the client in an ordinary selectable subfolder, for example `Download/MementoClient`, or an SD-card subfolder.

Copying occurs in the background with progress and cancellation. Saving stages a complete replacement before switching directories. Failed imports retain the installed client. The swap journal is separate from named client folders, and a subsequent launch can restore the previous directory if the app stopped during a swap. The imported copy is normally moved into place on the selected storage volume; changing storage after importing requires an additional copy. The existing client is retained until the replacement is ready.

## Configure the controller

Open **MobileUO Menu → Physical controller controls**, or press the mapped **Start** button while playing. Touch controls remain available.

| Input | Initial action |
| --- | --- |
| Left stick | Character movement |
| Right stick | Visible game cursor |
| A / Cross | Left mouse button; hold to drag |
| B / Circle | Escape |
| X / Square | Tab |
| Y / Triangle | Alt+I |
| Left / right shoulder | Ctrl / Shift modifier |
| Left stick click | Space |
| Right stick click | Right mouse button |
| Select / Back | Open or dismiss the chat keyboard |
| Start | Controls editor |
| Triggers and D-pad | Capture input once with **Bind input** |

The initial stick axes follow a common legacy Unity layout. If either stick differs on your Thor, tap **Bind** on its horizontal/vertical row and move it in the direction shown. The editor displays live raw buttons and axes to make calibration visible. Triggers and the D-pad deliberately start unbound because Android controllers may expose them as different buttons or axes; after binding them, their initial actions are right/left click and F1–F4 respectively.

Every input can be rebound to a key, modifier chord, left/right mouse button, mouse-wheel direction, keyboard toggle, or Controls. An ordinary UO macro shortcut can therefore be assigned to a controller button. This does not install a separate macro engine.

Adjust dead zone, cursor speed, run threshold, and individual axis inversion. ClassicUO's existing **Always Run** setting still takes priority over the analog walk/run threshold. Bindings save automatically for this app installation. Release held controls after closing menus, changing bindings, reconnecting a controller, or returning to the app. Those transitions clear injected input before accepting new presses.

The current profile combines the controllers reported by Unity; it is intended for one active gamepad at a time.

## Build and validation

Use the project's pinned Unity editor in `ProjectSettings/ProjectVersion.txt` (6000.3.8f1), with Android build support. The new Java source plugin uses Android's document-tree picker through a retained platform Fragment and does not replace Unity's Activity. Keep rules cover the JNI entry points and Fragment recreation.

The **Android import and controller checks** workflow runs on pull requests to `master` and supports manual execution. It runs the portable core checks and compiles the Java picker against the runner's Android SDK. When `UNITY_LICENSE` is configured, it also attempts a Unity Android build and uploads the APK as an artifact. The Unity account credentials used by the existing workflow are passed through. The development build uses the existing development application settings and debug signing; it does not publish a release or merge code. An installed APK signed with a different key cannot be upgraded in place with that test APK.

Run the portable checks locally with:

```sh
dotnet run --project tests/MobileUO.CoreChecks/MobileUO.CoreChecks.csproj
```

They exercise asset replacement, profile preservation, failed-copy recovery, interrupted swaps, collision with another client folder, overlapping held keys/modifiers, signed triggers, blocked/disabled input, dead zones, inversion, and corrupt profile bounds.

Device acceptance still requires an APK run on the Thor:

- Import the Memento asset folder from internal storage and SD card; cancel one import and confirm the existing client still launches.
- Reimport while retaining a character's existing UI/profile settings; verify music and custom assets.
- Calibrate sticks, triggers, and D-pad; check walking/running, targeting, double-clicking, inventory dragging, and a modifier macro.
- Open Controls, switch apps, and disconnect/reconnect while holding a key or click; verify nothing remains held.
- Restart the app and verify that bindings persist.

Portable checks and Java/reference compilation do not establish Unity IL2CPP build success or on-device behavior.
