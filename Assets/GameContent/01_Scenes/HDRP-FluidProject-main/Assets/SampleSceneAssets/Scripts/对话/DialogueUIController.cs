using EasyTextEffects;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XiYouJi.Events;
using static System.Net.Mime.MediaTypeNames;


public class DialogueUIController : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private Transform contentParent;      // ScrollView 的 Content
    [SerializeField] private GameObject entryTemplate;     // 对话条目模板
    [SerializeField] private Button nextButton;            // “继续”按钮
    [SerializeField] private Button choiceButtonTemplate;  // 选项按钮模板
    [SerializeField] private GameObject dialoguePanelRoot; // 对话面板根节点
    [SerializeField] private UnityEngine.UI.Image avatarImage;            // 说话人头像
    [SerializeField] private ScrollRect scrollRect;          // 滚动视图（用于自动滚到最新一条）


    private float lastNextClickTime = -1f;
    private const float NEXT_CLICK_COOLDOWN = 0.8f;
    // 这个变量保留，用途不明，不动它
    public GameObject gg;

    // 存储历史对话条目（用于清空）
    private List<GameObject> historyEntries = new List<GameObject>();
    // 存储当前显示的选项按钮（用于清除）
    private List<Button> currentChoiceButtons = new List<Button>();

    // “讲完后广播”相关：等文本效果播完 → 广播 DialogueLineFinished → 锁定按钮，等监听方回调
    private Coroutine finishBroadcastRoutine;
    private bool interactionLockedByEvent = false;
    private const float FINISH_WAIT_TIMEOUT = 20f;   // 循环类文本效果永远不“完成”，用超时兜底

    public static DialogueUIController Instance { get; private set; }

    public static event System.Action OnDialogueEnd;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextButtonClicked);
        PrewarmTextEffect();
        ClosePanel();
    }

    public void ClosePanel()
    {
        if (dialoguePanelRoot != null)
            dialoguePanelRoot.SetActive(false);
    }

    public void OpenPanel()
    {
        if (dialoguePanelRoot != null)
        {
            dialoguePanelRoot.SetActive(true);
            if (dialoguePanelRoot.transform.childCount > 0)
                dialoguePanelRoot.transform.GetChild(0).gameObject.SetActive(true);
        }

    }

    // 对话面板是否打开（供外部判断“对话进行中”，避免重复触发）
    public bool IsPanelOpen
    {
        get { return dialoguePanelRoot != null && dialoguePanelRoot.activeSelf; }
    }

    // 显示当前对话（追加到信息流）
    public void ShowCurrentDialogue()
    {
        var controller = DialogueController.Instance;
        if (controller == null) return;

        interactionLockedByEvent = false;   // 重新显示对话时清除上一次的锁定状态

        DialogueData data = controller.GetCurrentDialogue();
        if (data == null)
        {
            EndDialogue();
            return;
        }

        if (avatarImage != null)
        {
            avatarImage.sprite = data.portrait;
            avatarImage.gameObject.SetActive(data.portrait != null);
        }
        Debug.Log($"[对话调试] 当前索引: {controller.CurrentIndex} ");
        // 添加对话条目
        BroadcastIfNeeded(data);

        TextEffect entryEffect = AddDialogueEntry(data.speaker, data.content, data.portrait);

        // 新条目可能比视口高，滚到最新一条
        StartCoroutine(ScrollToBottom());

        // 清除旧的选项按钮
        ClearChoiceButtons();

        // 判断是否有选项
        if (controller.HasChoices)
        {
            // 有选项：隐藏“继续”按钮，创建选项按钮
            nextButton.interactable = false;


            var choices = controller.GetCurrentChoices();
            foreach (var choice in choices)
            {
                Button btn = Instantiate(choiceButtonTemplate, contentParent);
                btn.gameObject.SetActive(true);
                btn.GetComponentInChildren<TMP_Text>().text = choice.choiceText;

                int target = choice.targetIndex;
                int current = controller.CurrentIndex;
                string text = choice.choiceText;

                DialogueData container = controller.GetCurrentDialogue();
                btn.onClick.AddListener(() =>
                {
                    ClearChoiceButtons(); // 立即移除选项按钮
                    if (target == -1)
                    {
                        if (choice.broadcastEvent)  // 只有勾选了才广播
                        {
                            EventBus<DialogueChoiceSelected>.Publish(
                                new DialogueChoiceSelected(text, target, current, container)
                            );
                        }
                        // 选项标记为结束对话
                        EndDialogue();
                        Debug.Log("选项结束对话");
                    }
                    else
                    {
                        // 跳转到目标索引
                        if (choice.broadcastEvent)  // 只有勾选了才广播
                        {
                            EventBus<DialogueChoiceSelected>.Publish(
                                new DialogueChoiceSelected(text, target, current, container)
                            );
                        }
                        controller.JumpToDialogue(target);
                        ShowCurrentDialogue(); // 刷新显示
                    }
                });
                currentChoiceButtons.Add(btn);
            }

            StartCoroutine(ScrollToBottom());
        }
        else
        {
            // 无选项：显示“继续”按钮
            nextButton.gameObject.SetActive(true);
            if (controller.IsEnd())
                nextButton.interactable = false;
            else
                nextButton.interactable = true;
        }

        // 如果已经是最后一句且无选项，禁用“继续”并自动结束？
        if (controller.IsEnd() && !controller.HasChoices)
        {
            nextButton.interactable = false;
            // 如果你想自动结束，可以调用 EndDialogue();
        }

        // 这句配置了 broadcastOnFinish：讲完后广播事件并锁定按钮，等待监听方回调
        QueueFinishBroadcast(data, entryEffect);
    }
    private void PrewarmTextEffect()
    {
        if (entryTemplate == null) return;
        GameObject temp = Instantiate(entryTemplate, contentParent);
        temp.SetActive(false);
        TMP_Text tempText = temp.transform.Find("ContentText")?.GetComponent<TMP_Text>();
        if (tempText != null)
        {
            TextEffect effect = tempText.GetComponent<TextEffect>();
            if (effect != null && effect.isActiveAndEnabled)
            {
                effect.StartManualEffects();
            }
        }
        // 不销毁，保留作为隐藏模板，但这样会产生多余对象，可以销毁
        Destroy(temp);
    }
    // 添加一条对话条目

    private void BroadcastIfNeeded(DialogueData data)
    {
        // 1. 检查是否勾选了广播，并且事件名不为空
        if (data == null || !data.broadcastOnShow || string.IsNullOrEmpty(data.broadcastEventKey))
            return;

        // 2. 发布事件（这里借用现有的 EventBus，但定义一个泛型，或者直接用字符串作为事件）
        // 注意：EventBus 是泛型的，你的 EventBus<T> 需要 T 是结构体。
        // 最简单的办法：定义一个通用的 GameEvent 结构体，只包含字符串 Key。
        // 或者，为了不改动 EventBus，我们发布一个具体的场景事件。

        // 这里演示一种通用做法：发布一个包含 Key 的事件
        // 假设你定义了 public struct SceneTriggerEvent { public string EventKey; }
        EventBus<SceneTriggerEvent>.Publish(new SceneTriggerEvent { EventKey = data.broadcastEventKey });

        Debug.Log($"[对话系统] 广播场景事件：{data.broadcastEventKey}");
    }
    // 返回该条目的 TextEffect（没有则返回 null），供“讲完后广播”等待效果播完
    private TextEffect AddDialogueEntry(string speaker, string content, Sprite portrait)
    {
        GameObject entry = Instantiate(entryTemplate, contentParent);
        historyEntries.Add(entry);

        Transform nameTrans = entry.transform.Find("NameText");
        Transform contentTrans = entry.transform.Find("ContentText");
        
        if (nameTrans == null || contentTrans == null) return null;

        

        TMP_Text nameText = nameTrans.GetComponent<TMP_Text>();
        TMP_Text contentText = contentTrans.GetComponent<TMP_Text>();
        TextEffect effect = contentText.GetComponent<TextEffect>();

        // 说话人为空时隐藏名字行，气泡只显示正文
        bool showName = !string.IsNullOrEmpty(speaker);
        nameText.text = showName ? $"{speaker}:" : "";
        if (nameText.gameObject.activeSelf != showName)
            nameText.gameObject.SetActive(showName);

        contentText.text = "" + content;



        Canvas.ForceUpdateCanvases();
        contentText.ForceMeshUpdate(true, true);
        if (effect != null) effect.Refresh();

        // 按文字内容重新计算气泡尺寸
        var sizer = entry.GetComponent<ChatBubbleAutoSize>();
        if (sizer != null) sizer.Refresh();

        StartCoroutine(StartEffectNextFrame(effect));
        return effect;
    }

    private IEnumerator StartEffectNextFrame(TextEffect effect)
    {
        yield return null;
        if (effect != null && effect.isActiveAndEnabled)
            effect.StartManualEffects();
    }

    // 清除选项按钮
    private void ClearChoiceButtons()
    {
        foreach (var btn in currentChoiceButtons)
        {
            if (btn != null) Destroy(btn.gameObject);
        }
        currentChoiceButtons.Clear();
    }

    // 清空所有历史对话条目
    public void ClearHistory()
    {
        foreach (var entry in historyEntries)
        {
            if (entry != null) Destroy(entry);
        }
        historyEntries.Clear();
    }

    // 结束对话（清空历史、选项，重置索引，关闭面板）
    public void EndDialogue()
    {
        // 取消尚未触发的“讲完后广播”，并解除锁定
        if (finishBroadcastRoutine != null)
        {
            StopCoroutine(finishBroadcastRoutine);
            finishBroadcastRoutine = null;
        }
        interactionLockedByEvent = false;

        ClearHistory();
        ClearChoiceButtons();

        var controller = DialogueController.Instance;
        if (controller != null)
            controller.ResetDialogue();

        if (nextButton != null)
            nextButton.gameObject.SetActive(false);

        // 关闭面板（根据需求，也可以保留打开但隐藏按钮）
        ClosePanel();

        // 如果 gg 有特殊用途，可以处理，这里保持原样
        if (gg != null)
            gg.SetActive(false);

        OnDialogueEnd?.Invoke();

    }

    // 点击“继续”按钮

    public void BlockProceed()
    {
        if (nextButton != null)
            nextButton.interactable = false;
    }

    public void UnblockProceed()
    {
        if (nextButton != null)
            nextButton.interactable = true;
    }

    // ---------- 讲完后广播并暂停 ----------

    // 这句配置了 broadcastOnFinish 时调用：等文本效果播完 → 广播 DialogueLineFinished → 锁定按钮
    private void QueueFinishBroadcast(DialogueData data, TextEffect effect)
    {
        if (finishBroadcastRoutine != null)
        {
            StopCoroutine(finishBroadcastRoutine);
            finishBroadcastRoutine = null;
        }
        if (data == null || !data.broadcastOnFinish || string.IsNullOrEmpty(data.finishEventKey))
            return;

        finishBroadcastRoutine = StartCoroutine(BroadcastAfterLineFinished(data, effect));
    }

    private IEnumerator BroadcastAfterLineFinished(DialogueData data, TextEffect effect)
    {
        var controller = DialogueController.Instance;
        int lineIndex = controller != null ? controller.CurrentIndex : -1;

        // 等 AddDialogueEntry 里下一帧的 StartManualEffects 生效
        yield return null;
        yield return null;

        // 等所有已开始的手动效果播完（打字机等）；循环类效果不会自己结束，靠超时兜底
        float waited = 0f;
        while (waited < FINISH_WAIT_TIMEOUT && effect != null && HasRunningManualEffect(effect))
        {
            yield return null;
            waited += Time.unscaledDeltaTime;
        }
        if (waited >= FINISH_WAIT_TIMEOUT)
            Debug.LogWarning($"[对话系统] 等待文本效果完成超时({FINISH_WAIT_TIMEOUT}s)，强制广播：{data.finishEventKey}");

        // 等待期间已翻页或结束对话：放弃本次广播
        if (DialogueController.Instance == null
            || DialogueController.Instance.CurrentIndex != lineIndex
            || dialoguePanelRoot == null || !dialoguePanelRoot.activeSelf)
        {
            finishBroadcastRoutine = null;
            yield break;
        }

        Debug.Log($"[对话系统] 第 {lineIndex} 句讲完，广播事件：{data.finishEventKey}，锁定按钮等待回调");
        EventBus<DialogueLineFinished>.Publish(new DialogueLineFinished
        {
            EventKey = data.finishEventKey,
            LineIndex = lineIndex,
            Speaker = data.speaker,
            Content = data.content,
            Container = data
        });

        LockInteraction();
        finishBroadcastRoutine = null;
    }

    private static bool HasRunningManualEffect(TextEffect effect)
    {
        foreach (var status in effect.QueryEffectStatuses(TextEffectType.Global, TextEffectEntry.TriggerWhen.Manual))
            if (status.Started && !status.IsComplete) return true;
        foreach (var status in effect.QueryEffectStatuses(TextEffectType.Tag, TextEffectEntry.TriggerWhen.Manual))
            if (status.Started && !status.IsComplete) return true;
        return false;
    }

    // 锁定对话交互：继续按钮 + 选项按钮都不可点，Controller 层也同步阻塞
    public void LockInteraction()
    {
        interactionLockedByEvent = true;
        if (nextButton != null)
            nextButton.interactable = false;
        foreach (var btn in currentChoiceButtons)
            if (btn != null) btn.interactable = false;
        if (DialogueController.Instance != null)
            DialogueController.Instance.BlockProceed();
    }

    // 监听方任务完成后的回调入口：收到回调后解锁，对话继续
    // autoContinue=true 时，无选项的句子自动推进到下一句（有选项的句子仍等待玩家选择）
    public void ResumeDialogue(bool autoContinue = false)
    {
        interactionLockedByEvent = false;
        if (DialogueController.Instance != null)
            DialogueController.Instance.UnblockProceed(false);
        RestoreButtons();
        Debug.Log("[对话系统] 收到任务完成回调，对话恢复");

        if (autoContinue && DialogueController.Instance != null && !DialogueController.Instance.HasChoices)
            OnNextButtonClicked();
    }

    // 按当前对话状态恢复按钮的可交互性
    private void RestoreButtons()
    {
        var controller = DialogueController.Instance;
        if (controller == null) return;

        if (controller.HasChoices)
        {
            foreach (var btn in currentChoiceButtons)
                if (btn != null) btn.interactable = true;
        }
        else if (nextButton != null && nextButton.gameObject.activeSelf)
        {
            nextButton.interactable = !controller.IsEnd();
        }
    }

    public void OnNextButtonClicked()
    {
        if (interactionLockedByEvent) return;   // 讲完后广播暂停期间禁止推进
        if (Time.unscaledTime - lastNextClickTime < NEXT_CLICK_COOLDOWN)
            return;
        lastNextClickTime = Time.unscaledTime;
        var controller = DialogueController.Instance;
        if (controller == null || controller.IsBlocked) return;
     
        if (controller.IsEnd()) return;

        controller.NextDialogue();
        ShowCurrentDialogue();
    }

    // 滚动到底部（协程）
    private IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        var target = scrollRect != null ? scrollRect : GetComponentInParent<ScrollRect>();
        if (target != null)
            target.normalizedPosition = new Vector2(0, 0);
    }

    private void OnDestroy()
    {
        if (nextButton != null)
            nextButton.onClick.RemoveListener(OnNextButtonClicked);
    }
}