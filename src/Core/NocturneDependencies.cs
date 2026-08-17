using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Nocturne;

internal static class NocturneDependencies
{
    private static readonly string[] Libs = { "NAudio", "NAudio.Core", "NAudio.WinMM", "NVorbis" };
    private static string _pluginDir;
    private static readonly List<string> _pendingInfo = new List<string>(8);
    private static readonly List<string> _pendingWarn = new List<string>(8);

    internal static void Setup()
    {
        try
        {
            _pluginDir = ResolvePluginDir();
            ExtractAll();
            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
        }
        catch (Exception e)
        {
            _pendingWarn.Add($"NocturneDependencies.Setup failed: {e.Message}");
        }
    }

    internal static void FlushLog()
    {
        var log = NocturnePlugin.Logger;
        if (log == null) return;
        for (int i = 0; i < _pendingInfo.Count; i++) log.LogInfo((object)_pendingInfo[i]);
        for (int i = 0; i < _pendingWarn.Count; i++) log.LogWarning((object)_pendingWarn[i]);
        _pendingInfo.Clear();
        _pendingWarn.Clear();
    }

    private static string ResolvePluginDir()
    {
        try
        {
            string loc = typeof(NocturneDependencies).Assembly.Location;
            if (!string.IsNullOrEmpty(loc))
            {
                string dir = Path.GetDirectoryName(loc);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
            }
        }
        catch { }

        try
        {
            string bepPlugins = BepInEx.Paths.PluginPath;
            if (!string.IsNullOrEmpty(bepPlugins) && Directory.Exists(bepPlugins)) return bepPlugins;
        }
        catch { }

#pragma warning disable SYSLIB0012
        try
        {
            string code = typeof(NocturneDependencies).Assembly.CodeBase;
            if (!string.IsNullOrEmpty(code))
            {
                if (code.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                    code = code.Substring(8).Replace('/', Path.DirectorySeparatorChar);
                string dir = Path.GetDirectoryName(code);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
            }
        }
        catch { }
#pragma warning restore SYSLIB0012

        return Environment.CurrentDirectory;
    }

    private static void ExtractAll()
    {
        if (string.IsNullOrEmpty(_pluginDir))
        {
            _pendingWarn.Add("NocturneDependencies: plugin directory unresolved, skipping extraction.");
            return;
        }

        Assembly self = typeof(NocturneDependencies).Assembly;
        foreach (string lib in Libs)
        {
            string dest = Path.Combine(_pluginDir, lib + ".dll");
            try
            {
                if (File.Exists(dest))
                {
                    try { if (new FileInfo(dest).Length > 0) continue; }
                    catch { continue; }
                }

                using Stream stream = self.GetManifestResourceStream($"Nocturne.libs.{lib}.dll");
                if (stream == null)
                {
                    _pendingWarn.Add($"Embedded resource Nocturne.libs.{lib}.dll not found.");
                    continue;
                }
                using (FileStream fs = File.Create(dest)) { stream.CopyTo(fs); }
                _pendingInfo.Add($"Extracted {lib}.dll to {dest}.");
            }
            catch (Exception e)
            {
                _pendingWarn.Add($"Failed to extract {lib}.dll to {dest}: {e.Message}");
            }
        }
    }

    private static Assembly OnResolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name;
        foreach (string lib in Libs)
        {
            if (!string.Equals(lib, name, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrEmpty(_pluginDir))
            {
                string path = Path.Combine(_pluginDir, lib + ".dll");
                if (File.Exists(path))
                {
                    try { return Assembly.LoadFile(path); } catch { }
                }
            }
            try
            {
                using Stream stream = typeof(NocturneDependencies).Assembly.GetManifestResourceStream($"Nocturne.libs.{lib}.dll");
                if (stream == null) return null;
                byte[] bytes = new byte[stream.Length];
                _ = stream.Read(bytes, 0, bytes.Length);
                return Assembly.Load(bytes);
            }
            catch { return null; }
        }
        return null;
    }
}
