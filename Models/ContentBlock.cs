using System.Text.Json.Serialization;

namespace OCCMissionGoals.Models;

/// <summary>
/// 内容区块的类型。条目的「内容区」由若干可拖拽排序的块组成，
/// 取代以往「一整段 Markdown 文本」的编辑方式。
/// </summary>
public enum ContentBlockKind
{
    /// <summary>文本：只支持 # 标题、粗体、斜体、删除线、编号列表与列表。</summary>
    Text,

    /// <summary>分割线。</summary>
    Divider,

    /// <summary>表格。</summary>
    Table,

    /// <summary>代码块：可选语言、行号、复制。</summary>
    Code,

    /// <summary>文件引用项：路径 / 行 / 列 / 函数。内容区只显示文件名，完整路径见「相关文件」表。</summary>
    FileRef,

    /// <summary>列表：Items 为该层级的条目（多级列表即条目中再嵌一个 List）。</summary>
    List,

    /// <summary>子任务：一条可勾选完成的小项，Done 记录勾选状态，参与条目的完成度。</summary>
    SubTask
}

/// <summary>表格块的数据。</summary>
public class TableBlockData
{
    [JsonPropertyName("Headers")]
    public List<string> Headers { get; set; } = new();

    [JsonPropertyName("Rows")]
    public List<List<string>> Rows { get; set; } = new();
}

/// <summary>代码块的数据。</summary>
public class CodeBlockData
{
    /// <summary>语言名（用于头部标签，扩展包可据此着色）。</summary>
    [JsonPropertyName("Language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("Code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("ShowLineNumbers")]
    public bool ShowLineNumbers { get; set; } = true;
}

/// <summary>
/// 内容区的一个区块。除 <see cref="Kind"/> 外的字段按类型取用：
/// Text 用 <see cref="Text"/>，Table / Code / FileRef 各用自己的数据，
/// List 用 <see cref="Items"/> 承载该层级的条目。
/// </summary>
public class ContentBlock
{
    [JsonPropertyName("Kind")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContentBlockKind Kind { get; set; } = ContentBlockKind.Text;

    /// <summary>文本块正文；列表条目（List）也可用 Text 作为条目文字。</summary>
    [JsonPropertyName("Text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("Table")]
    public TableBlockData? Table { get; set; }

    [JsonPropertyName("Code")]
    public CodeBlockData? Code { get; set; }

    [JsonPropertyName("File")]
    public FileRef? File { get; set; }

    /// <summary>List：该层条目是否使用编号（true）还是「·」符号（false）。</summary>
    [JsonPropertyName("Ordered")]
    public bool Ordered { get; set; }

    /// <summary>List 条目 / SubTask 子任务：是否已完成（完成度统计的就是这些勾选）。</summary>
    [JsonPropertyName("Done")]
    public bool Done { get; set; }

    /// <summary>List 的条目列表：条目本身仍是 ContentBlock，可以继续嵌套 List 形成多级列表。</summary>
    [JsonPropertyName("Items")]
    public List<ContentBlock> Items { get; set; } = new();

    public static ContentBlock NewText(string text = "") =>
        new() { Kind = ContentBlockKind.Text, Text = text };

    public static ContentBlock NewDivider() => new() { Kind = ContentBlockKind.Divider };

    public static ContentBlock NewTable() => new()
    {
        Kind = ContentBlockKind.Table,
        Table = new TableBlockData
        {
            Headers = new List<string> { string.Empty, string.Empty },
            Rows = new List<List<string>> { new() { string.Empty, string.Empty } }
        }
    };

    public static ContentBlock NewCode() => new()
    {
        Kind = ContentBlockKind.Code,
        Code = new CodeBlockData { Language = string.Empty, Code = string.Empty, ShowLineNumbers = true }
    };

    public static ContentBlock NewFileRef() => new()
    {
        Kind = ContentBlockKind.FileRef,
        File = new FileRef()
    };

    public static ContentBlock NewList(bool ordered = false) => new()
    {
        Kind = ContentBlockKind.List,
        Ordered = ordered,
        Items = new List<ContentBlock> { NewText() }
    };

    /// <summary>子任务区块：一条可勾选完成的小项。</summary>
    public static ContentBlock NewSubTask(string text = "") => new()
    {
        Kind = ContentBlockKind.SubTask,
        Text = text
    };
}
