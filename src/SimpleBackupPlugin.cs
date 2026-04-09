using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

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

                    Logger.LogInfo($"Performing scheduled automatic backup for World: {worldName} | Char: {charName}...");
                    System.Threading.Tasks.Task.Run(() => BackupManager.PerformFullBackup(worldName, charName));
                }
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
