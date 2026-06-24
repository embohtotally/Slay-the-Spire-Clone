using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gameseed26
{
    public class SfxSpreader : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private SfxID _clickSound;
        [SerializeField] private SfxID _hoverSound;
        [SerializeField] private List<Button> _excludeButtons;

        void Awake()
        {
            RegisterButtons();
        }

        public void PlayClickSound() => Tune.SFX(_clickSound);
        public void PlayHoverSound() => Tune.SFX(_hoverSound);

        private void RegisterButtons()
        {
            var buttons = GetAllButtons();
            if (buttons.Count > 0)
            {
                foreach (var button in buttons)
                {
                    button.gameObject.RegisterTrigger(EventTriggerType.PointerClick, OnButtonClick);
                    button.gameObject.RegisterTrigger(EventTriggerType.PointerEnter, OnButtonHover);
                }
            }
        }

        private void OnButtonClick(BaseEventData data) => PlayClickSound();
        private void OnButtonHover(BaseEventData data) => PlayHoverSound();

        private List<Button> GetAllButtons()
        {
            List<Button> targetButtons = new();
            Button[] buttonsArray = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var button in buttonsArray)
            {
                if (_excludeButtons.Contains(button)) continue;
                targetButtons.Add(button);
            }
            return targetButtons;
        }
    }
}
