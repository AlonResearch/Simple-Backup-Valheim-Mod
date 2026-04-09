# SimpleBackup Mod: Quality Assurance & Testing Guide 🧪

Before uploading the mod to NexusMods or Thunderstore for thousands of users to download, it is highly recommended to run through this QA checklist to validate that no edge-cases crash the game!

## Phase 1: Installation & Setup
1. Open your project folder and run `.\build_release.ps1` to ensure you build the absolute latest version.
2. Navigate to your Valheim game directory (usually `C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins\`).
3. If BepInEx is installed, drag and drop the `SimpleBackup.dll` (located in `src\bin\Release\net462\`) directly into the `plugins` folder.
4. Launch Valheim. You should see BepInEx load up, and the console window should quickly mention `[Info   :SimpleBackup] SimpleBackup v0.0.1 loaded!`.

## Phase 2: In-Game UI Validation
1. Create a **throwaway Test World** and a **Test Character** specifically for validation. Do not test on your main 400-hour server yet!
2. Once loaded into the world, hit **Escape** to bring up the main menu.
3. Validate: Is the **"Backup"** button cleanly injected below the "Save" button? 
4. Click the "Backup" button. 
5. Validate: Does the golden text `"Session Backup Started in Background!"` smoothly appear in the center of the screen?

## Phase 3: Console Commands Validation
1. While still loaded in the test world, hit `F5` to open the terminal.
2. Type `backup char` and hit Enter.
   - Validate: The console says "Starting background backup for character: [YourName]..."
3. Type `backup world` and hit Enter.
   - Validate: The console says "Starting background backup for world: [WorldName]..."
4. Type `backup_list` and hit Enter.
   - Validate: The console prints a clean list containing the zip files that were just generated.

## Phase 4: Output Verification
1. Press `Win + R` on your keyboard, type `%LOCALAPPDATA%Low\IronGate\Valheim\SimpleBackup\` and hit Enter.
2. Validate: Are there folders dynamically created (e.g. `Local_worlds` or `SteamCloud_characters`)?
3. Validate: Open one of the `.zip` files generated. 
   - Does a character zip actually contain the `.fch` file?
   - Does a world zip actually contain the `.db` and `.fwl` files? Ensure it isn't an empty zip file!

## Phase 5: Restoration (Destructive Testing)
> [!WARNING]
> This is testing the catastrophic recovery mechanism!

1. Build a campfire, drop an item on the floor, and hit the **Backup** button. Wait 5 seconds.
2. Destroy the campfire, pick up the item. 
3. Disconnect from the server and go to the **Main Menu**.
4. Press `F5` and type `backup_restore [NameOfTestWorld]`.
   - Validate: The console should say "Restore complete!".
5. Log back into the test world.
   - Validate: Is your campfire back exactly where it was before you destroyed it? Is the item back on the floor?

---
*If you can check all of these boxes, the mod is 100% airtight and ready for the masses!*
