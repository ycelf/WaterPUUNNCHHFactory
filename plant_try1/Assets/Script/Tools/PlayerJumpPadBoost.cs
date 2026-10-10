using UnityEngine;
using StarterAssets;


[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(ThirdPersonController))]


public class PlayerJumpPadBoost : MonoBehaviour
{
    [Header("脚下检测")]

    [Tooltip("射线起点高于角色脚底的距离")]
    [Min(0.1f)]
    [SerializeField] private float probeStartHeight = 0.2f;

    [Tooltip("允许脚底与起跳板之间的最大距离")]
    [Min(0.01f)]
    [SerializeField] private float groundCheckDistance = 0.15f;

    private CharacterController character;
    private ThirdPersonController movement;

    private float normalJumpHeight;
    private bool heightCaptured;

    private void Awake()
    {
        character = GetComponent<CharacterController>();
        movement = GetComponent<ThirdPersonController>();
    }

    private void OnEnable()
    {
        normalJumpHeight = movement.JumpHeight;
        heightCaptured = true;
    }

    private void Update()
    {
        float selectedHeight = normalJumpHeight;

        if(character.enabled && movement.enabled)
        {
            Bounds bounds = character.bounds;

            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + probeStartHeight, bounds.center.z);

            float distance = probeStartHeight + groundCheckDistance;

            if(Physics.Raycast(origin,Vector3.down,out RaycastHit hit,distance , movement.GroundLayers, QueryTriggerInteraction.Ignore))
            {
                JumpPad pad = hit.collider.GetComponentInParent<JumpPad>();

                if(pad != null && pad.IsPowered)
                {
                    //起跳板至少保留玩家原本的跳跃高度
                    selectedHeight = Mathf.Max(normalJumpHeight, pad.jumpHeight);
                }
            }
        }
        movement.JumpHeight = selectedHeight;
    }

    private void OnDisable()
    {
        if(movement != null && heightCaptured)
        {
            movement.JumpHeight = normalJumpHeight;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created


    // Update is called once per frame

}
