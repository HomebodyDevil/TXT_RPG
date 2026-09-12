# Bag 준비 오류·세로 오류 문구 수정과 표시 설정 기본값

## 목적과 범위

TMP_MainScene에서 Bag 버튼을 누르면 "Inventory page references are incomplete."가 세로로 표시되는 문제를 수정합니다. 가방 준비 실패와 오류 텍스트 배치를 별도로 진단하고, 표시 설정 누락에는 개발자가 수정할 수 있는 기본값을 적용합니다.

이번 문서는 후속 구현 지침입니다. 게임 데이터 초기화, 신규 가방 시스템, 전체 Scene/Prefab 재생성, 운영 결과물에 남는 일회성 Tools 메뉴 추가는 범위 밖입니다. 다만 실행 중인 Unity Editor에서 선별 적용 메서드를 호출하기 위한 임시 내부 Tools 메뉴는 사용할 수 있습니다. 이 메뉴는 적용과 검증이 끝난 뒤 코드에서 제거하고, 제거 상태로 다시 컴파일하여 최종 결과물에 남지 않았음을 확인해야 합니다.

## 사전 확인과 근거

AGENTS.md, DOCS/architecture/quick-items-and-game-windows.md, DOCS/architecture/player-session.md, DOCS/development/workflows.md 및 DOCS/instructions/modal-content-container-and-grid-configuration.md를 읽습니다.

| 경로 | 확인된 내용과 조사 대상 |
| --- | --- |
| Assets/TxTRPG/Application/Runtime/Items/PlayerGameWindowPages.cs | InventoryGameWindowPage.PrepareAsync는 host, catalog, itemGrid 중 하나가 null이면 해당 예외를 발생시킵니다. |
| Assets/TxTRPG/Application/Runtime/Items/InventoryWindowModels.cs | GridContentLayoutSettings와 Normalize가 있으며 표시 설정 기본값이 이미 정의되어 있습니다. |
| Assets/TxTRPG/UI/Runtime/Windows/GameWindowService.cs | 준비 예외의 exception.Message를 host.SetError로 직접 전달합니다. |
| Assets/TxTRPG/UI/Runtime/Windows/ModalWindowHost.cs | SetError가 TMP_Text에 문자열을 넣고 활성화합니다. |
| Assets/TxTRPG/UI/Runtime/Windows/ModalContentContainer.cs | ContentRoot의 앵커와 Padding을 적용합니다. 오류 UI가 같은 레이아웃에 잘못 포함되는지 확인합니다. |
| Assets/TxTRPG/UI/Prefabs/InventoryWindowPage.prefab | 저장된 가방 내부 필수 참조를 검사합니다. |
| Assets/TxTRPG/UI/Prefabs/ModalWindowHost.prefab | 오류 표시 영역과 페이지의 실제 계층을 검사합니다. |
| Assets/Scenes/TMP_MainScene.unity | 기존 메뉴·Overlay·서비스 인스턴스를 보존하며 수정합니다. |

확인된 오류 조건은 필수 참조 누락이며 표시 설정 로드 실패를 의미하지 않습니다. gridSettings에 기본 객체 생성과 Normalize 경로도 이미 있으므로 중복 초기화 체계를 만들지 않습니다.

어떤 참조가 실제 누락됐는지와 세로 표시의 정확한 원인은 아직 런타임으로 확정되지 않았습니다. 좁은 RectTransform, 부모 LayoutGroup, 잘못된 재부모화, 텍스트 회전과 Wrap 등을 확인합니다.

## 1. 실제 원인 재현

- AppScene에서 TMP_MainScene으로 진입하는 정식 경로와 TMP_MainScene 직접 Play 경로를 구분합니다.
- Bag 클릭 시 실제 서비스가 준비하는 페이지 인스턴스의 경로·PageId·host·catalog·itemGrid를 확인합니다. 프로젝트 자산에 참조가 있다고 Scene 인스턴스에도 유효하다고 가정하지 않습니다.
- 중복 또는 구형 inventory 페이지가 등록됐는지 검사합니다.
- 세션이 없는 것과 세션이 아직 준비 중인 것을 구분합니다. 현재 host 조회 fallback 이후 실제 대상이 있는지 확인합니다.
- 오류 TMP_Text의 화면 폭·높이·회전·Scale·Anchor·Padding 및 부모가 제어하는 배치 값을 기록합니다.
- 예외 문자열이 실제로 공백 없이 생성되는지, 좁은 폭 때문에 한 글자씩 줄바꿈되는지 구분합니다.
- 원인별 진단 코드를 제공하여 앞으로 동일한 뭉뚱그린 문구만으로 상태를 판단하지 않게 합니다.

## 2. 필수 참조를 복구합니다

