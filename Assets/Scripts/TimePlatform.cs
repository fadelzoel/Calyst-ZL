using UnityEngine;

/// <summary>Chrono-Spear platform: hangs in the air, and only glides forward while time is stopped.</summary>
public class TimePlatform : MonoBehaviour
{
    static Sprite sprite;

    PlayerSkills owner;
    int dir = 1;
    float age;
    Rigidbody2D rb;
    BoxCollider2D col;
    Rigidbody2D playerRb;
    Collider2D playerCol;

    public static TimePlatform Spawn(PlayerSkills owner, Vector2 pos, int dir)
    {
        GameObject go = new GameObject("TimePlatform");
        go.transform.position = pos;
        go.transform.localScale = new Vector3(owner.platformSize.x, owner.platformSize.y, 1f);

        int layer = LayerMask.NameToLayer(owner.platformLayerName);
        if (layer >= 0) go.layer = layer;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSprite();
        sr.color = new Color(0.4f, 0.8f, 1f, 0.85f);
        sr.sortingOrder = 5;

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
        box.usedByEffector = true;

        PlatformEffector2D eff = go.AddComponent<PlatformEffector2D>();
        eff.useOneWay = true;
        eff.surfaceArc = 170f;

        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;

        TimePlatform tp = go.AddComponent<TimePlatform>();
        tp.owner = owner;
        tp.dir = dir;
        tp.rb = body;
        tp.col = box;
        tp.playerRb = owner.GetComponent<Rigidbody2D>();
        tp.playerCol = owner.GetComponent<Collider2D>();
        return tp;
    }

    static Sprite GetSprite()
    {
        if (sprite != null) return sprite;
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return sprite;
    }

    void Update()
    {
        if (owner == null) { Destroy(gameObject); return; }

        age += Time.unscaledDeltaTime;
        if (age >= owner.platformLifetime) Destroy(gameObject);
    }

    void FixedUpdate()
    {
        // Frozen in the air until time stops, then it glides forward like a spear
        if (owner == null || !TimeStop.Active) return;
        if (Blocked()) return;

        Vector2 delta = new Vector2(dir * owner.platformSpeed * Time.fixedDeltaTime, 0f);

        // Carry the player if she is standing on it
        if (RiderOnTop() && playerRb != null)
            playerRb.position += delta;

        rb.MovePosition(rb.position + delta);
    }

    bool Blocked()
    {
        Vector2 size = owner.platformSize;
        Vector2 c = rb.position + new Vector2(dir * (size.x * 0.5f + 0.06f), 0f);

        foreach (Collider2D h in Physics2D.OverlapBoxAll(c, new Vector2(0.08f, size.y * 0.8f), 0f, owner.StickLayers))
        {
            if (h.isTrigger || h.gameObject == gameObject) continue;
            if (h.GetComponentInParent<TimePlatform>() != null) continue;
            return true;
        }
        return false;
    }

    bool RiderOnTop()
    {
        if (playerCol == null || playerRb == null) return false;

        Bounds pb = playerCol.bounds;
        Bounds b = col.bounds;

        bool overlapX = pb.max.x > b.min.x && pb.min.x < b.max.x;
        bool onTop = Mathf.Abs(pb.min.y - b.max.y) < 0.12f;
        return overlapX && onTop && playerRb.linearVelocity.y <= 0.1f;
    }
}