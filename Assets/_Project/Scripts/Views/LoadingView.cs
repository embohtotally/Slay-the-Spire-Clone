using TMPro;
using UnityEngine;

namespace Gameseed26
{
    public class LoadingView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _loadingText;

        public void SetLoadingProgress(float value)
        {
            _loadingText.text = $"{Mathf.Clamp01(value) * 100f:0}%"; ;
        }
    }
}
