// Confirm Steam writes before reporting success or accepting another operation.
namespace SAM.Game
{
    internal partial class Manager
    {
        private API.Callbacks.UserStatsStored _StoredCallback;
        private void InitializeScheduler()
        {
            if (_Demo) return;
            _StoredCallback = _SteamClient.CreateAndRegisterCallback<API.Callbacks.UserStatsStored>();
            _StoredCallback.OnRun += OnQueueStored;
        }
        private void OnQueueStored(API.Types.UserStatsStored result)
        {
            if (result.GameId != (ulong)this._GameId) return;
            if (this.OnCartStored(result)) return;
            if (this._ManualSaving)
            {
                this._ManualSaving = false;
                this._LastStoreMessage = result.Result == 1
                    ? $"Steam a confirmé {_ManualAchievementCount} succès et {_ManualStatCount} statistiques."
                    : $"Steam a refusé l’enregistrement (erreur {result.Result}).";
                this.RefreshStats();
                return;
            }
        }
    }
}
