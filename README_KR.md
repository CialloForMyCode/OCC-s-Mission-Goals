# OCC's Mission & Goals

ONC Compiler Collection을 위한 업데이트 / 수정 추적 도구로, 작업 관리 효율을 높이고 생산성을 향상시킵니다. GUI(WPF)와 CLI(표준 JSON 출력) 듀얼 모드로 AI / 스크립트 / CI 통합에 적합합니다.

# Language

[中文 README](README_ZH.md) **|**
[README for English](README.md) **|**
[README на русском](README_RU.md) **|**
[日本語の README](README_JP.md) **|**
[한국어 README](README_KR.md) **|**
[README en français](README_FR.md) **|**

# 목차

- [설치](#설치)
- [사용법](#사용법)
- [CLI 명령어](#cli-명령어)
- [아키텍처](#아키텍처)
- [기여자](#기여자)

---

# 설치

### 요구 사항

- Windows 10 / 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

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

서드파티 NuGet 의존성 없음 — 순수 .NET 8 + WPF로 즉시 사용 가능.

---

# 사용법

### 기본 워크플로

1. **프로젝트 생성** — 메뉴 → 새 프로젝트 (`Ctrl+N`): 이름, 설명, 초기 버전 설정
2. **버전 생성** — 버전 대화상자로 번호 반복 (예: `0.1.0-alpha.1` → `0.1.0-alpha.2`)
3. **항목 추가** — 도구 모음 → 새 항목: 제목, 심각도, 마감일, 관련 파일 등 입력
4. **진행 추적** — 「미완료」페이지에서 항목 탐색 및 관리
5. **완료 및 보관** — 완료 표시 후 「완료됨」페이지에 표시. 버전 내 모든 항목 완료 시 원클릭 보관

### 페이지

| 페이지 | 기능 |
|--------|------|
| 대시보드 | 심각도 분포 차트, 최근 동향 및 프로젝트 개요 |
| 미완료 | 버전별 그룹화된 미완료 항목: 검색, 정렬, 편집, 완료, 삭제 |
| 완료됨 | 버전별 완료 항목: 실행 취소, 편집, 삭제; 전체 완료 시 보관 가능 |
| 확장 센터 | 플러그인 / 확장 관리 |
| 도움말 | 전체 사용자 가이드: 기본 작업, 단축키, 필드 참조, CLI 참조 |

### 정렬 옵션

하단 도구 모음에서 7가지 정렬 모드 제공:

| 정렬 | 설명 |
|------|------|
| 심각도 오름차순 | Fatal → Update |
| 심각도 내림차순 | Update → Fatal |
| 마감일 오름차순 | 빠른 순 |
| 마감일 내림차순 | 늦은 순 |
| 버전 오름차순 | 버전 문자열 알파벳순 |
| 버전 내림차순 | 버전 문자열 역순 |
| 즐겨찾기만 | 즐겨찾기 항목만, 심각도순 |

### 심각도 수준

| 값 | 의미 |
|----|------|
| `Fatal` | 치명적 — 최우선 즉시 조치 필요 |
| `Severe` | 심각 — 높은 우선순위 |
| `General` | 일반 — 기본값 |
| `Patch` | 패치 — 경미한 수정 |
| `Update` | 업데이트 — 기능 업데이트 |

### 데이터 저장

모든 데이터는 실행 파일과 동일한 위치의 `Projects/`에 저장됩니다:

```
Projects/
└── <프로젝트이름>/
    ├── project.json              # 프로젝트 메타데이터
    └── versions/
        ├── 0.1.0-alpha.0.json    # 버전 데이터 파일
        ├── 0.2.0-alpha.0.json
        └── archive/              # 보관된 버전
```

항목 ID 형식은 `PPPEEEEEE`(9자리): 앞 3자리 프로젝트 번호, 뒤 6자리 자동 증가.

### 듀얼 모드

`Main`에서 시작 인수를 감지: 인수 없음 → **GUI 모드**(WPF 창); 인수 있음 → **CLI 모드**(콘솔, JSON 출력).

---

# CLI 명령어

CLI 모드는 AI / 스크립트 / CI용으로 설계되었습니다. 모든 출력은 JSON(stdout), 오류는 stderr로.

```
OCCMissionGoals.exe [-p <프로젝트>] [-v <버전>] <명령> [인수]
```

### 항목 명령어

| 명령 | 짧은 | 긴 | 인수 | 설명 |
|------|------|-----|------|------|
| 추가 | `-a` | `--add` | `{Title="...", Severity="Fatal", ...}` | JSON 또는 간소화된 `Key="Value"` 구문으로 항목 추가 |
| 확인 | `-c` | `--check` | `<id>` | 항목 상세 정보 보기 (JSON) |
| 완료 | `-d` | `--done` | `<id>` | 항목을 완료로 표시 |
| 취소 | `-u` | `--undone` | `<id>` | 완료를 취소 |
| 삭제 | `-D` | `--delete` | `<id>` | 항목 삭제 (복구 불가) |
| 즐겨찾기 | `-f` | `--favorited` | `<id> true\|false` | 즐겨찾기 상태 설정 |
| 목록 | `-l` | `--list` | — | 모든 항목 나열 (JSON 배열) |

### 버전 명령어 (`-v`)

| 사용법 | 설명 |
|--------|------|
| `-v <버전>` | 지정 버전으로 전환 |
| `-v Iterate` | 반복 번호 증가 (예: `alpha.0` → `alpha.1`) |
| `-v Delete <버전>` | 버전 파일 삭제 (현재 버전은 불가) |
| `-v Archive <버전>` | 버전을 `versions/archive/`로 보관 (모든 항목 완료 필수, 현재 버전 불가) |

### 전역 옵션

| 플래그 | 설명 |
|--------|------|
| `-p <이름>` / `--project <이름>` | 대상 프로젝트 지정 |
| `-v <버전>` | 대상 버전 지정 (항목 명령어와 함께 사용) |
| `help` / `-h` / `--help` | 도움말 출력 |

### 항목 추가 형식

```
-a {Title="버그수정", Severity="Fatal", Brief="요약", Detail="상세",
    IsFavorited=false, Version="0.1.0", Type=["Bug"],
    RelatedFiles={"C:\\src\\App.cs"=[25,10,"App.Init"]}}
```

필수 필드는 `Title`뿐. `Severity` 기본값은 `General`. `Type`은 문자열 배열, `RelatedFiles`는 경로 → `[행, 열, 함수]` 맵.

### 예제

```bash
# 프로젝트 "ONC"의 모든 항목 나열
OCCMissionGoals.exe -p ONC -l

# 치명적 버그 추가
OCCMissionGoals.exe -a {Title="NullRef 충돌", Severity="Fatal", Brief="시작 시 충돌", Version="0.1.0-alpha.0", Type=["Bug"], RelatedFiles={"C:\\src\\App.cs"=[25,10,"App.Init"]}}

# 완료 표시
OCCMissionGoals.exe -d 001000001

# 버전 전환 후 항목 추가
OCCMissionGoals.exe -v 0.2.0-alpha.0 -a {Title="로그인 추가", Severity="Update"}
```

---

# 아키텍처

```
OCC-s-Mission-Goals/
├── App.xaml / .cs              # 진입점: 인수 감지 → GUI 또는 CLI
├── MainWindow.xaml / .cs       # 메인 창, 프레임리스 + 블러 오버레이
├── CliCommand.cs               # CLI 파싱 및 실행
├── ConfigManager.cs            # config.ini 읽기/쓰기
├── ThemeManager.cs             # 라이트 / 다크 테마 전환
├── FolderPicker.cs             # 폴더 선택 래퍼
├── AssemblyInfo.cs             # 어셈블리 정보
│
├── Models/                     # 데이터 모델
│   ├── GoalEntry.cs            # 항목 엔티티 + SortMode 열거형
│   ├── DataFile.cs             # JSON 데이터 파일 구조
│   ├── ProjectConfig.cs        # 프로젝트 설정
│   ├── PageRegistration.cs     # 페이지 등록
│   └── SeverityHelper.cs       # 심각도 → 표시 텍스트
│
├── Services/                   # 서비스 계층
│   ├── DataService.cs          # JSON 읽기/쓰기 + 버전 간 CRUD
│   ├── ProjectService.cs       # 다중 프로젝트 및 버전 관리
│   └── TipService.cs           # 알림 메시지 생성
│
├── Pages/                      # 메인 페이지
│   ├── LogPage.xaml            # 대시보드
│   ├── UnDonePage.xaml         # 미완료 항목
│   ├── DonePage.xaml           # 완료 항목
│   ├── ExpandPage.xaml         # 확장 센터
│   └── HelpPage.xaml           # 도움말 (전체 CLI 참조 포함)
│
├── ToolPages/                  # 하단 도구 모음 페이지
│   ├── SwitchPage.xaml         # 탭 전환
│   ├── SortPage.xaml           # 정렬 선택
│   ├── ControlButtonPage.xaml  # 빠른 작업 버튼
│   └── MenuPage.xaml           # 메뉴 바
│
├── Dialogs/                    # 대화상자
│   ├── NewEntryDialog.xaml     # 새 항목 / 편집
│   ├── NewProjectDialog.xaml   # 새 프로젝트 / 편집
│   └── VersionDialog.xaml      # 버전 관리
│
├── Styles.xaml                 # 전역 WPF 스타일
└── ThemeBrushes.xaml           # 테마 색상 브러시
```

### 기술 스택

- **런타임**: .NET 8
- **UI**: WPF (Windows Presentation Foundation)
- **데이터 형식**: JSON (System.Text.Json)
- **설정 형식**: INI
- **서드파티 의존성**: 없음

### 데이터 모델

```
project.json          →  ProjectConfig (Name, Description, CurrentVersion, ProjectNumber)
versions/*.json       →  DataFile (User, Unfinished[], Finished[])
각 항목               →  GoalEntry
```

항목 필드:

| 필드 | JSON 타입 | 설명 |
|------|-----------|------|
| `Id` | `string` | 숨김 ID `PPPEEEEEE` (9자리) |
| `Title` | `string` | 제목 (필수) |
| `Severity` | `string` | Fatal / Severe / General / Patch / Update |
| `Brief` | `string` | 요약 설명 |
| `Detail` | `string` | 상세 설명 |
| `Deadline` | `[년,월,일]` | 마감일 |
| `CompletedAt` | `[년,월,일]` | 완료일 |
| `ChangeDemand` | `int` | 변경 요구 카운터 |
| `IsFavorited` | `bool` | 즐겨찾기 플래그 |
| `Version` | `string` | 버전 문자열 |
| `Type` | `string[]` | 유형 태그 (Bug, UI, Feature 등) |
| `RelatedFiles` | `{경로: [행,열,함수]}` | 관련 파일 참조 |

---

# 기여자

| 이름 | 역할 | 기여 |
|------|------|------|
| [I-AM-SOLO](https://github.com/CialloForMyCode) | 저자 | 설계, 개발, 유지보수 |

---

> MIT License.
> 저장소: [https://github.com/CialloForMyCode/OCC-s-Mission-Goals](https://github.com/CialloForMyCode/OCC-s-Mission-Goals)
