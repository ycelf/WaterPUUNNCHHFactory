using UnityEngine;
using TMPro;
using System.Collections.Generic;


[RequireComponent(typeof(CanvasGroup))]
public class PickupNotificationUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private TMP_Text messageText;

    [Tooltip("可选，常驻金币总数文本")]
    [SerializeField] private TMP_Text coinTotalText;

    [SerializeField] private float holdDuration = 1.3f;
    [Min(0.05f)]
    [SerializeField] private float fadeDuration = 0.3f;

    private struct Notice
    {
        public string id;
        public string displayName;
        public int amount;
    }

    private readonly Queue<Notice> queue = new Queue<Notice>();

    private CanvasGroup canvasGroup;
    private Notice current;
    private bool showing;
    private float elapsed;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

    }


    private void OnEnable()
    {
        if (inventory != null)
            inventory.ItemCollected += HandleCollected;

        RefreshCoinTotal();
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.ItemCollected -= HandleCollected;

        queue.Clear();
        showing = false;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void HandleCollected(string id,string displayName , int amount)
    {
        RefreshCoinTotal();

        if(showing && current.id == id)
        {
            current.amount += amount;
            elapsed = 0f;
            UpdateMessage();
            canvasGroup.alpha = 1f;
            return;
        }

        queue.Enqueue(new Notice
        {
            id = id,
            displayName = displayName,
            amount = amount
        });

        if (!showing)
            ShowNext();

    }

    private void ShowNext()
    {
        if(queue.Count == 0)
        {
            showing = false;
            canvasGroup.alpha = 0f;
            return;
        }

        current = queue.Dequeue();
        showing = true;
        elapsed = 0f;
        canvasGroup.alpha = 1f;
        UpdateMessage();
    }

    private void UpdateMessage()
    {
        if (messageText != null)
            messageText.text = $"Gain:{current.displayName} *{current.amount}";
    }

    private void RefreshCoinTotal()
    {
        if (inventory != null && coinTotalText != null)
            coinTotalText.text = $"Coin:{inventory.GetCount("coin")}";
    }

    private void Update()
    {
        if (!showing)
            return;

        elapsed += Time.deltaTime;

        if (elapsed <= holdDuration)
            return;

        float fade = Mathf.Clamp01(
            (elapsed - holdDuration) / fadeDuration);

        canvasGroup.alpha = 1f - fade;

        if (fade >= 1f)
            ShowNext();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    
}
