using UnityEngine;

/// <summary>Chrono-Spear platform: hangs in the air, and only glides forward while time is stopped.</summary>
public class TimePlatform : MonoBehaviour
{
    PlayerSkills owner;
    int dir = 1;
    float age;
    Rigidbody2D rb;
    BoxCollider2D col;
    Rigidbody2D playerRb;
    Collider2D playerCol;

  public static TimePlatform Spawn(PlayerSkills owner, Vector2 pos, int dir)
    {
        GameObject go;

        if (owner.platformPrefab != null)
        {
            go = Instantiate(owner.platformPrefab, pos, Quaternion.identity);
        }
        else
        {
            go = new GameObject("TimePlatform");
            go.transform.position = pos;
        }

        // Apply platformSize accurately even if the prefab sprite isn't 1x1 Unity units
        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        Vector2 baseSize = (sr != null && sr.sprite != null) ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        float scaleX = (owner.platformSize.x / baseSize.x) * dir;
        float scaleY = owner.platformSize.y / baseSize.y;
        go.transform.localScale = new Vector3(scaleX, scaleY, 1f);

        int layer = LayerMask.NameToLayer(owner.platformLayerName);
        if (layer >= 0) go.layer = layer;

        BoxCollider2D box = go.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = go.AddComponent<BoxCollider2D>();
            box.size = baseSize;
        }
        box.usedByEffector = true;

        PlatformEffector2D eff = go.GetComponent<PlatformEffector2D>();
        if (eff == null)
        {
            eff = go.AddComponent<PlatformEffector2D>();
            eff.useOneWay = true;
            eff.surfaceArc = 170f;
        }

        Rigidbody2D body = go.GetComponent<Rigidbody2D>();
        if (body == null)
            body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;

        TimePlatform tp = go.GetComponent<TimePlatform>();
        if (tp == null)
            tp = go.AddComponent<TimePlatform>();

        tp.owner = owner;
        tp.dir = dir;
        tp.rb = body;
        tp.col = box;
        tp.playerRb = owner.GetComponent<Rigidbody2D>();
        tp.playerCol = owner.GetComponent<Collider2D>();
        return tp;
    }
    void Update()
    {
        if (owner == null) { Destroy(gameObject); return; }

        age += Time.unscaledDeltaTime;
        if (age >= owner.platformLifetime) Destroy(gameObject);
    }

    void FixedUpdate()
    {
        // Frozen in the air until time stops and Caly finishes her cast animation
        if (owner == null || !TimeStop.Active || owner.IsTimeStopCasting) return;
        if (Blocked()) return;

        Vector2 delta = new Vector2(dir * owner.platformSpeed * Time.fixedDeltaTime, 0f);

        // Carry the player if she is standing on it
        if (RiderOnTop() && playerRb != null)
            playerRb.position += delta;

        rb.MovePosition(rb.position + delta);
    }

    bool Blocked()
    {
        Vector2 size = col != null ? (Vector2)col.bounds.size : owner.platformSize;
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