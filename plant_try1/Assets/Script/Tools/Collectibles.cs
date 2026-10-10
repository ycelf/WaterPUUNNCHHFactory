using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]


public class Collectibles : MonoBehaviour
{

    [Header("物品")]
    [SerializeField] private string itemId = "coin";
    [SerializeField] private string displayName = "金币";
    [Min(1)]
    [SerializeField] private int amount = 1;

    [Header("吸附")]
    [Min(0.1f)]
    [SerializeField] private float pickupRadius = 1.5f;
    [Min(0.05f)]
    [SerializeField] private float flyDuration = 0.04f;

    [Tooltip("只选择墙，地面等阻挡层，不选择玩家层")]
    [SerializeField] private LayerMask obstacleMask;

    [Header("外观")]
    [SerializeField] private Transform visual;
    [SerializeField] private float rotateSpeed = 100f;
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobSpeed = 2f;

    [Header("拾取闪光")]
    [SerializeField] private ParticleSystem pickupEffectPrefab;
    [Min(0.1f)]
    [SerializeField] private float effectLifetime = 2f;

    private enum State
    {
        Waiting,
        Flying,
        Collected
    }

    private State state;
    private PlayerInventory target;

    private Vector3 visualStartPosition;
    private Vector3 initialScale;
    private Vector3 flightStart;
    private float flightTime;

    private void Reset()
    {
        ConfigurePhysics();

        initialScale = transform.localScale;

        if (visual != null)
            visualStartPosition = visual.localPosition;
    }

    private void ConfigurePhysics()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = pickupRadius;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

    }
    private void Update()
    {
        if(state == State.Waiting)
        {
            UpdateIdleVisual();
        }
        else if (state == State.Flying)
        {
            UpdateFlight();
        }
    }
    private void UpdateIdleVisual()
    {
        if (visual == null)
            return;

        visual.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);

        visual.localPosition = visualStartPosition + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;

    }

    private void OnTriggerEnter(Collider other)
    {
        TryBeginPickup(other);
    }

    private void OnTriggerStay(Collider other)
    {
        //如果刚进入的时候被墙挡住，绕过墙后仍然可以拾取。
        TryBeginPickup(other);
    }
    private void TryBeginPickup(Collider other)
    {
        if (state != State.Waiting)
            return;

        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();

        if (inventory == null || !inventory.isActiveAndEnabled)
            return;

        Vector3 direction = inventory.PickupPosition - transform.position;

        //忽略水体，拾取范围等trigger

        if (Physics.Raycast(
            transform.position,direction.normalized,
            out RaycastHit hit,
            direction.magnitude,
            obstacleMask,
            QueryTriggerInteraction.Ignore))
        {
            //即使玩家也在阻挡层，命中玩家自己仍然允许拾取
            if (!hit.transform.IsChildOf(inventory.transform))
                return;
        }

        target = inventory;
        flightStart = transform.position;
        flightTime = 0f;
        state = State.Flying;

        if (visual != null)
            visual.localPosition = visualStartPosition;

        if(pickupEffectPrefab != null)
        {
            ParticleSystem effect = Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);

            effect.Play();
            Destroy(effect.gameObject, effectLifetime);

        }
    }

    private void UpdateFlight()
    {
        //玩家被销毁或停用时，恢复为可拾取状态
        if (target == null || !target.isActiveAndEnabled)
        {
            transform.position = flightStart;
            transform.localScale = initialScale;
            target = null;
            state = State.Waiting;
            return;
        }

        flightTime += Time.deltaTime;
        float t = Mathf.Clamp01(flightTime / flyDuration);

        //先慢后快地飞向玩家，终点随玩家实时移动
        float progress = t * t;
        transform.position = Vector3.Lerp(flightStart, target.PickupPosition, progress);

        //前半段放大一下，后半段缩小消失
        float scale = t < 0.25f
        ? Mathf.Lerp(1f, 1.2f, t / 0.25f)
        : Mathf.Lerp(1.2f, 0.1f, (t - 0.25f) / 0.75f);

        transform.localScale = initialScale * scale;

        if(t >= 1f)
        {
            //先标记，确保只结算一次
            state = State.Collected;
            target.AddItem(itemId, displayName, amount);
            Destroy(gameObject);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
   
}
