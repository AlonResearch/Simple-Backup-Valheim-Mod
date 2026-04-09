# TESTING GUIDE: SimpleBackup 0.0.1 ALPHA MVP

Focus: Validating the core Manual Backup Button functionality.

## Phase 1: Environment Setup
- BepInEx installed.
- SimpleBackup.dll in plugins.

## Phase 2: UI Validation
1. Launch Valheim.
2. Load any world.
3. Press **Esc**.
4. **Target**: Verify the orange **BACKUP** button appears below the **SAVE** button.

## Phase 3: Backup Execution
1. Click the **BACKUP** button.
2. Observe the orange text overlay: "Session Backup Started in Background!"
3. Observe the console/log: "Manual UI Backup Triggered!"

## Phase 4: Output Verification
1. Open your world save folder (or `BepInEx/plugins/SimpleBackup/Backups`).
2. Verify a new `.zip` file exists with the current timestamp.
3. Open the `.zip` and verify it contains your `.fwl`, `.db`, or `.fch` files.
