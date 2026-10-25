using System.Globalization;
using System.Windows;

namespace Pinlist.Desktop;

public static class UiText
{
    public const string English = "en";
    public const string SimplifiedChinese = "zh-CN";

    private static readonly IReadOnlyDictionary<string, string> EnglishText = new Dictionary<string, string>
    {
        ["AlwaysOnTopOff"] = "Always on top: off",
        ["AlwaysOnTopOn"] = "Always on top: on",
        ["AddTask"] = "Add task",
        ["Collapse"] = "Collapse",
        ["Expand"] = "Expand",
        ["CloseToTray"] = "Close to tray",
        ["Edit"] = "Edit",
        ["Delete"] = "Delete",
        ["MarkComplete"] = "Mark complete",
        ["ToggleImportant"] = "Toggle important",
        ["NothingPending"] = "Nothing pending",
        ["AddHint"] = "Use + or Alt+Shift+P to add a task",
        ["Undo"] = "Undo",
        ["Details"] = "DETAILS",
        ["NoDetails"] = "No additional details",
        ["NewTask"] = "New task",
        ["EditTask"] = "Edit task",
        ["TitleLabel"] = "TITLE",
        ["Low"] = "Low",
        ["Normal"] = "Normal",
        ["Urgent"] = "Urgent",
        ["Important"] = "Important",
        ["MarkDone"] = "Mark done",
        ["Cancel"] = "Cancel",
        ["SaveChanges"] = "Save changes",
        ["NeedTitle"] = "Give this task a title.",
        ["HistoryTitle"] = "History & Trash",
        ["HistorySubtitle"] = "Completed and removed tasks",
        ["Restore"] = "Restore",
        ["EmptyHistory"] = "No completed or removed tasks",
        ["StartWithWindows"] = "Start with Windows",
        ["GlobalShortcut"] = "Global shortcut: Alt+Shift+P",
        ["HideDuringFullscreen"] = "Hide during fullscreen",
        ["OpenPinlist"] = "Open Pinlist",
        ["QuickAdd"] = "Quick add",
        ["Language"] = "Language",
        ["Exit"] = "Exit",
        ["English"] = "English",
        ["SimplifiedChinese"] = "Simplified Chinese",
        ["ChooseLanguage"] = "Choose language / 选择语言",
        ["ChooseLanguageHint"] = "You can change this later from the tray menu.\n稍后可在托盘菜单中更改。",
        ["StatusOpen"] = "Open",
        ["StatusClosed"] = "Completed",
        ["StatusTrashed"] = "Removed",
        ["CouldNotLoadTasks"] = "Could not load tasks",
        ["BackgroundSyncStopped"] = "Background sync stopped",
        ["CouldNotSaveTask"] = "Could not save the task",
        ["TaskRestored"] = "Task restored",
        ["TaskCompleted"] = "Task completed",
        ["TaskDeleted"] = "Moved to trash",
        ["CouldNotUpdateTask"] = "Could not update the task",
        ["CouldNotDeleteTask"] = "Could not delete the task",
        ["MarkedImportant"] = "Marked important",
        ["ImportanceRemoved"] = "Importance removed",
        ["CouldNotUpdateImportance"] = "Could not update importance",
        ["OrderUpdated"] = "Order updated",
        ["CouldNotSaveOrder"] = "Could not save the new order",
        ["CouldNotUndo"] = "Could not undo the action"
    };

    private static readonly IReadOnlyDictionary<string, string> ChineseText = new Dictionary<string, string>
    {
        ["AlwaysOnTopOff"] = "置顶：关",
        ["AlwaysOnTopOn"] = "置顶：开",
        ["AddTask"] = "添加待办",
        ["Collapse"] = "折叠",
        ["Expand"] = "展开",
        ["CloseToTray"] = "关闭到托盘",
        ["Edit"] = "编辑",
        ["Delete"] = "删除",
        ["MarkComplete"] = "标记完成",
        ["ToggleImportant"] = "切换重要标记",
        ["NothingPending"] = "暂无待办",
        ["AddHint"] = "使用 + 或 Alt+Shift+P 添加待办",
        ["Undo"] = "撤销",
        ["Details"] = "详细内容",
        ["NoDetails"] = "没有补充内容",
        ["NewTask"] = "新建待办",
        ["EditTask"] = "编辑待办",
        ["TitleLabel"] = "标题",
        ["Low"] = "低",
        ["Normal"] = "普通",
        ["Urgent"] = "紧急",
        ["Important"] = "重要",
        ["MarkDone"] = "标记完成",
        ["Cancel"] = "取消",
        ["SaveChanges"] = "保存修改",
        ["NeedTitle"] = "请填写待办标题。",
        ["HistoryTitle"] = "历史与回收站",
        ["HistorySubtitle"] = "已完成和已移除的待办",
        ["Restore"] = "恢复",
        ["EmptyHistory"] = "暂无已完成或已移除的待办",
        ["StartWithWindows"] = "开机启动",
        ["GlobalShortcut"] = "全局快捷键：Alt+Shift+P",
        ["HideDuringFullscreen"] = "全屏时隐藏",
        ["OpenPinlist"] = "打开 Pinlist",
        ["QuickAdd"] = "快速添加",
        ["Language"] = "语言",
        ["Exit"] = "退出",
        ["English"] = "English",
        ["SimplifiedChinese"] = "简体中文",
        ["ChooseLanguage"] = "Choose language / 选择语言",
        ["ChooseLanguageHint"] = "You can change this later from the tray menu.\n稍后可在托盘菜单中更改。",
        ["StatusOpen"] = "待办",
        ["StatusClosed"] = "已完成",
        ["StatusTrashed"] = "已移除",
        ["CouldNotLoadTasks"] = "无法加载待办",
        ["BackgroundSyncStopped"] = "后台同步已停止",
        ["CouldNotSaveTask"] = "无法保存待办",
        ["TaskRestored"] = "待办已恢复",
        ["TaskCompleted"] = "待办已完成",
        ["TaskDeleted"] = "已移至回收站",
        ["CouldNotUpdateTask"] = "无法更新待办",
        ["CouldNotDeleteTask"] = "无法删除待办",
        ["MarkedImportant"] = "已标记为重要",
        ["ImportanceRemoved"] = "已取消重要标记",
        ["CouldNotUpdateImportance"] = "无法更新重要标记",
        ["OrderUpdated"] = "顺序已更新",
        ["CouldNotSaveOrder"] = "无法保存新顺序",
        ["CouldNotUndo"] = "无法撤销操作"
    };

    public static string CurrentLanguage { get; private set; } = DetectLanguage();

    public static string DetectLanguage() =>
        CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? SimplifiedChinese
            : English;

    public static void Apply(string? language)
    {
        CurrentLanguage = Normalize(language);
        var resources = System.Windows.Application.Current.Resources;
        foreach (var pair in CurrentDictionary()) resources[$"Text.{pair.Key}"] = pair.Value;
    }

    public static string Get(string key) => CurrentDictionary().TryGetValue(key, out var value) ? value : key;

    public static string OpenCount(int count) => CurrentLanguage == SimplifiedChinese
        ? $"{count} 个待办"
        : count == 1 ? "1 open" : $"{count} open";

    public static string Normalize(string? language) =>
        string.Equals(language, SimplifiedChinese, StringComparison.OrdinalIgnoreCase)
        || string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase)
            ? SimplifiedChinese
            : English;

    private static IReadOnlyDictionary<string, string> CurrentDictionary() =>
        CurrentLanguage == SimplifiedChinese ? ChineseText : EnglishText;
}
