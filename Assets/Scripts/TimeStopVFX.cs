using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TimeStopVFX : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Center of the shockwave. Defaults to this transform (Player).")]
    public Transform origin;

    [Header("Shockwave Ring (Luna Nights / Za Warudo)")]
    public float expandDuration = 0.32f;
    public float collapseDuration = 0.18f;
    public float maxWaveRadius = 26f;
    public float waveThickness = 0.45f;
    public Color waveOuterColor = new Color(0.45f, 0.85f, 1f, 0.95f);
    public Color waveInnerTint = new Color(0.25f, 0.35f, 0.9f, 0.14f);

    [Header("Frozen World Color & Distortion (URP)")]
    [Tooltip("Leave empty to auto-create a global URP Volume at runtime.")]
    public Volume postProcessVolume;
    [Range(-100f, 0f)] public float frozenSaturation = -85f;
    [Range(0f, 100f)] public float frozenContrast = 35f;
    [Range(-180f, 180f)] public float frozenHueShift = -25f;
    public Color frozenColorFilter = new Color(0.75f, 0.85f, 1f, 1f);
    [Range(-1f, 1f)] public float burstLensDistortion = -0.45f;
    [Range(-1f, 1f)] public float holdLensDistortion = -0.06f;
    [Range(0f, 1f)] public float burstChromaticAberration = 1f;
    [Range(0f, 1f)] public float holdChromaticAberration = 0.25f;

    [Header("Camera Punch")]
    public float zoomPunchAmount = 0.35f;

    [Header("Audio (Optional)")]
    public AudioSource audioSource;
    public AudioClip timeStopStartSfx;
    public AudioClip timeStopEndSfx;

    private ColorAdjustments colorAdj;
    private LensDistortion lensDist;
    private ChromaticAberration chromAb;
    private Vignette vignette;

    private LineRenderer waveRing;
    private SpriteRenderer waveFill;
    private Coroutine animRoutine;
    private Camera mainCam;
    private float baseOrthoSize;
    private bool hasBaseOrtho;

    private const int RingSegments = 72;

    void Awake()
    {
        if (origin == null) origin = transform;
        mainCam = Camera.main;
        if (mainCam != null && mainCam.orthographic)
        {
            baseOrthoSize = mainCam.orthographicSize;
            hasBaseOrtho = true;
        }

        SetupVolume();
        SetupWaveVisuals();
    }

    void OnEnable()
    {
        TimeStop.Started += HandleTimeStopStarted;
        TimeStop.Ended += HandleTimeStopEnded;
    }

    void OnDisable()
    {
        TimeStop.Started -= HandleTimeStopStarted;
        TimeStop.Ended -= HandleTimeStopEnded;
        ResetEffectsImmediate();
    }

    void SetupVolume()
    {
        if (postProcessVolume == null)
        {
            GameObject volObj = new GameObject("TimeStop_URPVolume");
            volObj.transform.SetParent(transform);
            postProcessVolume = volObj.AddComponent<Volume>();
            postProcessVolume.isGlobal = true;
            postProcessVolume.priority = 50f;
            postProcessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        }

        VolumeProfile profile = postProcessVolume.profile;
        if (!profile.TryGet(out colorAdj)) colorAdj = profile.Add<ColorAdjustments>(true);
        if (!profile.TryGet(out lensDist)) lensDist = profile.Add<LensDistortion>(true);
        if (!profile.TryGet(out chromAb)) chromAb = profile.Add<ChromaticAberration>(true);
        if (!profile.TryGet(out vignette)) vignette = profile.Add<Vignette>(true);

        colorAdj.saturation.overrideState = true;
        colorAdj.contrast.overrideState = true;
        colorAdj.hueShift.overrideState = true;
        colorAdj.colorFilter.overrideState = true;

        lensDist.intensity.overrideState = true;
        chromAb.intensity.overrideState = true;
        vignette.intensity.overrideState = true;
        vignette.color.overrideState = true;
        vignette.color.value = new Color(0.05f, 0.1f, 0.25f, 1f);

        postProcessVolume.weight = 0f;
    }

    void SetupWaveVisuals()
    {
        GameObject ringObj = new GameObject("TimeStop_ShockwaveRing");
        ringObj.transform.SetParent(transform);
        waveRing = ringObj.AddComponent<LineRenderer>();
        waveRing.useWorldSpace = true;
        waveRing.loop = true;
        waveRing.positionCount = RingSegments;
        waveRing.material = new Material(Shader.Find("Sprites/Default"));
        waveRing.startColor = waveOuterColor;
        waveRing.endColor = Color.white;
        waveRing.sortingOrder = 50;
        waveRing.enabled = false;

        GameObject fillObj = new GameObject("TimeStop_ShockwaveFill");
        fillObj.transform.SetParent(transform);
        waveFill = fillObj.AddComponent<SpriteRenderer>();
        waveFill.sprite = CreateDiscSprite(128);
        waveFill.color = waveInnerTint;
        waveFill.sortingOrder = 49;
        waveFill.enabled = false;
    }

    void HandleTimeStopStarted()
    {
        if (audioSource != null && timeStopStartSfx != null)
            audioSource.PlayOneShot(timeStopStartSfx);

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(ActivateRoutine());
    }

    void HandleTimeStopEnded()
    {
        if (audioSource != null && timeStopEndSfx != null)
            audioSource.PlayOneShot(timeStopEndSfx);

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(DeactivateRoutine());
    }

    IEnumerator ActivateRoutine()
    {
        Vector3 center = origin != null ? origin.position : transform.position;
        waveRing.enabled = true;
        waveFill.enabled = true;
        postProcessVolume.weight = 1f;

        colorAdj.saturation.value = frozenSaturation;
        colorAdj.contrast.value = frozenContrast;
        colorAdj.hueShift.value = frozenHueShift;
        colorAdj.colorFilter.value = frozenColorFilter;
        vignette.intensity.value = 0.32f;

        float t = 0f;
        while (t < expandDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / expandDuration);
            float ease = 1f - Mathf.Pow(1f - u, 3f); // fast burst, smooth settle

            float radius = Mathf.Lerp(0.1f, maxWaveRadius, ease);
            DrawRing(center, radius, Mathf.Lerp(waveThickness * 1.6f, waveThickness * 0.4f, ease));

            waveFill.transform.position = center;
            waveFill.transform.localScale = Vector3.one * (radius * 2f);
            Color fc = waveInnerTint;
            fc.a = Mathf.Lerp(waveInnerTint.a, 0f, u);
            waveFill.color = fc;

            // Spatial warp spike on activation
            float spike = Mathf.Sin(u * Mathf.PI);
            lensDist.intensity.value = Mathf.Lerp(holdLensDistortion, burstLensDistortion, spike);
            chromAb.intensity.value = Mathf.Lerp(holdChromaticAberration, burstChromaticAberration, spike);

            if (hasBaseOrtho && mainCam != null)
                mainCam.orthographicSize = baseOrthoSize - (zoomPunchAmount * spike);

            yield return null;
        }

        waveRing.enabled = false;
        waveFill.enabled = false;
        lensDist.intensity.value = holdLensDistortion;
        chromAb.intensity.value = holdChromaticAberration;
        if (hasBaseOrtho && mainCam != null) mainCam.orthographicSize = baseOrthoSize;
    }

    IEnumerator DeactivateRoutine()
    {
        Vector3 center = origin != null ? origin.position : transform.position;
        waveRing.enabled = true;
        waveFill.enabled = false;

        float t = 0f;
        while (t < collapseDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / collapseDuration);
            float ease = u * u; // accelerates inward into Caly

            float radius = Mathf.Lerp(maxWaveRadius * 0.75f, 0.05f, ease);
            DrawRing(center, radius, Mathf.Lerp(waveThickness * 0.5f, 0.05f, ease));

            postProcessVolume.weight = 1f - ease;
            chromAb.intensity.value = Mathf.Lerp(0.8f, 0f, ease);
            lensDist.intensity.value = Mathf.Lerp(0.25f, 0f, ease);

            yield return null;
        }

        ResetEffectsImmediate();
    }

    void ResetEffectsImmediate()
    {
        if (waveRing != null) waveRing.enabled = false;
        if (waveFill != null) waveFill.enabled = false;
        if (postProcessVolume != null) postProcessVolume.weight = 0f;
        if (hasBaseOrtho && mainCam != null) mainCam.orthographicSize = baseOrthoSize;
    }

    void DrawRing(Vector3 center, float radius, float width)
    {
        waveRing.startWidth = width;
        waveRing.endWidth = width;

        for (int i = 0; i < RingSegments; i++)
        {
            float angle = (i / (float)RingSegments) * Mathf.PI * 2f;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            waveRing.SetPosition(i, pos);
        }
    }

    static Sprite CreateDiscSprite(int res)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float half = res * 0.5f;
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                float a = d <= 1f ? Mathf.SmoothStep(1f, 0f, d * d) : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 1f);
    }
}