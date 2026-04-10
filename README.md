# SimpleBackup

SimpleBackup is a Valheim backup plugin that provides fast in-game backup triggers while using the game's native backup backend and native save ecosystem.

## Current State

SimpleBackup runs on BepInEx 5 and Harmony and currently provides:

1. Esc-menu backup button integrated into the existing pause menu.
2. Console backup commands for targeted and combined backups.
3. Automatic timed backups through plugin config.
4. Native backup generation through Valheim save APIs.
5. Main-thread-safe status messaging for user feedback.

## Active Backup Flow

Every backup trigger (button, command, timer) goes through one coordinated flow:

1. Determine active targets from game state.
2. Start a single backup job through the coordinator gate.
3. Call Valheim native backup creation per target.
4. Report completion to UI and console.

Target behavior:

1. Hosted session: world + character can be backed up.
2. Client session: character backup is available.
3. Explicit command mode supports world-only or character-only targeting.

## Native Compatibility Direction

Project direction is native-first backup compatibility:

1. Keep backup UX entry points in this mod.
2. Keep generated backups compatible with Valheim's native backup/restore system.
3. Use the game's existing save management surface as the primary restore/listing experience.

This keeps backup creation accessible while aligning storage and restore behavior with Valheim-native conventions.

## Runtime Architecture

1. Plugin lifecycle: BepInEx plugin with Harmony patch registration.
2. Backup execution: asynchronous jobs guarded by a single-flight coordinator and cooldown.
3. Messaging: cross-thread queue consumed on Update for safe in-game notifications.
4. Save awareness: world/character target resolution uses current ZNet and player state.

## Commands and UX

Available command surface:

1. sb.backup
2. sb.backup world
3. sb.backup char
4. sb.list

Primary in-game UX:

1. Backup button in Esc menu.
2. Top-left completion and status messages.

## Configuration

Config file: com.aloncifer.simplebackup.cfg

1. BackupIntervalMinutes: minutes between automatic backups. 0 disables timer.
2. MaxBackupsToKeep: retention setting currently used by legacy archive indexing paths.

## Build and Install

1. Build target: .NET Framework 4.6.2.
2. Output: SimpleBackup.dll for BepInEx plugins folder.
3. Packaging: Thunderstore-ready layout with plugin DLL in plugins.

## Project Direction Summary

SimpleBackup is evolving into a native-compatible backup frontend: quick triggers, deterministic target selection, and backup outputs that integrate seamlessly with Valheim's own save and restore model.
