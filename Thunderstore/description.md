<div align="center">
  <h1>🛡️ SimpleBackup 0.0.1 Beta</h1>
  <b>Back up your worlds & characters without affecting your game performance!</b>
</div>

**Native Major Version:** Valheim 0.217+  
**Source Code:** [GitHub Repository](https://github.com/AlonResearch/Simple-Backup-Valheim-Mod)  
**Donation Link:** [PayPal / Ko-fi Placeholder]  
**Discord Server:** [Discord Placeholder]  

________________________________________________

### 🚀 OVERVIEW: Dynamic Host-Aware Backups
First of all, you can save massive amounts of disk space! SimpleBackup is designed intelligently: If you are hosting a local world, it detects this natively and secures both your World and Character. If you are just a client visiting someone else's server, it automatically calculates the connection and *only* secures your local Character profile! 

________________________________________________

### ✨ Features:
* **no performance impact:** this plugin runs every `.zip` compression natively in asynchronous threaded mode, so you won't notice any impact on your framerate
* **1-click UI integration:** an easily accessible "Backup" button perfectly tucked beneath the "Save" button inside the Valheim Esc Menu
* **automatic Backup creation:** set configurable timers to run quietly in the background (e.g. every 30 minutes)
* **storage retention control:** automatic deletion of old backups to save storage space (default: automatically limits to your youngest 5 files)
* **safe '.old' restoration guard:** never corrupt your game! SimpleBackup guarantees absolute safety by temporarily appending your currently active (potentially corrupted) world files with `.old` before securely unpacking your historic backup zip
* **steam cloud & local friendly:** seamlessly scans your Windows Registry keys to track Steam Cloud remote folders (`892970/remote`) and Valheim's modern 2022 `worlds_local` directories automatically without any messy configuration

________________________________________________

### ⚙️ Commands (F5 Console):
Because forcefully overwriting heavily compressed `.fwl` and `.db` files while Valheim's engine is actively rendering them will instantly crash the game, SimpleBackup intelligently forces restoration queries from the absolute safety of the Main Menu F5 Console!

> **`sb.list`** - shows a clean horizontal list of all currently available zip backups your system has secured. 

> **`sb.backup [char|world]`** - in-game command to manually trigger a zip compression specifically for your current character (`sb.backup char`) or world (`sb.backup world`). No arguments backs up both.

> **`sb.restore <name>`** - securely renames your active target files to `.old` to prevent data-loss, and magically unpacks the ZIP exactly back into the Valheim root directories! *(Example: `sb.restore MyWorld`)*

________________________________________________

### 🔧 Config:
This plugin generates a BepInEx config (`com.aloncifer.simplebackup.cfg`) where you can heavily tailor your experience.

**Explanation of the config:**
* **BackupIntervalMinutes** - the time (in minutes) between automatic background backups. Set to `0` to disable auto-backup entirely. Default: `0`
* **MaxBackupsToKeep** - the number of total Backups you want to keep per distinct profile. If you want to keep only the 5 latest backups you must set this to `5`. Older backups are permanently deleted to save storage space! Default: `5`

<details>
<summary><b>Click to see the literal config generation!</b></summary>

```ini
[General]
# Time in minutes between automatic background backups. Set to 0 to disable auto-backup entirely.
BackupIntervalMinutes = 0

# Maximum amount of .zip files retained per distinct world / character. Older files are deleted safely.
MaxBackupsToKeep = 5
```
</details>

________________________________________________

### 📥 Installation 

1. Ensure you have the core framework **BepInExPack Valheim** installed.
2. Download the `Aloncifer-SimpleBackup-0.0.1.zip` file.
3. Open **r2modman** (or Thunderstore Mod Manager) and your Valheim profile.
4. Go to **Settings** -> **Import local mod** and select the `.zip` file.
Alternatively, you can manually drop `SimpleBackup.dll` into your `Valheim/BepInEx/plugins/` folder.
4. Boot Valheim once to automatically render the configuration file!

________________________________________________

*Created with native performance in mind by Aloncifer.*
