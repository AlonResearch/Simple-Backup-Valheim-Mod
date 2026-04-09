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

            RegisterCommands(__instance);
            SimpleBackupPlugin.Log.LogInfo("sb. Commands Registered in Postfix.");
        }

        [HarmonyPatch("TryRunCommand")]
        [HarmonyPrefix]
        public static bool TryRunCommand_Prefix(Terminal __instance, string text)
        {
            if (string.IsNullOrEmpty(text)) return true;

            string cmd = text.Trim();
            string cmdLower = cmd.ToLower();

            if (cmdLower.StartsWith("sb."))
            {
                string[] parts = cmd.Split(' ');
                string commandName = parts[0].ToLower();

                if (commandName == "sb.backup")
                {
                    HandleBackupCommand(__instance, parts);
                    return false; // Handled
                }
                else if (commandName == "sb.list")
                {
                    HandleListCommand(__instance);
                    return false; // Handled
                }
                else if (commandName == "sb.restore")
                {
                    HandleRestoreCommand(__instance, parts);
                    return false; // Handled
                }
            }

            return true; // Not our command, let the game handle normally
        }

        private static void RegisterCommands(Terminal terminal)
        {
            if (!Terminal.commands.ContainsKey("sb.restore"))
            {
                new Terminal.ConsoleCommand("sb.restore", "Restores a backup.", (args) => HandleRestoreCommand(args.Context, args.Args));
            }

            if (!Terminal.commands.ContainsKey("sb.backup"))
            {
                new Terminal.ConsoleCommand("sb.backup", "Triggers a backup.", (args) => HandleBackupCommand(args.Context, args.Args));
            }

            if (!Terminal.commands.ContainsKey("sb.list"))
            {
                new Terminal.ConsoleCommand("sb.list", "Lists backups.", (args) => HandleListCommand(args.Context));
            }

            terminal.updateCommandList();
        }

        private static void HandleBackupCommand(Terminal context, string[] args)
        {
            string option = args.Length >= 2 ? args[1].ToLower() : "both";
            
            if (option == "char" || option == "both")
            {
                string cName = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : null;
                if (!string.IsNullOrEmpty(cName))
                {
                    context.AddString($"Starting background backup for character: {cName}...");
                    System.Threading.Tasks.Task.Run(() => BackupManager.PerformFullBackup(null, cName));
                }
                else if (option == "char")
                {
                    context.AddString("ERROR: No active character found.");
                }
            }

            if (option == "world" || option == "both")
            {
                if (ZNet.instance != null && ZNet.instance.IsServer())
                {
                    string wName = ZNet.instance.GetWorldName();
                    if (!string.IsNullOrEmpty(wName))
                    {
                        context.AddString($"Starting background backup for world: {wName}...");
                        System.Threading.Tasks.Task.Run(() => BackupManager.PerformFullBackup(wName, null));
                    }
                }
                else if (option == "world")
                {
                    context.AddString("ERROR: You can only backup a world if you are actively hosting it locally.");
                }
            }
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

            string saveName = args[1];
            RestoreLatestBackup(saveName, context);
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
