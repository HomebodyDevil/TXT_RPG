# 주사위 Gameplay 도메인

## 책임과 경계

`TxTRPG.Gameplay.Dice`는 Unity UI, GameObject와 Scene 수명에 의존하지 않는 동기식 순수 C# 도메인입니다. `Dice`는 순서가 있는 면 값과 허용 범위를 소유하고, 면 교체·범위 변경·굴림을 제공합니다.

| 타입 | 책임 |
| --- | --- |
| `Dice` | 면 구성과 허용 범위를 검증하고 원자적으로 변경하며 굴림을 수행합니다. |
| `IRandomIndexSource` | `[0, FaceCount)` 범위에서 균등한 인덱스를 제공하는 주입 경계입니다. |
| `SystemRandomIndexSource` | `System.Random.Next(exclusiveUpperBound)`를 사용하는 런타임 구현입니다. |
| `DiceRollResult` | 굴린 시점의 0 기반 면 인덱스와 값을 보존하는 불변 결과입니다. |

## 생성과 사용

```csharp
var dice = new Dice(new[] { 1, 1, 3, 5 }, 1, 5);
var random = new SystemRandomIndexSource();
DiceRollResult result = dice.Roll(random);
```

면의 개수는 `FaceValues.Count`와 같은 `FaceCount`로만 결정됩니다. 숫자가 중복되어도 각 면은 독립적으로 추첨됩니다. 생성자와 변경 API는 입력 컬렉션을 복사하므로 호출자나 다른 주사위가 내부 구성을 변경할 수 없습니다.

## 런타임 변경과 오류 계약

- `SetFaceValue`는 한 면을 변경합니다.
- `ReplaceFaces`는 현재 허용 범위 안에서 전체 면을 교체합니다.
- `SetAllowedRange`는 현재 면이 모두 새 범위에 포함될 때만 범위를 변경합니다.
- `ReplaceConfiguration`은 면과 범위를 함께 검증한 뒤 한 번에 교체합니다.

null·빈 면 목록, 역전된 범위, 범위 밖 값과 잘못된 면 인덱스는 인수 예외로 거부됩니다. 검증 실패 전의 상태는 유지됩니다. 난수 공급자가 요청 범위를 어긴 인덱스를 반환하면 `InvalidOperationException`으로 계약 위반을 알리며 보정하거나 재추첨하지 않습니다.

`Dice`와 `SystemRandomIndexSource`는 같은 실행 흐름에서 사용하도록 설계했으며 스레드 안전성을 보장하지 않습니다. 난수 공급자는 장기 수명으로 재사용하고 매 굴림마다 시간 기반 시드로 만들지 않습니다.

## 추후 연결 경계

현재 구현은 플레이어 소유, 장착, 저장 DTO, 인벤토리, 결과 합산, 재굴림, 턴 진행과 시각 연출을 포함하지 않습니다. 향후 콘텐츠 정의와 플레이어별 인스턴스 상태를 구분하고, 저장 가능한 면 구성 및 범위를 별도 버전 DTO로 연결해야 합니다. 표현 계층은 `DiceRollResult`를 받아 애니메이션과 UI를 표시하되 논리 결과를 다시 계산하지 않아야 합니다.

## 임시 플레이어 주사위 연결

`TemporaryDiceConfiguration`은 AppScene 수명의 `PlayerSessionHost`가 복사하여 소유하는 임시 D4/D6/D8 구성을 제공합니다. 이 상태는 콘텐츠 Scene 전환 동안 유지되지만 `PlayerSaveData`, Inventory와 QuickItem 저장에는 포함되지 않으며 실행을 다시 시작하면 설정 원본으로 초기화됩니다. `TemporaryPlayerDiceState.RollAll`은 세션 난수 공급자를 재사용하여 안정적인 보유 순서로 각 주사위를 한 번씩 굴립니다.

`TemporaryDiceRollMenuController`는 TMP_MainScene 초기화 순서 `-800`에서 PlayerSession과 Story 출력을 확인합니다. 결과 문구는 Application 표시 경계에서 `[주사위] N번 주사위 (D면수): 값`으로 만들며 Dice 도메인에는 사용자 표시 문자열이 들어가지 않습니다.
