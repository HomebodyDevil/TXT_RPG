# 보유 주사위 가장 가까운 가장자리 회수와 던지기 전 지연

## 1. 목적과 작업 범위

현재 굴림을 시작하면 바닥에 놓인 모든 주사위가 같은 이동량으로 함께 화면 아래쪽(카메라 쪽) 밖으로 회수되고, 회수가 끝나는 즉시 새 던지기가 화면 아래쪽에서 들어온다. 이 작업은 두 가지를 바꾼다.

1. 각 주사위를 화면의 위·아래·왼쪽·오른쪽 가장자리 중 그 주사위에서 가장 가까운 쪽으로 회수한다.
2. 모든 주사위의 회수가 끝난 뒤, 개발자가 정한 고정 지연 시간이 지나면 던지기를 시작한다.

던지기 진입 방향(화면 아래쪽), 결과 판정, 결과 보정 회전, 정지 후 결과 대기, 시간 제한 계산 원칙은 [보유 주사위 화면 밖 던지기와 정지 후 결과](owned-dice-offscreen-throw-and-settled-results.md)에서 정한 구조를 그대로 유지한다.

이 문서는 구현 지침이다. 아래의 신규 설정, 메서드, 동작은 예정된 구조이며 아직 구현되었다고 간주하지 않는다. 이 문서를 작성하는 단계에서는 Scene, Prefab, 런타임 코드를 수정하지 않았다.

포함 범위는 다음과 같다.

- 주사위별 회수 방향(위·아래·왼쪽·오른쪽) 선택과 방향별 화면 밖 목표 위치 계산
- 방향이 다른 주사위끼리 회수 중 겹치지 않도록 하는 처리
- 회수 후 고정 지연과 그 시간을 포함한 최대 연출 길이·시간 제한 갱신
- 관련 테스트, 검증 메뉴, 문서 갱신

제외 범위는 다음과 같다.

- 던지기 진입 방향 변경. 주사위는 계속 화면 아래쪽 밖에서 던져져 들어온다.
- 지연 시간의 무작위화. 지연은 Inspector에서 정한 하나의 고정값이다.
- 물리 시뮬레이션, 진입 통로, 정지 판정, 결과 적용 순서 변경
- 효과음, 진동, 파티클, 새 UI

## 2. 사용자 확정 사항

| 항목 | 확정 요구 |
| --- | --- |
| 회수 방향 | 각 주사위는 위·아래·왼쪽·오른쪽 화면 가장자리 중 가장 가까운 쪽으로 회수한다. 주사위마다 방향이 다를 수 있다. |
| 던지기 진입 방향 | 지금처럼 화면 아래쪽 밖에서 던져져 들어온다. 회수 방향과 관계없다. |
| 회수 시간 | 모든 주사위가 같은 `recallDuration` 동안 동시에 화면 밖으로 나간다. 가장자리까지 먼 주사위일수록 빠르게 움직이며, 전체 회수 시간은 배치와 관계없이 일정하다. |
| 던지기 전 지연 | 모든 주사위의 회수가 끝난 뒤 고정 지연 시간이 지나면 던지기를 시작한다. 지연 시간은 Inspector에서 설정하는 하나의 고정값이며 굴림마다 바뀌지 않는다. |

## 3. 선행 문서와 확인한 현재 구조

작업 전 `AGENTS.md`, `DOCS/README.md`와 아래 문서를 읽는다.

- `DOCS/architecture/owned-dice-presentation.md`
- `DOCS/development/workflows.md`의 "보유 주사위 3D 패널과 결과 기록"
- `DOCS/instructions/owned-dice-offscreen-throw-and-settled-results.md`

문서 작성 시 현재 작업 트리에서 다음을 확인했다. 직전 지침서(화면 밖 던지기와 정지 후 결과)의 구현은 이 시점에 아직 커밋되지 않은 변경으로 작업 트리에 있다. 구현 직전 현재 작업 트리와 커밋 상태를 다시 확인하고, 다르면 실제 상태를 우선한다. 직전 지침서의 구현이 작업 트리에 없으면 작업을 멈추고 보고한다.

