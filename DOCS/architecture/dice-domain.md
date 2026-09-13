# 주사위 Gameplay 도메인

## 책임과 경계

`TxTRPG.Gameplay.Dice`는 Unity UI, GameObject와 Scene 수명에 의존하지 않는 동기식 순수 C# 도메인입니다. 주사위의 각 면은 효과 종류와 수치를 함께 보유하며, 실제 체력 변경과 시각 연출은 이 도메인의 책임이 아닙니다.

| 타입 | 책임 |
| --- | --- |
| `DiceEffectKind` | 저장과 콘텐츠에서 안정적으로 사용할 명시적 효과 식별자입니다. `Unknown=0`, `Attack=1`, `Heal=2`를 유지합니다. |
| `DiceFace` | 한 면의 `EffectKind`와 `Amount`를 보존하는 불변 값입니다. |
| `Dice` | 순서가 있는 면 구성과 수치 범위를 검증하고 원자적으로 변경하며 굴림을 수행합니다. |
| `IRandomIndexSource` | `[0, FaceCount)` 범위의 인덱스를 제공하는 주입 경계입니다. |
| `DiceRollResult` | 굴린 시점의 면 인덱스, 효과 종류와 수치를 보존하는 불변 스냅샷입니다. |

현재 실행 가능한 효과는 `Attack`과 `Heal`입니다. `Unknown` 또는 지원하지 않는 enum 값이 면 구성에 포함되면 도메인은 이를 공격이나 회복으로 추정하지 않고 거부합니다.

## 생성과 굴림

```csharp
var dice = new Dice(
    new[]
    {
        new DiceFace(DiceEffectKind.Attack, 1),
        new DiceFace(DiceEffectKind.Heal, 2),
        new DiceFace(DiceEffectKind.Attack, 3),
        new DiceFace(DiceEffectKind.Heal, 4)
    },
    minimumValue: 1,
    maximumValue: 4);

DiceRollResult result = dice.Roll(new SystemRandomIndexSource());
```

같은 효과와 수치를 가진 면이 중복되어도 각 인덱스는 독립적으로 추첨됩니다. 생성자와 교체 API는 입력 배열을 복사하므로 외부 변경이 내부 구성에 영향을 주지 않습니다.

`ResolveFace(faceIndex)`는 난수를 소비하지 않고 지정한 면에서 일반 굴림과 같은 결과를 생성합니다. 향후 3D 물리 판정기는 물리적으로 결정된 면 인덱스만 이 API에 전달해야 하며, 효과와 수치를 별도로 재계산해서는 안 됩니다.

## 런타임 변경과 오류 계약

- `SetFace`는 한 면을 변경합니다.
- `ReplaceFaces`는 현재 허용 범위 안에서 전체 면을 교체합니다.
- `SetAllowedRange`는 현재 면의 수치가 모두 새 범위에 포함될 때만 범위를 변경합니다.
- `ReplaceConfiguration`은 면과 범위를 모두 검증한 뒤 한 번에 교체합니다.

null·빈 면 목록, 역전된 범위, 범위 밖 수치, 미지원 효과와 잘못된 면 인덱스는 예외로 거부합니다. 검증 실패 전의 상태와 이미 반환된 `DiceRollResult`는 유지됩니다.

## 임시 플레이어 주사위 연결

`TemporaryDiceConfiguration`은 AppScene 수명의 `PlayerSessionHost`가 복사하여 소유하는 임시 D4/D6/D8 구성을 제공합니다. 각 기본 주사위는 공격부터 시작하여 공격과 회복이 교대로 배치되고, 수치는 1부터 면 개수까지 증가합니다. Inspector의 각 `Faces` 항목에서 `Effect Kind`와 `Amount`를 개별 설정할 수 있습니다.

기존 숫자형 기본 자산은 ID, 면 개수, 범위와 `1..N` 값이 모두 알려진 D4/D6/D8 기본 구성과 일치할 때만 생성기가 자동 변환합니다. 사용자 정의 숫자형 구성은 공격과 회복을 임의로 추정하지 않으며, 개발자가 각 효과를 명시할 때까지 생성기가 오류로 중단합니다.

`DiceRollStoryFormatter`는 효과 표시명을 한곳에서 관리합니다. 현재 한국어 기본 공급자는 공격, 회복과 알 수 없는 효과를 구분하며, 향후 로컬라이제이션 공급자로 교체할 수 있습니다. `TemporaryDiceRollMenuController`는 다음 형식으로 결과 사실만 StoryTextPanel에 누적합니다.

```text
[주사위] 1번 주사위 (D4): 공격 3
[주사위] 2번 주사위 (D6): 회복 4
```

TMP_MainScene에서는 `TemporaryDiceRollMenuController`가 이 결과를 임시 전투 상태에 전달합니다. 공격은 임시 적에게, 회복은 복사된 임시 플레이어 체력에 적용하며 운영 Player 저장 데이터는 변경하지 않습니다. 정식 효과 실행기와 3D 판정은 이 임시 연결과 별도로 구현해야 합니다.
