# -*- coding: utf-8 -*-
"""Apply Russian (Ru=) translations to every {loc:Loc ...} entry in the XAML files."""
import re, html, glob

EN2RU = {
    # --- Dialogs/GitHubLoginDialog.xaml ---
    "Sign in to GitHub": "Войти в GitHub",
    "Enter a Personal Access Token (classic or fine-grained). The read:user scope is required.": "Введите Personal Access Token (классический или с тонкой настройкой прав). Требуется область read:user.",
    "How to create a token? Open the GitHub tokens page": "Как создать токен? Открыть страницу токенов GitHub",
    "Cancel": "Отмена",
    "Sign in": "Войти",
    # --- Dialogs/NewEntryDialog.xaml ---
    "New Entry": "Новая запись",
    "Severity": "Важность",
    "🔴 Fatal": "🔴 Критический",
    "🔴 Severe": "🔴 Серьёзный",
    "🔴 General": "🔴 Обычный",
    "🔴 Patch": "🔴 Исправление",
    "🔵 Update": "🔵 Обновление",
    "Category": "Категория",
    "Select an existing category or type a new one and press Enter / click Add": "Выберите существующую категорию или введите новую и нажмите Enter / «Добавить»",
    "Add": "Добавить",
    "Color (optional)": "Цвет (необязательно)",
    "Title": "Заголовок",
    "Brief": "Краткое описание",
    "Details": "Подробности",
    "Deadline": "Срок",
    "Version": "Версия",
    "The version this entry belongs to": "Версия данной записи",
    "Related Files": "Связанные файлы",
    "＋ Add File": "＋ Добавить файл",
    "Namespace.Class.Method": "Пространство имён.Класс.Метод",
    "Line": "Строка",
    "Column": "Столбец",
    "Confirm": "Подтвердить",
    # --- Dialogs/NewProjectDialog.xaml ---
    "New Project": "Новый проект",
    "Open Project": "Открыть проект",
    "Project Name": "Название проекта",
    "Project Description": "Описание проекта",
    "Initial Version": "Начальная версия",
    "Select Project": "Выбрать проект",
    # --- Dialogs/VersionDialog.xaml ---
    "Version Management": "Управление версиями",
    "New Version": "Новая версия",
    "Open Version": "Открыть версию",
    "Select Version File": "Выбрать файл версии",
    "OK": "ОК",
    # --- MainWindow.xaml ---
    "Ready": "Готово",
    "No status update": "Нет обновлений статуса",
    "Adjust target file": "Изменить целевой файл",
    "Search by mode (click to fill prefix):": "Поиск по режиму (нажмите для подстановки префикса):",
    "Text: Text": "Text: Текст",
    "Tag: Tag": "Tag: Тег",
    "File: File": "File: Файл",
    "Date: Date": "Date: Дата",
    "Setting: Settings": "Setting: Настройки",
    "Plugins: Plugins": "Plugins: Плагины",
    "Expand: Installed": "Expand: Установленные",
    "No results": "Нет результатов",
    # --- Pages/DonePage.xaml ---
    "Archive": "Архивировать",
    "Completed on": "Завершено",
    "Edit": "Изменить",
    "Undo Complete": "Отменить завершение",
    "Delete": "Удалить",
    "Function": "Функция",
    "No completed entries": "Нет завершённых записей",
    # --- Pages/ExpandPage.xaml ---
    "Extension Center": "Центр расширений",
    "Search extensions...": "Поиск расширений...",
    "Refresh": "Обновить",
    "Installed": "Установлено",
    "Author:": "Автор:",
    "Install": "Установить",
    "Uninstall": "Удалить",
    "No extensions": "Нет расширений",
    # --- Pages/HelpPage.xaml (quoted) ---
    "Help / User Guide": "Справка / Руководство пользователя",
    "Overview": "Обзор",
    "Dashboard": "Панель управления",
    "Unfinished": "Незавершённые",
    "Finished": "Завершённые",
    "Extensions": "Расширения",
    "Change Demand": "Требование изменений",
    "Type": "Тип",
    "Shortcuts": "Горячие клавиши",
    "Search": "Поиск",
    "Projects": "Проекты",
    "CLI": "CLI",
    "OCC's Mission & Goals is a lightweight task-tracking tool for developers and project maintainers. It uses the entry (Mission) as its basic unit; each entry records one independent goal, supporting severity levels, deadlines, version ownership, related-file tracking, and multi-project switching.": "OCC's Mission & Goals — это лёгкий инструмент отслеживания задач для разработчиков и сопровождающих проектов. Основной единицей является запись (Mission); каждая запись описывает одну самостоятельную цель и поддерживает уровни важности, сроки, привязку к версии, отслеживание связанных файлов и переключение между несколькими проектами.",
    "Mission": "Запись (Mission)",
    "The most basic unit of work, containing fields such as title, brief, details, severity, deadline, change demand, and type tags.": "Самая базовая единица работы, содержащая такие поля, как заголовок, краткое описание, подробности, важность, срок, требование изменений и теги типа.",
    "Entries are grouped by version number. The version format is major.minor.patch-tag.iteration, e.g. 0.1.0-alpha.0.": "Записи группируются по номеру версии. Формат версии: major.minor.patch-tag.iteration, например 0.1.0-alpha.0.",
    "Project": "Проект",
    "Supports multiple project directories; each project has its own data.json and project.json for Git version control and team collaboration.": "Поддерживает несколько каталогов проектов; у каждого проекта свои data.json и project.json для контроля версий в Git и совместной работы команды.",
    "App Structure": "Структура приложения",
    "• Navigation      Dashboard / Unfinished / Finished / Extensions / Help": "• Навигация      Панель управления / Незавершённые / Завершённые / Расширения / Справка",
    "• Status bar      Project switcher dropdown": "• Строка состояния      Выпадающий список переключения проекта",
    "• Toolbar      Hold Ctrl to show: New Entry / Search / Sort / Push / CLI shortcuts": "• Панель инструментов      Удерживайте Ctrl, чтобы показать: Новая запись / Поиск / Сортировка / Отправка / CLI",
    "• Storage      All entries are saved as JSON in each project directory": "• Хранилище      Все записи сохраняются в формате JSON в каталоге каждого проекта",
    "Dashboard (LogPage)": "Панель управления (LogPage)",
    "Project overview and statistics center": "Обзор проекта и центр статистики",
    "Progress card": "Карточка прогресса",
    "Shows the completion rate of the current project: finished / total, with a percentage progress bar. Gives an at-a-glance view of overall project progress.": "Показывает процент завершения текущего проекта: завершено / всего, с процентной полосой прогресса. Даёт быстрый взгляд на общий прогресс проекта.",
    "Severity distribution": "Распределение важности",
    "Visualizes the entry count for each of the five severity levels (Fatal/Severe/General/Patch/Update), helping you quickly identify risk distribution.": "Визуализирует количество записей для каждого из пяти уровней важности (Критический/Серьёзный/Обычный/Исправление/Обновление), помогая быстро оценить распределение рисков.",
    "Upcoming deadlines": "Ближайшие сроки",
    "Lists unfinished entries due within 7 days, sorted by due date. Click to jump to the corresponding entry.": "Показывает незавершённые записи со сроком в течение 7 дней, отсортированные по дате. Нажмите, чтобы перейти к нужной записи.",
    "Recent activity timeline": "Хроника недавней активности",
    "Shows recently completed entries in reverse chronological order with completion dates, for tracking the latest progress.": "Показывает недавно завершённые записи в обратном хронологическом порядке с датами завершения, для отслеживания последних изменений.",
    "Contribution chart": "График вклада",
    "Aggregates completions by date and shows daily contribution trends as a bar chart, with switchable time ranges and details.": "Суммирует завершения по датам и показывает динамику ежедневного вклада в виде столбчатой диаграммы, с переключаемыми периодами и деталями.",
    "Version editor": "Редактор версии",
    "Edit the current version directly at the bottom of the dashboard. Click the Iterate button to auto-increment the iteration number (last segment +1). Save after editing, and new entries will belong to that version.": "Редактируйте текущую версию прямо внизу панели управления. Нажмите кнопку «Итерация», чтобы автоматически увеличить номер итерации (последний сегмент +1). После сохранения новые записи будут относиться к этой версии.",
    "GitHub push settings": "Настройки отправки в GitHub",
    "Configure the GitHub repository URL, branch name, and Personal Access Token for one-click pushing of data files. Supports auto-generated commit messages.": "Настройте URL репозитория GitHub, имя ветки и Personal Access Token для отправки файлов данных в один клик. Поддерживает автоматическую генерацию сообщений коммитов.",
    "Unfinished (UnDonePage)": "Незавершённые (UnDonePage)",
    "Manage all unfinished goals": "Управление всеми незавершёнными целями",
    "Core features": "Основные возможности",
    "• Version grouping — entries are grouped by version; each group can collapse/expand, and its header shows the version name and entry count": "• Группировка по версиям — записи группируются по версии; каждую группу можно свернуть/развернуть, а в заголовке показаны имя версии и количество записей",
    "• Favoriting — click the star button to favorite an entry; favorited entries are highlighted in the list": "• Избранное — нажмите кнопку со звездой, чтобы добавить запись в избранное; избранные записи подсвечиваются в списке",
    "• Sorting — by severity (high→low), deadline (soonest→latest), version (asc/desc), or favorites only": "• Сортировка — по важности (высокая→низкая), по сроку (ближайший→дальний), по версии (возр./убыв.) или только избранное",
    "• Search filter — type keywords in the top search box for real-time filtering, with prefix switches for match scope: Text: content, Tag: tags, Setting: settings (project/theme/push), File: file path, Date: date; without a prefix it matches text, and under Setting: press Enter to run a setting": "• Поиск и фильтр — вводите ключевые слова в верхнее поле поиска для фильтрации в реальном времени; префиксы переключают область поиска: Text: содержимое, Tag: теги, Setting: настройки (проект/тема/отправка), File: путь к файлу, Date: дата; без префикса поиск идёт по тексту, а в режиме Setting: нажмите Enter, чтобы выполнить настройку",
    "• Expand details — click the Details button to expand the full info panel, including the description and related-file table": "• Развернуть подробности — нажмите кнопку «Подробнее», чтобы развернуть полную панель информации, включая описание и таблицу связанных файлов",
    "• Edit entry — click the edit button to open the editor and modify all fields: title, brief, details, severity, deadline, change demand, type tags, related files": "• Изменить запись — нажмите кнопку изменения, чтобы открыть редактор и изменить все поля: заголовок, краткое описание, подробности, важность, срок, требование изменений, теги типа, связанные файлы",
    "• Complete entry — mark an entry as finished, record the completion time automatically, and move it from this page to the Finished page": "• Завершить запись — отметьте запись как завершённую, время завершения запишется автоматически, и запись переместится на страницу «Завершённые»",
    "• Delete entry — remove an unwanted entry (irreversible)": "• Удалить запись — удалите ненужную запись (безвозвратно)",
    "Entry fields": "Поля записи",
    "Title — required, the entry's short name": "Заголовок — обязательно, краткое название записи",
    "Brief — optional, one-line description": "Краткое описание — необязательно, описание в одну строку",
    "Details — optional, full description in Markdown": "Подробности — необязательно, полное описание в формате Markdown",
    "Severity — Fatal / Severe / General / Patch / Update": "Важность — Критический / Серьёзный / Обычный / Исправление / Обновление",
    "Deadline — optional; overdue entries are highlighted in red": "Срок — необязательно; просроченные записи подсвечиваются красным",
    "Change demand — optional integer representing change complexity and impact": "Требование изменений — необязательное целое число, отражающее сложность и влияние изменений",
    "Type tags — optional multi-select, e.g. bug, feature, refactor": "Теги типа — необязательный множественный выбор, например bug, feature, refactor",
    "Related files — optional multiple entries, each with path, function name, line, and column": "Связанные файлы — необязательные несколько записей, каждая с путём, именем функции, строкой и столбцом",
    "Version — assigned to the current version at creation and cannot be changed manually": "Версия — присваивается текущей версии при создании и не может быть изменена вручную",
    "Finished (DonePage)": "Завершённые (DonePage)",
    "View and manage finished entries": "Просмотр и управление завершёнными записями",
    "• Same overall structure as the Unfinished page — version grouping, search filter, detail expansion, etc.": "• Та же общая структура, что и на странице «Незавершённые» — группировка по версиям, поиск и фильтр, развёртывание подробностей и т. д.",
    "• Undo completion — restore a finished entry to unfinished status, moving it back to UnDonePage": "• Отменить завершение — вернуть завершённую запись в статус незавершённой, переместив её обратно на UnDonePage",
    "• Archive by version — the archive button only appears when all entries in a version are finished. Archiving exports Markdown and deletes the version data file": "• Архивация по версии — кнопка архивации появляется только тогда, когда все записи версии завершены. Архивация экспортирует Markdown и удаляет файл данных версии",
    "• Edit — the same editing as on the Unfinished page": "• Изменение — то же редактирование, что и на странице «Незавершённые»",
    "• Delete — remove a finished entry": "• Удаление — удалить завершённую запись",
    "• Completion date — each entry card also shows a Completed-on date": "• Дата завершения — каждая карточка записи также показывает дату завершения",
    "Extensions (ExpandPage)": "Расширения (ExpandPage)",
    "Plugin and feature extension management": "Управление плагинами и расширениями",
    "• Browse by category — available extensions are grouped and shown by category": "• Просмотр по категориям — доступные расширения сгруппированы и показаны по категориям",
    "• Search filter — the top search box filters the plugin list in real time": "• Поиск и фильтр — верхнее поле поиска фильтрует список плагинов в реальном времени",
    "• Install / uninstall — install or remove an extension with one click": "• Установка / удаление — установите или удалите расширение в один клик",
    "• View details — click a plugin card to see its description and usage": "• Просмотр подробностей — нажмите на карточку плагина, чтобы увидеть описание и способ использования",
    "Entry priority and severity system": "Система приоритетов и важности записей",
    "There are five severity levels from high to low, each marked by its own color:": "Существует пять уровней важности от высокого к низкому, каждый со своим цветом:",
    "Fatal": "Критический",
    "Blocker-level issue, must be handled immediately": "Блокирующая проблема, требует немедленного решения",
    "Severe": "Серьёзный",
    "Important issue, resolve as soon as possible": "Важная проблема, решить как можно скорее",
    "General": "Обычный",
    "Routine tasks and requirements, the default level": "Обычные задачи и требования, уровень по умолчанию",
    "Patch": "Исправление",
    "Minor changes and fixes, low priority": "Небольшие изменения и исправления, низкий приоритет",
    "Update": "Обновление",
    "Feature updates and experience improvements, lowest priority": "Обновления функций и улучшения интерфейса, самый низкий приоритет",
    "Severity directly affects the default list sorting (high→low) and is visualized in the dashboard distribution chart. It can be adjusted at any time when editing an entry.": "Важность напрямую влияет на сортировку списка по умолчанию (высокая→низкая) и отображается на диаграмме распределения панели управления. Её можно изменить в любой момент при редактировании записи.",
    "Measuring task change impact": "Измерение влияния изменений задачи",
    "Change demand is an optional integer field that quantifies the complexity and impact of the changes involved in an entry.": "Требование изменений — необязательное целое поле, которое количественно оценивает сложность и влияние изменений, связанных с записью.",
    "• The larger the value, the more complex and wide-ranging the change": "• Чем больше значение, тем сложнее и масштабнее изменение",
    "• Entries can be sorted by change demand ascending or descending": "• Записи можно сортировать по требованию изменений по возрастанию или убыванию",
    "• Typical usage: 0 = no change / 1-3 = small adjustments / 4-6 = medium refactor / 7+ = large-scale change": "• Типичное использование: 0 = без изменений / 1–3 = небольшие правки / 4–6 = средний рефакторинг / 7+ = крупное изменение",
    "• Set it in the editor when editing an entry": "• Задаётся в редакторе при изменении записи",
    "Version management system": "Система управления версиями",
    "The version number uses a five-segment semantic format:": "Номер версии использует пятисегментный семантический формат:",
    "major.minor.patch-tag.iteration": "major.minor.patch-tag.iteration",
    "e.g. 0.1.0-alpha.0": "например 0.1.0-alpha.0",
    "• Each entry belongs to a version and is set to the current version when created": "• Каждая запись относится к версии и при создании получает текущую версию",
    "• Edit the current version at the bottom of the dashboard; new entries belong to the new version after the change": "• Измените текущую версию внизу панели управления; после изменения новые записи будут относиться к новой версии",
    "• Click the Iterate button to increment the iteration number by 1 (e.g. alpha.0 → alpha.1)": "• Нажмите кнопку «Итерация», чтобы увеличить номер итерации на 1 (например alpha.0 → alpha.1)",
    "• The entry list is grouped by version, and each version can collapse/expand": "• Список записей сгруппирован по версиям, каждую версию можно свернуть/развернуть",
    "• Finished entries can only be archived after the whole version is finished, keeping the view tidy": "• Завершённые записи можно архивировать только после завершения всей версии, чтобы список оставался аккуратным",
    "Category tag system": "Система тегов категорий",
    "• Each entry can have one or more type tags (multi-select)": "• Каждая запись может иметь один или несколько тегов типа (множественный выбор)",
    "• Type tags are defined and managed centrally in the project config (project.json)": "• Теги типа определяются и управляются централизованно в конфигурации проекта (project.json)",
    "• Common tags: bug, feature, refactor, docs, test, chore": "• Распространённые теги: bug, feature, refactor, docs, test, chore",
    "• Type tags appear as labels on entry cards for quick identification": "• Теги типа отображаются как метки на карточках записей для быстрой идентификации",
    "• Check or uncheck type tags in the editor when editing an entry": "• Отмечайте или снимайте теги типа в редакторе при изменении записи",
    "Code file reference tracking": "Отслеживание ссылок на файлы кода",
    "Each entry can link to multiple code file references for recording source code locations related to the task. Each reference contains the following fields:": "Каждая запись может ссылаться на несколько файлов кода для фиксации мест исходного кода, связанных с задачей. Каждая ссылка содержит следующие поля:",
    "Path": "Путь",
    "The associated source file path (required)": "Путь к связанному исходному файлу (обязательно)",
    "The related function or method name (optional)": "Имя связанной функции или метода (необязательно)",
    "The exact code line number (optional)": "Точный номер строки кода (необязательно)",
    "The exact code column number (optional)": "Точный номер столбца кода (необязательно)",
    "In the entry detail panel, the Related Files table shows all linked references. You can add, modify, or delete related files when editing an entry.": "На панели подробностей записи таблица «Связанные файлы» показывает все ссылки. Вы можете добавлять, изменять или удалять связанные файлы при редактировании записи.",
    "Keyboard shortcuts": "Горячие клавиши",
    "Hold Ctrl": "Удерживайте Ctrl",
    "Show the bottom toolbar and sidebar tab labels": "Показать нижнюю панель инструментов и подписи вкладок боковой панели",
    "Release Ctrl": "Отпустите Ctrl",
    "Automatically collapse the toolbar and sidebar to maximize the content view": "Автоматически свернуть панель инструментов и боковую панель, чтобы максимально расширить область содержимого",
    "The bottom toolbar provides quick actions: new entry, search filter, sort toggle, GitHub push, CLI mode, etc. Hold Ctrl to view and use them.": "Нижняя панель инструментов даёт быстрые действия: новая запись, поиск и фильтр, переключение сортировки, отправка в GitHub, режим CLI и т. д. Удерживайте Ctrl, чтобы увидеть и использовать их.",
    "A guide to finding entries and settings": "Руководство по поиску записей и настроек",
    "Basic usage": "Основное использование",
    "The search box sits in the center of the top toolbar and is always visible. Type a keyword to filter the current list page in real time and pop up the results board; click a result to jump, or press Enter to run the top result.": "Поле поиска находится в центре верхней панели инструментов и всегда видимо. Введите ключевое слово, чтобы фильтровать текущую страницу списка в реальном времени и открыть панель результатов; нажмите на результат, чтобы перейти, или нажмите Enter, чтобы выполнить первый результат.",
    "Search modes": "Режимы поиска",
    "Prefix syntax switches the match scope; without a prefix it matches as text by default.": "Префиксный синтаксис переключает область поиска; без префикса по умолчанию идёт поиск по тексту.",
    "• Text: — title, brief, details": "• Text: — заголовок, краткое описание, подробности",
    "• Tag: — type tags": "• Tag: — теги типа",
    "• Setting: / settings: — settings (global search)": "• Setting: / settings: — настройки (глобальный поиск)",
    "• File: — related-file path and function name": "• File: — путь к связанному файлу и имя функции",
    "• Date: — date (yyyy-MM-dd)": "• Date: — дата (yyyy-MM-dd)",
    "• Plugins: — Extension Center plugins (global search)": "• Plugins: — плагины центра расширений (глобальный поиск)",
    "• Expand: — installed plugins (global search)": "• Expand: — установленные плагины (глобальный поиск)",
    "Setting search": "Поиск по настройкам",
    "Setting: mode does not filter the entry list; it lists setting shortcuts in the results board instead, and shows all of them even when the keyword is empty. Click to run directly: toggle dark/light theme, open project settings, or open push settings.": "Режим Setting: не фильтрует список записей; вместо этого он показывает ярлыки настроек на панели результатов, причём показывает их все даже при пустом ключевом слове. Нажмите, чтобы выполнить напрямую: переключить тёмную/светлую тему, открыть настройки проекта или открыть настройки отправки.",
    "Plugin search": "Поиск плагинов",
    "Plugins: searches all plugins in the Extension Center; Expand: searches only installed plugins. Neither filters the entry list — results appear in the board, listing all matching plugins when the keyword is empty; click a result to jump to the Extension Center.": "Plugins: ищет все плагины в центре расширений; Expand: ищет только установленные плагины. Ни один из них не фильтрует список записей — результаты появляются на панели, при пустом ключевом слове перечисляются все подходящие плагины; нажмите на результат, чтобы перейти в центр расширений.",
    "Results board": "Панель результатов",
    "• Entry results show a type label (Unfinished / Finished); click Go to locate the entry.": "• Результаты записей показывают метку типа (Незавершённые / Завершённые); нажмите «Перейти», чтобы найти запись.",
    "• With empty input, a search-by-mode guide appears; click a prefix chip to fill it in.": "• При пустом вводе появляется подсказка поиска по режиму; нажмите на чип префикса, чтобы подставить его.",
    "• Click outside the search box to close the results board.": "• Нажмите вне поля поиска, чтобы закрыть панель результатов.",
    "Multi-project directories and data management": "Несколько каталогов проектов и управление данными",
    "Project directories": "Каталоги проектов",
    "Supports multiple project directories, each with its own folder. Use the top-right dropdown to switch projects at any time. Entries and versions are fully independent between projects.": "Поддерживает несколько каталогов проектов, у каждого своя папка. Используйте выпадающий список в правом верхнем углу, чтобы переключать проекты в любой момент. Записи и версии полностью независимы между проектами.",
    "Data files": "Файлы данных",
    "• data.json — stores all entry data (unfinished + finished) as a JSON array": "• data.json — хранит все данные записей (незавершённые + завершённые) в виде массива JSON",
    "• project.json — stores project metadata: project name, type tag definitions, version number, etc.": "• project.json — хранит метаданные проекта: название проекта, определения тегов типа, номер версии и т. д.",
    "• All files are plain-text JSON, so they can be tracked with Git and used for team collaboration": "• Все файлы — обычный текстовый JSON, поэтому их можно отслеживать в Git и использовать для совместной работы команды",
    "CLI mode": "Режим CLI",
    "Supports scripted operations via the command line, for CI/CD integration and batch processing. Click the CLI button in the bottom toolbar or launch with command-line arguments.": "Поддерживает скриптовые операции через командную строку для интеграции с CI/CD и пакетной обработки. Нажмите кнопку CLI на нижней панели инструментов или запустите с аргументами командной строки.",
    "GitHub integration": "Интеграция с GitHub",
    "After configuring the GitHub repository, branch, and Personal Access Token, you can push data.json to the remote repository with one click. It supports auto-generated commit messages (including the version number and change summary) for automatic backup and team sharing.": "После настройки репозитория GitHub, ветки и Personal Access Token вы можете отправить data.json в удалённый репозиторий одним кликом. Поддерживается автоматическая генерация сообщений коммитов (включая номер версии и сводку изменений) для автоматического резервного копирования и совместного использования командой.",
    "OCCMissionGoals.exe command-line interface": "Интерфейс командной строки OCCMissionGoals.exe",
    "The program detects command-line arguments at startup: launching with arguments enters CLI mode (console), and launching without arguments enters GUI mode. All CLI output is standard JSON; errors go to stderr. Suitable for AI / scripts / CI integration.": "Программа определяет аргументы командной строки при запуске: запуск с аргументами включает режим CLI (консоль), а запуск без аргументов — режим GUI. Весь вывод CLI — стандартный JSON; ошибки выводятся в stderr. Подходит для интеграции с AI / скриптами / CI.",
    "OCCMissionGoals.exe [-p <project>] <command> [args]": "OCCMissionGoals.exe [-p <проект>] <команда> [аргументы]",
    "Entry operations": "Операции с записями",
    "Add an entry. The data format is JSON or simplified key=value syntax.": "Добавить запись. Формат данных — JSON или упрощённый синтаксис key=value.",
    "View entry details (e.g. -c 001000001), returns the full JSON.": "Показать подробности записи (например -c 001000001), возвращает полный JSON.",
    "Mark an entry as finished and record the completion date automatically.": "Отметить запись как завершённую и автоматически записать дату завершения.",
    "Restore a finished entry to unfinished status.": "Вернуть завершённую запись в статус незавершённой.",
    "Delete an entry (irreversible).": "Удалить запись (безвозвратно).",
    "Favorite / unfavorite. Usage: -f <id> true|false": "Добавить в избранное / убрать. Использование: -f <id> true|false",
    "List all entries in the current project (unfinished + finished) as a JSON array.": "Вывести все записи текущего проекта (незавершённые + завершённые) в виде массива JSON.",
    "Version operations": "Операции с версиями",
    "-v <version>": "-v <версия>",
    "Switch to a specified version; subsequent operations run on that version's data file.": "Переключиться на указанную версию; последующие операции выполняются с файлом данных этой версии.",
    "Version iteration: increments the current version's iteration number by 1 (e.g. alpha.0 → alpha.1).": "Итерация версии: увеличивает номер итерации текущей версии на 1 (например alpha.0 → alpha.1).",
    "-v Delete <version>": "-v Delete <версия>",
    "Delete the specified version's data file. The current version cannot be deleted.": "Удалить файл данных указанной версии. Текущую версию удалить нельзя.",
    "-v Archive <version>": "-v Archive <версия>",
    "Archive the specified version to the versions/archive/ directory. Requires all entries in the version to be finished; the current version cannot be archived.": "Архивировать указанную версию в каталог versions/archive/. Требуется, чтобы все записи версии были завершены; текущую версию архивировать нельзя.",
    "Global options": "Глобальные параметры",
    "Specify the target project (matched by directory name or project name in project.json).": "Указать целевой проект (по имени каталога или имени проекта в project.json).",
    "When used with entry commands, specifies the target version.": "При использовании с командами записей задаёт целевую версию.",
    "Print help information.": "Вывести справочную информацию.",
    "-a data format example": "-a пример формата данных",
    "Supports standard JSON or simplified syntax using = instead of : (field names can be unquoted):": "Поддерживает стандартный JSON или упрощённый синтаксис с = вместо : (имена полей можно не заключать в кавычки):",
    "FixBug": "FixBug",
    "Only Title is required. Severity defaults to General. Type is a string array; RelatedFiles maps a path → [line, column, function].": "Обязательно только поле Title. Severity по умолчанию равно General. Type — массив строк; RelatedFiles сопоставляет путь → [строка, столбец, функция].",
    "Output format": "Формат вывода",
    "• Normal output is JSON (stdout), indented for readability": "• Обычный вывод — JSON (stdout) с отступами для читаемости",
    "• Errors are written to stderr": "• Ошибки выводятся в stderr",
    "• Exit code: 0 = success, non-zero = failure": "• Код возврата: 0 = успех, не 0 = ошибка",
    "• Entry fields: index, id, title, severity, severityLabel, brief, detail, deadline, completedAt, changeDemand, isFavorited, version, relatedFiles": "• Поля записи: index, id, title, severity, severityLabel, brief, detail, deadline, completedAt, changeDemand, isFavorited, version, relatedFiles",
    # --- Pages/LogPage.xaml ---
    "Completed": "Завершено",
    "Total": "Всего",
    "Completion": "Завершение",
    "Severity Distribution": "Распределение важности",
    "Upcoming": "Предстоящие",
    "Recent Activity": "Недавняя активность",
    "Contributions": "Вклад",
    "No activity": "Нет активности",
    "Log out": "Выйти",
    "Save": "Сохранить",
    "Maj": "Мажор",
    "Min": "Минор",
    "Pat": "Патч",
    "Iterate": "Итерация",
    "Push Data Now": "Отправить данные",
    "GitHub Settings": "Настройки GitHub",
    "Push Location": "Место отправки",
    "Push File Settings": "Настройки файла отправки",
    "More Push Settings": "Другие настройки отправки",
    # --- Pages/SettingsPage.xaml ---
    "Settings": "Настройки",
    "Appearance": "Внешний вид",
    "Project Info": "Информация о проекте",
    "Push / Repos": "Отправка / Репозитории",
    "System / Updates": "Система / Обновления",
    "Choose the app light or dark style. Changes apply and save immediately.": "Выберите светлый или тёмный стиль приложения. Изменения применяются и сохраняются сразу.",
    "Light": "Светлая",
    "Dark": "Тёмная",
    "Language": "Язык",
    "Choose the interface language. Changes apply and save immediately.": "Выберите язык интерфейса. Изменения применяются и сохраняются сразу.",
    "Accent Color": "Акцентный цвет",
    "Choose the accent color used for buttons progress and selected states. Changes apply and save immediately.": "Выберите акцентный цвет для кнопок прогресса и выбранных состояний. Изменения применяются и сохраняются сразу.",
    "Custom": "Свой",
    "Apply": "Применить",
    "Edit the current project name and description. The version is maintained by version management and shown read-only here.": "Измените название и описание текущего проекта. Версия управляется через управление версиями и показана здесь только для чтения.",
    "Current Version": "Текущая версия",
    "Save Project Info": "Сохранить информацию о проекте",
    "Push / Repo Settings": "Настройки отправки / репозитория",
    "Manage the target repos and commit options used when generating update log commits. Changes save automatically.": "Управление целевыми репозиториями и параметрами коммитов для создания коммитов журнала обновлений. Изменения сохраняются автоматически.",
    "Target Repo": "Целевой репозиторий",
    "Select a target repo from the signed-in GitHub account": "Выберите целевой репозиторий из аккаунта GitHub после входа",
    "Not signed in to GitHub: sign in on the account page then click Refresh.": "Не выполнен вход в GitHub: войдите на странице аккаунта и нажмите «Обновить».",
    "Branch": "Ветка",
    "Select an existing branch or type a new branch name to create it on push": "Выберите существующую ветку или введите имя новой ветки чтобы создать её при отправке",
    "File to push": "Файл для отправки",
    "Select the file to push from the app bin folder": "Выберите файл для отправки из папки bin приложения",
    "Commit Generation Options": "Параметры создания коммита",
    "Include author in commit message": "Включить автора в сообщение коммита",
    "Group by date": "Группировать по дате",
    "Launch at startup": "Автозапуск",
    "Automatically launch this app when you sign in to Windows. It uses the current user startup registry entry and requires no administrator rights.": "Автоматически запускать приложение при входе в Windows. Используется запись автозапуска текущего пользователя в реестре и не требуются права администратора.",
    "Start with Windows": "Запускать вместе с Windows",
    "Auto Update": "Автообновление",
    "Check GitHub Releases for new versions then download and run the installer after confirmation.": "Проверяет GitHub Releases на новые версии и после подтверждения загружает и запускает установщик.",
    "Check for updates on startup": "Проверять обновления при запуске",
    "Check for Updates": "Проверить обновления",
    "Download & Install": "Загрузить и установить",
    "Open Download Page": "Открыть страницу загрузки",
    # --- Pages/UnDonePage.xaml ---
    "Due": "Срок",
    "Complete": "Завершить",
    "No unfinished entries": "Нет незавершённых записей",
    # --- ToolPages/ControlButtonPage.xaml ---
    "Edit Entry": "Изменить запись",
    "Theme": "Тема",
    # --- ToolPages/MenuPage.xaml ---
    "Project(_P)": "Проект(_P)",
    "New Project(_N)": "Новый проект(_N)",
    "Open Project(_O)": "Открыть проект(_O)",
    "Project Settings(_S)": "Настройки проекта(_S)",
    "Entries(_V)": "Записи(_V)",
    "New Entry(_N)": "Новая запись(_N)",
    "File(_F)": "Файл(_F)",
    "New Data File(_N)": "Новый файл данных(_N)",
    "Open Data File(_O)": "Открыть файл данных(_O)",
    "Help(_H)": "Справка(_H)",
    # --- ToolPages/SortPage.xaml ---
    "Sort by:": "Сортировка:",
    "Severity ascending": "По важности (возрастание)",
    "Severity descending": "По важности (убывание)",
    "Deadline ascending": "По сроку (возрастание)",
    "Deadline descending": "По сроку (убывание)",
    "Version ascending": "По версии (возрастание)",
    "Version descending": "По версии (убывание)",
    "Favorites only": "Только избранное",
}


