# 슬롯머신 Fortune Critical (행운 배수)

## 1. 목적과 작업 범위

현재 `TMP_MainScene`의 전투 행동은 슬롯머신을 한 번 돌려 각 Reel의 Payline 값을 더한 `SlotSpinResult.PaylineTotal`을 룬 줄 가중치로 분배한다. 이 작업은 회전 결과에 일정 확률로 n배 배수를 적용하는 **Fortune Critical**을 추가한다.

- 각 Reel은 독립적으로 Fortune Critical을 판정한다. 발생하면 그 Reel의 Payline 값에 배수를 곱한다.
- 합계도 독립적으로 Fortune Critical을 판정한다. 발생하면 Reel 배수를 적용한 값 전체에 합계 배수를 곱한다.
- 발생 확률과 배수 분포는 개발자가 설정 자산에서 정하고, 실행 중에는 세션 상태 API로 바꿀 수 있다.
- 배수 분포는 등록된 **가중치 함수 목록**에서 함수를 고르고 매개변수를 입력해 만든다. 함수는 클래스 하나와 등록 한 줄로 추가할 수 있어야 한다.

이 문서는 구현 지침이다. 아래의 신규 타입, 자산, 계층, 메뉴는 예정된 구조이며 아직 구현되었다고 간주하지 않는다. 이 문서를 작성하는 단계에서는 코드, Prefab, Scene, 설정 자산을 수정하지 않았다. 현재 구현은 [`DOCS/architecture/slot-machine-and-runes.md`](../architecture/slot-machine-and-runes.md)에 설명되어 있다.

포함 범위는 다음과 같다.

- Gameplay 도메인: Fortune Critical 규칙, 가중치 함수 계약과 기본 함수 목록, 배수 분포 생성, 판정기, 불변 결과
- Application: 설정 자산, 세션 상태와 실행 중 변경 API, 전투 컨트롤러의 수락 단계 연결, 수락·턴 기록, Story 문구
- UI: Reel별 배수 표시와 합계 배수 표시(텍스트), 표시 데이터 확장
- 설정 자산 Inspector의 확률 미리보기(배수별 확률, 기댓값)
- `AppRoot.prefab`, `SlotReel.prefab`, `SlotMachinePanel.prefab`의 필요한 부분 수정과 새 설정 자산 생성
- 테스트, 문서 갱신

제외 범위는 다음과 같다.

- 반짝임·흔들림·숫자 튀어오름 같은 강조 연출. 효과를 붙일 자리(표시 루트 분리)만 만든다.
- 개발용 디버그 창이나 Play Mode 전용 조작 UI
- 룬·아이템·버프가 Fortune Critical 설정을 바꾸는 게임 기능. 이 기능들이 나중에 호출할 API만 만든다.
- 연속 실패 보정(PRD, 천장) 같은 의사 난수 방식. 11.5절에 확장 방향만 적는다.
- 저장 데이터. 슬롯머신·룬 상태처럼 세션 수명이며 저장하지 않는다.
- 주사위 경로(`TemporaryDiceRollMenuController`)에 Fortune Critical 적용

## 2. 사용자 확정 사항

| 항목 | 확정 요구 |
| --- | --- |
| 이름 | 이 시스템을 "Fortune Critical"이라고 부른다. 코드 이름은 `FortuneCritical`을 쓴다. |
| 발생 단위 | 각 Reel에 독립적으로 발생할 수 있고, 합계에도 발생할 수 있다. |
| 배수 형식 | 정수 배수만 쓴다. 기본 범위는 ×2부터 ×10까지다. 반올림 규칙은 필요 없다. |
| 확률 구조 | "발생 확률 p"와 "발생했을 때 배수 n의 분포"를 따로 설정한다. |
| 분포 정의 | 배수 분포는 함수 그래프를 따른다. 개발자가 함수 목록에서 고르고 매개변수를 설정한다. 함수 추가가 쉬워야 한다. |
| 기본 함수 | 기하 분포(지수함수의 이산형), 감소 비율 r = 0.7 |
| 중첩 규칙 | Reel 배수와 합계 배수는 곱셈으로 중첩한다. |
| 상한 | 각 Reel의 유효 배수(Reel 배수 × 합계 배수)를 개발자가 정한 상한으로 자른다. |
| Reel별 설정 | Reel ID별로 개별 설정할 수 있어야 한다. 실행 중 Reel이 추가될 수 있음을 고려한다. |
| 실행 중 변경 | 설정 자산은 기본값으로 복사하고, 실행 중에는 검증된 세션 상태 API로 바꾼다. 기존 슬롯머신·룬 상태와 같은 방식이다. |
| 표시 범위 | Reel의 Payline 칸 근처와 합계 상자에 `×3` 같은 배수 텍스트를 표시하고, Story에 기록한다. 강조 연출은 넣지 않는다. |

다음 값은 사용자가 정하지 않은 임시 밸런스 값이다. 설정 자산의 기본값으로만 쓰며, 사용자가 자산에서 바꿀 수 있다. 문서와 Tooltip에 임시값임을 적는다.

| 항목 | 임시 기본값 |
| --- | --- |
| Reel 기본 발생 확률 | 0.05 (5%) |
| 합계 발생 확률 | 0.03 (3%) |
| 배수 범위 | 최소 2, 최대 10 (Reel·합계 공통) |
| 유효 배수 상한 | 30 |
| Story·패널의 시스템 이름 | `행운 치명` (`SlotCombatStoryFormatter`의 상수) |

## 3. 선행 문서와 확인한 현재 구조

작업 전 `AGENTS.md`, `DOCS/README.md`와 아래 문서를 읽는다.

- `DOCS/architecture/slot-machine-and-runes.md` (도메인, 컨트롤러 수락 단계, 표시 데이터, 패널 계층)
- `DOCS/architecture/temporary-combat.md`
- `DOCS/development/visual-effect-readiness.md` (배수 표시의 표시 루트 분리)
- `DOCS/architecture/korean-font-fallback.md` (`행운 치명`, `×` 글자의 글꼴 표시)
- `DOCS/development/workflows.md`의 `슬롯머신 전투 적용` 절
- `DOCS/architecture/prefab-rebuild-registry.md` (슬롯머신 Prefab은 Rebuild 제외)

