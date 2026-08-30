using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class WaterPunchWaterSafety : MonoBehaviour
{
    [Header("Water Punch Safety")]
    [Min(0.1f)]
    [SerializeField] private float safetyDuration = 0.45f;

    [Min(0.5f)]
    [SerializeField] private float safetyZoneLength = 2.6f;

    [Min(0.25f)]
    [SerializeField] private float safetyZoneHalfWidth = 1.15f;

    [Min(0.25f)]
    [SerializeField] private float safetyZoneHalfHeight = 1.25f;

    [Min(0f)]
    [SerializeField] private float punchForwardOffset = 0.7f;

    [Header("Punch Feedback")]
    [Min(1)]
    [SerializeField] private int splashParticleCount = 18;

    [Min(0.1f)]
    [SerializeField] private float rippleDuration = 0.38f;

    [Min(0.1f)]
    [SerializeField] private float rippleStartRadius = 0.2f;

    [Min(0.1f)]
    [SerializeField] private float rippleEndRadius = 1.35f;

    [SerializeField] private Color splashColor = new(0.2f, 0.95f, 0.9f, 0.9f);

    private float safetyExpiresAt;
    private Vector3 safetyZoneCenter;
    private Vector3 safetyZoneDirection;
    private ParticleSystem punchSplash;
    private ParticleSystem punchMist;
    private LineRenderer rippleRenderer;
    private GameObject rippleObject;
    private Material particleMaterial;
    private Material rippleMaterial;
    private Vector3 rippleOrigin;
    private float rippleStartedAt;
    private bool rippleActive;

    /// <summary>
    /// Returns true while the latest punch is still protecting the player.
    /// </summary>
    public bool IsSafetyActive => Time.time < safetyExpiresAt;

    private void Awake()
    {
        safetyZoneDirection = transform.forward;
        CreatePunchFeedback();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            PerformPunch();
        }
#endif

        UpdateRipple();
    }

    /// <summary>
    /// Checks whether a world position is inside the latest temporary water-safe zone.
    /// </summary>
    public bool IsPositionInSafeZone(Vector3 worldPosition)
    {
        if (!IsSafetyActive)
        {
            return false;
        }

        Vector3 offset = worldPosition - safetyZoneCenter;
        if (Mathf.Abs(offset.y) > safetyZoneHalfHeight)
        {
            return false;
        }

        float forwardDistance = Vector3.Dot(offset, safetyZoneDirection);
        if (forwardDistance < -0.85f || forwardDistance > safetyZoneLength)
        {
            return false;
        }

        Vector3 sideDirection = Vector3.Cross(Vector3.up, safetyZoneDirection).normalized;
        float sideDistance = Vector3.Dot(offset, sideDirection);
        return Mathf.Abs(sideDistance) <= safetyZoneHalfWidth;
    }

    private void PerformPunch()
    {
        safetyZoneDirection = transform.forward;
        if (safetyZoneDirection.sqrMagnitude < 0.001f)
        {
            safetyZoneDirection = Vector3.forward;
        }

        safetyZoneDirection.y = 0f;
        safetyZoneDirection.Normalize();
        safetyZoneCenter = transform.position + Vector3.up * 0.9f + safetyZoneDirection * punchForwardOffset;
        safetyExpiresAt = Time.time + safetyDuration;

        Vector3 punchOrigin = transform.position + Vector3.up * 0.9f + safetyZoneDirection * punchForwardOffset;
        PlayPunchParticles(punchSplash, punchOrigin, 24f, splashParticleCount);
        PlayPunchParticles(punchMist, punchOrigin + safetyZoneDirection * 0.2f, 48f, Mathf.Max(8, splashParticleCount / 2));
        StartRipple(punchOrigin + safetyZoneDirection * 0.35f);
    }

    private void CreatePunchFeedback()
    {
        particleMaterial = CreateTransparentMaterial("Water Punch Particle", "Universal Render Pipeline/Particles/Unlit", splashColor);
        punchSplash = CreateParticleSystem("Water Punch Splash", particleMaterial, 0.32f, 0.08f, 2.6f, 24f, 18f);
        punchMist = CreateParticleSystem("Water Punch Mist", particleMaterial, 0.48f, 0.14f, 1.5f, 48f, 50f);

        rippleObject = new GameObject("Water Punch Ripple");
        rippleRenderer = rippleObject.AddComponent<LineRenderer>();
        rippleRenderer.loop = true;
        rippleRenderer.useWorldSpace = true;
        rippleRenderer.positionCount = 32;
        rippleRenderer.widthMultiplier = 0.065f;
        rippleRenderer.numCapVertices = 2;
        rippleRenderer.sharedMaterial = rippleMaterial = CreateTransparentMaterial("Water Punch Ripple", "Universal Render Pipeline/Unlit", new Color(0.25f, 1f, 0.92f, 0.85f));
        rippleRenderer.enabled = false;
    }

    private ParticleSystem CreateParticleSystem(string objectName, Material material, float lifetime, float size, float speed, float coneAngle, float shapeRadius)
    {
        GameObject particleObject = new(objectName);
        particleObject.transform.SetParent(transform, false);
        particleObject.SetActive(false);

        ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.7f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.65f, size);
        main.startColor = splashColor;
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = shapeRadius;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new();
        gradient.SetKeys(
            new[] { new GradientColorKey(splashColor, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(splashColor.a, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new(
            new Keyframe(0f, 0.65f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0.05f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 20;
        return particleSystem;
    }

    private void PlayPunchParticles(ParticleSystem particleSystem, Vector3 worldPosition, float coneAngle, int count)
    {
        if (particleSystem == null)
        {
            return;
        }

        particleSystem.transform.position = worldPosition;
        particleSystem.transform.rotation = Quaternion.FromToRotation(Vector3.up, safetyZoneDirection);
        particleSystem.gameObject.SetActive(true);
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleSystem.Emit(count);
    }

    private void StartRipple(Vector3 worldPosition)
    {
        if (rippleRenderer == null)
        {
            return;
        }

        rippleOrigin = worldPosition;
        rippleStartedAt = Time.time;
        rippleActive = true;
        rippleRenderer.enabled = true;
        UpdateRipple();
    }

    private void UpdateRipple()
    {
        if (!rippleActive || rippleRenderer == null)
        {
            return;
        }

        float normalizedTime = Mathf.Clamp01((Time.time - rippleStartedAt) / rippleDuration);
        float radius = Mathf.Lerp(rippleStartRadius, rippleEndRadius, normalizedTime);
        Color color = new(splashColor.r, splashColor.g, splashColor.b, (1f - normalizedTime) * 0.8f);
        rippleRenderer.startColor = color;
        rippleRenderer.endColor = color;
        rippleRenderer.SetPosition(0, rippleOrigin + Vector3.up * 0.03f + Vector3.right * radius);

        for (int index = 1; index < rippleRenderer.positionCount; index++)
        {
            float angle = index / (float)rippleRenderer.positionCount * Mathf.PI * 2f;
            rippleRenderer.SetPosition(index, rippleOrigin + Vector3.up * 0.03f + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        if (normalizedTime >= 1f)
        {
            rippleActive = false;
            rippleRenderer.enabled = false;
        }
    }

    private Material CreateTransparentMaterial(string materialName, string shaderName, Color color)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new(shader)
        {
            name = materialName,
            renderQueue = 3000
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        return material;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 direction = safetyZoneDirection.sqrMagnitude > 0.001f ? safetyZoneDirection : transform.forward;
        direction.y = 0f;
        direction.Normalize();
        Vector3 center = transform.position + Vector3.up * 0.9f + direction * (punchForwardOffset + safetyZoneLength * 0.5f);
        Gizmos.color = new Color(0.1f, 1f, 0.9f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.LookRotation(direction), Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0f, 0f, 0f), new Vector3(safetyZoneHalfWidth * 2f, safetyZoneHalfHeight * 2f, safetyZoneLength));
        Gizmos.matrix = Matrix4x4.identity;
    }

    private void OnDestroy()
    {
        if (rippleObject != null)
        {
            Destroy(rippleObject);
        }

        if (particleMaterial != null)
        {
            Destroy(particleMaterial);
        }

        if (rippleMaterial != null)
        {
            Destroy(rippleMaterial);
        }
    }
}