- `OwnedDicePanel.PlayThrowAsync`는 회수 단계에서 모든 참여 주사위를 `fromPositions[i] + (0, lift * eased, offsetZ * eased)`로 옮기고, `-X`축 기준으로 `recallTumbleDegrees * eased`만큼 굴린다. `eased = t * t`(가속 곡선), `lift = arena.DieRadius * recallLiftRadii`이다.
- `offsetZ`는 `DiceArenaLayout.RecallOffsetZ(arena, positions, lift)`가 계산한 하나의 공통 Z 이동량이다. 모든 주사위가 같은 이동을 하므로 서로 교차하지 않는다는 것이 현재 설계의 근거다.
- 회수가 끝나면 `timeline.RecallEndedAt`을 기록하고, 같은 프레임에 Root를 궤적 첫 프레임 자세로, Body를 새 보정 회전으로 바꾼다. 이어서 `AreBelowScreen`으로 교체 순간 모든 주사위가 화면 아래 밖에 있었는지 `timeline.HiddenAtSwap`·`SwapDetail`에 기록하고, 곧바로 궤적 재생을 시작한다. 회수와 재생 사이에 지연이 없다.
- `DiceArenaLayout`에는 화면 아래쪽 판정과 계산만 있다: `ScreenHeight`, `IsBelowScreen`, `BelowScreenZ`, `OffscreenStartPositions`, `RecallOffsetZ`, `OffscreenMarginRadii`(0.1). 위쪽·왼쪽·오른쪽 화면 밖 판정과 목표 위치 계산은 없다.
- 직교 카메라는 원점을 바라보며 `Pitch`만큼 아래로 기울어져 있다. 화면 가로 범위는 `±OrthographicSize * Aspect`이고, 세로 좌표는 `ScreenHeight = y·cos(pitch) + z·sin(pitch)`이며 범위는 `±OrthographicSize`이다. 보이는 `Floor` Quad는 `[±HalfWidth] × [NearZ, FarZ]` 범위만 덮는다.
- `DiceThrowSettings`의 회수 관련 설정은 `recallDuration`(기본 0.25초, 0~1초), `recallLiftRadii`(0.4), `recallTumbleDegrees`(90°)이다. `MaximumThrowPresentationSeconds = RecallDuration + MaximumSimulationSeconds / PlaybackSpeed + ResultRevealDelay`이다.
- `OwnedDicePanel.MaximumPresentationSeconds`는 `max(MaximumThrowPresentationSeconds, max(fallbackDuration, reducedMotionDuration) + ResultRevealDelay)`이고, `TemporaryDiceRollMenuController.ResolveRollTimeout`이 이 값으로 실제 시간 제한을 계산한다.
- 회수 관련 사용처: `OwnedDiceIntegrationTests`가 `DiceArenaLayout.RecallOffsetZ`를 직접 검증하고, `OwnedDiceThrowValidation`이 `timeline.HiddenAtSwap`을, `TemporaryOwnedDiceIntegration`(Validate Play Flow)이 `HiddenAtSwap`과 경과 시간을 보고한다.
- 회수 대상은 결과 목록에 있는 참여 주사위뿐이다. 비참여 장애물 주사위는 회수하지 않는다. 모션 감소와 시뮬레이션 실패 경로에는 회수가 없다.

CodeGraph로 `OwnedDicePanel.PlayThrowAsync`, `DiceArenaLayout`, `DiceThrowSettings`, `OwnedDiceRollTimeline`, `OwnedDiceThrowValidation`, `TemporaryOwnedDiceIntegration`의 호출자와 테스트를 먼저 탐색하고, 실제 소스로 확인한다.

## 4. 연출 순서