이 문서를 작성할 때(2026-10-13) 확인한 현재 구조는 다음과 같다. 구현 전에 다시 확인한다.

| 대상 | 현재 상태 |
| --- | --- |
| `IRandomIndexProvider` (`Assets/TxTRPG/Gameplay/Runtime/Common/RandomIndexProvider.cs`) | `int NextIndex(int exclusiveUpperBound)` 하나뿐인 공용 난수 계약이다. `SystemRandomIndexProvider`가 구현한다. 실수 난수 계약은 없다. |
| `SlotMachine.Spin` (`Assets/TxTRPG/Gameplay/Runtime/SlotMachine/SlotMachine.cs`) | Reel 순서대로 `NextIndex(reel.Count)`를 한 번씩 호출하고 `SlotSpinResult`를 만든다. `PaylineTotal`은 `checked` int다. |
| `TemporarySlotCombatController.AcceptSpin` (`Assets/TxTRPG/Application/Runtime/SlotMachine/TemporarySlotCombatController.cs`) | `Spin(random)` → `TemporaryRunes.Allocate(spin.PaylineTotal)` → `TemporarySlotAcceptedAction`을 만들어 `AcceptTemporarySlotAction`으로 세션에 보관한다. 예외가 나면 `TryExecute`가 잠금을 풀고 실패로 처리한다. 테스트는 `SetRandomForTests`로 난수를 주입한다. |
| 수락·턴 기록 (`TemporarySlotCombatRecords.cs`) | `TemporarySlotAcceptedAction`과 `TemporarySlotTurnRecord`가 `SlotSpinResult`와 `RuneAllocation`을 가진다. `SlotCombatStoryFormatter.Spin`이 `[슬롯] 3 + 5 + 2 = 10`을 만든다. |
| `CreateSpinDisplay(SlotSpinResult, RuneAllocation)` | `SlotReelStopDisplay`(ReelId, 정지 인덱스, Strip 값)와 회전 중·정지 후 `SlotSummaryDisplay`를 만든다. 정지 후 합계 값은 `spin.PaylineTotal`이다. Scene 재로드 복원(`PendingSlotAction`, `LastTemporarySlotTurn`)도 이 메서드를 쓴다. |
| 표시 데이터 (`Assets/TxTRPG/UI/Runtime/SlotMachine/SlotMachineDisplays.cs`) | UI 어셈블리는 Gameplay·Application 타입을 모른다. 모든 문구는 Application이 만든다. |
| `SlotReelViewBase` | 추상 Reel 표시 계약이다. 스킨처럼 선택 기능은 기본 구현이 비어 있는 가상 메서드(`ApplySkin`)로 추가한 선례가 있다. `SlotReelView`에는 Window 밖의 `EffectRoot`가 있다. |
| `PlayerSessionHost` (`Assets/TxTRPG/Application/Runtime/Players/PlayerSessionHost.cs`, `AppRoot.prefab`) | `temporarySlotMachineConfiguration`·`temporaryRuneConfiguration`을 Awake와 `EnsureInitializedAsync`에서 한 번 복사한다. 잘못된 설정은 게임 시작을 막지 않고 `TemporarySlotCombatError`에 사유를 남긴다. Editor용 `ConfigureTemporarySlotCombat(slots, runes)`가 있다. |
| 기존 테스트 | `TxTRPG.Gameplay.Tests.Editor`의 `SlotMachineTests`·`RuneTests`, `TxTRPG.Application.Tests.Editor`의 `SlotMachineCombatTests`, `TxTRPG.UI.Tests.Editor`의 `SlotMachinePanelTests`·`SlotMachinePanelSkinTests`·`SlotReelSpinPlannerTests` |

## 4. 계산 규칙

### 4.1 기호

| 기호 | 의미 |
| --- | --- |
| `vᵢ` | Reel i의 Payline 값 (`SlotReelStop.PaylineValue`) |
| `rᵢ` | Reel i의 Fortune 배수. 발생하지 않으면 1 |
| `t` | 합계 Fortune 배수. 발생하지 않으면 1 |
| `C` | 유효 배수 상한 (`maximumEffectiveMultiplier`, 기본 30) |
| `eᵢ` | Reel i의 유효 배수 `min(C, rᵢ × t)` |
| `fᵢ` | Reel i의 최종 값 `vᵢ × eᵢ` |
| `F` | 최종 합계 `Σ fᵢ`. 룬 분배와 전투에는 이 값을 쓴다. |

상한이 적용되지 않으면 `F = t × Σ(vᵢ × rᵢ)`이므로 "Reel 배수를 적용해 더한 뒤 합계 배수를 곱한다"는 설명과 같다. 상한은 Reel별 유효 배수에만 적용하므로, 어떤 Reel 값이 몇 배가 되었는지 표시와 기록이 항상 일치한다.

예: `v = (3, 5, 2)`, Reel 2에서 ×10, 합계에서 ×5, `C = 30`이면 `e = (5, 30, 5)`, `f = (15, 150, 10)`, `F = 175`이다. 상한이 없었다면 Reel 2는 ×50이었다.

### 4.2 판정 순서와 난수

판정은 `SlotMachine.Spin`이 끝난 뒤 별도 단계에서 한다. `Spin`의 난수 사용은 바꾸지 않는다.

1. Reel을 결과 순서대로 하나씩 판정한다. 각 Reel은 자기 규칙(5절)으로 발생 여부를 뽑고, 발생했으면 배수를 뽑는다.
2. 합계를 합계 규칙으로 같은 방법으로 판정한다.
3. 4.1의 식으로 결과를 만든다.

확률은 `IRandomIndexProvider` 하나로 뽑는다. 실수 난수 계약을 새로 만들지 않는다. 해상도는 `FortuneCriticalProbability.Resolution = 1_000_000`(백만분율)이다.

