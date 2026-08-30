using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.Analytics;



public enum WaterCycleMode//水循环时的模式
{
    PresetSequence, //按照预设绝对水位循环
    Accumulating //每轮累计一档，直到最高水位

}

public enum WaterStopMode//水会停下的时候的模式？
{
    Pause,      //暂停再当前水位和计时阶段
    ResetImmediately,   //立刻停止并回到初始水位
    FinishCurrentStepThenReset//当前阶段完成后立刻回到初始水位
}

public enum WaterExitBehaviour
{
    StopAndReset,       //停下并且重置
    Pause,              //暂停
    KeepRunning         //继续的状态
}

[Serializable]//可下拉菜单
public class WaterLevelStep
{
    [Tooltip("便于识别，例如：安全期、中水位、高水位")]
    public string stepName;//填上面那些名字

    [Min(0f)]//限制最小值不能小于0
    [Tooltip("水面距离房间地面的绝对高度")]
    public float absoluteHeight;

    [Min(0f)]
    [Tooltip("保持该水位的时间")]
    public float holdSeconds = 3f;

}

[Serializable]
public class WaterTransferLink
{
    [Tooltip("接受上层排水的下层房间")]
    public WaterRoomController targetRoom;

    [Min(0.01f)]
    [Tooltip("上层水位每下降多少米")]
    public float sourceDropAmount = 1f;

    [Min(0f)]
    [Tooltip("上层水位对应上升多少米")]
    public float targetRiseAmount = 1f;

    [Min(0f)]
    [Tooltip("上层下降后，等多少秒再让下层上涨")]
    public float transferDelay = 2f;
}

[Serializable]
public class FloatEvent: UnityEvent<float>  //广播一个floatEvent
{

}



public class WaterRoomController : MonoBehaviour
{
    //现在开始定义那些变量
    [Header("水体对象")]
    [SerializeField] private Transform waterBody;

    [Tooltip("水体上的致死触发器，collider那种，box挺好")]
    [SerializeField] private Collider waterTrigger;

    [Tooltip("水体底面相对于父物体的本地y坐标，啥啊")]
    [SerializeField] private float waterBottomLocalY;

    [Min(0.01f)]
    [Tooltip("模型在scaleY=1时的实际高度。Unity Cube一般填1")]
    [SerializeField] private float meshHeightAtScaleOne = 1f;

    [Min(0f)]
    [SerializeField] private float initialHeight;//最开始的高度

    [Min(0.01f)]
    [SerializeField] private float maximumHeight = 10f;

    [Min(0f)]
    [Tooltip("低于这个高度时关闭致死触发器")]
    [SerializeField] private float minimumDangerousHeight = 0.05f;

    [Header("循环类型")]
    [SerializeField] private WaterCycleMode cycleMode;

    [Min(0f)]
    [Tooltip("房间开始运行后，首次变化前的等待时间")]
    [SerializeField] private float startDelay;

    [Header("预设序列模式")]
    [SerializeField] private List<WaterLevelStep> presetSteps = new();
    //创建一个空表，里面只能装waterlevelstep类型的数据，定义一个变量名presetstep，然后真的建一个新表new（）

    [Header("累计模式")]
    [Min(0.01f)]
    [SerializeField] private float heightPerPulse = 1f;//每次涨潮涨多少

    [Min(0f)]
    [Tooltip("每次涨水前等待多久")]
    [SerializeField] private float accumulationInterval = 5f;

    [Min(0f)]
    [Tooltip("每次涨水后停留多久，再进入下一轮等待")]
    [SerializeField] private float holdAfterPulse = 2f;

    [Header("进入与离开")]
    [SerializeField] private bool startWhenPlayerEnters = true;//检测玩家有没有进入
    [SerializeField] private WaterExitBehaviour exitBehaviour = WaterExitBehaviour.StopAndReset;//选waterexitvehaviour里的stopandreset状态

    [Header("死亡与复活")]
    [SerializeField] private Transform respawnPoint;

    [Tooltip("掉进水里复活后，是否让房间循环重新开始")]
    [SerializeField] private bool restartCycleAfterRespawn = true;

    [Header("上下层水量的联动")]
    [SerializeField] private List<WaterTransferLink> lowerRoomLinks = new();

    [Header("事件：可以链接粒子、声音、警报、闸门动画etc")]
    public UnityEvent onCycleStarted;
    public UnityEvent onCyclePaused;
    public UnityEvent onCycleStopped;
    public UnityEvent onRoomReset;

