using DG.Tweening;
using UnityEngine;

namespace PlushieOdyssey.Tweens
{
    /// <summary>Continuous local rotation for the loading icon or other UI decoration.</summary>
    public class RotationLoopTween : BaseTween
    {
        [SerializeField] private Vector3 _rotation = new(0f, 0f, 360f);

        protected override bool SupportsRelativeAndSpeedBased => false;

        protected override void ResetTweenSettings()
        {
            base.ResetTweenSettings();
            ConfigureBaseSettings(Ease.Linear, -1, independentUpdate: true);
        }

        protected override Tween CreateTween()
        {
            return Target.DOLocalRotate(Target.localEulerAngles + _rotation, Duration, RotateMode.FastBeyond360);
        }
    }
}
