using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn Settings")]
    public Transform spawnPoint;
    [Tooltip("Y-coordinate below the map that triggers an instant pitfall respawn.")]
    public float fallDeathY = -20f;
    public float respawnDelay = 0.8f;

    [Header("Optional")]
    public bool reloadSceneOnDeath = false;

    private Vector3 savedSpawnPos;
    private PlayerController controller;
    private PlayerSkills skills;
    private Rigidbody2D rb;
    private bool isRespawning;

    void Start()
    {
        controller = GetComponent<PlayerController>();
        skills = GetComponent<PlayerSkills>();
        rb = GetComponent<Rigidbody2D>();
        savedSpawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
    }

    void Update()
    {
        if (isRespawning) return;

        // 1. Fell into a bottomless pit
        if (transform.position.y < fallDeathY)
        {
            StartCoroutine(RespawnRoutine());
            return;
        }

        // 2. HP reached 0
        if (controller != null && controller.currentHealth <= 0f)
        {
            StartCoroutine(RespawnRoutine());
        }
    }

    public void SetCheckpoint(Vector3 newPos)
    {
        savedSpawnPos = newPos;
    }

    IEnumerator RespawnRoutine()
    {
        isRespawning = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        yield return new WaitForSeconds(respawnDelay);

        if (reloadSceneOnDeath)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield break;
        }

        transform.position = spawnPoint != null ? spawnPoint.position : savedSpawnPos;

        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
        }

        if (controller != null)
            controller.currentHealth = controller.maxHealth;

        if (skills != null)
            skills.overloadMeter = 0f;

        isRespawning = false;
    }
}