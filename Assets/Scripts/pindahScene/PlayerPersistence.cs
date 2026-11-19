using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Membawa Sanity, Overload, dan senjata player antar scene / arena.
/// HP TIDAK dibawa: setiap pindah scene HP player selalu penuh.
/// Terpasang OTOMATIS di Player setiap scene dimuat (tidak perlu Add Component),
/// dan menyimpan status terus-menerus, jadi tetap jalan walau pindah scene
/// lewat script apa pun (SceneExit, tombol, trigger buatanmu sendiri, dll).
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerPersistence : MonoBehaviour
{
    // ---------- Data yang dibawa antar scene ----------
    static bool hasData;
    static int savedSanity;
    static float savedOverload;
    static WeaponType savedWeapon;

    /// <summary>ID SpawnPoint tujuan di scene berikutnya (diisi oleh SceneExit).</summary>
    public static string NextSpawnId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        hasData = false;
        NextSpawnId = null;
    }

    // ---------- Pasang otomatis di Player ----------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstall()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureOnPlayer();   // scene pertama
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureOnPlayer();
    }

    static void EnsureOnPlayer()
    {
        PlayerController p = FindFirstObjectByType<PlayerController>();
        if (p != null && p.GetComponent<PlayerPersistence>() == null)
            p.gameObject.AddComponent<PlayerPersistence>();
    }

    // ---------- API ----------
    /// <summary>Simpan status sekarang (SceneExit memanggil ini sebelum LoadScene).</summary>
    public static void Save(PlayerController player)
    {
        if (player == null || player.IsDead) return;

        PlayerSkills skills = player.GetComponent<PlayerSkills>();

        if (skills != null)
        {
            savedSanity = skills.sanity;
            savedOverload = skills.overloadMeter;
            savedWeapon = skills.currentWeapon;
        }
        hasData = true;
    }

    /// <summary>Hapus data tersimpan (mulai dari awal).</summary>
    public static void Clear()
    {
        hasData = false;
        NextSpawnId = null;
    }

    // ---------- Di dalam scene ----------
    private PlayerController player;
    private PlayerSkills skills;
    private bool restored;

    void Awake()
    {
        MoveToSpawnPoint();
    }

    IEnumerator Start()
    {
        player = GetComponent<PlayerController>();
        skills = GetComponent<PlayerSkills>();

        // PlayerController.Start() mengisi HP penuh, jadi tunggu satu frame baru pulihkan
        yield return null;

        if (hasData)
        {
            player.currentHealth = player.maxHealth;   // HP selalu penuh saat pindah scene
            if (skills != null)
            {
                skills.sanity = savedSanity;
                skills.overloadMeter = savedOverload;
                skills.currentWeapon = savedWeapon;
            }
        }

        restored = true;
    }

    void Update()
    {
        // Sebelum dipulihkan, jangan menimpa data tersimpan dengan nilai awal scene
        if (!restored || player == null) return;

        if (player.IsDead)
        {
            Clear();   // mati: hidup berikutnya mulai dari awal
            return;
        }

        Save(player);
    }

    void MoveToSpawnPoint()
    {
        if (string.IsNullOrEmpty(NextSpawnId)) return;

        foreach (SpawnPoint sp in FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
        {
            if (sp.spawnId == NextSpawnId)
            {
                transform.position = sp.transform.position;
                break;
            }
        }

        NextSpawnId = null;
    }
}