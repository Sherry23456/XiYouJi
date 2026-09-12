using System.Collections;
using UnityEngine;

// 白骨精线：等 watchObject 被 LineSceneState 激活、并自行播完消失（如 SelfDestroyer 到时销毁/失活）后，
// 再做一组显隐切换（village：介绍播完 → 隐藏场景四-孙悟空、显示最后一幕白骨精）。
public class TimedStateSwap : MonoBehaviour
{
    [Tooltip("要盯着的物体（等它激活后消失/失活）")]
    public GameObject watchObject;

    [Tooltip("等待激活的超时（秒），超时视为非本线流程，放弃")]
    public float activateTimeout = 15f;

    [Tooltip("激活后等待消失的超时（秒）")]
    public float finishTimeout = 60f;

    [Header("消失后执行")]
    public GameObject[] hideAfter;
    public GameObject[] showAfter;

    private void Start()
    {
        if (!BaiGuJingLineState.Active) return;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (watchObject == null) yield break;

        float t = 0f;
        while ((watchObject != null) && !watchObject.activeSelf && t < activateTimeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
        // 未被激活或已被销毁：不是本线流程，放弃
        if (watchObject == null || !watchObject.activeSelf) yield break;

        // 等它自己消失（SelfDestroyer 销毁后 == null 为真）
        t = 0f;
        while (watchObject != null && watchObject.activeSelf && t < finishTimeout)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (hideAfter != null)
            foreach (var g in hideAfter)
                if (g != null) g.SetActive(false);

        if (showAfter != null)
            foreach (var g in showAfter)
                if (g != null) g.SetActive(true);
    }
}
