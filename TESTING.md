# TESTING GUIDE: NativeBackup beta 0.1.1 (Current Ground Truth)

This test plan validates all currently implemented backup features, including current-version save-sync reliability, strict target behavior, indicator UX, duration toasts, retention pruning, and button cooldown states.

## 1. Prerequisites

1. Valheim installed and launchable.
2. BepInEx 5 installed.
3. Latest `NativeBackup.dll` in BepInEx plugins.
4. At least one character exists.
5. At least one local world exists for host tests.

## 2. Build Verification

1. Run `dotnet build NativeBackup.sln -c Release`.
2. Confirm output file exists at `src/bin/Release/net462/NativeBackup.dll`.

Expected:

1. Build succeeds without errors.

## 3. Startup And Injection Check

1. Launch game and enter a world.
2. Press `Esc` and verify `Backup` button exists under `Save`.

Expected:

1. Backup button is present.
2. No Harmony patch exceptions for menu injection.
3. Backup button becomes disabled while backup is running and during cooldown.

## 4. Backup Indicator UX Check

1. Trigger backup once (Esc button or `sb.backup`).
2. Watch top-right while backup is running.
3. Wait for completion.

Expected:

1. Flashing backup badge appears at top-right during backup.
2. Badge disappears when backup finishes/fails/cancels.
3. No center-screen backup spam appears.

## 5. Duration Toast Check

1. Trigger backup and observe completion toast.
2. Trigger at least one forced failure scenario (invalid explicit target, if reproducible).

Expected:

1. Success toast format includes elapsed seconds, for example `Backup complete (1.8s): ...`.
2. Failure/cancel toasts also include elapsed seconds.

## 6. Save-Sync Freshness (Core Persistence Test)

Character freshness:

1. Enter world with character A.
2. Make a visible state change that should persist (inventory/equipment/stat change).
3. Do not press vanilla Save.
4. Run `sb.backup char` immediately.
5. Restore from native Manage Saves and load again.

Expected:

1. Restored character includes the latest change.
2. Backup triggers the same native Save-button flow first, then creates backup after save completion is confirmed.

World freshness:

1. Host local world B.
2. Make a visible world change (place/remove structure).
3. Do not press vanilla Save.
4. Run `sb.backup world` immediately.
5. Restore via native UI and load world.

Expected:

1. Restored world includes latest change.
2. No stale snapshot from pre-change state.

Inventory race regression (berry scenario):

1. Enter world with an empty character inventory.
2. Pick up exactly one berry (or another unique single item).
3. Open Esc menu and trigger backup.
4. Wait for `Backup complete (...)` toast.
5. Drop the item from inventory.
6. Exit to menu and restore the exact latest character backup entry.
7. Load the same character again.

Expected:

1. The restored character has the backed-up item in inventory.
2. If item is missing, collect logs for save/backup ordering (SaveStartTime/SaveDoneTime flow) and exact backup filename restored.

## 7. Command Pipeline Validation

Run in console (`F5`):

1. `sb.backup`
2. `sb.backup char`
3. `sb.backup world`
4. `sb.list`

Expected:

1. `sb.backup` targets available saves for current session context.
2. `sb.backup char` targets character only.
3. `sb.backup world` targets world only when host.
4. `sb.list` lists legacy archive index entries.

## 8. Strict Target Intent Validation

1. Run `sb.backup char` in hosted world session.
2. Create a condition where char backup cannot be resolved (if reproducible).
3. Observe result.
4. Run `sb.backup world` with world unavailable (non-host/client).

Expected:

1. Explicit commands do not fall back to the other target type.
2. Failure message is explicit for requested target.

## 9. Cooldown And Single-Flight

1. Trigger backup.
2. Trigger backup again immediately.
3. Trigger a third while previous is still running (if timing allows).

Expected:

1. `Backup on cooldown.` or `Backup already running.` appears.
2. No overlapping backup jobs start.
3. Cooldown is approximately 5 seconds.
4. Esc-menu Backup button is grayed out while cooldown is active.

## 10. Scene Gating

1. From a scene where save backend is unavailable, attempt backup trigger.

Expected:

1. Toast indicates backup unavailable in current scene.
2. No crash or unhandled exception.

## 11. Native Manage Saves Visibility

1. After successful backup runs, return to main menu.
2. Open native Manage Saves for Characters and Worlds.
3. Verify new entries and timestamps.

Expected:

1. Backup entries appear under correct native target category.

## 12. Scheduled Backup Path

1. Set `BackupIntervalMinutes=1` in config.
2. Enter world and wait for interval.
3. Observe indicator and completion toast.

Expected:

1. Scheduled backup follows same pipeline.
2. Indicator and duration toast behavior matches manual triggers.

## 13. Log Quality Validation

1. Check BepInEx log during successful and failed runs.

Expected:

1. Routine flow remains mostly debug/info level.
2. Warnings/errors appear only for actionable problems (sync unavailable, target unavailable, etc.).
3. No noisy repetitive spam each frame.

## 14. Current-Version Save Sync Regression

1. Start game and trigger backup once.
2. Inspect logs for save trigger routing.

Expected:

1. Save sync uses native Save-button routing (menu save handler or the Save button `onClick` path).
2. Backup waits for save completion signal before creating backup.
3. No `Failed to trigger native Save-button flow before backup.` warnings during normal in-world use.

## 15. Native Retention Validation

1. Set `MaxBackupsPerSave=5`.
2. Trigger more than five successful backups for the same save target.
3. Inspect the native `backups` folder in Valheim save storage.

Expected:

1. Only the newest five backup entries remain for that save.
2. Older entries are pruned after each successful backup.
3. Pruning does not affect backups for other saves.

## 16. Regression Checklist

1. Esc menu remains usable and responsive.
2. No game freeze during backup operations.
3. No corrupted save identity after restore.
4. No command registration regressions (`sb.backup` present after loading).

## 17. Failure Report Template

Include all items below for failed scenarios:

1. Test section number and failing step.
2. Host/client state.
3. Trigger used (Esc, `sb.backup`, `sb.backup world`, `sb.backup char`, timer).
4. On-screen toast text.
5. Console output.
6. Relevant BepInEx/Player.log excerpt.
7. Manage Saves screenshots before/after.

## 18. Pass Criteria

Build passes when all are true:

1. Backup button and command flows work in expected contexts.
2. Save-sync freshness test passes for both character and world.
3. Explicit target commands remain strict without cross-target fallback.
4. Top-right backup indicator and duration toasts behave correctly.
5. Native Manage Saves entries appear for successful backups.
6. No unhandled exceptions or severe runtime regressions.
