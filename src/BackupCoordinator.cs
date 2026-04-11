using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleBackup
{
    public static class BackupCoordinator
    {
        public enum BackupStartResult
        {
            Started,
            AlreadyRunning,
            CooldownActive
        }

        private const int MinimumBackupIntervalSeconds = 5;
        private static readonly object _backupGate = new object();
        private static int _backupInProgress;
        private static long _lastBackupStartTicks;

        public static bool IsBackupInProgress => Volatile.Read(ref _backupInProgress) == 1;
        public static bool IsCooldownActive => IsWithinCooldown(DateTime.UtcNow.Ticks);

        public static BackupStartResult TryStartBackup(string targetWorld, string targetCharacter)
        {
            long nowTicks = DateTime.UtcNow.Ticks;

            lock (_backupGate)
            {
                if (Volatile.Read(ref _backupInProgress) == 1)
                {
                    return BackupStartResult.AlreadyRunning;
                }

                if (IsWithinCooldown(nowTicks))
                {
                    return BackupStartResult.CooldownActive;
                }

                _backupInProgress = 1;
                _lastBackupStartTicks = nowTicks;
            }

            Task.Run(() =>
            {
                try
                {
                    BackupManager.PerformFullBackup(targetWorld, targetCharacter);
                }
                catch (Exception ex)
                {
                    SimpleBackupPlugin.Log?.LogError(ex);
                    SimpleBackupPlugin.QueueUIMessage("Backup failed unexpectedly.");
                }
                finally
                {
                    lock (_backupGate)
                    {
                        Volatile.Write(ref _backupInProgress, 0);
                    }
                }
            });

            return BackupStartResult.Started;
        }

        private static bool IsWithinCooldown(long nowTicks)
        {
            long lastStartTicks = Volatile.Read(ref _lastBackupStartTicks);
            if (lastStartTicks == 0)
            {
                return false;
            }

            long elapsedTicks = nowTicks - lastStartTicks;
            return elapsedTicks < TimeSpan.FromSeconds(MinimumBackupIntervalSeconds).Ticks;
        }
    }
}