using UnityEngine;
using UnityEngine.UI;

// 白骨精线：UI按钮点击 → 跳转场景（cloud 的 Button (2) → village）。
[RequireComponent(typeof(Button))]
public class SceneJumpButton : MonoBehaviour
{
    [Header("要跳转的场景名")]
    public string sceneName;

    [Tooltip("跳转前清除线路标志")]
    public bool clearLineFlag;

    private void Start()
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(Go);
        else Debug.LogWarning($"{name} 缺少 Button 组件，无法绑定场景跳转");
    }

    public void Go()
    {
        if (clearLineFlag) BaiGuJingLineState.Active = false;
        if (!string.IsNullOrEmpty(sceneName))
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}
