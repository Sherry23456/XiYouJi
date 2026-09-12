// 白骨精线全局状态：只在 cave 里点完第一幕对话时置 true，
// wild/cloud/village 的进场切换与流程编排都以它为开关，保证不影响其他线路。
public static class BaiGuJingLineState
{
    public static bool Active;
}
