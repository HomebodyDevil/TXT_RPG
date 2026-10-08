# 보유 주사위 던지기 연출과 결과 고정 물리 재생

## 1. 목적과 작업 범위

`OwnedDicePanel`의 주사위는 현재 정렬 위치에서 제자리 회전만 하다가 결과 자세로 멈춘다. 이 작업은 주사위 표시 영역 안의 바닥으로 주사위를 실제로 던져 벽과 바닥, 다른 주사위와 부딪히며 구르게 바꾼다. 결과는 지금처럼 연출 전에 기존 확률로 한 번 정하고, 주사위는 반드시 그 결과 면이 위를 향한 상태로 멈춰야 한다.

이 문서는 구현 지침이다. 아래의 신규 타입, 자산, 데이터 필드는 예정된 구조이며 아직 구현되었다고 간주하지 않는다. 이 문서를 작성하는 단계에서는 Scene, Prefab, 런타임 코드를 수정하지 않았다.

포함 범위는 다음과 같다.

- 격리된 물리 공간에서 던지기를 미리 계산하고, 결과 면이 위로 오도록 보정해서 재생하는 연출
- 비스듬히 내려다보는 카메라와 바닥, 표시 영역에 맞춘 보이지 않는 벽
- 다면체 형태의 "윗면 평행" 조건 검증과 조건을 만족하지 못하는 기본 D4의 교체
- 모션 감소, 실패, 타임아웃, 화면 크기 변경, 패널 숨김에 대한 동작
- 관련 테스트, 자산 적용, 문서 갱신

제외 범위는 다음과 같다.

- 물리 시뮬레이션의 정지 자세로 결과를 판정하는 방식. 판정은 계속 Gameplay의 확률 판정이 소유한다.
- 드래그·플릭 같은 새로운 던지기 입력. 던지기는 기존 Actions의 주사위 명령으로만 시작한다.
- 효과음, 진동, 파티클 같은 추가 피드백
- 보유 주사위·연출 상태의 영구 저장, 획득·제거 화면, 인게임 옵션 화면

이 문서는 [보유 주사위 3D 패널과 굴림 결과](owned-dice-3d-panel-and-roll-results.md)의 "4. 판정 방식 비교"에서 후속 후보로 남긴 "확률 판정 + 물리 연출 + 최종 자세 보정" 방식을 채택한다. 같은 문서의 행동 처리 순서, 결과 적용 1회 보장, 결과 모달과 기록 정책은 그대로 유지한다.

## 2. 사용자 확정 사항

| 항목 | 확정 요구 |
| --- | --- |
| 결과 판정 | 기존처럼 연출 전에 확률로 한 번 판정하고 결과를 고정한다. 연출은 고정된 결과를 보여주기만 한다. |
| 연출 방식 | 격리된 물리 공간에서 던지기를 미리 계산하고, 정지 시 위를 향한 면이 확정 결과가 되도록 모델 방향을 보정한 뒤 재생한다. 벽·바닥 튕김과 주사위끼리의 충돌을 표현한다. |
| 카메라 | 바닥을 비스듬히 내려다보는 시점으로 바꾼다. |
| 정지 후 배치 | 멈춘 위치와 방향을 다음 굴림이나 보유 목록 변경 전까지 유지한다. |
| 던지기 입력 | 기존 Actions의 주사위 명령만 사용한다. 방향과 세기는 연출용 난수로 정한다. |
| 윗면 조건 | 멈춘 모든 주사위에는 바닥과 평행하고 바닥에서 수직으로 위를 향하는 면이 있어야 하며, 그 면을 결과로 읽는다. |
| 조건 미충족 형태 | 조건을 만족하지 못하는 주사위는 조건을 만족하는 정다면체로 바꾼다. 현재 기본 D4(정사면체)가 해당한다. |
| D4 교체 | 기본 보유 D4를 정육면체로 바꾸고, 기존 4면(공1·회2·공3·회4)에 공5·회6을 추가한다. |
| 교체 주사위 이름 | 형태와 일치하는 새 식별자와 새 표시 이름을 사용한다. 이 문서는 식별자 `temporary.d6-secondary`, 표시 이름 `D6 (보조)`를 사용한다. |

