using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Collections.Concurrent;
using System;
using System.Threading;
using System.Collections.Generic;

namespace SimpleBackup
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class SimpleBackupPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.aloncifer.simplebackup";
        public const string PluginName = "SimpleBackup";
        public const string PluginVersion = "0.0.3";

        private Harmony _harmony;
        public static SimpleBackupPlugin Instance;
        private static readonly ConcurrentQueue<string> _uiMessageQueue = new ConcurrentQueue<string>();
        private static readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
        private static int _backupIndicatorActive;
        private static long _backupIndicatorStartTicks;
        private static GUIStyle _backupIndicatorStyle;

        public static void QueueUIMessage(string msg)
        {
            _uiMessageQueue.Enqueue(msg);
        }

        public static void SetBackupIndicatorActive(bool active)
        {
            if (active)
            {
                Interlocked.Exchange(ref _backupIndicatorActive, 1);
                Interlocked.Exchange(ref _backupIndicatorStartTicks, DateTime.UtcNow.Ticks);
                return;
            }

            Interlocked.Exchange(ref _backupIndicatorActive, 0);
        }

        public static bool TryInvokeOnMainThread(Action action, int timeoutMs = 3000)
        {
            if (action == null || Instance == null)
            {
                return false;
            }

            Exception actionException = null;
            using (var completed = new ManualResetEventSlim(false))
            {
                _mainThreadActions.Enqueue(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        actionException = ex;
                    }
                    finally
                    {
                        completed.Set();
                    }
                });

                if (!completed.Wait(timeoutMs))
                {
                    return false;
                }
            }

            if (actionException != null)
            {
                Log?.LogWarning($"Main-thread action failed: {actionException.Message}");
                return false;
            }

            return true;
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
            }

            var pendingMainThreadActions = new List<Action>();
            while (_mainThreadActions.TryDequeue(out Action action))
            {
                pendingMainThreadActions.Add(action);
            }

            foreach (Action action in pendingMainThreadActions)
            {
                action?.Invoke();
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
                    string charName = BackupManager.GetCurrentCharacterSaveName();

                    BackupCoordinator.BackupStartResult startResult = BackupCoordinator.TryStartBackup(worldName, charName);
                    if (startResult == BackupCoordinator.BackupStartResult.Started)
                    {
                        Logger.LogDebug($"Scheduled backup started for world='{worldName}', character='{charName}'.");
                    }
                    else
                    {
                        if (startResult == BackupCoordinator.BackupStartResult.CooldownActive)
                        {
                            Logger.LogDebug("Skipped scheduled backup due to cooldown.");
                        }
                        else
                        {
                            Logger.LogDebug("Skipped scheduled backup because another backup is running.");
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void OnGUI()
        {
            if (Volatile.Read(ref _backupIndicatorActive) != 1)
            {
                return;
            }

            if (_backupIndicatorStyle == null)
            {
                _backupIndicatorStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    richText = true
                };
            }

            float pulse = 0.35f + (0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)));
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, pulse);

            var badgeRect = new Rect(Screen.width - 74f, 18f, 56f, 38f);
            GUI.Label(badgeRect, "🛡💾", _backupIndicatorStyle);

            GUI.color = previous;
        }
    }
}
