# SimpleBackup

SimpleBackup is a Valheim backup plugin that adds fast in-game backup triggers on top of Valheim's native backup system.

## Ground Truth (Current Build)

SimpleBackup currently provides:

1. Esc-menu Backup button integrated under Save.
2. Console commands: `sb.backup`, `sb.backup world`, `sb.backup char`, `sb.list`.
3. Automatic timed backups (optional) via config.
4. Native backup creation through Valheim save APIs.
5. Save-before-backup synchronization (best effort, fail-closed when unconfirmed).
6. Single-flight execution with cooldown guard.
7. Minimalist UX messaging with duration in completion/failure toasts.
8. Flashing top-right backup indicator while backup is running.

## Backup Pipeline

Every trigger path (button, command, timer) follows the same pipeline:

1. Resolve requested target intent.
2. Enter coordinator gate (no overlap, short cooldown).
3. Trigger native save sync before backup.
4. Confirm save completion when possible.
5. Create native backup(s) for selected target(s).
6. Emit concise completion/failure toast with elapsed time.

Important behavior details:

1. Explicit target commands are strict.
2. `sb.backup world` will not silently fall back to character.
3. `sb.backup char` will not silently fall back to world.
4. If current-state save sync cannot be confirmed, backup is canceled instead of producing a stale snapshot.

## User Experience

Design goals are minimal and informative:

1. No center-screen backup spam.
2. Flashing top-right backup badge while backup is running.
3. Concise top-left toast on completion/failure/cancel with timing.

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

## Configuration

Config file: `com.aloncifer.simplebackup.cfg`

1. `BackupIntervalMinutes`: timed backup interval. `0` disables scheduler.
2. `MaxBackupsToKeep`: retained for legacy archive-index path compatibility.

## Build and Install

1. Build target: .NET Framework 4.6.2.
2. Build command: `dotnet build SimpleBackup.sln -c Release`.
3. Output: `src/bin/Release/net462/SimpleBackup.dll`.
4. Install DLL to BepInEx plugins folder.

## Notes

1. The backup badge uses IMGUI; icon glyph rendering can vary by system font.
2. Native restore/list rendering is still Valheim-owned UI behavior.
