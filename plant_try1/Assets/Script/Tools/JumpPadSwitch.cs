using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[DisallowMultipleComponent]

public class JumpPadSwitch : MonoBehaviour
{
    [Header("控制对象")]

    [SerializeField] private JumpPad targetPad;

    [Header("交互")]

    [Tooltip("拖入这个开关上的 ProximityPrompt")]
    [SerializeField] private ProximityPrompt proximity;

    [SerializeField] private Key interactKey = Key.E;

    [Header("可选：提示文字")]

    [SerializeField] private TMP_Text promptText;

    [Header("材质")]

    [SerializeField] private Material poweredMaterial;
    [SerializeField] private Material unpoweredMaterial;

    private void Update()
    {
        if (targetPad == null)
            return;

        //文字始终反映起跳板的实际状态
        if(promptText != null)
        {
            string message = targetPad.IsPowered
                ? $"Press{interactKey}to close the Jumping pad"
                : $"Press {interactKey} to Start the Jumping pad";

            if(promptText.text != message)
            {
                promptText.text = message;

            }

            if(Time.timeScale <= 0f || proximity == null || !proximity.CanInteract || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current[interactKey].wasPressedThisFrame)
            {
                targetPad.TogglePower();
            }
        }
    }
    
}
