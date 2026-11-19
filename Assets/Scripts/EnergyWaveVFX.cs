using UnityEngine;

public class EnergyWaveVFX : MonoBehaviour
{
    public int direction = 1;
    public bool isFrozen;

    private float rippleTimer;
    private Material lineMat;
    private TrailRenderer topTrail;
    private TrailRenderer bottomTrail;
    private const float TrailDuration = 0.14f;

    public void Init(int dir, bool hasCustomPrefab)
    {
        direction = dir >= 0 ? 1 : -1;
        lineMat = new Material(Shader.Find("Sprites/Default"));

        if (!hasCustomPrefab)
            BuildTaperedCrescent();

        BuildAirBendQuad();

        topTrail = CreateTipTrail("TopWindTrail", new Vector3(-0.1f * direction, 0.52f, 0f));
        bottomTrail = CreateTipTrail("BottomWindTrail", new Vector3(-0.1f * direction, -0.52f, 0f));
    }

    void Update()
    {
        if (topTrail != null)
        {
            topTrail.emitting = !isFrozen;
            topTrail.time = isFrozen ? 999f : TrailDuration;
        }
        if (bottomTrail != null)
        {
            bottomTrail.emitting = !isFrozen;
            bottomTrail.time = isFrozen ? 999f : TrailDuration;
        }

        if (isFrozen) return;

        rippleTimer -= Time.deltaTime;
        if (rippleTimer <= 0f)
        {
            rippleTimer = 0.045f;
            SpawnAirPressureArc(transform.position);
        }
    }

    void BuildTaperedCrescent()
    {
        GameObject core = new GameObject("SlashCore");
        core.transform.SetParent(transform, false);

        LineRenderer lr = core.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = lineMat;
        lr.sortingOrder = 25;

        int pts = 15;
        lr.positionCount = pts;
        lr.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.01f),
            new Keyframe(0.5f, 0.24f),
            new Keyframe(1f, 0.01f)
        );

        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.2f, 0.85f, 1f), 0f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(new Color(0.2f, 0.85f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.15f, 0f),
                new GradientAlphaKey(1f, 0.5f),
                new GradientAlphaKey(0.15f, 1f)
            }
        );
        lr.colorGradient = grad;

        for (int i = 0; i < pts; i++)
        {
            float t = (i / (float)(pts - 1)) * 2f - 1f;
            float x = (1f - t * t) * 0.38f * direction;
            float y = t * 0.58f;
            lr.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    void BuildAirBendQuad()
    {
        Shader bendShader = Shader.Find("Custom/2D_AirBend");
        if (bendShader == null) return;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "AirBendLens";
        quad.transform.SetParent(transform, false);
        quad.transform.localPosition = new Vector3(-0.08f * direction, 0f, 0f);
        quad.transform.localScale = new Vector3(1.55f, 1.75f, 1f);

        MeshRenderer mr = quad.GetComponent<MeshRenderer>();
        mr.sortingOrder = 24;
        Material bendMat = new Material(bendShader);
        bendMat.SetFloat("_Dir", direction);
        mr.material = bendMat;
    }

    TrailRenderer CreateTipTrail(string trailName, Vector3 localOffset)
    {
        GameObject tObj = new GameObject(trailName);
        tObj.transform.SetParent(transform, false);
        tObj.transform.localPosition = localOffset;

        TrailRenderer tr = tObj.AddComponent<TrailRenderer>();
        tr.material = lineMat;
        tr.sortingOrder = 23;
        tr.time = TrailDuration;
        tr.minVertexDistance = 0.05f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.06f), new Keyframe(1f, 0f));

        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(0.6f, 0.95f, 1f), 0f), new GradientColorKey(new Color(0.2f, 0.6f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.65f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        tr.colorGradient = g;
        return tr;
    }

    void SpawnAirPressureArc(Vector3 spawnPos)
    {
        GameObject arcObj = new GameObject("AirPressureArc");
        arcObj.transform.position = spawnPos;
        Destroy(arcObj, 0.14f);

        LineRenderer lr = arcObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = lineMat;
        lr.sortingOrder = 22;
        lr.startColor = new Color(0.75f, 0.95f, 1f, 0.35f);
        lr.endColor = new Color(0.75f, 0.95f, 1f, 0f);

        int pts = 11;
        lr.positionCount = pts;
        lr.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 0.05f),
            new Keyframe(1f, 0f)
        );

        for (int i = 0; i < pts; i++)
        {
            float t = (i / (float)(pts - 1)) * 2f - 1f;
            float x = (1f - t * t) * 0.32f * direction;
            float y = t * 0.52f;
            lr.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    public void SpawnImpactBurst(Vector3 hitPos)
    {
        GameObject burst = new GameObject("WaveImpactBurst");
        burst.transform.position = hitPos;

        LineRenderer lr = burst.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = 24;
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;
        lr.material = lineMat;
        lr.startColor = Color.white;
        lr.endColor = Color.cyan;
        lr.sortingOrder = 26;

        for (int i = 0; i < 24; i++)
        {
            float a = (i / 24f) * Mathf.PI * 2f;
            lr.SetPosition(i, hitPos + new Vector3(Mathf.Cos(a) * 0.55f, Mathf.Sin(a) * 0.55f, 0f));
        }
        Destroy(burst, 0.12f);
    }
}