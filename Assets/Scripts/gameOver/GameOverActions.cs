using UnityEngine;

/// <summary>
/// Pasang di objek mana pun di dalam prefab layar Game Over (misal di root Canvas),
/// lalu sambungkan OnClick tombol ke method di bawah:
///   Tombol Retry  -> GameOverActions.Retry
///   Tombol Menu   -> GameOverActions.LoadScene (isi nama scene menu di kolom string)
///   Tombol Keluar -> GameOverActions.QuitGame
/// </summary>
public class GameOverActions : MonoBehaviour
{
    public void Retry()
    {
        GameOverManager.Retry();
    }

    public void LoadScene(string sceneName)
    {
        GameOverManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
