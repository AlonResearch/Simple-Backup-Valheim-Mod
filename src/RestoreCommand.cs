using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using HarmonyLib;

namespace SimpleBackup
{
    [HarmonyPatch(typeof(Terminal))]
    public static class RestoreCommandPatch
    {
        [HarmonyPatch("InitTerminal")]
        [HarmonyPostfix]
        public static void InitTerminal_Postfix(Terminal __instance)
        {
            if (__instance == null) return;

            new Terminal.ConsoleCommand(
                "sb.restore",
                "Restores the latest backup for a given save file base name (e.g., 'sb.restore MyWorld')",
                (Terminal.ConsoleEventArgs args) =>
                {
                    if (args.Length < 2)
                    {
                        args.Context.AddString("Usage: sb.restore <SaveName>");
                        return;
                    }

                    string saveName = args[1];
                    RestoreLatestBackup(saveName, args.Context);
                },
                isCheat: false,
                isNetwork: false,
                onlyServer: false,
                isSecret: false
            );

            new Terminal.ConsoleCommand(
                "sb.backup",
                "Triggers a backup. Usage: sb.backup [char|world]. No arguments backs up both (if applicable).",
                (Terminal.ConsoleEventArgs args) =>
                {
                    string option = args.Length >= 2 ? args[1].ToLower() : "both";
                    
                    if (option == "char" || option == "both")
                    {
                        string cName = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : null;
                        if (!string.IsNullOrEmpty(cName))
                        {
                            args.Context.AddString($"Starting background backup for character: {cName}...");
                            System.Threading.Tasks.Task.Run(() => BackupManager.PerformFullBackup(null, cName));
                        }
                        else if (option == "char")
                        {
                            args.Context.AddString("ERROR: No active character found. Please load into the game first.");
                        }
                    }

                    if (option == "world" || option == "both")
                    {
                        if (ZNet.instance != null && ZNet.instance.IsServer())
                        {
                            string wName = ZNet.instance.GetWorldName();
                            if (!string.IsNullOrEmpty(wName))
                            {
                                args.Context.AddString($"Starting background backup for world: {wName}...");
                                System.Threading.Tasks.Task.Run(() => BackupManager.PerformFullBackup(wName, null));
                            }
                        }
                        else if (option == "world")
                        {
                            args.Context.AddString("ERROR: You can only backup a world if you are actively hosting it locally.");
                        }
                    }
                    
                    if (option != "char" && option != "world" && option != "both")
                    {
                        args.Context.AddString("Unknown option. Usage: sb.backup [char|world]");
                    }
                },
                isCheat: false,
                isNetwork: false,
                onlyServer: false,
                isSecret: false
            );

            new Terminal.ConsoleCommand(
                "sb.list",
                "Lists available backups.",
                (Terminal.ConsoleEventArgs args) =>
                {
                    var backups = BackupManager.GetAllAvailableBackups();
                    if (backups.Count == 0)
                    {
                        args.Context.AddString("No backups found.");
                        return;
                    }

                    int displayCount = Math.Min(15, backups.Count);
                    args.Context.AddString($"Found {backups.Count} backups. Showing last {displayCount}:");
                    
                    var sorted = backups.OrderByDescending(f => File.GetCreationTime(f)).Take(displayCount).ToList();
                    foreach (var b in sorted)
                    {
                        args.Context.AddString($"- {Path.GetFileName(b)}");
                    }
                },
                isCheat: false,
                isNetwork: false,
                onlyServer: false,
                isSecret: false
            );
        }

        private static void RestoreLatestBackup(string saveName, Terminal context)
        {
            if (ZNet.instance != null || Player.m_localPlayer != null)
            {
                context.AddString("ERROR: Restoring while actively loaded into a world is extremely dangerous and can corrupt your game! Please return to the Main Menu to restore.");
                return;
            }

            var backups = BackupManager.GetAllAvailableBackups();
            var targetBackups = backups.Where(b => Path.GetFileName(b).Contains(saveName))
                                       .OrderByDescending(b => File.GetCreationTime(b))
                                       .ToList();

            if (targetBackups.Count == 0)
            {
                context.AddString($"No backups found for target: {saveName}");
                return;
            }

            string latestZip = targetBackups.First();
            context.AddString($"Found latest backup: {Path.GetFileName(latestZip)}");

            try
            {
                // To safely extract without nuking original folder, we must determine the original path.
                // The easiest way is to extract it back to where the original files are located.
                // For simplicity, we search Valheim save dirs for the original files, or ask user to do it from Main Menu.
                
                string originalSaveDir = FindOriginalSaveDirectory(saveName);
                if (string.IsNullOrEmpty(originalSaveDir))
                {
                    context.AddString("Could not locate original save file location in Steam/Local. You may need to manually extract this zip from: \n" + latestZip);
                    return;
                }

                context.AddString($"Original location found: {originalSaveDir}");
                
                // Backup current files to .old
                var activeFiles = Directory.GetFiles(originalSaveDir).Where(f => Path.GetFileNameWithoutExtension(f) == saveName && !f.EndsWith(".old") && !f.EndsWith(".zip")).ToList();
                foreach (var activeFile in activeFiles)
                {
                    string oldPath = activeFile + ".old";
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                    File.Move(activeFile, oldPath);
                    context.AddString($"Renamed current active save to {Path.GetFileName(oldPath)}");
                }

                // Extract Zip
                using (ZipArchive archive = ZipFile.OpenRead(latestZip))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string extractPath = Path.Combine(originalSaveDir, entry.FullName);
                        entry.ExtractToFile(extractPath, overwrite: true);
                        context.AddString($"Extracted: {entry.FullName}");
                    }
                }

                context.AddString("Restore complete! Please restart your game session or reload from Main Menu if necessary.");
            }
            catch (Exception ex)
            {
                context.AddString($"Error during restore: {ex.Message}");
                SimpleBackupPlugin.Log.LogError(ex);
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
