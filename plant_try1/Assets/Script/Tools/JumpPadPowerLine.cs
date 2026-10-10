using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]

public class JumpPadPowerLine : MonoBehaviour
{
    [Header("读取哪个起跳板的状态")]

    [SerializeField] private JumpPad targetPad;

    [Header("连线路径")]

    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Tooltip("按从开关到起跳板的顺序填写。没有拐弯就留空")]
    [SerializeField] private Transform[] bendPoints = new Transform[0];

    [Header("材质")]

    [SerializeField] private Material poweredMaterial;
    [SerializeField] private Material unpoweredMaterial;

    [Header("外观")]

    [Min(0.001f)]
    [SerializeField] private float lineWidth = 0.06f;

    private LineRenderer line;

    private void OnEnable()
    {
        line = GetComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.loop = false;

        line.alignment = LineAlignment.View;
        line.generateLightingData = true;

        line.numCapVertices = 4;
        line.numCornerVertices = 4;

        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;

        line.startColor = Color.white;
        line.endColor = Color.white;

        RefreshLine();

    }

    private void LateUpdate()
    {
        RefreshLine();
    }

    private void RefreshLine()
    {
        if (line == null)
            return;

        Material selected = targetPad != null && targetPad.IsPowered
            ? poweredMaterial
            : unpoweredMaterial;

        //没设置完整时先不显示
        if(targetPad == null || startPoint == null || endPoint == null || selected == null)
        {
            line.enabled = false;
            return;
        }

        line.enabled = true;

        // 只有状态对应的材质变化时才切换。
        if(line.sharedMaterial != selected)
        {
            line.sharedMaterial = selected;
        }
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        int count = 2;

        if(bendPoints != null)
        {
            foreach ( Transform point in bendPoints)
            {
                if (point != null)
                    count++;
            }
        }

        line.positionCount = count;

        int index = 0;
        line.SetPosition(index ++, startPoint.position);

        if(bendPoints != null)
        {
            foreach(Transform point in bendPoints)
            {
                if(point != null)
                {
                    line.SetPosition(index++, point.position);
                }
            }
        }

        line.SetPosition(index, endPoint.position);
    }

    private void OnDisable()
    {
        if(line != null)
        {
            line.enabled = false;
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

}
