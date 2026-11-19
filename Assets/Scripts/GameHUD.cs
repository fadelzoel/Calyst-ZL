using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    [Header("Player References")]
    public PlayerController player;
    public PlayerSkills skills;

    [Header("HP Bar")]
    public Image hpBarFill;
    [Tooltip("Optional: assign HPbar (sharp) and HPbarFull (flat) to auto-switch when damaged.")]
    public Sprite hpSharpSprite;
    public Sprite hpFlatSprite;

    [Header("Overload Clock")]
    public RectTransform clockArrow;

    [Header("Sanity Strip (8 Sprites: SaneEmpty -> Sane7Bar)")]
    public Image saneBarImage;
    [Tooltip("Element 0 = SaneEmpty, Element 1 = Sane1Bar ... Element 7 = Sane7Bar")]
    public Sprite[] saneSprites = new Sprite[8];

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (skills == null && player != null) skills = player.GetComponent<PlayerSkills>();
    }

    void Update()
    {
        if (player != null && hpBarFill != null)
        {
            float hpRatio = player.maxHealth > 0f
                ? Mathf.Clamp01(player.currentHealth / player.maxHealth)
                : 0f;

            hpBarFill.fillAmount = hpRatio;

            if (hpSharpSprite != null && hpFlatSprite != null)
                hpBarFill.sprite = hpRatio >= 0.999f ? hpSharpSprite : hpFlatSprite;
        }

        if (skills != null)
        {
            // Clockwise rotation from 0° (0% Overload) to -360° (100% Overload)
            if (clockArrow != null)
            {
                float angle = -(skills.overloadMeter / 100f) * 360f;
                clockArrow.localEulerAngles = new Vector3(0f, 0f, angle);
            }

            // Swap SaneEmpty -> Sane1Bar..Sane7Bar directly
            if (saneBarImage != null && saneSprites != null && saneSprites.Length > 0)
            {
                int idx = Mathf.Clamp(skills.sanity, 0, saneSprites.Length - 1);
                if (saneSprites[idx] != null)
                    saneBarImage.sprite = saneSprites[idx];
            }
        }
    }
}