D4 교체로 새 주사위의 면 구성은 기본 D6(`temporary.d6`)과 같아진다. 따라서 기본 보유는 같은 면 구성의 D6 두 개와 D8 한 개가 된다. 이것은 사용자가 확정한 결과이며 구현 중 임의로 수치를 바꾸지 않는다.

## 3. 선행 문서와 확인한 현재 구조

작업 전 `AGENTS.md`, `DOCS/README.md`와 아래 문서를 읽는다.

- `DOCS/architecture/owned-dice-presentation.md`
- `DOCS/architecture/dice-domain.md`
- `DOCS/architecture/temporary-combat.md`
- `DOCS/architecture/player-session.md`
- `DOCS/architecture/scene-transition.md`
- `DOCS/development/workflows.md`의 "보유 주사위 3D 패널과 결과 기록"
- `DOCS/instructions/owned-dice-3d-panel-and-roll-results.md`

문서 작성 시 저장된 소스와 자산에서 다음을 확인했다. 구현 직전 현재 작업 트리를 다시 확인하고, 다르면 실제 상태를 우선한다.

- `TemporaryDiceRollMenuController.TryExecute`는 `TemporaryDice.RollAll(random)`로 결과를 먼저 정하고 `CompleteAcceptedActionAsync`에서 연출을 기다린 뒤 `ExecuteTurn`을 한 번 호출한다. 결과 고정은 이미 구현되어 있으므로 이 흐름을 바꾸지 않는다.
- `AnimateOrFallbackAsync`는 `OwnedDicePanel.PlayRollAsync(DieRollDisplay[], reduceMotion, token)`을 `rollTimeoutSeconds`(1~15초로 제한)와 경쟁시킨다. 시간 초과, `false` 반환, 예외에서는 `ShowRollFallback`으로 텍스트를 표시하고 같은 결과를 적용한다.
- `OwnedDicePanel.PlayRollAsync`는 `rollDuration`(기본 0.85초) 동안 정렬 위치에서 회전하고 z축으로 살짝 들어 올린 뒤 `DieShapeDefinition.PoseFor(faceIndex)` 자세로 멈춘다. 경과 시간은 `Time.realtimeSinceStartup`으로 계산한다.
- `OwnedDicePanel.Arrange`는 보유 주사위를 2.6 간격의 격자로 놓고 `stageCamera.orthographicSize`를 조절한다.
- `DiceStage.prefab`은 `Models`, `Camera`, `Light`로 구성된다. Camera는 z = -20에서 정면을 보는 직교 카메라이며 바닥과 벽은 없다. 주사위 모델은 레이어 30을 사용한다.
- `DieShapeDefinition`은 `definitionId`, `modelPrefab`, 면별 `facePoseEuler`, `readingRule`만 가진다. 면 법선이나 충돌 형태 데이터는 없다.
- `DieShapeCatalog.Find(definitionId, faceCount)`는 정의 ID가 정확히 같은 형태 하나를 찾는다. 하나의 형태를 여러 정의 ID가 공유하는 수단은 없다.
- `DiceD4/D6/D8.prefab`은 `MeshFilter`, `MeshRenderer`, `DieModelView`와 면 중심의 `Face{n}` TextMeshPro 라벨로 구성되며 Collider와 Rigidbody는 없다. `Assets/TxTRPG` 전체에 `Rigidbody`, `PhysicsScene`, `Physics.Simulate` 사용이 없다.
- 메시와 라벨, 형태 자산은 `TemporaryOwnedDiceIntegration.CreateBaseAssets`(메뉴 `Tools/TxT RPG/UI/Dice/Temporary/Create Base Dice Assets`)가 생성한다. 이 생성기는 형태 정의와 카탈로그가 이미 있으면 보존하므로, 기존 `facePoseEuler`를 갱신하는 경로가 없다. `ValidateBaseAssets`는 `temporary.d4`의 형태가 있어야 통과한다.
- 기본 보유 설정은 `Assets/TxTRPG/Application/Configuration/TemporaryDiceConfiguration.asset`이다. `TemporaryDiceConfiguration.TryUpgradeKnownDefaultsForEditor`와 `TemporaryDiceMenuBuilder`는 D4·D6·D8 기본 구성을 전제로 한다.
- 임시 보유 주사위와 결과 기록은 세션 수명이며 저장 파일에 기록되지 않는다. 따라서 D4 교체에 저장 데이터 마이그레이션은 필요하지 않다.
- 런타임 코드에서 `SceneManager.sceneCount`나 `GetSceneAt`으로 로드된 Scene을 순회하는 곳은 발견되지 않았다. 그래도 별도 물리 Scene을 추가하기 전에 Scene 전환 시스템과 충돌하지 않는지 다시 확인한다.

