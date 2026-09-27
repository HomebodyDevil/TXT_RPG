# Bag 빈 상태 문구와 공통 모달 바깥 클릭 닫기

## 1. 목적과 확정 사항

Bag의 슬롯과 `No items in this category` 문구가 동시에 표시되는 문제를 수정하고, 공통 모달 바깥을 클릭하거나 탭하면 기존 닫기 경로로 창을 닫는다. 이 문서는 후속 구현 지침이며 실제 기능 적용·실행 검증 완료를 의미하지 않는다.

사용자가 확정한 기준은 다음과 같다.

- 아이템이 없는 빈 슬롯도 표시 슬롯으로 센다. 현재 표시 대상으로 구성된 슬롯이 하나라도 있으면 빈 상태 문구를 숨긴다.
- 현재 표시할 슬롯이 전혀 없을 때만 빈 상태 문구를 표시한다. 슬롯 수는 보유 아이템 수, 전체 카테고리 수, 단순한 컨테이너 자식 수와 구분한다.
- 바깥 클릭 닫기는 Bag에 한정하지 않고 현재 공통 모달을 사용하는 Bag·System·Status에 적용한다. 터치로 바깥을 탭하는 경우에도 닫는다.
- 창 내부의 빈 공간·슬롯·스크롤·버튼 조작은 바깥 클릭으로 취급하지 않는다. 기존 닫기 버튼과 키보드·컨트롤러 Cancel 동작은 유지한다.

## 2. 선행 조사와 현재 사실

`AGENTS.md`, `DOCS/README.md`, `DOCS/architecture/quick-items-and-game-windows.md`, `DOCS/architecture/action-grid-panel.md`, `DOCS/development/workflows.md`를 먼저 읽는다. 필요하면 `DOCS/instructions/inventory-error-layout-and-settings-fallback.md`와 `DOCS/instructions/modal-content-container-and-grid-configuration.md`를 참고한다. 기존 지침이 구현 완료 상태를 보장하지 않으므로 최신 코드와 저장 자산을 확인한다.

시작 시 Git 상태와 관련 diff를 확인하여 기존 사용자 변경을 보존한다. `.codegraph/`가 있으면 호출·영향 분석에 먼저 사용하고 실제 소스·자산으로 확인한다. 임의로 재색인하지 않는다.

작성 시 소스로 확인한 내용은 다음과 같다.

1. `InventoryGameWindowPage`는 독립된 동명 파일이 아니라 `PlayerGameWindowPages.cs`에 선언되어 있다. `Refresh`에서 현재 페이지의 `entries`를 구한 뒤 `gridSettings.CalculateDisplayCapacity(entries.Count)`로 표시 용량을 계산하고 `itemGrid.SetEntries(entries, capacity)`에 전달한다.
2. 같은 메서드의 빈 상태 조건은 `emptyState.gameObject.SetActive(entries.Count == 0)`이다. 아이템이 0개여도 빈 슬롯 용량이 양수일 수 있으므로 슬롯 표시와 문구 판정이 서로 다르다. 실제 사용자가 본 Scene 설정과 화면은 구현 단계에서 재현한다.
3. `GridContentLayoutSettings.CalculateDisplayCapacity`는 VerticalScroll에서 아이템 수와 minimumScrollSlots 중 큰 값을 반환한다. Paged에서 fillPageWithEmptySlots가 켜져 있으면 slotsPerPage를 반환하고, 꺼져 있으면 현재 페이지 아이템 수를 반환한다.
4. `ModalWindowHost`는 닫기 버튼과 Cancel에서 `RequestClose`를 호출하고, `GameWindowService`가 `CloseRequested`를 받아 `Close`를 실행한다. 현재 Host에는 바깥 클릭 전용 처리가 없다.
5. 생성기 `BuildModal`은 모달 루트에 전체 화면 배경 Image를 만들고 그 아래 `Window`에 창 배경 Image를 만든다. 루트에 클릭 처리만 추가하면 내부의 처리되지 않은 클릭 이벤트가 상위로 전달될 위험이 있으므로 내부·외부 영역을 명시적으로 구분해야 한다.
6. `GameWindowService.Close`는 준비 요청 취소, 현재 페이지 Hide, Host 숨김과 포커스 복원을 처리한다. 가방 페이지 Hide에는 컨텍스트 메뉴 닫기 경로가 있다. 새 입력 경로도 이 수명 처리를 재사용한다.

