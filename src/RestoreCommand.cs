using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using HarmonyLib;
using System.Collections.Generic;

namespace NativeBackup
{
    public static class RestoreCommandLogic
    {
        private static System.Reflection.FieldInfo _terminalCommandsField;

        public static bool IsBackupCommandMissing()
        {
            return !CommandExists("sb.backup");
        }

        private static bool CommandExists(string cmd)
        {
            try
            {
                if (_terminalCommandsField == null)
                    _terminalCommandsField = typeof(Terminal).GetField("commands", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                if (_terminalCommandsField != null)
                {
                    var dict = _terminalCommandsField.GetValue(null) as System.Collections.IDictionary;
                    return dict != null && dict.Contains(cmd);
                }
            }
            catch { }
            return true; // Return true for 'missing' if we can't read it
        }

        public static void RegisterCommands()
        {
            bool injected = false;
            
            if (!CommandExists("sb.restore"))
            {
                new Terminal.ConsoleCommand("sb.restore", "Restores a backup.", (args) => HandleRestoreCommand(args.Context, args.Args),
                    isCheat: false, isNetwork: false, onlyServer: false, isSecret: false);
                injected = true;
            }

            if (!CommandExists("sb.backup"))
            {
                new Terminal.ConsoleCommand("sb.backup", "Triggers a backup.", (args) => HandleBackupCommand(args.Context, args.Args),
                    isCheat: false, isNetwork: false, onlyServer: false, isSecret: false);
                injected = true;
            }

            if (!CommandExists("sb.list"))
            {
                new Terminal.ConsoleCommand("sb.list", "Lists backups.", (args) => HandleListCommand(args.Context),
                    isCheat: false, isNetwork: false, onlyServer: false, isSecret: false);
                injected = true;
            }

            if (injected)
            {
                NativeBackupPlugin.Log.LogInfo("sb. Commands forcefully registered into Terminal dictionary.");
            }
        }

        private static void HandleBackupCommand(Terminal context, string[] args)
        {
            string option = args.Length >= 2 ? args[1].ToLower() : "both";

            string cName = BackupManager.GetCurrentCharacterSaveName();
            string wName = (ZNet.instance != null && ZNet.instance.IsServer()) ? ZNet.instance.GetWorldName() : null;

            if (option == "char")
            {
                if (string.IsNullOrEmpty(cName))
                {
                    context.AddString("ERROR: No active character found.");
                    return;
                }

                StartBackupFromConsole(context, null, cName, $"Backup started: character '{cName}'.");
                return;
            }

            if (option == "world")
            {
                if (string.IsNullOrEmpty(wName))
                {
                    context.AddString("ERROR: You can only backup a world if you are actively hosting it locally.");
                    return;
                }

                StartBackupFromConsole(context, wName, null, $"Backup started: world '{wName}'.");
                return;
            }

            if (string.IsNullOrEmpty(cName) && string.IsNullOrEmpty(wName))
            {
                context.AddString("ERROR: No active world or character found.");
                return;
            }

            string targetLabel = DescribeTarget(wName, cName);
            StartBackupFromConsole(context, wName, cName, $"Backup started: {targetLabel}.");
        }

        private static void StartBackupFromConsole(Terminal context, string worldName, string characterName, string startMessage)
        {
            BackupCoordinator.BackupStartResult startResult = BackupCoordinator.TryStartBackup(worldName, characterName);
            if (startResult == BackupCoordinator.BackupStartResult.Started)
            {
                context.AddString(startMessage);
            }
            else
            {
                if (startResult == BackupCoordinator.BackupStartResult.CooldownActive)
                {
                    context.AddString("Backup on cooldown.");
                }
                else
                {
                    context.AddString("Backup already running.");
                }
            }
        }

        private static string DescribeTarget(string worldName, string characterName)
        {
            if (!string.IsNullOrEmpty(worldName) && !string.IsNullOrEmpty(characterName))
            {
                return $"world '{worldName}' and character '{characterName}'";
            }

            if (!string.IsNullOrEmpty(worldName))
            {
                return $"world '{worldName}'";
            }

            if (!string.IsNullOrEmpty(characterName))
            {
                return $"character '{characterName}'";
            }

            return "the current save targets";
        }

        private static void HandleListCommand(Terminal context)
        {
            var backups = BackupManager.GetAllAvailableBackups();
            if (backups.Count == 0)
            {
                context.AddString("No backups found.");
                return;
            }

            int displayCount = Math.Min(15, backups.Count);
            context.AddString($"Found {backups.Count} backups. Showing last {displayCount}:");
            
            var sorted = backups.OrderByDescending(f => File.GetCreationTime(f)).Take(displayCount).ToList();
            foreach (var b in sorted)
            {
                context.AddString($"- {Path.GetFileName(b)}");
            }
        }

        private static void HandleRestoreCommand(Terminal context, string[] args)
        {
            if (args.Length < 2)
            {
                context.AddString("Usage: sb.restore <SaveName>");
                return;
            }

            TryRestoreLatestBackup(args[1], context != null ? new Action<string>(context.AddString) : null);
        }

        public static bool TryRestoreLatestBackup(string saveName, Action<string> reportMessage)
        {
            if (ZNet.instance != null || Player.m_localPlayer != null)
            {
                Emit(reportMessage, "ERROR: Restoring while actively loaded into a world is extremely dangerous and can corrupt your game! Please return to the Main Menu to restore.");
                return false;
            }

            var backups = BackupManager.GetAllAvailableBackups();
            var targetBackups = backups.Where(b => string.Equals(BackupManager.GetTargetNameFromBackupFile(b), saveName, StringComparison.OrdinalIgnoreCase))
                                       .OrderByDescending(b => File.GetCreationTime(b))
                                       .ToList();

            if (targetBackups.Count == 0)
            {
                Emit(reportMessage, $"No backups found for target: {saveName}");
                return false;
            }

            string latestZip = targetBackups.First();
            Emit(reportMessage, $"Found latest backup: {Path.GetFileName(latestZip)}");

            try
            {
                // To safely extract without nuking original folder, we must determine the original path.
                // The easiest way is to extract it back to where the original files are located.
                // For simplicity, we search Valheim save dirs for the original files, or ask user to do it from Main Menu.
                
                string originalSaveDir = FindOriginalSaveDirectory(saveName);
                if (string.IsNullOrEmpty(originalSaveDir))
                {
                    Emit(reportMessage, "Could not locate original save file location in Steam/Local. You may need to manually extract this zip from: \n" + latestZip);
                    return false;
                }

                Emit(reportMessage, $"Original location found: {originalSaveDir}");
                
                // Backup current files to .old
                var activeFiles = Directory.GetFiles(originalSaveDir).Where(f => Path.GetFileNameWithoutExtension(f) == saveName && !f.EndsWith(".old") && !f.EndsWith(".zip")).ToList();
                foreach (var activeFile in activeFiles)
                {
                    string oldPath = activeFile + ".old";
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                    File.Move(activeFile, oldPath);
                    Emit(reportMessage, $"Renamed current active save to {Path.GetFileName(oldPath)}");
                }

                // Extract Zip
                using (ZipArchive archive = ZipFile.OpenRead(latestZip))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string extractPath = Path.Combine(originalSaveDir, entry.FullName);
                        entry.ExtractToFile(extractPath, overwrite: true);
                        Emit(reportMessage, $"Extracted: {entry.FullName}");
                    }
                }

                Emit(reportMessage, "Restore complete! Please restart your game session or reload from Main Menu if necessary.");
                return true;
            }
            catch (Exception ex)
            {
                Emit(reportMessage, $"Error during restore: {ex.Message}");
                NativeBackupPlugin.Log.LogError(ex);
                return false;
            }
        }

        public static bool TryRestoreLatestBackup(string saveName, Terminal context)
        {
            return TryRestoreLatestBackup(saveName, context != null ? new Action<string>(context.AddString) : null);
        }

        public static List<BackupManager.BackupTargetInfo> GetAvailableRestoreTargets()
        {
            return BackupManager.GetLatestBackupTargets();
        }

        private static void Emit(Action<string> reportMessage, string message)
        {
            if (reportMessage != null)
            {
                reportMessage(message);
            }
            else
            {
                NativeBackupPlugin.Log.LogInfo(message);
            }
        }

        private static string FindOriginalSaveDirectory(string saveName)
        {
            var dirs = BackupManager.GetValheimSaveDirectories();
            foreach (var dir in dirs)
            {
                if (Directory.Exists(dir))
                {
                    var matching = Directory.GetFiles(dir).Any(f => Path.GetFileNameWithoutExtension(f) == saveName && !f.EndsWith(".old") && !f.EndsWith(".zip"));
                    if (matching) return dir;
                }
            }
            return null;
        }
    }
}

