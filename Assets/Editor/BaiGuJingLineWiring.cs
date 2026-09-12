using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 白骨精线一次性接线工具（编辑器专用）：
// 在 execute_code 里调用 BaiGuJingLineWiring.Wire("start"/"cave"/"wild"/"cloud"/"village") 即可。
// 每个场景独立以 Single 模式打开 → 挂组件/赋引用 → 保存，互不干扰；全部完成后用 Wire("reopen-cave") 回到 cave。
public static class BaiGuJingLineWiring
{
    private const string DialogueDir = "Assets/GameContent/01_Scenes/HDRP-FluidProject-main/Assets/SampleSceneAssets/Scripts/对话/文本";
    private const string FlowObjectName = "白骨精线-流程";

    public static string Wire(string scene)
    {
        switch (scene)
        {
            case "start": return WireStart();
            case "cave": return WireCave();
            case "wild": return WireWild();
            case "cloud": return WireCloud();
            case "village": return WireVillage();
            case "tool": return WireTool();
            case "reopen-cave":
            {
                var s = OpenSingle("cave");
                return "reopened: " + s.path;
            }
            default: return "unknown scene: " + scene;
        }
    }

    // 菜单入口（execute_code 通过 ExecuteMenuItem 调用，报告输出到 Console）
    [MenuItem("Tools/白骨精线/Wire Start")] public static void MenuStart() { Debug.Log("[白骨精线接线] " + Wire("start")); }
    [MenuItem("Tools/白骨精线/Wire Cave")] public static void MenuCave() { Debug.Log("[白骨精线接线] " + Wire("cave")); }
    [MenuItem("Tools/白骨精线/Wire Wild")] public static void MenuWild() { Debug.Log("[白骨精线接线] " + Wire("wild")); }
    [MenuItem("Tools/白骨精线/Wire Cloud")] public static void MenuCloud() { Debug.Log("[白骨精线接线] " + Wire("cloud")); }
    [MenuItem("Tools/白骨精线/Wire Village")] public static void MenuVillage() { Debug.Log("[白骨精线接线] " + Wire("village")); }
    [MenuItem("Tools/白骨精线/Wire Tool")] public static void MenuTool() { Debug.Log("[白骨精线接线] " + Wire("tool")); }
    [MenuItem("Tools/白骨精线/Reopen Cave")] public static void MenuReopen() { Debug.Log("[白骨精线接线] " + Wire("reopen-cave")); }

    // ---------- Start：Card_2 → cave ----------
    private static string WireStart()
    {
        var s = OpenSingle("Start");
        var sb = new StringBuilder();
        var card2 = FindByPath(s, "MainCanvas/RootPanel/ExperienceSelectPanel/ScrollContent/Card_2");
        if (card2 == null) return "FAIL: Card_2 not found";
        var jump = AddOrGet<CardSceneJump>(card2);
        jump.sceneName = "cave";
        EditorUtility.SetDirty(jump);
        sb.Append("Card_2 + CardSceneJump(cave) ");
        return sb + " | " + Save(s);
    }

    // ---------- cave：Canvas 挂到 dialoguUI 下持久化 + 第一幕结束跳 wild ----------
    private static string WireCave()
    {
        var s = OpenSingle("cave");
        var sb = new StringBuilder();

        GameObject ui = null, canvas = null;
        foreach (var r in s.GetRootGameObjects())
        {
            if (r.GetComponent<DialogueUIController>() != null) ui = r;
            if (r.name == "对话系统") canvas = r;
        }
        if (ui == null) return "FAIL: dialoguUI not found";
        if (canvas != null && canvas.transform.parent != ui.transform)
        {
            canvas.transform.SetParent(ui.transform, false);
            sb.Append("canvas reparented under dialoguUI; ");
        }
        else
        {
            sb.Append(canvas == null ? "canvas MISSING!; " : "canvas already under dialoguUI; ");
        }

        var flow = EnsureFlowObject(s);
        var f = AddOrGet<DialogueEndFlow>(flow);
        f.targetContainerName = "白骨精（第一幕）";
        f.finishMode = DialogueEndFlow.FinishMode.Immediately;
        f.nextScene = "wild";
        f.setLineFlagBeforeJump = true;
        f.showOnDialogueEnd = null;
        f.hideOnDialogueEnd = null;
        f.showAfterFinish = null;
        f.hideAfterFinish = null;
        EditorUtility.SetDirty(f);
        sb.Append("flow(第一幕→wild) ");
        return sb + " | " + Save(s);
    }

