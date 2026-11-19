using UnityEngine;

/// <summary>Titik tempat player muncul saat masuk dari SceneExit dengan spawnId yang sama.</summary>
public class SpawnPoint : MonoBehaviour
{
    public string spawnId = "Default";

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}
