# SimpleBackup

SimpleBackup is a Valheim backup plugin that adds fast in-game backup triggers on top of Valheim's native backup system.

## Ground Truth (Current Build)

SimpleBackup currently provides:

1. Esc-menu Backup button integrated under Save.
2. Console commands: `sb.backup`, `sb.backup world`, `sb.backup char`, `sb.list`.
3. Automatic timed backups (optional) via config.
4. Native backup creation through Valheim save APIs.
5. Save-before-backup synchronization through current-version ZNet save flow.
6. Single-flight execution with cooldown guard.
7. Minimalist UX messaging with duration in completion/failure toasts.
8. Flashing top-right backup indicator while backup is running.

## Backup Pipeline

Every trigger path (button, command, timer) follows the same pipeline:

1. Resolve requested target intent.
2. Enter coordinator gate (no overlap, 5-second cooldown).
3. Trigger native save sync via `ZNet.Save(false, true, false)` on main thread.
4. Wait for `ZNet.SaveDoneTime` to advance.
5. Create native backup(s) for selected target(s).
6. Emit concise completion/failure toast with elapsed time.

Important behavior details:

1. Explicit target commands are strict.
2. `sb.backup world` will not silently fall back to character.
3. `sb.backup char` will not silently fall back to world.
4. Backup sync is current-version only and does not use legacy trigger discovery fallbacks.
5. If save completion cannot be confirmed within timeout, backup is canceled to avoid stale snapshots.

## User Experience

Design goals are minimal and informative:

1. No center-screen backup spam.
2. Flashing top-right backup badge while backup is running.
3. Backup button is grayed out while backup is running or cooldown is active.
4. Concise top-left toast on completion/failure/cancel with timing.

Examples:

1. `Backup complete (1.8s): world 'MyWorld' and character 'test'.`
2. `Backup failed (1.2s): requested target unavailable.`
3. `Backup canceled (2.0s): could not confirm current save state.`
4. `Backup on cooldown.`
5. `Backup already running.`

## Target Rules

1. Hosted local world:
1. `sb.backup world` backs up world.
2. `sb.backup char` backs up character.
3. Esc button and `sb.backup` attempt both.
2. Non-host/client session:
1. `sb.backup world` is blocked.
2. Character backup paths remain available when character save is resolvable.

## Commands

1. `sb.backup` - backup current available targets.
2. `sb.backup world` - world only (host required).
3. `sb.backup char` - character only.
4. `sb.list` - lists legacy archive index entries.

Cooldown behavior:

1. Backup actions use a 5-second cooldown window.
2. During cooldown, the Esc-menu `Backup` button remains disabled (grayed out).

## Configuration

Config file: `com.aloncifer.simplebackup.cfg`

1. `BackupIntervalMinutes`: timed backup interval. `0` disables scheduler.
2. `MaxBackupsPerSave`: maximum number of native backups kept per save target.

Retention behavior:

1. Native backups are pruned after each successful backup so only the newest `MaxBackupsPerSave` entries remain per save.
2. Setting the value to `0` disables pruning.

## Build and Install

1. Build target: .NET Framework 4.6.2.
2. Build command: `dotnet build SimpleBackup.sln -c Release`.
3. Output: `src/bin/Release/net462/SimpleBackup.dll`.
4. Install DLL to BepInEx plugins folder.

## Notes

1. The backup badge uses IMGUI; icon glyph rendering can vary by system font.
2. Native restore/list rendering is still Valheim-owned UI behavior.
3. This build targets current Valheim save APIs and intentionally removes legacy trigger compatibility paths.
