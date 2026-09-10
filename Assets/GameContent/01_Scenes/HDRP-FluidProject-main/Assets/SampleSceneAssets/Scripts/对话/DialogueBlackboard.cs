using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class DialogueData
{
    public string speaker;
    [TextArea] public string content;
    public Sprite portrait;
    public int nextIndex = -1; // -1 表示无跳转，按顺序+1
    public List<DialogueChoice> choices = new List<DialogueChoice>();
    // 其他元数据
    [Header("对话触发事件 (广播)")]
    public bool broadcastOnShow = false;      // 显示这句话时是否广播
    public string broadcastEventKey = "";     // 要广播的事件名称（例如 "WaterAppear"）

    [Header("讲完后广播事件并暂停对话")]
    public bool broadcastOnFinish = false;    // 这句话“讲完”(文本效果播完)后广播事件并锁定对话按钮
    public string finishEventKey = "";        // 广播的事件 Key，监听方据此订阅 (DialogueLineFinished)
}
[System.Serializable]
public class DialogueChoice
{
    public string choiceText;   // 选项显示的文字
    public int targetIndex;     // 选择后跳转到的对话索引
    public bool broadcastEvent = false;
}
[System.Serializable]
public class SpeakerAvatar
{
    public string speakerName;   // 说话人名字，如 "NPC"
    public Sprite avatarSprite;  // 对应的头像图片
}


public class DialogueBlackboard : MonoBehaviour
{
    [SerializeField] public DialogueDataContainer currentDialogue;   // 直接在Inspector拖拽
    private int currentIndex = 0;

   
    public void SetDialogueData(DialogueDataContainer container)
    {
        currentDialogue = container;
        currentIndex = 0;
    }

    public int GetDialogueCount() => currentDialogue.dialoguePieces.Count;
    // 如果只需要顺序访问，直接用 List 即可
    public DialogueData GetDialogue(int index)
    {
        if (index >= 0 && index < currentDialogue.dialoguePieces.Count)
            return currentDialogue.dialoguePieces[index];
        Debug.LogWarning($"对话索引 {index} 超出范围 (0~{currentDialogue.dialoguePieces.Count - 1})");
        return null;
    }
    
    public bool HasDialogue(int index)
    {
        return index >= 0 && index < currentDialogue.dialoguePieces.Count;
    }

    public int Count => currentDialogue.dialoguePieces.Count;
}
