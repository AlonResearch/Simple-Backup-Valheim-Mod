# 🛡️ SimpleBackup 0.0.1 ALPHA MVP

**SimpleBackup** is a performance-first, host-aware backup tool for Valheim. This version is an Alpha MVP focusing on providing a rock-solid, 1-click manual backup solution that doesn't lag your game.

---

## 🚀 Version 0.0.1 Alpha: Stable MVP
This initial release focuses on the core user experience: **The Manual Backup Button.**

- **[STABLE] 1-Click UI Integration**: A "Backup" button perfectly tucked beneath the "Save" button in the Esc Menu.
- **[STABLE] Async Compression**: Zero lag. Backups run on background threads so your framerate stays smooth.
- **[STABLE] Smart Targeting**: Automatically detects if you are a host (World + Character) or a guest (Character only) and secures the appropriate files.
- **[STABLE] Steam Cloud & Local Support**: Automatically tracks your save file locations regardless of where they are stored on your disk.

---

## ⚙️ How it Works
1. Press **Esc** while playing.
2. Click the new **Backup** button.
3. Your character and world files are instantly compressed into a `.zip` in your `BepInEx/plugins/SimpleBackup/Backups` folder.

---

## 🗺️ Roadmap (Planned for Beta/0.0.2)
The following features are currently in development/experimental and will be fully verified in upcoming releases:

- **sb.list / sb.backup**: Unified console commands for advanced control.
- **sb.restore**: Secure in-game restoration system with `.old` safety mechanisms.
- **Auto-Backup Timer**: Configurable background timers for automatic protection.
- **Storage Retention**: Automatic deletion of old backups (currently unlimited in Alpha).

---

## 📥 Installation
1. Install **BepInExPack Valheim**.
2. Download `Aloncifer-SimpleBackup-0.0.1.zip` from the [Releases](https://github.com/AlonResearch/Simple-Backup-Valheim-Mod/releases) page.
3. drag and drop the `.dll` into `BepInEx/plugins`.

*Developed by Aloncifer.*
