<div align="center">
  <h1>🛡️ SimpleBackup 0.0.1 ALPHA MVP</h1>
  <b>The rock-solid 1-click backup solution for Valheim!</b>
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
* **[STABLE] Async Threading:** Zero performance impact. Backups run quietly in the background.
* **[STABLE] 1-Click UI:** A "Backup" button perfectly integrated into your Esc Menu.
* **[STABLE] Smart Targeting:** Automatically backs up both World/Character if you are hosting, or only Character if you are a guest.
* **[ROADMAP] Unified Commands:** `sb.list` and `sb.backup` for advanced control (Experimental).
* **[ROADMAP] Safe Restoration:** `sb.restore` logic with `.old` guard (Experimental).

________________________________________________

### ⚙️ How to use:
1. Open the **Esc Menu** while playing.
2. Click **Backup**.
3. Check your `BepInEx/plugins/SimpleBackup/Backups` folder for your new `.zip`!

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
