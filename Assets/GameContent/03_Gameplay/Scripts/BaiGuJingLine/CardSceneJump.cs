using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// 白骨精线：角色卡片点击进入洞窟场景（Start 的 Card_2）。
// 卡片处于拖拽轮播里，用“按下-抬起位移”判定真点击，拖动选卡松手不会误触发跳转。
public class CardSceneJump : MonoBehaviour, IPointerClickHandler
{
    [Header("要进入的场景名")]
    public string sceneName = "cave";

    [Tooltip("按下-抬起最大位移（像素），超过视为拖拽而不是点击")]
    public float maxClickDrift = 40f;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Vector2.Distance(eventData.position, eventData.pressPosition) > maxClickDrift) return;

        BaiGuJingLineState.Active = false; // 重新开线
        SceneManager.LoadScene(sceneName);
    }
}