- itemGrid가 없으면 기존 가방 본문의 ActionGridPanel을 연결합니다. 운영 퀵 슬롯 Grid와 혼동하지 않습니다.
- 카탈로그가 누락되면 기존 운영 ItemCatalog를 명시적으로 연결합니다. Scene 전체 검색이나 이름 추측으로 첫 자산을 선택하지 않습니다.
- 세션은 기존 AppScene의 PlayerSessionHost를 재사용합니다. 새 플레이어 상태·임시 Singleton을 만들지 않습니다.
- cross-scene 객체 참조를 Prefab에 강제로 저장하지 않습니다. 기존 런타임 세션 연결 경계를 사용합니다.
- 가방의 필수 UI 참조는 저장된 Prefab과 필요한 Scene Override에 반영하고 재로드하여 검증합니다.
- 참조 복구가 불가능하면 오류 상태를 유지하되 닫기와 안전한 재시도 경로를 제공합니다. UI 객체를 무제한 자동 생성하여 누락을 숨기지 않습니다.

## 3. 기본값은 표시 설정에만 적용합니다

Unity 아키텍처 지침에 따라 UI 표시 설정과 실제 게임 상태를 분리합니다.

| 실패 종류 | 처리 정책 |
| --- | --- |
| Grid 표시 설정이 미지정 또는 로드 실패 | 유효한 기본 표시 설정을 사용합니다. |
| 일부 표시 설정 값이 잘못됨 | 정상 값은 유지하고 해당 필드만 검증·보정 또는 기본값으로 대체합니다. |
| 아이콘·장식 Style 로드 실패 | 기존 대체 아이콘·기본 Style을 사용합니다. |
| PlayerSession 준비 실패·손상된 저장 데이터 | 새 게임이나 빈 인벤토리로 대체하지 않고 오류 상태로 표시합니다. |
| ItemCatalog 또는 필수 itemGrid 누락 | 연결을 복구하거나 진단 가능한 오류 상태로 표시합니다. |
| 유효한 인벤토리의 항목 수 0 | 정상 EmptyState를 표시합니다. |

기본값 우선순위는 다음과 같습니다.

1. 명시적으로 적용된 유효한 설정입니다.
2. 해당 가방 Prefab에 작성된 기본 설정입니다.
3. 앞선 기본 설정도 사용할 수 없는 경우 코드의 안전한 최종 기본값입니다.

기본 설정은 개발자가 Inspector에서 수정할 수 있어야 합니다. 기존 직렬화 설정으로 충분하면 별도 ScriptableObject 로더를 만들지 않습니다. 이미 Profile 로드 구조가 있다면 재사용합니다. 코드 기본값은 외부 로드 실패 없이 생성 가능해야 합니다.

현재 기본값을 기준으로 다음을 제안합니다.

- VerticalScroll, AdaptiveUpToConfigured, 최대 4열입니다.
- 페이지 모드 전환 시 SlotsPerPage는 12이며 MinimumScrollSlots는 12입니다.
- 셀은 72×72, 간격은 8×8이며 기본 Padding은 0입니다.
- 수평 중앙, 마지막 행 왼쪽, 세로 상단 배치를 사용합니다.
- 개발자가 이미 유효하게 작성한 값을 이 제안으로 덮어쓰지 않습니다.

추가 요구 사항은 다음과 같습니다.

- enum의 알 수 없는 값, null Padding, 음수·0 크기, NaN·Infinity, 과도한 슬롯 수로 인한 메모리 위험을 검증합니다. 허용 상한은 현재 규모에 맞춰 문서화합니다.
- 공유 설정은 복사한 유효 설정으로 사용합니다. Normalize가 공유 원본이나 개발자 자산을 런타임에 변경하지 않게 합니다.
- 기본값을 사용한 이유와 실제 설정 출처를 Inspector 또는 개발 로그에서 확인할 수 있게 합니다.
- 기본값 사용은 정상 표시 가능한 상태로 처리하고 예외 원문을 플레이어에게 노출하지 않습니다.
- 사용자가 의도한 유효한 Paged 설정을 공간 부족만으로 몰래 VerticalScroll로 바꾸지 않습니다. 잘못된 설정값과 부족한 화면 크기를 구분합니다.
- 비동기 설정 로드가 실제로 존재한다면 timeout·취소·요청 세대를 처리합니다. 창을 닫은 뒤 늦게 도착한 설정이 다음 페이지를 덮어쓰지 않게 합니다.
- 기본값으로 표시한 후 무한 자동 재시도하지 않습니다. 재시도 시 사용자 선택과 스크롤을 불필요하게 초기화하지 않습니다.

## 4. 오류 영역을 가방 레이아웃과 분리합니다

- 공통 모달에 별도 ErrorStateRoot를 두거나 기존 오류 영역을 재사용합니다.
- ErrorStateRoot는 Grid 셀 배치나 CategoryTabs LayoutGroup의 자식 항목으로 취급하지 않습니다.
- 제목·닫기 영역을 제외한 본문 폭을 사용하고 적절한 Padding을 둡니다.
- TMP_Text는 가로쓰기와 정상적인 단어 줄바꿈을 사용합니다. 폭 문제를 극단적인 폰트 축소나 강제 줄바꿈 삭제로 숨기지 않습니다.
- 상위 LayoutGroup에 의해 폭이 한 글자 크기로 계산되는지 확인하고 레이아웃의 크기 소유자를 하나로 정합니다.
- ContentContainer가 현재 페이지 외 오류 UI까지 재부모화하거나 크기를 덮어쓰지 않게 합니다.
- 좁은 화면에서도 여러 글자가 읽히는 본문 폭과 가독성 있는 폰트 크기를 확보합니다. 긴 설명은 영역 내 스크롤 등으로 접근 가능하게 하고 닫기 버튼은 유지합니다.
- 오류 발생 시 로딩을 종료하고 실패한 본문의 입력을 차단합니다. 오류 UI와 닫기 입력까지 함께 비활성화하지 않습니다.

