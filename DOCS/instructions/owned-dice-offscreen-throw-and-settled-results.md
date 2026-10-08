# 보유 주사위 화면 밖 던지기와 정지 후 결과 표시

## 1. 목적과 작업 범위

현재 `OwnedDicePanel`의 던지기는 카메라에 보이는 영역 안쪽, 천장 바로 아래에서 시작한다. 재생 첫 0.16초(`liftDuration`) 동안 주사위가 정지 자세에서 출발 자세로 화면 안에서 들어 올려지고, 이어서 기록한 궤적을 재생한다. 이 작업은 두 가지를 바꾼다.

1. 굴림을 시작하면 주사위가 주사위 표시 영역(카메라 시야) 밖에서 영역 안으로 던져져 들어오는 것처럼 보이게 한다.
2. 모든 주사위가 완전히 멈춘 뒤에만 결과(Story 기록, HP 변화, 결과 창)를 보여준다. 시간 초과와 실패 경로에서도 주사위가 결과 자세로 정지한 뒤에 결과를 적용한다.

결과 판정 방식, 결과 보정 회전, 결과 적용 1회 보장은 [보유 주사위 던지기 연출](owned-dice-physics-throw.md)에서 구현한 구조를 그대로 유지한다.

이 문서는 구현 지침이다. 아래의 신규 설정, 메서드, 동작은 예정된 구조이며 아직 구현되었다고 간주하지 않는다. 이 문서를 작성하는 단계에서는 Scene, Prefab, 런타임 코드를 수정하지 않았다.

포함 범위는 다음과 같다.

- 화면 아래쪽(카메라 쪽) 밖에서 영역 안으로 들어오는 던지기 시뮬레이션과 재생
- 굴림 시작 시 이전 결과 주사위를 화면 밖으로 회수하는 연출
- 모든 주사위가 정지한 뒤 짧게 대기하고 결과를 적용하는 순서
- 연출 길이에 맞춘 시간 제한 계산과, 시간 초과·실패 시 "정지 배치 후 결과 적용" 순서
- 관련 테스트, 검증 메뉴, 문서 갱신

제외 범위는 다음과 같다.

- 결과 판정 방식 변경. 판정은 계속 연출 전에 `TemporaryPlayerDiceState.RollAll`이 한 번 정한다.
- 아래쪽 이외의 진입 방향(좌·우·무작위 방향)
- 주사위 표시 영역 안의 결과 강조 표시(배지, 숫자 오버레이 등) 같은 새 UI
- 효과음, 진동, 파티클, 카메라 흔들림
- 드래그·플릭 같은 새 던지기 입력
- 보유 주사위·연출 상태의 영구 저장

## 2. 사용자 확정 사항

| 항목 | 확정 요구 |
| --- | --- |
| 진입 방향 | 화면 아래쪽, 즉 카메라 쪽 화면 밖에서 먼 쪽으로 던진다. 현재 던지기 방향(+Z)을 그대로 잇는다. 굴림마다 방향을 바꾸지 않는다. |
| 이전 주사위 처리 | 굴림 시작 시 바닥에 놓인 주사위는 짧은 시간 동안 진입 방향 쪽(화면 아래쪽) 화면 밖으로 빠르게 이동한 뒤, 다시 던져져 들어온다. 사라지기·축소·위로 들어 올리기 방식은 쓰지 않는다. |
| 결과 표시 시점 | 모든 주사위가 완전히 멈춘 뒤 짧은 대기 시간이 지나면 결과를 적용한다. 대기 시간은 Inspector에서 조정하며 기본값은 0.3~0.5초 범위에서 측정해 정한다. |
| 시간 제한 | 고정 3초 제한이 정상 연출을 끊지 않도록, 시간 제한을 "계산된 최대 연출 길이 + 여유"로 정한다. 실제로 시간 제한을 넘긴 경우에만 주사위를 결과 자세로 먼저 정지·배치한 뒤 결과를 보여준다. |

