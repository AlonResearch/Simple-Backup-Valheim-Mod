using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Win32;
using UnityEngine;

namespace SimpleBackup
{
    public static class BackupManager
    {
        public static string GetBackupRootDirectory()
        {
            string localLow = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low";
            string path = Path.Combine(localLow, "IronGate", "Valheim", "SimpleBackup");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public static void PerformFullBackup(string targetWorld = null, string targetCharacter = null)
        {
            SimpleBackupPlugin.Log.LogInfo("Starting Backup procedure...");

            List<string> sourceDirectories = GetValheimSaveDirectories();
            foreach (string dir in sourceDirectories)
            {
                if (!Directory.Exists(dir)) continue;

                string folderName = new DirectoryInfo(dir).Name; // "worlds", "characters", "worlds_local", etc.
                bool isCharacter = folderName.Contains("character");

                BackupDirectory(dir, isCharacter, targetWorld, targetCharacter);
            }
        }

        private static void BackupDirectory(string directoryPath, bool isCharacter, string targetWorld, string targetCharacter)
        {
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
                    SimpleBackupPlugin.Log.LogInfo($"Backed up {baseName} to {zipFileName}");
                }
                catch (Exception ex)
                {
                    SimpleBackupPlugin.Log.LogError($"Failed to backup {baseName}: {ex.Message}");
                }

                EnforceRetentionPolicy(targetBackupFolder, baseName);
            }
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
    }
}
