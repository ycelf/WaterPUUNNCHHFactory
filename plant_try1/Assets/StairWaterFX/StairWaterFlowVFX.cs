using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A visual-only waterfall guided by ordered points, independent of gameplay water.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class StairWaterFlowVFX : MonoBehaviour
{
    [Header("路径：Path 的直接子物体，按 Hierarchy 从上到下的顺序流动")]
    [SerializeField] private Transform pathRoot;
    [Min(0.05f)] [SerializeField] private float width = 1.2f;
    [Min(0f)] [SerializeField] private float surfaceOffset = 0.035f;

    [Header("水带外观")]
    [SerializeField] private Shader flowShader;
    [SerializeField] private Color waterColor = new Color(0.16f, 0.62f, 0.68f, 0.78f);
    [SerializeField] private Color foamColor = new Color(0.85f, 0.98f, 1f, 0.85f);
    [Range(0f, 1f)] [SerializeField] private float foamAmount = 0.55f;
    [Min(0.1f)] [SerializeField] private float textureScale = 2f;

    [Header("播放")]
    [SerializeField] private bool flowEnabled = true;
    [Min(0.05f)] [SerializeField] private float flowSpeed = 2.5f;
    [Min(0.01f)] [SerializeField] private float fadeSeconds = 0.35f;

    [Header("沿水带移动的泡沫粒子")]
    [Range(0f, 200f)] [SerializeField] private float foamPerSecond = 35f;
    [Range(16, 2000)] [SerializeField] private int maxFoamParticles = 400;
    [Min(0.01f)] [SerializeField] private float foamSize = 0.16f;

    [Header("落差底部水花：自动寻找从下落转为平缓的位置")]
    [SerializeField] private bool landingSplashes = true;
    [Min(0.05f)] [SerializeField] private float minimumDrop = 0.3f;
    [Range(0f, 100f)] [SerializeField] private float splashPerLanding = 12f;
    [Min(0f)] [SerializeField] private float splashUpSpeed = 1.1f;

    private GameObject generatedRoot;
    private Mesh ribbonMesh;
    private MeshRenderer ribbonRenderer;
    private Material ribbonMaterial;
    private Material particleMaterial;
    private ParticleSystem foam;
    private ParticleSystem splashes;
    private ParticleSystem.Particle[] buffer;
    private Vector3[] points;
    private Vector3[] sides;
    private Vector3[] normals;
    private float[] distances;
    private float[] splashClocks;
    private bool[] landing;
    private float totalLength;
    private float foamClock;
    private float visibility;
    private float flowDistance;
    private bool rebuildPending = true;

    public bool IsFlowing => flowEnabled;
    public void StartFlow() => flowEnabled = true;
    public void StopFlow() => flowEnabled = false;
    public void ToggleFlow() => flowEnabled = !flowEnabled;
    public void SetFlowEnabled(bool value) => flowEnabled = value;

    private void Reset()
    {
        flowShader = Shader.Find("WaterFX/Stair Flow");
        pathRoot = transform.Find("Path");
    }

    private void OnEnable()
    {
        visibility = flowEnabled ? 1f : 0f;
        rebuildPending = true;
    }

    private void OnValidate() => rebuildPending = true;

    [ContextMenu("Rebuild Flow")]
    public void RebuildFlow() => rebuildPending = true;

    [ContextMenu("Create Example Path")]
    private void CreateExamplePath()
    {
        if (pathRoot != null && pathRoot.childCount > 0)
        {
            Debug.LogWarning("Path 已有路径点，保留现有内容。直接移动或复制这些点即可。", this);
            return;
        }

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Create water path");
#endif
        if (pathRoot == null)
        {
            GameObject path = new GameObject("Path");
            path.transform.SetParent(transform, false);
            pathRoot = path.transform;
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(path, "Create water path");
#endif
        }

        Vector3[] example = {
            new Vector3(0, 2, 0), new Vector3(0, 2, 2),
            new Vector3(0, 1, 2.15f), new Vector3(0, 1, 4),
            new Vector3(0, 0, 4.15f), new Vector3(0, 0, 6)
        };
        for (int i = 0; i < example.Length; i++)
        {
            GameObject point = new GameObject($"Point_{i:00}");
            point.transform.SetParent(pathRoot, false);
            point.transform.localPosition = example[i];
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(point, "Create water path point");
#endif
        }
        rebuildPending = true;
    }

    private bool PathChanged()
    {
        if (pathRoot == null) return points != null;
        if (points == null || pathRoot.childCount != points.Length) return true;
        for (int i = 0; i < points.Length; i++)
            if ((pathRoot.GetChild(i).position - points[i]).sqrMagnitude > 0.000001f) return true;
        return false;
    }

    private void Update()
    {
        if (rebuildPending || PathChanged())
        {
            rebuildPending = false;
            BuildFlow();
        }
        if (ribbonMaterial == null) return;

        bool playing = Application.IsPlaying(gameObject);
        if (playing)
        {
            visibility = Mathf.MoveTowards(visibility, flowEnabled ? 1f : 0f,
                Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
            flowDistance += Time.deltaTime * Mathf.Max(0.05f, flowSpeed);
        }
        else visibility = flowEnabled ? 1f : 0f;

        ribbonRenderer.enabled = visibility > 0.001f;
        ribbonMaterial.SetFloat("_Visibility", visibility);
        ribbonMaterial.SetFloat("_FlowDistance", flowDistance);
        particleMaterial.SetFloat("_Visibility", visibility);

        if (!playing || !flowEnabled) return;
        EmitFoam();
        EmitLandingSplashes();
    }

    private void LateUpdate()
    {
        if (!Application.IsPlaying(gameObject) || foam == null || totalLength <= 0f) return;
        int count = foam.GetParticles(buffer);
        for (int i = 0; i < count; i++)
        {
            float progress = 1f - buffer[i].remainingLifetime / buffer[i].startLifetime;
            float distance = Mathf.Clamp01(progress) * totalLength;
            int segment = 0;
            while (segment < points.Length - 2 && distances[segment + 1] < distance) segment++;
            float t = Mathf.InverseLerp(distances[segment], distances[segment + 1], distance);
            Vector3 side = Vector3.Lerp(sides[segment], sides[segment + 1], t).normalized;
            Vector3 normal = Vector3.Lerp(normals[segment], normals[segment + 1], t).normalized;
            float lane = (buffer[i].randomSeed % 10000u) / 9999f - 0.5f;
            buffer[i].position = Vector3.Lerp(points[segment], points[segment + 1], t)
                + side * (lane * width * 0.9f) + normal * (surfaceOffset + 0.035f);
            buffer[i].velocity = Vector3.zero;
            Color color = foamColor;
            color.a *= Mathf.Clamp01(progress * 15f) * Mathf.Clamp01((1f - progress) * 12f);
            buffer[i].startColor = color;
        }
        foam.SetParticles(buffer, count);
    }

    private void BuildFlow()
    {
        ReleaseGenerated();
        if (pathRoot == null || pathRoot == transform || pathRoot.childCount < 2) return;
        int count = pathRoot.childCount;
        points = new Vector3[count];
        sides = new Vector3[count];
        normals = new Vector3[count];
        distances = new float[count];
        landing = new bool[count];
        splashClocks = new float[count];
        totalLength = 0f;

        for (int i = 0; i < count; i++) points[i] = pathRoot.GetChild(i).position;
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                float length = Vector3.Distance(points[i - 1], points[i]);
                if (length < 0.005f)
                {
                    Debug.LogWarning("水流路径中有重合点，请拉开距离。", this);
                    return;
                }
                totalLength += length;
            }
            distances[i] = totalLength;
        }

        if (flowShader == null) flowShader = Shader.Find("WaterFX/Stair Flow");
        if (flowShader == null)
        {
            Debug.LogError("请给 Flow Shader 指定 StairWaterFlow.shader。", this);
            return;
        }

        Vector3 previousSide = transform.right;
        // Choose the first horizontal direction, including when the route starts with a drop.
        for (int i = 0; i < count - 1; i++)
        {
            Vector3 candidate = Vector3.Cross(Vector3.up, points[i + 1] - points[i]);
            if (candidate.sqrMagnitude < 0.001f) continue;
            previousSide = candidate.normalized;
            break;
        }
        for (int i = 0; i < count; i++)
        {
            Vector3 tangent = i == 0 ? points[1] - points[0] : i == count - 1
                ? points[i] - points[i - 1] : (points[i] - points[i - 1]).normalized
                    + (points[i + 1] - points[i]).normalized;
            tangent.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, tangent);
            if (side.sqrMagnitude < 0.001f) side = previousSide;
            else side.Normalize();
            if (Vector3.Dot(side, previousSide) < 0f) side = -side;
            sides[i] = side;
            normals[i] = Vector3.Cross(tangent, side).normalized;
            previousSide = side;
            landing[i] = i > 0 && points[i - 1].y - points[i].y >= minimumDrop
                && (i == count - 1 || Mathf.Abs(points[i + 1].y - points[i].y) < 0.25f);
        }

        generatedRoot = new GameObject("Generated Stair Water") { hideFlags = HideFlags.HideAndDontSave };
        // Convert world path positions into the generated mesh's local space below.
        generatedRoot.transform.SetParent(transform, false);
        generatedRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Vector3[] vertices = new Vector3[count * 2];
        Vector2[] uv = new Vector2[count * 2];
        Color[] colors = new Color[count * 2];
        int[] triangles = new int[(count - 1) * 6];
        for (int i = 0; i < count; i++)
        {
            Vector3 center = points[i] + normals[i] * surfaceOffset;
            vertices[i * 2] = generatedRoot.transform.InverseTransformPoint(center - sides[i] * width * 0.5f);
            vertices[i * 2 + 1] = generatedRoot.transform.InverseTransformPoint(center + sides[i] * width * 0.5f);
            uv[i * 2] = new Vector2(0f, distances[i]);
            uv[i * 2 + 1] = new Vector2(1f, distances[i]);
            colors[i * 2] = colors[i * 2 + 1] = Color.white;
            if (i == count - 1) continue;
            int v = i * 2, k = i * 6;
            triangles[k] = v; triangles[k + 1] = v + 2; triangles[k + 2] = v + 1;
            triangles[k + 3] = v + 1; triangles[k + 4] = v + 2; triangles[k + 5] = v + 3;
        }

        ribbonMesh = new Mesh { name = "Stair water ribbon", hideFlags = HideFlags.HideAndDontSave };
        ribbonMesh.vertices = vertices; ribbonMesh.uv = uv; ribbonMesh.colors = colors;
        ribbonMesh.triangles = triangles;
        ribbonMesh.RecalculateBounds();
        generatedRoot.AddComponent<MeshFilter>().sharedMesh = ribbonMesh;
        ribbonRenderer = generatedRoot.AddComponent<MeshRenderer>();
        ribbonRenderer.shadowCastingMode = ShadowCastingMode.Off;
        ribbonRenderer.receiveShadows = false;
        ribbonMaterial = new Material(flowShader) { hideFlags = HideFlags.HideAndDontSave };
        ribbonMaterial.SetColor("_WaterColor", waterColor);
        ribbonMaterial.SetColor("_FoamColor", foamColor);
        ribbonMaterial.SetFloat("_FoamAmount", foamAmount);
        ribbonMaterial.SetFloat("_TextureScale", textureScale);
        ribbonRenderer.sharedMaterial = ribbonMaterial;
        particleMaterial = new Material(flowShader) { hideFlags = HideFlags.HideAndDontSave };
        particleMaterial.SetFloat("_ParticleMode", 1f);
        particleMaterial.renderQueue = 3001;

        if (Application.IsPlaying(gameObject))
        {
            foam = CreateParticles("Flow foam", maxFoamParticles, 0f);
            splashes = CreateParticles("Landing splashes", 256, 0.65f);
            buffer = new ParticleSystem.Particle[maxFoamParticles];
        }
        foamClock = 0f;
    }

    private ParticleSystem CreateParticles(string objectName, int maximum, float gravity)
    {
        GameObject child = new GameObject(objectName) { hideFlags = HideFlags.HideAndDontSave };
        child.SetActive(false);
        child.transform.SetParent(generatedRoot.transform, false);
        ParticleSystem system = child.AddComponent<ParticleSystem>();
        var main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.maxParticles = Mathf.Max(16, maximum);
        main.startSpeed = 0f;
        main.gravityModifier = gravity;
        var emission = system.emission; emission.enabled = false;
        var shape = system.shape; shape.enabled = false;
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        child.SetActive(true);
        system.Play(false);
        return system;
    }

    private void EmitFoam()
    {
        if (foam == null) return;
        foamClock += Time.deltaTime * foamPerSecond;
        int count = Mathf.Min(Mathf.FloorToInt(foamClock), 32);
        foamClock -= Mathf.Floor(foamClock);
        var particle = new ParticleSystem.EmitParams {
            position = points[0], velocity = Vector3.zero,
            startLifetime = totalLength / Mathf.Max(0.05f, flowSpeed),
            startSize = foamSize, startColor = foamColor
        };
        if (count > 0) foam.Emit(particle, count);
    }

    private void EmitLandingSplashes()
    {
        if (!landingSplashes || splashes == null) return;
        for (int i = 1; i < points.Length; i++)
        {
            if (!landing[i]) continue;
            splashClocks[i] += Time.deltaTime * splashPerLanding;
            int count = Mathf.Min(Mathf.FloorToInt(splashClocks[i]), 8);
            splashClocks[i] -= Mathf.Floor(splashClocks[i]);
            for (int j = 0; j < count; j++)
            {
                var particle = new ParticleSystem.EmitParams {
                    position = points[i] + Vector3.up * 0.07f + sides[i] * Random.Range(-width * 0.4f, width * 0.4f),
                    velocity = Vector3.up * Random.Range(splashUpSpeed * 0.5f, splashUpSpeed)
                        + sides[i] * Random.Range(-0.45f, 0.45f),
                    startLifetime = Random.Range(0.2f, 0.4f),
                    startSize = Random.Range(foamSize * 0.4f, foamSize * 0.9f),
                    startColor = foamColor
                };
                splashes.Emit(particle, 1);
            }
        }
    }

    private void OnDisable() => ReleaseGenerated();
    private void OnDestroy() => ReleaseGenerated();

    private void ReleaseGenerated()
    {
        if (generatedRoot != null) generatedRoot.SetActive(false);
        Release(generatedRoot); Release(ribbonMesh); Release(ribbonMaterial); Release(particleMaterial);
        generatedRoot = null; ribbonMesh = null; ribbonRenderer = null;
        ribbonMaterial = null; particleMaterial = null; foam = null; splashes = null;
        points = null; buffer = null;
    }

    private void Release(Object item)
    {
        if (item == null) return;
        if (Application.IsPlaying(gameObject)) Destroy(item);
        else DestroyImmediate(item);
    }

    private void OnDrawGizmosSelected()
    {
        if (pathRoot == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < pathRoot.childCount; i++)
        {
            Vector3 point = pathRoot.GetChild(i).position;
            Gizmos.DrawWireSphere(point, 0.08f);
            if (i > 0) Gizmos.DrawLine(pathRoot.GetChild(i - 1).position, point);
        }
    }
}
