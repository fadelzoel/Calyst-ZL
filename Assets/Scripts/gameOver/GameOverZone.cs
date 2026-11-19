using UnityEngine;

/// <summary>
/// Taruh di objek dengan Collider2D (Is Trigger) di ujung / bawah map.
/// Player yang menyentuhnya langsung game over.
/// </summary>
public class GameOverZone : MonoBehaviour
{
    [Tooltip("Jeda (detik) sebelum layar Game Over muncul.")]
    public float delay = 0.3f;

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        GameOverManager.Trigger(delay);
    }
}
