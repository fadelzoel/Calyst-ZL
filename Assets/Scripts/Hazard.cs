using UnityEngine;

public class Hazard : MonoBehaviour
{
    public float damageAmount = 15f;

    // OnTriggerStay2D runs every frame the player is inside the trigger
    void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController stats = other.GetComponent<PlayerController>();
            if (stats != null)
            {
                // We will check if the player is dashing later, but for now, just deal damage
                stats.TakeDamage(damageAmount);
            }
        }
    }
}