### 관련 경로와 책임

| 경로 | 책임과 수정 관점 |
| --- | --- |
| `Assets/TxTRPG/Application/Runtime/Items/PlayerGameWindowPages.cs` | InventoryGameWindowPage의 표시 갱신·빈 상태·카테고리·페이지·Hide 처리이다. |
| `Assets/TxTRPG/Application/Runtime/Items/InventoryWindowModels.cs` | GridContentLayoutSettings와 표시 용량 계산이다. 기존 슬롯 정책을 유지한다. |
| `Assets/TxTRPG/UI/Runtime/ActionGridPanel.cs` | 실제 표시 슬롯 수와 빈 슬롯 구성, 컨텍스트 메뉴를 확인한다. |
| `Assets/TxTRPG/UI/Runtime/Windows/ModalWindowHost.cs` | 공통 모달 표시와 닫기 요청, 입력 영역 연결이다. |
| `Assets/TxTRPG/UI/Runtime/Windows/GameWindowService.cs` | 준비 취소·페이지 수명·포커스 복원·게임 입력 차단이다. |
| `Assets/TxTRPG/UI/Runtime/Windows/ModalContentContainer.cs` | 본문과 Overlay 영역의 명시적 경계이다. |
| `Assets/TxTRPG/Application/Editor/QuickItemsUiProjectBuilder.cs` | 모달 생성과 저장 자산 연결을 일관되게 유지한다. |
| `Assets/TxTRPG/UI/Prefabs/InventoryWindowPage.prefab` | 빈 상태 문구와 Grid의 저장된 참조를 확인한다. |
| `Assets/TxTRPG/UI/Prefabs/ModalWindowHost.prefab` | 바깥 입력 영역과 Window 영역의 필수 참조를 저장한다. |
| `Assets/TxTRPG/UI/Prefabs/GameMenuScreen.prefab` | 중첩 모달과 관련 override를 확인한다. |
| `Assets/Scenes/AppScene.unity`, `Assets/Scenes/TMP_MainScene.unity` | 정상 시작 경로와 실제 운영 Overlay를 확인한다. |
| `Assets/TxTRPG/Application/Tests/Editor/InventoryProjectionTests.cs` | 기존 표시 설정·투영 정책 회귀 검증의 조사 지점이다. |
| `Assets/TxTRPG/Application/Tests/Editor/QuickItemsUiProjectBuilderTests.cs` | 저장된 필수 참조·자산 구성의 검증 지점이다. |
| `Assets/TxTRPG/UI/Tests/PlayMode/MainSceneMenuInputPlayModeTests.cs` | 실제 공통 모달 입력·닫기·포커스 회귀 검증의 확장 지점이다. |

## 3. Bag 빈 상태 판정

### 표시 슬롯의 정의

현재 카테고리·현재 페이지에 바인딩할 슬롯 수를 기준으로 한다. 실제 아이템 슬롯, 빈 칸 채우기 슬롯과 기존 미등록 아이템의 대체 슬롯도 포함한다. 풀에 남아 있는 비활성 셀, 이전 페이지의 셀, 다른 Grid의 퀵 슬롯과 화면 밖에 보관한 오브젝트는 포함하지 않는다. 스크롤로 잠시 화면 밖에 나간 현재 목록의 슬롯은 여전히 표시 대상으로 센다.

`ActionGridPanel`의 실제 슬롯 수 계산과 같은 기준을 사용한다. 이번 경로에서 capacity가 최종 표시 슬롯 수와 일치하는지 확인한 뒤 사용하며, 필요하면 기존 Grid의 표시 결과 API를 재사용한다. Hierarchy childCount, FindObjectsOfType 또는 매 프레임 셀 개수 세기로 판정하지 않는다. 이 작은 수정만을 위해 별도의 상태 관리 시스템을 만들지 않는다.

