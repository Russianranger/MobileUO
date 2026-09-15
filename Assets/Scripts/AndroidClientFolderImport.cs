using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class AndroidClientFolderImport
{
    [Serializable] public class Progress
    {
        public string state;
        public string message;
        public long copied;
        public long total;
    }

    public static async Task<string> Pick(string storageRoot, Action<Progress> changed)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        string root = Path.Combine(storageRoot, ".mobileuo-imports", "incoming");
        Directory.CreateDirectory(root);
        string destination = Path.Combine(root, Guid.NewGuid().ToString("N"));
        using (var picker = new AndroidJavaClass("com.mobileuo.importer.ClientFolderPicker"))
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            picker.CallStatic("begin", activity, destination);
            while (true)
            {
                await Task.Delay(150);
                var progress = JsonUtility.FromJson<Progress>(picker.CallStatic<string>("status"));
                changed?.Invoke(progress);
                if (progress.state == "complete") return destination;
                if (progress.state == "cancelled") return null;
                if (progress.state == "error") throw new IOException(progress.message);
            }
        }
#elif UNITY_EDITOR
        string source = UnityEditor.EditorUtility.OpenFolderPanel("Import UO client assets", "", "");
        if (string.IsNullOrEmpty(source)) return null;
        string destination = Path.Combine(Application.temporaryCachePath, "ClientImports", Guid.NewGuid().ToString("N"));
        await Task.Run(() => ClientImportTransaction.Install(source, destination, null));
        return destination;
#else
        await Task.CompletedTask;
        throw new NotSupportedException("Folder import is available on Android.");
#endif
    }

    public static void Cancel()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var picker = new AndroidJavaClass("com.mobileuo.importer.ClientFolderPicker")) picker.CallStatic("cancel");
#endif
    }
}
