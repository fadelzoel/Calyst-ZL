using UnityEngine;

public class SkillHUD : MonoBehaviour
{
    public PlayerSkills skills;
    public PlayerController player;

    [Header("Layout")]
    [Tooltip("Tinggi layar acuan. HUD otomatis membesar kalau layarmu lebih tinggi dari ini.")]
    public float referenceHeight = 540f;
    [Tooltip("Pengali ukuran tambahan. Naikkan kalau HUD masih terasa kecil.")]
    [Range(0.5f, 4f)] public float uiScale = 1.5f;
    public float margin = 20f;
    public float labelWidth = 100f;
    public float barWidth = 220f;
    public float barHeight = 18f;
    public float rowSpacing = 26f;
    public int fontSize = 13;

    private GUIStyle labelStyle;
    private GUIStyle valueStyle;

    void Start()
    {
        if (skills == null) skills = FindFirstObjectByType<PlayerSkills>();
        if (player == null) player = FindFirstObjectByType<PlayerController>();
    }

    void OnGUI()
    {
        if (skills == null || player == null) return;

        BuildStyles();

        // Skala HUD mengikuti tinggi layar
        float s = Mathf.Max(1f, Screen.height / referenceHeight) * uiScale;
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

        float x = margin, y = margin;
        float barX = x + labelWidth;

        // Health
        Label(new Rect(x, y, labelWidth, barHeight), "HP");
        Bar(new Rect(barX, y, barWidth, barHeight),
            player.currentHealth / player.maxHealth,
            new Color(0.9f, 0.2f, 0.2f),
            Mathf.CeilToInt(player.currentHealth) + " / " + Mathf.CeilToInt(player.maxHealth));

        // Overload
        y += rowSpacing;
        Label(new Rect(x, y, labelWidth, barHeight), "OVERLOAD");
        Color oc = skills.overloadMeter >= 80f ? new Color(1f, 0.3f, 0.1f) : new Color(1f, 0.75f, 0.1f);
        Bar(new Rect(barX, y, barWidth, barHeight),
            skills.overloadMeter / 100f,
            oc,
            Mathf.RoundToInt(skills.overloadMeter) + "%");

        // Sanity pips
        y += rowSpacing;
        Label(new Rect(x, y, labelWidth, barHeight), "SANITY");
        float pipGap = 4f;
        float pipW = (barWidth - pipGap * (skills.maxSanity - 1)) / Mathf.Max(1, skills.maxSanity);
        for (int i = 0; i < skills.maxSanity; i++)
        {
            Color c = i < skills.sanity ? new Color(0.7f, 0.2f, 0.9f) : new Color(0.15f, 0.15f, 0.15f, 0.85f);
            Box(new Rect(barX + i * (pipW + pipGap), y, pipW, barHeight), c);
        }

        // Spear ammo
        if (skills.currentWeapon == WeaponType.ChronoSpear)
        {
            y += rowSpacing;
            Label(new Rect(x, y, labelWidth, barHeight), "SPEAR");
            Box(new Rect(barX, y, barHeight * 1.5f, barHeight),
                skills.spearAmmo > 0 ? new Color(0.4f, 0.8f, 1f) : new Color(0.15f, 0.15f, 0.15f, 0.85f));
        }

        GUI.matrix = oldMatrix;
    }

    void BuildStyles()
    {
        if (labelStyle != null) return;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = fontSize;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleLeft;
        labelStyle.normal.textColor = Color.white;

        valueStyle = new GUIStyle(labelStyle);
        valueStyle.alignment = TextAnchor.MiddleCenter;
        valueStyle.fontSize = fontSize - 1;
    }

    // Teks dengan bayangan hitam supaya terbaca di background terang (langit)
    void ShadowText(Rect r, string text, GUIStyle style)
    {
        GUI.color = Color.black;
        GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
        GUI.color = Color.white;
        GUI.Label(r, text, style);
    }

    void Label(Rect r, string text)
    {
        ShadowText(r, text, labelStyle);
    }

    void Box(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void Bar(Rect r, float t, Color fill, string valueText)
    {
        Box(r, new Color(0f, 0f, 0f, 0.65f));
        Box(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(t), r.height - 4), fill);
        if (!string.IsNullOrEmpty(valueText))
            ShadowText(r, valueText, valueStyle);
    }
}
