using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Transisi pindah scene bergaya sci-fi:
///   1. Dua pintu logam menutup dari atas & bawah (garis glow cyan + efek glitch).
///   2. Layar HUD "WARP DRIVE ENGAGED" dengan teks ketik, nama tujuan, dan progress bar bersegmen.
///   3. Scene baru dimuat, lalu pintu terbuka dengan kilatan cahaya.
/// Selama animasi, waktu game DIBEKUKAN (Time.timeScale = 0) dan input player dimatikan.
/// Semua visual digambar lewat kode (tanpa aset). Dibuat otomatis dan bertahan antar scene.
/// Pakai: SceneFader.LoadScene("NamaScene");
/// </summary>
public class SceneFader : MonoBehaviour
{
    // ---------- Atur di sini ----------
    const float CloseDuration = 0.9f;     // detik, pintu menutup (+ glitch)
    const float LoadingHold = 1.2f;       // detik, layar HUD loading sebelum pindah scene
    const float OpenDuration = 0.8f;      // detik, pintu terbuka di scene baru

    const string SubtitleText = "// SECTOR TRANSIT";
    const string HeadlineClosing = "WARP DRIVE ENGAGED";
    const string HeadlineDone = "TRANSIT COMPLETE";
    const string DestinationPrefix = "DESTINATION > ";

    static readonly Color BgColor = new Color(0.02f, 0.04f, 0.08f);       // biru gelap
    static readonly Color AccentColor = new Color(0.10f, 0.90f, 1.00f);   // cyan
    static readonly Color AccentColor2 = new Color(1.00f, 0.15f, 0.60f);  // magenta (glitch)

    static SceneFader instance;

    // ---------- State animasi ----------
    private float doorProgress;     // 0 = terbuka penuh, 1 = tertutup penuh
    private float glitch;           // 0..1 intensitas glitch
    private float flash;            // 0..1 kilatan cahaya saat pintu terbuka
    private float hudAlpha;         // 0..1 transparansi HUD loading
    private float barProgress;      // 0..1 progress bar
    private float textReveal;       // 0..1 teks headline diketik
    private string headline = HeadlineClosing;
    private string targetLabel = "";

    private int glitchSeed;
    private float glitchTimer;

    private Coroutine routine;
    private bool frozen;
    private float prevTimeScale = 1f;
    private bool transitioning;
    private bool introPending;
    private readonly List<PlayerInput> disabledInputs = new List<PlayerInput>();