- 발생 판정: 임계값 `k = round(p × 1_000_000)`을 미리 계산하고 `NextIndex(1_000_000) < k`이면 발생이다. `k = 0`이면 난수를 뽑지 않고 발생하지 않는다. `k = 1_000_000`이면 난수를 뽑지 않고 발생한다.
- 배수 판정: 배수별 확률을 백만분율 정수표로 바꾼 누적표에서 `NextIndex(1_000_000)`으로 고른다(6.3절). 최소 배수와 최대 배수가 같으면 난수를 뽑지 않는다.

이 규칙 덕분에 테스트는 순서 난수로 결과를 정확히 지정할 수 있고, 확률 0인 설정은 난수를 소비하지 않는다.

Fortune Critical 판정 난수는 회전 난수와 분리한다. `TemporarySlotCombatController`는 `fortuneRandom` 필드(기본 `SystemRandomIndexProvider`)를 따로 두고, 테스트 주입용 `SetFortuneRandomForTests(IRandomIndexProvider)`를 추가한다. 그래서 기존 `SetRandomForTests` 테스트의 난수 호출 순서가 바뀌지 않는다.

### 4.3 범위와 오버플로

- 배수 범위: `2 ≤ 최소 ≤ 최대 ≤ FortuneCriticalLimits.MaximumMultiplier`. 절대 상한 상수는 100이다. 사용자가 생각하는 범위(최대 10)보다 넓게 두어 나중에 상한을 올릴 수 있게 한다.
- 유효 배수 상한: `1 ≤ C ≤ FortuneCriticalLimits.MaximumMultiplier × FortuneCriticalLimits.MaximumMultiplier`. `C`가 Reel 최대 배수보다 작아도 거부하지 않지만, Inspector 미리보기에 경고를 표시한다.
- 최종 값 계산은 `long`과 `checked`로 한다. 최종 합계가 `int` 범위를 넘으면 `OverflowException`을 던진다. 컨트롤러의 기존 수락 실패 경로(오류 기록, 잠금 해제, 상태 변경 없음)가 그대로 처리한다. 이 경우를 테스트한다.

## 5. Reel별 설정과 규칙 선택

규칙 집합 `FortuneCriticalRules`는 다음으로 이루어진다.

| 구성 | 의미 |
| --- | --- |
| `DefaultReelRule` | 개별 규칙이 없는 Reel에 쓰는 규칙 |
| `ReelRules` | Reel ID → 규칙. 순서 있는 목록으로 보관한다(Inspector와 결정적 직렬화를 위해). |
| `TotalRule` | 합계 규칙 |
| `MaximumEffectiveMultiplier` | 유효 배수 상한 `C` |

규칙 하나(`FortuneCriticalRule`)는 발생 확률 `Chance`(0~1), `MinimumMultiplier`, `MaximumMultiplier`, 배수 분포 `Distribution`(6절)을 가진다. 발생을 끄려면 `Chance = 0`으로 둔다. 별도의 켜기·끄기 값을 두지 않는다.

Reel 규칙 선택은 다음을 따른다.

- 판정할 때 Reel의 `ReelId`로 `ReelRules`를 찾고, 없으면 `DefaultReelRule`을 쓴다.
- 실행 중 `TryAddReel`로 추가된 Reel은 자기 ID의 개별 규칙이 있으면 그것을, 없으면 기본 규칙을 쓴다. 따라서 Reel 추가 때 Fortune 설정을 맞출 필요가 없다.
- 아직 없는 Reel ID의 개별 규칙도 허용한다. 나중에 그 ID의 Reel이 추가되면 그때부터 쓰인다. Inspector 미리보기는 현재 `TemporarySlotMachineConfiguration`에 없는 ID를 정보 문구로 표시하되 오류로 취급하지 않는다.
- Reel이 제거되어도 개별 규칙은 지우지 않는다.
- Reel ID는 기존 Reel ID와 같은 규칙(앞뒤 공백 제거, 대소문자 구분, `StringComparison.Ordinal`)으로 비교한다. 빈 ID와 중복 ID는 거부한다.

판정은 수락 시점에 한 번만 한다. 수락 뒤에 규칙이나 Reel 구성이 바뀌어도 이미 수락한 결과와 그 표시는 바뀌지 않는다.

## 6. 배수 분포와 가중치 함수

### 6.1 함수 계약

Gameplay에 가중치 함수 계약을 둔다. Unity에 의존하지 않는다.

```csharp
public interface IFortuneWeightFunction
{
    string Id { get; }                       // 안정적인 ID. 설정 자산에 저장되므로 바꾸지 않는다.
    string DisplayName { get; }              // Inspector 표시용
    IReadOnlyList<FortuneFunctionParameter> Parameters { get; }  // 이름, 기본값, 최소, 최대, 설명
    double Weight(int multiplier, int minimumMultiplier, int maximumMultiplier, FortuneFunctionArguments arguments);
}
```

- `Weight`는 음수가 아니고 유한한 값을 돌려줘야 한다. 정규화는 분포 생성기가 한다. 함수는 합이 1이 될 필요가 없다.
- 매개변수는 이름으로 찾는다. 설정 자산은 `(이름, 값)` 목록으로 저장하므로 함수 안에서 매개변수 순서가 바뀌어도 기존 자산이 깨지지 않는다. 자산에 없는 매개변수는 함수가 정의한 기본값을 쓰고, 함수가 모르는 이름은 무시하되 Inspector에 경고를 표시한다.
- `FortuneWeightFunctionRegistry`가 ID → 함수를 보관한다. `FortuneWeightFunctionRegistry.Default`에 기본 함수를 명시적으로 등록한다. 리플렉션 검색이나 `[SerializeReference]` 다형 직렬화는 쓰지 않는다. IL2CPP 코드 제거와 클래스 이름 변경에 약하기 때문이다.
- 함수를 추가하는 방법은 "`IFortuneWeightFunction` 구현 클래스 하나 추가 + `Default` 등록 한 줄"이어야 한다. 이 절차를 아키텍처 문서에 적는다.

