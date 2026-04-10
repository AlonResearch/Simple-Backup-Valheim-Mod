using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Win32;
using UnityEngine;
using System.Text.RegularExpressions;

namespace SimpleBackup
{
    public static class BackupManager
    {
        public struct BackupTargetInfo
        {
            public string TargetName;
            public string LatestBackupPath;
            public DateTime CreatedAt;
        }

        public static string GetBackupRootDirectory()
        {
            string localLow = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low";
            string path = Path.Combine(localLow, "IronGate", "Valheim", "SimpleBackup");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public struct BackupMetrics
        {
            public int Count;
            public long Bytes;
        }

        public static void PerformFullBackup(string targetWorld = null, string targetCharacter = null)
        {
            SimpleBackupPlugin.Log.LogInfo("Starting Backup procedure...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            List<string> sourceDirectories = GetValheimSaveDirectories();
            int successCount = 0;
            long totalBytes = 0;
            
            foreach (string dir in sourceDirectories)
            {
                if (!Directory.Exists(dir)) continue;

                string folderName = new DirectoryInfo(dir).Name; // "worlds", "characters", "worlds_local", etc.
                bool isCharacter = folderName.Contains("character");

                if (!ShouldBackupDirectory(isCharacter, targetWorld, targetCharacter))
                {
                    continue;
                }

                var metrics = BackupDirectory(dir, isCharacter, targetWorld, targetCharacter);
                successCount += metrics.Count;
                totalBytes += metrics.Bytes;
            }
            
            stopwatch.Stop();
            double seconds = stopwatch.Elapsed.TotalSeconds;
            double megabytes = totalBytes / 1048576.0;

            if (successCount > 0)
            {
                string msg = $"Backup complete! {successCount} saves ({megabytes:F2} MB) safely archived in {seconds:F1}s.";
                SimpleBackupPlugin.QueueUIMessage(msg);
                SimpleBackupPlugin.Log.LogInfo(msg);
            }
            else
            {
                string msg = "Backup process finished, but no matching saves were found to backup.";
                SimpleBackupPlugin.QueueUIMessage(msg);
                SimpleBackupPlugin.Log.LogWarning(msg);
            }
        }

        private static BackupMetrics BackupDirectory(string directoryPath, bool isCharacter, string targetWorld, string targetCharacter)
        {
            BackupMetrics metrics = new BackupMetrics();
            string backupRoot = GetBackupRootDirectory();
            string categoryName = directoryPath.Contains("remote") ? "SteamCloud_" + new DirectoryInfo(directoryPath).Name : "Local_" + new DirectoryInfo(directoryPath).Name;
            string targetBackupFolder = Path.Combine(backupRoot, categoryName);
            if (!Directory.Exists(targetBackupFolder)) Directory.CreateDirectory(targetBackupFolder);

            var files = Directory.GetFiles(directoryPath).Where(f => !f.EndsWith(".old") && !f.EndsWith(".zip")).ToList();
            
            // Group files by base name for worlds (.fwl and .db match), chars just have .fch and maybe .ptx
            var groupedFiles = files.GroupBy(f => Path.GetFileNameWithoutExtension(f)).ToList();

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            foreach (var group in groupedFiles)
            {
                string baseName = group.Key;
                if (string.IsNullOrEmpty(baseName)) continue;

                // Restrict backup to only the actively specified targets if they are provided
                if (isCharacter && !string.IsNullOrEmpty(targetCharacter) && !baseName.Equals(targetCharacter, StringComparison.OrdinalIgnoreCase)) continue;
                if (!isCharacter && !string.IsNullOrEmpty(targetWorld) && !baseName.Equals(targetWorld, StringComparison.OrdinalIgnoreCase)) continue;

                string zipFileName = $"{timestamp}-{baseName}.zip";
                string zipFilePath = Path.Combine(targetBackupFolder, zipFileName);

                try
                {
                    using (ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                    {
                        foreach (string file in group)
                        {
                            archive.CreateEntryFromFile(file, Path.GetFileName(file));
                        }
                    }
                    var fileInfo = new FileInfo(zipFilePath);
                    metrics.Bytes += fileInfo.Length;
                    metrics.Count++;
                    SimpleBackupPlugin.Log.LogInfo($"Backed up {baseName} to {zipFileName}");
                }
                catch (Exception ex)
                {
                    SimpleBackupPlugin.Log.LogError($"Failed to backup {baseName}: {ex.Message}");
                }

                EnforceRetentionPolicy(targetBackupFolder, baseName);
            }
            
            return metrics;
        }

        private static bool ShouldBackupDirectory(bool isCharacterDirectory, string targetWorld, string targetCharacter)
        {
            bool wantsWorld = !string.IsNullOrEmpty(targetWorld);
            bool wantsCharacter = !string.IsNullOrEmpty(targetCharacter);

            if (!wantsWorld && !wantsCharacter)
            {
                return true;
            }

            return isCharacterDirectory ? wantsCharacter : wantsWorld;
        }

        private static void EnforceRetentionPolicy(string backupDirectory, string baseName)
        {
            int maxBackups = SimpleBackupPlugin.MaxBackupsToKeep.Value;
            if (maxBackups <= 0) return;

            var allZips = Directory.GetFiles(backupDirectory, $"*-{baseName}.zip")
                                   .Select(f => new FileInfo(f))
                                   .OrderByDescending(f => f.CreationTime)
                                   .ToList();

            if (allZips.Count > maxBackups)
            {
                for (int i = maxBackups; i < allZips.Count; i++)
                {
                    try
                    {
                        allZips[i].Delete();
                        SimpleBackupPlugin.Log.LogInfo($"Deleted old backup: {allZips[i].Name}");
                    }
                    catch { }
                }
            }
        }

        public static List<string> GetValheimSaveDirectories()
        {
            List<string> dirs = new List<string>();

            // Local directories
            string localLow = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low";
            string valheimLocal = Path.Combine(localLow, "IronGate", "Valheim");
            
            string[] localSubdirs = { "characters", "characters_local", "worlds", "worlds_local" };
            foreach (var sub in localSubdirs)
            {
                string p = Path.Combine(valheimLocal, sub);
                if (Directory.Exists(p)) dirs.Add(p);
            }

            // Steam Cloud directories
            try
            {
                string steamPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
                if (string.IsNullOrEmpty(steamPath))
                {
                    steamPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;
                }

                if (!string.IsNullOrEmpty(steamPath))
                {
                    string userdata = Path.Combine(steamPath, "userdata");
                    if (Directory.Exists(userdata))
                    {
                        foreach (string userDir in Directory.GetDirectories(userdata))
                        {
                            string remoteChars = Path.Combine(userDir, "892970", "remote", "characters");
                            string remoteWorlds = Path.Combine(userDir, "892970", "remote", "worlds");

                            if (Directory.Exists(remoteChars)) dirs.Add(remoteChars);
                            if (Directory.Exists(remoteWorlds)) dirs.Add(remoteWorlds);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleBackupPlugin.Log.LogWarning($"Failed to read Steam path from registry: {ex.Message}");
            }

            return dirs;
        }

        // Helper string to easily map .zip paths during manual UI testing or restoring
        public static List<string> GetAllAvailableBackups()
        {
            List<string> backups = new List<string>();
            string root = GetBackupRootDirectory();
            if (!Directory.Exists(root)) return backups;

            foreach (var categoryDir in Directory.GetDirectories(root))
            {
                backups.AddRange(Directory.GetFiles(categoryDir, "*.zip"));
            }
            return backups;
        }

        public static List<BackupTargetInfo> GetLatestBackupTargets()
        {
            var latestByTarget = new Dictionary<string, BackupTargetInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (string backupPath in GetAllAvailableBackups())
            {
                string targetName = GetTargetNameFromBackupFile(backupPath);
                if (string.IsNullOrEmpty(targetName))
                {
                    continue;
                }

                DateTime createdAt = File.GetCreationTime(backupPath);
                BackupTargetInfo current;
                if (!latestByTarget.TryGetValue(targetName, out current) || createdAt > current.CreatedAt)
                {
                    latestByTarget[targetName] = new BackupTargetInfo
                    {
                        TargetName = targetName,
                        LatestBackupPath = backupPath,
                        CreatedAt = createdAt
                    };
                }
            }

            return latestByTarget.Values.OrderByDescending(entry => entry.CreatedAt).ToList();
        }

        public static string GetTargetNameFromBackupFile(string backupPath)
        {
            if (string.IsNullOrEmpty(backupPath))
            {
                return null;
            }

            string fileName = Path.GetFileNameWithoutExtension(backupPath);
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            Match match = Regex.Match(fileName, @"^\d{8}_\d{6}-(.+)$");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            int firstDash = fileName.IndexOf('-');
            if (firstDash >= 0 && firstDash < fileName.Length - 1)
            {
                return fileName.Substring(firstDash + 1).Trim();
            }

            return fileName.Trim();
        }
    }
}
