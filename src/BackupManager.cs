using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Win32;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Threading;
using System.Diagnostics;

namespace SimpleBackup
{
    public static class BackupManager
    {
        private const int SaveSyncTimeoutMs = 10000;
        private const int SavePollIntervalMs = 100;
        private const int SaveSettleDelayMs = 350;

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
            bool explicitTargetsRequested = !string.IsNullOrEmpty(targetWorld) || !string.IsNullOrEmpty(targetCharacter);

            if (ZNet.instance == null)
            {
                string message = "Backup unavailable in this scene.";
                SimpleBackupPlugin.QueueUIMessage(message);
                SimpleBackupPlugin.Log.LogWarning(message);
                return;
            }

            var duration = Stopwatch.StartNew();
            SimpleBackupPlugin.SetBackupIndicatorActive(true);
            try
            {
                bool syncSuccessful = TrySyncLiveStateBeforeBackup();
                if (!syncSuccessful)
                {
                    string msg = $"Backup canceled ({duration.Elapsed.TotalSeconds:0.0}s): could not confirm current save state.";
                    SimpleBackupPlugin.QueueUIMessage(msg);
                    SimpleBackupPlugin.Log.LogWarning(msg);
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

                if (!explicitTargetsRequested && backupItems.Count == 0)
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
                    string msg = $"Backup complete ({duration.Elapsed.TotalSeconds:0.0}s): {scope}.";
                    SimpleBackupPlugin.QueueUIMessage(msg);
                    SimpleBackupPlugin.Log.LogInfo(msg);
                }
                else
                {
                    string reason = explicitTargetsRequested ? "requested target unavailable" : "no eligible save target found";
                    string msg = $"Backup failed ({duration.Elapsed.TotalSeconds:0.0}s): {reason}.";
                    SimpleBackupPlugin.QueueUIMessage(msg);
                    SimpleBackupPlugin.Log.LogWarning(msg);
                }
            }
            finally
            {
                SimpleBackupPlugin.SetBackupIndicatorActive(false);
            }
        }

        private static bool TrySyncLiveStateBeforeBackup()
        {
            try
            {
                if (ZNet.instance == null)
                {
                    SimpleBackupPlugin.Log.LogWarning("Could not sync save because ZNet is unavailable.");
                    return false;
                }

                float baselineDoneTime = 0f;
                bool baselineRead = SimpleBackupPlugin.TryInvokeOnMainThread(() =>
                {
                    baselineDoneTime = ZNet.instance.SaveDoneTime;
                }, timeoutMs: 3000);

                if (!baselineRead)
                {
                    SimpleBackupPlugin.Log.LogWarning("Could not read baseline save timestamp before triggering save.");
                    return false;
                }

                bool saveTriggered = SimpleBackupPlugin.TryInvokeOnMainThread(() =>
                {
                    ZNet.instance.Save(false, true, false);
                }, timeoutMs: 3000);

                if (!saveTriggered)
                {
                    SimpleBackupPlugin.Log.LogWarning("Failed to trigger ZNet save on main thread.");
                    return false;
                }

                SimpleBackupPlugin.Log.LogDebug("Triggered native save via ZNet.Save(sync:false, saveOtherPlayerProfiles:true, waitForNextFrame:false).");

                var timeoutAt = DateTime.UtcNow.AddMilliseconds(SaveSyncTimeoutMs);
                while (DateTime.UtcNow < timeoutAt)
                {
                    float currentDoneTime = baselineDoneTime;
                    bool readDoneTime = SimpleBackupPlugin.TryInvokeOnMainThread(() =>
                    {
                        currentDoneTime = ZNet.instance.SaveDoneTime;
                    }, timeoutMs: 3000);

                    if (!readDoneTime)
                    {
                        SimpleBackupPlugin.Log.LogWarning("Could not read save completion timestamp from ZNet.");
                        return false;
                    }

                    if (currentDoneTime > baselineDoneTime)
                    {
                        Thread.Sleep(SaveSettleDelayMs);
                        return true;
                    }

                    Thread.Sleep(SavePollIntervalMs);
                }

                SimpleBackupPlugin.Log.LogWarning($"Timed out waiting for ZNet save completion after {SaveSyncTimeoutMs} ms.");
                return false;
            }
            catch (Exception ex)
            {
                SimpleBackupPlugin.Log.LogWarning($"Failed while syncing live save state before backup: {ex.Message}");
                return false;
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
                    SimpleBackupPlugin.Log.LogDebug($"Native backup created for {DescribeNativeTarget(saveDataType == SaveDataType.World ? BackupSaveType.World : BackupSaveType.Character, saveName)}");
                    PruneNativeBackupsForTarget(primary);
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

        private static void PruneNativeBackupsForTarget(SaveFile primaryFile)
        {
            try
            {
                int maxBackupsPerSave = SimpleBackupPlugin.MaxBackupsPerSave != null ? SimpleBackupPlugin.MaxBackupsPerSave.Value : 0;
                if (maxBackupsPerSave <= 0 || primaryFile == null)
                {
                    return;
                }

                string primaryPath = primaryFile.PathPrimary;
                if (string.IsNullOrEmpty(primaryPath))
                {
                    return;
                }

                string targetDirectory = Path.GetDirectoryName(primaryPath);
                if (string.IsNullOrEmpty(targetDirectory))
                {
                    return;
                }

                string backupDirectory = Path.Combine(targetDirectory, "backups");
                if (!Directory.Exists(backupDirectory))
                {
                    return;
                }

                string filePrefix = primaryFile.FileName;
                if (string.IsNullOrEmpty(filePrefix))
                {
                    filePrefix = Path.GetFileName(primaryPath);
                }

                if (string.IsNullOrEmpty(filePrefix))
                {
                    return;
                }

                List<FileInfo> backups = Directory.GetFiles(backupDirectory, filePrefix + "*")
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.CreationTimeUtc)
                    .ToList();

                if (backups.Count <= maxBackupsPerSave)
                {
                    return;
                }

                for (int index = maxBackupsPerSave; index < backups.Count; index++)
                {
                    try
                    {
                        backups[index].Delete();
                        SimpleBackupPlugin.Log.LogDebug($"Pruned native backup: {backups[index].Name}");
                    }
                    catch (Exception ex)
                    {
                        SimpleBackupPlugin.Log.LogWarning($"Failed to prune native backup {backups[index].Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleBackupPlugin.Log.LogWarning($"Native backup pruning failed: {ex.Message}");
            }
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