### 6.2 기본 함수 목록

`x = n - 최소`, `L = 최대 - 최소`로 둔다. 모든 함수는 `n`이 최소 배수에서 최대 배수까지인 정수에서만 평가한다.

| ID | 이름 | 가중치 `w(n)` | 매개변수 (기본값, 범위) | 성격 |
| --- | --- | --- | --- | --- |
| `geometric` | 기하(지수) | `r^x` | `ratio` r (0.7, 0 초과 1 이하) | 한 단계 오를 때마다 확률이 r배가 된다. 기본 함수 |
| `power` | 거듭제곱 | `n^(-s)` | `exponent` s (2, 0 이상 8 이하) | 낮은 배수에 몰리고 꼬리가 길다. 대박 체감이 강하다. |
| `half-normal` | 반정규 | `exp(-x² / (2σ²))` | `sigma` σ (3, 0.1 이상 50 이하) | 낮은 배수는 비슷하다가 높은 배수에서 급격히 줄어든다. |
| `linear` | 선형 | `1 + (e - 1) × x / L` (`L = 0`이면 1) | `endWeight` e (0.1, 0 이상 1 이하) | 최소 배수 가중치 1에서 최대 배수 가중치 e까지 직선으로 줄어든다. |
| `equal-expected` | 기대 피해 균등 | `1 / (n - 1)` | 없음 | 배수마다 추가 기대 피해 `(n-1)·P(n)`가 같다. |
| `uniform` | 균등 | `1` | 없음 | 모든 배수가 같은 확률이다. 비교·검증용이다. |

함수 대신 배수별 가중치를 직접 적는 **표 분포**도 지원한다. 분포의 종류를 `FortuneDistributionSource.Function` 또는 `Table`로 정한다. 표는 최소 배수부터 차례로 가중치를 적으며, 길이는 `최대 - 최소 + 1`과 같아야 한다. 함수로 표현하기 어려운 곡선은 표로 만든다.

### 6.3 정규화와 정수 확률표

`FortuneMultiplierTable.Create(rule, registry)`가 다음 순서로 분포를 만든다.

1. 각 배수의 가중치를 계산한다. 음수, NaN, 무한대가 하나라도 있으면 실패한다.
2. 합이 0이면 실패한다.
3. 확률 `wₙ / Σw`를 백만분율로 바꾸고 최대 나머지 방식으로 합이 정확히 1,000,000이 되게 한다. 나머지가 같으면 낮은 배수가 먼저 받는다. 기존 `WeightedRuneAllocationPolicy`와 같은 결정적 규칙이다.
4. 가중치가 0보다 큰데 백만분율이 0이 된 배수는 결과적으로 나오지 않는다. 분포 생성은 성공시키고 Inspector 미리보기에 경고를 표시한다.

규칙이 만들어질 때 이 표를 미리 계산해 둔다. 판정 중에는 할당과 실수 계산을 하지 않는다.

## 7. 구현 요구

### 7.1 Gameplay (`Assets/TxTRPG/Gameplay/Runtime/SlotMachine/FortuneCritical/`)

네임스페이스는 `TxTRPG.Gameplay.SlotMachine`을 쓴다. 파일은 새 하위 폴더에 둔다. Unity API에 의존하지 않는다.

| 타입 | 책임 |
| --- | --- |
| `FortuneCriticalLimits` | 절대 상한(100), 확률 해상도(1,000,000) 상수 |
| `IFortuneWeightFunction`, `FortuneFunctionParameter`, `FortuneFunctionArguments` | 6.1절 계약. 인자는 불변 이름→값 사전이다. |
| 기본 함수 클래스 6개, `FortuneWeightFunctionRegistry` | 6.2절. 레지스트리는 중복 ID를 거부한다. |
| `FortuneMultiplierDistribution` | 함수 ID + 인자, 또는 가중치 표. 불변이다. |
| `FortuneMultiplierTable` | 6.3절의 정수 확률표. 배수별 확률, 기댓값 `E[n]`을 제공한다(미리보기용). |
| `FortuneCriticalRule` | 확률, 최소·최대 배수, 분포. `TryCreate(..., registry, out rule, out reason)`으로 검증하고 확률표를 미리 만든다. 발생 기대 배수 `1 + p × (E[n] - 1)`를 제공한다. |
| `FortuneCriticalRules` | 5절 규칙 집합. 불변이다. `RuleForReel(reelId)`를 제공한다. `With...` 메서드는 검증한 새 인스턴스를 돌려준다. `Disabled`(모든 확률 0) 정적 값이 있다. |
| `FortuneCriticalResolver` | `Resolve(SlotSpinResult, FortuneCriticalRules, IRandomIndexProvider) → FortuneCriticalResult`. 4.2절 순서를 따른다. |
| `FortuneCriticalResult`, `FortuneCriticalReelResult` | 불변 스냅샷. Reel마다 `ReelId`, `BaseValue`, `ReelMultiplier`, `EffectiveMultiplier`, `FinalValue`, `IsCapped`를 가진다. 전체는 `TotalMultiplier`, `BaseTotal`(= `PaylineTotal`), `FinalTotal`, `HasAnyCritical`, `IsAnyCapped`를 가진다. `FortuneCriticalResult.None(spin)`은 모든 배수가 1인 결과다. |

`SlotSpinResult`와 `SlotMachine.Spin`은 바꾸지 않는다. Fortune 결과는 회전 결과를 감싸는 별도 스냅샷이다.

### 7.2 Application

