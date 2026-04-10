using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleBackup
{
    [HarmonyPatch]
    public static class ManageSavesMenuPatch
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            bool hasOpenWithSelected = AccessTools.Method(
                typeof(ManageSavesMenu),
                "Open",
                new[]
                {
                    typeof(SaveDataType),
                    typeof(string),
                    typeof(ManageSavesMenu.ClosedCallback),
                    typeof(ManageSavesMenu.SavesModifiedCallback)
                }) != null;

            bool hasOpen = AccessTools.Method(
                typeof(ManageSavesMenu),
                "Open",
                new[]
                {
                    typeof(SaveDataType),
                    typeof(ManageSavesMenu.ClosedCallback),
                    typeof(ManageSavesMenu.SavesModifiedCallback)
                }) != null;

            bool hasUpdateList = AccessTools.Method(typeof(ManageSavesMenu), "UpdateSavesListGui") != null;

            if (!hasOpenWithSelected && !hasOpen && !hasUpdateList)
            {
                SimpleBackupPlugin.Log?.LogWarning("ManageSavesMenu patch skipped because the current Valheim build does not expose the expected Open/UpdateSavesListGui signatures.");
                return false;
            }

            return true;
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase openWithSelected = AccessTools.Method(
                typeof(ManageSavesMenu),
                "Open",
                new[]
                {
                    typeof(SaveDataType),
                    typeof(string),
                    typeof(ManageSavesMenu.ClosedCallback),
                    typeof(ManageSavesMenu.SavesModifiedCallback)
                });
            if (openWithSelected != null)
            {
                yield return openWithSelected;
            }

            MethodBase open = AccessTools.Method(
                typeof(ManageSavesMenu),
                "Open",
                new[]
                {
                    typeof(SaveDataType),
                    typeof(ManageSavesMenu.ClosedCallback),
                    typeof(ManageSavesMenu.SavesModifiedCallback)
                });
            if (open != null)
            {
                yield return open;
            }

            MethodBase updateList = AccessTools.Method(typeof(ManageSavesMenu), "UpdateSavesListGui");
            if (updateList != null)
            {
                yield return updateList;
            }
        }

        private static void Postfix(ManageSavesMenu __instance)
        {
            SimpleBackupManageSavesOverlay.Ensure(__instance);
        }
    }

    internal static class SimpleBackupManageSavesOverlay
    {
        private const string OpenButtonName = "SimpleBackupManageSavesButton";
        private const string PanelName = "SimpleBackupManageSavesPanel";
        private const string ContentName = "SimpleBackupManageSavesContent";
        private const string TitleName = "SimpleBackupManageSavesTitle";
        private const string StatusName = "SimpleBackupManageSavesStatus";
        private const string CloseButtonName = "SimpleBackupManageSavesClose";

        public static void Ensure(ManageSavesMenu menu)
        {
            if (menu == null || menu.transform == null)
            {
                return;
            }

            Transform root = menu.transform;
            Button templateButton = FindButton(root, "Save", "ButtonSave") ?? root.GetComponentInChildren<Button>(true);
            if (templateButton == null)
            {
                return;
            }

            EnsureOpenButton(root, templateButton);
            EnsurePanel(root, templateButton);
        }

        private static void EnsureOpenButton(Transform root, Button templateButton)
        {
            Transform existing = root.Find(OpenButtonName);
            if (existing == null)
            {
                GameObject buttonObject = UnityEngine.Object.Instantiate(templateButton.gameObject, root);
                buttonObject.name = OpenButtonName;

                Transform saveAnchor = FindButton(root, "Save", "ButtonSave")?.transform;
                if (saveAnchor != null)
                {
                    buttonObject.transform.SetSiblingIndex(saveAnchor.GetSiblingIndex() + 1);
                }

                TMP_Text text = buttonObject.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                {
                    text.text = "Backups";
                }

                Button button = buttonObject.GetComponent<Button>();
                if (button != null)
                {
                    ClearButtonEvents(button);
                    button.onClick.AddListener(() => TogglePanel(root));
                    CopyNavigation(templateButton, button);
                }
            }
            else
            {
                Button button = existing.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = true;
                }
            }
        }

        private static void EnsurePanel(Transform root, Button templateButton)
        {
            Transform panelTransform = root.Find(PanelName);
            if (panelTransform != null)
            {
                return;
            }

            GameObject panelObject = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(root, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 540f);
            panelRect.anchoredPosition = Vector2.zero;

            Image background = panelObject.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.82f);

            VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 18, 18);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = panelObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            TMP_Text title = CreateTextLabel(panelObject.transform, TitleName, "SimpleBackup Backups", 28, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;

            TMP_Text status = CreateTextLabel(panelObject.transform, StatusName, "Select a backup to restore.", 20, TextAlignmentOptions.Center);
            status.color = new Color(0.88f, 0.88f, 0.88f, 1f);

            GameObject contentObject = new GameObject(ContentName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(panelObject.transform, false);

            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(0, 0, 6, 6);
            contentLayout.spacing = 8f;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            ContentSizeFitter contentFitter = contentObject.GetComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            GameObject closeButtonObject = UnityEngine.Object.Instantiate(templateButton.gameObject, panelObject.transform);
            closeButtonObject.name = CloseButtonName;

            RectTransform closeRect = closeButtonObject.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-18f, -18f);

            TMP_Text closeText = closeButtonObject.GetComponentInChildren<TMP_Text>(true);
            if (closeText != null)
            {
                closeText.text = "Close";
            }

            Button closeButton = closeButtonObject.GetComponent<Button>();
            if (closeButton != null)
            {
                ClearButtonEvents(closeButton);
                closeButton.onClick.AddListener(() => SetPanelVisible(root, false));
                CopyNavigation(templateButton, closeButton);
            }

            LayoutElement closeLayout = closeButtonObject.GetComponent<LayoutElement>();
            if (closeLayout == null)
            {
                closeLayout = closeButtonObject.AddComponent<LayoutElement>();
            }
            closeLayout.ignoreLayout = true;

            SetPanelVisible(root, false);
            RefreshPanel(root, templateButton);
        }

        private static void TogglePanel(Transform root)
        {
            Transform panel = root.Find(PanelName);
            if (panel == null)
            {
                return;
            }

            bool shouldShow = !panel.gameObject.activeSelf;
            SetPanelVisible(root, shouldShow);
            if (shouldShow)
            {
                RefreshPanel(root, FindButton(root, "Save", "ButtonSave") ?? root.GetComponentInChildren<Button>(true));
            }
        }

        private static void SetPanelVisible(Transform root, bool visible)
        {
            Transform panel = root.Find(PanelName);
            if (panel != null)
            {
                panel.gameObject.SetActive(visible);
            }
        }

        private static void RefreshPanel(Transform root, Button templateButton)
        {
            Transform panel = root.Find(PanelName);
            if (panel == null)
            {
                return;
            }

            Transform content = panel.Find(ContentName);
            TMP_Text status = panel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text != null && text.gameObject.name == StatusName);
            if (content == null)
            {
                return;
            }

            foreach (Transform child in content)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            List<BackupManager.BackupTargetInfo> targets = RestoreCommandLogic.GetAvailableRestoreTargets();
            if (targets.Count == 0)
            {
                SetStatus(status, "No backups found.");
                return;
            }

            SetStatus(status, $"Found {targets.Count} backup targets.");

            int displayCount = Math.Min(targets.Count, 12);
            for (int i = 0; i < displayCount; i++)
            {
                BackupManager.BackupTargetInfo target = targets[i];
                CreateTargetRow(content, templateButton, target, status);
            }

            if (targets.Count > displayCount)
            {
                CreateSummaryRow(content, templateButton, targets.Count - displayCount);
            }
        }

        private static void CreateTargetRow(Transform parent, Button templateButton, BackupManager.BackupTargetInfo target, TMP_Text status)
        {
            GameObject rowObject = UnityEngine.Object.Instantiate(templateButton.gameObject, parent);
            rowObject.name = $"SimpleBackupRestore_{SanitizeName(target.TargetName)}";

            TMP_Text text = rowObject.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = $"Restore {target.TargetName}  ({target.CreatedAt:g})";
            }

            Button button = rowObject.GetComponent<Button>();
            if (button != null)
            {
                ClearButtonEvents(button);
                button.interactable = !BackupCoordinator.IsBackupInProgress;
                button.onClick.AddListener(() =>
                {
                    if (BackupCoordinator.IsBackupInProgress)
                    {
                        SetStatus(status, "Wait for the current backup to finish first.");
                        return;
                    }

                    if (!button.interactable)
                    {
                        return;
                    }

                    button.interactable = false;
                    SetStatus(status, $"Restoring latest backup for {target.TargetName}...");
                    Task.Run(() =>
                    {
                        bool restored = RestoreCommandLogic.TryRestoreLatestBackup(target.TargetName, SimpleBackupPlugin.QueueUIMessage);
                        if (!restored)
                        {
                            SimpleBackupPlugin.QueueUIMessage($"Restore failed for {target.TargetName}.");
                        }
                    });
                });
            }
        }

        private static void CreateSummaryRow(Transform parent, Button templateButton, int remainingCount)
        {
            GameObject summaryObject = UnityEngine.Object.Instantiate(templateButton.gameObject, parent);
            summaryObject.name = "SimpleBackupRestoreSummary";

            TMP_Text text = summaryObject.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = $"And {remainingCount} more backup target(s)...";
            }

            Button button = summaryObject.GetComponent<Button>();
            if (button != null)
            {
                ClearButtonEvents(button);
                button.interactable = false;
            }
        }

        private static TMP_Text CreateTextLabel(Transform parent, string name, string textValue, int fontSize, TextAlignmentOptions alignment)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);

            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.text = textValue;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static void SetStatus(TMP_Text status, string message)
        {
            if (status != null)
            {
                status.text = message;
            }
        }

        private static void CopyNavigation(Button source, Button target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.navigation = source.navigation;
        }

        private static void ClearButtonEvents(Button button)
        {
            if (button == null)
            {
                return;
            }

            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            }

            button.onClick.RemoveAllListeners();
        }

        private static Button FindButton(Transform root, params string[] labels)
        {
            if (root == null)
            {
                return null;
            }

            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button == null)
                {
                    continue;
                }

                if (labels.Any(label => string.Equals(button.name, label, StringComparison.OrdinalIgnoreCase)))
                {
                    return button;
                }

                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                {
                    string value = text.text != null ? text.text.Trim() : string.Empty;
                    if (labels.Any(label => string.Equals(value, label, StringComparison.OrdinalIgnoreCase)))
                    {
                        return button;
                    }
                }
            }

            return null;
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Unknown";
            }

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        }
    }
}
