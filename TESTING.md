# TESTING GUIDE: SimpleBackup

This guide covers the current feature set:
- Esc menu backup button
- Console commands
- Manage Saves backup browser and restore flow
- Background backup safety and concurrency

## 1. Prerequisites
1. Install Valheim with BepInEx 5.
2. Build the mod and place the output DLL in the BepInEx plugins folder.
3. Start the game once so the config and backup directory are created.
4. If you want to test restore, make sure you have at least one existing character or world backup.

## 2. Build Check
Before launching the game, verify the project compiles cleanly.

1. Open the solution in Visual Studio or VS Code.
2. Build `SimpleBackup.sln`.
3. Confirm there are no compile errors in the mod files.

Expected result:
- The mod builds successfully.
- `SimpleBackup.dll` is produced for `net462`.

## 3. Esc Menu Backup Button
This checks the in-game backup trigger in the vanilla pause menu.

1. Launch Valheim and load a world.
2. Press `Esc`.
3. Verify a `Backup` button appears under `Save`.
4. Click `Backup`.

Expected result:
- A center or top-left message says the backup started.
- The backup runs in the background without freezing the game.
- Only one backup job should run at a time.

Concurrency check:
1. Click `Backup` repeatedly while a backup is already running.
2. Confirm the UI shows that a backup is already running.

## 4. Automatic Backup Timer
Use this only if you enabled the config value.

1. Set `BackupIntervalMinutes` to a small value such as `1`.
2. Load a world and stay in-game until the interval passes.
3. Watch for the backup notification.

Expected result:
- The scheduled backup starts once the timer elapses.
- It does not start a second job if one is already active.

## 5. Console Commands
Open the Valheim console and test each command separately.

### `sb.list`
1. Open the console with `F5`.
2. Run `sb.list`.

Expected result:
- The console prints the available backups.
- If none exist, it prints `No backups found.`

### `sb.backup`
1. Open the console with `F5`.
2. Run `sb.backup`.

Expected result:
- The command starts a backup for the currently available save targets.
- If a backup is already running, it prints a busy message.

### `sb.backup char`
1. Open the console with `F5`.
2. Run `sb.backup char`.

Expected result:
- It backs up the local character save.
- If no local character is available, it prints an error.

### `sb.backup world`
1. Host a local world.
2. Open the console with `F5`.
3. Run `sb.backup world`.

Expected result:
- It backs up the current host world.
- If you are not the host, it prints an error.

### `sb.restore`
1. Open the console with `F5`.
2. Run `sb.restore <SaveName>` using a save name that exists in your backup archive.

Expected result:
- The command refuses to restore while you are actively in a world.
- From the main menu, it should locate the latest matching backup and restore it.

## 6. Manage Saves UI
This checks the native-style backup browser added to the game’s existing save management screen.

1. Return to the main menu.
2. Open the game’s `Manage Saves` screen.
3. Verify a `Backups` button appears in the existing save UI.
4. Click `Backups`.

Expected result:
- A backup list panel opens inside the same vanilla UI.
- The panel shows available backup targets.
- A close button hides the panel without leaving the menu.

Restore check:
1. Pick a backup target from the panel.
2. Trigger restore.
3. Confirm the mod reports restore progress and completion.

Expected result:
- The mod restores the latest matching backup for that target.
- Restore should only work from the main menu, not while loaded into a world.

## 7. Backup Output Verification
After any successful backup, verify the files on disk.

1. Open the backup root directory at `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\SimpleBackup`.
2. Confirm a new `.zip` file was created.
3. Check that the zip name includes the timestamp and save target.
4. Open the archive and confirm it contains the expected `.db`, `.fwl`, or `.fch` files.

Expected result:
- Backups are stored outside the live save folders.
- The archive contains the real save payload, not an empty file.

## 8. Restore Safety Check
This validates the `.old` safety behavior.

1. Make a test backup of a save you can safely restore.
2. Restore it from the main menu.
3. Inspect the original save directory.

Expected result:
- Existing files are renamed with `.old` before extraction.
- The restored files are extracted into the original save directory.
- If restore fails midway, the previous files should still be recoverable from the `.old` copies.

## 9. What To Report If Something Fails
If a test fails, note these details:

1. Which feature failed: Esc menu, console command, Manage Saves, backup output, or restore.
2. The exact save target name you used.
3. Any console output or on-screen message.
4. Whether you were in a world, in the main menu, or hosting locally.

## 10. Minimal Pass/Fail Checklist
- `Backup` appears under `Save` in the Esc menu.
- `sb.backup`, `sb.list`, and `sb.restore` all register and print output.
- The Manage Saves screen shows the `Backups` button.
- Backups create timestamped zip files.
- Restore refuses to run in an active world.
- Restore uses the latest matching backup and keeps `.old` safety copies.
