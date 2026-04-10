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
        public enum BackupSaveType
        {
            Unknown,
            Character,
            World
        }

        public struct BackupArchiveInfo
        {
            public string TargetName;
            public string SourceCategory;
            public string ArchivePath;
            public BackupSaveType SaveType;
            public DateTime CreatedAt;
        }

        public struct BackupTargetInfo
        {
            public string TargetName;
            public string LatestBackupPath;
            public string SourceCategory;
            public BackupSaveType SaveType;
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
            SimpleBackupPlugin.Log.LogInfo("Starting native backup procedure...");

            if (ZNet.instance == null)
            {
                string message = "Backup skipped because the native Valheim save backend is not available in the current scene.";
                SimpleBackupPlugin.QueueUIMessage(message);
                SimpleBackupPlugin.Log.LogWarning(message);
                return;
            }

            var backupItems = new List<string>();

            if (!string.IsNullOrEmpty(targetWorld))
            {
                if (TryCreateNativeBackup(targetWorld, SaveDataType.World))
                {
                    backupItems.Add(DescribeNativeTarget(BackupSaveType.World, targetWorld));
                }
            }

            if (!string.IsNullOrEmpty(targetCharacter))
            {
                if (TryCreateNativeBackup(targetCharacter, SaveDataType.Character))
                {
                    backupItems.Add(DescribeNativeTarget(BackupSaveType.Character, targetCharacter));
                }
            }

            if (backupItems.Count == 0)
            {
                if (ZNet.instance.IsServer())
                {
                    string worldName = ZNet.instance.GetWorldName();
                    if (!string.IsNullOrEmpty(worldName))
                    {
                        if (TryCreateNativeBackup(worldName, SaveDataType.World))
                        {
                            backupItems.Add(DescribeNativeTarget(BackupSaveType.World, worldName));
                        }
                    }
                }

                string characterName = GetCurrentCharacterSaveName();
                if (!string.IsNullOrEmpty(characterName))
                {
                    if (TryCreateNativeBackup(characterName, SaveDataType.Character))
                    {
                        backupItems.Add(DescribeNativeTarget(BackupSaveType.Character, characterName));
                    }
                }
            }

            if (backupItems.Count > 0)
            {
                string scope = string.Join(" and ", backupItems.Distinct().ToArray());
                string msg = $"Native backup complete for {scope}.";
                SimpleBackupPlugin.QueueUIMessage(msg);
                SimpleBackupPlugin.Log.LogInfo(msg);
            }
            else
            {
                string msg = "No current character or hosted world was available for native backup.";
                SimpleBackupPlugin.QueueUIMessage(msg);
                SimpleBackupPlugin.Log.LogWarning(msg);
            }
        }

        private static bool TryCreateNativeBackup(string saveName, SaveDataType saveDataType)
        {
            try
            {
                SaveWithBackups save;
                if (!SaveSystem.TryGetSaveByName(saveName, saveDataType, out save) || save == null)
                {
                    SimpleBackupPlugin.Log.LogWarning($"Native backup skipped because save '{saveName}' was not found for type {saveDataType}.");
                    return false;
                }

                SaveFile primary = save.PrimaryFile;
                if (primary == null)
                {
                    SimpleBackupPlugin.Log.LogWarning($"Native backup skipped because no primary file exists for '{saveName}'.");
                    return false;
                }

                bool created = InvokeMoveToBackup(primary, DateTime.Now);
                if (created)
                {
                    SimpleBackupPlugin.Log.LogInfo($"Native backup created for {DescribeNativeTarget(saveDataType == SaveDataType.World ? BackupSaveType.World : BackupSaveType.Character, saveName)}");
                }
                else
                {
                    SimpleBackupPlugin.Log.LogWarning($"Native backup call did not create a backup entry for {DescribeNativeTarget(saveDataType == SaveDataType.World ? BackupSaveType.World : BackupSaveType.Character, saveName)}.");
                }

                return created;
            }
            catch (Exception ex)
            {
                SimpleBackupPlugin.Log.LogError($"Native backup failed for {saveName}: {ex.Message}");
                return false;
            }
        }

        private static string DescribeNativeTarget(BackupSaveType saveType, string saveName)
        {
            if (string.IsNullOrEmpty(saveName))
            {
                return saveType == BackupSaveType.World ? "world" : "character";
            }

            return saveType == BackupSaveType.World ? $"world '{saveName}'" : $"character '{saveName}'";
        }

        private static bool InvokeMoveToBackup(SaveFile saveFile, DateTime now)
        {
            try
            {
                var method = typeof(SaveSystem).GetMethod("MoveToBackup", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (method == null)
                {
                    SimpleBackupPlugin.Log.LogWarning("Could not find native SaveSystem.MoveToBackup method.");
                    return false;
                }

                object result = method.Invoke(null, new object[] { saveFile, now });
                return result is bool && (bool)result;
            }
            catch (Exception ex)
            {
                SimpleBackupPlugin.Log.LogError($"Reflection call to MoveToBackup failed: {ex.Message}");
                return false;
            }
        }

        public static string GetCurrentCharacterSaveName()
        {
            try
            {
                if (Game.instance != null)
                {
                    PlayerProfile profile = Game.instance.GetPlayerProfile();
                    if (profile != null)
                    {
                        string filename = profile.GetFilename();
                        if (!string.IsNullOrEmpty(filename))
                        {
                            return filename;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleBackupPlugin.Log.LogWarning($"Failed to resolve canonical character save name: {ex.Message}");
            }

            if (Player.m_localPlayer != null)
            {
                return Player.m_localPlayer.GetPlayerName();
            }

            return null;
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

            foreach (BackupArchiveInfo archive in GetAllBackupArchives())
            {
                string targetName = archive.TargetName;
                if (string.IsNullOrEmpty(targetName))
                {
                    continue;
                }

                BackupTargetInfo current;
                string targetKey = BuildTargetKey(archive.SaveType, targetName);
                if (!latestByTarget.TryGetValue(targetKey, out current) || archive.CreatedAt > current.CreatedAt)
                {
                    latestByTarget[targetKey] = new BackupTargetInfo
                    {
                        TargetName = targetName,
                        LatestBackupPath = archive.ArchivePath,
                        SourceCategory = archive.SourceCategory,
                        SaveType = archive.SaveType,
                        CreatedAt = archive.CreatedAt
                    };
                }
            }

            return latestByTarget.Values.OrderByDescending(entry => entry.CreatedAt).ToList();
        }

        public static List<BackupArchiveInfo> GetAllBackupArchives()
        {
            List<BackupArchiveInfo> archives = new List<BackupArchiveInfo>();
            string root = GetBackupRootDirectory();
            if (!Directory.Exists(root))
            {
                return archives;
            }

            foreach (string categoryDir in Directory.GetDirectories(root))
            {
                string sourceCategory = new DirectoryInfo(categoryDir).Name;
                foreach (string archivePath in Directory.GetFiles(categoryDir, "*.zip"))
                {
                    BackupArchiveInfo archiveInfo;
                    if (TryCreateBackupArchiveInfo(archivePath, sourceCategory, out archiveInfo))
                    {
                        archives.Add(archiveInfo);
                    }
                }
            }

            return archives.OrderByDescending(archive => archive.CreatedAt).ToList();
        }

        public static bool TryCreateBackupArchiveInfo(string archivePath, string sourceCategory, out BackupArchiveInfo archiveInfo)
        {
            archiveInfo = default(BackupArchiveInfo);

            if (string.IsNullOrEmpty(archivePath) || !File.Exists(archivePath))
            {
                return false;
            }

            string targetName = GetTargetNameFromBackupFile(archivePath);
            if (string.IsNullOrEmpty(targetName))
            {
                return false;
            }

            archiveInfo = new BackupArchiveInfo
            {
                TargetName = targetName,
                SourceCategory = sourceCategory,
                ArchivePath = archivePath,
                SaveType = GetSaveTypeFromCategory(sourceCategory),
                CreatedAt = File.GetCreationTime(archivePath)
            };

            return true;
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

        public static BackupSaveType GetSaveTypeFromCategory(string sourceCategory)
        {
            if (string.IsNullOrEmpty(sourceCategory))
            {
                return BackupSaveType.Unknown;
            }

            string normalized = sourceCategory.ToLowerInvariant();
            if (normalized.Contains("character"))
            {
                return BackupSaveType.Character;
            }

            if (normalized.Contains("world"))
            {
                return BackupSaveType.World;
            }

            return BackupSaveType.Unknown;
        }

        private static string BuildTargetKey(BackupSaveType saveType, string targetName)
        {
            return $"{saveType}:{targetName}";
        }
    }
}