| 대상 | 요구 |
| --- | --- |
| `TemporaryFortuneCriticalConfiguration` (`Assets/TxTRPG/Application/Runtime/SlotMachine/`) | ScriptableObject. 만들기 메뉴 `Assets > Create > TxT RPG > Temporary Fortune Critical Configuration`. 필드: `defaultReelRule`, `reelRules`(Reel ID + 규칙 목록), `totalRule`, `maximumEffectiveMultiplier`. 규칙의 직렬화 형태는 `chance`(Range 0~1), `minimumMultiplier`, `maximumMultiplier`, `distributionSource`(Function/Table), `functionId`(문자열), `parameters`(이름·값 목록), `tableWeights`(float 목록)이다. 기본값은 2절 표를 따른다. `TryCreateRules(registry, out rules, out reason)`으로 Gameplay 규칙을 만든다. `OnValidate`는 이 자산의 값만 범위로 보정하며 다른 자산이나 Scene을 건드리지 않는다. |
| 설정 자산 | `Assets/TxTRPG/Application/Configuration/TemporaryFortuneCriticalConfiguration.asset`를 기본값으로 만든다. |
| `TemporaryFortuneCriticalState` | 세션 수명 상태. 현재 `FortuneCriticalRules`를 가진다. 변경 API: `TrySetDefaultReelRule`, `TrySetReelRule(reelId, rule)`, `TryRemoveReelRule(reelId)`, `TrySetTotalRule`, `TrySetMaximumEffectiveMultiplier`, `TryReplaceAll(rules)`. 확률만 바꾸는 편의 API `TrySetDefaultReelChance`, `TrySetReelChance(reelId, chance)`, `TrySetTotalChance`도 둔다. 모두 후보를 검증한 뒤에만 반영하고 `Changed`를 한 번 발생시키며, 실패하면 이전 규칙을 유지하고 사유를 돌려준다. 같은 값으로 바꾸면 `Changed`를 발생시키지 않는다. |
| `PlayerSessionHost` | 직렬화 필드 `temporaryFortuneCriticalConfiguration`과 읽기 속성 `TemporaryFortuneCritical`을 추가한다. 슬롯머신·룬과 같은 시점에 한 번 복사한다. Editor용 `ConfigureTemporaryFortuneCritical(configuration)`을 기존 `ConfigureTemporarySlotCombat`과 별도로 추가해 기존 테스트 호출을 바꾸지 않는다. |
| `TemporarySlotCombatController.AcceptSpin` | `Spin` → `FortuneCriticalResolver.Resolve(spin, rules, fortuneRandom)` → `Allocate(result.FinalTotal)` 순서로 바꾼다. 4.2절의 `fortuneRandom`·`SetFortuneRandomForTests`를 추가한다. |
| 수락·턴 기록 | `TemporarySlotAcceptedAction`과 `TemporarySlotTurnRecord`에 `FortuneCriticalResult Fortune`을 추가한다. 기존 생성자는 `FortuneCriticalResult.None(spin)`을 쓰는 오버로드로 유지한다. Scene 재로드 복원 경로도 저장된 Fortune 결과로 표시한다. |
| `CreateSpinDisplay` | `(SlotSpinResult, FortuneCriticalResult, RuneAllocation)` 오버로드를 추가하고 정지 후 합계 값은 `FinalTotal`로 만든다. 기존 2인자 오버로드는 `None`으로 위임한다. |
| `SlotCombatStoryFormatter` | 8절 문구를 만든다. 시스템 이름 `FortuneCriticalName = "행운 치명"`을 상수로 둔다. |

실패 처리는 다음과 같다. Fortune Critical은 선택 기능이므로 슬롯머신 전투를 막지 않는다.

| 상황 | 동작 |
| --- | --- |
| 설정 자산 참조 없음 | `FortuneCriticalRules.Disabled`로 상태를 만들고 `Debug.LogWarning`을 한 번 남긴다. 전투는 Fortune 없이 동작한다. |
| 설정이 잘못됨(알 수 없는 함수 ID, 범위 밖, 표 길이 불일치, 가중치 합 0 등) | `Disabled`로 상태를 만들고 사유와 함께 `Debug.LogError`를 남긴다. `TemporarySlotCombatError`는 설정하지 않는다. |
| 실행 중 잘못된 변경 | 해당 `Try...`가 `false`와 사유를 돌려주고 이전 규칙을 유지한다. |
| 판정 중 오버플로 | 4.3절대로 수락 실패로 처리한다. |

### 7.3 UI

UI 어셈블리는 Gameplay·Application 타입을 모르는 현재 원칙을 지킨다.

| 대상 | 요구 |
| --- | --- |
| `SlotReelStopDisplay` | 선택 속성 `MultiplierText`(빈 문자열이면 표시 없음)를 추가한다. 기존 생성자는 빈 문자열로 동작한다. |
| `SlotSummaryDisplay` | 선택 속성 `TotalMultiplierText`를 추가한다. 기존 생성자는 빈 문자열로 동작한다. |
| `SlotReelViewBase` | 가상 메서드 `ShowFortuneMultiplier(string text)`를 추가한다. 기본 구현은 비어 있어 다른 Reel 구현도 동작한다. |
| `SlotReelView` | Reel이 결과 칸에 멈춘 뒤(`FinishAt`, `ShowStop`, `StopAtResult`) 그 Reel의 배수 텍스트를 보여 준다. 회전이 시작되면 이전 배수 표시를 지운다. Reduce Motion에서도 결과를 보여 주는 시점에 표시한다. 중단·비활성 시에는 결과 상태 표시 규칙을 그대로 따른다. |
| `SlotMachinePanel` | 정지 후 요약을 적용할 때 합계 배수 텍스트를 보여 주고, 회전 중 요약에서는 지운다. |

배수 표시의 계층과 배치는 다음을 따른다.

