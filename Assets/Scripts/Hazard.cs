using UnityEngine;

public class Hazard : MonoBehaviour
{
    public float damageAmount = 15f;

    // Runs every frame the player is inside the trigger.
    // PlayerController.TakeDamage already ignores damage while dashing or in i-frames.
    void OnTriggerStay2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
            player.TakeDamage(damageAmount, transform.position);
    }
}