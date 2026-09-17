namespace StarterAssets
{
    // 角色控制器只读取水中运动数据，具体次数和特效仍由游泳脚本管理。
    public interface IWaterMovementSource
    {
        bool IsInWater { get; }
        bool IsChestSubmerged { get; }
        bool IsSwimming { get; }
        void SetGroundSupport(bool supported);
        float SwimmingGravity { get; }
        float MaximumSinkSpeed { get; }
        bool ConsumeSwimUpwardVelocity(out float upwardVelocity);
        float ConstrainUpwardVelocity(float verticalVelocity, float deltaTime);
    }
}
