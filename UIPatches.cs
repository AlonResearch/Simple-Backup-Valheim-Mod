using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleBackup
{
    [HarmonyPatch(typeof(Menu))]
    public static class MenuPatch
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void Start_Postfix(Menu __instance)
        {
            // Valheim's escape menu typically holds elements in a child named "Menu" or "MENU"
            Transform menuRoot = __instance.transform.Find("menu") ?? __instance.transform.Find("MENU") ?? __instance.transform.Find("MenuContainer");

            if (menuRoot == null)
            {
                // Fallback attempt to find save button directly
                Transform possibleSaveBtn = __instance.transform.Find("MENU/Save") ?? __instance.transform.Find("menu/Save");
                if (possibleSaveBtn != null)
                {
                    menuRoot = possibleSaveBtn.parent;
                }
            }

            if (menuRoot != null)
            {
                Transform saveButton = menuRoot.Find("Save") ?? menuRoot.Find("SaveBtn") ?? menuRoot.Find("ButtonSave");

                if (saveButton != null)
                {
                    SimpleBackupPlugin.Log.LogInfo("Found Save button in Esc Menu, injecting Backup button.");

                    GameObject backupButtonObj = GameObject.Instantiate(saveButton.gameObject, menuRoot);
                    backupButtonObj.name = "BackupGame";
                    
                    int saveBtnIndex = saveButton.GetSiblingIndex();
                    backupButtonObj.transform.SetSiblingIndex(saveBtnIndex + 1);

                    Text btnText = backupButtonObj.GetComponentInChildren<Text>();
                    if (btnText != null)
                    {
                        btnText.text = "Backup";
                    }

                    Button btn = backupButtonObj.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() =>
                        {
                            // Only backup the world if you are hosting it (ZNet.instance.IsServer())
                            string wName = (ZNet.instance != null && ZNet.instance.IsServer()) ? ZNet.instance.GetWorldName() : null;
                            string cName = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : null;

                            SimpleBackupPlugin.Log.LogInfo($"Manual UI Backup Triggered! Target: {wName}/{cName}");
                            // Run the backup on a background thread so the game does not freeze
                            System.Threading.Tasks.Task.Run(() => BackupManager.PerformFullBackup(wName, cName));
                            
                            if (MessageHud.instance != null)
                            {
                                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "Session Backup Started in Background!");
                            }
                        });
                    }
                    
                    // Valheim's Menu generally uses a VerticalLayoutGroup.
                    // Adding a sibling dynamically automatically adjusts the layout spacing for compatible mods!
                }
                else
                {
                    SimpleBackupPlugin.Log.LogWarning("Could not find the Save button in the Esc Menu.");
                }
            }
        }
    }
}
