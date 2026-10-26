using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reference subscriber to BeamVfxSystem.ImpactOccurred.
/// Listens to beam impact events and drives trauma camera shake and screen-space flash.
/// </summary>
[DisallowMultipleComponent]
public class BeamCameraFx : MonoBehaviour
{
    [SerializeField] private Camera shakeCamera;
    [SerializeField] private Image flashImage;          // full-screen overlay, raycastTarget = OFF, topmost canvas
    [SerializeField] private float traumaDecay = 1.6f;
    [SerializeField] private float shakeFrequency = 28f;

    private float trauma;
    private float traumaDurationLeft;
    private Vector3 cameraBaseLocal;
    private bool baseCached;
    private Coroutine flashRoutine;

    private void Awake()
    {
        if (shakeCamera == null)
            shakeCamera = GetComponent<Camera>() ?? Camera.main;
    }

    private void OnEnable() => BeamVfxSystem.ImpactOccurred += OnImpact;
    private void OnDisable()
    {
        BeamVfxSystem.ImpactOccurred -= OnImpact;
        RestoreCamera();
    }

    private void OnImpact(BeamImpact impact)
    {
        if (shakeCamera == null) return;
        AddTrauma(Mathf.Clamp01(impact.Config.ShakeAmplitude), impact.Config.ShakeDuration);
        RestartFlash(impact.Config.FlashStrength);
    }

    private void AddTrauma(float amount, float duration)
    {
        if (!baseCached)
        {
            cameraBaseLocal = shakeCamera.transform.localPosition;
            baseCached = true;
        }
        trauma = Mathf.Clamp01(trauma + amount * 2.5f);
        traumaDurationLeft = Mathf.Max(traumaDurationLeft, duration);
    }

    private void LateUpdate()
    {
        if (trauma <= 0f || shakeCamera == null) return;

        float t = Time.time * shakeFrequency;
        float amp = trauma * trauma;
        shakeCamera.transform.localPosition = cameraBaseLocal + new Vector3(
            (Mathf.PerlinNoise(t, 0.37f) * 2f - 1f) * amp,
            (Mathf.PerlinNoise(t, 11.9f) * 2f - 1f) * amp,
            (Mathf.PerlinNoise(t, 27.7f) * 2f - 1f) * amp * 0.4f);

        traumaDurationLeft -= Time.deltaTime;
        trauma = Mathf.Max(0f, trauma - traumaDecay * Time.deltaTime);
        if (trauma <= 0f || traumaDurationLeft <= 0f)
        {
            trauma = 0f;
            RestoreCamera();
        }
    }

    private void RestoreCamera()
    {
        if (shakeCamera != null && baseCached) shakeCamera.transform.localPosition = cameraBaseLocal;
    }

    private void RestartFlash(float strength)
    {
        if (flashImage == null || strength <= 0f) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(strength));
    }

    private IEnumerator FlashRoutine(float strength)
    {
        flashImage.gameObject.SetActive(true);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.16f;
            float a = Mathf.Pow(1f - Mathf.Clamp01(t), 2.5f) * strength;
            flashImage.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        flashImage.gameObject.SetActive(false);
        flashRoutine = null;
    }
}