| 준비가 완료된 정상 상태 | 빈 문구 |
| --- | --- |
| 실제 아이템 슬롯 1개 이상 | 숨긴다. |
| 아이템 0개, 빈 슬롯 1개 이상 | 숨긴다. |
| 아이템 0개, 표시 슬롯 0개 | 표시한다. |
| 현재 페이지의 슬롯은 있으나 스크롤로 가려짐 | 숨긴다. |

Paged의 마지막 페이지, 필터 변경, 아이템 사용으로 마지막 아이템이 제거되는 경우에도 슬롯 정책을 적용한 뒤 판정한다. fillPageWithEmptySlots가 꺼져 있고 슬롯이 0이면 문구를 유지한다. VerticalScroll에서 minimumScrollSlots가 양수이면 아이템이 없어도 문구를 숨긴다. 기존 페이지 번호 보정과 슬롯 용량을 문구를 숨기기 위해 변경하지 않는다.

### 갱신과 실패 처리

- Grid 바인딩과 같은 갱신 흐름에서 빈 상태를 결정한다. 최초 개방·재개방·카테고리·페이지·표시 모드·수량·설정 변경 후 결과가 일치해야 한다.
- 준비 중 이전 빈 문구가 잠깐 노출되지 않게 한다. 로딩, 정상 빈 상태, 오류를 분리한다. 설정·세션·카탈로그·Grid 참조 오류를 빈 가방으로 위장하지 않는다.
- 빈 상태 오브젝트는 필요할 때만 켜고 raycast로 슬롯 조작을 방해하지 않게 한다. 기존 참조를 삭제하거나 문구를 빈 문자열로 덮어써서 항상 숨기는 방식은 사용하지 않는다.
- 이 수정으로 실제 아이템을 생성하거나 빈 슬롯을 인벤토리 데이터에 추가하지 않는다. 저장 형식, 보유량과 가방 용량 의미는 유지한다.

## 4. 공통 모달 바깥 클릭·탭 닫기

### 영역과 이벤트 소유권

공통 Host에 바깥 클릭 닫기를 기본 활성화한다. Window의 제목·닫기 버튼·본문·빈 공간·로딩·오류·스크롤바를 모두 창 내부로 취급한다. 단순히 콘텐츠 컨테이너나 슬롯 영역 밖이라는 이유로 닫으면 안 된다.

기존 전체 화면 배경을 활용하되 클릭 이벤트의 소유권을 명확히 한다. 예를 들어 Window 뒤의 전용 배경 입력 영역과 Window 내부 입력 차단을 구성하거나, 저장된 Window RectTransform과 올바른 이벤트 카메라로 위치를 판정할 수 있다. 도구 선택은 구현자가 현재 Canvas 구조에 맞춰 결정한다. 루트의 IPointerClickHandler에서 모든 자식 클릭을 바깥 클릭으로 간주하는 구현은 금지한다.

Window 밖으로 펼쳐진, 모달이 소유한 컨텍스트 메뉴·Overlay 컨트롤은 모달 내부 상호작용으로 취급한다. 해당 컨트롤을 누르는 동안 부모 모달이 닫히거나 명령 실행이 취소되지 않게 한다. 기존 컨텍스트 메뉴의 바깥 입력 차단 정책과 충돌하는 경우 실제 raycast 순서를 확인하고, 모달과 소속 보조 UI 모두의 바깥인 클릭은 공통 닫기 요청으로 전달한다. 부모를 닫을 때 보조 UI도 기존 Hide 경로에서 정리한다. 새 모달 스택 시스템은 만들지 않는다.

### 유효한 닫기 입력