- `SlotReel.prefab`: Window 밖(마스크 밖)에 `FortuneBadge`(배치 루트) > `VisualRoot` > `Text`(TMP)를 추가한다. 배치 루트는 Payline 칸의 오른쪽 위 모서리에 걸치게 두고, Payline 숫자의 가운데를 가리지 않는다. 글자 크기는 Payline 칸 높이 × 비율(기본 0.38)을 최소 12, 최대 36으로 제한하고, 크기가 바뀔 때만 다시 계산한다.
- `SlotMachinePanel.prefab`: `TotalBox` 안 `TotalValue` 옆(좌우 배치) 또는 위(상하 배치)에 `TotalMultiplier`(배치 루트) > `VisualRoot` > `Text`를 추가한다. 합계 값 글자를 가리거나 밀어내지 않아야 한다.
- 텍스트는 `×3` 형식이며 Application이 만든다. 색만으로 구분하지 않고 글자 자체로 전달한다. 색은 스킨 텍스트 대상과 같은 방식으로 기본값을 기록하고, 스킨 확장 필드(`fortuneTextColor`)는 선택 사항이다. 추가하면 기존 스킨 자산이 빈 값으로 기본 외형을 유지해야 한다.
- 모든 새 Graphic은 `raycastTarget`을 끈다. 공유 Material을 바꾸지 않는다.
- 효과는 넣지 않는다. 나중에 효과가 `VisualRoot`의 크기·회전·알파를 바꿔도 배치 루트와 Reel 레이아웃이 흔들리지 않게 한다.
- 텍스트는 TMP Auto Size를 쓰지 않는다. 문자열 할당은 표시가 바뀔 때만 하며, 회전 중 매 프레임 할당하지 않는다.

Prefab은 개발자 소유 자산이다. 전체 재생성하지 말고 필요한 객체와 참조만 추가한다. Editor 자동화로 적용하거나, 다시 실행해도 안전한 임시 적용 메뉴 `Tools > TxT RPG > Application > Temporary > Apply Slot Fortune Critical`을 만든다. 메뉴는 없을 때만 설정 자산을 만들고, `AppRoot.prefab`의 빈 `temporaryFortuneCriticalConfiguration` 참조를 채우고, 두 Prefab에 표시 객체가 없을 때만 추가하고 기본 외형을 기록한다. 이미 원하는 상태면 파일을 쓰지 않는다. 열린 Scene에 저장되지 않은 변경이 있으면 중단한다. 이 메뉴는 등록 기반 Prefab Rebuild에 등록하지 않으며, 제외 사유는 기존 슬롯머신 메뉴와 같다.

### 7.4 Inspector 미리보기

`TemporaryFortuneCriticalConfiguration`의 Inspector(`Assets/TxTRPG/Application/Editor/`의 사용자 지정 Editor)에서 규칙마다 다음을 읽기 전용으로 보여 준다. 계산은 Gameplay의 `FortuneMultiplierTable`·`FortuneCriticalRule`을 그대로 써서 런타임과 같은 값을 보여 준다.

- 함수 선택: 레지스트리에 등록된 함수의 `DisplayName` 목록. 선택하면 그 함수의 매개변수 입력란을 이름·범위·설명과 함께 보여 준다.
- 배수별 확률(백분율, 소수 둘째 자리)과 간단한 막대 표시
- 발생 시 평균 배수 `E[n]`, 발생 확률을 포함한 기대 배수 `1 + p(E[n] - 1)`
- Reel 기본 규칙과 합계 규칙을 모두 반영한 대략적 기대 배수 `E[M_reel] × E[M_total]`(상한 무시, 그렇다는 문구 포함)
- 경고: 검증 실패 사유, 백만분율 0이 된 배수, `C`가 최대 배수보다 작음, 알 수 없는 매개변수 이름, 현재 슬롯머신 설정에 없는 Reel ID(정보)

## 8. Story 문구

| 경우 | 예 |
| --- | --- |
| Fortune 없음 | `[슬롯] 3 + 5 + 2 = 10` (기존과 같음) |
| Reel만 발생 | `[슬롯] 3 + 5×3 + 2 = 20` |
| 합계만 발생 | `[슬롯] (3 + 5 + 2) ×2 = 20` |
| 둘 다 발생 | `[슬롯] (3 + 5×3 + 2) ×2 = 40` |
| 발생 요약 줄 (하나라도 발생 시) | `[행운 치명] 2번 Reel ×3, 합계 ×2` |
| 상한 적용 줄 | `[행운 치명] 최대 배수 ×30 적용: 2번 Reel (×50 → ×30)` |

상한이 적용되면 `[슬롯]` 줄의 마지막 값은 상한을 적용한 최종 합계다. Reel 번호는 결과 순서(1부터)를 쓴다. 룬 줄 기록은 바뀌지 않으며 최종 합계를 분배한 값을 보여 준다. 문구는 `SlotCombatStoryFormatter`에만 두어 나중에 현지화 공급자로 바꿀 수 있게 한다.

## 9. 호환성

- 저장 데이터, `PlayerState`, 기존 설정 자산의 형식은 바뀌지 않는다. 새 자산과 새 직렬화 필드만 추가한다.
- `AppRoot.prefab`의 새 필드가 비어 있어도 게임은 Fortune 없이 동작한다.
- 기존 `SlotMachineCombatTests`는 Fortune 설정을 지정하지 않으면 `Disabled`로 동작하므로 결과가 바뀌지 않아야 한다. 바뀌는 테스트가 있으면 원인을 확인하고 보고한다.
- `SlotSpinResult`, `IRandomIndexProvider`, `IRuneAllocationPolicy`의 공개 계약은 바꾸지 않는다.
- 주사위 경로는 바뀌지 않는다.

## 10. 검증 기준

Edit Mode 테스트는 관련 클래스만 이름으로 지정해 실행한다. 전체 Suite와 `AssetWriting` 범주는 실행하지 않는다(`!AssetWriting`). 실행 전 Git 상태를 기록한다.

`TxTRPG.Gameplay.Tests.Editor`에 `FortuneCriticalTests`를 추가한다.

- 각 기본 함수의 가중치를 배수 2~10에서 기대값과 비교한다(11절 표의 확률과 소수 첫째 자리까지 일치).
- 정수 확률표의 합이 정확히 1,000,000이고, 같은 입력이면 같은 표가 나온다. 나머지가 같으면 낮은 배수가 먼저 받는다.
- 확률 0은 난수를 소비하지 않고, 확률 1은 발생 판정 난수를 소비하지 않으며, 최소 = 최대이면 배수 난수를 소비하지 않는다.
- 순서 난수로 Reel 배수·합계 배수·상한 적용을 정확히 지정해 4.1절 예(`F = 175`)를 재현한다.
- Reel 개별 규칙 선택, 개별 규칙이 없는 Reel의 기본 규칙 사용, 아직 없는 ID의 규칙 허용, 빈·중복 ID 거부
- 잘못된 설정 거부: 범위 밖 확률·배수, 알 수 없는 함수 ID, 표 길이 불일치, 가중치 합 0, NaN
- 오버플로 시 `OverflowException`
- 고정 시드 `SystemRandomIndexProvider`로 100,000회 판정한 빈도가 기대 확률과 ±0.5%p 안에 있는지 확인하는 표본 검사 1개
- 레지스트리에 테스트 전용 함수를 등록해 분포를 만드는 확장 검사