    // ---------- wild：换主角换场景组 + 第二幕结束播动画→cloud ----------
    private static string WireWild()
    {
        var s = OpenSingle("wild");
        var sb = new StringBuilder();
        var flow = EnsureFlowObject(s);

        var state = AddOrGet<LineSceneState>(flow);
        state.hideOnEnter = new[]
        {
            Must(FindByPath(s, "Player_ClickMove"), "Player_ClickMove", sb),
            Must(FindByPath(s, "场景一孙悟空"), "场景一孙悟空", sb),
        };
        state.showOnEnter = new[]
        {
            Must(FindByPath(s, "BaiGuJing_ClickMove"), "BaiGuJing_ClickMove", sb),
            Must(FindByPath(s, "场景二白骨精"), "场景二白骨精", sb),
        };
        EditorUtility.SetDirty(state);

        var animRoot = Must(FindByPath(s, "--------第二幕动画------------"), "第二幕动画", sb);
        var director = animRoot != null ? animRoot.GetComponentInChildren<PlayableDirector>(true) : null;
        if (director == null) sb.Append("WARN: 第二幕TimeLine PlayableDirector not found; ");

        var f = AddOrGet<DialogueEndFlow>(flow);
        f.targetContainerName = "白骨精（第二幕）";
        f.showOnDialogueEnd = new[] { animRoot };
        f.hideOnDialogueEnd = null;
        f.finishMode = DialogueEndFlow.FinishMode.WaitDirector;
        f.waitDirector = director;
        f.nextScene = "cloud";
        f.showAfterFinish = null;
        f.hideAfterFinish = null;
        f.setLineFlagBeforeJump = false;
        f.clearLineFlagBeforeJump = false;
        EditorUtility.SetDirty(f);
        sb.Append("flow(第二幕→动画→cloud) ");
        return sb + " | " + Save(s);
    }

    // ---------- cloud：换主角换按钮 + 第三幕结束显示白骨精二次→换按钮 ----------
    private static string WireCloud()
    {
        var s = OpenSingle("cloud");
        var sb = new StringBuilder();
        var flow = EnsureFlowObject(s);

        var state = AddOrGet<LineSceneState>(flow);
        state.hideOnEnter = new[]
        {
            Must(FindByPath(s, "player"), "player", sb),
            Must(FindByPath(s, "BaiGuJing/Canvas 1/Button"), "Button", sb),
        };
        state.showOnEnter = new[]
        {
            Must(FindByPath(s, "BaiGuJing_ClickMove"), "BaiGuJing_ClickMove", sb),
            Must(FindByPath(s, "BaiGuJing/Canvas 1/Button (1)"), "Button (1)", sb),
        };
        EditorUtility.SetDirty(state);

        var f = AddOrGet<DialogueEndFlow>(flow);
        f.targetContainerName = "白骨精（第三幕）";
        f.showOnDialogueEnd = new[] { Must(FindByPath(s, "白骨精二次"), "白骨精二次", sb) };
        f.hideOnDialogueEnd = null;
        f.finishMode = DialogueEndFlow.FinishMode.WaitShownDestroyed;
        f.hideAfterFinish = new[] { FindByPath(s, "BaiGuJing/Canvas 1/Button (1)") };
        f.showAfterFinish = new[] { FindByPath(s, "BaiGuJing/Canvas 1/Button (2)") };
        f.nextScene = "";
        EditorUtility.SetDirty(f);

        var b1 = Must(FindByPath(s, "BaiGuJing/Canvas 1/Button (1)"), "Button (1)", sb);
        AddOrGet<Button>(b1);
        var opener = AddOrGet<DialogueOpenerButton>(b1);
        opener.container = Container("白骨精（第三幕）");
        EditorUtility.SetDirty(opener);

        var b2 = Must(FindByPath(s, "BaiGuJing/Canvas 1/Button (2)"), "Button (2)", sb);
        AddOrGet<Button>(b2);
        var jump = AddOrGet<SceneJumpButton>(b2);
        jump.sceneName = "village";
        jump.clearLineFlag = false;
        EditorUtility.SetDirty(jump);

        sb.Append("flow(第三幕→白骨精二次→换按钮) b1+opener b2+jump ");
        return sb + " | " + Save(s);
    }