## 3. 선행 문서와 확인한 현재 구조

작업 전 `AGENTS.md`, `DOCS/README.md`와 아래 문서를 읽는다.

- `DOCS/architecture/owned-dice-presentation.md`
- `DOCS/architecture/temporary-combat.md`
- `DOCS/development/workflows.md`의 "보유 주사위 3D 패널과 결과 기록"
- `DOCS/instructions/owned-dice-physics-throw.md`

문서 작성 시 저장된 소스와 자산에서 다음을 확인했다. 구현 직전 현재 작업 트리를 다시 확인하고, 다르면 실제 상태를 우선한다.

- `TemporaryDiceRollMenuController.CompleteAcceptedActionAsync`는 `AnimateOrFallbackAsync`가 끝나면 같은 흐름에서 `ExecuteTurn`, `TemporaryDiceRollHistory.AddTurn`, `PublishTemporaryDiceStory`, HP 표시 갱신, `QueueTemporaryDiceResult`를 차례로 호출한다. 따라서 결과 표시 시점은 `PlayRollAsync`가 끝나는 시점에 묶여 있다.
- `AnimateOrFallbackAsync`는 `PlayRollAsync`와 `Task.Delay(rollTimeoutSeconds)`를 경쟁시킨다. `rollTimeoutSeconds`는 1~15초로 제한되며 `Assets/Scenes/TMP_MainScene.unity`에 3으로 저장되어 있다. 시간 초과가 먼저 끝나면 토큰을 취소하고 `ShowRollFallback`을 호출한 뒤 바로 반환한다. 이때 `PlayRollAsync`는 다음 `Task.Yield` 이후에야 취소를 관찰하고 `finally`에서 정돈 배치를 하므로, 결과 적용이 주사위 정지보다 먼저 일어난다.
- `OwnedDicePanel.MaximumPresentationSeconds`는 `DiceThrowSettings.MaximumPresentationSeconds`(`LiftDuration + MaximumSimulationSeconds / PlaybackSpeed`, 기본 약 2.16초)를 반환한다. 현재 이 값을 호출하는 코드는 없다.
- `OwnedDicePanel.PlayThrowAsync`는 `liftDuration` 동안 정지 자세에서 `Trajectory.Sample(i, 0f)` 출발 자세로 이동하면서 Body의 보정 회전을 이전 값에서 새 S로 섞는다. 출발 자세는 화면 안이므로 이 과정이 모두 보인다.
- `DiceThrowSimulator.TryRun`은 `DiceArenaLayout.ThrowStartPositions`로 카메라 쪽 벽(`InnerNearZ`)부터 출발 위치를 채우고, 높이 `Ceiling - DieRadius * 1.05`(최소 `RestHeight + DieRadius * 0.2`)에서 +Z 방향 속도로 던진다. 바닥, 천장, 네 벽은 `BuildBounds`가 처음부터 모두 만든다.
- 정지 판정은 모든 주사위의 선속도·각속도가 기준 이하로 `settleHoldSeconds` 동안 유지될 때이며, 궤적은 첫 정지 프레임에서 끝나고 그 프레임 자세를 유지 구간 끝의 최종 자세로 바꾼다.
- `DiceArenaLayout.Compute`는 벽·천장 상자 전체가 직교 카메라 화면 안에 들도록 크기를 정한다. 카메라는 원점을 바라보며 `cameraPitch`(기본 55°)만큼 아래로 기울어져 있다. 화면 아래 가장자리는 바닥 높이에서 대략 `NearZ - padding / sin(pitch)` 위치에 해당한다.
- `PlayRollAsync`의 `finally`는 `FinishRollLayout`을 호출하고, 이 메서드는 `Compose`를 실행한다. `Compose`는 정지한 주사위를 범위 안으로 옮긴 뒤 `DiceArenaLayout.HasOverlap(clamped, radius * 1.5f)`이 참이면 정돈 배치로 다시 놓는다. 물리적으로 맞닿아 정지한 주사위 사이 거리는 `radius * 1.5`보다 짧을 수 있으므로, 정상 던지기 직후에도 주사위가 정돈 배치로 순간 이동할 가능성이 있다. 구현 전에 실제 재현 여부를 측정한다.
- `OwnedDiceIntegrationTests`가 `DiceArenaLayout.ThrowStartPositions`를 직접 사용한다.
- 검증 메뉴는 `Tools/TxT RPG/UI/Dice/Temporary/Validate Throw Presentation`(`OwnedDiceThrowValidation`)과 `Tools/TxT RPG/UI/Dice/Temporary/Validate Play Flow`(`TemporaryOwnedDiceIntegration`)이다. Play Mode 테스트는 `Assets/TxTRPG/UI/Tests/PlayMode/DiceThrowSimulationPlayModeTests.cs`에 있다.

