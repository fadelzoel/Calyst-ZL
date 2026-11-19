using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Menampilkan layar GAME OVER saat:
///   - HP player habis (PlayerController.IsDead), atau
///   - GameOverManager.Trigger() dipanggil dari script mana pun (misal GameOverZone di ujung map).
///
/// Layar dibuat sendiri sebagai prefab dan diletakkan di:  Assets/Resources/GameOverScreen.prefab
/// (nama file harus persis "GameOverScreen"). Tombol-tombolnya disambungkan lewat GameOverActions.
/// Kalau prefab itu belum ada, muncul layar sementara sederhana supaya bisa dites.
///
/// Dibuat otomatis (tidak perlu dipasang di scene) dan bertahan antar scene.
/// </summary>
public class GameOverManager : MonoBehaviour
{
    // ---------- Atur di sini ----------
    const string ScreenResourceName = "GameOverScreen";
    const float DeathDelay = 1.5f;            // detik, jeda setelah HP habis (biar animasi mati sempat main)
    const bool FreezeTimeOnShow = true;       // bekukan game saat layar Game Over tampil

    static GameOverManager instance;

    private PlayerController player;
    private bool active;                      // layar game over sedang / sudah tampil
    private bool leaving;                     // pemain sudah memilih Retry / Menu
    private GameObject screenInstance;
    private bool showFallback;
    private GUIStyle fallbackStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        Get().FindPlayer();   // scene pertama
    }

    static GameOverManager Get()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("GameOverManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameOverManager>();
        }
        return instance;
    }

    // ---------- API ----------
    /// <summary>Tampilkan layar Game Over (dipanggil oleh GameOverZone atau script lain).</summary>
    public static void Trigger(float delay = 0f)
    {
        Get().Begin(delay);
    }

    /// <summary>Ulangi scene yang sedang aktif dari awal (dipanggil lewat GameOverActions).</summary>
    public static void Retry()
    {
        Get().DoRetry();
    }

    /// <summary>Pindah ke scene lain, misal menu utama (dipanggil lewat GameOverActions).</summary>
    public static void LoadScene(string sceneName)
    {
        Get().DoLoadScene(sceneName);
    }

    public static bool IsGameOver
    {
        get { return instance != null && instance.active; }
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

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        active = false;
        leaving = false;
        showFallback = false;

        if (screenInstance != null)
        {
            Destroy(screenInstance);
            screenInstance = null;
        }

        FindPlayer();
    }

    void FindPlayer()
    {
        player = FindFirstObjectByType<PlayerController>();
    }

    void Update()
    {
        if (active || player == null) return;

        if (player.IsDead)
            Begin(DeathDelay);
    }

    void Begin(float delay)
    {
        if (active) return;
        active = true;
        StartCoroutine(ShowRoutine(delay));
    }

    IEnumerator ShowRoutine(float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        // Mati = mulai dari awal lagi
        PlayerPersistence.Clear();

        // Matikan input player supaya tidak bisa bergerak / menyerang di belakang layar
        foreach (PlayerInput pi in FindObjectsByType<PlayerInput>(FindObjectsSortMode.None))
            pi.enabled = false;

        if (FreezeTimeOnShow) Time.timeScale = 0f;

        GameObject prefab = Resources.Load<GameObject>(ScreenResourceName);
        if (prefab != null)
        {
            screenInstance = Instantiate(prefab);
        }
        else
        {
            Debug.LogWarning("GameOverManager: prefab 'Assets/Resources/" + ScreenResourceName +
                             ".prefab' tidak ditemukan, memakai layar sementara.");
            showFallback = true;
        }
    }

    void DoRetry()
    {
        if (!active || leaving) return;
        leaving = true;
        SceneFader.Reload();   // dengan animasi transisi; layar game over dibersihkan saat scene baru dimuat
    }

    void DoLoadScene(string sceneName)
    {
        if (!active || leaving) return;
        leaving = true;
        SceneFader.LoadScene(sceneName);
    }

    // ---------- Layar sementara (hanya dipakai kalau prefab belum dibuat) ----------
    void OnGUI()
    {
        if (!showFallback) return;

        GUI.depth = -100;   // di bawah animasi transisi SceneFader

        float w = Screen.width, h = Screen.height;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (fallbackStyle == null)
        {
            fallbackStyle = new GUIStyle(GUI.skin.label);
            fallbackStyle.alignment = TextAnchor.MiddleCenter;
            fallbackStyle.fontStyle = FontStyle.Bold;
        }
        fallbackStyle.fontSize = Mathf.RoundToInt(h * 0.09f);
        GUI.Label(new Rect(0f, h * 0.25f, w, h * 0.15f), "GAME OVER", fallbackStyle);

        if (GUI.Button(new Rect(w * 0.5f - 110f, h * 0.5f, 220f, 54f), "RETRY"))
            DoRetry();
    }
}
