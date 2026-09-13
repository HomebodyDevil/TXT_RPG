# 점진 생성 탐험 노드 트리

## 책임과 수명

`ExplorationRunState`는 Unity UI에 의존하지 않는 탐험 원본입니다. 가상 Root, 현재 후보 집합, 활성 노드, 마지막 완료 노드, 선택 경로와 모든 선택·미선택 기록을 보존합니다. 후보는 선택한 노드를 완료한 뒤에만 생성되므로 미래 트리를 미리 만들지 않습니다.

`PlayerSessionHost`는 앱 실행 동안 임시 탐험 상태와 진행 중 임시 전투를 보관합니다. 운영 `PlayerState`와 저장 DTO에는 탐험 체력이나 트리를 기록하지 않습니다. 콘텐츠 Scene이 다시 바인딩되면 `ExplorationRunController`는 기존 탐험과 전투를 다시 표시하며 새 후보를 추첨하지 않습니다.

| 타입 | 책임 |
| --- | --- |
| `ExplorationNodeGenerator` | 선택지 수와 종류별 정수 가중치로 후보 종류를 독립 추첨합니다. |
| `ExplorationRunState` | 노드 ID, 부모, 형제 순서, 깊이, 상태, 완료 이유와 탐험 체력을 관리합니다. |
| `ExplorationRunConfiguration` | 기본 선택지 3개와 전투/회복·강화 50/50 가중치를 제공합니다. |
| `ExplorationRunController` | 선택 UI, 처리기 연결, Story 기록, 포커스와 Scene 재바인딩을 담당합니다. |
| `TemporaryDiceRollMenuController` | 전투 노드에서만 행동을 허용하고 탐험의 동일한 `HealthState`를 사용합니다. |

## 상태 전환과 기록

```text
Root(Completed)
└─ Choice Set 1
   ├─ 선택 노드: Available → Active → Completed/Failed
   └─ 형제 노드: Available → Unchosen
      └─ 자식 없음

선택 노드 완료
└─ Choice Set 2를 선택 노드의 자식으로 생성
```

후보 집합 ID가 다른 선택 요청, 활성 Node ID가 다른 완료 요청과 중복 완료는 거부합니다. 미선택 노드는 방문할 수 없으며 실패나 완료로 취급하지 않습니다. 회복·강화 노드는 `PlaceholderAcknowledged`, 전투 승리는 `CombatVictory`, 패배는 `CombatDefeat` 이유를 기록합니다.

예를 들어 세 번 진행하면 선택 경로는 `node-1 → node-4 → node-7`처럼 부모 연결을 유지합니다. 각 선택 집합에서 선택하지 않은 두 형제는 `Unchosen`으로 남으며 자식을 갖지 않습니다.

## 체력과 노드 처리

탐험을 처음 생성할 때 운영 캐릭터의 현재·최대 체력을 새 `HealthState`에 한 번 복사합니다. 모든 전투 노드는 이 인스턴스를 직접 사용하므로 피해와 회복이 다음 노드까지 이어집니다. 회복·강화 미구현 노드는 체력, 스탯과 주사위를 변경하지 않습니다.

전투 노드를 선택하면 임시 적을 생성하고 기존 주사위 전투 규칙을 실행합니다. 승리 시 현재 노드를 완료한 뒤 다음 후보를 생성하고, 패배 시 탐험을 실패 상태로 고정합니다. 선택 대기와 미구현 안내 중에는 `행동` 명령을 실행할 수 없습니다.

## 현재 범위

현재는 전투와 효과 없는 회복·강화 처리기만 등록되어 있습니다. 전체 트리 시각화, 되돌아가기, 최대 깊이, 엔딩, 보상, 실제 강화 효과와 디스크 저장·이어하기는 구현하지 않았습니다.

![탐험 노드 선택 실행 화면](../images/exploration-node-selection-runtime.png)