CodeGraph로 `OwnedDicePanel`, `DieShapeDefinition`, `DieShapeCatalog`, `TemporaryOwnedDiceIntegration`, `TemporaryDiceRollMenuController`, `TemporaryDiceConfiguration`의 호출자와 테스트를 먼저 탐색하고, 실제 소스와 직렬화 자산으로 확인한다.

## 4. 연출 방식

### 4.1 전체 순서

```text
행동 수락 및 RollAll로 결과 확정 (기존)
→ OwnedDicePanel이 결과 목록을 받음
→ 현재 표시 영역 크기로 바닥·벽 범위 계산
→ 격리된 물리 공간에서 던지기를 시뮬레이션하고 프레임별 자세를 기록
→ 각 주사위의 정지 상태와 위를 향한 면 판정
→ 확정 결과 면이 위로 오도록 주사위별 보정 회전 계산
→ 기록한 궤적을 실시간으로 재생 (보정 회전은 시각 모델에만 적용)
→ 정지 위치·방향 유지, true 반환
→ ExecuteTurn 한 번 (기존)
```

판정 결과, `ExecuteTurn` 호출 시점과 횟수, 결과 기록과 모달은 바꾸지 않는다. 시뮬레이션과 재생은 표시 계층의 일이며, 시뮬레이션 결과가 판정을 바꾸는 경로를 만들지 않는다.

### 4.2 미리 계산과 재생

- 화면에 보이는 모델로 실시간 물리를 돌리지 않는다. 별도 물리 공간에서 고정 시간 간격으로 끝까지 계산하고, 그 기록을 재생한다. 이렇게 하면 결과 보정이 재생 시작 전에 끝나고, 플랫폼별 물리 차이가 화면 결과에 영향을 주지 않는다.
- 격리된 물리 공간은 `SceneManager.CreateScene`과 `LocalPhysicsMode.Physics3D`로 만든 Scene의 `PhysicsScene.Simulate`를 기본 방향으로 한다. 사용 전 Unity `6000.3`의 공식 문서로 API 동작과 자동 시뮬레이션 설정과의 관계를 확인한다. 기본 물리 Scene의 `Physics.Simulate`를 호출해 프로젝트 전체 물리를 진행시키지 않는다.
- 물리 공간에는 시각 모델이 아니라 충돌용 대리 오브젝트(Collider + Rigidbody)만 둔다. 대리 오브젝트의 형태는 시각 모델의 다면체와 같아야 한다.
- 물리 공간의 수명은 `OwnedDicePanel` 또는 전용 컴포넌트가 소유한다. 비활성화와 파괴 시 해제하고, Scene 교체와 반복 진입에서 누적되지 않게 한다. 빌드 Scene 목록에 추가하지 않는다.
- 시간 간격, 최대 시뮬레이션 시간, 정지 판정 기준, 재시도 횟수, 재생 속도는 Inspector에서 조정할 수 있게 한다. 기본값은 구현 중 측정해서 정하고 근거를 보고한다.
- 재생은 기록한 프레임 사이를 실제 경과 시간으로 보간한다. 화면 프레임 속도에 따라 재생 길이나 궤적이 달라지지 않아야 한다. 기존 코드처럼 `Time.realtimeSinceStartup` 기반을 유지하거나, 다른 시간원을 쓰면 그 이유를 기록한다.
- 계산 비용이 행동 수락 프레임에 몰려 끊김을 만들 수 있다. 측정 결과 필요하면 여러 프레임에 나눠 계산하되, 계산과 재생을 합친 시간이 `rollTimeoutSeconds`보다 충분히 짧아야 한다. 정적 검토만으로 모바일 성능을 단정하지 않는다.

