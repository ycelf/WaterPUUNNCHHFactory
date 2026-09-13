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

    private CharacterController playerInWater;
    private Coroutine deathCountdownRoutine;

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

    private void Update()
    {
        if (playerInWater == null)
        {
            return;
        }

        if (waterVolume == null || !waterVolume.enabled)
        {
            StopDeathCountdown();
            SetPlayerWaterState(playerInWater, false, false, false);
            playerInWater = null;
            return;
        }

        WaterPunchWaterSafety swimming = playerInWater.GetComponent<WaterPunchWaterSafety>();
        UpdateWaterSurface(swimming);

        bool lowerBodySubmerged = IsLowerBodySubmerged(swimming);
        bool chestSubmerged = IsChestSubmerged(swimming);
        bool headSubmerged = IsHeadSubmerged(swimming);
        SetPlayerWaterState(playerInWater, lowerBodySubmerged, chestSubmerged, headSubmerged);

        if (!lowerBodySubmerged)
        {
            StopDeathCountdown();
            playerInWater = null;
            return;
        }

        if (!headSubmerged)
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

    private void UpdateWaterSurface(WaterPunchWaterSafety swimming)
    {
        if (swimming != null)
        {
            swimming.SetWaterSurfaceHeight(waterVolume.bounds.max.y);
        }
    }

    private bool IsChestSubmerged(WaterPunchWaterSafety swimming)
    {
        Vector3 detectionPoint = swimming != null && swimming.DrowningDetectionPoint != null
            ? swimming.DrowningDetectionPoint.position
            : playerInWater.transform.position + Vector3.up * fallbackChestDetectionPointHeight;

        return IsPointInsideWaterVolume(detectionPoint);
    }

    private bool IsHeadSubmerged(WaterPunchWaterSafety swimming)
    {
        Vector3 detectionPoint = swimming != null && swimming.HeadDrowningDetectionPoint != null
            ? swimming.HeadDrowningDetectionPoint.position
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
            swimming.SetInWater(lowerBodySubmerged);
            swimming.SetChestSubmerged(chestSubmerged);
        }

        Animator animator = characterController.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetBool("IsInWater", chestSubmerged);
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

        if (playerInWater != null)
        {
            SetPlayerWaterState(playerInWater, false, false, false);
        }

        playerInWater = null;
    }
}
