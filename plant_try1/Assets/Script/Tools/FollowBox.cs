using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>One physical follower at a time; movement, interaction and room-reset wiring stay local.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class FollowBox : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("拖入玩家根物体，例如 PlayerArmature")]
    [SerializeField] private Transform player;
    [Tooltip("该房间触发 On Room Reset 时，箱子自动复位")]
    [SerializeField] private WaterRoomController resetRoom;
    [Tooltip("可选。留空时回到游戏开始的位置；指定后回到这个点。不要放在箱子下面")]
    [SerializeField] private Transform resetPoint;

    [Header("交互")]
    [SerializeField] private Key interactKey = Key.E;
    [Min(0.1f)] [SerializeField] private float interactDistance = 2.5f;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask interactionObstacles = ~0;

    [Header("地面跟随")]
    [Min(0.5f)] [SerializeField] private float followDistance = 1.8f;
    [Min(0.1f)] [SerializeField] private float maxSpeed = 6f;
    [Min(0.1f)] [SerializeField] private float acceleration = 18f;
    [Min(0.1f)] [SerializeField] private float approachSpeed = 4f;
    [Min(0.01f)] [SerializeField] private float stoppingDistance = 0.12f;
    [Tooltip("超过此距离自动取消跟随，避免玩家传送后箱子追到其他房间")]
    [Min(1f)] [SerializeField] private float breakFollowDistance = 15f;

    [Header("可选：复用自己的提示 Panel")]
    [SerializeField] private WorldInteractionPrompt promptView;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string idleMessage = "按 E 让箱子跟随";
    [SerializeField] private string followingMessage = "按 E 放下箱子";

    [Header("事件：可接灯光、音效")]
    public UnityEvent onFollowStarted = new UnityEvent();
    public UnityEvent onFollowStopped = new UnityEvent();
    public UnityEvent onBoxReset = new UnityEvent();

    private static readonly List<FollowBox> availableBoxes = new List<FollowBox>();
    private static FollowBox currentFollower;
    private static int handledInputFrame = -1;

    private Rigidbody body;
    private BoxCollider box;
    private WaterPunchWaterSafety waterSafety;
    private UnityEvent subscribedReset;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool initialized;

    public bool IsFollowing { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearStaticState()
    {
        availableBoxes.Clear();
        currentFollower = null;
        handledInputFrame = -1;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        box = GetComponent<BoxCollider>();
        initialPosition = body.position;
        initialRotation = body.rotation;
        initialized = true;

        // A parked box is fixed horizontally but still falls onto its supporting floor.
        body.isKinematic = false;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        box.isTrigger = false;
        ParkBody();

        if (player != null) waterSafety = player.GetComponent<WaterPunchWaterSafety>();
        else Debug.LogWarning($"{name}: 请给 FollowBox 指定 Player。", this);
    }

    private void OnEnable()
    {
        if (!availableBoxes.Contains(this)) availableBoxes.Add(this);
        if (resetRoom != null)
        {
            if (resetRoom.onRoomReset == null) resetRoom.onRoomReset = new UnityEvent();
            subscribedReset = resetRoom.onRoomReset;
            subscribedReset.AddListener(ResetBox);
        }
    }

    private void Update()
    {
        if (!PlayerAvailable())
        {
            StopFollowing();
            ShowPrompt(false);
            return;
        }

        if (IsFollowing && Vector3.Distance(player.position, body.position) > breakFollowDistance)
            StopFollowing();

        // A following box owns E until released. Otherwise the closest eligible box owns it.
        bool followingThisPlayer = currentFollower != null && currentFollower.player == player;
        bool selected = followingThisPlayer ? currentFollower == this : FindNearest(player, interactKey) == this;
        ShowPrompt(selected && (IsFollowing || CanStartInteraction()));

        if (!selected || Keyboard.current == null || handledInputFrame == Time.frameCount ||
            !Keyboard.current[interactKey].wasPressedThisFrame) return;

        handledInputFrame = Time.frameCount;
        ToggleFollow();
    }

    private bool PlayerAvailable()
    {
        return player != null && player.gameObject.activeInHierarchy &&
            (waterSafety == null || !waterSafety.IsDrowning);
    }

    private bool CanStartInteraction()
    {
        if (!isActiveAndEnabled || !PlayerAvailable()) return false;
        Vector3 origin = player.position + Vector3.up;
        Vector3 closest = box.ClosestPoint(origin);
        if ((closest - origin).sqrMagnitude > interactDistance * interactDistance) return false;
        if (!requireLineOfSight) return true;

        if (!Physics.Linecast(origin, box.bounds.center, out RaycastHit hit,
                interactionObstacles, QueryTriggerInteraction.Ignore)) return true;
        return hit.rigidbody == body;
    }

    private static FollowBox FindNearest(Transform targetPlayer, Key key)
    {
        FollowBox nearest = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < availableBoxes.Count; i++)
        {
            FollowBox candidate = availableBoxes[i];
            if (candidate == null || candidate.player != targetPlayer || candidate.interactKey != key ||
                !candidate.CanStartInteraction()) continue;

            float distance = (candidate.body.position - targetPlayer.position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = candidate;
        }
        return nearest;
    }

    private void FixedUpdate()
    {
        // Cancel upward impulses even while parked; keep gravity and falling velocity.
        // Do not freeze Y: the box still needs to settle onto the floor.
        Vector3 currentVelocity = body.linearVelocity;
        if (currentVelocity.y > 0f)
            body.linearVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        if (!IsFollowing || !PlayerAvailable()) return;

        Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Vector3 destination = player.position - forward * followDistance;
        Vector3 offset = Vector3.ProjectOnPlane(destination - body.position, Vector3.up);
        float distance = offset.magnitude;
        Vector3 desired = distance <= stoppingDistance ? Vector3.zero :
            offset / distance * Mathf.Min(maxSpeed, (distance - stoppingDistance) * approachSpeed);
        Vector3 velocity = body.linearVelocity;
        Vector3 horizontal = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z),
            desired, acceleration * Time.fixedDeltaTime);
        body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
    }

    public void ToggleFollow()
    {
        if (IsFollowing) StopFollowing();
        else StartFollowing();
    }

    public void StartFollowing()
    {
        if (!initialized || !isActiveAndEnabled || !PlayerAvailable() || IsFollowing) return;
        if (currentFollower != null && currentFollower != this) currentFollower.StopFollowing();

        currentFollower = this;
        IsFollowing = true;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.WakeUp();
        UpdatePromptText();
        onFollowStarted.Invoke();
    }

    public void StopFollowing()
    {
        bool wasFollowing = IsFollowing;
        IsFollowing = false;
        if (currentFollower == this) currentFollower = null;
        if (body != null && wasFollowing) ParkBody();
        UpdatePromptText();
        if (wasFollowing) onFollowStopped.Invoke();
    }

    public void ResetBox()
    {
        if (!initialized) return;
        StopFollowing();
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        bool validResetPoint = resetPoint != null && !resetPoint.IsChildOf(transform);
        body.position = validResetPoint ? resetPoint.position : initialPosition;
        body.rotation = validResetPoint ? resetPoint.rotation : initialRotation;
        ParkBody();
        body.WakeUp();
        ShowPrompt(false);
        onBoxReset.Invoke();
    }

    private void ParkBody()
    {
        Vector3 velocity = body.linearVelocity;
        body.linearVelocity = new Vector3(0f, velocity.y, 0f);
        body.angularVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.FreezeRotation |
            RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
    }

    private void UpdatePromptText()
    {
        if (promptText == null) return;
        string text = IsFollowing ? followingMessage : idleMessage;
        if (promptText.text != text) promptText.text = text;
    }

    private void ShowPrompt(bool visible)
    {
        UpdatePromptText();
        if (promptView == null) return;
        if (visible) promptView.Show();
        else promptView.Hide();
    }

    private void OnDisable()
    {
        if (subscribedReset != null) subscribedReset.RemoveListener(ResetBox);
        subscribedReset = null;
        availableBoxes.Remove(this);
        StopFollowing();
        ShowPrompt(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