### 4.3 던지기 구성

- 주사위는 표시 영역에서 카메라에 가까운 쪽 가장자리 위에서 출발해 반대쪽으로 던진다. 시작 위치는 서로 겹치지 않게 배치하고, 방향·속도·회전 속도에 연출용 난수로 변화를 준다.
- 연출용 난수는 Gameplay의 `IRandomIndexSource`와 분리한다. 연출 난수가 판정 난수의 소비 순서를 바꾸지 않아야 한다. 재현을 위해 개발 빌드와 Editor에서는 연출 시드를 로그로 남길 수 있게 한다. 시드는 저장하지 않는다.
- 주사위끼리의 충돌, 바닥·벽 튕김, 마찰은 물리 공간이 처리한다. 반발·마찰 계수는 별도 설정 자산이나 Inspector 값으로 조정할 수 있게 한다.

### 4.4 정지 판정과 위를 향한 면

- 정지는 일정 시간 연속으로 선속도와 각속도가 기준 이하일 때로 판정한다.
- 위를 향한 면은 각 면의 월드 법선과 위쪽 벡터의 내적이 가장 큰 면이다. 그 내적이 "바닥과 평행" 기준(예: 법선과 위쪽의 각도 차이가 수 도 이내) 이상이어야 정상 정지로 본다. 벽이나 다른 주사위에 기대어 기울어진 상태는 정상 정지가 아니다.
- 최대 시뮬레이션 시간 안에 정지하지 않거나 기울어진 주사위가 있으면, 연출 시드를 바꿔 다시 계산한다. 재시도 횟수를 넘기면 4.6의 대체 연출을 사용한다.

### 4.5 결과 보정 회전

- 시뮬레이션에서 위를 향한 면을 L, 확정 결과 면을 D라고 한다. 다면체의 대칭 회전 중에서 D의 법선을 L의 법선으로 보내는 회전 S를 찾아, 재생 시작 전에 시각 모델 쪽 자식 Transform에 적용한다. 재생 중에는 S를 바꾸지 않는다.
- S는 다면체의 대칭 회전이므로 시각 모델의 외형은 대리 오브젝트와 겹치고, 충돌 궤적은 그대로 맞는다. 정육면체와 정팔면체는 어떤 면이든 다른 면으로 옮기는 대칭 회전이 있다. 이 성질을 형태별 데이터와 테스트로 보장한다.
- 같은 L, D 쌍에 대칭 회전이 여러 개 있으면, 정지 후 결과 라벨이 카메라에서 가장 바로 읽히는 회전을 고른다.
- 면 라벨의 내용을 다른 면으로 바꿔 쓰는 방식은 사용하지 않는다. 주사위의 면 배치가 굴릴 때마다 달라 보이기 때문이다.
- 배치를 담당하는 Root와 굴림·보정을 담당하는 시각 자식을 분리한다. 기존 지침의 "Root는 배치, VisualRoot는 굴림·효과" 원칙을 따른다. 현재 모델 Prefab에는 이 분리가 없으므로 계층을 추가하거나 동등한 구조를 만든다.

### 4.6 대체 연출과 실패

- 재시도까지 실패하거나 형태 데이터가 잘못되면, 주사위를 영역 안의 겹치지 않는 위치에 결과 면이 위를 향한 자세로 놓는 짧은 전환을 재생하고 `true`를 반환한다. 이때도 화면의 결과와 확정 결과는 같아야 한다.
- 표시 자체를 할 수 없는 경우(형태 없음, 카메라·모델 누락, 패널 비활성, 취소)는 지금처럼 `false`를 반환하고, 기존 `ShowRollFallback` 텍스트와 결과 적용 경로를 사용한다.
- 시뮬레이션 실패와 표시 실패를 로그에서 구분한다. 어떤 경우에도 재추첨하거나 결과를 두 번 적용하지 않는다.

### 4.7 모션 감소

