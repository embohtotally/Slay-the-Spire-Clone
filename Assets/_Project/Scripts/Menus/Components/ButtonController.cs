using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gameseed26
{
    public class ButtonController : MonoBehaviour
    {
        [Header("On Hover")]
        [SerializeField] private GameObject hoveringObject;
        [SerializeField] private SfxID onHoverSound;

        [Header("On Click")]
        [SerializeField] private SfxID onClickSound;

        private Button button;

        private void Awake()
        {
            button = gameObject.GetOrAdd<Button>();
        }

        private void OnEnable()
        {
            if (hoveringObject == null) return;

            gameObject.RegisterTrigger(EventTriggerType.PointerEnter, HoverOn);
            gameObject.RegisterTrigger(EventTriggerType.PointerExit, HoverOff);
            gameObject.RegisterTrigger(EventTriggerType.Select, HoverOn);
            gameObject.RegisterTrigger(EventTriggerType.Deselect, HoverOff);

            button.onClick.AddListener(OnClick);
        }

        void OnDisable()
        {
            if (hoveringObject == null) return;

            gameObject.DeregisterTrigger(EventTriggerType.PointerEnter, HoverOn);
            gameObject.DeregisterTrigger(EventTriggerType.PointerExit, HoverOff);
            gameObject.DeregisterTrigger(EventTriggerType.Select, HoverOn);
            gameObject.DeregisterTrigger(EventTriggerType.Deselect, HoverOff);

            button.onClick.RemoveListener(OnClick);
        }

        private void HoverOn(BaseEventData data)
        {
            Tune.SFX(onHoverSound);
            hoveringObject.SetActive(true);
        }

        private void HoverOff(BaseEventData data)
        {
            hoveringObject.SetActive(false);
        }

        private void OnClick()
        {
            Tune.SFX(onClickSound);
        }
    }
}
