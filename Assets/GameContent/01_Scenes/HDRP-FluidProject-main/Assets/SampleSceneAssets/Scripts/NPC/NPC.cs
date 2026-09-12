using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class NPCInteract : MonoBehaviour
{
    [SerializeField] private DialogueDataContainer dialogueContainer; // 在 Inspector 中拖拽对话资产

    [Header("点击检测")]
    [Tooltip("点击射线的最大距离")]
    [Min(1f)]
    [SerializeField] private float maxClickDistance = 2000f;

    private Camera clickCamera;

    private void Start()
    {
        // 如果未手动拖拽，尝试从同物体获取（但 DialogueDataContainer 是 ScriptableObject，不是组件，所以大概率失败）
        // 这里保留兼容性，但强烈建议在 Inspector 中显式赋值
        if (dialogueContainer == null)
        {
            // 尝试从 Resources 加载？不推荐，最好强制要求拖拽
            Debug.LogWarning($"{gameObject.name} 缺少对话数据容器，请在 Inspector 中赋值。");
        }
    }
    public void SetDialogueContainer(DialogueDataContainer newContainer)
    {
        dialogueContainer = newContainer;
        Debug.Log($"{gameObject.name} 的对话已更新为: {newContainer.name}");
    }

    // 用 RaycastAll 代替 OnMouseDown：相机与 NPC 之间有岩石等遮挡物时（OnMouseDown 只认第一个
    // 命中，会被遮挡物截走），穿透命中列表找到第一个 NPC，是自己才触发对话
    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;
        // 点在对话面板等 UI 上时不当作场景点击
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        TryInteractAtScreen(Input.mousePosition);
    }

    // 从指定屏幕位置检测：命中列表里第一个 NPC 是自己 → 开始对话；是别的 NPC → 交给它处理
    public bool TryInteractAtScreen(Vector2 screenPosition)
    {
        if (clickCamera == null)
            clickCamera = Camera.main;
        if (clickCamera == null)
            return false;

        Ray ray = clickCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxClickDistance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            var npc = hit.collider.GetComponentInParent<NPCInteract>();
            if (npc == null)
                continue; // 岩石、墙体等无脚本物体，穿透
            if (npc != this)
                return false; // 点到更近的其他 NPC，由它自己的 Update 处理
            return TryStartDialogue();
        }
        return false;
    }

    private bool TryStartDialogue()
    {
        // 1. 检查数据是否有效
        if (dialogueContainer == null)
        {
            Debug.LogWarning("NPC 没有对话数据容器，无法开始对话");
            return false;
        }

        // 2. 获取全局管理器单例
        var controller = DialogueController.Instance;
        var ui = DialogueUIController.Instance;

        if (controller == null || ui == null)
        {
            Debug.LogError("对话系统未初始化，请确保场景中存在 DialogueController 和 DialogueUIController");
            return false;
        }

        // 3. 启动对话：传入数据容器，控制器会自动重置索引
        controller.StartDialogue(dialogueContainer);

        // 4. 打开 UI 面板并显示第一条对话
        ui.OpenPanel();
        ui.ShowCurrentDialogue(); // 这个方法内部会调用 GetCurrentDialogue 并渲染
        return true;
    }
}
