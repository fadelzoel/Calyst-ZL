using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class GameSoundController : MonoBehaviour
{
    [Header("References (Auto-find if left empty)")]
    public PlayerController player;
    public PlayerCombat combat;
    public PlayerSkills skills;

    [Header("Audio Sources")]
    public AudioSource sfxSource;
    [Tooltip("Opsional: sumber suara terpisah untuk enemy/dunia agar tidak terpotong.")]
    public AudioSource worldSource;
    [Tooltip("Opsional: AudioSource khusus backsound. Jika kosong, akan dibuatkan otomatis.")]
    public AudioSource bgmSource;

    [Header("Backsound / BGM Settings")]
    public AudioClip backsoundClip;
    [Range(0f, 1f)] public float backsoundVolume = 0.5f;
    public bool playBacksoundOnStart = true;
    public bool loopBacksound = true;
    [Tooltip("Turunkan volume backsound saat Time Stop aktif (opsional).")]
    public bool duckBacksoundDuringTimeStop = false;
    [Range(0f, 1f)] public float timeStopBacksoundMultiplier = 0.35f;

    [Header("Portal & Scene Transition SFX")]
    [Tooltip("Suara saat player menyentuh portal / pindah antar map.")]
    public AudioClip mapTransitionClip;
    [Tooltip("Jika dicentang, GameObject ini akan bertahan (DontDestroyOnLoad) agar suara portal tidak terpotong saat pindah scene.")]
    public bool persistAcrossScenes = true;
    [Tooltip("Jika dicentang, suara juga bunyi saat scene pertama kali dibuka.")]
    public bool playMapTransitionOnFirstScene = false;

    [Header("Player Movement & Hurt SFX")]
    public AudioClip jumpClip;
    public AudioClip dashClip;
    public AudioClip hurtClip;
    public AudioClip dieClip;

    [Header("Player Combat SFX")]
    public AudioClip[] comboSlashClips;
    public AudioClip airSlashClip;
    [Tooltip("Jeda minimum antar suara slash saat serangan di-spam agar tidak tumpang tindih berlebihan.")]
    [Range(0.05f, 0.3f)] public float minSlashSoundInterval = 0.12f;

    [Header("Skills & TimeStop SFX")]
    public AudioClip energyWaveClip;
    public AudioClip judgementCutClip;
    public AudioClip spearThrowClip;
    public AudioClip spearReturnClip;
    public AudioClip timePlatformClip;
    public AudioClip timeStopStartClip;
    public AudioClip timeStopEndClip;
    public AudioClip burnoutClip;

    [Header("Enemy SFX")]
    public AudioClip enemyContactHitClip;

    [Header("Variation Settings")]
    [Range(0f, 0.25f)] public float pitchRandomness = 0.06f;
    [Range(0.1f, 1f)] public float defaultVolume = 0.9f;

    private bool prevGrounded;
    private bool prevDashing;
    private bool prevDead;
    private bool prevAttacking;
    private bool prevTimeStopActive;
    private bool prevBurnedOut;
    private bool prevUsingSkill;
    private bool airSlashUsedInAir;
    private float prevHealth;
    private int prevSpearAmmo;
    private WeaponType prevWeapon;
    private int comboCounter;
    private float slashSoundCooldown;
    private int lastSceneBuildIndex = -999;
    private bool portalSoundPlayedForCurrentTransit;

    private float skill1ActionCooldown;
    private float skill2ActionCooldown;
    private bool judgementAudioPlayedThisCast;

    private static GameSoundController instance;

    void Awake()
    {
        if (persistAcrossScenes)
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
        if (worldSource == null) worldSource = sfxSource;

        ConfigureAudioSource(sfxSource, false);
        ConfigureAudioSource(worldSource, false);
        SetupBgmSource();

        RefreshSceneReferences();
    }

    void ConfigureAudioSource(AudioSource src, bool isLoop)
    {
        if (src == null) return;
        src.playOnAwake = false;
        src.loop = isLoop;
        src.ignoreListenerPause = true;
    }

    void RefreshSceneReferences()
    {
        player = FindAnyObjectByType<PlayerController>();
        combat = FindAnyObjectByType<PlayerCombat>();
        skills = FindAnyObjectByType<PlayerSkills>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSceneReferences();
        SyncInitialStates();

        if (lastSceneBuildIndex != -999 && scene.buildIndex != lastSceneBuildIndex && !portalSoundPlayedForCurrentTransit)
        {
            PlayClip(mapTransitionClip, worldSource, 1f, false);
        }

        portalSoundPlayedForCurrentTransit = false;
        lastSceneBuildIndex = scene.buildIndex;
    }

    void SetupBgmSource()
    {
        if (bgmSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            foreach (AudioSource src in sources)
            {
                if (src != sfxSource && src != worldSource)
                {
                    bgmSource = src;
                    break;
                }
            }

            if (bgmSource == null)
                bgmSource = gameObject.AddComponent<AudioSource>();
        }

        bgmSource.playOnAwake = false;
        bgmSource.loop = loopBacksound;
        bgmSource.pitch = 1f;
        bgmSource.volume = backsoundVolume;
        bgmSource.ignoreListenerPause = true;
        if (backsoundClip != null)
            bgmSource.clip = backsoundClip;
    }

    void Start()
    {
        lastSceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
        SyncInitialStates();

        if (playMapTransitionOnFirstScene)
            PlayClip(mapTransitionClip, worldSource, 1f, false);

        if (playBacksoundOnStart && backsoundClip != null)
            PlayBacksound(backsoundClip, loopBacksound);
    }

    void SyncInitialStates()
    {
        if (player != null)
        {
            prevGrounded = player.IsGrounded;
            prevDashing = player.IsDashing;
            prevDead = player.IsDead;
            prevHealth = player.currentHealth;
            if (player.IsGrounded) airSlashUsedInAir = false;
        }

        if (combat != null)
        {
            prevAttacking = combat.IsAttacking;
        }

        if (skills != null)
        {
            prevSpearAmmo = skills.spearAmmo;
            prevBurnedOut = skills.IsBurnedOut;
            prevUsingSkill = skills.IsUsingSkill;
            prevWeapon = skills.currentWeapon;
        }

        prevTimeStopActive = TimeStop.Active;
    }

    void Update()
    {
        if (player == null || combat == null || skills == null)
            RefreshSceneReferences();

        DetectPortalTouch();

        if (player != null && player.IsGrounded)
            airSlashUsedInAir = false;

        if (slashSoundCooldown > 0f) slashSoundCooldown -= Time.unscaledDeltaTime;
        if (skill1ActionCooldown > 0f) skill1ActionCooldown -= Time.unscaledDeltaTime;
        if (skill2ActionCooldown > 0f) skill2ActionCooldown -= Time.unscaledDeltaTime;

        UpdateBacksoundState();
        DetectPlayerEvents();
        DetectCombatEvents();
        DetectSkillAndTimeStopEvents();
    }

    void DetectPortalTouch()
    {
        if (player == null || portalSoundPlayedForCurrentTransit) return;

        Collider2D playerCol = player.GetComponent<Collider2D>();
        if (playerCol == null) return;

        SceneExit[] exits = FindObjectsByType<SceneExit>(FindObjectsSortMode.None);
        foreach (SceneExit exit in exits)
        {
            if (exit == null || !exit.isActiveAndEnabled) continue;
            Collider2D exitCol = exit.GetComponent<Collider2D>();
            if (exitCol == null) continue;

            if (playerCol.bounds.Intersects(exitCol.bounds))
            {
                portalSoundPlayedForCurrentTransit = true;
                PlayClip(mapTransitionClip, worldSource, 1f, false);
                break;
            }
        }
    }

    void UpdateBacksoundState()
    {
        if (bgmSource == null) return;

        bgmSource.loop = loopBacksound;
        bgmSource.pitch = 1f;

        float targetVolume = backsoundVolume;
        if (duckBacksoundDuringTimeStop && TimeStop.Active)
            targetVolume *= timeStopBacksoundMultiplier;

        bgmSource.volume = Mathf.Clamp01(targetVolume);
    }

    public void PlayBacksound(AudioClip clip = null, bool loop = true)
    {
        if (bgmSource == null) SetupBgmSource();
        if (bgmSource == null) return;

        AudioClip targetClip = clip != null ? clip : backsoundClip;
        if (targetClip == null) return;

        backsoundClip = targetClip;
        loopBacksound = loop;
        bgmSource.clip = targetClip;
        bgmSource.loop = loop;
        bgmSource.pitch = 1f;
        bgmSource.volume = backsoundVolume;

        if (!bgmSource.isPlaying || bgmSource.clip != targetClip)
            bgmSource.Play();
    }

    public void StopBacksound()
    {
        if (bgmSource != null && bgmSource.isPlaying)
            bgmSource.Stop();
    }

    public void SetBacksoundVolume(float volume)
    {
        backsoundVolume = Mathf.Clamp01(volume);
        if (bgmSource != null)
            bgmSource.volume = backsoundVolume;
    }

    void DetectPlayerEvents()
    {
        if (player == null) return;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        float vy = rb != null ? rb.linearVelocity.y : 0f;
        if (prevGrounded && !player.IsGrounded && vy > 0.5f && !player.IsDashing && !player.IsDead)
        {
            PlayClip(jumpClip);
        }

        if (!prevDashing && player.IsDashing)
        {
            PlayClip(dashClip);
        }

        if (player.currentHealth < prevHealth - 0.01f)
        {
            if (player.IsDead && !prevDead)
            {
                PlayClip(dieClip);
            }
            else
            {
                PlayClip(hurtClip);
                PlayClip(enemyContactHitClip, worldSource);
            }
        }
        else if (!prevDead && player.IsDead)
        {
            PlayClip(dieClip);
        }

        prevGrounded = player.IsGrounded;
        prevDashing = player.IsDashing;
        prevDead = player.IsDead;
        prevHealth = player.currentHealth;
    }

    bool WasAttackInputPressedThisFrame()
    {
        bool pressed = false;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            pressed = true;
        if (Keyboard.current != null && (Keyboard.current.jKey.wasPressedThisFrame || Keyboard.current.zKey.wasPressedThisFrame))
            pressed = true;
        if (Gamepad.current != null && (Gamepad.current.buttonWest.wasPressedThisFrame || Gamepad.current.xButton.wasPressedThisFrame))
            pressed = true;
        return pressed;
    }

    void TriggerGroundSlashSound()
    {
        if (player == null || !player.IsGrounded) return;
        if (slashSoundCooldown > 0f) return;

        if (comboSlashClips != null && comboSlashClips.Length > 0)
        {
            int idx = comboCounter % comboSlashClips.Length;
            PlayClip(comboSlashClips[idx]);
            comboCounter = (comboCounter + 1) % 3;
            slashSoundCooldown = minSlashSoundInterval;
        }
    }

    void TriggerAirSlashSound()
    {
        if (player == null || player.IsGrounded) return;
        if (airSlashUsedInAir || airSlashClip == null) return;

        PlayClip(airSlashClip);
        airSlashUsedInAir = true;
        slashSoundCooldown = minSlashSoundInterval;
    }

    void DetectCombatEvents()
    {
        if (combat == null || player == null) return;

        bool attackStarted = !prevAttacking && combat.IsAttacking;

        if (player.IsGrounded)
        {
            bool canSpamSlash = !player.IsDead
                && !player.IsDashing
                && !player.IsStunned
                && !player.IsSkillLocked
                && WasAttackInputPressedThisFrame();

            if (attackStarted || canSpamSlash)
            {
                TriggerGroundSlashSound();
            }
        }
        else
        {
            if (attackStarted && !airSlashUsedInAir && !player.IsDead && !player.IsDashing && !player.IsSkillLocked)
            {
                TriggerAirSlashSound();
            }
        }

        prevAttacking = combat.IsAttacking;
    }

    void DetectSkillAndTimeStopEvents()
    {
        bool timeStopJustStarted = !prevTimeStopActive && TimeStop.Active;
        bool timeStopJustEnded = prevTimeStopActive && !TimeStop.Active;

        if (timeStopJustStarted)
            PlayClip(timeStopStartClip, worldSource);
        else if (timeStopJustEnded)
            PlayClip(timeStopEndClip, worldSource);

        prevTimeStopActive = TimeStop.Active;

        if (skills == null) return;

        if (skills.currentWeapon != prevWeapon)
        {
            comboCounter = 0;
            prevWeapon = skills.currentWeapon;
        }

        bool isCastingTimeStop = timeStopJustStarted || skills.IsTimeStopCasting;

        if (!isCastingTimeStop && skills.currentWeapon == WeaponType.ChronoSpear && skills.spearAmmo < prevSpearAmmo)
        {
            PlayClip(spearThrowClip);
            skill1ActionCooldown = 0.25f;
        }

        if (skills.spearAmmo > prevSpearAmmo)
        {
            PlayClip(spearReturnClip);
        }

        if (!isCastingTimeStop && !prevUsingSkill && skills.IsUsingSkill)
        {
            judgementAudioPlayedThisCast = false;

            if (skills.currentWeapon == WeaponType.Katana && skill1ActionCooldown <= 0f)
            {
                float distToEnemy = GetNearestEnemyDistance();
                if (distToEnemy <= skills.judgementRange)
                {
                    PlayClip(judgementCutClip);
                    judgementAudioPlayedThisCast = true;
                    skill2ActionCooldown = 0.35f;
                }
                else
                {
                    PlayClip(energyWaveClip);
                    skill1ActionCooldown = 0.25f;
                }
            }
            else if (skills.currentWeapon == WeaponType.ChronoSpear && skill2ActionCooldown <= 0f)
            {
                if (player != null && !player.IsGrounded)
                {
                    PlayClip(timePlatformClip);
                    skill2ActionCooldown = 0.25f;
                }
            }
        }

        if (!isCastingTimeStop && skills.IsUsingSkill && skills.currentWeapon == WeaponType.Katana && !judgementAudioPlayedThisCast && skill2ActionCooldown <= 0f)
        {
            if (GetNearestEnemyDistance() <= skills.judgementRange)
            {
                PlayClip(judgementCutClip);
                judgementAudioPlayedThisCast = true;
                skill2ActionCooldown = 0.35f;
            }
        }

        if (!prevBurnedOut && skills.IsBurnedOut)
            PlayClip(burnoutClip);

        prevSpearAmmo = skills.spearAmmo;
        prevBurnedOut = skills.IsBurnedOut;
        prevUsingSkill = skills.IsUsingSkill;
    }

    float GetNearestEnemyDistance()
    {
        if (skills == null) return float.MaxValue;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, skills.judgementRange, skills.enemyLayers);
        float minDist = float.MaxValue;

        foreach (Collider2D col in hits)
        {
            if (col == null) continue;
            IDamageable dmg = col.GetComponentInParent<IDamageable>();
            if (dmg == null) continue;

            float d = Vector2.Distance(transform.position, col.transform.position);
            if (d < minDist) minDist = d;
        }

        return minDist;
    }

    public void PlayMapTransition() => PlayClip(mapTransitionClip, worldSource, 1f, false);
    public void PlayJump() => PlayClip(jumpClip);
    public void PlayDash() => PlayClip(dashClip);
    public void PlayHurt() => PlayClip(hurtClip);
    public void PlayDie() => PlayClip(dieClip);
    public void PlayEnergyWave() => PlayClip(energyWaveClip);
    public void PlayJudgementCut() => PlayClip(judgementCutClip);
    public void PlaySpearThrow() => PlayClip(spearThrowClip);
    public void PlaySpearReturn() => PlayClip(spearReturnClip);
    public void PlayTimePlatform() => PlayClip(timePlatformClip);
    public void PlayEnemyContactHit() => PlayClip(enemyContactHitClip, worldSource);

    void PlayClip(AudioClip clip, AudioSource source = null, float volumeScale = 1f, bool randomizePitch = true)
    {
        if (clip == null) return;
        AudioSource targetSource = source != null ? source : sfxSource;
        if (targetSource == null) return;

        float prevPitch = targetSource.pitch;
        targetSource.pitch = randomizePitch ? (1f + Random.Range(-pitchRandomness, pitchRandomness)) : 1f;
        targetSource.PlayOneShot(clip, defaultVolume * volumeScale);
        targetSource.pitch = prevPitch;
    }
}