`TxTRPG.Application.Tests.Editor`의 `SlotMachineCombatTests`를 확장한다(또는 `SlotFortuneCriticalCombatTests`를 추가한다).

- 회전·Fortune·분배 순서와 최종 합계 분배, `ExecuteTurn` 1회 호출
- Story 문구 6가지 경우
- 설정 없음·잘못된 설정에서 Fortune 없이 전투가 진행되고 경고·오류가 기록됨
- 실행 중 변경 API의 성공·실패·`Changed` 발생, 수락 뒤 변경이 수락된 결과에 영향을 주지 않음
- 실행 중 `TryAddReel`로 추가한 Reel이 개별 규칙 또는 기본 규칙을 씀
- Scene 재로드 복원 표시가 저장된 Fortune 결과를 씀
- 설정 자산 `OnValidate` 보정

`TxTRPG.UI.Tests.Editor`의 `SlotMachinePanelTests`를 확장한다.

- 배수 표시가 Reel 정지 후에만 나타나고, 다음 회전 시작과 `StopAtResult`·Reduce Motion에서 올바르게 바뀜
- 빈 배수 텍스트일 때 표시가 없음, 새 Graphic의 `raycastTarget`이 꺼짐
- 스킨 교체 후에도 표시가 유지됨

Play Mode 확인은 `AppScene`을 거쳐 `TMP_MainScene`에서 한다.

- 임시로 Reel 확률을 1로 올린 세션 상태 API 호출(또는 테스트용 순서 난수)로 Reel·합계 배수 표시와 Story 문구를 확인한다. 확인 뒤 기본값으로 되돌리고 자산을 저장하지 않는다.
- 1920×1080, 21:9, 1024×768, 390×844에서 배수 표시가 Payline 숫자, 합계 값, 행동 버튼과 겹치지 않는다.
- 키보드 `Submit`, 포인터 클릭, Reduce Motion으로 실행한다.
- 적용 메뉴나 Editor 작업이 `.unity`, `.prefab`, `.asset`, `.meta` 파일을 바꿨으면 `serialization-diff-reviewer`로 의도하지 않은 변경을 확인한다.

모바일·Steam 실제 기기 빌드는 이 작업에서 확인하지 않는다면 미확인으로 보고한다.

## 11. 부록: 배수 분포 함수 조사와 분석

### 11.1 요구를 수치로 옮기기

사용자 요구는 "낮은 배수가 자주, 최대 배수는 드물게, 변화는 완만하게"이다. 이것을 다음 세 지표로 비교한다.

- `P(×2)`: 가장 흔한 결과의 확률
- `P(×10)`: 최대 배수의 확률
- `E[n]`: 발생했을 때의 평균 배수. 실제 전투 밸런스에 가장 직접 영향을 준다.

### 11.2 후보 비교 (n = 2~10, 발생했을 때의 조건부 확률)

| 함수 | P(×2) | P(×3) | P(×5) | P(×10) | P(×6 이상) | E[n] |
| --- | --- | --- | --- | --- | --- | --- |
| 기하 r=0.6 | 40.4% | 24.2% | 8.7% | 0.68% | 12.1% | 3.41 |
| **기하 r=0.7 (기본)** | 31.3% | 21.9% | 10.7% | 1.80% | 20.8% | 3.95 |
| 기하 r=0.8 | 23.1% | 18.5% | 11.8% | 3.88% | 31.8% | 4.60 |
| 거듭제곱 s=1.5 | 35.5% | 19.3% | 9.0% | 3.18% | 23.6% | 4.04 |
| 거듭제곱 s=2 | 45.5% | 20.2% | 7.3% | 1.82% | 15.7% | 3.51 |
| 거듭제곱 s=3 | 63.3% | 18.7% | 4.0% | 0.51% | 6.0% | 2.78 |
| 반정규 σ=2 | 33.3% | 29.4% | 10.8% | 0.01% | 6.4% | 3.30 |
| 반정규 σ=3 | 23.6% | 22.3% | 14.3% | 0.67% | 21.0% | 4.06 |
| 선형 (e→0, `11-n`) | 20.0% | 17.8% | 13.3% | 2.22% | 33.3% | 4.67 |
| 기대 피해 균등 `1/(n-1)` | 35.3% | 17.7% | 8.8% | 3.93% | 26.4% | 4.18 |
| 균등 | 11.1% | 11.1% | 11.1% | 11.11% | 55.6% | 6.00 |

### 11.3 분석

