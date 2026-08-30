using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public enum WaterDeviceMode
{
    Freeze,
    Delay,
    StepBack,
    Solve
}
public class WaterControlDevice : MonoBehaviour
{
    [Header("控制哪个房间")]
    [SerializeField]
    private WaterRoomController roomController;

    [Header("机关类型")]
    [SerializeField]
    private WaterDeviceMode deviceMode;

    [Header("互动")]
    [SerializeField]
    private Key InteraactKey = Key.E;

    [Header("Delay模式")]
    [SerializeField]
    private float delaySeconds = 5f;

    [Header("机关设置")]
    [SerializeField]
    private bool oneShot = true;

    private bool playerInsede;

    private bool hasBeenUsed;

    private Coroutine delayRoutine;

    private void Update()
    {
        //玩家不在互动范围内
        if (!playerInsede)
        {
            return;

        }
        //一次性机关已经使用过
        if (oneShot && hasBeenUsed)
        {
            return;
        }

        //键盘不存在
        if (Keyboard.current[InteraactKey].wasPressedThisFrame)
        {
            Interact();
        }
    }

    private void Interact()
    {
        if (roomController == null)
        {
            Debug.LogWarning($"{name}:没有设置WaterROomCOntroller");
            return;
        }
        switch (deviceMode)
        {
            case WaterDeviceMode.Freeze:
                FreezeWater();

                break;

            case WaterDeviceMode.Delay:

                DelayWater();

                break;

            case WaterDeviceMode.StepBack:

                StepBackWater();

                break;

            case WaterDeviceMode.Solve:

                SolveRoom();

                break;

        }

        hasBeenUsed = true;
    }

    //====
    //1.停在当前水位
    //====

    private void FreezeWater()
    {
        roomController.PauseCycle();

        Debug.Log("水循环被冻结");
    }
    //====
    //2.暂停几秒，然后继续
    //====

    private void DelayWater()
    {
        if (delayRoutine != null)
        {
            StopCoroutine(delayRoutine);
        }
        delayRoutine = StartCoroutine(DelayWaterRoutine());
    }

    public void ResetDevice()
{
    hasBeenUsed = false;

    if (delayRoutine != null)
    {
        StopCoroutine(delayRoutine);
        delayRoutine = null;
    }

    Debug.Log("机关已重置，可以再次互动");
}

    private IEnumerator DelayWaterRoutine()
    {
        roomController.PauseCycle();

        Debug.Log($"水循环暂停{delaySeconds}秒");

        yield return new WaitForSeconds(delaySeconds);

        //如果期间房间已经被solve，就不要把水重新启动
        if (roomController.IsRunning && roomController.IsPaused)
        {
            roomController.StartCycle();

            Debug.Log("水循环重新开始");


        }
        delayRoutine = null;

    }

    private void StepBackWater()
    {
        roomController.StepBackOneStage();

        Debug.Log("水位退回上一阶段");
    }

    //=====
    //完全解决
    //===

    private void SolveRoom()
    {
        roomController.SolveRoom();

        Debug.Log("水循环彻底停止，房间完成");
    }

    //====
    //判断玩家是否靠近
    //===
    private void OnTriggerEnter(Collider other)
    {
        CharacterController characterController = other.GetComponentInParent<CharacterController>();

        if(characterController == null)
        {
            return;
        }

        if (!characterController.CompareTag("Player"))
        {
            return;

        }

        playerInsede = true;

        Debug.Log($"可以互动：按{InteraactKey}");

    }

    private void OnTriggerExit(Collider other)
    {
        CharacterController characterController = other.GetComponentInParent<CharacterController>();

        if(characterController == null)
        {
            return;
        }
        if (!characterController.CompareTag("Player"))
        {
            return;
        }
        playerInsede = false;
    }


}





    
