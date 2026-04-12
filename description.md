# NativeBackup - Valheim Backup Safety, Made Easy

Never lose progress again.
NativeBackup gives you a clean, in-game Backup button that feels native to Valheim and protects both your character and your world with one click.

A simple backup valheim mod.

## Important Safety Warning

Do not quit, log out, or close the game while a save or backup is in progress.
Always wait until you see the top-left completion or fail message before exiting.

## Why Players Use NativeBackup

- Instant peace of mind before risky fights, building sessions, or mod testing.
- Backup and keep playing with almost no interruption.
- Clean, minimal messages instead of chat spam.
- Works for solo players and local hosts without complicated setup.

## What It Does

NativeBackup adds a Backup option in your Esc menu and runs a safe save-before-backup flow.

- One-click backup directly from the game menu.
- Smart target behavior:
  - Hosting locally: backs up world and character.
  - Playing as a guest: backs up your character.
- Optional automatic interval backups.
- Built-in retention so old backups can be pruned automatically.

## Built For Real Gameplay

This mod is designed for practical, everyday use:

- Minimalist on-screen notifications.
- Visible backup activity indicator while work is running.
- Cooldown and single-flight protection to avoid duplicate backup jobs.
- Native-style save behavior before backup creation for fresher restore points.

## Installation

### Vortex (Recommended)

1. Install BepInEx for Valheim.
2. Open Vortex and select your Valheim profile.
3. Install the NativeBackup package with Vortex.
4. Enable/deploy mods.
5. Launch Valheim, enter a world, press Esc, and click Backup.

### Manual Quick Guide

1. Install BepInEx for Valheim.
2. Extract the NativeBackup package.
3. Copy NativeBackup.dll into Valheim/BepInEx/plugins/.
4. Launch Valheim, enter a world, press Esc, and click Backup.

That is it. You now have a current backup point.

## Commands (Optional)

If you prefer console control, NativeBackup includes:

- sb.backup
- sb.backup world
- sb.backup char
- sb.list

## Configuration

The mod generates a config file so you can tune behavior:

- BackupIntervalMinutes: automatic backup interval. Set 0 to disable.
- MaxBackupsPerSave: how many backups to keep per save.

## Who This Is For

- Players who want a simple backup safety net.
- Hosts who want quick pre-event snapshots.
- Modded players who test often and need reliable rollback points.

## Compatibility

- Valheim with BepInEx.
- Current NativeBackup release line: beta 0.1.1.

## Support And Feedback

If something feels off, open an issue in the project repository with:

- what you were doing,
- what you expected,
- what happened instead.

Clear reports help fixes land fast.

## Final Note

NativeBackup is focused on one job: making backup protection easy enough that you actually use it.
