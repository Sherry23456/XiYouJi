using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using XiYouJi.Events;

// 白骨精动画1 的对话中途触发器：
// 对话（白骨精（第一幕））进行到“夫人，请看。”时广播 SceneTriggerEvent；
// 本组件：暂停对话并隐藏面板 → 显示三人、启用专用摄像机推进 → 播 Timeline（雾飘过、三人消失）；
// 黑屏字幕由本脚本按真实时间开关（不依赖 Timeline），且整个流程有硬超时——
// 即使导演卡住/编辑器失焦暂停，黑屏也保证关闭并恢复对话。
public class BaiGuJingCutsceneTrigger : MonoBehaviour
{
    [Tooltip("要监听的广播Key，对应对话 asset 里“夫人，请看。”那句填的 broadcastEventKey")]
    public string eventKey = "白骨精动画1";

    [Header("动画元素")]
    public PlayableDirector director;

    [Tooltip("动画的三个角色（初始隐藏，触发时显示，播完隐藏）")]
    public GameObject[] characters;

    [Tooltip("专用摄像机（初始禁用）")]
    public GameObject cutsceneCamera;

    [Tooltip("黑屏字幕 Canvas（本脚本控制显示/关闭）")]
    public GameObject blackoutCanvas;

    [Header("时间（秒，自触发起算，用真实时间，不随失焦/暂停卡死）")]
    [Tooltip("黑屏出现时刻")]
    public float blackoutShowAt = 3.4f;
    [Tooltip("黑屏关闭时刻")]
    public float blackoutHideAt = 7.2f;
    [Tooltip("导演播完判定时刻")]
    public float cutsceneEndAt = 7.5f;
    [Tooltip("整体硬超时：超过该时长强制收尾恢复对话")]
    public float hardTimeout = 12f;

    [Header("镜头推进")]
    public float dollyDuration = 1.2f;
    public float retreat = 3.5f;
    public float rise = 1.1f;
    public Vector3 lookAtOffset = new Vector3(-2.24f, -0.6f, 2.55f);

    private bool triggered;

    private void OnEnable()
    {
        EventBus<SceneTriggerEvent>.Subscribe(OnBroadcast);
    }

    private void OnDisable()
    {
        EventBus<SceneTriggerEvent>.Unsubscribe(OnBroadcast);
    }

    private void OnBroadcast(SceneTriggerEvent e)
    {
        if (triggered) return;
        if (((e.EventKey) ?? "").Trim() != eventKey.Trim()) return;
        triggered = true;
        StartCoroutine(RunCutscene());
    }

    private IEnumerator RunCutscene()
    {
        var ui = DialogueUIController.Instance;
        var ctrl = DialogueController.Instance;

        if (ctrl != null) ctrl.BlockProceed();
        if (ui != null) ui.ClosePanel();
        Application.runInBackground = true;

        if (characters != null)
            foreach (var g in characters)
                if (g != null) g.SetActive(true);
        if (cutsceneCamera != null) cutsceneCamera.SetActive(true);
        StartCoroutine(Dolly());
        if (director != null)
        {
            director.time = 0;
            director.Play();
        }

        // 黑屏按真实时间开关；任何一步都有墙钟兜底
        float t0 = Time.realtimeSinceStartup;
        bool shown = false, hidden = false;
        while (Time.realtimeSinceStartup - t0 < hardTimeout)
        {
            float el = Time.realtimeSinceStartup - t0;
            if (!shown && el >= blackoutShowAt)
            {
                shown = true;
                if (blackoutCanvas != null) blackoutCanvas.SetActive(true);
            }
            if (shown && !hidden && el >= blackoutHideAt)
            {
                hidden = true;
                if (blackoutCanvas != null) blackoutCanvas.SetActive(false);
            }
            if (hidden)
            {
                // 黑屏已关，导演到尾/已停（或黑屏后再宽限 3 秒）就可以收尾
                bool directorDone = director == null
                    || (float)director.time >= cutsceneEndAt - 0.05f
                    || director.state != PlayState.Playing;
                if (directorDone || el >= blackoutHideAt + 3f) break;
            }
            yield return null;
        }

        // 强制收尾：隐藏一切，恢复对话系统
        if (characters != null)
            foreach (var g in characters)
                if (g != null) g.SetActive(false);
        if (cutsceneCamera != null) cutsceneCamera.SetActive(false);
        if (blackoutCanvas != null) blackoutCanvas.SetActive(false);
        if (director != null && director.state == PlayState.Playing) director.Stop();

        if (ui != null) ui.OpenPanel();
        if (ctrl != null) ctrl.UnblockProceed(true);
    }

    private IEnumerator Dolly()
    {
        if (cutsceneCamera == null) yield break;
        var camT = cutsceneCamera.transform;
        Vector3 endPos = camT.position;
        Vector3 lookAt = endPos + lookAtOffset;
        Vector3 fwd = (lookAt - endPos).normalized;
        Vector3 startPos = endPos - fwd * retreat + Vector3.up * rise;
        float t = 0f;
        while (t < dollyDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dollyDuration);
            u = u * u * (3f - 2f * u);
            camT.position = Vector3.Lerp(startPos, endPos, u);
            camT.LookAt(lookAt);
            yield return null;
        }
        camT.position = endPos;
        camT.LookAt(lookAt);
    }
}
