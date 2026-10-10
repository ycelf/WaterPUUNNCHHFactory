using UnityEngine;
using System;
using System.Collections.Generic;

public class PlayerInventory : MonoBehaviour
{
    [Tooltip("吸附终点相对于玩家的位置，避免金币飞到脚底")]
    [SerializeField] private Vector3 pickupOffset = new Vector3(0, 1.2f, 0);

    private readonly Dictionary<string, int> items = new Dictionary<string, int>();

    //参数：物品id，显示名称，本次获得数量
    public event Action<string, string, int> ItemCollected;

    public Vector3 PickupPosition => transform.TransformPoint(pickupOffset);

    public int GetCount(string itemId)
    {
        return items.TryGetValue(itemId, out int count) ? count : 0;

    }

    public void AddItem(string itemId,string displayName,int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return;

        items[itemId] = GetCount(itemId) + amount;
        ItemCollected?.Invoke(itemId, displayName, amount);
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
