# 多语言资源（Languages）

每个语言一个 XAML 资源字典（`ResourceDictionary`），程序启动时会自动扫描本目录下的所有
`*.xaml` 并加载。**新增语言无需改代码**：复制任意一个现有文件，改名为目标语言代码
（如 `de.xaml`），修改 `__lang_code` / `__lang_name`，并逐条填写译文即可。

## 文件格式

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:s="clr-namespace:System;assembly=mscorlib">

    <!-- Deutsch (de) -->
    <s:String x:Key="__lang_code">de</s:String>
    <s:String x:Key="__lang_name">Deutsch</s:String>

    <s:String xml:space="preserve" x:Key="取消">Abbrechen</s:String>
    <s:String xml:space="preserve" x:Key="设置">Einstellungen</s:String>
</ResourceDictionary>
```

- `__lang_code`：语言代码（与 `config.ini` 中的 `language` 值对应，如 `zh` / `en` / `ru`）。
- `__lang_name`：语言在设置页下拉框中显示的名称。
- `<s:String x:Key="中文原文">译文</s:String>`：`x:Key` 是**中文原文**（界面默认文案），
  元素内容为该语言译文。程序按 key 查找，找不到时回退显示 key（中文）本身。

## 占位符

需要动态插入文本时，key 中使用 `{0}`、`{1}` 等占位符，例如：

```xml
<s:String xml:space="preserve" x:Key="已更新条目「{0}」">Updated entry "{0}"</s:String>
<s:String xml:space="preserve" x:Key="| 截止日期 | {0:yyyy-MM-dd} |">| Deadline | {0:yyyy-MM-dd} |</s:String>
```

## 注意事项

- 编码必须为 UTF-8。
- 键（`x:Key`）里的空格、换行、`{0}` 等由程序处理；手工编辑时：
  - 以 `{` 开头的键，前缀写成 `{}`（如 `{} {0} 天` → 实际键 `{0} 天`）。
  - 键或译文中的换行 / 制表符用 `&#10;` / `&#9;` 表示。
  - 译文含首尾空格或多个连续空格时，保留元素上的 `xml:space="preserve"`。
- 同一份中文原文（key）在所有语言间共用；若两处界面用了相同中文但想用不同译文，
  需要先把中文文案改成不同的措辞，否则会被视为同一条。
- 默认语言为 `zh`（代码见 `LocalizationManager.DefaultLanguage`），始终可用，
  无需提供 `zh.xaml`（但保留它便于对照与补全）。
