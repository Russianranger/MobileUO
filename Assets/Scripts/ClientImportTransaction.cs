using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// No Unity dependency: the directory swap and recovery are also exercised by the CLI tests.
public static class ClientImportTransaction
{
    public static bool ValidConfigurationName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && name == name.Trim() && !name.StartsWith(".")
            && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && name.IndexOfAny(new[] { '/', '\\', ':' }) < 0 && !name.EndsWith(".");
    }

    internal static string WorkPath(string destination, string name)
    {
        using (var hash = SHA256.Create())
        {
            string key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFileName(destination)))).Replace("-", "");
            return Path.Combine(Path.GetDirectoryName(destination), ".mobileuo-imports", key, name);
        }
    }

    public static void Recover(string destination)
    {
        string previous = WorkPath(destination, "previous");
        if (!Directory.Exists(previous)) return;
        if (!Directory.Exists(destination)) Directory.Move(previous, destination);
        else
        {
            try { Directory.Delete(previous, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static void Install(string source, string destination, string existing, bool consumeSource = false)
    {
        Recover(destination);
        string staging = WorkPath(destination, "staging");
        string previous = WorkPath(destination, "previous");
        if (Path.GetFullPath(staging).StartsWith(Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new IOException("The import source cannot contain its destination.");
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(Path.GetDirectoryName(staging));
        try
        {
            // The Android importer stages on the selected storage volume, so installing usually
            // needs only a rename. Copy if the user changed storage after selecting the folder.
            if (consumeSource)
            {
                try { Directory.Move(source, staging); }
                catch (IOException) { CopyDirectory(source, staging); }
            }
            else CopyDirectory(source, staging);
            // MobileUO stores its character profiles and assistant configuration below Data.
            if (!string.IsNullOrEmpty(existing) && Directory.Exists(Path.Combine(existing, "Data")))
            {
                string data = Path.Combine(staging, "Data");
                if (Directory.Exists(data)) Directory.Delete(data, true);
                CopyDirectory(Path.Combine(existing, "Data"), data);
            }
            if (Directory.Exists(destination)) Directory.Move(destination, previous);
            try { Directory.Move(staging, destination); }
            catch
            {
                if (Directory.Exists(previous) && !Directory.Exists(destination))
                    Directory.Move(previous, destination);
                throw;
            }
        }
        finally
        {
            // Preserve the staged import for retry if profile copying or the final swap failed.
            if (Directory.Exists(staging) && consumeSource && !Directory.Exists(source))
                Directory.Move(staging, source);
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
        // The new client is already installed. A failed old-copy cleanup must not report a failed import.
        try { if (Directory.Exists(previous)) Directory.Delete(previous, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symbolic links are not supported in client imports.");
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
        foreach (string directory in Directory.GetDirectories(source))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symbolic links are not supported in client imports.");
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