`TemporaryDicePreferences.ReduceMotion`이 켜져 있으면 던지기와 시뮬레이션을 생략한다. 주사위를 영역 안의 겹치지 않는 위치에 결과 면이 위를 향한 자세로 놓고, `reducedMotionDuration` 이내의 짧은 전환이나 즉시 배치를 사용한다.

## 5. 카메라, 바닥, 표시 영역

- `DiceStage`의 카메라를 바닥을 비스듬히 내려다보는 시점으로 바꾸고, 보이는 바닥면을 추가한다. 직교와 원근 중 어느 쪽을 쓸지는 영역 계산의 단순성과 화면 품질을 비교해 정하고 이유를 기록한다.
- 기존 분리 원칙을 유지한다. 주사위 Stage의 카메라·조명·바닥은 메인 화면의 다른 오브젝트와 섞이지 않게 하고, 새 AudioListener를 추가하지 않는다. 레이어 30 사용을 유지하거나 바꾸면 이유를 기록한다.
- 벽은 보이지 않게 두고, 카메라에 보이는 바닥 범위에서 주사위 반지름과 여백만큼 안쪽에 둔다. 범위는 현재 `DiceViewport`의 가로세로 비율로 계산한다. 16:9를 가정하지 않는다.
- 모든 보유 주사위가 한 화면에 보여야 한다는 기존 요구를 유지한다. 주사위 수와 영역 크기에 따라 주사위 크기나 카메라 구도를 조절해서, 던지기 중과 정지 후 모두 영역 밖으로 잘리지 않게 한다. 지원 가능한 최대 개수는 측정해서 보고하고, 임의 제한을 추가하지 않는다.
- 굴림 중 영역 크기가 바뀌면 진행 중인 재생의 카메라 구도는 굴림이 끝날 때까지 유지한다. 굴림이 끝난 뒤 새 구도를 적용하고, 새 범위 밖에 놓인 주사위는 방향을 유지한 채 가장 가까운 유효 위치로 옮긴다.
- RenderTexture의 크기 상한, 재할당 조건, 비활성 시 해제 정책은 기존 구현을 유지한다.

## 6. 정지 후 배치와 보유 목록 변경

- 첫 굴림 전과 보유 목록이 바뀐 직후에는 주사위를 바닥 위의 겹치지 않는 정돈된 위치에 둔다. 각 주사위는 한 면이 위를 향한 평평한 자세로 놓는다.
- 굴림이 끝나면 멈춘 위치와 방향을 유지한다. 다음 굴림은 이 상태에서 다시 던진다.
- 보유 목록이 바뀌면 정돈된 배치로 다시 놓는다. 이미 굴림 결과를 표시하던 주사위의 처리 방식(결과 자세 유지 또는 기본 자세)을 구현 중 정하고 문서에 기록한다.
- 패널이 비활성화되었다가 다시 활성화되면, 마지막으로 정지한 배치를 복원하거나 정돈된 배치로 놓는다. 어느 쪽이든 수락된 행동의 결과 적용에는 영향을 주지 않아야 한다. Scene 재진입 후의 배치는 저장하지 않는다.
- 진행 중 추가된 주사위는 다음 행동부터 참여하고, 진행 중 제거된 주사위의 재생은 안전하게 중단한다는 기존 규칙을 유지한다.

## 7. 형태 조건과 D4 교체

### 7.1 윗면 평행 조건

- 표시에 사용하는 모든 형태는 "어느 면으로 바닥에 놓여도 그 반대편에 바닥과 평행하고 위를 향하는 면이 있다"는 조건을 만족해야 한다. 즉 모든 면 법선 n에 대해 -n인 면이 있어야 한다.
- 정육면체(D6), 정팔면체(D8), 정십이면체(D12), 정이십면체(D20)는 조건을 만족한다. 정사면체(D4)는 위쪽에 꼭짓점이 오므로 만족하지 않는다.
- 형태 자산 검증과 카탈로그 검증에서 이 조건과 4.5의 대칭 회전 존재를 확인한다. 조건을 만족하지 않는 형태는 등록을 거부하고 진단 메시지를 남긴다.
- Gameplay 도메인은 바꾸지 않는다. 런타임에 4면 주사위가 추가되는 것 자체는 계속 허용하되, 맞는 형태가 없으므로 기존의 "3D 형태 없음" 대체 표시와 텍스트 결과로 처리한다. 다른 형태로 위장해서 표시하지 않는다.

