# 阶梯水流：安装与调节

这是独立的视觉效果，不引用 WaterRoomController、PipeWaterVFX 或游泳脚本，也不会改变游戏水位。

## 第一次安装

1. 等 Unity 导入本文件夹。场景里创建空物体 StairWaterFlow，Scale 保持 (1,1,1)。
2. 添加 StairWaterFlowVFX 组件。Flow Shader 应为 WaterFX/Stair Flow；为空时把同目录 StairWaterFlow.shader 拖进去。
3. 点击组件右上角三个点，选择 Create Example Path。会生成 Path 和六个示范路径点。
4. 选中 StairWaterFlow，开启 Scene 视图的 Gizmos，可以看到青色路径线。
5. 把点从高到低放在中央水槽内。点的顺序取决于 Path 下的 Hierarchy 顺序，不取决于名字或高度。
6. 每层落差至少放两个点：一个在上层平台前沿稍微向外，另一个在下层落点。每段平台用平台上的前后两个点连接。
7. 按 Play。水带上出现顺流的泡沫粒子，符合条件的落点会出现小水花。

示例侧视路径：

    P0 ---- P1     上层平台
            |
            P2 ---- P3     中层平台
                    |
                    P4 ---- P5     最下层出口

路径点本身放在表面略上方约 0.02～0.05 米，Surface Offset 默认还会沿表面法向抬高 0.035 米。
竖直落差的顶部点稍微放到台阶外面，避免水带贴进立面。不要用一个长斜线跨过多级台阶，否则水带会穿过中间平台。
遇到不平整的坡面，复制一个点到折角处，并在 Hierarchy 中拖到正确顺序。
所有点必须是 Path 的直接子物体，点之间不要重合，不要把后一个点放到上游。

## 参数起步值

| 字段 | 建议 | 用途 |
| --- | --- | --- |
| Width | 1.2 | 整条水带宽度，单位按场景米制 |
| Surface Offset | 0.035 | 避免与地面重叠闪烁；穿台阶时先检查路径 |
| Flow Speed | 2.5 | 下游方向的流动速度 |
| Foam Amount | 0.55 | 水带纹理中的白色泡沫量 |
| Texture Scale | 2 | 越大，纵向泡沫纹理越细密 |
| Foam Per Second | 35 | 每秒产生多少沿路径移动的粒子 |
| Max Foam Particles | 400 | 流动泡沫粒子的上限 |
| Foam Size | 0.16 | 单颗粒子大小 |
| Landing Splashes | 开启 | 自动在落差底部发射水花 |
| Minimum Drop | 0.3 | 前一个点到当前点的最小高度差 |
| Splash Per Landing | 12 | 每个落点每秒的水花数量 |
| Splash Up Speed | 1.1 | 水花向上速度 |
| Fade Seconds | 0.35 | 开关时淡入淡出的时间 |

全局 Width 先设置为最窄水槽宽度的约 70%～85%。需要宽窄变化很大时，用多个独立 StairWaterFlow 覆盖不同段。
水花判定：前一段高度下降至少 Minimum Drop，后一段两点高度差小于 0.25 米。终点前有明显落差时也会发射。
本版本的水花是短寿命装饰粒子，不执行碰撞检测。

## 开关连接

脚本提供 StartFlow()、StopFlow()、ToggleFlow()、SetFlowEnabled(bool)。
可以把 StairWaterFlow 物体拖进现有 UnityEvent，选择对应方法。
需要由代码调用时，另一个脚本持有 StairWaterFlowVFX 引用即可；原水位控制器不需要成为本脚本的依赖。
StopFlow 会停止发射并让整个特效淡出，不模拟关阀后残留水的排空。

## 排查

- 看不到水：确认组件启用、Flow Enabled 勾选、Path 至少两个不重合的直接子物体、Shader 引用正确。
- 水从台阶中穿过：每个立面的顶部和底部都要有点。路线按相邻点直线连接。
- 水变成一大片斜坡：你漏了平台边缘点或落差底部点。
- 只有水带没有粒子：粒子只在 Play Mode 发射，编辑模式只预览水带。
- 粒子像大团棉花：把 Foam Size 降到 0.08～0.12，再适当增加 Foam Per Second。
- 看不到小水花：落点后再放一个同高度平台点，检查 Minimum Drop；开启 Landing Splashes。
- 改路径后粒子消失一瞬：路径变化会重建水带并清理旧粒子，几秒后会重新充满。
- 水面粉色：看 Console 中的 Shader 错误；项目必须使用 URP。
- 路线在狭窄转角溢出：缩小 Width，在转角增加点，避免突然掉头。

## 性能和限制

静止路线不会每帧重建 Mesh。活动泡沫粒子每帧更新位置，复用 Particle 数组；没有逐粒子射线或地形碰撞。
移动路径点或改 Inspector 配置会重建特效；它适合静态关卡路径。
默认最多 400 个流动粒子和 256 个水花粒子；许多水流同时出现时减少发射数量。
基础 Shader 是无光照的风格化水流，不包含折射、场景反射或真实流体计算。
末端不会自动识别已有水体：把最后一个点放在目标水面附近，和已有水面视觉衔接。

新增文件的 C# 已用项目现有编译引用检查；实际水带宽度、粒子观感和 GPU Shader 效果仍需在 Unity 中预览。