CodeGraph로 `OwnedDicePanel`, `DiceThrowSimulator`, `DiceArenaLayout`, `DiceThrowSettings`, `TemporaryDiceRollMenuController`, `OwnedDiceThrowValidation`의 호출자와 테스트를 먼저 탐색하고, 실제 소스와 직렬화 자산으로 확인한다.

## 4. 연출 순서

```text
행동 수락 및 RollAll로 결과 확정 (기존)
→ OwnedDicePanel.PlayRollAsync가 결과 목록을 받음
→ 화면 밖 출발 위치로 던지기 시뮬레이션 (진입 통로 포함), 보정 회전 계산 (기존 방식)
→ 회수: 바닥의 주사위가 화면 아래쪽 밖으로 빠르게 이동
→ 화면 밖에서 출발 자세와 새 보정 회전으로 즉시 교체 (보이지 않음)
→ 궤적 재생: 주사위가 화면 아래 가장자리에서 들어와 구르다가 모두 정지
→ 결과 대기: 모든 주사위가 정지 자세를 유지한 채 resultRevealDelay 동안 대기
→ true 반환
→ ExecuteTurn, Story, HP, 결과 창 (기존 순서, 1회)
```

시뮬레이션은 회수 연출을 시작하기 전에 끝낸다. 시뮬레이션이 실패하면 회수 연출을 재생하지 않고 6절의 대체 경로로 간다.

## 5. 화면 밖 던지기

### 5.1 출발 위치

- 모든 참여 주사위는 시뮬레이션 첫 프레임에 카메라 화면 밖, 화면 아래 가장자리 너머(카메라 쪽, `z < NearZ`)에 있어야 한다. 판정 기준은 주사위의 `boundingRadius`를 포함한 전체 외형이다. 중심점만 화면 밖인 상태는 허용하지 않는다.
- 출발 위치는 현재 구도의 `OrthographicSize`, `Pitch`, 출발 높이, `DieRadius`, `padding`으로 계산한다. 화면 비율, 주사위 수, 카메라 기울기 설정이 바뀌어도 화면 밖 조건이 유지되어야 한다. 16:9를 가정하지 않는다.
- 여러 주사위의 출발 위치는 서로 겹치지 않아야 한다. 가로 위치는 진입 후 좌우 벽 안쪽(`±InnerHalfWidth`)에 들어오도록 정하고, 행이 여러 개이면 카메라 쪽으로 더 멀리 배치한다.
- `DiceArenaLayout.ThrowStartPositions`는 새 출발 위치 계산으로 바꾸거나, 새 메서드를 추가하고 기존 메서드와 그 테스트를 정리한다. 어느 쪽이든 Unity 객체 없이 계산할 수 있는 순수 계산으로 두고 Edit Mode 테스트로 화면 밖 조건을 검증한다.
- 연출 난수와 시드 규칙은 기존과 같다. 같은 시드, 같은 구도, 같은 주사위 구성에서는 같은 출발 위치와 궤적이 나와야 한다.