### 7.2 형태 데이터

- 면 인덱스별 로컬 법선(읽기 방향)을 명시적인 형태 데이터로 둔다. 기하학적 삼각형 순서나 표시 숫자로 면 인덱스를 추정하지 않는다.
- 생성기가 만드는 메시·라벨의 면 순서와 형태 데이터의 면 인덱스가 일치하는지 검증한다. 같은 효과·수치가 반복되는 서로 다른 면도 구분한다.
- 충돌 대리 오브젝트의 형태(볼록 MeshCollider, BoxCollider 등)를 형태 데이터에서 찾을 수 있게 한다.
- `facePoseEuler`와 `PoseFor`는 대체 연출, 모션 감소, 정돈된 배치에서 계속 쓸 수 있다. 다만 새 카메라에서 결과 면이 위를 향하고 라벨이 읽히는 자세로 값을 다시 계산해야 한다. 생성기가 기존 형태 자산을 보존하므로 값을 갱신하는 명시적 경로를 만든다. 개발자가 수정한 형태 자산을 덮어쓰지 않도록, 생성기가 소유하는 값과 개발자 값을 구분한다.

### 7.3 기본 D4 교체

- `TemporaryDiceConfiguration.asset`의 `temporary.d4` 항목을 다음으로 바꾼다. 같은 배열 위치를 유지해서 보유 순서가 달라지지 않게 한다.

| 항목 | 값 |
| --- | --- |
| `id` | `temporary.d6-secondary` |
| `displayName` | `D6 (보조)` |
| `faces` | 공1, 회2, 공3, 회4, 공5, 회6 (순서대로 `effectKind` 1·2 반복, `amount` 1~6) |
| `minimumValue`, `maximumValue` | 1, 6 |

- 새 정의 ID가 정육면체 형태를 쓰도록 연결한다. 같은 모델 Prefab을 참조하는 형태 자산을 하나 더 두는 방법과, 하나의 형태가 여러 정의 ID를 받도록 카탈로그를 확장하는 방법 중 하나를 고르고 이유를 기록한다. 모델 Prefab을 복제하지 않는다.
- `TemporaryDiceMenuBuilder`의 기본 생성 구성과 `TemporaryDiceConfiguration.TryUpgradeKnownDefaultsForEditor`의 기대값을 새 기본 구성에 맞춘다. 업그레이드 경로는 알려진 이전 기본 D4 항목만 교체하고, 개발자가 바꾼 다른 항목은 보존한다.
- `TemporaryOwnedDiceIntegration`의 D4 생성과 `ValidateBaseAssets`의 D4 요구를 제거한다. D4 메시·Prefab·형태 자산은 카탈로그에서 뺀다. 자산 파일은 참조가 남아 있지 않음을 확인한 뒤 삭제하고, 참조가 남아 있으면 삭제하지 말고 보고한다.
- 형태 정의를 테스트 픽스처로만 쓰는 Gameplay·Application 단위 테스트의 `"d4"` 값은 4면 도메인 동작을 검증하는 용도이면 유지한다. 기본 보유 구성이나 형태 카탈로그를 전제로 한 테스트만 갱신한다.

## 8. 주요 수정 경로

문서 작성 시 아래 경로를 확인했다.