```text
행동 수락 및 RollAll로 결과 확정 (기존)
→ 던지기 시뮬레이션과 보정 회전 계산 (기존, 동기)
→ 주사위별 회수 방향과 화면 밖 목표 위치 계산 (신규)
→ 회수: 모든 참여 주사위가 recallDuration 동안 각자 가장 가까운 가장자리 밖으로 이동 (변경)
→ 화면 밖에서 출발 자세와 새 보정 회전으로 즉시 교체 (기존)
→ 던지기 전 지연: throwDelay 동안 대기 (신규)
→ 궤적 재생: 화면 아래 가장자리에서 들어와 구르다가 모두 정지 (기존)
→ 결과 대기 resultRevealDelay (기존)
→ true 반환 → ExecuteTurn, Story, HP, 결과 창 (기존, 1회)
```

출발 자세 교체는 회수 직후, 지연 시작 전에 한다. 지연 동안 참여 주사위는 화면 밖 출발 자세에 머무르며 화면에 보이지 않는다.

## 5. 회수 방향 선택

### 5.1 가장 가까운 가장자리

- 각 참여 주사위의 현재 정지 위치를 화면 좌표(직교 카메라의 가로 `x`, 세로 `ScreenHeight`)로 바꾸고, 네 가장자리(`x = ±OrthographicSize * Aspect`, `ScreenHeight = ±OrthographicSize`)까지의 화면상 거리를 비교해 가장 짧은 쪽을 고른다. 플레이어가 보는 화면 기준이므로 바닥 평면 위의 이동 거리로 비교하지 않는다.
- 거리가 같으면 아래 → 왼쪽 → 오른쪽 → 위 순서로 정한다. 같은 배치와 같은 구도에서는 항상 같은 방향이 나와야 한다. 회수 방향 선택에는 연출 난수를 쓰지 않는다.
- 방향 선택과 방향별 목표 위치 계산은 Unity 객체 없이 계산할 수 있는 순수 계산으로 `DiceArenaLayout`에 두고 Edit Mode 테스트로 검증한다.

### 5.2 방향별 화면 밖 목표 위치

- 목표 위치는 주사위의 `boundingRadius` 전체가 해당 가장자리 밖에 있는 위치다. 기존 `OffscreenMarginRadii` 여백을 같은 방식으로 적용한다. 중심점만 화면 밖인 상태는 허용하지 않는다.
- 각 주사위는 고른 가장자리에 수직인 방향으로만 이동한다. 왼쪽·오른쪽은 X만, 아래·위는 화면 세로 방향으로 이동한다. 아래·위 이동은 바닥 평면에서 Z 이동과 들림(`recallLiftRadii`)으로 표현하고, 들림을 포함한 최종 높이로 화면 밖 조건을 계산한다.
- 위쪽 회수는 카메라에서 먼 쪽(+Z)으로 이동한다. 이때 주사위가 보이는 `Floor` 범위(`FarZ`)를 지나 바닥이 없는 배경 위를 지나갈 수 있다. 이 구간이 어색하게 보이는지 확인하고, 필요하면 들림 높이를 조정한다. 판단과 조정 내용을 보고한다.
- 회전(`recallTumbleDegrees`)은 이동 방향으로 구르는 방향의 축(이동 방향과 위쪽의 외적 방향)을 사용한다. 현재의 아래쪽 회수 회전과 같은 느낌을 유지한다.
- 화면 비율(넓은 화면, 좁은 세로 화면), 주사위 수, `cameraPitch`가 바뀌어도 목표 위치가 화면 밖 조건을 만족해야 한다. 16:9를 가정하지 않는다.

### 5.3 회수 중 겹침 방지

현재 설계는 모든 주사위가 같은 이동을 해서 교차하지 않는다는 성질에 기대고 있다. 방향이 서로 다르면 이 성질이 사라지므로 다음을 지킨다.

