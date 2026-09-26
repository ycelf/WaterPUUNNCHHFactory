using UnityEngine;

public class PipeWaterVFX : MonoBehaviour
{
    [Header("粒子")]
    [SerializeField] private ParticleSystem stream;
    [SerializeField] private ParticleSystem splash;

    [Header("接收水流的水体")]
    [Tooltip("t拖入水体本身的BoxCollider，不是房间进入触发器")]
    [SerializeField] private BoxCollider waterVolume;

    [Header("开关")]
    [SerializeField] private bool flowEnabled = true;

    [Tooltip("勾选后，水循环运行且没有暂停时才出水")]
    [SerializeField] private bool followRoomState = true;

    [SerializeField] private WaterRoomController roomController;

    [Header("水花")]
    [Range(1, 8)]
    [SerializeField] private int splashParticlesPerHit = 2;

    [Min(1)]
    [SerializeField] private int maxSplashHitsPerFrame = 4;

    [Min(0f)]
    [SerializeField] private float splashUpSpeed = 1.6f;

    [Min(0f)]
    [SerializeField] private float splashSideSpeed = 0.7f;

    private ParticleSystem.Particle[] streamBuffer;
    private ParticleSystem.Particle[] splashBuffer;

    private void Awake()
    {
        ConfigureSystem(stream);
        ConfigureSystem(splash);

        if(splash != null)
        {
            var emission = splash.emission;
            emission.enabled = false;

        }
    }

    private void OnEnable()
    {
        ClearSystem(stream);
        ClearSystem(splash);

        if (splash != null)
        {
            // 不自动发射，由 EmitSplash() 决定何时生成。
            var emission = splash.emission;
            emission.enabled = false;

            // 启动粒子模拟，让生成的水花正常运动。
            splash.Play(false);
        }
    }

    private static void ConfigureSystem(ParticleSystem system)
    {
        if (system == null)
            return;

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        main.playOnAwake = false;

        //镜头外页继续模拟，避免重新看到时粒子停在旧位置
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
    }

    private void LateUpdate()
    {
        if (stream == null)
            return;

        bool roomAllowsFlow =
            !followRoomState ||
            (roomController != null &&
             roomController.IsRunning &&
             !roomController.IsPaused);

        bool shouldEmit = flowEnabled && roomAllowsFlow;

        if (shouldEmit && !stream.isEmitting)
        {
            var emission = stream.emission;
            emission.enabled = true;
            stream.Play(false);
        }
        else if (!shouldEmit && stream.isEmitting)
        {
            stream.Stop(
                false,
                ParticleSystemStopBehavior.StopEmitting);
        } // 关闭流水的分支在这里结束！

        // 每帧都处理碰水，包括关水后仍在下落的水滴。
        if (!TryGetWaterBounds(out Bounds waterBounds))
            return;

        ProcessStream(waterBounds);
        RemoveSubmergedSplash(waterBounds);
    }

    private bool TryGetWaterBounds(out Bounds bounds)
    {
        bounds = default;

        if(waterVolume == null || !waterVolume.gameObject.activeInHierarchy)
        {
            return false;

        }
        //不依赖Collider.enabled
        //水体脚本可能在浅水时关闭危险触发器
        Transform waterTransform = waterVolume.transform;
        Vector3 scale = waterTransform.lossyScale;

        scale = new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z));

        Vector3 center =
            waterTransform.TransformPoint(waterVolume.center);

        Vector3 size = Vector3.Scale(waterVolume.size, scale);
        bounds = new Bounds (center, size);

        return size.x > 0.001f &&
               size.y > 0.001f &&
               size.z > 0.001f;
    }

    private void ProcessStream(Bounds waterBounds)
    {
        EnsureBuffer(stream, ref streamBuffer);

        int count = stream.GetParticles(streamBuffer);
        int splashHits = 0;
        float surfaceY = waterBounds.max.y;

        for (int i = 0; i < count; i++)
        {
            Vector3 position = streamBuffer[i].position;

            if (position.y > surfaceY)
                continue;

            // 估算上一帧位置，减少水滴高速穿过薄水体时的漏检。
            float age = Mathf.Max(
                0f,
                streamBuffer[i].startLifetime -
                streamBuffer[i].remainingLifetime);

            float step = Mathf.Min(Time.deltaTime, age);
            Vector3 previous =
                position - streamBuffer[i].velocity * step;

            bool crossedSurface = previous.y > surfaceY;
            Vector3 contact = position;

            if (crossedSurface)
            {
                float t = Mathf.InverseLerp(
                    previous.y, position.y, surfaceY);

                contact = Vector3.Lerp(previous, position, t);
            }
            else if (position.y < waterBounds.min.y)
            {
                continue;
            }

            if (!InsideHorizontalBounds(contact, waterBounds))
                continue;

            // 到水面或者进入水体后，删除这颗流水粒子。
            streamBuffer[i].remainingLifetime = -1f;

            // 只有从空气落到水面时产生水花。
            // 管口本来就在水下时，不凭空在水面喷水花。
            if (crossedSurface &&
                splashHits < maxSplashHitsPerFrame)
            {
                contact.y = surfaceY + 0.03f;
                EmitSplash(contact);
                splashHits++;
            }
        }

        stream.SetParticles(streamBuffer, count);
    }

    private void EmitSplash(Vector3 position)
    {
        if (splash == null)
            return;

        for (int i = 0; i < splashParticlesPerHit; i++)
        {
            Vector2 side = Random.insideUnitCircle * splashSideSpeed;

            var parameters = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = new Vector3(
                    side.x,
                    Random.Range(0.7f, 1.2f) * splashUpSpeed,
                    side.y),
                applyShapeToPosition = false
            };

            splash.Emit(parameters, 1);
        }
    }

    private void RemoveSubmergedSplash(Bounds waterBounds)
    {
        if (splash == null)
            return;

        EnsureBuffer(splash, ref splashBuffer);
        int count = splash.GetParticles(splashBuffer);

        for (int i = 0; i < count; i++)
        {
            Vector3 position = splashBuffer[i].position;

            if (position.y <= waterBounds.max.y &&
                InsideHorizontalBounds(position, waterBounds))
            {
                splashBuffer[i].remainingLifetime = -1f;
            }
        }

        splash.SetParticles(splashBuffer, count);
    }

    private static bool InsideHorizontalBounds(
        Vector3 position, Bounds bounds)
    {
        return position.x >= bounds.min.x &&
               position.x <= bounds.max.x &&
               position.z >= bounds.min.z &&
               position.z <= bounds.max.z;
    }

    private static void EnsureBuffer(
        ParticleSystem system,
        ref ParticleSystem.Particle[] buffer)
    {
        int capacity = system.main.maxParticles;

        if (buffer == null || buffer.Length < capacity)
            buffer = new ParticleSystem.Particle[capacity];
    }

    public void StartFlow()
    {
        flowEnabled = true;
    }

    public void StopFlow()
    {
        flowEnabled = false;
    }

    public void ToggleFlow()
    {
        flowEnabled = !flowEnabled;
    }

    public void SetFlow(bool enabled)
    {
        flowEnabled = enabled;
    }

    private void OnDisable()
    {
        ClearSystem(stream);
        ClearSystem(splash);
    }

    private static void ClearSystem(ParticleSystem system)
    {
        if (system != null)
        {
            system.Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }


}
