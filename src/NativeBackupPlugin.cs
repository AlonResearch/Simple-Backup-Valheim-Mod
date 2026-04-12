using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using System.Reflection;
using System.Collections.Concurrent;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.IO;

namespace NativeBackup
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class NativeBackupPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.aloncifer.nativebackup";
        public const string PluginName = "NativeBackup";
        public const string PluginVersion = "0.1.1";

        private Harmony _harmony;
        public static NativeBackupPlugin Instance;
        private static readonly ConcurrentQueue<string> _uiMessageQueue = new ConcurrentQueue<string>();
        private static readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
        private static int _backupIndicatorActive;
        private static Texture2D _backupIndicatorTexture;
        private static bool _iconLoadInProgress;
        private static bool _iconLoadCompleted;
        private static int _nativeFallbackActive;
        private static readonly string[] _indicatorMethodNames =
        {
            "ShowSavingIcon",
            "SetSavingIcon",
            "SetSavingIndicator",
            "SetSaving"
        };
        private static readonly string[] _indicatorEnableMethodNames =
        {
            "ShowSavingIcon",
            "ShowSaving",
            "StartSavingIcon"
        };
        private static readonly string[] _indicatorDisableMethodNames =
        {
            "HideSavingIcon",
            "HideSaving",
            "StopSavingIcon"
        };
        private static readonly string[] _indicatorFieldNames =
        {
            "m_showSavingIcon",
            "m_showSaving",
            "m_saving"
        };

        public static void QueueUIMessage(string msg)
        {
            _uiMessageQueue.Enqueue(msg);
        }

        public static void SetBackupIndicatorActive(bool active)
        {
            Interlocked.Exchange(ref _backupIndicatorActive, active ? 1 : 0);

            // Native UI hooks must run on the main Unity thread.
            _mainThreadActions.Enqueue(() =>
            {
                try
                {
                    if (active)
                    {
                        bool hasCustomIcon = TryEnsureBackupIndicatorIconLoaded();
                        if (!hasCustomIcon && _iconLoadCompleted && Volatile.Read(ref _nativeFallbackActive) == 0)
                        {
                            if (TrySetNativeSavingIndicator(true))
                            {
                                Interlocked.Exchange(ref _nativeFallbackActive, 1);
                            }
                        }
                        return;
                    }

                    if (Volatile.Read(ref _nativeFallbackActive) == 1 && !IsNativeSaveStillRunning())
                    {
                        TrySetNativeSavingIndicator(false);
                        Interlocked.Exchange(ref _nativeFallbackActive, 0);
                    }
                }
                catch (Exception ex)
                {
                    Log?.LogDebug($"Failed to toggle native saving indicator: {ex.Message}");
                }
            });
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
        public static ConfigEntry<int> MaxBackupsPerSave;

        private float _timeSinceLastBackup = 0f;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BackupIntervalMinutes = Config.Bind("General", "BackupIntervalMinutes", 0, "Time in minutes between automatic backups. Set to 0 to disable automatic backups.");
            MaxBackupsPerSave = Config.Bind("General", "MaxBackupsPerSave", 5, "Maximum number of native backups to keep per save.");

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            StartIconLoadIfNeeded();

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
            if (_backupIndicatorTexture != null)
            {
                Destroy(_backupIndicatorTexture);
                _backupIndicatorTexture = null;
            }
        }

        private void OnGUI()
        {
            if (Volatile.Read(ref _backupIndicatorActive) != 1)
            {
                return;
            }

            if (!TryEnsureBackupIndicatorIconLoaded())
            {
                if (_iconLoadCompleted && Volatile.Read(ref _nativeFallbackActive) == 0)
                {
                    if (TrySetNativeSavingIndicator(true))
                    {
                        Interlocked.Exchange(ref _nativeFallbackActive, 1);
                    }
                }
                return;
            }

            float pulse = 0.35f + (0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)));
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, pulse);

            var iconRect = new Rect(18f, 18f, 40f, 40f);
            GUI.DrawTexture(iconRect, _backupIndicatorTexture, ScaleMode.ScaleToFit, true);

            GUI.color = previous;
        }

        private static bool TryEnsureBackupIndicatorIconLoaded()
        {
            if (_backupIndicatorTexture != null)
            {
                return true;
            }

            StartIconLoadIfNeeded();
            return false;
        }

        private static void StartIconLoadIfNeeded()
        {
            if (Instance == null || _backupIndicatorTexture != null || _iconLoadCompleted || _iconLoadInProgress)
            {
                return;
            }

            Instance.StartCoroutine(Instance.LoadBackupIndicatorIconCoroutine());
        }

        private IEnumerator LoadBackupIndicatorIconCoroutine()
        {
            _iconLoadInProgress = true;

            foreach (string path in GetIconCandidatePaths())
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    continue;
                }

                string absolutePath = Path.GetFullPath(path).Replace("\\", "/");
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture("file:///" + absolutePath, false))
                {
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Texture2D texture = DownloadHandlerTexture.GetContent(request);
                        if (texture != null)
                        {
                            texture.wrapMode = TextureWrapMode.Clamp;
                            texture.filterMode = FilterMode.Bilinear;
                            _backupIndicatorTexture = texture;
                            _iconLoadInProgress = false;
                            _iconLoadCompleted = true;
                            Log?.LogDebug($"Loaded backup indicator icon from '{path}'.");
                            yield break;
                        }
                    }
                }
            }

            _iconLoadInProgress = false;
            _iconLoadCompleted = true;
            Log?.LogDebug("Backup indicator icon load failed; native indicator fallback will be used.");
        }

        private static IEnumerable<string> GetIconCandidatePaths()
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (!string.IsNullOrEmpty(assemblyDir))
            {
                yield return Path.Combine(assemblyDir, "icon.png");
                yield return Path.Combine(assemblyDir, "NativeBackup", "icon.png");
            }

            yield return Path.Combine(Paths.PluginPath, "icon.png");
            yield return Path.Combine(Paths.PluginPath, "NativeBackup", "icon.png");
            yield return Path.Combine(Paths.BepInExRootPath, "icon.png");
            yield return Path.Combine(Environment.CurrentDirectory, "icon.png");
        }

        private static bool IsNativeSaveStillRunning()
        {
            if (ZNet.instance == null)
            {
                return false;
            }

            return ZNet.instance.SaveStartTime > ZNet.instance.SaveDoneTime;
        }

        private static bool TrySetNativeSavingIndicator(bool active)
        {
            object[] targets =
            {
                MessageHud.instance,
                Hud.instance
            };

            foreach (object target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                Type targetType = target.GetType();

                foreach (string methodName in _indicatorMethodNames)
                {
                    MethodInfo boolMethod = AccessTools.Method(targetType, methodName, new[] { typeof(bool) });
                    if (boolMethod != null)
                    {
                        boolMethod.Invoke(target, new object[] { active });
                        return true;
                    }
                }

                string[] noArgMethods = active ? _indicatorEnableMethodNames : _indicatorDisableMethodNames;
                foreach (string methodName in noArgMethods)
                {
                    MethodInfo method = AccessTools.Method(targetType, methodName, Type.EmptyTypes);
                    if (method != null)
                    {
                        method.Invoke(target, null);
                        return true;
                    }
                }

                foreach (string fieldName in _indicatorFieldNames)
                {
                    FieldInfo field = AccessTools.Field(targetType, fieldName);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        field.SetValue(target, active);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

