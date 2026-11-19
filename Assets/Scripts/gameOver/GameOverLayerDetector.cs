using UnityEngine;

/// <summary>
/// Dipasang otomatis ke Player oleh GameOverManager.
/// Jika player menyentuh objek ber-layer "over" (collider biasa atau trigger), layar Game Over muncul.
/// </summary>
public class GameOverLayerDetector : MonoBehaviour
{
    [Tooltip("Jeda (detik) sebelum layar Game Over muncul.")]
    public float delay = 0.3f;

    int overLayer = -1;

    void Awake()
    {
        overLayer = LayerMask.NameToLayer(GameOverManager.OverLayerName);
        if (overLayer < 0)
            Debug.LogWarning("GameOverLayerDetector: layer '" + GameOverManager.OverLayerName +
                             "' belum dibuat di Project Settings > Tags and Layers.");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Check(other.gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Check(collision.gameObject);
    }

    void Check(GameObject other)
    {
        if (overLayer < 0 || other.layer != overLayer) return;
        GameOverManager.Trigger(delay);
    }
}
