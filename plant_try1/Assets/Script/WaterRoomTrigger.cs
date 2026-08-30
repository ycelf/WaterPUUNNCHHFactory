using UnityEngine;

public class WaterRoomTrigger : MonoBehaviour
{
    [SerializeField] private WaterRoomController roomController;
    [SerializeField] private string playerTag = "Player";

    private bool playerInside;

    private void OnTriggerEnter(Collider other)//判断玩家有没有进入触发区域，然后通知水房间系统
        //不论进入的collider是什么，进入了就把它传给other
    {
        Debug.Log("jinlaile");
        CharacterController characterController = other.GetComponentInParent<CharacterController>();

        if(characterController == null || !characterController.CompareTag(playerTag))
        {
            return;
        }

        if (playerInside)
        {
            return;
        }

        playerInside = true;
        roomController.HandlePlayerEnter();
    }//只负责识别


    private void OnTriggerExit(Collider other)
    {
        CharacterController characterController = other.GetComponentInParent<CharacterController>();

        if (characterController == null || !characterController.CompareTag(playerTag))
        {
            return;
        }

        if (!playerInside)
        {
            return;
        }

        playerInside = false;
        roomController.HandlePlayerExit();

    }
    private void Start()
    {
        Debug.Log("jinlaile");
    }
}
