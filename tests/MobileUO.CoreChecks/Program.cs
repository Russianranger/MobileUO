using System;
using System.IO;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}

foreach (string name in new[] { "../client", "/client", "a/b", "a\\b", ".", "..", "", " ", "a:", "trailing.", " padded " })
    Check(!ClientImportTransaction.ValidConfigurationName(name), "Unsafe configuration name accepted: " + name);
Check(ClientImportTransaction.ValidConfigurationName("Ultima Memento"), "Ordinary server name rejected");

string root = Path.Combine(Path.GetTempPath(), "MobileUO-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string source = Path.Combine(root, "source");
    string installed = Path.Combine(root, "client");
    Directory.CreateDirectory(Path.Combine(source, "Music", "Digital"));
    Directory.CreateDirectory(Path.Combine(source, "Data"));
    File.WriteAllText(Path.Combine(source, "anim.mul"), "new assets");
    File.WriteAllText(Path.Combine(source, "Music", "Digital", "song.mp3"), "music");
    File.WriteAllText(Path.Combine(source, "Data", "profile.json"), "imported profile");
    Directory.CreateDirectory(Path.Combine(installed, "Data"));
    File.WriteAllText(Path.Combine(installed, "anim.mul"), "old assets");
    File.WriteAllText(Path.Combine(installed, "obsolete.mul"), "obsolete assets");
    File.WriteAllText(Path.Combine(installed, "Data", "profile.json"), "my existing character");
    ClientImportTransaction.Install(source, installed, installed);
    Check(File.ReadAllText(Path.Combine(installed, "anim.mul")) == "new assets", "Assets were not replaced");
    Check(File.ReadAllText(Path.Combine(installed, "Data", "profile.json")) == "my existing character", "Character profile was overwritten");
    Check(File.Exists(Path.Combine(installed, "Music", "Digital", "song.mp3")), "Music hierarchy was lost");
    Check(!File.Exists(Path.Combine(installed, "obsolete.mul")), "Old assets were mixed with the new client");
    Check(File.Exists(Path.Combine(source, "anim.mul")), "Source files were removed");
    try { ClientImportTransaction.Install(Path.Combine(root, "missing"), installed, installed); throw new Exception("Missing source accepted"); }
    catch (DirectoryNotFoundException) { }
    Check(File.ReadAllText(Path.Combine(installed, "anim.mul")) == "new assets", "Failed copy damaged the current install");
    Check(!Directory.Exists(ClientImportTransaction.WorkPath(installed, "staging")), "Failed staging folder was not cleaned up");
    string previous = ClientImportTransaction.WorkPath(installed, "previous");
    Directory.Move(installed, previous);
    ClientImportTransaction.Recover(installed);
    Check(File.Exists(Path.Combine(installed, "anim.mul")), "Interrupted swap could not recover old client");
    Directory.CreateDirectory(previous);
    File.WriteAllText(Path.Combine(previous, "old.mul"), "old");
    ClientImportTransaction.Recover(installed);
    Check(!Directory.Exists(previous), "Completed swap did not clean previous copy");
    // A configuration name which looks like an old-style backup must never be deleted as cleanup.
    string otherClient = installed + ".import-previous";
    Directory.CreateDirectory(otherClient);
    File.WriteAllText(Path.Combine(otherClient, "anim.mul"), "another client");
    ClientImportTransaction.Install(source, installed, installed);
    Check(File.ReadAllText(Path.Combine(otherClient, "anim.mul")) == "another client", "Import cleanup damaged a different client folder");
    File.CreateSymbolicLink(Path.Combine(installed, "Data", "bad-link"), Path.Combine(source, "anim.mul"));
    try { ClientImportTransaction.Install(source, installed, installed, true); throw new Exception("Symbolic profile link accepted"); }
    catch (IOException) { }
    Check(File.Exists(Path.Combine(source, "anim.mul")), "Failed installation lost its staged import before retry");
    Check(File.ReadAllText(Path.Combine(installed, "Data", "profile.json")) == "my existing character", "Profile copy failure damaged the installed client");
    File.Delete(Path.Combine(installed, "Data", "bad-link"));
    ClientImportTransaction.Install(source, installed, installed, true);
    Check(!Directory.Exists(source) && File.Exists(Path.Combine(installed, "anim.mul")), "Staged client was not moved into place");
}
finally { Directory.Delete(root, true); }

var profile = new ControllerProfile();
profile.Bindings.Add(new ControllerBinding { Source = new ControllerSource { Button = 0 }, Action = ControllerAction.Key, Key = 113, Modifiers = 2 });
profile.Bindings.Add(new ControllerBinding { Source = new ControllerSource { Button = 1 }, Action = ControllerAction.Key, Key = 113, Modifiers = 1 });
profile.Bindings.Add(new ControllerBinding { Source = new ControllerSource { Axis = 2, Rest = -1, Pressed = 1 }, Action = ControllerAction.LeftClick });
var output = new ControllerOutput();
var buttons = new bool[20]; var axes = new float[16]; axes[2] = -1;
buttons[0] = buttons[1] = true;
output.Read(profile, buttons, axes, false);
Check(output.Keys.Count == 1 && output.Keys.Contains(113) && output.Modifiers == 3, "Overlapping keys/chords were not combined");
Check(!output.Left, "Resting signed trigger pressed the mouse");
buttons[0] = false;
output.Read(profile, buttons, axes, false);
Check(output.Keys.Contains(113) && output.Modifiers == 1, "Releasing one source released another source's held key");
axes[2] = 1;
output.Read(profile, buttons, axes, false);
Check(output.Left, "Signed trigger failed to press");
output.Read(profile, buttons, axes, true);
Check(!output.Left && output.Keys.Count == 0 && output.Modifiers == 0, "Blocked input was not fully released");
profile.Enabled = false;
output.Read(profile, buttons, axes, false);
Check(!output.Left && output.Keys.Count == 0, "Disabled controller delivered input");
var negative = new ControllerSource { Axis = 0, Rest = 0, Pressed = -1 };
axes[0] = -.8f;
Check(negative.Read(buttons, axes), "Negative axis binding failed");
axes[0] = .8f;
Check(!negative.Read(buttons, axes), "Opposite axis direction activated binding");
axes[0] = .15f;
Check(ControllerProfile.ReadAxis(axes, 0, false, .2f) == 0, "Stick drift escaped the dead zone");
axes[0] = -1;
Check(ControllerProfile.ReadAxis(axes, 0, true, .2f) == 1, "Stick inversion failed");
Check(ControllerProfile.ReadAxis(axes, -1, false, .2f) == 0, "Disabled stick axis was read");
profile.DeadZone = float.NaN; profile.CursorSpeed = float.PositiveInfinity; profile.MoveX = 100;
profile.Validate();
Check(profile.DeadZone == .2f && profile.CursorSpeed == 650 && profile.MoveX == 15, "Invalid profile values were not repaired");
Console.WriteLine($"PASS: {checks} import and controller checks.");