사용자 문구와 개발 진단을 분리합니다.

- 사용자 예시: "가방을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요."
- 개발 진단: 누락된 의존성 이름, 페이지 경로, 오류 코드와 원래 예외입니다.
- exception.Message를 사용자 UI에 그대로 표시하는 경로를 해당 공통 오류 처리 범위에서 개선합니다.
- 현지화 서비스가 준비되지 않았어도 읽을 수 있는 기본 문구를 제공합니다. 내부 경로나 키를 사용자에게 노출하지 않습니다.
- 취소는 오류로 표시하지 않습니다. 준비 요청 교체·창 닫기와 실패를 구분합니다.

## 5. 직접 적용과 회귀 방지

운영 결과물에 새 Tools 메뉴를 추가하지 않습니다. 컴파일 완료를 확인한 뒤 Editor 자동화로 관련 코드·Prefab·Scene의 필요한 부분만 적용·저장·재로드 검증합니다. 실행 중인 Editor에서 공개 정적 적용 메서드를 호출해야 할 때에는 `Tools/TxT RPG/Internal/...` 아래에 임시 메뉴를 추가할 수 있습니다. 임시 메뉴는 다음 조건을 모두 지켜야 합니다.

- 전체 Rebuild가 아니라 이번 작업의 선별 적용 메서드만 호출합니다.
- 적용 전에 Scene의 미저장 변경과 컴파일 상태를 확인합니다.
- 적용 성공 또는 실패 후 임시 `MenuItem` 선언을 제거합니다.
- 제거 후 Asset Refresh와 재컴파일을 수행하고, 메뉴가 더 이상 등록되지 않았는지 확인합니다.
- 임시 메뉴 경로를 개발자 사용 절차나 운영 문서의 상시 기능으로 기록하지 않습니다.

기존 메뉴 배치, Inventory 표시 모드, 열 수·슬롯 수, 이미지, 모달 크기와 사용자 Override를 가능한 한 보존합니다. 변경이 필요한 값은 근거와 함께 보고합니다. 전체 Rebuild 또는 기존 통합 메뉴를 무조건 실행하지 않습니다.

기존 생성형 자산의 원본 코드가 관련됐다면 같은 수정이 사라지지 않도록 소유권에 맞춰 갱신합니다. 사용자 저장 파일과 신규 게임 초기 지급 데이터는 변경하지 않습니다.

## 6. 테스트 및 완료 기준

- 기존 Bag 클릭 오류를 실제 Play Mode에서 재현하고 복구 후 같은 경로에서 가방이 열리는지 검사합니다.
- host, catalog, itemGrid 누락을 각각 격리된 테스트에서 재현하고 서로 다른 진단이 나오는지 확인합니다.
- 표시 설정 null·부분 오류·로드 실패·기본 Profile 실패에서 유효한 기본값으로 표시되는지 검사합니다.
- 기본값 사용 중에도 실제 소유 아이템이 유지되고 새 인벤토리가 생성되지 않는지 확인합니다.
- 빈 인벤토리, 정상 아이템, 카테고리·Paged·VerticalScroll 및 사용·퀵 등록 동작을 회귀 검증합니다.
- 오류 문구를 한국어·영어·긴 문구로 검사하고 좁은 모바일·데스크톱 화면에서 세로 한 글자 열로 붕괴하지 않는지 확인합니다.
- 닫기·Cancel·재시도와 빠른 열기·닫기에서 입력 차단·리스너·로드 작업이 남지 않는지 확인합니다.
- 오류 후 System 모달과 Inventory를 다시 열어 공통 오류 UI가 다른 본문을 덮지 않는지 검사합니다.
- 수정한 Prefab과 Scene을 저장 후 다시 열어 참조와 레이아웃을 검증합니다.
- 실제 검증 화면과 사용 설정을 기록하고, 자동화 불가 시 구체적인 차단 원인과 미완료 사항을 보고합니다.

## 문서와 최종 보고

DOCS/architecture/quick-items-and-game-windows.md에 기본값 정책, 필수 의존성과 오류 처리 경계를 반영합니다. DOCS/development/workflows.md에는 기본 설정 편집 위치와 오류 진단·복구 방법을 기록합니다.

최종 보고에는 실제 누락된 참조, 세로 텍스트의 원인, 기본값 적용 범위, 직접 적용한 자산·Scene, 수행 테스트와 미검증 사항을 포함합니다. 설정 기본값만 추가하고 기존 참조 오류가 남아 있다면 완료로 처리하지 않습니다.
