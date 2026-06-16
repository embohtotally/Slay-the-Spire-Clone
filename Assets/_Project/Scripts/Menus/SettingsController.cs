using System;
using System.Collections.Generic;
using Gameseed26;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Gameseed26
{
    [Serializable]
    public class AudioSliderData
    {
        public string Name;
        public Slider SliderRef;
    }

    public class SettingsController : MonoBehaviour
    {
        [SerializeField] private AudioMixer _mainMixer;
        [SerializeField] private List<AudioSliderData> _audioSliders;
        [SerializeField] private SfxID _slidingSound = SfxID.None;
        [SerializeField] private float _soundDebounce = 0.1f;

        static float _soundTimer;

        private void Start()
        {
            LoadAudioWithoutNotify(_audioSliders);
        }

        private void Update()
        {
            if (_soundTimer > 0)
            {
                _soundTimer -= Time.deltaTime;
            }
        }

        private void OnEnable()
        {
            BindAudioSliders(_audioSliders);
        }

        private void OnDisable()
        {
            UnbindAudioSliders(_audioSliders);
        }

        public void LoadAudioWithoutNotify() => LoadAudioWithoutNotify(_audioSliders);

        private void BindAudioSliders(List<AudioSliderData> sliders)
        {
            if (sliders.Count == 0) return;

            sliders.ForEach(
                s => s.SliderRef.onValueChanged.AddListener(
                    value =>
                    {
                        var dbValue = FloatToDb(value);
                        _mainMixer.SetFloat(s.Name, dbValue);

                        PlayerPrefs.SetFloat(GetPrefKey(s.Name), value);

                        if (_soundTimer <= 0f)
                        {
                            Tune.Instance.PlaySFX(_slidingSound);
                            _soundTimer = _soundDebounce;
                        }
                    }
                )
            );
        }

        private void UnbindAudioSliders(List<AudioSliderData> sliders)
        {
            if (sliders.Count == 0) return;

            _audioSliders.ForEach(s => s.SliderRef.onValueChanged.RemoveAllListeners());
        }

        private void LoadAudioWithoutNotify(List<AudioSliderData> sliders)
        {
            if (sliders.Count == 0) return;

            sliders.ForEach(s =>
            {
                var value = PlayerPrefs.GetFloat(GetPrefKey(s.Name), 0.5f);
                s.SliderRef.SetValueWithoutNotify(value);
            });
        }

        float FloatToDb(float value) => (value > 0.0001f) ? 20f * Mathf.Log10(value) : -80f;
        string GetPrefKey(string name) => $"{name}_Prefs";
    }
}