def xml_escape(s: str) -> str:
    return (s.replace('&', '&amp;').replace('<', '&lt;')
             .replace('>', '&gt;').replace('"', '&quot;'))


def extract_en(body: str):
    """Return (en_raw_clean_with_ws, style) for the En= value of a loc body (raw, may contain entities)."""
    i = body.find('En=')
    if i < 0:
        return None, None
    rest = body[i + 3:]
    if rest.startswith('&quot;') and rest.endswith('&quot;'):
        return rest[6:-6], 'quoted'
    if rest.startswith('"') and rest.endswith('"'):
        return rest[1:-1], 'dquoted'
    return rest, 'unquoted'


def main():
    files = [p for p in glob.glob('**/*.xaml', recursive=True)
             if '/obj/' not in p and '/bin/' not in p]
    total_applied = 0
    missing = []
    skipped = []
    for p in sorted(files):
        data = open(p, encoding='utf-8').read()
        out = []
        pos = 0
        changed = False
        for m in re.finditer(r'\{loc:Loc\s+.*?\}', data, re.S):
            full = m.group(0)
            body = full[len('{loc:Loc '):-1]
            if 'Ru=' in body:
                out.append(data[pos:m.start()])
                out.append(full)
                pos = m.end()
                continue
            en_raw, style = extract_en(body)
            if en_raw is None:
                out.append(data[pos:m.start()]); out.append(full); pos = m.end(); continue
            en_clean = html.unescape(en_raw)
            en_strip = en_clean.strip()
            if en_strip not in EN2RU:
                missing.append((p, en_strip))
                out.append(data[pos:m.start()]); out.append(full); pos = m.end(); continue
            lead = en_clean[:len(en_clean) - len(en_clean.lstrip())]
            trail = en_clean[len(en_clean.rstrip()):]
            ru = lead + EN2RU[en_strip] + trail
            ru_esc = xml_escape(ru)
            # Always emit Ru using the double-quote (entity) form, matching the
            # markup-extension quoting that HelpPage already uses; safe for both
            # attribute- and element-content loc blocks.
            new_body = body + ', Ru=&quot;' + ru_esc + '&quot;'
            out.append(data[pos:m.start()])
            out.append('{loc:Loc ' + new_body + '}')
            pos = m.end()
            changed = True
            total_applied += 1
        out.append(data[pos:])
        if changed:
            open(p, 'w', encoding='utf-8').write(''.join(out))
    print('Applied Ru to', total_applied, 'loc entries')
    if missing:
        print('MISSING translations for', len(missing), 'unique strings:')
        seen = set()
        for p, s in missing:
            if s not in seen:
                seen.add(s)
                print('  -', repr(s))
    else:
        print('No missing translations.')


if __name__ == '__main__':
    main()