    private GUIStyle labelStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    // Dibuat otomatis saat game mulai, dan langsung membuka pintu untuk scene pertama
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneFader f = Get();
        f.doorProgress = 1f;
        f.hudAlpha = 0f;
        f.StartFade(OpenRoutine(f, false));
    }

    static SceneFader Get()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("SceneFader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SceneFader>();
        }
        return instance;
    }

    // ---------- API ----------
    public static void LoadScene(string sceneName)
    {
        SceneFader f = Get();
        if (f.transitioning) return;
        f.targetLabel = string.IsNullOrEmpty(sceneName) ? "" : sceneName.ToUpperInvariant();
        f.StartFade(TransitionRoutine(f, sceneName, -1));
    }

    public static void LoadScene(int buildIndex)
    {
        SceneFader f = Get();
        if (f.transitioning) return;
        string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        f.targetLabel = string.IsNullOrEmpty(path)
            ? ""
            : System.IO.Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
        f.StartFade(TransitionRoutine(f, null, buildIndex));
    }

    public static void Reload()
    {
        LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------- Internal ----------
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Scene dimuat dengan cara apa pun: pintu mulai tertutup lalu terbuka
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        doorProgress = 1f;
        flash = 0f;

        bool frozenIntro = introPending;
        introPending = false;

        hudAlpha = frozenIntro ? 1f : 0f;
        StartFade(OpenRoutine(this, frozenIntro));
    }

    void StartFade(IEnumerator r)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(r);
    }

    void Update()
    {
        // ganti pola glitch ~16x per detik (pakai unscaled time karena game beku)
        if (glitch > 0.001f)
        {
            glitchTimer += Time.unscaledDeltaTime;
            if (glitchTimer > 0.06f)
            {
                glitchTimer = 0f;
                glitchSeed++;
            }
        }
    }

    // Pastikan waktu tetap 0 walau script lain (misal hit-stop) mengembalikannya ke 1
    void LateUpdate()
    {
        if (frozen) Time.timeScale = 0f;
    }

    void Freeze(bool on)
    {
        if (on == frozen) return;
        frozen = on;

        if (on)
        {
            prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = prevTimeScale;
        }
    }

    void SetPlayerInput(bool on)
    {
        if (!on)
        {
            foreach (PlayerInput pi in FindObjectsByType<PlayerInput>(FindObjectsSortMode.None))
            {
                if (pi.enabled)
                {
                    pi.enabled = false;
                    disabledInputs.Add(pi);
                }
            }
        }
        else
        {
            foreach (PlayerInput pi in disabledInputs)
                if (pi != null) pi.enabled = true;
            disabledInputs.Clear();
        }
    }

    // ---------- Animasi ----------
    static IEnumerator TransitionRoutine(SceneFader f, string sceneName, int buildIndex)
    {
        f.transitioning = true;
        f.Freeze(true);
        f.SetPlayerInput(false);

        f.headline = HeadlineClosing;
        f.hudAlpha = 0f;
        f.barProgress = 0f;
        f.textReveal = 0f;
        f.flash = 0f;

        // ---------- Fase 1: pintu menutup + glitch ----------
        float t = 0f;
        while (t < CloseDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / CloseDuration);
            f.doorProgress = k * k;                          // makin cepat, seperti dibanting
            f.glitch = Mathf.Lerp(0f, 0.25f, k);
            yield return null;
        }
        f.doorProgress = 1f;

        // ---------- Fase 2: layar HUD loading ----------
        t = 0f;
        while (t < LoadingHold)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / LoadingHold);
            f.hudAlpha = Mathf.Clamp01(t / 0.15f);
            f.textReveal = Mathf.Clamp01(t / (LoadingHold * 0.45f));
            f.barProgress = Mathf.SmoothStep(0f, 1f, k);
            f.glitch = k > 0.9f ? 0.12f : 0f;                // glitch hanya sedikit menjelang pindah
            yield return null;
        }
        f.barProgress = 1f;

        // Time stop (skill) jangan terbawa ke scene baru
        TimeStop.End();

        // Scene baru tetap beku sampai animasi buka selesai
        f.introPending = true;
        if (!string.IsNullOrEmpty(sceneName)) SceneManager.LoadScene(sceneName);
        else SceneManager.LoadScene(buildIndex);
    }

    static IEnumerator OpenRoutine(SceneFader f, bool frozenIntro)
    {
        if (frozenIntro) f.SetPlayerInput(false);

        yield return null;   // satu frame supaya scene baru sempat tampil di belakang pintu

        if (frozenIntro)
        {
            f.headline = HeadlineDone;
            f.textReveal = 1f;
            f.barProgress = 1f;
            f.glitch = 0.12f;
            yield return new WaitForSecondsRealtime(0.2f);
        }

        // ---------- Fase 3: pintu terbuka + kilatan ----------
        float t = 0f;
        while (t < OpenDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / OpenDuration);
            float eased = 1f - Mathf.Pow(1f - k, 3f);        // cepat di awal, melambat di akhir
            f.doorProgress = 1f - eased;
            f.hudAlpha = 1f - Mathf.Clamp01(k / 0.3f);
            f.glitch = (1f - k) * 0.15f;
            f.flash = (1f - k) * (1f - k) * 0.2f;
            yield return null;
        }
        f.doorProgress = 0f;
        f.glitch = 0f;
        f.flash = 0f;
        f.hudAlpha = 0f;

        // Animasi selesai: waktu berjalan lagi dan input aktif kembali
        f.Freeze(false);
        f.SetPlayerInput(true);
        f.transitioning = false;
    }

    // ---------- Gambar ----------
    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
    }

    static float Rand01(int n)
    {
        n = (n << 13) ^ n;
        int v = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
        return v / 2147483647f;
    }

    void EnsureStyles()
    {
        if (labelStyle != null) return;
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        Font mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Menlo", "monospace" }, 16);
        if (mono != null) labelStyle.font = mono;
    }

    void DrawLabel(Rect r, string text, int size, Color c, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        labelStyle.fontSize = Mathf.Max(8, size);
        labelStyle.alignment = anchor;
        labelStyle.normal.textColor = c;
        GUI.color = Color.white;
        GUI.Label(r, text, labelStyle);
    }

    void DrawPanel(Rect r, bool isTop, float u)
    {
        if (r.height <= 0.5f) return;

        // dasar logam gelap
        Fill(r, BgColor);

        // garis scan horizontal tipis
        Color sc = AccentColor; sc.a = 0.05f;
        for (float y = r.y; y < r.yMax; y += 6f * u)
            Fill(new Rect(r.x, y, r.width, Mathf.Max(1f, u)), sc);

        // grid vertikal yang bergeser pelan
        float spacing = 90f * u;
        float scroll = (Time.unscaledTime * 30f * u) % spacing;
        Color gc = AccentColor; gc.a = 0.07f;
        for (float x = -scroll; x < r.width; x += spacing)
            Fill(new Rect(x, r.y, Mathf.Max(1f, u), r.height), gc);

        // tanda ukur di tepi dalam pintu
        float edgeY = isTop ? r.yMax : r.y;
        Color tc = AccentColor; tc.a = 0.55f;
        float tickSpacing = 36f * u;
        float tickScroll = (Time.unscaledTime * 60f * u) % tickSpacing;
        for (float x = -tickScroll; x < r.width; x += tickSpacing)
        {
            float th = 9f * u;
            float ty = isTop ? edgeY - th - 5f * u : edgeY + 5f * u;
            Fill(new Rect(x, ty, Mathf.Max(2f, 2f * u), th), tc);
        }
    }

    void DrawEdgeGlow(float edgeY, bool isTop, float w, float u)
    {
        // glow memancar ke arah celah (layer bertumpuk: dekat tepi paling terang)
        Color gc = AccentColor; gc.a = 0.12f;
        for (int i = 0; i < 6; i++)
        {
            float g = (4f + i * 5f) * u;
            Fill(isTop ? new Rect(0f, edgeY, w, g) : new Rect(0f, edgeY - g, w, g), gc);
        }

        // garis inti terang
        Color core = Color.Lerp(AccentColor, Color.white, 0.6f);
        float ch = Mathf.Max(2f, 3f * u);
        Fill(isTop ? new Rect(0f, edgeY - ch, w, ch) : new Rect(0f, edgeY, w, ch), core);
    }

    void DrawHud(float w, float h, float u)
    {
        if (hudAlpha <= 0.01f) return;

        float cx = w * 0.5f, cy = h * 0.5f;
        float bw = 680f * u, bh = 230f * u;
        Rect box = new Rect(cx - bw * 0.5f, cy - bh * 0.5f, bw, bh);

        Color ac = AccentColor; ac.a = hudAlpha;

        // kurung siku di empat sudut
        float len = 42f * u, th = Mathf.Max(2f, 3f * u);
        Fill(new Rect(box.x, box.y, len, th), ac);                    Fill(new Rect(box.x, box.y, th, len), ac);
        Fill(new Rect(box.xMax - len, box.y, len, th), ac);           Fill(new Rect(box.xMax - th, box.y, th, len), ac);
        Fill(new Rect(box.x, box.yMax - th, len, th), ac);            Fill(new Rect(box.x, box.yMax - len, th, len), ac);
        Fill(new Rect(box.xMax - len, box.yMax - th, len, th), ac);   Fill(new Rect(box.xMax - th, box.yMax - len, th, len), ac);

        // subjudul
        Color sub = AccentColor; sub.a = 0.7f * hudAlpha;
        DrawLabel(new Rect(box.x, box.y + 10f * u, box.width, 30f * u), SubtitleText, Mathf.RoundToInt(20f * u), sub);

        // headline diketik + kursor berkedip
        int chars = Mathf.Clamp(Mathf.RoundToInt(headline.Length * textReveal), 0, headline.Length);
        string shown = headline.Substring(0, chars);
        bool typing = chars < headline.Length;
        if (typing || ((int)(Time.unscaledTime * 3f) % 2 == 0)) shown += "_";
        Color head = Color.Lerp(AccentColor, Color.white, 0.75f); head.a = hudAlpha;
        DrawLabel(new Rect(box.x, box.y + 48f * u, box.width, 70f * u), shown, Mathf.RoundToInt(46f * u), head);

        // tujuan
        if (!string.IsNullOrEmpty(targetLabel))
        {
            Color dest = AccentColor; dest.a = 0.9f * hudAlpha;
            DrawLabel(new Rect(box.x, box.y + 120f * u, box.width, 30f * u),
                      DestinationPrefix + targetLabel, Mathf.RoundToInt(22f * u), dest);
        }

        // progress bar bersegmen
        const int segments = 30;
        float barX = box.x + 30f * u, barW = box.width - 60f * u;
        float barY = box.y + 164f * u, barH = 18f * u;
        Color frame = AccentColor; frame.a = 0.6f * hudAlpha;
        Fill(new Rect(barX, barY, barW, Mathf.Max(1f, u)), frame);
        Fill(new Rect(barX, barY + barH, barW, Mathf.Max(1f, u)), frame);
        Fill(new Rect(barX, barY, Mathf.Max(1f, u), barH), frame);
        Fill(new Rect(barX + barW - Mathf.Max(1f, u), barY, Mathf.Max(1f, u), barH), frame);

        float pad = 3f * u;
        float segW = (barW - pad * 2f) / segments;
        int filled = Mathf.FloorToInt(barProgress * segments + 0.0001f);
        Color fc = AccentColor; fc.a = hudAlpha;
        for (int i = 0; i < filled; i++)
            Fill(new Rect(barX + pad + i * segW + 1f, barY + pad, segW - 2f, barH - pad * 2f), fc);

        // persen
        Color pc = AccentColor; pc.a = 0.8f * hudAlpha;
        DrawLabel(new Rect(box.x, box.y + 190f * u, box.width, 26f * u),
                  Mathf.RoundToInt(barProgress * 100f) + "%", Mathf.RoundToInt(18f * u), pc);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (doorProgress <= 0.001f && glitch <= 0.001f && flash <= 0.001f) return;

        EnsureStyles();
        GUI.depth = -10000;   // paling atas, menutupi HUD juga

        float w = Screen.width, h = Screen.height;
        float u = h / 1080f;
        float half = h * 0.5f * doorProgress;   // tinggi tiap pintu

        // ---------- pintu atas & bawah ----------
        if (doorProgress > 0.001f)
        {
            DrawPanel(new Rect(0f, 0f, w, half), true, u);
            DrawPanel(new Rect(0f, h - half, w, half), false, u);

            if (doorProgress < 0.999f)
            {
                DrawEdgeGlow(half, true, w, u);
                DrawEdgeGlow(h - half, false, w, u);
            }
            else
            {
                // pintu tertutup penuh: garis scan menyapu layar
                float scanY = (Time.unscaledTime * 0.7f % 1f) * h;
                Color s1 = AccentColor; s1.a = 0.05f;
                Color s2 = AccentColor; s2.a = 0.4f;
                Fill(new Rect(0f, scanY - 14f * u, w, 14f * u), s1);
                Fill(new Rect(0f, scanY, w, Mathf.Max(2f, 2f * u)), s2);
            }
        }

        // ---------- HUD loading ----------
        if (doorProgress >= 0.999f) DrawHud(w, h, u);

        // ---------- glitch: bar acak di atas segalanya ----------
        int count = Mathf.RoundToInt(glitch * 8f);
        for (int i = 0; i < count; i++)
        {
            int s = glitchSeed * 131 + i * 17;
            float gy = Rand01(s) * h;
            float gh = (2f + Rand01(s + 1) * 8f) * u;
            float gx = Rand01(s + 2) * w * 0.7f;
            float gw = (0.1f + Rand01(s + 3) * 0.3f) * w;
            float r = Rand01(s + 4);
            Color gc = r < 0.8f ? AccentColor : Color.white;   // tanpa magenta, lebih kalem
            gc.a = 0.12f + Rand01(s + 5) * 0.2f;
            Fill(new Rect(gx, gy, gw, gh), gc);
        }

        // ---------- kilatan cahaya saat pintu terbuka ----------
        if (flash > 0.001f)
        {
            Color fc = Color.Lerp(AccentColor, Color.white, 0.5f);
            fc.a = flash;
            Fill(new Rect(0f, 0f, w, h), fc);
        }

        GUI.color = Color.white;
    }
}