using UnityEngine;

/// <summary>Taruh di objek dengan Collider2D (Is Trigger). Player yang menyentuhnya pindah scene.</summary>
public class SceneExit : MonoBehaviour
{
    [Tooltip("Nama scene tujuan. Harus ada di File > Build Settings > Scenes In Build.")]
    public string targetScene;
    [Tooltip("ID SpawnPoint di scene tujuan. Kosongkan untuk memakai posisi awal Player di scene itu.")]
    public string spawnId = "";

    private bool used;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead) return;

        used = true;
        PlayerPersistence.Save(player);
        PlayerPersistence.NextSpawnId = spawnId;
        SceneFader.LoadScene(targetScene);   // pindah dengan animasi fade
    }
}