using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Collections.Concurrent;

namespace SimpleBackup
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class SimpleBackupPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.aloncifer.simplebackup";
        public const string PluginName = "SimpleBackup";
        public const string PluginVersion = "0.0.2";

        private Harmony _harmony;
        public static SimpleBackupPlugin Instance;
        private static readonly ConcurrentQueue<string> _uiMessageQueue = new ConcurrentQueue<string>();

        public static void QueueUIMessage(string msg)
        {
            _uiMessageQueue.Enqueue(msg);
        }
        public static ManualLogSource Log;

        public static ConfigEntry<int> BackupIntervalMinutes;
        public static ConfigEntry<int> MaxBackupsToKeep;

        private float _timeSinceLastBackup = 0f;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BackupIntervalMinutes = Config.Bind("General", "BackupIntervalMinutes", 0, "Time in minutes between automatic backups. Set to 0 to disable automatic backups.");
            MaxBackupsToKeep = Config.Bind("General", "MaxBackupsToKeep", 5, "Maximum number of backups to keep per world or character.");

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded!");
        }

        private float _commandCheckTimer = 0f;

        private void Update()
        {
            _commandCheckTimer += Time.deltaTime;
            if (_commandCheckTimer > 2.0f)
            {
                _commandCheckTimer = 0f;
                // Failsafe: Continuously verify our console commands are registered
                if (RestoreCommandLogic.IsBackupCommandMissing())
                {
                    RestoreCommandLogic.RegisterCommands();
                }
            }

            while (_uiMessageQueue.TryDequeue(out string message))
            {
                if (MessageHud.instance != null)
                {
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, message);
                }
                
                // If console is open, also safely print it there
                if (Console.instance != null)
                {
                    Console.instance.Print(message);
                }
            }

            if (BackupIntervalMinutes.Value > 0)
            {
                // Only run the timer if we are actively loaded into a world
                if (ZNet.instance == null) return;

                _timeSinceLastBackup += Time.deltaTime;
                float intervalSeconds = BackupIntervalMinutes.Value * 60f;

                if (_timeSinceLastBackup >= intervalSeconds)
                {
                    _timeSinceLastBackup = 0f;
                    
                    // Only backup world automatically if the user is the host
                    string worldName = (ZNet.instance != null && ZNet.instance.IsServer()) ? ZNet.instance.GetWorldName() : null;
                    string charName = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : null;

                    BackupCoordinator.BackupStartResult startResult = BackupCoordinator.TryStartBackup(worldName, charName);
                    if (startResult == BackupCoordinator.BackupStartResult.Started)
                    {
                        Logger.LogInfo($"Performing scheduled automatic backup for World: {worldName} | Char: {charName}...");
                    }
                    else
                    {
                        if (startResult == BackupCoordinator.BackupStartResult.CooldownActive)
                        {
                            Logger.LogWarning("Skipped scheduled backup because the 10-second backup cooldown is active.");
                        }
                        else
                        {
                            Logger.LogWarning("Skipped scheduled backup because another backup is already running.");
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
