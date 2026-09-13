using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class WaterPunchWaterSafety : MonoBehaviour
{
    [Header("Swimming")]
    [Min(0)]
    [SerializeField] private int swimUses = 6;

    [Min(0)]
    [SerializeField] private int struggleUses = 2;

    [Min(0.1f)]
    [SerializeField] private float swimUpwardVelocity = 5.5f;

    [Min(0f)]
    [SerializeField] private float struggleUpwardVelocity = 1.6f;

    [Header("Drowning Detection Points")]
    [SerializeField] private Transform drowningDetectionPoint;

    [Min(0.1f)]
    [SerializeField] private float drowningDetectionPointHeight = 1f;

    [SerializeField] private Transform headDrowningDetectionPoint;

    [Min(0.1f)]
    [SerializeField] private float headDrowningDetectionPointHeight = 1.65f;

    [Min(0.05f)]
    [SerializeField] private float lowerBodyDetectionPointHeight = 0.2f;

    [Min(0f)]
    [SerializeField] private float surfaceClearance = 0.05f;
    [Min(1)]
    [SerializeField] private int splashParticleCount = 18;

    [Min(0.1f)]
    [SerializeField] private float rippleDuration = 0.38f;

    [Min(0.1f)]
    [SerializeField] private float rippleStartRadius = 0.2f;

    [Min(0.1f)]
    [SerializeField] private float rippleEndRadius = 1.35f;

    [SerializeField] private Color splashColor = new(0.2f, 0.95f, 0.9f, 0.9f);

    private int remainingSwimUses;
    private int remainingStruggleUses;
    private float pendingSwimUpwardVelocity;
    private float waterSurfaceY;
    private bool hasWaterSurface;
    private bool isInWater;
    private bool isChestSubmerged;
    private CharacterController characterController;
    private ParticleSystem swimSplash;
    private ParticleSystem swimMist;
    private LineRenderer rippleRenderer;
    private GameObject rippleObject;
    private Material particleMaterial;
    private Material rippleMaterial;
    private Vector3 rippleOrigin;
    private float rippleStartedAt;
    private bool rippleActive;

    /// <summary>
    /// Gets the current number of full-strength swimming opportunities.
    /// </summary>
    public int RemainingSwimUses => remainingSwimUses;

    /// <summary>
    /// Gets the current number of weak struggle opportunities.
    /// </summary>
    public int RemainingStruggleUses => remainingStruggleUses;

    /// <summary>
    /// Gets whether this player is currently inside a water hazard.
    /// </summary>
    public bool IsInWater => isInWater;

    /// <summary>
    /// Gets whether the player's chest is submerged. This controls swimming and ordinary jumping.
    /// </summary>
    public bool IsChestSubmerged => isChestSubmerged;

    public Transform DrowningDetectionPoint => drowningDetectionPoint;

    /// <summary>
    /// Gets the head point used to decide whether the player is drowning.
    /// </summary>
    public Transform HeadDrowningDetectionPoint => headDrowningDetectionPoint;

    /// <summary>
    /// Gets the lower-body point used to keep the player in the water until the legs leave it.
    /// </summary>
    public Transform LowerBodyDetectionPoint { get; private set; }

    /// <summary>
    /// Sets the current world-space water surface height.
    /// </summary>
    public void SetWaterSurfaceHeight(float surfaceY)
    {
        waterSurfaceY = surfaceY;
        hasWaterSurface = true;
    }

    /// <summary>
    /// Prevents upward swimming movement from carrying the chest above the water surface.
    /// </summary>
    public float ConstrainUpwardVelocity(float verticalVelocity, float deltaTime)
    {
        if (!isInWater || !hasWaterSurface || verticalVelocity <= 0f || deltaTime <= 0f || drowningDetectionPoint == null)
        {
            return verticalVelocity;
        }

        float chestOffset = drowningDetectionPoint.position.y - transform.position.y;
        float maximumRootY = waterSurfaceY - chestOffset - surfaceClearance;
        float maximumDelta = maximumRootY - transform.position.y;
        if (maximumDelta <= 0f)
        {
            return 0f;
        }

        return Mathf.Min(verticalVelocity, maximumDelta / deltaTime);
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        EnsureDrowningDetectionPoint();
        CreateSwimmingFeedback();
    }

    private void Update()
    {
        if (isChestSubmerged)
        {
            SendMessage("JumpInput", false, SendMessageOptions.DontRequireReceiver);
        }

        UpdateRipple();
        ApplyPendingSwimMovement();
    }

#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Receives the Player action map Attack event, which is bound to the left mouse button.
    /// </summary>
    public void OnAttack(UnityEngine.InputSystem.InputValue value)
    {
        if (value.isPressed)
        {
            TrySwim();
        }
    }
#endif

    private void ApplyPendingSwimMovement()
    {
        if (characterController == null || pendingSwimUpwardVelocity <= 0f || !isInWater)
        {
            return;
        }

        float movement = pendingSwimUpwardVelocity * Time.deltaTime;
        if (hasWaterSurface && drowningDetectionPoint != null)
        {
            float chestOffset = drowningDetectionPoint.position.y - transform.position.y;
            float maximumRootY = waterSurfaceY - chestOffset - surfaceClearance;
            movement = Mathf.Min(movement, Mathf.Max(0f, maximumRootY - transform.position.y));
        }

        if (movement > 0f)
        {
            characterController.Move(Vector3.up * movement);
        }

        pendingSwimUpwardVelocity = 0f;
    }

    public void SetInWater(bool value)
    {
        if (value == isInWater)
        {
            return;
        }

        isInWater = value;
        pendingSwimUpwardVelocity = 0f;

        if (value)
        {
            ResetSwimUses();
        }
    }

    /// <summary>
    /// Updates whether the player's chest is submerged.
    /// </summary>
    public void SetChestSubmerged(bool value)
    {
        isChestSubmerged = value;
    }

    public void ResetSwimUses()
    {
        remainingSwimUses = Mathf.Max(0, swimUses);
        remainingStruggleUses = Mathf.Max(0, struggleUses);
    }

    /// <summary>
    /// Consumes one swimming or struggle opportunity and queues upward movement.
    /// </summary>
    public bool TrySwim()
    {
        if (!isInWater)
        {
            return false;
        }

        float upwardVelocity;
        if (remainingSwimUses > 0)
        {
            remainingSwimUses--;
            upwardVelocity = swimUpwardVelocity;
        }
        else if (remainingStruggleUses > 0)
        {
            remainingStruggleUses--;
            upwardVelocity = struggleUpwardVelocity;
        }
        else
        {
            return false;
        }

        pendingSwimUpwardVelocity = Mathf.Max(pendingSwimUpwardVelocity, upwardVelocity);
        PlaySwimmingFeedback(upwardVelocity > struggleUpwardVelocity);
        return true;
    }

    /// <summary>
    /// Supplies the queued upward velocity to the character controller once.
    /// </summary>
    public bool ConsumeSwimUpwardVelocity(out float upwardVelocity)
    {
        upwardVelocity = pendingSwimUpwardVelocity;
        pendingSwimUpwardVelocity = 0f;
        return upwardVelocity > 0f;
    }

    private void EnsureDrowningDetectionPoint()
    {
        if (drowningDetectionPoint == null)
        {
            GameObject pointObject = new("Drowning Detection Point");
            pointObject.transform.SetParent(transform, false);
            pointObject.transform.localPosition = Vector3.up * drowningDetectionPointHeight;
            drowningDetectionPoint = pointObject.transform;
        }

        if (headDrowningDetectionPoint == null)
        {
            GameObject headPointObject = new("Head Drowning Detection Point");
            headPointObject.transform.SetParent(transform, false);
            headPointObject.transform.localPosition = Vector3.up * headDrowningDetectionPointHeight;
            headDrowningDetectionPoint = headPointObject.transform;
        }

        if (LowerBodyDetectionPoint != null)
        {
            return;
        }

        GameObject lowerPointObject = new("Lower Body Detection Point");
        lowerPointObject.transform.SetParent(transform, false);
        lowerPointObject.transform.localPosition = Vector3.up * lowerBodyDetectionPointHeight;
        LowerBodyDetectionPoint = lowerPointObject.transform;
    }

    private void PlaySwimmingFeedback(bool isFullStrength)
    {
        if (drowningDetectionPoint == null)
        {
            return;
        }

        Vector3 feedbackOrigin = drowningDetectionPoint.position;
        int particleCount = isFullStrength ? splashParticleCount : Mathf.Max(4, splashParticleCount / 3);
        PlaySwimmingParticles(swimSplash, feedbackOrigin, 24f, particleCount);
        PlaySwimmingParticles(swimMist, feedbackOrigin + Vector3.up * 0.12f, 48f, Mathf.Max(3, particleCount / 2));
        StartRipple(feedbackOrigin);
    }

    private void CreateSwimmingFeedback()
    {
        particleMaterial = CreateTransparentMaterial("Swimming Particle", "Universal Render Pipeline/Particles/Unlit", splashColor);
        swimSplash = CreateParticleSystem("Swimming Splash", particleMaterial, 0.32f, 0.08f, 2.6f, 24f, 18f);
        swimMist = CreateParticleSystem("Swimming Mist", particleMaterial, 0.48f, 0.14f, 1.5f, 48f, 50f);

        rippleObject = new GameObject("Swimming Ripple");
        rippleRenderer = rippleObject.AddComponent<LineRenderer>();
        rippleRenderer.loop = true;
        rippleRenderer.useWorldSpace = true;
        rippleRenderer.positionCount = 32;
        rippleRenderer.widthMultiplier = 0.065f;
        rippleRenderer.numCapVertices = 2;
        rippleRenderer.sharedMaterial = rippleMaterial = CreateTransparentMaterial("Swimming Ripple", "Universal Render Pipeline/Unlit", new Color(0.25f, 1f, 0.92f, 0.85f));
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

    private void PlaySwimmingParticles(ParticleSystem particleSystem, Vector3 worldPosition, float coneAngle, int count)
    {
        if (particleSystem == null)
        {
            return;
        }

        particleSystem.transform.position = worldPosition;
        particleSystem.transform.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.up);
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
}
