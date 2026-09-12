using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

// 白骨精线通用流程编排：某一幕对话结束（OnDialogueEnd 且 currentContainer 名字匹配）后：
// 显/隐一组物体 → 按模式等“播完”（Timeline 播完 / 激活物体自毁 / 立即）→ 再显/隐一组物体 → 跳转下一场景。
// 容器名不匹配的对话结束事件一律忽略，不会误伤同场景其他线路的对话。
// cave（第一幕→wild）、wild（第二幕→动画→cloud）、cloud（第三幕→白骨精二次→换按钮）、village（第六幕→Start）共用本组件。
public class DialogueEndFlow : MonoBehaviour
{
    public enum FinishMode
    {
        Immediately,          // 对话结束立即继续
        WaitShownDestroyed,   // 等 showOnDialogueEnd 里的物体自己销毁/失活（如 SelfDestroyer）
        WaitDirector,         // 等 Timeline 播完（PlayableDirector 停止播放）
    }

    [Header("监听的对话容器名（= 结束时 DialogueController.currentContainer.name）")]
    public string targetContainerName;

    [Header("对话结束立即执行")]
    public GameObject[] showOnDialogueEnd;
    public GameObject[] hideOnDialogueEnd;

    [Header("完成判定方式")]
    public FinishMode finishMode = FinishMode.Immediately;

    [Tooltip("WaitDirector 模式：要等的 PlayableDirector（留空则自动在 showOnDialogueEnd 子级里找）")]
    public PlayableDirector waitDirector;

    [Tooltip("等待播完的超时（秒），超时强制继续")]
    public float finishTimeout = 120f;

    [Header("完成后执行")]
    public GameObject[] showAfterFinish;
    public GameObject[] hideAfterFinish;

    [Header("完成后跳转场景（留空 = 不跳转）")]
    public string nextScene;

    [Tooltip("跳转前置位线路标志（cave 的第一幕流程用）")]
    public bool setLineFlagBeforeJump;

    [Tooltip("跳转前清除线路标志（village 的第六幕流程用）")]
    public bool clearLineFlagBeforeJump;

    private bool running;

    private void OnEnable()
    {
        DialogueUIController.OnDialogueEnd += OnDialogueEnd;
    }

    private void OnDisable()
    {
        DialogueUIController.OnDialogueEnd -= OnDialogueEnd;
    }

    private void OnDialogueEnd()
    {
        var controller = DialogueController.Instance;
        if (controller == null || controller.currentContainer == null) return;
        if (controller.currentContainer.name != targetContainerName) return;
        if (running) return;
        running = true;

        Apply(hideOnDialogueEnd, false);
        Apply(showOnDialogueEnd, true);

        if (finishMode == FinishMode.Immediately)
        {
            Finish();
            return;
        }
        StartCoroutine(WaitFinish());
    }

    private IEnumerator WaitFinish()
    {
        float t = 0f;
        if (finishMode == FinishMode.WaitDirector)
        {
            var director = waitDirector;
            if (director == null && showOnDialogueEnd != null)
            {
                foreach (var g in showOnDialogueEnd)
                {
                    if (g == null) continue;
                    director = g.GetComponentInChildren<PlayableDirector>(true);
                    if (director != null) break;
                }
            }
            if (director != null)
            {
                // playOnAwake 已经在播就不打断，否则从头播放
                if (director.state != PlayState.Playing)
                {
                    director.time = 0;
                    director.Play();
                }
                while (director != null && director.state == PlayState.Playing && t < finishTimeout)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
            }
        }
        else // WaitShownDestroyed
        {
            while (t < finishTimeout && AnyShownAlive())
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
        Finish();
    }

    private bool AnyShownAlive()
    {
        if (showOnDialogueEnd == null) return false;
        foreach (var g in showOnDialogueEnd)
            if (g != null && g.activeSelf) return true; // g != null 兼容被 Destroy 后的假空
        return false;
    }

    private void Finish()
    {
        Apply(hideAfterFinish, false);
        Apply(showAfterFinish, true);
        running = false;

        if (!string.IsNullOrEmpty(nextScene))
        {
            if (setLineFlagBeforeJump) BaiGuJingLineState.Active = true;
            if (clearLineFlagBeforeJump) BaiGuJingLineState.Active = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
        }
    }

    private static void Apply(GameObject[] list, bool state)
    {
        if (list == null) return;
        foreach (var g in list)
            if (g != null) g.SetActive(state);
    }
}
