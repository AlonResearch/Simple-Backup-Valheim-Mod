# 🛡️ SimpleBackup: Technical Ground Truth

**SimpleBackup** is a performance-first, host-aware backup engine for Valheim. This document serves as the definitive technical specification and operational logic for the mod, intended as the primary source of truth for maintainers and automated agents.

---

## 🏗️ Core Architecture

SimpleBackup operates as a **BepInEx 5** plugin using **Harmony** for runtime UI injection. It is designed to be completely decoupled from large modding frameworks like Jotunn to ensure maximum compatibility and zero dependency bloat.

### 1. Context-Aware Target Logic
The mod intelligently isolates backup targets based on the current game state:
- **Host Check**: Determined via `ZNet.instance.IsServer()`.
- **Target Extraction**:
    - **World Name**: `ZNet.instance.GetWorldName()` (only if Host).
    - **Character Name**: `Player.m_localPlayer.GetPlayerName()`.
- **Ground Truth**: If the player is a client on a dedicated server, `PerformFullBackup` is invoked with a `null` world parameter, ensuring only the local character profile is secured.

### 2. File Topology & Path Resolution
Valheim save data is highly fragmented due to Steam Cloud and legacy updates. SimpleBackup sweeps three distinct layers:
- **Registry Engine (Windows Only)**: Accesses `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam` to locate the local Steam installation. It then iterates through all `userdata` folders to find AppID `892970` (Valheim) remote saves.
- **LocalLow Engine**: Probes `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\` for `worlds`, `worlds_local`, `characters`, and `characters_local`.
- **Ground Truth**: Backups are archived in a dedicated central directory: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\SimpleBackup`. This directory is outside the standard save paths to prevent Steam Cloud from attempting to sync large `.zip` archives.

### 3. Concurrency & Main-Thread Safety
Unity is single-threaded for most engine operations. SimpleBackup enforces strict concurrency rules to prevent game lag:
- **Asynchronous I/O**: All compression and disk operations (`ZipFile.CreateEntryFromFile`) MUST run via `System.Threading.Tasks.Task.Run()`.
- **Cross-Thread UI Messaging**: Since background tasks cannot touch Unity UI (like `MessageHud`), SimpleBackup utilizes a `ConcurrentQueue<string>` system.
    - Background tasks push messages to the queue.
    - The `Update()` loop on the main thread polls `TryDequeue` and safely invokes `MessageHud.instance.ShowMessage`.

### 4. Console System & Resilience
To ensure commands are always available even when other mods (like Jotunn) attempt to wipe the terminal dictionary:
- **Reflection Injection**: Uses `System.Reflection` to access the private `Terminal.commands` dictionary.
- **Failsafe Registration**: The `Update()` loop performs a heartbeat check every 2.0 seconds. If `sb.backup` is missing from the dictionary, the mod force-re-registers all `sb.*` commands.
- **Restoration Safety**: `sb.restore` is hard-locked to the **Main Menu** (`ZNet.instance == null`) to prevent fatal race conditions while game databases are open.

### 5. Data Integrity: The `.old` Strategy
During restoration, SimpleBackup never immediately overwrites active saves:
1. Current `.db`/`.fwl`/`.fch` files are renamed with a `.old` suffix.
2. The archive is extracted cleanly into the source directory.
3. This ensures that even a failed restore does not result in data loss.

---

## 🛠️ Ground Truth Feature Set

| Feature | Implementation Detail |
| :--- | :--- |
| **Manual Trigger** | Cloned "Settings" button in Esc Menu, positioned via `SetSiblingIndex`. |
| **Auto-Backup** | Background timer configurable via `.cfg`. |
| **Metrics** | Tracks total bytes and duration using `Stopwatch` and `FileInfo`. |
| **Retention** | `MaxBackupsToKeep` config determines how many zips are kept per character/world. |
| **Commands** | `sb.backup` (manual sync), `sb.list` (history), `sb.restore` (safety extraction). |

---

## 📥 Installation Logic
- **Mod Manager**: Primary channel. The `.zip` contains a standard `manifest.json`, `icon.png`, and the `plugins/SimpleBackup.dll`.
- **Manual**: Direct drop of `SimpleBackup.dll` into `BepInEx/plugins`.

*Developed by Aloncifer.*
