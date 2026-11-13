using PlushieOdyssey.Core;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.Run;
using UnityEngine;

namespace PlushieOdyssey.Map.Manual
{
    public class ManualMapRunStarter : MonoBehaviour
    {
        [SerializeField] private ManualMapLayoutRegistry layoutRegistry;
        [SerializeField] private string mapSceneName = "Map";
        [SerializeField] private bool clearManualMapProgress = true;
        [SerializeField] private bool abandonExistingRun = true;

        public void StartRandomManualMapRun()
        {
            EnsureSelectionManagerExists();
            ResetRunStateIfNeeded();

            ManualMapLayoutEntry layout = ManualMapRunSelection.Instance.SelectRandomLayout(layoutRegistry);
            if (layout == null) return;

            LoadMapScene();
        }

        public void StartManualMapRunWithLayout(string layoutId)
        {
            EnsureSelectionManagerExists();
            if (layoutRegistry == null)
            {
                global::PlushieOdyssey.Core.Logger.LogWarning("Cannot start manual map run because layout registry is missing.");
                return;
            }

            ManualMapLayoutEntry layout = layoutRegistry.GetById(layoutId);
            if (layout == null)
            {
                global::PlushieOdyssey.Core.Logger.LogWarning($"Cannot start manual map run because layout '{layoutId}' was not found.");
                return;
            }

            ResetRunStateIfNeeded();
            ManualMapRunSelection.Instance.SelectLayout(layout.SafeId);
            LoadMapScene();
        }

        public void ClearManualMapSelectionAndProgress()
        {
            EnsureSelectionManagerExists();
            ManualMapRunSelection.Instance.ClearSelection();
            ManualMapController.ClearAllSavedStates();
        }

        private void ResetRunStateIfNeeded()
        {
            if (clearManualMapProgress)
            {
                ManualMapController.ClearAllSavedStates();
            }

            if (ManualMapRunSelection.Instance != null)
            {
                ManualMapRunSelection.Instance.ClearSelection();
            }

            if (abandonExistingRun && RunManager.Instance != null)
            {
                RunManager.Instance.AbandonRun();
            }
        }

        private void LoadMapScene()
        {
            if (string.IsNullOrWhiteSpace(mapSceneName))
            {
                global::PlushieOdyssey.Core.Logger.LogWarning("Cannot load map scene because map scene name is empty.");
                return;
            }

            SceneLoader.LoadScene(mapSceneName);
        }

        private static void EnsureSelectionManagerExists()
        {
            if (ManualMapRunSelection.Instance != null) return;

            GameObject selectionObject = new("Manual Map Run Selection");
            selectionObject.AddComponent<ManualMapRunSelection>();
        }
    }
}