### 5.2 진입 통로와 카메라 쪽 벽

현재 시뮬레이션은 네 벽을 처음부터 만들기 때문에, 화면 밖에서 출발한 주사위는 카메라 쪽 벽(`WallNear`)에 막힌다. 다음 조건을 만족하도록 물리 경계를 바꾼다.

- 출발 위치에서 영역 안까지의 진입 통로에 바닥, 천장, 좌우 벽을 연장해 주사위가 통로 밖으로 떨어지거나 옆으로 빠지지 않게 한다.
- 카메라 쪽 벽은 모든 참여 주사위의 외형 전체가 `NearZ` 안쪽으로 들어온 뒤에 활성화한다. 활성화 시점에 벽 콜라이더가 주사위와 겹치지 않아야 한다. 진입 후 다시 통로 쪽으로 튕겨 나가는 주사위는 활성화된 벽이 막는다.
- 정해진 진입 시간 안에 모든 주사위가 들어오지 않거나, 벽 활성화 전에 주사위가 통로 쪽으로 되돌아가면 그 시도는 실패로 보고 기존 재시도 규칙(시드 변경, `maximumAttempts`)을 따른다. 실패 이유를 `DiceThrowOutcome.FailureReason`에 구분해서 남긴다.
- 위 방식은 권장 방향이다. 다른 방식(예: 처음부터 통로까지 포함한 긴 상자를 쓰고 진입 후 카메라 쪽 경계를 옮기는 방식)을 쓰면 이유를 기록한다. 어느 방식이든 정지 판정은 기존처럼 영역 안의 바닥에서만 성공으로 인정하고, 결과 화면에서 모든 주사위가 시야 안에 있어야 한다.
- 비참여 장애물 주사위(형태 오류 등으로 결과 목록에 없는 표시 중 주사위)는 기존처럼 영역 안의 현재 자세로 고정 장애물이 된다. 회수 대상이 아니다.
- 프로젝트 기본 물리 Scene과 전역 `Physics.gravity`를 건드리지 않는다는 기존 원칙을 유지한다.

### 5.3 던지기 세기와 시간

- 진입 거리만큼 비행 시간이 늘어나므로 `forwardSpeed`, `upwardSpeed`, `maximumSimulationSeconds`, `playbackSpeed`를 다시 측정해 정한다. 주사위가 영역 안에서 충분히 구르되 지나치게 길어지지 않게 한다.
- 진입 중 주사위가 천장에 닿지 않고 자연스러운 포물선으로 화면 아래 가장자리를 넘도록 출발 높이와 위쪽 속도를 정한다.
- 기본 3개와 많은 수(20개)에서 재시도 횟수, 대체 연출 사용률, 시뮬레이션 계산 시간, 전체 연출 시간을 측정해 보고한다. 현재 기준(20개 약 36ms 계산, 최장 약 2.01초 굴림)과 비교한다.

## 6. 회수 연출

- 굴림을 시작하면, 시뮬레이션이 성공한 경우에 한해 참여 주사위를 정지 자세에서 화면 아래쪽 밖으로 이동시킨다. 회수 시간은 새 설정 `recallDuration`으로 두고 기본값은 측정으로 정한다(목표 0.2~0.3초).
- 각 주사위는 X 위치를 유지한 채 -Z 방향(화면 아래쪽)으로 이동하고, 필요하면 살짝 들어 올린다. 같은 방향·같은 진행률로 움직이므로 주사위끼리 시각적으로 교차하지 않아야 한다. 이동 중 회전은 자연스러운 범위에서 더해도 된다.
- 회수 목표 지점은 5.1과 같은 기준으로 계산한 화면 밖 위치다. 모든 회수 주사위가 화면 밖에 도달한 뒤, 보이지 않는 상태에서 Root를 궤적 첫 프레임 자세로, Body를 새 보정 회전 S로 즉시 교체한다. 현재처럼 화면 안에서 보정 회전을 섞는 동작은 없앤다.
- 첫 표시 직후처럼 이전 결과가 없는 정돈 배치 상태에서도 같은 회수 연출을 쓴다.
- 굴림 중 추가된 주사위는 기존처럼 숨겨 두었다가 굴림이 끝나면 나타난다. 굴림 중 제거된 주사위의 회수·재생은 안전하게 건너뛴다.
- 회수 연출 시간은 화면 프레임 속도와 무관하게 `Time.realtimeSinceStartup` 기반 경과 시간으로 계산한다. 다른 시간원을 쓰면 이유를 기록한다.

