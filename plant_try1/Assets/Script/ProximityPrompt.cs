using UnityEngine;

public class ProximityPrompt : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("拖入玩家根物体，例如 PlayerArmature")]
    [SerializeField] private Transform player;

    [Tooltip("拖入这个物体自己的提示 Panel 上的 WorldInteractionPrompt")]
    [SerializeField] private WorldInteractionPrompt promptView;

    [Header("距离检测")]
    [Tooltip("留空时，以挂载本脚本的物体位置为中心")]
    [SerializeField] private Transform rangeCenter;

    [Min(0.1f)]
    [SerializeField] private float showDistance = 2.5f;

    [Tooltip("多久检测一次提示显示。0.1 表示每秒检测 10 次")]
    [Min(0.02f)]
    [SerializeField] private float checkInterval = 0.1f;

    [Header("是否允许显示和交互")]
    [SerializeField] private bool available = true;

    private float nextCheckTime;

    // 检测范围的中心，与提示 Panel 放在哪里无关。
    private Vector3 CenterPosition =>
        rangeCenter != null ? rangeCenter.position : transform.position;

    // 其他脚本可以读取这个属性，判断玩家是否真的在范围内。
    public bool IsPlayerInRange
    {
        get
        {
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                return false;
            }

            float distanceSquared =
                (player.position - CenterPosition).sqrMagnitude;

            return distanceSquared <= showDistance * showDistance;
        }
    }

    // 按键交互时会读取实时距离，不依赖上一次提示刷新结果。
    public bool CanInteract =>
        isActiveAndEnabled && available && IsPlayerInRange;

    private void OnEnable()
    {
        nextCheckTime = 0f;
    }

    private void Start()
    {
        if (player == null || promptView == null)
        {
            Debug.LogWarning(
                $"{name}: ProximityPrompt 需要设置 Player 和 Prompt View。",
                this);
        }

        RefreshPrompt();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheckTime)
        {
            return;
        }

        nextCheckTime =
            Time.unscaledTime + Mathf.Max(0.02f, checkInterval);

        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        if (promptView == null)
        {
            return;
        }

        if (CanInteract)
        {
            promptView.Show();
        }
        else
        {
            promptView.Hide();
        }
    }

    // 使用完、收集完时传 false；重置后传 true。
    public void SetAvailable(bool value)
    {
        available = value;
        RefreshPrompt();
    }

    private void OnDisable()
    {
        if (promptView != null)
        {
            promptView.Hide();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(CenterPosition, showDistance);
    }
}