namespace Smith.Rendering;

/// <summary>
/// 控制台输出抽象接口，定义所有用户界面交互方法
/// </summary>
public interface IConsoleRenderer
{
    /// <summary>成功消息（绿色 ✓）</summary>
    void Success(string message);

    /// <summary>错误消息（红色 ✗）</summary>
    void Error(string message);

    /// <summary>警告消息（黄色 ⚠）</summary>
    void Warning(string message);

    /// <summary>信息消息（青色 ℹ）</summary>
    void Info(string message);

    /// <summary>标题（品红色粗体）</summary>
    void Title(string title);

    /// <summary>键值对输出</summary>
    void KeyValue(string key, string value, int keyWidth = 20);

    /// <summary>表格输出</summary>
    void Table(string[] headers, List<string[]> rows);

    /// <summary>空行</summary>
    void NewLine();

    /// <summary>
    /// 用户确认提示，返回用户是否同意
    /// </summary>
    bool Confirm(string message);
}
