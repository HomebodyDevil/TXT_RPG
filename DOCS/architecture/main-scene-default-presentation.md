# TMP_MainScene 기본 표시와 Preview

## 목적

`MainScenePresentationController`는 기존 `TMP_MainScene`의 레이아웃과 패널 인스턴스를 유지하면서 운영 데이터의 누락·빈 상태·로드 실패를 명시적으로 표시합니다. UI가 새 `PlayerState`나 적, 아이템을 만들지 않으며 배포용 대체 표시는 저장 데이터에 포함되지 않습니다.

## 상태 계약

| 상태 | Story | Action Grid | Enemy |
| --- | --- | --- | --- |
| `Loading` | 기존 정상 표시를 유지하고 입력 가능한 가짜 데이터를 만들지 않습니다. | 실제 Presenter의 준비를 기다립니다. | 기존 정상 표시를 유지합니다. |
| `ReadyWithData` | 전달된 메시지로 대체합니다. | 실제 슬롯과 수량을 표시합니다. | 전달된 적을 표시합니다. |
| `ReadyEmpty` | 빈 기록을 정상 상태로 유지합니다. | 기존 Capacity의 빈 슬롯을 유지합니다. | 적을 만들지 않고 비웁니다. |
| `MissingConfiguration` | Profile의 중립 안내를 임시 표시합니다. | 빈 슬롯을 유지합니다. | 빈 상태를 유지합니다. |
| `Failed` | 실패 안내를 한 번 표시하고 오류 코드를 한 번 기록합니다. | 실제 상태를 임의 변경하지 않습니다. | 이미 표시된 정상 적을 유지합니다. |

Story 대체 문구는 Controller가 소유권을 기록하므로 재활성화해도 중복 추가되지 않습니다. 이후 `ReadyWithData`가 도착하면 정상 메시지로 교체하고, `ReadyEmpty`가 도착하면 Controller가 추가했던 문구만 제거합니다.

## 자산과 적용 주체

| 자산 | 용도 |
| --- | --- |
| `Assets/TxTRPG/Application/Configuration/MainScenePresentation/MainSceneDefaultPresentationProfile.asset` | 배포용 안내 문구, 알 수 없는 체력 문구와 로컬 배경 Style 참조입니다. |
| `MainSceneEnemyFallbackStyle.asset` | DEMO 폴더와 분리한 적 패널의 로컬 배경 대체 Style입니다. |
| `Preview/*.asset` | Normal, MissingConfiguration, ReadyEmpty, PartialAssetFailure, DelayedRecovery, InitializationFailure 시나리오입니다. |
| `Assets/TxTRPG/UI/Prefabs/MainScenePreviewHarness.prefab` | 명시적으로 배치하고 Target을 연결할 때만 Preview를 실행하는 격리된 도구입니다. |

Preview Harness는 `PlayerSessionHost`와 일반 저장소를 참조하지 않습니다. Story와 Enemy의 기존 DemoData를 읽기 전용 표현 데이터로만 사용하고, Action Grid에 아이템을 지급하거나 저장하지 않습니다.

## Scene 보존

생성기는 `Main_FlexibleLayoutPanel`, 자식 순서, RectTransform, FlexibleLayoutItem, 배경·Border와 Prefab 연결을 재생성하지 않습니다. `EnemyDisplayPanelDemo` 컨테이너는 같은 Transform과 자식을 유지한 채 이름만 `EnemyDisplayPanelContainer`로 변경하고, 운영에서 샘플 적을 쓰던 `EnemyDisplayPanelDemoLoader`, `PanelStartupController`, `FadePanelRevealTransition`만 제거합니다. `MainScenePresentationController`는 기존 최상위 FlexibleLayoutPanel에 추가됩니다.

배경 Addressables 로드가 실패하면 `PanelBackgroundRenderer`가 Profile에 포함된 로컬 Sprite 또는 Tint를 사용합니다. 선택적 장식 실패는 Scene 준비를 영구적으로 차단하지 않으며, 필수 PlayerSession 실패는 이 Controller가 새 게임으로 대체하지 않습니다.
