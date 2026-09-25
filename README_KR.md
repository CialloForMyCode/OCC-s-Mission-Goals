<div align="center">

# OCC's Mission & Goals

ONC Compiler Collection을 위한 업데이트 / 수정 관리 도구입니다. 항목 기록을 빠르게 남기고 무엇이 아직 남았는지 한눈에 확인할 수 있게 해 줍니다.
듀얼 모드: 일상적인 작업은 WPF GUI로, AI / 스크립트 / CI 연동은 표준 JSON을 출력하는 CLI로 사용합니다.

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![UI](https://img.shields.io/badge/UI-WPF-5C2D91)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/license-GPL--2.0-blue)](LICENSE.txt)
[![Release](https://img.shields.io/github/v/release/CialloForMyCode/OCC-s-Mission-Goals?label=release&color=brightgreen)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/CialloForMyCode/OCC-s-Mission-Goals/total?label=downloads&color=blue)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases)

**언어:** [中文](README_ZH.md) | [English](README.md) | [Русский](README_RU.md) | [日本語](README_JP.md) | [한국어](README_KR.md) | [Français](README_FR.md)

</div>

---

# 목차

- [설치](#설치)
- [사용법](#사용법)
- [CLI 명령어](#cli-명령어)
- [아키텍처](#아키텍처)
- [기여자](#기여자)
- [라이선스](#라이선스)

---

# 설치

### 다운로드

빌드된 설치 프로그램은 [Releases](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest) 페이지에 공개되어 있습니다.
두 가지 모두 자체 포함(self-contained) 빌드이며 .NET 런타임 설치가 필요 없습니다:

| 아키텍처 | 설치 프로그램 |
|----------|---------------|
| x64 (권장) | `OCC-Mission-Goals-<버전>-x64-setup.exe` |
| x86 | `OCC-Mission-Goals-<버전>-x86-setup.exe` |

### 요구 사항

| 항목 | 요구 사항 |
|------|-----------|
| 운영 체제 | Windows 10 / 11 |
| 설치 프로그램 실행 | 필요 없음 (자체 포함) |
| 소스에서 빌드 | [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) |

### 빌드

```bash
git clone https://github.com/CialloForMyCode/OCC-s-Mission-Goals.git
cd "OCC-s-Mission-Goals"
dotnet build
```

### 실행

```bash
# GUI 모드
dotnet run

# CLI 모드 (도움말 보기)
dotnet run -- -h
```

### 설치 프로그램 만들기

```bash
installer\build-installer.cmd
```

[Inno Setup 6](https://jrsoftware.org/isinfo.php)이 필요하며 결과물은 `output/`에 생성됩니다.

서드파티 NuGet 의존성이 전혀 없습니다. 순수 .NET 8 + WPF로 바로 사용할 수 있습니다.

---

# 사용법

### 기본 워크플로

1. **프로젝트 만들기** — 메뉴 → 새 프로젝트 (`Ctrl+N`): 이름, 설명, 초기 버전 설정
2. **버전 만들기** — 버전 대화상자로 버전 번호를 반복 생성 (예: `0.1.0-alpha.1` → `0.1.0-alpha.2`)
3. **항목 추가** — 도구 모음 → 새 항목: 제목, 심각도, 유형 태그, 내용 블록 입력
4. **진행 상황 확인** — 「미완료」 페이지에서 항목을 탐색하고 관리
5. **완료 및 보관** — 완료한 항목은 「완료됨」 페이지로 이동하며, 버전의 모든 항목이 끝나면 한 번에 보관할 수 있습니다
6. **프로젝트 관리** — 설정 → 프로젝트 정보: 이름과 설명을 수정하거나 현재 프로젝트를 삭제 (모든 버전과 항목이 영구 삭제되며 되돌릴 수 없습니다)

### 페이지

| 페이지 | 기능 |
|--------|------|
| 대시보드 | 심각도 분포, 최근 30일 심각도 추이 그래프, 완료 통계와 프로젝트 상태 개요 |
| 미완료 | 버전별로 미완료 항목을 표시합니다. 검색, 정렬, 편집, 완료, 삭제 지원 |
| 완료됨 | 버전별로 완료 항목을 표시합니다. 완료 취소, 편집, 삭제, 전부 완료 시 보관 지원 |
| 확장 센터 | 언어 팩, 테마, 확장을 진행률 표시줄과 함께 설치 / 제거합니다. 설치된 항목은 저장소와 비교되어 한 번에 업그레이드할 수 있습니다 |
| 도움말 | 전체 사용 설명서: 기본 조작, 단축키, 필드 설명, CLI 참조 |

### 항목 내용

항목 본문은 하나의 Markdown 덩어리가 아니라 드래그로 순서를 바꿀 수 있는 블록으로 구성됩니다.
왼쪽 손잡이를 끌어 순서를 바꾸고 `+`로 하위 블록을 추가합니다:

| 블록 | 설명 |
|------|------|
| 텍스트 | `#` 제목, 굵게, 기울임, 취소선, 번호 목록과 글머리 목록 |
| 표 | 머리글 행 + 임의의 데이터 행 |
| 구분선 | 가로 구분선 |
| 코드 | 언어 지정(선택)과 줄 번호 |
| 파일 참조 | 경로 + 행 / 열 / 함수 |
| 다단계 목록 | 중첩된 목록 항목 |
| 하위 작업 | 체크 항목으로, 항목의 완성도를 결정합니다 |

항목을 저장할 때 관련 파일과 완성도는 내용 영역에서 자동으로 집계됩니다.

### 정렬 옵션

| 정렬 | 설명 |
|------|------|
| 심각도 오름차순 | Fatal → Update |
| 심각도 내림차순 | Update → Fatal |
| 버전 오름차순 | 버전 문자열 기준 오름차순 |
| 버전 내림차순 | 버전 문자열 기준 내림차순 |
| 유형 오름차순 | 첫 번째 유형 태그 기준 사전순 |
| 즐겨찾기만 | 즐겨찾기한 항목만, 심각도 순 |

### 심각도 수준

| 값 | 표시 | 의미 |
|----|------|------|
| `Fatal` | 빨강 | 치명적 — 최우선 즉시 조치 필요 |
| `Severe` | 주황 | 심각 — 높은 우선순위 |
| `General` | 노랑 | 일반 — 기본값 |
| `Patch` | 초록 | 패치 — 경미한 수정 |
| `Update` | 파랑 | 업데이트 — 기능 업데이트 |

### 검색

상단 검색 상자에 입력하면 현재 목록이 실시간으로 필터링되며, 접두사로 일치 범위를 바꿀 수 있습니다:

| 접두사 | 일치 범위 |
|--------|-----------|
| `Text:` | 제목과 간단 설명 (접두사가 없을 때도 동일) |
| `Tag:` | 유형 태그 |
| `Setting:` | 설정 단축 항목 (프로젝트 정보 / 테마 / 통계). Enter로 선택 항목 실행 |
| `Function:` | 기능 명령 (새 항목, 새 프로젝트, 프로젝트 열기 등) |
| `File:` | 관련 파일 경로 |
| `Date:` | 날짜 |
| `Plugins:` | 확장 센터의 모든 플러그인 (전역 검색) |
| `Expand:` | 설치된 플러그인만 (전역 검색) |

### 데이터 저장

모든 데이터는 실행 파일과 같은 위치의 `Projects/` 폴더에 저장됩니다:

```
Projects/
└── <프로젝트 이름>/
    ├── project.json              # 프로젝트 메타데이터
    └── versions/
        ├── 0.1.0-alpha.0.json    # 버전 데이터 파일
        ├── 0.2.0-alpha.0.json
        └── archive/              # 보관된 버전
```

항목 번호 형식은 `PPPEEEEEE` (9자리)이며, 앞 3자리는 프로젝트 번호, 뒤 6자리는 자동 증가 항목 번호입니다.

### 확장 저장

확장 센터에서 설치한 언어 팩, 테마, 확장은 실행 파일과 같은 위치에 저장됩니다:

```
Languages/            # 언어 팩 (*.xaml)
Themes/               # 테마 (*.xaml)
Expand/<이름>/        # 확장: expand.json 매니페스트 + 선택적 XAML
```

확장은 리소스 키(색상, 모서리 반경, 테두리 두께 등)를 덮어쓸 수 있고, 레이아웃 조각을 제공해 기본 내용의 일부를 교체할 수도 있습니다.

### 듀얼 모드

`Main` 진입점이 시작 인수를 확인합니다. 인수가 없으면 **GUI 모드**(WPF 창), 인수가 있으면 **CLI 모드**(콘솔 출력)로 실행됩니다.
GUI와 CLI는 별도 프로세스이며 이름 있는 뮤텍스로 데이터 접근을 직렬화하므로, 동시에 실행해도 변경 내용이 유실되지 않습니다.

---

# CLI 명령어

CLI 모드는 AI / 스크립트 / CI를 위한 모드입니다. 기본은 사람이 읽기 쉬운 텍스트를 출력하고, `--json`을 붙이면 표준 JSON을 출력합니다. 오류는 stderr로 출력됩니다.

```
OCCMissionGoals.exe [전역 옵션] <명령어> [하위 명령어] [인수]
```

### 전역 옵션

| 플래그 | 설명 |
|--------|------|
| `-p`, `--project <이름>` | 대상 프로젝트 (폴더 이름 또는 프로젝트 이름) |
| `--json` | 사람이 읽는 텍스트 대신 JSON 출력 |
| `--version <버전>` | 전환하지 않고 지정 버전 읽기 |
| `-h`, `--help` | 도움말 출력. 명령어 뒤에 붙이면 상세 설명 (예: `entry --help`) |

### project — 프로젝트 관리

| 사용법 | 설명 |
|--------|------|
| `project list` | 모든 프로젝트 나열 |
| `project info [이름]` | 프로젝트 정보 표시 (기본은 `-p` 프로젝트) |

### version — 버전 관리

| 사용법 | 설명 |
|--------|------|
| `version list` | 모든 버전 나열 (`*`는 현재 버전) |
| `version current` | 현재 버전 번호 표시 |
| `version switch <버전>` | 지정 버전으로 전환 (저장됨) |
| `version iterate` | 현재 버전 반복 (사전 릴리스 번호 +1, 예: `0.1.0-alpha.0` → `0.1.0-alpha.1`) |
| `version delete <버전>` | 버전 삭제 (현재 버전은 삭제 불가) |
| `version archive <버전>` | `versions/archive/`로 보관 (모든 항목이 완료되어야 함) |

### entry — 항목 관리

| 명령어 | 설명 |
|--------|------|
| `entry list` | 항목 나열 |
| `entry show <번호\|인덱스\|제목>` | 항목 하나 표시 (JSON) |
| `entry add` | 항목 추가 |
| `entry edit <번호\|인덱스\|제목>` | 항목 편집 |
| `entry done <번호\|인덱스\|제목>` | 완료로 표시 |
| `entry undone <번호\|인덱스\|제목>` | 완료 취소 |
| `entry delete <번호\|인덱스\|제목>` | 항목 삭제 (되돌릴 수 없음) |
| `entry favorite <번호\|인덱스\|제목> <true\|false>` | 즐겨찾기 설정 |

전체 구문:

```
entry list     [--type u|f|a] [--search <키워드>] [--tag <태그>] [--favorite] [--all] [--version <버전>]
entry show     <번호|인덱스|제목> [--type u|f] [--version <버전>]
entry add      --title <제목> [--severity <수준>] [--brief <간단설명>] [--type <태그1,태그2>]
               [--favorite] [--version <버전>]
entry edit     <번호|인덱스|제목> [--title ...] [--severity ...] [--brief ...] [--type ...]
               [--favorite|--unfavorite] [--version <버전>]
```

`<번호|인덱스|제목>`에는 숨은 번호(예: `001000001`), 목록 인덱스, 제목 완전 일치를 사용할 수 있습니다.
`--type`은 `list` / `show`에서 범위(`u` 미완료, `f` 완료됨, `a` 전체), `add` / `edit`에서 유형 태그(쉼표 구분)를 지정합니다.

### tag — 태그 관리

현재 프로젝트의 유형 태그를 관리하며, 변경 사항은 모든 버전의 항목에 동기화됩니다.

| 사용법 | 설명 |
|--------|------|
| `tag list` | 모든 태그 나열 (색상 포함) |
| `tag add <이름> [--color <hex>]` | 태그 만들기 |
| `tag delete <이름>` | 태그를 삭제하고 모든 항목에서 제거 |
| `tag rename <이전> <새 이름>` | 태그 이름을 바꾸고 모든 항목 동기화 |

### 예전 표기

예전 단일 플래그 표기도 계속 지원됩니다:

```
-a/--add   -c/--check   -d/--done   -u/--undone   -D/--delete
-f/--favorited   -l/--list   -v <버전|Iterate|Delete|Archive>
```

`-a` / `--add`는 예전 JSON 형식도 받습니다: `-a {Title="...", Severity="Fatal", ...}`

### 예제

```bash
# 프로젝트 "ONC"의 모든 항목 나열
OCCMissionGoals.exe -p ONC entry list

# 치명적 버그 추가
OCCMissionGoals.exe -p ONC entry add --title "시작 시 NullReferenceException" --severity Fatal --brief "실행 직후 충돌" --type Bug --version 0.1.0-alpha.0

# 스크립트용 JSON 출력
OCCMissionGoals.exe -p ONC --json entry list

# 완료 표시
OCCMissionGoals.exe -p ONC entry done 001000001

# 버전 전환 후 항목 추가
OCCMissionGoals.exe -p ONC version switch 0.2.0-alpha.0
OCCMissionGoals.exe -p ONC entry add --title "로그인 추가" --severity Update

# 태그 관리
OCCMissionGoals.exe -p ONC tag add UI --color "#3D9DE8"
```

---

# 아키텍처

```
OCC-s-Mission-Goals/
├── App.xaml / .cs              # 진입점: 인수 확인 → GUI 또는 CLI
├── MainWindow.xaml / .cs       # 메인 창 (테두리 없음 + 흐림 오버레이)
├── CliCommand.cs               # CLI 파싱과 실행
├── ConfigManager.cs            # config.ini 읽기 / 쓰기
├── LocalizationManager.cs      # 언어 팩 검색, T(key)
├── ThemeManager.cs             # 밝은 / 어두운 테마 전환
├── FolderPicker.cs             # 폴더 선택기 래퍼
├── AssemblyInfo.cs             # 어셈블리 정보
├── Styles.xaml                 # 전역 WPF 스타일
│
├── Models/                     # 데이터 모델
│   ├── GoalEntry.cs            # 항목 엔터티; GoalSeverity / SortMode / SearchMode
│   ├── ContentBlock.cs         # 내용 블록 (텍스트 / 표 / 코드 / 파일 / 목록 / 하위 작업)
│   ├── DataFile.cs             # 데이터 파일 루트: User + Entries
│   ├── ProjectConfig.cs        # project.json
│   ├── PageRegistration.cs     # 페이지 등록
│   ├── SeverityHelper.cs       # 심각도 → 표시 문자열과 색상
│   ├── TypeTag.cs              # 유형 태그 표시 모델 (문자열 + 색상)
│   ├── SearchMatcher.cs        # 검색 모드별 항목 일치 판정
│   ├── RelativeTime.cs         # "몇 분 전" 같은 상대 시간 문구
│   ├── ExpandInfo.cs           # 확장 매니페스트 (expand.json)
│   ├── PluginInfo.cs           # 확장 카드 상태
│   └── ColorUtil.cs            # 색상 문자열 ↔ 브러시
│
├── Services/                   # 서비스 계층
│   ├── DataService.cs          # 데이터 파일 읽기 / 쓰기
│   ├── ProjectService.cs       # 다중 프로젝트와 버전 관리
│   ├── ContentBlocks.cs        # 내용 영역 렌더링과 파생 데이터
│   ├── Markdown.cs             # 경량 Markdown 렌더러
│   ├── EntryCopyFormatter.cs   # 「정보 복사」가 만드는 텍스트
│   ├── FileLock.cs             # 데이터 파일용 프로세스 간 뮤텍스
│   ├── FileRefJump.cs          # 관련 파일 위치로 이동
│   ├── ExpandService.cs        # 확장 검색, 로드, 관리
│   ├── ExpandAnimation.cs      # 펼침 / 접힘의 부드러운 전환
│   ├── LanguagePackService.cs  # 언어 팩 다운로드와 설치
│   ├── ThemePackService.cs     # 테마 다운로드와 설치
│   ├── PluginCatalog.cs        # 확장 센터와 검색이 공유하는 카탈로그
│   ├── UpdateService.cs        # 업데이트 확인
│   ├── AutoStartService.cs     # 시작 시 자동 실행 (HKCU Run 키)
│   └── TipService.cs           # 작업 팁 문구
│
├── Pages/                      # 메인 페이지
│   ├── LogPage.xaml            # 대시보드 (분포 + 추이 그래프 + 통계)
│   ├── UnDonePage.xaml         # 미완료 항목
│   ├── DonePage.xaml           # 완료된 항목
│   ├── ExpandPage.xaml         # 확장 센터
│   ├── SettingsPage.xaml       # 설정
│   └── HelpPage.xaml           # 도움말 (전체 CLI 참조 포함)
│
├── Controls/                   # 사용자 정의 컨트롤
│   ├── Marquee.cs              # 한 줄 텍스트 흐르기
│   └── ProgressBarAnimation.cs # 진행률 표시줄의 부드러운 채우기
│
├── ToolPages/                  # 하단 도구 모음 페이지
│   ├── SwitchPage.xaml         # 페이지 탭 전환
│   ├── SortPage.xaml           # 정렬 선택
│   ├── ControlButtonPage.xaml  # 빠른 작업 버튼
│   └── MenuPage.xaml           # 메뉴 모음
│
├── Dialogs/                    # 대화상자
│   ├── NewEntryDialog.xaml     # 항목 새로 만들기 / 편집
│   ├── BlockEditor.xaml        # 내용 블록 편집기
│   ├── NewProjectDialog.xaml   # 프로젝트 새로 만들기 / 편집
│   └── VersionDialog.xaml      # 버전 관리
│
├── Languages/                  # 함께 배포되는 언어 팩 (zh, en, ja, ko, ru)
├── Themes/                     # 함께 배포되는 테마 (Default, ItIsPinkish)
└── installer/                  # Inno Setup 스크립트와 빌드 스크립트
```

### 기술 스택

| 계층 | 선택 |
|------|------|
| 런타임 | .NET 8 |
| UI 프레임워크 | WPF (Windows Presentation Foundation) |
| 데이터 형식 | JSON (System.Text.Json) |
| 설정 형식 | INI |
| 설치 프로그램 | Inno Setup 6 |
| 서드파티 의존성 | 없음 |

### 데이터 모델

```
project.json        → ProjectConfig (Name, Description, CurrentVersion, ProjectNumber,
                                      TypeOptions, TypeColors, NextEntryId, StatsVersions, CreatedAt)
versions/*.json     → DataFile (User, Entries[])      # 미완료 / 완료는 Entry.Status로 구분
각 항목             → GoalEntry
```

항목 필드:

| 필드 | JSON 타입 | 설명 |
|------|-----------|------|
| `Id` | `string` | 숨은 번호 `PPPEEEEEE` (9자리). 항목의 안정적인 참조 |
| `Title` | `string` | 제목 (유일한 필수 필드) |
| `Severity` | `string` | `Fatal` / `Severe` / `General` / `Patch` / `Update` |
| `Status` | `string` | `Unfinished` / `Finished` |
| `Brief` | `string` | 간단한 설명 |
| `Contents` | `ContentBlock[]` | 본문: 순서가 있는 블록 (텍스트 / 표 / 구분선 / 코드 / 파일 참조 / 다단계 목록 / 하위 작업) |
| `Progress` | `int` | 완성도 0–100. 「하위 작업」 블록의 체크 상태에서 산출(목록 항목은 제외). 저장 시 갱신하고 로드 시 다시 계산 |
| `CompletedAt` | `string` (ISO 8601) | 완료 시각. 완료 전에는 비어 있음 |
| `CreatedAt` | `string` (ISO 8601) | 생성 시각 |
| `UpdatedAt` | `string` (ISO 8601) | 마지막 수정 시각 |
| `IsFavorited` | `bool` | 즐겨찾기 여부 |
| `Type` | `string[]` | 유형 태그 (Bug, UI, Feature 등) |
| `RelatedFiles` | `{경로: [행,열,함수]}` | 관련 파일 참조. 내용 영역에서 자동 집계 |

---

# 기여자

| 이름 | 역할 | 기여 |
|------|------|------|
| [I-AM-SOLO](https://github.com/CialloForMyCode) | 작성자 | 설계, 개발, 유지 보수 |

---

# 라이선스

이 프로젝트는 [GPL-2.0 License](LICENSE.txt)를 따릅니다.

저장소: [https://github.com/CialloForMyCode/OCC-s-Mission-Goals](https://github.com/CialloForMyCode/OCC-s-Mission-Goals)
