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
                 // Fallback for different UI versions
                 menuEntries = __instance.m_menuDialog.transform.Find("menu") ?? 
                               __instance.m_menuDialog.transform.Find("MENU") ?? 
                               __instance.m_menuDialog.transform.Find("MenuContainer");
            }

            if (menuEntries != null)
            {
                // Clone the 'Settings' button as it's the most stable anchor
                Transform settingsButton = menuEntries.Find("Settings") ?? 
                                            menuEntries.Find("ButtonSettings") ??
                                            menuEntries.Find("SettingsBtn");

                if (settingsButton != null)
                {
                    SimpleBackupPlugin.Log.LogInfo("Found Settings button in Esc Menu, injecting Backup button.");

                    GameObject backupButtonObj = GameObject.Instantiate(settingsButton.gameObject, menuEntries);
                    backupButtonObj.name = "BackupGame";
                    
                    int settingsBtnIndex = settingsButton.GetSiblingIndex();
                    backupButtonObj.transform.SetSiblingIndex(settingsBtnIndex + 1);

                    // Modern Valheim uses TextMeshPro (TMP_Text)
                    TMP_Text btnText = backupButtonObj.GetComponentInChildren<TMP_Text>();
                    if (btnText != null)
                    {
                        btnText.text = "Backup";
                    }
                    else
                    {
                        // Fallback for older UI
                        Text legacyText = backupButtonObj.GetComponentInChildren<Text>();
                        if (legacyText != null) legacyText.text = "Backup";
                    }

                    Button btn = backupButtonObj.GetComponent<Button>();
                    if (btn != null)
                    {
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

                        // Ensure controller/keyboard navigation works by cloning the settings button's navigation
                        btn.navigation = settingsButton.GetComponent<Button>().navigation;
                    }
                }
                else
                {
                    SimpleBackupPlugin.Log.LogWarning("Could not find the Settings button in the Esc Menu container.");
                }
            }
            else
            {
                SimpleBackupPlugin.Log.LogWarning("Could not find the MenuEntries container in the Esc Menu.");
            }
        }
    }
}