    [Tooltip("每次有一大堆水进入房间的时候触发，可以播放泄水的特效")]
    public UnityEvent onFloodPulse;

    public UnityEvent onWaterRaised;
    public UnityEvent onWaterLowered;
    public FloatEvent onWaterHeightChanged;

    public float CurrentHeight { get; private set; }
    public bool IsRunning => isRunning;
    public bool IsPaused => isPaused;

    private Coroutine cycleRoutine;
    private bool isRunning;
    private bool isPaused;
    private bool finishCurrentStepThenReset;
    private bool isRespawning;
    private int currentPresetIndex = 0;
    //如果外部机关修改了当前阶段，通知RunPresent Sequence 不要继续进入下一阶段
    private bool presetStepChangedExternally;

    private const float HeightEpsilon = 0.001f;

    private void Awake()
    {
        if(waterBody == null)
        {
            Debug.LogError($"{name}:没有指定 Water Body",this);
            enabled = false;
            return;
        }//检查一下有没有放waterbody，必须要有

        initialHeight = Mathf.Clamp(initialHeight, 0f, maximumHeight);
        //mathf是unity自带的数学工具，clamp是把数字限定在范围内（要限制的数字，min，max）
        SetWaterHeight(initialHeight, sendWaterToLowerRooms: false);
    }
    //====
    //对外控制接口
    //====

    public void StartCycle()//开始循环
    {
        //已经在运行，只是处于暂停状态，直接恢复
        if (isRunning)//如果已经在运行
        {
            isPaused = false;//让pause保持false
            finishCurrentStepThenReset = false;//finishcurrent结束当前循环的那个bool变成false
            return;//ok这个是打断循环，让上面的执行完之后就到下一步

        }

        isRunning = true;//开始后set这个running也就是运行中的为true
        isPaused = false;//让暂停变成false
        finishCurrentStepThenReset = false;//结束循环的bool变成false

        cycleRoutine = StartCoroutine(RunCycle());//这个是啥，哦开始循环
        onCycleStarted?.Invoke();//那个问号是啥，哦是空运算符号，如果最开始那个变量不是空的就调用它，是空的就不管，哦invoke是开始启动粒子效果之类的

    }

    public void StopCycle(WaterStopMode stopMode)//void是执行操作但不返回结果，stopcycle是名字，之后的是说必须传进来一个WaterStopMode的数据
    {
        //用什么模式停止循环↓
        switch (stopMode)//根据选项分流
        {
            case WaterStopMode.Pause:
                PauseCycle();
                break;//是说如果执行了就跳出分支，不要在执行下面的case

            case WaterStopMode.ResetImmediately:
                StopAndResetNow();
                break;

            case WaterStopMode.FinishCurrentStepThenReset:
                StopAfterCurrentStep();
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(stopMode),//得到文字stopmode，用于告诉报错系统哪里错了
                    stopMode,
                    null);//如果不是上面三个中的任意一个就报错


        }
    }    //这些物参数方法方便直接链接到inspector的UnityEvent

    public void PauseCycle()//不重置水位，只暂停循环
    {
        if (!isRunning)//暂停循环
        {
            return;
        }

        isPaused = true;
        onCyclePaused?.Invoke();//有特效不为空就释放
    }

    public void StopAndResetNow()
    {
        //先记录停止之前是不是处于活动状态
        bool wasActive = isRunning || cycleRoutine != null;
        //isrunning程序还在运行，或者水循环还在进行中，两者二选一只要一个成立就was active

        StopAllCoroutines();//停下所有的内容

        //清理运行状态
        cycleRoutine = null;//现在已经没有水循环协程
        isRunning = false;//循环没有运行
        isPaused = false;//也不在暂停状态
        finishCurrentStepThenReset = false;//取消完成当前阶段后重置的命令

        currentPresetIndex = 0;
        presetStepChangedExternally = false;
        


        //重置不能把“凭空消失的水”传给下层
        SetWaterHeight(initialHeight, sendWaterToLowerRooms: false);//把水位复原到初始高度
        //false那段是让水从10m重置到0m的时候，不要让下层以为要接收

        if (wasActive)
        {
            onCycleStopped?.Invoke();
        }

        onRoomReset?.Invoke();
    }

    public void StopAfterCurrentStep()
    {
        if (!isRunning)
        {
            StopAndResetNow();
            return;
        }

        //如果此前暂停了，先恢复，完成当前阶段后再停止
        isPaused = false;
        finishCurrentStepThenReset = true;

    }

    public void ResetRoom()
    {
        StopAndResetNow();
    }

    public void RaiseWaterBy(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        onFloodPulse?.Invoke();
        SetWaterHeight(
            CurrentHeight + amount,
            sendWaterToLowerRooms: false);
    }

    public void LowerWaterBy(float amount)
    {
        LowerWaterBy(amount, true);
    }

    public void LowerWaterWithoutTransfer(float amount)
    {
        LowerWaterBy(amount, false);
    }

