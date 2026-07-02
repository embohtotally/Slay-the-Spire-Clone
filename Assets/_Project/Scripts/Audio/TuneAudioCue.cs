using System;
using Gameseed26;
using NaughtyAttributes;
using UnityEngine;

[Serializable]
public class TuneSfxCue
{
    [Tooltip("Use this when the sound already exists in generated AudioEnums.cs.")]
    [SerializeField] private SfxID sfxId = SfxID.None;

    [Tooltip("Optional fallback for new sounds before regenerating AudioEnums.cs. Must match the TuneClipsSO asset name after spaces/hyphens become underscores.")]
    [SerializeField] private string sfxName;

    [Tooltip("If true and a target is passed, the pooled SFX source follows that transform.")]
    [SerializeField] private bool followTarget;

    [Tooltip("Small delay for layered UI/combat sound design. 0 = play immediately.")]
    [Min(0f)][SerializeField] private float delay;

    public bool HasCue => sfxId != SfxID.None || !string.IsNullOrWhiteSpace(sfxName);

    public void Play(MonoBehaviour runner = null, Transform target = null)
    {
        if (!HasCue) return;

        if (delay > 0f && runner != null && runner.isActiveAndEnabled)
        {
            runner.StartCoroutine(PlayAfterDelay(target));
            return;
        }

        PlayNow(target);
    }

    private System.Collections.IEnumerator PlayAfterDelay(Transform target)
    {
        yield return new WaitForSeconds(delay);
        PlayNow(target);
    }

    private void PlayNow(Transform target)
    {
        if (followTarget && target != null)
        {
            if (sfxId != SfxID.None) Tune.SFXFollow(sfxId, target);
            else Tune.SFXFollow(sfxName, target);
            return;
        }

        Vector2 position = target != null ? (Vector2)target.position : default;
        if (sfxId != SfxID.None) Tune.SFX(sfxId, position);
        else Tune.SFX(sfxName, position);
    }
}

public enum TuneMusicPlayMode
{
    Normal,
    Fade,
    CrossFade
}

[Serializable]
public class TuneMusicCue
{
    [SerializeField] private MusicID musicId = MusicID.None;
    [Tooltip("Optional fallback for new music before regenerating AudioEnums.cs. Must match the TuneTracksSO asset name after spaces/hyphens become underscores.")]
    [SerializeField] private string musicName;
    [SerializeField] private TuneMusicPlayMode playMode = TuneMusicPlayMode.Fade;
    [Min(0f)][SerializeField] private float fadeDuration = 1f;
    [Min(0)][SerializeField] private int variation;

    public bool HasCue => musicId != MusicID.None || !string.IsNullOrWhiteSpace(musicName);

    public void Play()
    {
        if (!HasCue) return;

        if (musicId != MusicID.None)
        {
            PlayById();
            return;
        }

        PlayByName();
    }

    public void Stop()
    {
        Tune.StopMusicSafe();
    }

    private void PlayById()
    {
        switch (playMode)
        {
            case TuneMusicPlayMode.Normal:
                Tune.Music(musicId, variation);
                break;
            case TuneMusicPlayMode.CrossFade:
                Tune.MusicCrossFade(musicId, variation, fadeDuration);
                break;
            default:
                Tune.MusicFade(musicId, variation, fadeDuration);
                break;
        }
    }

    private void PlayByName()
    {
        switch (playMode)
        {
            case TuneMusicPlayMode.Normal:
                Tune.Music(musicName, variation);
                break;
            case TuneMusicPlayMode.CrossFade:
                // Tune currently has no string overload for crossfade, so use fade as the safe string fallback.
                Tune.MusicFade(musicName, variation, fadeDuration);
                break;
            default:
                Tune.MusicFade(musicName, variation, fadeDuration);
                break;
        }
    }
}

[DisallowMultipleComponent]
public class TuneEventPlayer : MonoBehaviour
{
    [Header("SFX")]
    [SerializeField] private TuneSfxCue sfx;

    [Header("Music")]
    [SerializeField] private TuneMusicCue music;

    [Button("Play SFX", EButtonEnableMode.Playmode)]
    public void PlaySfx()
    {
        sfx?.Play(this, transform);
    }

    [Button("Play Music", EButtonEnableMode.Playmode)]
    public void PlayMusic()
    {
        music?.Play();
    }

    [Button("Stop Music", EButtonEnableMode.Playmode)]
    public void StopMusic()
    {
        music?.Stop();
    }

    public void StopAllAudio()
    {
        Tune.StopAllAudioSafe();
    }
}