- 모든 주사위가 같은 시간과 같은 진행 곡선으로 움직인다는 전제에서, 회수 중 어느 시점에도 두 주사위의 외형이 화면에 보이는 상태로 겹치지 않아야 한다. 판정은 같은 진행률 t에서의 두 위치 사이 거리를 기준으로 하고, 두 주사위 중 하나라도 화면에 보이는 구간만 검사한다.
- 계산 단계에서 겹침이 예상되면, 겹치는 주사위 중 하나를 다음으로 가까운 가장자리로 바꿔 다시 검사한다. 모든 조합으로도 해결되지 않으면 그 굴림에 한해 기존 공통 아래쪽 회수(`RecallOffsetZ`)로 대체하고 경고 로그를 남긴다. 다른 해결 방식을 쓰면 이유를 기록한다.
- 이 검사도 순수 계산으로 두고, 겹침이 생기는 배치를 일부러 만든 Edit Mode 테스트로 검증한다.
- 기본 3개와 많은 수(20개)에서 여러 시드의 정지 배치를 사용해, 방향 재배정과 공통 회수 대체가 얼마나 자주 일어나는지 측정해 보고한다.

### 5.4 교체 순간 화면 밖 확인

- `AreBelowScreen` 확인은 네 방향 모두를 다루는 화면 밖 확인으로 바꾼다. 교체 순간 모든 참여 주사위의 외형이 어느 가장자리든 화면 밖에 있어야 한다.
- `OwnedDiceRollTimeline.HiddenAtSwap`, `SwapDetail`의 의미를 "네 방향 중 어느 쪽이든 화면 밖"으로 갱신하고, 이를 읽는 `OwnedDiceThrowValidation`과 `TemporaryOwnedDiceIntegration`의 판정 문구를 맞춘다.
- 검증을 위해 주사위별로 고른 회수 방향을 타임라인이나 검증 전용 속성으로 확인할 수 있게 한다.

## 6. 던지기 전 지연

- `DiceThrowSettings`에 고정 지연 설정을 추가한다. 이름은 `throwDelay`를 기본으로 하고, 범위는 0~2초, 기본값은 0.3초로 한다. 0이면 지금처럼 회수 직후 바로 던진다. 기본값을 바꾸면 이유를 기록한다.
- 지연은 모든 참여 주사위의 회수와 출발 자세 교체가 끝난 뒤 시작한다. 지연 동안 화면에는 비참여 장애물 주사위와 바닥만 보인다.
- 지연 시간은 기존 회수·재생·결과 대기와 같이 `Time.realtimeSinceStartup` 기반 실제 경과 시간으로 계산한다. 화면 프레임 속도와 관계없이 길이가 같아야 한다.
- 지연 중에도 기존 취소 규칙을 따른다. 취소, 시간 초과, 패널 비활성화, Scene 전환이 일어나면 기존처럼 주사위를 결과 면이 위를 향한 정돈 배치로 놓고, 결과는 한 번만 적용한다.
- 지연은 회수가 있는 물리 던지기 경로에만 적용한다. 회수가 없는 모션 감소와 시뮬레이션 실패 대체 배치에는 적용하지 않는다.
- `OwnedDiceRollTimeline`에 던지기 재생 시작 시각(예: `ThrowStartedAt`)을 추가해 `RecallEndedAt`과의 차이로 지연을 검증할 수 있게 한다.

## 7. 시간 제한

- `DiceThrowSettings.MaximumThrowPresentationSeconds`에 `throwDelay`를 더한다. 따라서 `OwnedDicePanel.MaximumPresentationSeconds`와 `TemporaryDiceRollMenuController.ResolveRollTimeout`의 결과도 자동으로 늘어난다.
- `rollTimeoutSeconds`, `rollTimeoutMarginSeconds`, 15초 상한의 의미와 직렬화 이름은 바꾸지 않는다. Scene 변경은 필요하지 않다.
- `throwDelay`를 최대값(2초)으로 두어도 정상 연출이 시간 제한에 걸리지 않는지 기본 3개와 20개에서 확인한다.

## 8. 주요 수정 경로

