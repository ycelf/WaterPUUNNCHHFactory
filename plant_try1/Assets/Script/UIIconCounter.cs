using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIIconCounter : MonoBehaviour
{
    [Header("图标模板")]
    [Tooltip("拖入本物体下面的一个 Image，运行时会复制它")]
    [SerializeField] private Image iconTemplate;

    [Header("图标颜色")]
    [SerializeField] private Color availableColor = Color.white;

    [SerializeField]
    private Color spentColor = new Color(0.25f, 0.25f, 0.25f, 0.35f);

    private readonly List<Image> icons = new List<Image>();

    public void SetValue(int remaining, int maximum)
    {
        if (iconTemplate == null)
        {
            return;
        }

        maximum = Mathf.Max(0, maximum);
        remaining = Mathf.Clamp(remaining, 0, maximum);

        // 模板本身不显示，只显示它的复制品。
        iconTemplate.gameObject.SetActive(false);

        // 只有图标数量不够时才创建。
        while (icons.Count < maximum)
        {
            Image icon = Instantiate(
                iconTemplate,
                iconTemplate.transform.parent);

            icon.name = $"Icon_{icons.Count + 1}";
            icon.raycastTarget = false;

            icons.Add(icon);
        }

        for (int i = 0; i < icons.Count; i++)
        {
            Image icon = icons[i];
            bool shouldDisplay = i < maximum;

            if (icon.gameObject.activeSelf != shouldDisplay)
            {
                icon.gameObject.SetActive(shouldDisplay);
            }

            if (!shouldDisplay)
            {
                continue;
            }

            Color targetColor =
                i < remaining ? availableColor : spentColor;

            if (icon.color != targetColor)
            {
                icon.color = targetColor;
            }
        }
    }
}