public void StepBackOneStage()
    {
        //====
        //累计上涨模式
        //====
        if(cycleMode == WaterCycleMode.Accumulating)
        {
            float targetHeight = Mathf.Max(initialHeight, CurrentHeight - heightPerPulse);

            SetWaterHeight(targetHeight, sendWaterToLowerRooms: true);

            Debug.Log($"累计模式退回一档：{CurrentHeight}");

            return;
        }

        //====
        //预设阶段模式
        //===

        if(presetSteps == null || presetSteps.Count == 0)
        {
            return;
        }

        //已经是第一阶段了就不能往前退
        if(currentPresetIndex <= 0)
        {
            currentPresetIndex = 0;

        }
        else
        {
            currentPresetIndex--;
        }

        WaterLevelStep previousStep = presetSteps[currentPresetIndex];

        if(previousStep == null)
        {

            return;
        }

        SetWaterHeight(previousStep.absoluteHeight, sendWaterToLowerRooms: true);

        //告诉正在运行的循环：阶段刚被机关修改过，不要继续跳下一档
        presetStepChangedExternally = true;

        Debug.Log($"退回阶段{currentPresetIndex}:" + $"{previousStep.stepName}");
    }

    public void SolveRoom()
    {
        StopAllCoroutines();

        cycleRoutine = null;
        isRunning = false;
        isPaused = false;
        finishCurrentStepThenReset = false;

        SetWaterHeight(0f, sendWaterToLowerRooms: false);

        onCycleStopped?.Invoke();
    }
    public void SetAbsoluteWaterHeight(float height)
    {
        SetWaterHeight(height, sendWaterToLowerRooms: true);
    }

    //======
    //玩家进入、离开与死亡
    //======

    public void HandlePlayerEnter()
    {
        if (startWhenPlayerEnters)
        {
            StartCycle();
        }
    }

    public void HandlePlayerExit()
    {
        switch (exitBehaviour)
        {
            case WaterExitBehaviour.StopAndReset:
                StopAndResetNow();
                break;

            case WaterExitBehaviour.Pause:
                PauseCycle();
                break;

            case WaterExitBehaviour.KeepRunning:
                break;

            default:
                throw new ArgumentOutOfRangeException();

        }
    }

    public void RespawnPlayer(Transform playerRoot)
    {
        if(isRespawning || playerRoot == null || respawnPoint == null)
            //如果在respawn重生中，就不能，如果playerroot这边没有玩家，则不能，如果rewpawnPoint没有重生点，则不能
        {
            return;
        }

        isRespawning = true;
        //避免玩家一碰到水就反复死死活活仰卧起坐

        //重置前记住复活后要不要重新开始循环，同时满足下面两个
        bool shouldRestart =
            restartCycleAfterRespawn && //&&是并且的意思，aka↑restartCycle被勾选，and↓房间原本在运行，或者玩家进入后自动运行
            (isRunning || startWhenPlayerEnters);
        StopAndResetNow();
        //立刻停止水循环，取消暂停，恢复initialHeight初始水位，触发停止重置事件，这个会把is running 重置成false所以要提前保存

        CharacterController characterController =
            playerRoot.GetComponent<CharacterController>();//找到charactercontroller

        //characterController 开着的时候直接修改transfor，有时会被它的碰撞状态干扰
        //提前关掉它

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        playerRoot.SetPositionAndRotation(
            respawnPoint.position,
            respawnPoint.rotation);
        //把玩家的位置和旋转设置成复活点的

        if (characterController != null)
        {
            characterController.enabled = true;
        }
        //传送完成后把characterController再打开

        if(shouldRestart)
        {
            StartCycle();
        }

        isRespawning = false;
        //解除复活锁，之后可以重新死

    }
    //=====
    //循环逻辑
    //=====