문서 작성 시 아래 경로를 확인했다.

- `Assets/TxTRPG/UI/Runtime/Dice/OwnedDicePanel.cs`: `PlayThrowAsync`의 회수 단계, 교체 순간 화면 밖 확인(`AreBelowScreen`), 던지기 전 지연, `OwnedDiceRollTimeline`
- `Assets/TxTRPG/UI/Runtime/Dice/DiceArenaLayout.cs`: 화면 좌표 변환, 가장 가까운 가장자리 선택, 방향별 화면 밖 판정과 목표 위치, 회수 중 겹침 검사. 기존 `RecallOffsetZ`는 대체 경로에서 계속 쓰거나, 쓰지 않게 되면 테스트와 함께 정리한다.
- `Assets/TxTRPG/UI/Runtime/Dice/DiceThrowSettings.cs`: `throwDelay`, `MaximumThrowPresentationSeconds`
- `Assets/TxTRPG/Application/Editor/OwnedDiceThrowValidation.cs`: 회수 방향, 회수 중 겹침, 교체 순간 화면 밖, 지연 길이 검증
- `Assets/TxTRPG/Application/Editor/TemporaryOwnedDiceIntegration.cs`: Validate Play Flow의 `HiddenAtSwap` 보고 문구와 지연 반영
- `Assets/TxTRPG/Application/Tests/Editor/OwnedDiceIntegrationTests.cs`: 방향 선택, 동률 규칙, 방향별 목표 위치, 겹침 검사, 최대 연출 길이 계산
- `Assets/TxTRPG/UI/Tests/PlayMode/DiceThrowSimulationPlayModeTests.cs`: 영향이 있으면 갱신

표시 코드는 `Assets/TxTRPG/UI/Runtime/Dice/`에 두고, UI 어셈블리가 Application을 역참조하지 않게 한다. `OwnedDicePanel`의 공개 계약(`PlayRollAsync`, `StopRollAtResults`, `ShowRollFallback`, `IsRolling`, `MaximumPresentationSeconds`)은 유지한다. 바꾸면 모든 호출자와 테스트를 함께 갱신한다. 새 직렬화 필드는 코드 기본값을 쓰므로 `TMP_MainScene` 저장은 필요하지 않을 것으로 예상한다. Scene이나 Prefab 저장이 필요해지면 범위를 기록하고 의도하지 않은 변경이 없는지 확인한다.

## 9. 적용

- 작업 시작 시 Editor 연결, 컴파일 상태, 열린 Scene·Prefab의 미저장 변경을 확인하고 사용자 변경을 보존한다. 직전 지침서 구현을 포함한 커밋되지 않은 변경을 되돌리거나 덮어쓰지 않는다.
- 이 작업은 코드와 직렬화 기본값 변경이 중심이며, 새 Prefab 생성기나 임시 적용 메뉴는 필요하지 않을 것으로 예상한다. 추가하면 `AGENTS.md` 규칙에 따라 목적, 재실행 가능 여부, 저장 여부를 `DOCS/development/workflows.md`에 기록한다.

## 10. 검증

