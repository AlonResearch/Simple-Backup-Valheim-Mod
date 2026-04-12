NativeBackup - Valheim Backup Safety, Made Easy
Never lose progress again. Were you looking for an easy and reliable way to save and rollback your save? Or were you going to install those sketchy mods your friends made, and you are worried they'll corrupt your world?
We offer manual and automatic scheduled backups to protect your saves, integrated into the vanilla UI.
NativeBackup gives you a clean, in-game Backup button and commands that use the native Valheim backup and restore system, and protects both your character and your world with one click.
Important Safety Warning
Even on vanilla Valheim, never quit, log out, or close the game while a save or backup is in progress. 
Always wait until you see the top-left completion or fail message before exiting.
Why Players Use NativeBackup
- Instant peace of mind before risky fights, building sessions, or mod testing.
- Backup and keep playing with almost no game performance loss.
- Works for solo players, local hosts, and visitors without complicated setup.
What It Does
NativeBackup adds a Backup option in your Esc menu and runs a safe save-before-backup flow.
- One-click backup directly from the game menu.
- Smart target behavior:
  - Hosting locally: backs up world and character.
  - Playing as a guest: backs up your character.
- Optional automatic interval backups on the config file (default 0 min, turned off).
- Remove old backups automatically, as per config (default 5 most recent backups per save, configurable) (IN ALPHA TESTING).
Quick start
Vortex (Recommended)
1. Open Vortex and select your Valheim profile, log in to your Nexus account.
2. On the mods section, select "Download BeplnEx pack" (button location)
3. Install the NativeBackup package with Vortex at the top of this page.
4. Enable/deploy mods.
5. Launch Valheim, enter a world, press Esc, and click Backup.
Manual Quick Guide
1. Install BepInEx for Valheim (required mod loader).
2. Extract the NativeBackup package.
3. Copy NativeBackup.dll into Valheim/BepInEx/plugins/.
4. Launch Valheim, enter a world, press Esc, and click Backup.
That is it. You now have a current backup point.
Commands (Optional)
If you prefer console commands with more specific controls, NativeBackup includes:
- sb.backup (backup both current world and char)
- sb.backup world  (backup only current world)
- sb.backup char  (backup only current char)
- sb.list (List the last 15 backups)
Configuration
The mod generates a config file so you can tune behavior:
- BackupIntervalMinutes: automatic backup interval. Set 0 to disable. (Default 0)
- MaxBackupsPerSave: how many backups to keep per save. (Default 5)
Compatibility
It should be compatible with all mods, made with simplicity in mind, only triggering the native hooks for backup and save.
Compatible with Steam Cloud saves and local saves!
If something feels off, open an issue in the project GitHub repository with:
   - what you were doing,
   - what you expected,
   - What happened instead?
   - The BepInEx log with the events before and after the bug is very appreciated.
Clear reports help fix land fast.
NativeBackup is focused on one job: making backup protection easy enough that you actually use it. Enjoy :)
~Made by Aloncifer