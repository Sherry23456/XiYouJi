using UnityEngine;

// 白骨精线：跳转进场时的一次性显隐切换（如 wild 换主角、cloud 换按钮）。
// 只在 BaiGuJingLineState.Active 为真时执行，其他线路进场景不受影响。
public class LineSceneState : MonoBehaviour
{
    [Header("进场时隐藏")]
    public GameObject[] hideOnEnter;

    [Header("进场时显示")]
    public GameObject[] showOnEnter;

    private void Start()
    {
        if (!BaiGuJingLineState.Active) return;

        if (hideOnEnter != null)
            foreach (var g in hideOnEnter)
                if (g != null) g.SetActive(false);

        if (showOnEnter != null)
            foreach (var g in showOnEnter)
                if (g != null) g.SetActive(true);
    }
}
