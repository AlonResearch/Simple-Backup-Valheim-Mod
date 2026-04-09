# SimpleBackup for Valheim v0.0.1 Beta by Aloncifer

A robust, user-friendly, and open-source backup solution for Valheim that protects worlds and characters from file corruption. Perfect for modded playthroughs or just peace of mind.

This repository serves as the definitive source code and live documentation outlining exactly how `SimpleBackup` operates under the hood to ensure Valheim's stability.

> [!WARNING]
> **Compatibility Notice:** This mod was developed and tested exclusively on **Windows** using the **Steam version** of Valheim. Because the core backup engine actively relies on reading the Windows Registry to pinpoint Steam Cloud paths along with standard Windows `%AppData%` structures to locate local saves, **this mod currently will not work on Linux or the Steam Deck.**

## Features

- **Asynchronous Zipped Backups:** Safely compresses world and character saves into structured `.zip` files using background threads, preventing in-game lag spikes or micro-stutters.
- **Context-Aware Backup Logic:** Smartly evaluates your host state. If you are the Server Host or playing locally, both your Character and World are backed up. If you are a visiting Client on someone else's server, *only* your local Character profile is backed up.
- **Steam Cloud & Local Support:** Natively maps your Steam Installation to identify Cloud saves (`892970/remote`) and Local saves (`worlds_local`, `characters_local`, and legacy folders).
- **In-Game Backup Button:** Injects a "Backup" button perfectly into Valheim's `VerticalLayoutGroup` below the standard Esc Menu Save button.
- **Retention Limits:** Configurable limits (default 5) delete oldest zips automatically to prevent SSD bloat.
- **Safe '.old' Restoration Mechanism:** Restoring from the console guarantees no data is outright overridden; current files are temporarily renamed to `.old` before extraction.

---

## Technical Documentation & Architecture

For contributors, modders, or advanced users debugging issues, this section explains precisely how the mod integrates computationally with Valheim and Windows.

### 1. Path Resolution (`BackupManager.GetValheimSaveDirectories`)
Valheim save data is scattered depending on Cloud Sync settings and legacy updates (specifically the June 2022 patch). `SimpleBackup` sweeps all possibilities:
- **Steam Directory Search:** The mod reads the Windows Registry key `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam` (fallback to `SOFTWARE\Valve\Steam`) to dynamically locate the local Steam execution path. It then loops through `%SteamPath%\userdata\<All User IDs>\892970\remote` to index cloud saves regardless of which Steam account is logged in.
- **Local Application Data:** It probes `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\` scanning explicitly for `characters`, `characters_local`, `worlds`, and `worlds_local`.

### 2. Context-Aware Target Isolation
Rather than blinding compressing all files found in the save directories (which would lag the system), the Mod only targets what is actively being played:
- **Character Name:** Extracted via `Player.m_localPlayer.GetPlayerName()`.
- **World Name:** Extracted via `ZNet.instance.GetWorldName()`.

Crucially, **World Backups** are strictly bound to a Host check using `ZNet.instance.IsServer()`. If `IsServer()` evaluates to `false` (meaning the player is visiting a dedicated server), the World variable evaluates to `null` and `BackupManager` intelligently bypasses world extraction entirely.

### 3. Asynchronous Backup Engine
Zipping a 150MB+ Valheim `.db` file on Unity's primary thread would drop the game frame rate to 0, potentially causing network disconnects.
- `BackupManager.PerformFullBackup()` is always invoked via `System.Threading.Tasks.Task.Run()`. 
- File base-names are matched exactly. For instances like worlds (which utilize two file extensions: `.fwl` and `.db`), the engine safely groups them and bundles them both into a unified `[timestamp]-[WorldName].zip`.
- Backups are dumped into a safe custom directory (`%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\SimpleBackup`) preventing Steam Cloud from accidentally trying to upload massive .zip libraries.

### 4. UI Injection (`UIPatches.cs`)
- Hooks into `Menu.Start` using a `[HarmonyPostfix]`.
- Instead of using unstable floating coordinates, the mod clones the native `Settings` button game object via `GameObject.Instantiate`.
- **Silencing native triggers**: Standard `RemoveAllListeners()` only clears code-added triggers. To prevent the "Settings" menu from opening, the mod explicitly disables **Persistent Listeners** on the cloned button.
- **Positioning**: The mod finds the `"Save"` button transform and uses `SetSiblingIndex` to tuck the new "Backup" button perfectly beneath it. 
- Because Valheim's Esc Menu utilizes a `VerticalLayoutGroup`, Unity natively recalculates the spacing to flawlessly stack our button without conflicting with existing UI mods like Auga.

### 5. F5 Console Restoration (`RestoreCommand.cs`)
To prevent fatal read/write conflicts when attempting to restore files Unity is currently reading, the physical restoration logic is forcefully locked to the Main Menu.
- An injection via `Terminal.InitTerminal` adds the `backup_restore <name>` command.
- An immediate safety assertion evaluates `ZNet.instance == null` and `Player.m_localPlayer == null` before extracting.
- Instead of destroying corrupted saves, the active files residing in the Valheim Source Directory are appended with `.old` (e.g. `MyWorld.db.old`). The custom Zip entry is then safely inflated into the source directory.

---

## User Configuration (`com.aloncifer.simplebackup.cfg`)

Generated inside `Valheim/BepInEx/config/` automatically on the first boot:

| Variable | Default | Description |
|---|---|---|
| `BackupIntervalMinutes` | `0` | Background timer interval in minutes to trigger auto-backups. Set to `0` to completely disable the timer. |
| `MaxBackupsToKeep` | `5` | Limits the maximum amount of .zip backups stored per distinct character or world name. Older files are deleted. |

## Quick Restore Guide
1. Reboot to the Valheim Main Menu.
2. Press **F5** to open the developer console.
3. Type `backup_list` to see your tracked history.
4. Type `backup_restore <YourCharOrWorld>` to quickly unpack your archive.
5. In-game, you can also use `backup <char|world>` to quickly secure a specific state!
5. Launch the game normally! If you wish to revert to your corrupted save, navigate to your save directory and delete the restored files, and erase the `.old` extension from your backups.
