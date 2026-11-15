using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    private int currentHealth;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    void Start()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
    }

    // This is the function your PlayerCombat script looks for!
    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        Debug.Log($"{gameObject.name} took {amount} damage! HP left: {currentHealth}");

        // Flash red when hit
        StopAllCoroutines(); // Stop multiple flashes overlapping
        StartCoroutine(FlashRed());

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    IEnumerator FlashRed()
    {
        spriteRenderer.color = Color.white; // Flash bright white
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = originalColor; // Go back to red
    }

        public void Stun(float duration)
    {
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        // 1. Turn yellow so we know it's stunned
        spriteRenderer.color = Color.yellow; 
        
        // 2. Freeze its Rigidbody2D (Make sure your Dummy HAS a Rigidbody2D!)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static; // Locks it in place
        }

        // 3. Wait
        yield return new WaitForSeconds(duration);

        // 4. Wake it back up
        if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
        spriteRenderer.color = originalColor; // Go back to red
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} was destroyed!");
        // Disable the object so the Player's attack doesn't keep hitting it while it vanishes
        gameObject.SetActive(false); 
        // Or you can use Destroy(gameObject); if you want it gone forever
    }
}