//    检查能不能复活
//→ 锁住复活状态
//→ 记录是否需要重启循环
//→ 停止并重置房间
//→ 暂时关闭 CharacterController
//→ 把玩家传送到复活点
//→ 重新开启 CharacterController
//→ 按配置重新开始水循环
//→ 解除复活锁

    private IEnumerator RunCycle()//总调度，ienumerator说明可以分很多帧慢慢执行，用yield return是，可以暂停在这里，之后继续。yield break是整体到此结束
    {
        if(startDelay > 0f)
        {
            yield return WaitPausable(startDelay);//执行waitPausable，等到它彻底执行完，再往下继续

            if (TryFinishAndReset())
            {
                yield break;//over
            }
        }

        switch (cycleMode)
        {
            case WaterCycleMode.PresetSequence:
                yield return RunPresetSequence();
                //进入预设的水位循环，并等待它执行完毕
                break;

            case WaterCycleMode.Accumulating:
                yield return RunAccumulatingCycle();
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

//    IEnumerator  = 能暂停、能继续的方法
//yield return = 暂停，之后接着运行
//yield break  = 协程彻底结束
//continue     = 跳过本轮循环
//break        = 跳出循环或 switch
    private IEnumerator RunPresetSequence()//按照预设表循环水位
    {
        if(presetSteps == null || presetSteps.Count == 0)//依次执行每个水位阶段
        {
            Debug.LogWarning($"{name}:预设水位序列为空。", this);
            FinishCycleWithoutReset();
                yield break;//没有配置任何阶段就发出警告
        }

        while (isRunning)//只要还在运行，就反复播放下面这些
        {
           //防止index越界
           if(currentPresetIndex >= presetSteps.Count)
            {
                currentPresetIndex = 0;
            }

            WaterLevelStep step = presetSteps[currentPresetIndex];

            //如果现在这个阶段是空的
            if(step == null)
            {
                currentPresetIndex++;
                continue;
            }

            float targetHeight = Mathf.Clamp(step.absoluteHeight, 0f, maximumHeight);

            //如果是在涨水
            if(targetHeight > CurrentHeight + HeightEpsilon)
            {
                onFloodPulse?.Invoke();
            }

            SetWaterHeight(targetHeight, sendWaterToLowerRooms:true);

            //===
            //本阶段停留
            //====
            presetStepChangedExternally = false;

            float elapsed = 0f;
            while (elapsed < step.holdSeconds)
            {
                //如果玩家被按机关回退了阶段
                //马上结束当前阶段的等待
                if (presetStepChangedExternally)
                {
                    break;
                }

                if (!isPaused)
                {
                    elapsed += Time.deltaTime;
                }
                yield return null;

            }

            if (TryFinishAndReset())
            {
                yield break;

            }

            //如果刚才是外部机关导致的阶段转变
            if (presetStepChangedExternally)
            {
                presetStepChangedExternally = false;

                ////不要currentPresetIndex++
                //直接从新阶段开始
                continue;

            }


            //===
            //正常进入下一阶段
            //===

            currentPresetIndex++;

            //最后一阶段结束后
            //回到第一阶段
            if(currentPresetIndex >= presetSteps.Count)
            {
                currentPresetIndex = 0;
            }
        }
    }

    private IEnumerator RunAccumulatingCycle()//每隔一段时间张一档
    {
        while (isRunning)//要是真的就一直循环
        {
            yield return WaitPausable(accumulationInterval);//等一会

            if (TryFinishAndReset())//检查这次之后要不要重置（打破循环）
            {
                yield break;
            }
            //到最高水位之后循环仍然进行，泄水演出仍可以继续播放

            onFloodPulse?.Invoke();//有没有特效

            SetWaterHeight(
                CurrentHeight + heightPerPulse,//在当前水位上加heightPerPulse，每次涨水加一段
                sendWaterToLowerRooms: false);
            yield return WaitPausable(holdAfterPulse);//涨水后停一会

            if(TryFinishAndReset())
            {
                yield break;
            }

        }
    }

    private IEnumerator WaitPausable(float duration)//可以暂停的计时器
    {
        float elapsed = 0f;//记录等了几秒

        while (elapsed < duration)//要是等待还没达到目标时间就一直循环
        {
            if (!isPaused)//暂停了就不累计，不暂停就↓
            {
                elapsed += Time.deltaTime;//没有暂停就把这一帧经过的时间累加，让倒计时减少i guess
            }

            yield return null;//暂停到下一秒然后回来继续判定
        }
    }

    private bool TryFinishAndReset()//检查要不要结束然后重置
    {
        if (!finishCurrentStepThenReset)//没收到消息就什么都不做
        {
            return false;
        }

        //要是收到指令了就↓清理之前的状态
        cycleRoutine = null;
        isRunning = false;
        isPaused = false;
        finishCurrentStepThenReset = false;

        SetWaterHeight(initialHeight, sendWaterToLowerRooms: false);//水位复原

        onCycleStopped?.Invoke();//触发特效们
        onRoomReset?.Invoke();

        return true;//外面的ienumerator看到true了就yield break结束
    }


    private void FinishCycleWithoutReset()//结束但不恢复水位
    {
        //清理所有循环状态然后
        cycleRoutine = null;
        isRunning = false;
        isPaused = false;
        finishCurrentStepThenReset = false;
        //发点特效
        onCycleStopped?.Invoke();

    }
    //======
    //水位与模型
    //=====

    private void LowerWaterBy(float amount,bool sendWaterToLowerRooms)
        //降低水位，知道amount下降多少水位，以及sendWater这个bool，是否传递到下层
    {
        if (amount<= 0f)//下降量<0就不执行
        {
            return;
        }

        SetWaterHeight(CurrentHeight - amount ,sendWaterToLowerRooms);//计算目标水位
    }

    private void SetWaterHeight(       float requestedHeight,       bool sendWaterToLowerRooms)
        //真的开始修改水位
    {
        float previousHeight = CurrentHeight;//先记录修改之前的，用来之后判断涨跌

        CurrentHeight = Mathf.Clamp(requestedHeight,  0f, maximumHeight);//把目标水位限制在0-max

        ApplyWaterBodyTransform(CurrentHeight);//分局新水位修改场景中的模型

        float difference = CurrentHeight - previousHeight;//计算水位变化

        if(difference > HeightEpsilon)//判断有没有涨水，epsilon是李燕华讲的那个
        {
            onWaterRaised?.Invoke();//有就加特效
        }
        else if (difference < -HeightEpsilon)//明显是负数就说明下降
        {
            onWaterLowered?.Invoke();//特效

            if (sendWaterToLowerRooms)//如果允许传水的话就传过去
            {
                SendDroppedWaterToLowerRooms(-difference);//传下降量
            }
        }

        onWaterHeightChanged?.Invoke(CurrentHeight);//通知一下已经处理完了
    }

    private void ApplyWaterBodyTransform(float height)
    {
        Vector3 scale = waterBody.localScale;
        scale.y = height / meshHeightAtScaleOne;
        waterBody.localScale = scale;
        //取得水模型现在的缩放，然后只改y轴

        Vector3 localPosition = waterBody.localPosition;

        //模型原点通常在中心，因此位置随高度上移一半
        //让水体底面始终留在water BottomLocalY
        localPosition.y = waterBottomLocalY + height * 0.5f;
        waterBody.localPosition = localPosition;

        if( waterTrigger != null)//如果危险水域的触发存在，根据水位决定要不要用
        {
            waterTrigger.enabled = height > minimumDangerousHeight;
        }
    }

    //=========
   //上下层联动
   //==========

    private void SendDroppedWaterToLowerRooms(float actualDropAmount)//计算下层要涨多少
    {
        foreach(WaterTransferLink link in lowerRoomLinks)//遍历这个房间链接的所有房间
        {
            if(link == null || link.targetRoom == null || link.sourceDropAmount <= 0f)
                //怎样会跳过：1链接不存在，2没有指定目标房间，3上层下降量小于等于0
             {
                 continue;
             }

            //↓进行比例转换
        float targetRise =
            actualDropAmount / 
            link.sourceDropAmount * link.targetRiseAmount;

            StartCoroutine(TransferWaterAfterDelay(link.targetRoom, targetRise, link.transferDelay));
            //启动传送协程，不会堵住当前代码，而是在经过设定延迟后让下层涨水
        }        
    }

    private IEnumerator TransferWaterAfterDelay(WaterRoomController targetRoom , float targetRise,float delay)
    //延迟送水
    {
        if(delay > 0f)
        {
            yield return new WaitForSeconds(delay);//设置了延迟的话就等待相应秒数
        }

        if(targetRoom != null)//检查一下目标房间存在否，调用那个房间自己的方法
        {
            targetRoom.ReceiveTransferredWater(targetRise);
        }
    }

    private void ReceiveTransferredWater(float amount)//下层接水
    {
        if(amount <= 0f)//没有进水就不执行
        {
            return;
        }

        onFloodPulse?.Invoke();//特效

        //false:下层上涨不再次递归传递
        SetWaterHeight(CurrentHeight + amount, sendWaterToLowerRooms: false);//下层只是上涨，没有排水，避免继续传播
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
