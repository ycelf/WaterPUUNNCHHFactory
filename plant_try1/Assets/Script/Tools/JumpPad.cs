using UnityEngine;

public class JumpPad : MonoBehaviour
{
    [Header("跳跃设置")]

    [Tooltip("从起跳位置向上跳起的高度")]
    [Min(0.1f)]
    public float jumpHeight = 6f;

    [Header("初始状态")]

    [Tooltip("勾选：开局已经启用。取消：需要先操作开关")]
    [SerializeField] private bool powered = false;

    [Header("材质")]

    [SerializeField] private Material poweredMaterial;
    [SerializeField] private Material unpoweredMaterial;
    [Tooltip("拖入起跳板模型上的 Mesh Renderer")]
    [SerializeField] private Renderer padRenderer;


    private bool initialPower;

    // 起跳板物体和组件也必须处于启用状态。
    public bool IsPowered => isActiveAndEnabled && powered;

    private void Awake()
    {
        initialPower = powered;

    }
    private void LateUpdate()
    {
        RefreshMaterial();
    }

    public void SetPower(bool value)
    {
        powered = value;

    }

    public void TurnOn()
    {
        SetPower(true);
    }

    public void TurnOff()
    {
        SetPower(false);

    }

    public void TogglePower()
    {
        SetPower(!powered);
    }

    // 可以接到房间的 On Room Reset 事件。

    public void ResetPower()
    {
        SetPower(initialPower);
    }

    private void RefreshMaterial()
    {
        if(padRenderer == null)
        {
            return;
        }

        
        Material selected;
        if (IsPowered)
        {
            selected = poweredMaterial;
        }
        else
        {
            selected = unpoweredMaterial;
        }

        if(selected == null)
        {
            return;
        }

        if(padRenderer.sharedMaterial != selected)
        {
            padRenderer.sharedMaterial = selected;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created

}