- `Assets/TxTRPG/UI/Runtime/Dice/OwnedDicePanel.cs`
- `Assets/TxTRPG/UI/Runtime/Dice/DieShapeDefinition.cs`
- `Assets/TxTRPG/UI/Runtime/Dice/DieShapeCatalog.cs`
- `Assets/TxTRPG/UI/Runtime/Dice/DieModelView.cs`
- `Assets/TxTRPG/UI/Prefabs/Dice/DiceStage.prefab`, `DiceD4.prefab`, `DiceD6.prefab`, `DiceD8.prefab`
- `Assets/TxTRPG/UI/Styles/Dice/DieShapeCatalog.asset`, `DiceD4Shape.asset`, `DiceD6Shape.asset`, `DiceD8Shape.asset`, `DiceD4Mesh.asset`
- `Assets/TxTRPG/Application/Editor/TemporaryOwnedDiceIntegration.cs`
- `Assets/TxTRPG/Application/Editor/TemporaryDiceMenuBuilder.cs`
- `Assets/TxTRPG/Application/Runtime/Dice/TemporaryDiceConfiguration.cs`
- `Assets/TxTRPG/Application/Configuration/TemporaryDiceConfiguration.asset`
- `Assets/TxTRPG/Application/Runtime/Dice/TemporaryDiceRollMenuController.cs` (`rollTimeoutSeconds`와 연출 시간의 관계 확인)
- `Assets/TxTRPG/Application/Tests/Editor/OwnedDiceIntegrationTests.cs`
- `Assets/TxTRPG/UI/Tests/PlayMode/MainSceneMenuInputPlayModeTests.cs`

신규 표시 코드(시뮬레이션, 재생, 보정 회전 계산)는 `Assets/TxTRPG/UI/Runtime/Dice/`에 둔다. 판정과 전투는 Application·Gameplay에 남기고, UI 어셈블리가 Application을 역참조하지 않게 한다. 보정 회전과 정지 판정처럼 Unity 객체 없이 계산할 수 있는 부분은 테스트하기 쉬운 순수 계산으로 분리한다. 신규 자산은 기존 `Assets/TxTRPG/UI/Prefabs/Dice/`와 `Assets/TxTRPG/UI/Styles/Dice/`에 둔다.

필수 Prefab 내부 참조는 자산에 완성하고, 런타임 이름 검색이나 중복 생성으로 누락을 숨기지 않는다. `OwnedDicePanel`과 `TemporaryDiceRollMenuController` 사이의 기존 공개 계약(`PlayRollAsync`, `ShowRollFallback`, `IsRolling`)은 유지하거나, 바꾸면 모든 호출자와 테스트를 함께 갱신한다.

## 9. 적용

- 작업 시작 시 Editor 연결, 컴파일 상태, 열린 Scene·Prefab의 미저장 변경을 확인하고 사용자 변경을 보존한다.
- 모델·Stage·형태 자산은 생성기가 소유하는 부분만 갱신한다. 전체 재생성을 기본 적용 방법으로 쓰지 않는다. `Create Base Dice Assets`를 확장하거나 임시 적용 메뉴를 추가하면 목적, 범위, 재실행 가능 여부, 자동 저장 여부를 `DOCS/development/workflows.md`에 기록한다.
- `DiceStage`는 `TMP_MainScene`에 배치된 Prefab 인스턴스다. Prefab 변경이 Scene 인스턴스에 의도대로 반영되는지, Scene Override가 새 구성을 가리지 않는지 확인한다.
- 이 작업은 Scene 연결을 바꾸지 않는 것이 목표다. Scene 변경이 필요해지면 범위를 기록하고 저장 전 검증한다.
- 새 Prefab 생성기를 만드는 경우에만 `AGENTS.md`의 Prefab 일괄 Rebuild 등록 규칙을 따른다.

## 10. 검증

