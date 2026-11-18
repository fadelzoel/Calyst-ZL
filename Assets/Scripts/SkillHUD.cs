using UnityEngine;

public class SkillHUD : MonoBehaviour
{
    public PlayerSkills skills;
    public PlayerController player;

    void Start()
    {
        if (skills == null) skills = FindFirstObjectByType<PlayerSkills>();
        if (player == null) player = FindFirstObjectByType<PlayerController>();
    }

    void OnGUI()
    {
        if (skills == null || player == null) return;

        float x = 20f, y = 20f;

        // Health
        Label(x, y, "HP");
        Bar(new Rect(x + 70, y, 200, 16), player.currentHealth / player.maxHealth, new Color(0.9f, 0.2f, 0.2f));

        // Overload
        y += 24f;
        Label(x, y, "OVERLOAD");
        Color oc = skills.overloadMeter >= 80f ? new Color(1f, 0.3f, 0.1f) : new Color(1f, 0.75f, 0.1f);
        Bar(new Rect(x + 70, y, 200, 16), skills.overloadMeter / 100f, oc);

        // Sanity pips
        y += 24f;
        Label(x, y, "SANITY");
        for (int i = 0; i < skills.maxSanity; i++)
        {
            Color c = i < skills.sanity ? new Color(0.7f, 0.2f, 0.9f) : new Color(0.2f, 0.2f, 0.2f, 0.8f);
            Box(new Rect(x + 70 + i * 29, y, 25, 16), c);
        }

        if (skills.currentWeapon == WeaponType.ChronoSpear)
        {
            y += 24f;
            Label(x, y, "SPEAR");
            Box(new Rect(x + 70, y, 25, 16), skills.spearAmmo > 0 ? new Color(0.4f, 0.8f, 1f) : new Color(0.2f, 0.2f, 0.2f, 0.8f));
        }
    }

    void Label(float x, float y, string text)
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y - 2, 80, 20), text);
    }

    void Box(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void Bar(Rect r, float t, Color fill)
    {
        Box(r, new Color(0f, 0f, 0f, 0.6f));
        Box(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(t), r.height - 4), fill);
    }
}