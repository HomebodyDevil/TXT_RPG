# StoryTextPanel 레이아웃 콜백 NullReferenceException 수정

## 목적과 범위

StoryTextPanel.RebuildAndRefresh에서 발생하는 NullReferenceException의 실제 원인을 확인하고 초기화·레이아웃 갱신·비활성화·파괴 과정에서 안전하게 동작하도록 수정한다. 코드뿐 아니라 확인된 누락 자산 참조와 관련 테스트·문서까지 반영한다. UI 디자인, 스크롤 정책, 메시지 내용이나 다른 패널의 기능은 임의 변경하지 않는다.

## 보고된 오류와 확인된 코드

```text
StoryTextPanel.RebuildAndRefresh() :563
StoryTextPanel.OnRectTransformDimensionsChange() :331
UnityEngine.UI.LayoutGroup.SetChildAlongAxis(...)
FlexibleContentLayoutGroup.ArrangeAxis(...) :154
FlexibleContentLayoutGroup.SetLayoutHorizontal() :76
Canvas.ForceUpdateCanvases()
PanelStartupController.InitializeAsync() :100
EnemyDisplayPanelDemoLoader.LoadAndApplyAsync() :32
```

사용자가 제공한 스택은 뒷부분이 잘려 있다. 구현 시작 시 전체 오류와 실제 발생 Scene·오브젝트를 확인한다.

작성 시점의 `Assets/TxTRPG/UI/Runtime/StoryTextPanel.cs`에서 확인한 사실은 다음과 같다.

- 563행은 `SynchronizeFromScrollRect(scrollRect.normalizedPosition)`이다. 현재 코드 기준으로 scrollRect를 직접 역참조한다.
- OnRectTransformDimensionsChange는 isActiveAndEnabled만 확인하고 RebuildAndRefresh를 호출한다. 활성 상태가 필수 참조 준비 완료를 보장하지는 않는다.
- RebuildLayout은 Canvas.ForceUpdateCanvases, ForceRebuildLayoutImmediate(content), Canvas.ForceUpdateCanvases를 연속 호출한다.
- HasRequiredReferences와 OnEnable의 검사만으로 다른 콜백과 지연 실행 경로까지 보호되지는 않는다.

참조 누락·생성 중 미할당·파괴된 Unity 객체 중 어느 경우인지 아직 확정하지 않았다. 레이아웃 콜백 안의 강제 Canvas 갱신은 재진입 위험이지만 이번 예외가 반드시 재진입 때문에 발생했다고 단정하지 않는다. EnemyDisplayPanelDemoLoader는 스택의 상위 호출자일 뿐 원인으로 확정하지 않는다.

## 사전 조사 및 영향 경로

AGENTS.md, DOCS/README.md, DOCS/architecture/story-text-panel.md, DOCS/architecture/panel-startup.md, DOCS/architecture/flexible-layout-panel.md와 DOCS/development/workflows.md를 읽는다. CodeGraph로 호출자·비동기 완료·변경 영향 범위를 먼저 탐색하고 실제 소스로 검증한다.

| 경로 | 확인 범위 |
| --- | --- |
| `Assets/TxTRPG/UI/Runtime/StoryTextPanel.cs` | 콜백, 갱신 메서드, 코루틴, 이벤트 연결과 해제다. |
| `Assets/TxTRPG/UI/Runtime/FlexibleContentLayoutGroup.cs` | 부모 레이아웃이 크기 변경을 발생시키는 경로다. |
| `Assets/TxTRPG/UI/Runtime/PanelStartupController.cs` | 초기 데이터 완료 후 Layout 및 Reveal 준비 경로다. |
| `Assets/TxTRPG/UI/Runtime/EnemyDisplayPanelDemoLoader.cs` | 스택의 비동기 완료 경로다. 필요성을 확인한 경우에만 수정한다. |
| `Assets/TxTRPG/UI/Editor/StoryTextPanelPrefabBuilder.cs` | 참조 설정·활성화 순서와 재생성 호환성을 확인한다. |
| `Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab` | 필수 직렬화 참조를 검사한다. |
| `Assets/Scenes/TMP_MainScene.unity` | Scene Override·중복 패널·실제 오류 발생 인스턴스를 확인한다. |
| `Assets/TxTRPG/UI/Tests/Editor/StoryTextPanelTests.cs` | 정상 동작과 필수 참조 검증 회귀 테스트다. |
| `Assets/TxTRPG/UI/Tests/Editor/StoryTextPanelEditModePreviewTests.cs` | Editor 미리보기 회귀 테스트다. |
| `Assets/TxTRPG/UI/Tests/Editor/PanelStartupTests.cs` | 준비 완료·취소·실패 경로 회귀 테스트다. |

## 진단 절차

1. Editor 컴파일 상태·미저장 변경을 확인하고 오류를 재현한다. 최초 발생과 반복 발생을 구분한다.
2. 오류 인스턴스의 Scene·Hierarchy 경로, 필수 참조, 활성 상태, 초기화 단계와 파괴 여부를 확인한다. Prefab 원본과 인스턴스 Override를 비교한다.
3. scrollRect가 할당되지 않았는지, 런타임 변경으로 끊겼는지, Unity 객체가 파괴되었는지 구분한다. 생성·바인딩·해제 코드를 추적한다.
4. 강제 Canvas 갱신 전후에 콜백이 재진입하는지 확인한다. 진단은 제한적으로 수행하고 매 프레임 로그를 남기지 않는다.
5. 초기화 중 일시적인 미준비 상태와 영구적인 잘못된 Prefab 구성을 구분한다. OnEnable 경고 후 객체가 계속 활성 상태인지도 확인한다.

## 수정 요구사항

### 준비 상태와 참조 안전성