- **지수함수는 이산 기하 분포로 쓰는 것이 낫다.** 배수가 정수이므로 연속 지수함수 `e^(-λx)`를 샘플링해 반올림하면 경계 배수의 확률이 반올림 방식에 따라 달라진다. 기하 분포 `r^x`는 같은 모양이면서 정규화가 정확하고, 매개변수 r이 "한 단계 오를 때 확률이 r배"라는 직관적인 의미를 가진다. `λ = -ln r`이므로 두 표현은 같은 곡선이다. 사용자가 생각한 방향은 이 형태로 유지하는 것이 가장 단순하다.
- **완만함은 r로 조절한다.** r=0.7은 이웃 배수 간 비율이 일정해 변화가 완만하고, ×10이 약 1.8%로 "드물지만 보이는" 수준이다. r을 0.8로 올리면 고배수가 두 배 이상 흔해지고, 0.6으로 내리면 ×10은 1% 미만이 된다.
- **거듭제곱(멱법칙)은 "대박" 체감에 유리하다.** ×2에 많이 몰리지만 꼬리가 기하 분포보다 천천히 줄어든다. s=2는 r=0.7과 ×10 확률이 비슷하면서 ×2가 더 흔하고 평균이 낮다. 즉 평소에는 소소하고 가끔 크게 터지는 느낌을 원할 때 고른다.
- **반정규는 최대 배수를 강하게 억제한다.** 낮은 배수 구간이 평평해서 ×2와 ×3의 차이가 작고, 높은 배수에서 급격히 줄어든다. 상한 근처 결과를 거의 막고 싶을 때 적합하다.
- **기대 피해 균등 `1/(n-1)`은 밸런스 해석이 쉽다.** 배수 n이 기여하는 추가 기대 피해 `(n-1)·P(n)`이 모든 배수에서 같다. 따라서 최대 배수를 10에서 15로 늘리면 추가 기대 피해가 정확히 그 단계 수만큼 늘어나는 식으로 예측할 수 있다. 다만 ×10이 약 3.9%로 기본 함수보다 두 배 흔하다.
- **선형과 균등은 비교·검증 기준으로 둔다.** 선형은 고배수가 상대적으로 흔하고, 균등은 요구와 반대이다.
- **표 분포는 모든 함수의 대안이다.** 디자이너가 특정 배수를 의도적으로 튀게 만들고 싶을 때(예: ×7을 특별 연출 배수로) 함수로는 표현하기 어렵다.
- **곡선의 모양보다 발생 확률 p가 밸런스에 더 크게 작용한다.** 기대 배수는 `1 + p(E[n] - 1)`이다. 기본값(Reel p=5%, 합계 p=3%, r=0.7)에서 Reel 하나의 기대 배수는 약 1.148, 합계는 약 1.089이다. 서로 독립이고 상한을 무시하면 최종 합계의 기대값은 원래보다 약 25% 높다. 그래서 Inspector 미리보기에 기대 배수를 함께 보여 주도록 했다.
- **곱셈 중첩은 분산을 크게 늘린다.** Reel ×10과 합계 ×10이 함께 나올 확률은 기본값에서 Reel 하나당 약 `0.05 × 0.018 × 0.03 × 0.018 ≈ 4.9 × 10⁻⁷`로 매우 낮지만, 나오면 100배이다. 유효 배수 상한 `C`(기본 30)가 이 극단값을 막는다.

### 11.4 포함하지 않은 후보

| 후보 | 제외 이유 |
| --- | --- |
| Beta 분포를 [최소, 최대]로 사상 | 매개변수 두 개로 모양이 매우 유연하지만, 값의 의미가 직관적이지 않고 정수 구간에서 반올림 처리가 필요하다. 필요하면 6.1절 계약으로 나중에 추가한다. |
| 로지스틱(시그모이드) 감소 | "어느 배수까지는 비슷하다가 그 뒤 급감"하는 모양이다. 반정규와 역할이 겹친다. |
| `AnimationCurve` 직접 편집 | Unity 타입이라 Gameplay 어셈블리에 둘 수 없고, 곡선 샘플링이 반올림 문제를 만든다. 표 분포가 같은 역할을 한다. Editor에서 곡선을 그려 표로 굽는 도구는 나중에 추가할 수 있다. |

### 11.5 확장 방향 (미구현)

- **연속 실패 보정(PRD)**: 확률을 고정하지 않고, 발생하지 않을 때마다 조금씩 올리고 발생하면 초기화한다. 평균 발생률은 같으면서 "오래 안 나오는" 체감을 줄인다. 상태(누적 실패 횟수)가 필요하므로 `FortuneCriticalResolver`에 판정 상태 객체를 넘기는 오버로드로 추가한다.
- **게임 효과의 규칙 변경**: 룬·아이템·버프는 `TemporaryFortuneCriticalState`의 `Try...` API를 호출한다. 여러 효과가 동시에 확률을 바꾸려면 기본 규칙 위에 수정자(가산·곱셈)를 쌓는 계층이 필요하다. 이번 작업에서는 만들지 않는다.
- **Reel 기호와의 연계**: 특정 기호가 Payline에 오면 Fortune 확률이 오르는 규칙은 `SlotSpinResult`를 입력으로 받는 규칙 선택 함수로 추가할 수 있다.

## 12. 문서 갱신

- `DOCS/architecture/slot-machine-and-runes.md`: 전체 흐름 Mermaid에 Fortune 판정 단계를 추가하고, Fortune Critical 절(계산 규칙, 규칙 선택, 함수 목록과 추가 방법, 설정 자산, 세션 API, 실패 동작, 표시, Story 문구)을 추가한다. 확장 지점 표를 갱신한다.
- `DOCS/architecture/project-structure.md`: Gameplay `SlotMachine/FortuneCritical/` 폴더, Application의 새 설정 자산·상태, Editor 미리보기, 임시 적용 메뉴를 반영한다.
- `DOCS/development/workflows.md`의 `슬롯머신 전투 적용` 절: 적용 메뉴 행, 실행 중 변경 API 목록, 함수 추가 절차, Play Mode 확인 절차를 추가한다.
- `DOCS/instructions/README.md`: 이 지침서 행이 있는지 확인한다.

## 13. 최종 보고 형식

다음을 구분해 보고한다.

1. 구현 완료: 추가·변경한 타입과 파일
2. 적용 완료: Editor에서 이미 적용하고 저장한 자산·Prefab과 확인 방법
3. 적용 필요: 사용자가 실행해야 할 메뉴가 남았다면 `사용자가 수행할 적용 절차` 절과 복사 가능한 순서. 남은 절차가 없으면 없다고 명시한다.
4. 검증: 실행한 테스트 클래스와 결과, Play Mode에서 확인한 화면 크기와 입력 방식, 직렬화 변경 검토 결과
5. 미확인: 실제 모바일·Steam 빌드, 성능 측정 등 하지 않은 확인
6. 임시 밸런스 값(2절)을 그대로 두었는지, 바꿨다면 그 값
