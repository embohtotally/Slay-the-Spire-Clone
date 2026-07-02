using Gameseed26;
using NaughtyAttributes;
using UnityEngine;

[DisallowMultipleComponent]
public class SceneAudioPlayer : MonoBehaviour
{
    private enum PlayMoment
    {
        Awake,
        OnEnable,
        Start,
        ManualOnly
    }

    [Header("Music")]
    [SerializeField] private TuneMusicCue music;
    [SerializeField] private PlayMoment playMusicOn = PlayMoment.Start;
    [SerializeField] private bool stopMusicOnDisable;

    [Header("Optional SFX")]
    [SerializeField] private TuneSfxCue sceneEnterSfx;
    [SerializeField] private PlayMoment playSfxOn = PlayMoment.ManualOnly;

    private void Awake()
    {
        PlayIfNeeded(PlayMoment.Awake);
    }

    private void OnEnable()
    {
        PlayIfNeeded(PlayMoment.OnEnable);
    }

    private void Start()
    {
        PlayIfNeeded(PlayMoment.Start);
    }

    private void OnDisable()
    {
        if (stopMusicOnDisable)
        {
            Tune.StopMusicSafe();
        }
    }

    [Button("Play Music", EButtonEnableMode.Playmode)]
    public void PlayMusic()
    {
        music?.Play();
    }

    [Button("Play Scene Enter SFX", EButtonEnableMode.Playmode)]
    public void PlaySceneEnterSfx()
    {
        sceneEnterSfx?.Play(this, transform);
    }

    [Button("Stop Music", EButtonEnableMode.Playmode)]
    public void StopMusic()
    {
        Tune.StopMusicSafe();
    }

    private void PlayIfNeeded(PlayMoment moment)
    {
        if (playMusicOn == moment)
        {
            PlayMusic();
        }

        if (playSfxOn == moment)
        {
            PlaySceneEnterSfx();
        }
    }
}