- Unity 생명주기 콜백은 완전한 구성 이전이나 해제 도중에도 호출될 수 있다는 전제로 처리한다. 실제 필요한 참조와 실행 상태를 검사한다.
- RebuildAndRefresh뿐 아니라 RebuildLayout, 스크롤 동기화, 최초 Reveal, ScrollToBottomAfterLayout 등 같은 참조에 접근하는 연관 경로를 확인한다. 하나의 null 조건만 추가하고 조사를 끝내지 않는다.
- yield 또는 강제 갱신 이후 참조가 계속 유효하다고 가정하지 않는다. Unity 객체의 파괴 상태를 반영하는 검사와 취소 처리를 사용한다.
- 일시적으로 준비되지 않았다면 갱신을 보류하고, 유효한 참조가 연결된 후 실제 갱신이 재개되어야 한다. 영구 누락은 대상과 누락 필드를 명확하게 진단한다. 매 프레임 같은 경고를 반복하지 않는다.
- 자동 Scene 검색이나 임의 ScrollRect 생성으로 잘못된 직렬화 구성을 숨기지 않는다. 필수 참조 누락이 확인되면 원래 구성에 맞게 자산과 생성 경로를 함께 수정한다.

### 레이아웃 재진입과 지연 갱신

- 크기 변경 콜백에서 Canvas 전체 강제 갱신을 재귀적으로 요청하지 않도록 한다. 변경 요청을 dirty 상태로 모으고 안전한 기존 갱신 단계에서 처리하는 등 최소한의 방법을 선택한다.
- 재진입 방지 플래그를 사용한다면 예외·취소에도 해제되고 처리 중 추가된 변경 요청이 영구 유실되지 않아야 한다.
- 정상 상태에서 매 프레임 전체 Layout을 다시 만들지 않는다. 필요할 때만 제한적으로 갱신하며 동일 프레임의 중복 요청은 병합한다.
- PanelStartupController가 패널 준비 완료를 기다릴 때 지연된 레이아웃도 반영되어야 한다. 표시와 입력을 너무 일찍 허용하거나 상호 대기로 영원히 준비되지 않는 상태를 만들지 않는다.
- Script Execution Order 변경, 임의 시간 지연, 빈 catch, 로그 비활성화, 적 DemoLoader 삭제를 기본 해결책으로 사용하지 않는다.

### 수명과 기존 동작 보존

- 비활성화·파괴 시 예약 갱신과 코루틴을 취소하고 이벤트를 안전하게 해제한다. 재활성화하면 필요한 갱신이 재개되며 중복 구독되지 않아야 한다.
- 메시지 추가·초기화, 최신 메시지 추적, 수동 스크롤, ScrollToOldest/Latest, 오래된 메시지 투명도, 초기 Reveal, 한국어 fallback과 Editor 미리보기를 유지한다.
- 레이아웃 처리를 바꾸면서 기존 메시지를 Clear하거나 설정을 기본값으로 덮어쓰지 않는다. 스크롤 보존 정책을 임의 변경하지 않는다.

## 검증 기준

1. 참조가 없는 활성 컴포넌트의 크기를 바꾸어도 처리되지 않은 예외가 발생하지 않아야 한다. 진단은 명확하고 반복되지 않아야 한다.
2. 참조 연결 전에 크기가 변하고, 이후 정상 구성되는 경우 실제 메시지·스크롤 갱신이 복구되는지 확인한다.
3. 유효한 Prefab을 FlexibleLayout 부모 아래에서 반복 resize하고 Canvas.ForceUpdateCanvases를 호출하여 재귀·무한 갱신·Layout 경고가 없는지 확인한다.
4. 지연 갱신·초기 로드 중 비활성화·파괴·Scene 언로드를 수행해 예외·중복 이벤트·잔여 코루틴이 없는지 확인한다.
5. 테스트는 계산 메서드 호출만으로 끝내지 않고 실제 RectTransform 변경과 프레임 경계를 포함한다. 필요한 Play Mode 테스트를 `Assets/TxTRPG/UI/Tests/PlayMode/`에 추가한다.
6. TMP_MainScene의 실제 AppScene 초기화 경로와 보고된 DemoLoader 경로를 확인한다. 불필요하게 시작 Scene 정책을 변경하지 않는다.
7. 메시지 0개·다수, Follow Latest 켜짐/꺼짐, 스크롤 허용/비허용, 최초 Reveal 켜짐/꺼짐과 Editor 미리보기를 회귀 확인한다.
8. 기존 관련 Edit Mode 테스트, 신규 Play Mode 테스트와 컴파일을 수행한다. 오류가 사라지는 것뿐 아니라 레이아웃과 입력이 정상적으로 준비되는지 확인한다.

## 적용·문서·최종 보고

필요한 코드와 확인된 자산만 수정하고 Scene 배치·사용자 Override를 보존한다. 전체 Prefab 재생성을 기본 적용 방법으로 사용하지 않는다.

`DOCS/architecture/story-text-panel.md`의 초기화 안전성 설명을 실제 콜백 보호 범위에 맞게 수정한다. PanelStartup의 준비 계약이 바뀌면 관련 문서도 함께 수정한다. `DOCS/development/workflows.md`에 재현·검증 절차를 남긴다.

최종 보고에는 확인된 직접 원인과 추가 위험, 실제 오류 오브젝트, 수정 경로, 자산 적용 여부, 테스트 결과·미검증 사항을 구분하여 기록한다. 수동 Tools 단계가 남는다면 AGENTS.md에 따라 별도 `사용자가 수행할 적용 절차`에 전체 메뉴 경로·전제 조건·실행 순서·저장·성공 확인을 코드 블록으로 제공한다. 코드 작성만으로 적용·검증 완료라고 보고하지 않는다.
