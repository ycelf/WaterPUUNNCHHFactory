using System.Collections;
using UnityEngine;

public class WaterHazard : MonoBehaviour
{
    [Header("Water Room")]
    [SerializeField] private WaterRoomController roomController;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("The collider that represents the current water block. If empty, the collider on this object is used.")]
    [SerializeField] private Collider waterVolume;

    [Header("Chest And Head Detection")]
    [Tooltip("Fallback chest height used for the swimming animation when the player does not have a WaterPunchWaterSafety component.")]
    [Min(0.1f)]
    [SerializeField] private float fallbackChestDetectionPointHeight = 1f;

    [Tooltip("Fallback head height used for drowning when the player does not have a WaterPunchWaterSafety component.")]
    [Min(0.1f)]
    [SerializeField] private float fallbackHeadDetectionPointHeight = 1.65f;

    [Tooltip("Fallback lower-body height used to keep the player in water until the legs leave it.")]
    [Min(0.05f)]
    [SerializeField] private float fallbackLowerBodyDetectionPointHeight = 0.2f;

    [Header("Death Countdown")]
    [Min(0f)]
    [SerializeField] private float deathDelay = 2f;

    [Tooltip("死亡动画开始后，等待多久复活。禁止游泳的房间不使用 Death Delay。")]
    [Min(0.1f)]
    [SerializeField] private float drowningAnimationSeconds = 1f;

    [Tooltip("禁止游泳时直接进入此状态，无需先进入 Swim。")]
    [SerializeField] private string drowningStateName = "Base Layer.drown";

    private CharacterController playerInWater;
    private Coroutine deathCountdownRoutine;
    private bool lethalDeathCommitted;
    private StarterAssets.ThirdPersonController lockedMovement;
    private bool movementWasEnabled;
    private Animator lockedAnimator;
    private bool rootMotionWasEnabled;
    private bool reportedMissingRespawn;

    private bool AllowsSwimming => roomController == null || roomController.AllowSwimming;

    private void Reset()
    {
        roomController = GetComponentInParent<WaterRoomController>();
        waterVolume = GetComponent<Collider>();
    }

    private void Awake()
    {
        if (roomController == null)
        {
            roomController = GetComponentInParent<WaterRoomController>();
        }

        if (waterVolume == null)
        {
            waterVolume = GetComponent<Collider>();
        }
    }

    private void LateUpdate()
    {
        // A lethal room commits death immediately. Water lowering or leaving it cannot cancel it.
        if (lethalDeathCommitted) return;

        if (playerInWater == null)
        {
            TryAcquirePlayer();
        }

        if (playerInWater == null)
        {
            return;
        }

        WaterPunchWaterSafety swimming = playerInWater.GetComponent<WaterPunchWaterSafety>();
        // Another overlapping volume may already own the player's death sequence.
        if (swimming != null && swimming.IsDrowning) return;

        if (waterVolume == null || !waterVolume.enabled || !waterVolume.gameObject.activeInHierarchy)
        {
            StopDeathCountdown();
            SetPlayerWaterState(playerInWater, false, false, false);
            playerInWater = null;
            return;
        }

        UpdateWaterSurface(swimming);

        bool lowerBodySubmerged = IsLowerBodySubmerged(swimming);
        bool chestSubmerged = IsChestSubmerged(swimming);
        bool headSubmerged = IsHeadSubmerged(swimming);
        bool bodyInWater = lowerBodySubmerged || chestSubmerged || headSubmerged;
        SetPlayerWaterState(playerInWater, bodyInWater, chestSubmerged, headSubmerged);

        if (!AllowsSwimming && chestSubmerged)
        {
            BeginLethalDrowning(playerInWater, swimming);
            return;
        }

        if (!bodyInWater)
        {
            StopDeathCountdown();
            playerInWater = null;
            return;
        }

        if (!AllowsSwimming || !headSubmerged)
        {
            StopDeathCountdown();
            return;
        }

        if (deathCountdownRoutine == null)
        {

            deathCountdownRoutine = StartCoroutine(DeathCountdown(playerInWater));
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CharacterController characterController = other.GetComponentInParent<CharacterController>();
        if (characterController == null || !characterController.CompareTag(playerTag))
        {
            return;
        }

        if (playerInWater == null || playerInWater == characterController)
        {
            playerInWater = characterController;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CharacterController characterController = other.GetComponentInParent<CharacterController>();
        if (characterController == null || characterController != playerInWater)
        {
            return;
        }

        // Keep tracking until the lower-body detection point leaves the water volume.
    }

    private void TryAcquirePlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject == null)
        {
            return;
        }

        CharacterController characterController = playerObject.GetComponent<CharacterController>();
        WaterPunchWaterSafety swimming = characterController != null
            ? characterController.GetComponent<WaterPunchWaterSafety>()
            : null;
        Vector3 lowerBodyPoint = swimming != null && swimming.LowerBodyDetectionPoint != null
            ? swimming.LowerBodyDetectionPoint.position
            : playerObject.transform.position + Vector3.up * fallbackLowerBodyDetectionPointHeight;

        Vector3 chestPoint = characterController != null
            ? GetChestDetectionPosition(characterController, swimming)
            : playerObject.transform.position + Vector3.up * fallbackChestDetectionPointHeight;
        if (characterController != null && waterVolume != null && waterVolume.enabled &&
            waterVolume.gameObject.activeInHierarchy &&
            (waterVolume.bounds.Contains(lowerBodyPoint) || waterVolume.bounds.Contains(chestPoint)))
        {
            playerInWater = characterController;
        }
    }

    private void UpdateWaterSurface(WaterPunchWaterSafety swimming)
    {
        if (swimming != null)
        {
            swimming.SetWaterSurfaceHeight(waterVolume.bounds.max.y);
        }
    }

    private bool IsChestSubmerged(WaterPunchWaterSafety swimming)
    {
        return IsPointInsideWaterVolume(GetChestDetectionPosition(playerInWater, swimming));
    }

    private Vector3 GetChestDetectionPosition(CharacterController player, WaterPunchWaterSafety swimming)
    {
        if (!AllowsSwimming)
        {
            if (swimming != null) return swimming.GetLethalChestDetectionPosition();
            return player.transform.TransformPoint(player.center + Vector3.up * (player.height * 0.25f));
        }

        return swimming != null && swimming.DrowningDetectionPoint != null
            ? swimming.DrowningDetectionPoint.position
            : player.transform.position + Vector3.up * fallbackChestDetectionPointHeight;
    }

    private bool IsHeadSubmerged(WaterPunchWaterSafety swimming)
    {
        Vector3 detectionPoint = swimming != null
            ? swimming.GetHeadDrowningDetectionPosition()
            : playerInWater.transform.position + Vector3.up * fallbackHeadDetectionPointHeight;

        return IsPointInsideWaterVolume(detectionPoint);
    }

    private bool IsLowerBodySubmerged(WaterPunchWaterSafety swimming)
    {
        Vector3 detectionPoint = swimming != null && swimming.LowerBodyDetectionPoint != null
            ? swimming.LowerBodyDetectionPoint.position
            : playerInWater.transform.position + Vector3.up * fallbackLowerBodyDetectionPointHeight;

        return IsPointInsideWaterVolume(detectionPoint);
    }

    private bool IsPointInsideWaterVolume(Vector3 worldPosition)
    {
        return waterVolume.bounds.Contains(worldPosition);
    }

    private void SetPlayerWaterState(CharacterController characterController, bool lowerBodySubmerged, bool chestSubmerged, bool headSubmerged)
    {
        WaterPunchWaterSafety swimming = characterController.GetComponent<WaterPunchWaterSafety>();
        if (swimming != null)
        {
            swimming.SetSwimmingAllowed(!lowerBodySubmerged || AllowsSwimming);
            swimming.SetInWater(lowerBodySubmerged);
            swimming.SetChestSubmerged(chestSubmerged);
        }

        Animator animator = characterController.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetBool("IsInWater", swimming != null ? swimming.IsSwimming : chestSubmerged);
            if (!headSubmerged)
            {
                animator.SetBool("IsDrown", false);
            }
        }
    }

    private void StopDeathCountdown()
    {
        if (deathCountdownRoutine == null)
        {
            return;
        }

        StopCoroutine(deathCountdownRoutine);
        deathCountdownRoutine = null;
    }

    private IEnumerator DeathCountdown(CharacterController characterController)
    {
        yield return new WaitForSeconds(deathDelay);

        Animator animator = characterController.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetBool("IsDrown",true);
        }

        yield return new WaitForSeconds(drowningAnimationSeconds);

        deathCountdownRoutine = null;
        WaterPunchWaterSafety swimming = characterController.GetComponent<WaterPunchWaterSafety>();
        if (playerInWater != characterController || !IsHeadSubmerged(swimming))
        {
            yield break;
        }

        SetPlayerWaterState(characterController, false, false, false);
        playerInWater = null;

        if (roomController != null)
        {
            roomController.RespawnPlayer(characterController.transform);
        }
    }

    private void OnDisable()
    {
        StopDeathCountdown();

        if (lethalDeathCommitted)
        {
            ReleaseLethalDrowning();
        }

        if (playerInWater != null &&
            !(playerInWater.GetComponent<WaterPunchWaterSafety>()?.IsDrowning ?? false))
        {
            SetPlayerWaterState(playerInWater, false, false, false);
        }

        playerInWater = null;
    }

    private void BeginLethalDrowning(CharacterController player, WaterPunchWaterSafety swimming)
    {
        if (roomController == null || !roomController.HasRespawnPoint)
        {
            if (!reportedMissingRespawn)
            {
                Debug.LogError($"{name}: 请先设置房间的 Respawn Point，才能完成溺水复活。", this);
                reportedMissingRespawn = true;
            }
            return;
        }

        StopDeathCountdown();
        lethalDeathCommitted = true;
        if (swimming != null) swimming.SetDrowning(true);

        lockedMovement = player.GetComponent<StarterAssets.ThirdPersonController>();
        if (lockedMovement != null)
        {
            movementWasEnabled = lockedMovement.enabled;
            lockedMovement.enabled = false;
        }

        lockedAnimator = player.GetComponent<Animator>();
        if (lockedAnimator != null)
        {
            rootMotionWasEnabled = lockedAnimator.applyRootMotion;
            lockedAnimator.applyRootMotion = false;
            lockedAnimator.SetBool("IsDrown", true);
            int state = Animator.StringToHash(drowningStateName);
            if (lockedAnimator.HasState(0, state))
                lockedAnimator.CrossFadeInFixedTime(state, 0.1f, 0);
        }

        deathCountdownRoutine = StartCoroutine(LethalDrowning(player));
    }

    private IEnumerator LethalDrowning(CharacterController player)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, drowningAnimationSeconds));
        deathCountdownRoutine = null;
        ReleaseLethalDrowning();
        if (player != null)
        {
            SetPlayerWaterState(player, false, false, false);
            playerInWater = null;
            if (roomController != null) roomController.RespawnPlayer(player.transform);
        }
    }

    private void ReleaseLethalDrowning()
    {
        if (playerInWater != null)
        {
            WaterPunchWaterSafety swimming = playerInWater.GetComponent<WaterPunchWaterSafety>();
            if (swimming != null)
            {
                swimming.SetDrowning(false);
                swimming.SetSwimmingAllowed(true);
            }
        }

        if (lockedAnimator != null)
        {
            lockedAnimator.SetBool("IsDrown", false);
            lockedAnimator.SetBool("IsInWater", false);
            lockedAnimator.applyRootMotion = rootMotionWasEnabled;
            int locomotion = Animator.StringToHash("Base Layer.Idle Walk Run Blend");
            if (lockedAnimator.HasState(0, locomotion))
                lockedAnimator.CrossFadeInFixedTime(locomotion, 0.1f, 0);
        }
        if (lockedMovement != null) lockedMovement.enabled = movementWasEnabled;
        lockedMovement = null;
        lockedAnimator = null;
        lethalDeathCommitted = false;
    }
}
