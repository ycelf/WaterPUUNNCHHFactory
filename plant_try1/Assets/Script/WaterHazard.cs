using System.Collections;
using UnityEngine;

public class WaterHazard : MonoBehaviour
{
    [Header("Water Room")]
    [SerializeField] private WaterRoomController roomController;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Death Countdown")]
    [Min(0f)]
    [SerializeField] private float deathDelay = 2f;

    private CharacterController playerInWater;
    private Coroutine deathCountdownRoutine;

    private void Reset()
    {
        roomController = GetComponentInParent<WaterRoomController>();
    }

    private void Update()
    {
        if (playerInWater == null)
        {
            return;
        }

        if (IsPlayerProtected())
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

        playerInWater = characterController;
        if (!IsPlayerProtected() && deathCountdownRoutine == null)
        {
            deathCountdownRoutine = StartCoroutine(DeathCountdown(characterController));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CharacterController characterController = other.GetComponentInParent<CharacterController>();
        if (characterController == null || characterController != playerInWater)
        {
            return;
        }

        StopDeathCountdown();
        playerInWater = null;
    }

    private bool IsPlayerProtected()
    {
        if (playerInWater == null)
        {
            return false;
        }

        WaterPunchWaterSafety punchSafety = playerInWater.GetComponent<WaterPunchWaterSafety>();
        return punchSafety != null && punchSafety.IsPositionInSafeZone(playerInWater.transform.position);
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
        playerInWater = null;

        if (roomController != null)
        {
            roomController.RespawnPlayer(characterController.transform);
        }
    }

    private void OnDisable()
    {
        StopDeathCountdown();
        playerInWater = null;
    }
}
