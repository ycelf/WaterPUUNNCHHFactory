using UnityEngine;

/// <summary>Connects swimming state to the HUD. Does not own or consume uses.</summary>
public class SwimmingStatusUI : MonoBehaviour
{
    [Header("玩家数据")]
    [SerializeField] private WaterPunchWaterSafety playerWaterSafety;

    [Header("UI 引用")]
    [Tooltip("需要是本物体的子物体，不能是本物体或它的父物体")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private UIIconCounter swimIcons;
    [SerializeField] private UIIconCounter struggleIcons;

    [Header("显示规则")]
    [Tooltip("勾选时入水显示、离水隐藏；取消后始终显示剩余次数")]
    [SerializeField] private bool showOnlyInWater = true;

    private WaterPunchWaterSafety subscribedSource;

    private void OnEnable()
    {
        if (playerWaterSafety == null || panelRoot == null ||
            swimIcons == null || struggleIcons == null)
        {
            Debug.LogWarning($"{name}: 请设置 SwimmingStatusUI 的玩家和 UI 引用。", this);
            return;
        }

        if (transform.IsChildOf(panelRoot.transform))
        {
            Debug.LogError("SwimmingStatusUI 必须挂在始终启用的物体上，不能放在要隐藏的 Panel 内。", this);
            return;
        }

        subscribedSource = playerWaterSafety;
        subscribedSource.SwimmingStateChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (subscribedSource != null)
        {
            subscribedSource.SwimmingStateChanged -= Refresh;
        }

        subscribedSource = null;
    }

    private void Refresh()
    {
        if (subscribedSource == null || panelRoot == null ||
            swimIcons == null || struggleIcons == null)
        {
            return;
        }

        bool shouldShow = subscribedSource.SwimmingAllowed && !subscribedSource.IsDrowning &&
            (!showOnlyInWater || subscribedSource.IsInWater);
        panelRoot.SetActive(shouldShow);

        if (!shouldShow)
        {
            return;
        }

        swimIcons.SetValue(subscribedSource.RemainingSwimUses, subscribedSource.MaxSwimUses);
        struggleIcons.SetValue(subscribedSource.RemainingStruggleUses, subscribedSource.MaxStruggleUses);
    }
}
