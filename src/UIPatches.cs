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
                Transform settingsTemplate = menuEntries.Find("Settings") ?? menuEntries.Find("ButtonSettings");
                Transform saveAnchor = menuEntries.Find("Save") ?? menuEntries.Find("ButtonSave");

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

                            SimpleBackupPlugin.Log.LogInfo($"Manual UI Backup Triggered! Target: {wName}/{cName}");
                            System.Threading.Tasks.Task.Run(() => BackupManager.PerformFullBackup(wName, cName));
                            
                            if (MessageHud.instance != null)
                            {
                                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "Session Backup Started in Background!");
                            }
                        });

                        btn.navigation = settingsTemplate.GetComponent<Button>().navigation;
                    }
                }
            }
        }
    }
}