- 마우스 주 버튼의 바깥 클릭과 터치의 바깥 탭에서 닫는다. 우클릭·가운데 버튼·스크롤 휠·컨트롤러 포커스 이동을 새 닫기 입력으로 해석하지 않는다.
- 같은 포인터가 바깥에서 누르고 바깥에서 정상적으로 놓은 클릭을 처리한다. 내부에서 누른 뒤 밖에서 놓기, 밖에서 누른 뒤 내부에서 놓기, 스크롤 드래그와 취소된 터치는 닫기 클릭으로 처리하지 않는다.
- 서로 다른 터치의 Down·Up을 합치지 않는다. 숨김·비활성화·취소·재개방에서는 진행 중 포인터 상태를 정리한다. EventSystem의 드래그 임계값과 클릭 판정을 존중한다.
- 닫는 입력은 모달 배경에서 소비한다. 같은 입력으로 뒤의 메뉴·행동·노드·아이템 버튼이 실행되지 않아야 한다. 포인터 Down에서 곧바로 배경을 제거하여 Up 이벤트가 뒤쪽으로 전달되는 구현을 피한다.
- 모달을 여는 클릭이 새 배경에서 곧바로 닫기 클릭으로 처리되지 않아야 한다. 입력을 수동으로 뒤쪽 오브젝트에 재전달하지 않는다.
- EventSystem과 Input System의 기존 포인터 이벤트를 사용한다. Update에서 Mouse.current나 터치를 전역 폴링하거나 화면 좌표만 하드코딩하지 않는다.

### 닫기 수명

바깥 클릭은 `ModalWindowHost.RequestClose` → `GameWindowService.Close`와 같은 기존 단일 닫기 경로로 보낸다. 직접 Destroy, GameObject.SetActive(false), CanvasGroup 알파 변경만 실행하는 별도 닫기 경로를 만들지 않는다.

준비 중·오류 상태에서도 바깥 닫기를 허용한다. 진행 중 요청을 취소하고 늦은 완료가 창을 다시 표시하지 못하게 한다. 닫기와 재시도·새 개방 요청이 겹쳐도 오래된 이벤트가 새 창을 닫거나 이전 페이지를 다시 표시하지 않아야 한다. 중복 이벤트에 안전하게 처리하며 숨겨진 Host는 입력을 받지 않는다.

기존 게임 입력 차단과 닫은 뒤 유효한 호출 버튼으로 포커스를 복원하는 동작을 유지한다. 복귀 대상이 소멸·비활성인 경우 예외 없이 기존 대체 정책을 따른다. 구독은 명시적으로 해제하고 반복 개방에서 리스너를 누적하지 않는다. 이미 확정된 아이템 소비나 전투 결과는 창 닫기로 되돌리거나 다시 실행하지 않는다.

## 5. 적용 범위·호환성·작업 순서

1. 정상 AppScene 경로에서 Bag을 열어 현재 카테고리·표시 모드·빈 칸 설정·capacity·문구 활성 상태를 기록하여 재현한다. 실제 Window 경계와 배경 raycast를 확인한다.
2. 최소 수정으로 빈 상태를 최종 표시 슬롯 기준으로 바꾼다. 기존 필터·페이지·스크롤·선택 유지 계약을 보존한다.
3. Host의 외부 입력 경로와 필수 영역 참조를 구현하고 기존 서비스 닫기를 연결한다.
4. Unity 연결·컴파일·Play Mode·미저장 변경을 확인한 뒤 필요한 Prefab과 Scene 연결만 선별 적용한다. 생성기도 이후 생성 결과가 같은 기본 동작을 갖도록 갱신한다.
5. 저장된 자산을 다시 로드하여 참조·raycast·형제 순서·CanvasGroup 상태를 확인하고 실제 입력을 검증한다.

전체 모달이나 Scene 재생성은 기본 적용 방법이 아니다. 현재 배치, 메뉴 아이콘, 사용자 override, Grid 설정과 기존 작업을 보존한다. 별도 플랫폼 코드·새 패키지·저장 마이그레이션은 필요하지 않으며, 후속 강제 확인 모달이나 새 창 관리 시스템은 이번 범위 밖이다.

새 설정을 추가한다면 기존 자산에도 바깥 닫기 기본 활성화가 명시적으로 적용되어야 한다. C# 초기값 변경만으로 기존 저장 자산이 갱신되었다고 가정하지 않는다. 사용자 정의 참조를 전역 검색으로 조용히 대체하지 않는다.

자동 적용이 불가능하면 정확한 원인, 완료한 코드·자산 작업과 남은 적용·검증을 보고한다. 필요하면 좁은 범위의 임시 적용 메뉴를 만들되 실제 메뉴 경로·변경 대상·저장 방식·반복 실행 안전성과 오류 시 중단 절차를 남긴다. 도구 제공만으로 적용 완료라고 보고하지 않는다.

