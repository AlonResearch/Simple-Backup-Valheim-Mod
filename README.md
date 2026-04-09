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
3. Your character and world files are instantly compressed securely into a `.zip` in the background.

---

## 📂 Where Are My Backups?

Your `.zip` archives are stored safely next to Valheim's standard save folders!
To find them, paste this into your Windows File Explorer address bar:
`%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\SimpleBackup`

*(Alternatively, navigate to `C:\Users\YOUR_NAME\AppData\LocalLow\IronGate\Valheim\SimpleBackup`)*

Inside this directory, they are sorted into subfolders such as `SteamCloud_characters` or `Local_worlds`.

---

## 🔄 How to Manually Restore a Backup

1. Navigate to the Valheim saves folder: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim`
2. Open the `characters` or `worlds` folder (or `remote`/`SteamCloud` equivalent) that you want to restore.
3. **Delete** or move your current broken files (e.g., `MyChar.fch`).
4. Find your compressed backup in `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\SimpleBackup`.
5. Open the `.zip` and simply drag and drop the files back into Valheim's save folder!

---

## 🗺️ Roadmap (Planned for Beta/0.0.2)
The following features are currently in development/experimental and will be fully verified in upcoming releases:

- **sb.list / sb.backup**: Unified console commands for advanced control.
- **sb.restore**: Secure in-game restoration system with `.old` safety mechanisms.
- **Auto-Backup Timer**: Configurable background timers for automatic protection.
- **Storage Retention**: Automatic deletion of old backups (currently unlimited in Alpha).

---

## 📥 Installation

**Option A: Mod Manager (Recommended)**
1. Open **r2modman** or **Thunderstore Mod Manager**.
2. Search for **SimpleBackup** by Aloncifer and click Install.
3. (Alternatively) Download the `.zip` from Thunderstore/Releases and drag-and-drop the entire `.zip` file into the mod manager's interface.

<details>
<summary><b>Option B: Manual Installation</b></summary>

1. Install **BepInExPack Valheim**.
2. Download `Aloncifer-SimpleBackup-0.0.1.zip` from the [Releases](https://github.com/AlonResearch/Simple-Backup-Valheim-Mod/releases) page.
3. Extract the `.zip` contents directly into your Valheim installation folder so that `SimpleBackup.dll` is placed inside `BepInEx/plugins`.
</details>

*Developed by Aloncifer.*
