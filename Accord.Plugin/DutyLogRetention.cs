using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Accord;

internal static class DutyLogRetention
{
    internal static void Trim(string root, string keep, DirectoryInfo[]? candidates = null)
    {
        var parent = new DirectoryInfo(root);
        if (parent.Attributes.HasFlag(FileAttributes.ReparsePoint)) return;
        foreach (var folder in candidates ?? parent.GetDirectories())
        {
            if (folder.Name == keep || !Guid.TryParseExact(folder.Name, "N", out _) || Linked(folder)) continue;
            var marker = new FileInfo(Path.Combine(folder.FullName, "managed-duty"));
            if (!marker.Exists || Linked(marker) || File.ReadAllText(marker.FullName) != folder.Name) continue;
            var files = new List<FileInfo>();
            var directories = new List<DirectoryInfo>();
            if (!Collect(folder, 0, files, directories)) continue;
            // Remove only validated files, then empty directories. Never recursively remove unknown contents.
            foreach (var file in files.Where(f => f.Name != "managed-duty")) file.Delete();
            foreach (var dir in directories) dir.Delete();
            marker.Delete();
            folder.Delete();
        }
    }
    // Older releases stored one directory per recording directly under AccordDiagnostics.
    // Only retire closed, positively identified captures, after a new duty is recording.
    internal static void TrimLegacy(string root)
    {
        var parent = new DirectoryInfo(root);
        if (!parent.Exists || Linked(parent)) return;
        foreach (var folder in parent.EnumerateDirectories())
        {
            if (Linked(folder) || !Guid.TryParseExact(folder.Name, "N", out _)) continue;
            var files = new List<FileInfo>();
            var components = new List<DirectoryInfo>();
            bool recognized = true;
            foreach (var entry in folder.EnumerateFileSystemInfos())
            {
                if (Linked(entry)) { recognized = false; break; }
                if (entry is FileInfo meta && meta.Name == "components.json") { files.Add(meta); continue; }
                if (entry is not DirectoryInfo component || component.Name is not ("Accord" or "WrathCombo" or "RotationSolver"))
                { recognized = false; break; }
                var contents = component.GetFileSystemInfos();
                if (contents.Any(f => Linked(f) || f is not FileInfo || !(f.Name is "session.lock" or "summary.json" || Regex.IsMatch(f.Name, @"^events-[0-9]+\.jsonl$"))))
                { recognized = false; break; }
                var summary = contents.OfType<FileInfo>().FirstOrDefault(f => f.Name == "summary.json");
                if (summary == null) { recognized = false; break; }
                try
                {
                    var data = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(summary.FullName));
                    if ((string?)data["Id"] != folder.Name || (string?)data["Component"] != component.Name) { recognized = false; break; }
                }
                catch (Newtonsoft.Json.JsonException) { recognized = false; break; }
                files.AddRange(contents.Cast<FileInfo>());
                components.Add(component);
            }
            if (!recognized || components.Count == 0) continue;
            foreach (var file in files) file.Delete();
            foreach (var component in components) component.Delete();
            folder.Delete();
        }
    }
    private static bool Linked(FileSystemInfo entry) => entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
    private static bool Collect(DirectoryInfo dir, int depth, List<FileInfo> files, List<DirectoryInfo> directories)
    {
        foreach (var entry in dir.EnumerateFileSystemInfos())
        {
            if (Linked(entry)) return false;
            if (entry is DirectoryInfo child)
            {
                bool expected = depth == 0 ? Guid.TryParseExact(child.Name, "N", out _) :
                    depth == 1 && child.Name is "Accord" or "WrathCombo" or "RotationSolver";
                if (!expected || !Collect(child, depth + 1, files, directories)) return false;
                directories.Add(child);
            }
            else if (entry is FileInfo file)
            {
                bool expected = depth == 0 && file.Name is "managed-duty" or "components.json" ||
                    depth == 2 && (file.Name is "summary.json" or "session.lock" || Regex.IsMatch(file.Name, @"^events-[0-9]+\.jsonl\.gz$"));
                if (!expected) return false;
                files.Add(file);
            }
        }
        return true;
    }
}
