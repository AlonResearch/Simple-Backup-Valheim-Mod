# TESTING GUIDE: SimpleBackup 0.0.3

This guide is for the current native-backup transition build.

## 1. Scope Of This Version

The in-game behavior to validate in 0.0.3:

1. Esc menu `Backup` button works.
2. `sb.backup` command family works.
3. Backups are created through Valheim native backup flow.
4. Backups appear in Valheim native Manage Saves data.
5. No custom SimpleBackup restore panel is present.

## 2. Prerequisites

1. Valheim installed and launches normally.
2. BepInEx 5 installed.
3. `SimpleBackup.dll` from this build in `BepInEx/plugins`.
4. At least one character exists.
5. For world tests, at least one local hosted world exists.

## 3. Build Verification

1. Build `SimpleBackup.sln` in Release.
2. Confirm `src/bin/Release/net462/SimpleBackup.dll` is produced.
3. Launch game once and verify plugin load line in log.

Expected result:

1. Build succeeds with no compile errors.
2. Plugin loads as version `0.0.3`.

## 4. Smoke Test In Main Menu

1. Open Valheim and stop at main menu.
2. Open `Manage saves` for characters and worlds.
3. Confirm there is no custom backup overlay panel from the old implementation.

Expected result:

1. Only vanilla Manage Saves UI is shown.

## 5. In-World Esc Button Test

1. Enter a hosted local world.
2. Press `Esc`.
3. Verify `Backup` button exists under `Save`.
4. Click `Backup` once.
5. Observe top-left status text.

Expected result:

1. Backup starts and completes.
2. Message includes target identity, such as world and/or character name.
3. No UI freeze or major frame hitch.

## 6. Concurrency And Cooldown Test

1. Trigger backup with Esc button.
2. Immediately trigger again within cooldown window.
3. Repeat with console command.

Expected result:

1. Second trigger is blocked with cooldown or running message.
2. No overlapping backup jobs start.

## 7. Console Command Test

Open console with `F5` and validate these commands:

1. `sb.backup`
2. `sb.backup char`
3. `sb.backup world` while hosting
4. `sb.backup world` while not hosting
5. `sb.list`

Expected result:

1. `sb.backup` creates native backup for current available targets.
2. `sb.backup char` creates character backup only.
3. `sb.backup world` creates world backup when hosting.
4. Non-host `sb.backup world` prints an error.
5. `sb.list` prints legacy archive index output only (not the native save list).

## 8. Native Manage Saves Integration Check

After running backups from button/commands:

1. Return to main menu.
2. Open `Manage saves`.
3. Check the relevant target rows in `Worlds` and `Characters` tabs.
4. Expand the row and inspect backup entries/count and timestamps.

Expected result:

1. Newly created backups appear as native backup entries.
2. Entries are grouped in the correct target category.
3. Naming and restore options follow Valheim native style.

## 9. Native Restore Validation

Use only vanilla UI restore:

1. In `Manage saves`, pick one target with at least one new backup.
2. Use vanilla restore action from that backup entry.
3. Load into game with restored character/world.

Expected result:

1. Restore completes via native flow.
2. Save identity remains correct (no unexpected character rename/reset).
3. Character does not behave like first-time intro unless expected by selected backup state.

## 10. Target Correctness Matrix

Run and record this matrix:

1. Hosted local world + `sb.backup world` -> world only.
2. Hosted local world + `sb.backup char` -> character only.
3. Hosted local world + Esc button -> world + character.
4. Client session + Esc button -> character only.
5. Client session + `sb.backup world` -> blocked with error.

Expected result:

1. Each command/button affects only intended target type(s).

## 11. Stability Checklist

1. No Harmony exceptions in Player.log related to Menu or save systems.
2. No unhandled exceptions during backup triggers.
3. Repeated backup cycles remain stable.

## 12. What To Report If A Test Fails

Provide this exact info:

1. Test section number and step.
2. Host/client state.
3. Target names used.
4. Console output and on-screen messages.
5. Player.log excerpt around failure.
6. Screenshot of Manage Saves before/after action.

## 13. Quick Pass Criteria

This version passes if all are true:

1. Backup button works in Esc menu.
2. `sb.backup` commands run with correct target scope.
3. Native backup entries appear in vanilla Manage Saves.
4. Native restore works from vanilla UI without identity corruption.
5. No custom restore panel is required for backup/restore workflow.