## 7. 정지 후 결과 표시

### 7.1 정지 정의와 결과 대기

- "모든 주사위가 정지했다"는 궤적 재생이 마지막 프레임에 도달해 모든 참여 주사위가 최종 정지 자세에 놓인 상태를 말한다. 이 시점 이후 결과 대기 시간 동안 주사위의 위치와 회전이 바뀌면 안 된다.
- 첫 정지 프레임 자세를 유지 구간 끝의 최종 자세로 바꾸는 현재 처리 때문에 재생 끝에서 눈에 띄는 순간 이동이 생기면 안 된다. 최종 프레임 직전과 최종 자세의 위치·각도 차이를 측정하고, 눈에 띄면 유지 구간을 재생에 포함하거나 짧게 보간한다. 측정값과 선택을 보고한다.
- 정지 후 새 설정 `resultRevealDelay` 동안 대기한 뒤 `PlayRollAsync`가 `true`를 반환한다. 기본값은 0.3~0.5초 범위에서 측정해 정한다. 대기 중 취소되면 기존 취소 규칙을 따른다.
- 정상 던지기가 끝난 뒤 결과를 적용하기 전과 후에 `FinishRollLayout`·`Compose`가 정지한 주사위를 정돈 배치로 옮기면 안 된다. 3절에 적은 `HasOverlap(..., radius * 1.5f)` 기준이 맞닿은 정상 정지를 겹침으로 판정하는지 확인하고, 그렇다면 물리 정지에서 허용한 최소 간격과 일치하는 기준으로 고친다. 표시 영역 크기가 실제로 바뀐 경우의 재배치 규칙은 기존과 같다.
- 결과 대기 시간은 모션 감소와 대체 배치 경로에도 적용한다. 대기는 움직임이 아니라 결과 면을 읽는 시간이기 때문이다.

### 7.2 결과 적용 순서

- `TemporaryDiceRollMenuController`의 결과 적용 순서(`ExecuteTurn` → 기록 → Story → HP·적 표시 → 전투 완료 알림 → 결과 창 요청)와 1회 적용 보장은 바꾸지 않는다. 바뀌는 것은 이 순서가 시작되는 시점뿐이다.
- 결과 적용 전에는 Story, HP 바, 다음 적 행동 문구, 결과 창, `OwnedDicePanel`의 상태 문구가 이번 굴림의 결과를 미리 드러내면 안 된다.

## 8. 시간 제한과 실패 경로

### 8.1 시간 제한 계산

- `OwnedDicePanel.MaximumPresentationSeconds`가 회수, 최대 시뮬레이션 재생 시간, 결과 대기를 모두 포함한 상한을 반환하도록 고친다. 모션 감소와 대체 배치 경로의 길이도 이 상한 안에 들어야 한다.
- `TemporaryDiceRollMenuController`는 실제 시간 제한을 `max(rollTimeoutSeconds, panel.MaximumPresentationSeconds + 여유)`로 계산한다. 여유는 새 설정(예: `rollTimeoutMarginSeconds`)으로 두고 기본값을 측정해 정한다. 지나치게 긴 대기를 막는 상한은 기존 15초를 유지한다.
- 기존 `rollTimeoutSeconds` 필드와 `TMP_MainScene`에 저장된 값 3은 의미를 바꾸지 않고 최소값으로 남긴다. 필드 이름이나 직렬화 이름을 바꾸지 않는다. 의미가 바뀌지 않으므로 Scene 변경은 필요하지 않다.
- 정상 연출에서 시간 제한에 걸리지 않는지 기본 3개와 20개에서 확인한다.