## 6. 검증 기준

### 빈 상태

- VerticalScroll에서 아이템 0개와 minimumScrollSlots 0/1/12, 아이템 1개 이상을 각각 검사한다.
- Paged에서 아이템 0개, 1개, 페이지 용량 K개, K+1개를 검사한다. fillPageWithEmptySlots의 켜짐·꺼짐, 마지막 페이지와 마지막 아이템 제거를 포함한다.
- 카테고리 변경, 표시 모드 변경, 재개방, 미등록 아이템 대체 슬롯, 스크롤로 가려진 슬롯을 검증한다. 판정 때문에 데이터·페이지·선택이 변하면 안 된다.
- 준비 중과 오류 상태에서 정상 빈 문구가 겹치지 않고, 필수 오류·재시도가 유지되는지 확인한다.

### 모달 입력과 수명

- Bag·System·Status 각각에서 바깥 클릭·터치로 한 번 닫히고, 내부 배경·빈 슬롯·제목·탭·페이지 버튼·스크롤바·설정 Toggle 조작으로 닫히지 않아야 한다.
- 내부→외부 및 외부→내부 드래그, 외부 드래그, 우클릭·휠, 다중 터치·포인터 취소와 빠른 반복 개방을 검사한다.
- 소속 컨텍스트 메뉴 선택은 정상 실행되고, 모든 모달 영역 밖의 입력은 부모 닫기로 이어져 보조 UI도 정리되는지 확인한다.
- 닫는 클릭 아래에 메뉴·행동 버튼이 있어도 실행 횟수가 증가하지 않아야 한다. 닫기 버튼과 Cancel도 기존대로 동작하고 포커스가 복원되어야 한다.
- 느린 준비 중 닫기, 오류에서 닫기, 재시도와 닫기 경합, 닫은 직후 다른 창 열기를 검사한다. 늦은 완료의 재표시·이전 요청의 새 창 종료·구독 누적이 없어야 한다.
- 키보드·컨트롤러·마우스·터치와 좁은 화면·휴대폰·태블릿·데스크톱·울트라와이드, safe area·방향 변경에서 Window 경계와 바깥 영역을 확인한다. 실제 기기 검증과 Editor 시뮬레이션을 구분한다.

표시 슬롯 판정과 저장 자산 무결성은 집중 Edit Mode 테스트로, 실제 raycast·포인터·클릭 관통·비동기 수명은 Play Mode 테스트로 검증한다. 핸들러를 직접 호출하는 테스트만으로 실제 입력 전달을 검증했다고 주장하지 않는다. 필요한 대상 빌드의 컴파일은 도구가 준비된 범위에서 수행하고 미검증 대상과 이유를 보고한다. Unity 직렬화 diff의 무관한 변경을 확인한다.

## 7. 문서 갱신과 최종 보고

구현 결과에 맞춰 `DOCS/architecture/quick-items-and-game-windows.md`와 `DOCS/development/workflows.md`에 빈 상태 규칙, 바깥 입력 영역, 단일 닫기 경로·포커스·적용 및 검증 절차를 반영한다. 공통 Grid 계약을 바꾼 경우 `DOCS/architecture/action-grid-panel.md`, 주요 책임 경계를 바꾼 경우 `DOCS/architecture/project-structure.md`를 함께 갱신한다. 새 지침서는 `DOCS/README.md`와 `DOCS/instructions/README.md`에서 연결한다.

최종 보고에는 재현한 조건, 수정한 판정식과 입력 경계, 실제 적용한 Prefab·Scene, 수행한 테스트·입력 검증과 미검증 범위를 포함한다. 구현 완료·Editor 적용 완료·미적용 작업·미검증 항목을 분리한다. 사용자 실행 Tools 단계가 남으면 별도 `사용자가 수행할 적용 절차`에 정확한 메뉴 경로·순서·전제·저장·재실행·오류 중단·최종 검증을 fenced text 블록으로 제공하고 개발 문서에도 기록한다. 남은 수동 Tools 단계가 없으면 없다고 명시한다.
