using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleBackup
{
    public static class BackupCoordinator
    {
        private static int _backupInProgress;

        public static bool IsBackupInProgress => Volatile.Read(ref _backupInProgress) == 1;

        public static bool TryStartBackup(string targetWorld, string targetCharacter)
        {
            if (Interlocked.CompareExchange(ref _backupInProgress, 1, 0) != 0)
            {
                return false;
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
                    Volatile.Write(ref _backupInProgress, 0);
                }
            });

            return true;
        }
    }
}