using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class WorldInteractionPrompt : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform billboardRoot;
    [SerializeField] private Camera targetCamera;

    [Header("Animation")]
    [Min(0.01f)]
    [SerializeField] private float fadeDuration = 0.15f;

    [Range(0.5f, 1f)]
    [SerializeField] private float hiddenScale = 0.88f;

    private CanvasGroup canvasGroup;
    private Vector3 visibleScale;
    private bool shouldBeVisible;


    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if(billboardRoot == null)
        {
            billboardRoot = transform;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;

        }

        visibleScale = billboardRoot.localScale;

        HideImmediately();

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float targetAlpha = shouldBeVisible ? 1f : 0f;

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime / fadeDuration);

        float smoothAlpha = Mathf.SmoothStep(0f, 1f, canvasGroup.alpha);

        float currentScale = Mathf.Lerp(hiddenScale, 1f, smoothAlpha);

        billboardRoot.localScale = visibleScale * currentScale;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if(targetCamera == null)
        {
            return;
        }

        //让提示牌始终与摄像机保持相同朝向
        billboardRoot.rotation = targetCamera.transform.rotation;
    }

    public void Show()
    {
        shouldBeVisible = true;
    }

    public void Hide()
    {
        shouldBeVisible = false;
    }

    public void HideImmediately()
    {
        shouldBeVisible = false;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (billboardRoot != null)
        {
            billboardRoot.localScale = visibleScale * hiddenScale;
        }
    }

}