### 8.2 시간 초과

- 시간 초과가 먼저 끝나면 컨트롤러는 연출을 취소하고, 패널이 모든 주사위를 결과 면이 위를 향한 정지 자세로 놓은 뒤에 `ShowRollFallback`과 결과 적용을 진행한다. 현재처럼 취소 직후 같은 프레임에 결과를 적용하지 않는다.
- 이를 위해 취소된 `PlayRollAsync`의 완료를 짧은 상한 안에서 기다리거나, 패널에 동기적으로 즉시 정지·배치하는 공개 메서드를 추가한다. 어느 방식이든 패널이 비활성이거나 파괴된 경우에는 기다리지 않고 진행하며, 결과 적용이 막히거나 두 번 일어나면 안 된다.
- 시간 초과 경로에는 `resultRevealDelay`를 적용하지 않는다. 이미 늦어진 상황에서 대기를 더하지 않기 위해서다.

### 8.3 기타 경로

| 상황 | 동작 |
| --- | --- |
| 시뮬레이션 실패(재시도 초과, 진입 실패, 예외, 보정 실패) | 회수 연출 없이 기존 `fallbackDuration` 정돈 배치로 결과 면을 보이고, 정지 후 `resultRevealDelay`를 기다린 뒤 `true`를 반환한다. 로그 구분(`OWNED_DICE_SIMULATION_FALLBACK`, `OWNED_DICE_SIMULATION_ERROR`)은 유지한다. |
| 모션 감소 | 회수와 화면 밖 던지기를 모두 생략하고 기존 `reducedMotionDuration` 배치를 쓴다. 정지 후 `resultRevealDelay`를 기다린다. |
| 형태 없음, 카메라·모델·Rig 누락, 패널 비활성 | 기존처럼 `false`를 반환하고 텍스트 대체와 결과 적용 경로를 쓴다. |
| 굴림 중 패널 비활성화, Scene 전환, 새 세션 | 기존 규칙을 유지한다. 재생을 중단하고, 재활성화 시 확정 결과 면으로 정돈 배치하며, 오래된 행동의 결과는 적용하지 않는다. |
| 굴림 중 표시 영역 크기 변경 | 진행 중인 구도와 출발 위치를 굴림이 끝날 때까지 유지하고, 끝난 뒤 기존 규칙으로 재구성한다. 재구성이 결과 대기 이후에 일어나는지, 대기 중에 일어나면 어떻게 처리하는지 구현 중 정하고 기록한다. 이때도 결과 적용 전에 주사위가 움직이지 않는 상태여야 한다. |

어떤 경로에서도 재추첨하거나 결과를 두 번 적용하지 않는다.

## 9. 주요 수정 경로

문서 작성 시 아래 경로를 확인했다.

- `Assets/TxTRPG/UI/Runtime/Dice/OwnedDicePanel.cs`: 회수 연출, 화면 밖 교체, 결과 대기, `MaximumPresentationSeconds`, 즉시 정지·배치, 정지 후 재배치 기준
- `Assets/TxTRPG/UI/Runtime/Dice/DiceThrowSimulator.cs`: 화면 밖 출발, 진입 통로, 카메라 쪽 벽 활성화, 진입 실패 판정
- `Assets/TxTRPG/UI/Runtime/Dice/DiceArenaLayout.cs`: 화면 밖 출발·회수 위치 계산, 화면 밖 판정 보조 계산, 겹침 기준
- `Assets/TxTRPG/UI/Runtime/Dice/DiceThrowSettings.cs`: `recallDuration`, `resultRevealDelay`, 진입 관련 설정과 기본값
- `Assets/TxTRPG/Application/Runtime/Dice/TemporaryDiceRollMenuController.cs`: 시간 제한 계산, 시간 초과 시 정지 후 결과 적용 순서
- `Assets/TxTRPG/Application/Editor/OwnedDiceThrowValidation.cs`: `Validate Throw Presentation` 검증 항목 추가
- `Assets/TxTRPG/Application/Tests/Editor/OwnedDiceIntegrationTests.cs`
- `Assets/TxTRPG/UI/Tests/PlayMode/DiceThrowSimulationPlayModeTests.cs`
- `Assets/Scenes/TMP_MainScene.unity`: 변경하지 않는 것이 목표다. `OwnedDicePanel`의 새 직렬화 필드는 코드 기본값을 사용한다. Scene 저장이 필요해지면 범위를 기록하고 의도하지 않은 변경이 없는지 확인한다.

