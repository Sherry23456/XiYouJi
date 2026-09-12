using UnityEngine;
using UnityEngine.UI;

// 白骨精线：UI按钮点击 → 用指定容器开始一段对话（cloud 的 Button (1) 触发“白骨精（第三幕）”）。
[RequireComponent(typeof(Button))]
public class DialogueOpenerButton : MonoBehaviour
{
    [Header("点击后播放的对话容器")]
    public DialogueDataContainer container;

    private void Start()
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(Open);
        else Debug.LogWarning($"{name} 缺少 Button 组件，无法绑定对话打开");
    }

    public void Open()
    {
        var controller = DialogueController.Instance;
        var ui = DialogueUIController.Instance;
        if (controller == null || ui == null)
        {
            Debug.LogError("对话系统未初始化：场景需要 DialogueController 和 DialogueUIController");
            return;
        }
        if (container == null)
        {
            Debug.LogWarning($"{name} 未指定对话容器");
            return;
        }
        if (ui.IsPanelOpen) return; // 对话进行中不重复触发

        controller.StartDialogue(container);
        ui.OpenPanel();
        ui.ShowCurrentDialogue();
    }
}
