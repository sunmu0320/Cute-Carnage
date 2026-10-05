using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Blends sun, fog and the night post-process Volume between Day and Night presets on
/// GameManager.OnPhaseChanged (single-shot coroutine, no per-frame polling). Day look = whatever the
/// scene has at Awake; fog is off by day and fades in over the whole map at Night. Night darkness comes
/// from the Volume's exposure, so the scene's Skybox ambient stays untouched. GameOver keeps the
/// current look; RetryCurrentDay fires Day, which fades back.</summary>
public class PhaseLighting : MonoBehaviour
{
    [SerializeField] private Light sun;
    [SerializeField] private Volume nightVolume;
    [SerializeField] private ParticleSystem nightMist; // child of PlayerRoot, world-space sim: spawns only around the view
    [SerializeField, Min(0.01f)] private float blendSeconds = 3f;

    [Header("Night preset")]
    [SerializeField] private Color nightSunColor = new Color(0.55f, 0.65f, 1f);
    [SerializeField] private float nightSunIntensity = 0.35f;
    [SerializeField] private Color nightFogColor = new Color(0.36f, 0.42f, 0.52f);
    [SerializeField] private float nightFogDensity = 0.012f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Color daySunColor;
    private float daySunIntensity;
    private Coroutine blend;
    private float t; // 0 = day, 1 = night
    private MaterialPropertyBlock mistBlock;
    private Color mistBaseColor;

    private void Awake()
    {
        if (sun == null) sun = RenderSettings.sun;
        daySunColor = sun != null ? sun.color : Color.white;
        daySunIntensity = sun != null ? sun.intensity : 1f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        if (nightMist != null)
        {
            mistBlock = new MaterialPropertyBlock();
            mistBaseColor = nightMist.GetComponent<ParticleSystemRenderer>().sharedMaterial.GetColor(BaseColorId);
        }
        Apply(0f);
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged(GameManager.GamePhase phase)
    {
        if (phase == GameManager.GamePhase.GameOver) return;
        if (blend != null) StopCoroutine(blend);
        blend = StartCoroutine(BlendTo(phase == GameManager.GamePhase.Night ? 1f : 0f));
    }

    private IEnumerator BlendTo(float target)
    {
        float start = t;
        for (float e = 0f; e < blendSeconds; e += Time.deltaTime)
        {
            Apply(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, e / blendSeconds)));
            yield return null;
        }
        Apply(target);
        blend = null;
    }

    private void Apply(float k)
    {
        t = k;
        if (sun != null)
        {
            sun.color = Color.Lerp(daySunColor, nightSunColor, k);
            sun.intensity = Mathf.Lerp(daySunIntensity, nightSunIntensity, k);
        }
        RenderSettings.fog = k > 0.001f;
        RenderSettings.fogColor = nightFogColor;
        RenderSettings.fogDensity = nightFogDensity * k;
        if (nightVolume != null) nightVolume.weight = k;
        if (nightMist != null)
        {
            // Fade every live puff via the renderer's property block (never touches the material asset).
            var c = mistBaseColor; c.a *= k;
            mistBlock.SetColor(BaseColorId, c);
            nightMist.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(mistBlock);
            if (k > 0.001f && !nightMist.isPlaying) nightMist.Play();
            else if (k <= 0.001f && nightMist.isPlaying) nightMist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
