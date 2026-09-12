# 등록 기반 Prefab Rebuild

## 책임

| 구성 요소 | 책임 |
| --- | --- |
| `PrefabRebuildTaskDescriptor` | 안정적인 작업 ID, 분류, 입력·출력 경로, 의존성, 실행 및 검증을 선언합니다. |
| `IPrefabRebuildTaskProvider` | 각 Editor 모듈이 자신이 소유한 안전한 작업과 제외 사유를 명시적으로 제공합니다. |
| `PrefabRebuildRegistry` | `TypeCache`로 Provider를 수집하고 중복 ID, 잘못된 경로, 출력 충돌, 누락·순환 의존성을 검사합니다. |
| `PrefabRebuildRunner` | Editor 상태와 입력을 사전 검사하고 확정된 계획을 순차 실행하며 첫 실패나 취소에서 중단합니다. |
| `PrefabRebuildWindow` | 작업 선택, 계획 확인, 덮어쓰기 확인과 결과 보고를 제공합니다. |

레지스트리는 MenuItem이나 `.prefab` 파일을 검색하여 임의로 실행하지 않습니다. 출력은 `Assets/TxTRPG/` 아래의 선언된 경로만 허용하며, 같은 출력은 한 작업만 소유할 수 있습니다. 의존 작업은 ID 순으로 안정적으로 정렬되고 한 번만 실행됩니다.

## 현재 기본 포함 작업

| 작업 ID | 출력 |
| --- | --- |
| `ui.action-grid` | `ActionGridCell.prefab`, `ActionContextMenu.prefab`, `ActionGridPanel.prefab` |
| `ui.character-display` | `HealthBarPanel.prefab`, `CharacterStatusPanel.prefab`, `CharacterDisplayPanel.prefab` |
| `ui.enemy-display` | `EnemyDisplayPanel.prefab` |
| `ui.flexible-layout` | `FlexibleLayoutPlaceholder.prefab`, `FlexibleLayoutPanel.prefab` |

StoryTextPanel은 기존 Style을 기본값으로 다시 쓰기 때문에 제외합니다. Demo/Sample, Game Menu의 현재 복합 생성, 기본 콘텐츠, AppScene, Scene Transition, Addressables, 수작업 Prefab과 Variant도 Scene·콘텐츠·설정 변경 또는 출력 경계 혼합 때문에 제외합니다.

## 안전 계약

실행 전에는 Play Mode, 컴파일·가져오기, 다른 실행, Prefab Mode와 미저장 Scene을 검사합니다. 하나라도 충돌하면 어떤 작업도 시작하지 않습니다. 실행 도중에는 안전한 작업 경계에서만 취소하며, 첫 실패 후 남은 작업은 실행하지 않습니다. 일괄 실행은 트랜잭션이 아니므로 실행 전 버전 관리 체크포인트가 필요합니다.

각 작업은 생성 직후 저장된 Prefab을 다시 로드하여 존재 여부와 Missing Script를 검사합니다. 생성기 자체의 필수 참조 테스트는 기존 기능별 테스트가 계속 담당합니다.