표시 코드는 `Assets/TxTRPG/UI/Runtime/Dice/`에 두고, UI 어셈블리가 Application을 역참조하지 않게 한다. `OwnedDicePanel`의 기존 공개 계약(`PlayRollAsync`, `ShowRollFallback`, `IsRolling`, `MaximumPresentationSeconds`)은 유지하거나, 바꾸면 모든 호출자와 테스트를 함께 갱신한다.

## 10. 적용

- 작업 시작 시 Editor 연결, 컴파일 상태, 열린 Scene·Prefab의 미저장 변경을 확인하고 사용자 변경을 보존한다.
- 이 작업은 코드와 직렬화 기본값 변경이 중심이며 Prefab 구조나 Scene 연결을 바꾸지 않는 것이 목표다. `DiceStage.prefab`이나 `TMP_MainScene`의 변경이 필요해지면 이유와 범위를 기록하고 저장 전 검증한다.
- 새 Prefab 생성기나 임시 적용 메뉴가 필요하지 않을 것으로 예상한다. 추가하면 `AGENTS.md` 규칙에 따라 목적, 재실행 가능 여부, 저장 여부를 `DOCS/development/workflows.md`에 기록한다.

## 11. 검증

1. 여러 화면 비율(넓은 화면, 좁은 세로 화면, 휴대폰·태블릿 비율)과 주사위 수(1개, 기본 3개, 20개)에서, 궤적 첫 프레임과 회수 목표 지점의 모든 주사위 외형이 화면 밖(아래쪽)에 있는지 Edit Mode 계산 테스트와 Play Mode 검증으로 확인한다.
2. 재생 중 주사위가 화면 아래 가장자리를 넘어 들어오고, 진입 후 모든 주사위가 영역 안에서 정지하며, 정지 화면에서 모든 주사위가 시야 안에 있고 겹치지 않는지 확인한다(`AreAllDiceVisibleAndSeparated`).
3. 고정 결과로 D6·D8의 모든 FaceIndex를 여러 시드에서 강제하고, 정지 시 위를 향한 면이 확정 결과와 같은지 확인한다. 같은 효과·수치를 가진 서로 다른 면도 확인한다.
4. 회수 중 주사위끼리 교차하지 않고, 보정 회전 교체가 화면 밖에서만 일어나는지 확인한다.
5. 결과 적용 시점이 "마지막 주사위 정지 + `resultRevealDelay`" 이후인지 확인한다. 정지 이후 결과 적용 전까지 주사위 자세가 바뀌지 않는지, 정상 정지 직후 정돈 배치로 순간 이동하지 않는지 확인한다.
6. 시간 초과를 강제한 경우 주사위가 결과 자세로 정지·배치된 뒤에 텍스트 대체와 결과 적용이 일어나고, 결과가 한 번만 적용되는지 확인한다. 기본 설정의 정상 연출(3개, 20개)이 시간 제한에 걸리지 않는지 확인한다.
7. 진입 실패, 정지하지 않음, 기울어진 정지, 재시도 초과, 시뮬레이션 예외, 모션 감소에서 결과 면, 대기 시간, 로그를 확인한다.
8. 같은 연출 시드로 다른 프레임 속도(예: 30·60·120fps 제한)에서 회수·재생·대기 길이와 최종 자세가 같은지 확인한다.
9. 굴림 중 패널 숨김, Scene 전환, 새 세션, 굴림 중 표시 영역 크기 변경, 굴림 중 주사위 추가·제거를 확인한다.
10. 시뮬레이션 계산 시간, 전체 연출 시간, 재시도 횟수, 대체 연출 사용률, 물리 Scene 수명을 측정해 기존 값과 비교한다. 실제 모바일 기기 측정이 불가능하면 미검증으로 명시한다.
11. `AGENTS.md`의 Edit Mode 테스트 범위 규칙에 따라 `OwnedDiceIntegrationTests`, `TemporaryPlayerDiceStateTests` 등 관련 클래스만 필터로 실행하고 `AssetWriting` 범주는 제외한다. `DiceThrowSimulationPlayModeTests`와 관련 Play Mode 테스트를 실행한다.
12. `Tools/TxT RPG/UI/Dice/Temporary/Validate Throw Presentation`에 화면 밖 출발, 정지 후 결과 시점, 시간 초과 순서 검증을 추가하고 Play Mode에서 실행한다. `Tools/TxT RPG/UI/Dice/Temporary/Validate Play Flow`도 실행한다.
13. AppScene 시작 경로로 `TMP_MainScene`에 진입해 Actions의 주사위 명령을 키보드·마우스, 컨트롤러, 터치로 실행하고 회수, 진입, 정지, 결과 대기, 결과 창, 포커스 복원을 눈으로 확인한다.
14. 사용 가능한 대상 빌드의 컴파일을 확인하고, 직렬화 diff에서 Scene·Prefab의 의도하지 않은 변경이 없는지 확인한다.