1. Edit Mode 테스트로 네 방향 각각의 선택, 동률 우선순위, 넓은 화면·좁은 세로 화면·정사각형 비율에서의 방향별 목표 위치 화면 밖 조건을 확인한다.
2. 회수 중 겹침이 예상되는 배치에서 방향 재배정 또는 공통 아래쪽 회수 대체가 일어나는지 확인한다.
3. Play Mode에서 화면 네 구석과 네 변 근처에 주사위를 둔 배치로 굴려, 각 주사위가 가장 가까운 가장자리로 나가고 회수 중 겹치지 않는지 확인한다. 위쪽 회수에서 바닥 밖 구간이 어색하지 않은지 눈으로 확인한다.
4. 교체 순간 모든 참여 주사위가 화면 밖에 있는지(`HiddenAtSwap`) 확인한다.
5. `ThrowStartedAt - RecallEndedAt`이 `throwDelay`와 일치하는지, `throwDelay = 0`이면 지연 없이 던지는지 확인한다. 30·120fps에서 같은 시드로 회수·지연·재생 길이와 최종 자세가 같은지 확인한다.
6. 지연 중 취소, 시간 초과, 패널 숨김, Scene 전환에서 주사위가 결과 면 정돈 배치로 놓이고 결과가 한 번만 적용되는지 확인한다.
7. 기존 항목이 회귀하지 않는지 확인한다: 모든 면 강제 시 결과 일치, 정지 후 결과 대기, 정지 후 이동 없음, 모션 감소, 시뮬레이션 실패 대체 배치, 굴림 중 크기 변경, 6·12·20개 주사위, 런타임 4면 주사위 텍스트 대체.
8. `throwDelay`를 최대값으로 둔 상태를 포함해 정상 연출이 시간 제한 안에 끝나는지 확인한다.
9. `AGENTS.md`의 Edit Mode 테스트 범위 규칙에 따라 `OwnedDiceIntegrationTests` 등 관련 클래스만 필터로 실행하고 `AssetWriting` 범주는 제외한다. Play Mode 테스트가 Editor Play 시작 정책 때문에 실행되지 않으면 그 사실과 대체 검증을 보고한다.
10. `Tools/TxT RPG/UI/Dice/Temporary/Validate Throw Presentation`과 `Tools/TxT RPG/UI/Dice/Temporary/Validate Play Flow`를 Play Mode에서 실행한다.
11. AppScene 시작 경로로 `TMP_MainScene`에 진입해 Actions의 주사위 명령을 키보드·마우스, 컨트롤러, 터치로 실행하고 회수, 지연, 던지기, 정지, 결과 창을 확인한다.
12. 사용 가능한 대상 빌드의 컴파일을 확인하고, 직렬화 diff에서 Scene·Prefab의 의도하지 않은 변경이 없는지 확인한다. 실제 모바일 기기 확인이 불가능하면 미검증으로 명시한다.

## 11. 문서 갱신

- `DOCS/architecture/owned-dice-presentation.md`: 연출 순서 다이어그램과 단계 설명의 회수 부분(현재 "X를 유지한 채 -Z로 이동", "하나의 Z 이동")을 가장 가까운 가장자리 회수, 동률 규칙, 겹침 방지, 공통 아래쪽 회수 대체로 바꾼다. 던지기 전 지연, `MaximumPresentationSeconds` 계산식, 기본 연출 설정값 표(`throwDelay`)를 갱신한다.
- `DOCS/development/workflows.md`의 "보유 주사위 3D 패널과 결과 기록": 설정 목록에 `throwDelay`를 추가하고, `Validate Throw Presentation`과 Edit Mode 테스트 설명에 새 검증 항목을 반영한다.
- `DOCS/architecture/project-structure.md`: 새 타입이나 의존이 생기면 갱신한다.
- 이 지침서와 이전 지침서는 수정하지 않는다. 구현 결과는 아키텍처 문서에 기록한다.

## 12. 최종 보고

다음을 구분해서 보고한다.

- 구현 완료 항목과 실제 타입·경로
- Editor에 이미 적용하고 저장한 자산과 그 검증 결과
- 사용자가 실행해야 할 남은 적용 단계. 남은 수동 Tools 단계가 있으면 `AGENTS.md`에 따라 `사용자가 수행할 적용 절차` 절에 정확한 메뉴 경로, 순서, 전제 조건, 저장 여부, 성공 기준을 `text` 코드 블록으로 제공한다. 없으면 없다고 명시한다.
- 실제로 수행한 테스트와 측정, 확인하지 못한 기기·빌드
- `throwDelay` 기본값과 근거, 위쪽 회수의 바닥 밖 구간 처리
- 회수 중 겹침 방지 방식, 방향 재배정과 공통 아래쪽 회수 대체의 측정 빈도