1. 고정 난수 소스로 D6·D8의 모든 FaceIndex를 강제하고, 여러 연출 시드에서 재생 종료 시 위를 향한 면이 확정 결과와 같은지 확인한다. 결과 창과 Story의 효과·수치도 같아야 한다. 같은 효과·수치를 가진 서로 다른 면도 확인한다.
2. 형태별로 가능한 모든 (L, D) 쌍에 대해 보정 회전이 대칭 회전이며, 보정 후 D의 법선이 위쪽과 일치하고 메시 꼭짓점 집합이 원래와 겹치는지 Edit Mode 테스트로 확인한다.
3. 정사면체 형태가 윗면 평행 조건 검증에서 거부되는지 확인한다. 런타임에 4면 주사위를 추가하면 다른 형태로 위장하지 않고 대체 표시와 텍스트 결과로 처리되는지 확인한다.
4. 정지하지 않음, 기울어진 정지, 재시도 초과, 시뮬레이션 예외에서 대체 연출이 결과 면을 정확히 보여주는지 확인한다. 타임아웃, 굴림 중 패널 숨김, Scene 전환, 새 세션에서 결과가 한 번만 적용되는지 확인한다.
5. 모션 감소를 켜고 끈 두 경우를 확인한다.
6. 주사위 0개, 1개, 기본 3개, 많은 수, 같은 형태 여러 개에서 던지기 중과 정지 후 모든 주사위가 영역 안에 보이고 겹치지 않는지 확인한다. 지원 가능한 최대 개수를 측정해 보고한다.
7. 넓은 화면, 좁은 화면, 휴대폰·태블릿 비율, 회전, 굴림 중 크기 변경에서 벽 범위, 카메라 구도, 정지 후 재배치를 확인한다.
8. 같은 연출 시드로 다른 프레임 속도(예: 30·60·120fps 제한)에서 재생 길이와 최종 상태가 같은지 확인한다.
9. 시뮬레이션 계산 시간, 재생 중 프레임 시간, 메모리, 물리 Scene과 RenderTexture의 수명, 반복 Scene 진입을 측정한다. 실제 모바일 기기 측정이 불가능하면 미검증으로 명시한다.
10. `OwnedDiceIntegrationTests`, `TemporaryPlayerDiceStateTests`, `MainSceneMenuInputPlayModeTests`와 관련 Edit Mode·Play Mode 테스트를 실행한다. `Tools/TxT RPG/UI/Dice/Temporary/Validate Play Flow`를 새 연출에 맞게 갱신하고 실행한다. 사용 가능한 대상 빌드의 컴파일을 확인한다.
11. AppScene 시작 경로로 `TMP_MainScene`에 진입해 Actions의 주사위 명령을 키보드·마우스, 컨트롤러, 터치로 실행하고, 던지기, 정지, 결과 창, 포커스 복원을 확인한다.
12. 직렬화 diff에서 Scene·Prefab의 의도하지 않은 변경, 사용자 설정과 기존 참조의 소실이 없는지 확인한다.

## 11. 문서 갱신

- `DOCS/architecture/owned-dice-presentation.md`: 던지기 연출 순서, 물리 공간 수명, 보정 회전, 정지 판정, 대체 연출, 카메라·바닥·벽, 정지 후 배치, 윗면 평행 조건을 현재 구현 기준으로 기록한다. "물리 기반 판정은 포함되지 않습니다" 문장은 "판정은 확률, 물리는 연출"이라는 구분이 드러나게 고친다. 기본 형태 목록에서 D4를 빼고 새 보유 구성을 반영한다.
- `DOCS/architecture/dice-domain.md`, `DOCS/architecture/temporary-combat.md`: 기본 보유 구성이나 D4를 언급하는 부분을 갱신한다.
- `DOCS/architecture/project-structure.md`: 새 폴더, 어셈블리 참조, 주요 런타임 컴포넌트가 추가되면 갱신한다.
- `DOCS/development/workflows.md`의 "보유 주사위 3D 패널과 결과 기록": 연출 설정값, 형태 데이터와 새 다면체 추가 절차, 윗면 평행 조건, 생성기·적용 메뉴, 검증 절차를 갱신한다.
- 이 지침서와 이전 지침서는 수정하지 않는다. 구현 결과는 아키텍처 문서에 기록한다.

## 12. 최종 보고

다음을 구분해서 보고한다.

- 구현 완료 항목과 실제 타입·경로
- Editor에 이미 적용하고 저장한 자산과 그 검증 결과
- 사용자가 실행해야 할 남은 적용 단계. 남은 수동 Tools 단계가 있으면 `AGENTS.md`에 따라 `사용자가 수행할 적용 절차` 절에 정확한 메뉴 경로, 순서, 전제 조건, 저장 여부, 성공 기준을 `text` 코드 블록으로 제공한다. 없으면 없다고 명시한다.
- 실제로 수행한 테스트와 측정, 그리고 확인하지 못한 기기·빌드
- 구현 중 정한 기본값(시간 간격, 정지 기준, 재시도 횟수, 카메라 방식 등)과 근거
- 형태와 정의 ID 연결 방식의 선택과 이유, D4 자산 삭제 여부
