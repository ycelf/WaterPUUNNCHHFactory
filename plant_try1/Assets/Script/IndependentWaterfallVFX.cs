using UnityEngine;

/// <summary>
/// Plays existing stream/splash effects without a water volume or room controller.
/// Splash is a continuous decoration at its own transform, not a collision response.
/// </summary>
[DisallowMultipleComponent]
public class IndependentWaterfallVFX : MonoBehaviour
{
    [Header("独立粒子：可以只设置其中一个")]
    [SerializeField] private ParticleSystem stream;
    [SerializeField] private ParticleSystem splash;

    [Header("播放开关")]
    [SerializeField] private bool flowEnabled = true;

    [Header("每秒发射数量：覆盖这两个粒子系统的 Rate over Time")]
    [Min(0f)] [SerializeField] private float streamRate = 60f;
    [Min(0f)] [SerializeField] private float splashRate = 20f;

    [Header("可选：水流终止平面")]
    [Tooltip("留空时水滴按寿命消失。设置后，Stream 水滴到这个点的局部绿色 Y 轴下方时消失。绿色箭头通常朝上。")]
    [SerializeField] private Transform streamEnd;

    private ParticleSystem.Particle[] streamBuffer;
    private bool streamWasEnabled;
    private bool splashWasEnabled;

    public bool IsFlowing => flowEnabled;
    public void StartFlow() => flowEnabled = true;
    public void StopFlow() => flowEnabled = false;
    public void ToggleFlow() => flowEnabled = !flowEnabled;
    public void SetFlow(bool value) => flowEnabled = value;

    private void OnEnable()
    {
        if (stream != null && stream == splash)
        {
            Debug.LogError("Stream 和 Splash 不能引用同一个 Particle System。", this);
            enabled = false;
            return;
        }

        Configure(stream);
        Configure(splash);
        streamWasEnabled = splashWasEnabled = false;
        RefreshPlayback();
    }

    private static void Configure(ParticleSystem system)
    {
        if (system == null) return;

        system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.loop = true;
        main.stopAction = ParticleSystemStopAction.None;

        // Preserve the existing renderer, material, shape, size, lifetime and gravity.
        var emission = system.emission;
        emission.enabled = true;
    }

    private void LateUpdate()
    {
        RefreshPlayback();
        TrimStreamAtEnd();
    }

    private void RefreshPlayback()
    {
        ApplyPlayback(stream, streamRate, ref streamWasEnabled);
        ApplyPlayback(splash, splashRate, ref splashWasEnabled);
    }

    private void ApplyPlayback(ParticleSystem system, float rate, ref bool wasEnabled)
    {
        if (system == null) return;

        var emission = system.emission;
        emission.enabled = true;
        float targetRate = Mathf.Max(0f, rate);
        if (emission.rateOverTime.mode != ParticleSystemCurveMode.Constant ||
            !Mathf.Approximately(emission.rateOverTime.constant, targetRate))
        {
            emission.rateOverTime = targetRate;
        }

        bool shouldPlay = flowEnabled && targetRate > 0f;
        if (shouldPlay)
        {
            if (!wasEnabled || !system.isPlaying) system.Play(false);
        }
        else if (wasEnabled || system.isPlaying)
        {
            // Existing particles finish their lifetime when the flow is switched off.
            system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
        wasEnabled = shouldPlay;
    }

    private void TrimStreamAtEnd()
    {
        if (stream == null || streamEnd == null) return;

        int capacity = Mathf.Max(1, stream.main.maxParticles);
        if (streamBuffer == null || streamBuffer.Length < capacity)
            streamBuffer = new ParticleSystem.Particle[capacity];

        int count = stream.GetParticles(streamBuffer);
        bool changed = false;
        Vector3 normal = streamEnd.up;
        Vector3 origin = streamEnd.position;
        for (int i = 0; i < count; i++)
        {
            if (Vector3.Dot(streamBuffer[i].position - origin, normal) > 0f) continue;
            streamBuffer[i].remainingLifetime = -1f;
            changed = true;
        }
        if (changed) stream.SetParticles(streamBuffer, count);
    }

    private void OnDisable()
    {
        Clear(stream);
        Clear(splash);
        streamWasEnabled = splashWasEnabled = false;
    }

    private static void Clear(ParticleSystem system)
    {
        if (system != null)
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDrawGizmosSelected()
    {
        if (streamEnd == null) return;
        Gizmos.color = Color.cyan;
        Vector3 center = streamEnd.position;
        Vector3 right = streamEnd.right * 0.6f;
        Vector3 forward = streamEnd.forward * 0.6f;
        Gizmos.DrawLine(center - right - forward, center + right - forward);
        Gizmos.DrawLine(center + right - forward, center + right + forward);
        Gizmos.DrawLine(center + right + forward, center - right + forward);
        Gizmos.DrawLine(center - right + forward, center - right - forward);
        Gizmos.DrawRay(center, streamEnd.up * 0.4f);
    }
}
