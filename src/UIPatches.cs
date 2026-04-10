using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SimpleBackup
{
    [HarmonyPatch(typeof(Menu))]
    public static class MenuPatch
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void Start_Postfix(Menu __instance)
        {
            if (__instance == null || __instance.m_menuDialog == null) return;

            Transform menuEntries = __instance.m_menuDialog.transform.Find("MenuEntries");
            if (menuEntries == null)
            {
                 menuEntries = __instance.m_menuDialog.transform.Find("menu") ?? 
                               __instance.m_menuDialog.transform.Find("MENU") ?? 
                               __instance.m_menuDialog.transform.Find("MenuContainer");
            }

            if (menuEntries != null)
            {
                // We clone Settings but anchor to Save for better positioning
                Transform settingsTemplate = FindButton(menuEntries, "Settings", "ButtonSettings");
                Transform saveAnchor = FindButton(menuEntries, "Save", "ButtonSave");

                if (settingsTemplate != null)
                {
                    SimpleBackupPlugin.Log.LogInfo("Injecting Backup button under Save.");

                    GameObject backupButtonObj = GameObject.Instantiate(settingsTemplate.gameObject, menuEntries);
                    backupButtonObj.name = "BackupGame";
                    
                    // Position after Save if possible
                    if (saveAnchor != null)
                    {
                        backupButtonObj.transform.SetSiblingIndex(saveAnchor.GetSiblingIndex() + 1);
                    }
                    else
                    {
                        backupButtonObj.transform.SetSiblingIndex(settingsTemplate.GetSiblingIndex() + 1);
                    }

                    TMP_Text btnText = backupButtonObj.GetComponentInChildren<TMP_Text>();
                    if (btnText != null) btnText.text = "Backup";

                    Button btn = backupButtonObj.GetComponent<Button>();
                    if (btn != null)
                    {
                        // 1. Silent the native 'Settings' persistent trigger from the clone
                        for (int i = 0; i < btn.onClick.GetPersistentEventCount(); i++)
                        {
                            btn.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                        }

                        // 2. Add our clean backup functionality
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() =>
                        {
                            string wName = (ZNet.instance != null && ZNet.instance.IsServer()) ? ZNet.instance.GetWorldName() : null;
                            string cName = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : null;

                            BackupCoordinator.BackupStartResult startResult = BackupCoordinator.TryStartBackup(wName, cName);
                            if (startResult == BackupCoordinator.BackupStartResult.Started)
                            {
                                SimpleBackupPlugin.Log.LogInfo($"Manual UI Backup Triggered! Target: {wName}/{cName}");
                                if (MessageHud.instance != null)
                                {
                                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "Session Backup Started in Background!");
                                }
                            }
                            else if (MessageHud.instance != null)
                            {
                                string message = startResult == BackupCoordinator.BackupStartResult.CooldownActive
                                    ? "Backup cooldown active. Please wait 10 seconds before starting another backup."
                                    : "Backup already running.";
                                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, message);
                            }
                        });

                        Button templateButton = settingsTemplate.GetComponent<Button>();
                        if (templateButton != null)
                        {
                            btn.navigation = templateButton.navigation;
                        }
                    }
                }
            }
        }

        private static Transform FindButton(Transform root, params string[] labels)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button == null)
                {
                    continue;
                }

                if (labels.Any(label => string.Equals(button.name, label, StringComparison.OrdinalIgnoreCase)))
                {
                    return button.transform;
                }

                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                {
                    string value = text.text != null ? text.text.Trim() : string.Empty;
                    if (labels.Any(label => string.Equals(value, label, StringComparison.OrdinalIgnoreCase)))
                    {
                        return button.transform;
                    }
                }
            }

            return root.Find(labels.FirstOrDefault());
        }
    }
}
