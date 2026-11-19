using UnityEngine;

/// <summary>Thrown Chrono-Spear. Flies, sticks in walls or enemies, gets picked up or auto-returns.</summary>
public class SpearProjectile : MonoBehaviour
{
    enum State { Flying, StuckWall, StuckEnemy, Retrievable }

    PlayerSkills owner;
    Transform player;
    LineRenderer lr;
    State state = State.Flying;
    int dir = 1;
    float flown;
    float life;
    bool thrownDuringStop;
    EnemyStats target;
    Vector3 targetOffset;

    const float TipLength = 0.6f;

    public void Init(PlayerSkills o, int facing)
    {
        owner = o;
        player = o.transform;
        dir = facing;
        thrownDuringStop = TimeStop.Active;

        transform.localScale = new Vector3(dir, 1f, 1f);

        lr = gameObject.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = false;
        lr.startWidth = 0.1f;
        lr.endWidth = 0.1f;
        lr.sortingOrder = 6;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.SetPosition(0, new Vector3(-TipLength, 0f, 0f));
        lr.SetPosition(1, new Vector3(TipLength, 0f, 0f));
        SetColor(Color.white, Color.cyan);
    }

    void Update()
    {
        if (owner == null) { Destroy(gameObject); return; }

        life += Time.unscaledDeltaTime;
        if (life >= owner.spearAutoReturnTime) { Return(); return; }

        switch (state)
        {
            case State.Flying: Fly(); break;
            case State.StuckEnemy: FollowEnemy(); break;
        }

        // Walk over it to pick it up (not while lodged in a living enemy)
        if ((state == State.StuckWall || state == State.Retrievable)
            && Vector2.Distance(player.position, transform.position) <= owner.spearPickupRadius)
            Return();
    }

    void Fly()
    {
        if (!TimeStop.Active) thrownDuringStop = false;

        float step = owner.spearSpeed * Time.deltaTime;

        if (TimeStop.Active)
        {
            // Thrown before the stop: frozen with everything else
            if (!thrownDuringStop) return;

            // Thrown during the stop: travels a few inches, then hangs until time resumes
            float left = owner.spearTimeStopHangDistance - flown;
            if (left <= 0f) return;
            step = Mathf.Min(step, left);
        }

        transform.position += Vector3.right * (dir * step);
        flown += step;

        Vector2 tip = transform.position + Vector3.right * (dir * TipLength);

        // Enemy hit
        Collider2D e = Physics2D.OverlapCircle(tip, 0.3f, owner.enemyLayers);
        if (e != null)
        {
            EnemyStats es = e.GetComponentInParent<EnemyStats>();
            if (es != null && !es.IsDead)
            {
                HitEnemy(es);
                return;
            }
        }

        // Wall / ground hit: stays stuck until retrieved
        if (Physics2D.OverlapCircle(tip, 0.2f, owner.StickLayers))
        {
            state = State.StuckWall;
            SetColor(Color.white, Color.yellow);
            return;
        }

        if (flown >= owner.spearMaxRange)
            Return();
    }

    void HitEnemy(EnemyStats es)
    {
        target = es;
        targetOffset = transform.position - es.transform.position;

        es.TakeDamage(owner.spearDamage);
        es.Stun(owner.spearFreezeTime);

        if (es.IsDead)
        {
            Return(); // killed it: spear goes straight back to the player
        }
        else
        {
            state = State.StuckEnemy;
            SetColor(Color.white, Color.red); // lodged: can't be taken back yet
        }
    }

    void FollowEnemy()
    {
        // Only retrievable once the enemy is dead (or gone)
        if (target == null || target.IsDead)
        {
            Return(); // enemy died (from anything) or vanished: spear returns
            return;
        }

        transform.position = target.transform.position + targetOffset;
    }

    bool returned;

    void Return()
    {
        if (returned) return;
        returned = true;
        if (owner != null) owner.OnSpearReturned();
        Destroy(gameObject);
    }

    void SetColor(Color a, Color b)
    {
        lr.startColor = a;
        lr.endColor = b;
    }
}