## 12. 문서 갱신

- `DOCS/architecture/owned-dice-presentation.md`: 연출 순서 다이어그램과 단계 설명, 화면 밖 출발과 진입 통로, 회수 연출, 결과 대기, 시간 제한 계산, 시간 초과 시 정지 후 결과 적용, 정지 후 재배치 기준, 기본 연출 설정값 표를 현재 구현 기준으로 갱신한다. "재생은 `liftDuration` 동안 … 들어 올리며" 설명은 새 동작으로 바꾼다.
- `DOCS/architecture/temporary-combat.md`: 결과 적용 시점이나 시간 제한 설명이 있으면 갱신한다.
- `DOCS/development/workflows.md`의 "보유 주사위 3D 패널과 결과 기록": 새 설정값, `Validate Throw Presentation` 검증 항목, 검증 절차를 갱신한다.
- `DOCS/architecture/project-structure.md`: 새 타입이나 어셈블리 의존이 생기면 갱신한다.
- 이 지침서와 이전 지침서는 수정하지 않는다. 구현 결과는 아키텍처 문서에 기록한다.

## 13. 최종 보고

다음을 구분해서 보고한다.

- 구현 완료 항목과 실제 타입·경로
- Editor에 이미 적용하고 저장한 자산과 그 검증 결과
- 사용자가 실행해야 할 남은 적용 단계. 남은 수동 Tools 단계가 있으면 `AGENTS.md`에 따라 `사용자가 수행할 적용 절차` 절에 정확한 메뉴 경로, 순서, 전제 조건, 저장 여부, 성공 기준을 `text` 코드 블록으로 제공한다. 없으면 없다고 명시한다.
- 실제로 수행한 테스트와 측정, 확인하지 못한 기기·빌드
- 구현 중 정한 기본값(`recallDuration`, `resultRevealDelay`, 시간 제한 여유, 던지기 속도, 최대 시뮬레이션 시간 등)과 근거
- 진입 통로와 카메라 쪽 벽 처리 방식의 선택과 이유
- 재생 끝 순간 이동과 정지 후 정돈 배치 문제의 측정 결과와 처리 내용
