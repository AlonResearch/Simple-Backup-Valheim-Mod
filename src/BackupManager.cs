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
        private const int SaveSettleDelayWithoutStatusMs = 1200;

        private static readonly object _saveSyncInitGate = new object();
        private static bool _saveSyncInitialized;
        private static MethodInfo _nativeSaveTriggerMethod;
        private static object[] _nativeSaveTriggerArgs = Array.Empty<object>();
        private static MethodInfo _znetSaveTriggerMethod;
        private static object[] _znetSaveTriggerArgs = Array.Empty<object>();
        private static MethodInfo _nativeSaveInProgressMethod;

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
                EnsureSaveSyncMethodsInitialized();

                if (_nativeSaveTriggerMethod == null)
                {
                    if (_znetSaveTriggerMethod == null)
                    {
                        SimpleBackupPlugin.Log.LogWarning("No compatible native save trigger method was found; proceeding with best-effort backup.");
                        Thread.Sleep(SaveSettleDelayWithoutStatusMs);
                        return true;
                    }

                    bool invokedOnMainThread = SimpleBackupPlugin.TryInvokeOnMainThread(() =>
                    {
                        object target = _znetSaveTriggerMethod.IsStatic ? null : ZNet.instance;
                        _znetSaveTriggerMethod.Invoke(target, _znetSaveTriggerArgs);
                    }, timeoutMs: 3000);

                    if (!invokedOnMainThread)
                    {
                        SimpleBackupPlugin.Log.LogWarning($"Failed to invoke ZNet save trigger {_znetSaveTriggerMethod.Name}; proceeding with best-effort backup.");
                        Thread.Sleep(SaveSettleDelayWithoutStatusMs);
                        return true;
                    }

                    SimpleBackupPlugin.Log.LogDebug($"Triggered native save via ZNet.{_znetSaveTriggerMethod.Name}().");
                    Thread.Sleep(SaveSettleDelayWithoutStatusMs);
                    return true;
                }

                _nativeSaveTriggerMethod.Invoke(null, _nativeSaveTriggerArgs);
                SimpleBackupPlugin.Log.LogDebug($"Triggered native save via SaveSystem.{_nativeSaveTriggerMethod.Name}().");

                if (_nativeSaveInProgressMethod != null)
                {
                    var timeoutAt = DateTime.UtcNow.AddMilliseconds(SaveSyncTimeoutMs);
                    bool completed = false;
                    while (DateTime.UtcNow < timeoutAt)
                    {
                        bool inProgress;
                        if (!TryReadSaveInProgress(out inProgress))
                        {
                            SimpleBackupPlugin.Log.LogWarning($"Could not confirm save completion via SaveSystem.{_nativeSaveInProgressMethod.Name}().");
                            return false;
                        }

                        if (!inProgress)
                        {
                            completed = true;
                            break;
                        }

                        Thread.Sleep(SavePollIntervalMs);
                    }

                    if (!completed)
                    {
                        SimpleBackupPlugin.Log.LogWarning($"Timed out waiting for save completion after {SaveSyncTimeoutMs} ms.");
                        return false;
                    }

                    Thread.Sleep(SaveSettleDelayMs);
                    return true;
                }

                Thread.Sleep(SaveSettleDelayWithoutStatusMs);
                return true;
            }
            catch (Exception ex)
            {
                SimpleBackupPlugin.Log.LogWarning($"Failed while syncing live save state before backup: {ex.Message}");
                return false;
            }
        }

        private static bool TryReadSaveInProgress(out bool inProgress)
        {
            inProgress = false;

            try
            {
                if (_nativeSaveInProgressMethod == null)
                {
                    return false;
                }

                object raw = _nativeSaveInProgressMethod.Invoke(null, null);
                if (raw is bool)
                {
                    inProgress = (bool)raw;
                    return true;
                }
            }
            catch (Exception ex)
            {
                SimpleBackupPlugin.Log.LogWarning($"Could not evaluate SaveSystem.{_nativeSaveInProgressMethod.Name}(): {ex.Message}");
            }

            return false;
        }

        private static void EnsureSaveSyncMethodsInitialized()
        {
            if (_saveSyncInitialized)
            {
                return;
            }

            lock (_saveSyncInitGate)
            {
                if (_saveSyncInitialized)
                {
                    return;
                }

                BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                MethodInfo[] methods = typeof(SaveSystem).GetMethods(flags);
                MethodInfo[] znetMethods = typeof(ZNet).GetMethods(flags);

                TryResolveSaveTriggerMethod(methods, out _nativeSaveTriggerMethod, out _nativeSaveTriggerArgs);
                TryResolveZNetSaveTriggerMethod(znetMethods, out _znetSaveTriggerMethod, out _znetSaveTriggerArgs);

                _nativeSaveInProgressMethod =
                    FindMethod(methods, "IsSaving", "IsInProgress", "SaveInProgress", "IsBusy") ??
                    FindBoolNoArgMethodContaining(methods, "progress", "saving", "busy");

                SimpleBackupPlugin.Log.LogDebug(
                    $"Native save sync support: saveSystemTrigger={(_nativeSaveTriggerMethod != null ? _nativeSaveTriggerMethod.Name : "none")}, znetTrigger={(_znetSaveTriggerMethod != null ? _znetSaveTriggerMethod.Name : "none")}, status={(_nativeSaveInProgressMethod != null ? _nativeSaveInProgressMethod.Name : "none")}");

                _saveSyncInitialized = true;
            }
        }

        private static MethodInfo FindMethod(MethodInfo[] methods, params string[] names)
        {
            foreach (string name in names)
            {
                MethodInfo match = methods.FirstOrDefault(m =>
                    string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase) &&
                    m.GetParameters().Length == 0 &&
                    (m.ReturnType == typeof(void) || m.ReturnType == typeof(bool)));
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static bool TryResolveSaveTriggerMethod(MethodInfo[] methods, out MethodInfo triggerMethod, out object[] triggerArgs)
        {
            string[] preferredNames =
            {
                "Save",
                "SaveNow",
                "SaveGame",
                "RequestSave",
                "WriteSave",
                "SaveWorldAndCharacter"
            };

            foreach (string name in preferredNames)
            {
                MethodInfo noArg = FindMethod(methods, name);
                if (noArg != null)
                {
                    triggerMethod = noArg;
                    triggerArgs = Array.Empty<object>();
                    return true;
                }

                MethodInfo boolArg = methods.FirstOrDefault(m =>
                    string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase) &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(bool) &&
                    (m.ReturnType == typeof(void) || m.ReturnType == typeof(bool)));

                if (boolArg != null)
                {
                    triggerMethod = boolArg;
                    triggerArgs = new object[] { true };
                    return true;
                }
            }

            triggerMethod = null;
            triggerArgs = Array.Empty<object>();
            return false;
        }

        private static bool TryResolveZNetSaveTriggerMethod(MethodInfo[] methods, out MethodInfo triggerMethod, out object[] triggerArgs)
        {
            MethodInfo saveNoArg = FindMethod(methods, "SaveWorldAndPlayerProfiles");
            if (saveNoArg != null)
            {
                triggerMethod = saveNoArg;
                triggerArgs = Array.Empty<object>();
                return true;
            }

            MethodInfo saveThreeBool = methods.FirstOrDefault(m =>
                string.Equals(m.Name, "Save", StringComparison.OrdinalIgnoreCase) &&
                m.GetParameters().Length == 3 &&
                m.GetParameters().All(p => p.ParameterType == typeof(bool)) &&
                (m.ReturnType == typeof(void) || m.ReturnType == typeof(bool)));

            if (saveThreeBool != null)
            {
                triggerMethod = saveThreeBool;
                triggerArgs = new object[] { false, true, false };
                return true;
            }

            MethodInfo saveWorldBool = methods.FirstOrDefault(m =>
                string.Equals(m.Name, "SaveWorld", StringComparison.OrdinalIgnoreCase) &&
                m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType == typeof(bool) &&
                (m.ReturnType == typeof(void) || m.ReturnType == typeof(bool)));

            if (saveWorldBool != null)
            {
                triggerMethod = saveWorldBool;
                triggerArgs = new object[] { false };
                return true;
            }

            triggerMethod = null;
            triggerArgs = Array.Empty<object>();
            return false;
        }

        private static MethodInfo FindBoolNoArgMethodContaining(MethodInfo[] methods, params string[] tokens)
        {
            return methods.FirstOrDefault(m =>
                m.GetParameters().Length == 0 &&
                m.ReturnType == typeof(bool) &&
                tokens.Any(token => m.Name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0));
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