    // ---------- village：介绍切换 + 限时后换场景组 + 第六幕结束回 Start ----------
    private static string WireVillage()
    {
        var s = OpenSingle("village");
        var sb = new StringBuilder();
        var flow = EnsureFlowObject(s);

        var state = AddOrGet<LineSceneState>(flow);
        state.hideOnEnter = new[] { Must(FindByPath(s, "场景初介绍"), "场景初介绍", sb) };
        state.showOnEnter = new[] { Must(FindByPath(s, "场景初介绍 (1)"), "场景初介绍 (1)", sb) };
        EditorUtility.SetDirty(state);

        var swap = AddOrGet<TimedStateSwap>(flow);
        swap.watchObject = FindByPath(s, "场景初介绍 (1)");
        swap.hideAfter = new[] { Must(FindByPath(s, "场景四-孙悟空"), "场景四-孙悟空", sb) };
        swap.showAfter = new[] { Must(FindByPath(s, "最后一幕白骨精"), "最后一幕白骨精", sb) };
        EditorUtility.SetDirty(swap);

        var f = AddOrGet<DialogueEndFlow>(flow);
        f.targetContainerName = "白骨精（第六幕）";
        f.showOnDialogueEnd = null;
        f.hideOnDialogueEnd = null;
        f.finishMode = DialogueEndFlow.FinishMode.Immediately;
        f.nextScene = "Start";
        f.setLineFlagBeforeJump = false;
        f.clearLineFlagBeforeJump = true;
        EditorUtility.SetDirty(f);
        sb.Append("flow(第六幕→Start) + timedSwap ");
        return sb + " | " + Save(s);
    }

    // ---------- talksysteam：GameStart 开机叠加加载的对话系统老家，Canvas 同样挂到 dialoguUI 下持久化 ----------
    private static string WireTool()
    {
        var s = OpenSingle("tool/talksysteam");
        var sb = new StringBuilder();

        GameObject ui = null, canvas = null;
        foreach (var r in s.GetRootGameObjects())
        {
            if (r.GetComponent<DialogueUIController>() != null) ui = r;
            if (r.name == "对话系统") canvas = r;
        }
        if (ui == null) return "FAIL: dialoguUI not found";
        if (canvas == null) return "FAIL: 对话系统 canvas not found";
        if (canvas.transform.parent != ui.transform)
        {
            canvas.transform.SetParent(ui.transform, false);
            sb.Append("canvas reparented under dialoguUI; ");
        }
        else sb.Append("already reparented; ");
        return sb + " | " + Save(s);
    }

    // ---------- helpers ----------
    private static Scene OpenSingle(string name)
    {
        return EditorSceneManager.OpenScene("Assets/GameContent/01_Scenes/" + name + ".unity", OpenSceneMode.Single);
    }

    private static GameObject EnsureFlowObject(Scene s)
    {
        foreach (var r in s.GetRootGameObjects())
            if (r.name == FlowObjectName) return r;
        return new GameObject(FlowObjectName);
    }

    private static GameObject FindByPath(Scene s, string path)
    {
        int slash = path.IndexOf('/');
        string first = slash < 0 ? path : path.Substring(0, slash);
        GameObject root = null;
        foreach (var r in s.GetRootGameObjects())
        {
            if (r.name == first) { root = r; break; }
        }
        if (root == null) return null;
        if (slash < 0) return root;
        var t = root.transform.Find(path.Substring(slash + 1));
        return t != null ? t.gameObject : null;
    }

    private static T AddOrGet<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static GameObject Must(GameObject go, string label, StringBuilder sb)
    {
        if (go == null) sb.Append("MISSING[").Append(label).Append("] ");
        return go;
    }

    private static DialogueDataContainer Container(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<DialogueDataContainer>(DialogueDir + "/" + assetName + ".asset");
    }

    private static string Save(Scene s)
    {
        EditorSceneManager.MarkSceneDirty(s);
        return EditorSceneManager.SaveScene(s) ? "saved" : "SAVE-FAILED";
    }
}
