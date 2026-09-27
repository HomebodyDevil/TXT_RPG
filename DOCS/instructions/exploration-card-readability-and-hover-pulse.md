# 노드 선택 카드 가독성과 느린 박동 효과

## 목적과 확정 사항

노드 선택 카드에서 제목·설명·상태 문구가 제대로 보이지 않는 원인을 수정하고, 기본 색상을 실제 배경에서 명확히 읽히도록 설정한다. 호버가 유지되는 동안 과장되지 않지만 확실히 인지할 수 있는 반복 확대·축소를 제공한다. 이 문서는 후속 구현 지침이며, 기능 적용이나 실행 검증을 완료했다는 기록이 아니다.

사용자가 확정한 동작은 다음과 같다.

- 마우스 호버와 키보드·컨트롤러 포커스에 동일한 반복 효과를 적용한다. 터치는 기존 눌림 피드백을 유지한다.
- 기본 주기는 확대와 축소를 합쳐 약 1.8초이며, 기준 크기의 100%~103% 사이를 부드럽게 왕복한다. 기존 정적 강조를 함께 사용한다.
- 수치는 실제 화면에서 가독성·잘림·체감 강도를 확인하여 미세 조정할 수 있다. 큰 진폭, 빠른 진동, 회전·이동·플래시나 이중 박동으로 의미를 확대하지 않는다.
- 기존 효과 끄기와 움직임 줄이기 설정을 존중한다. 제목·설명·상태의 일렁임과 움직이는 그라데이션은 계속 기본 꺼짐이다.

## 선행 조사와 범위

`AGENTS.md`, `DOCS/README.md`, `DOCS/architecture/exploration-node-tree.md`, `DOCS/architecture/korean-font-fallback.md`, `DOCS/development/workflows.md`를 읽는다. `DOCS/instructions/exploration-card-feedback-and-text-effects.md`는 선행 작업의 의도를 설명하며, 현재 구현 여부는 최신 소스와 자산으로 판단한다. 이번 기본 반복 효과는 선행 지침의 정적 확대 동작을 보완한다.

현재 작업 트리에는 카드·Scene·프로필·문서 관련 기존 변경과 미추적 파일이 있다. 시작 시 상태와 diff를 확인하고 보존한다. `.codegraph/`가 있으면 관련 호출 및 영향 범위를 먼저 탐색한 뒤 실제 소스·직렬화 값으로 검증한다. 임의로 재색인하지 않는다.

범위는 공유 노드 선택 카드, 기본 표시 프로필, 관련 생성기와 운영 Scene의 필요한 연결, 집중 테스트와 문서이다. 다른 UI의 전역 색상 변경, 폰트 일괄 교체, 탐험 규칙·선택 확정 시간 변경, 새 설정 창·Tween 패키지 도입, 저장 형식 변경은 범위 밖이다.

### 확인한 경로와 책임

| 경로 | 확인할 책임 |
| --- | --- |
| `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationNodeChoiceCardView.cs` | Bind·Unbind, 텍스트와 선택 데이터 연결, ResetPresentation과 MotionRoot 초기화이다. |
| `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationCardFeedbackController.cs` | 포인터·포커스·눌림·확정 상태, 배율·배경 tint와 설정 변경 처리이다. |
| `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationCardPresentationProfile.cs` | 색상·배율·시간과 ExplorationCardPlayerPreferences를 정의한다. |
| `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationCardTextEffectController.cs` | 역할별 기본 색상, TMP 메시 캐시·효과·복원이다. |
| `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationCardShapePresentation.cs` | 이미지·절차적 도형 배경과 마스크 적용이다. |
| `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationNodeChoiceCardList.cs` | 재사용·포커스·목록 입력 잠금·선택 확정 수명이다. |
| `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationNodeChoiceLayoutGroup.cs` | CardRoot 배치와 스크롤 콘텐츠 크기이다. |
| `Assets/TxTRPG/UI/Styles/ExplorationCardDefaultPresentation.asset` | 운영 기본값이 저장된 프로필이다. |
| `Assets/TxTRPG/UI/Prefabs/ExplorationNodeChoiceCard.prefab` | 필수 참조와 기본 외형이 저장된 공유 카드이다. |
| `Assets/TxTRPG/UI/Editor/ExplorationNodeChoiceCardPrefabBuilder.cs` | 재생성 시 동일한 기본 구성을 제공해야 하는 기존 생성기이다. |
| `Assets/TxTRPG/Application/Runtime/Exploration/ExplorationRunController.cs` | 도메인 후보와 UI 선택을 연결한다. 게임 규칙은 유지한다. |
| `Assets/Scenes/AppScene.unity`, `Assets/Scenes/TMP_MainScene.unity` | 실제 시작·운영 화면과 카드 목록 연결의 검증 대상이다. |
| `Assets/TxTRPG/UI/Tests/Editor/ExplorationNodeChoiceCardTests.cs` | 기존 카드·배치·프로필·설정 테스트의 확장 지점이다. |
| `Assets/TxTRPG/UI/Tests/PlayMode/MainSceneMenuInputPlayModeTests.cs` | 기존 운영 화면 입력 회귀 테스트의 조사 지점이다. |

## 현재 사실과 미확인 원인

작성 시 확인한 소스와 자산에는 제목 약 `#F3F6FA`, 설명 약 `#D3DCE8`, 상태 약 `#FFF1CF`가 이미 설정되어 있다. 기본 프로필에서 세 텍스트 효과는 꺼져 있다. 따라서 밝은 색을 지정하는 코드만 추가하고 문제를 해결했다고 판단하지 않는다. 실제 실행 화면에서 사용자가 보고한 현상의 원인은 아직 재현·확정하지 않았다.

현재 피드백 제어기는 hovered 또는 focused일 때 highlightedScale의 고정 목표 배율로 보간한다. 기본값은 1.025이며 반복 위상 계산은 없다. 텍스트 색상은 별도 제어기가 적용한다. 생성기의 tintTargets에는 텍스트가 포함되지 않는다.

도형 모드는 legacyBackground를 비활성화하고 텍스트는 도형 마스크 밖에 배치한다. 따라서 텍스트 뒤에 실제로 어떤 배경이 남는지 확인해야 한다. 이는 가독성 위험 후보이며 확정 원인은 아니다. 기존 대비 테스트는 설명 색상과 고정 배경색을 비교하므로 실제 합성 화면을 검증한 증거가 아니다.

## 텍스트 표시 수정 요구사항

1. AppScene을 통한 실제 노드 선택 화면에서 제목·설명·상태 각각의 증상을 재현한다. 최초 표시, 후보 교체, 비활성화 후 재사용, 설정 변경 전후를 비교하고 수정 전 증거를 남긴다.
2. 실제 프로필 참조와 Scene·Prefab override, TMP 색상·Vertex Color·Face Color·Material 알파, 부모 CanvasGroup, 리치 텍스트 색상, fallback submesh, 마스크·RectTransform·겹침·정렬 순서를 확인한다. 색상 문제, 글리프 누락, 메시 복원 문제와 레이아웃 잘림을 구분한다.
3. 기본 팔레트는 장식보다 가독성을 우선한다. 어두운 텍스트 배경에서는 불투명한 흰색 또는 흰색에 가까운 제목·설명·상태를 사용하고, 실제 합성 배경 대비를 측정하여 최종 색상을 정한다. 중요한 설명을 낮은 알파로 흐리게 만들지 않는다. 기존 색상이 충분하면 색상 변경을 위한 변경 대신 확인된 원인을 수정한다.
4. 프로젝트 검증 목표는 모든 필수 문구의 대비 4.5:1 이상이며, 기본 정적 화면은 가능하면 7:1 이상으로 설정한다. 최종 색상과 배경·합성 알파, 측정 방법과 결과를 기록한다. 임의 이미지 위에서는 글자색만으로 보장하지 말고 필요한 텍스트 영역에 안정된 배경을 제공한다. 도형·이미지 두 모드와 상태 배지를 각각 확인한다.
5. 프로필 클래스 초기값, 저장된 기본 프로필, 저장된 Prefab, 생성기와 실제 운영 인스턴스의 결과를 일치시킨다. C# 초기값 수정만으로 기존 직렬화 자산이 갱신된다고 가정하지 않는다. 다른 사용자 정의 프로필·Variant는 일괄 덮어쓰지 않는다.
6. 프로필이 누락되어도 안전한 텍스트 색상과 정적 표시를 제공한다. 공유 Material·폰트·프로필 자산을 런타임에 수정하지 않는다. 기본 색상을 설정한 뒤 오래된 TMP 색상 캐시가 덮어쓰는지 확인한다. 메시 크기·문자열·fallback submesh가 달라진 경우 이전 배열을 무조건 복사하지 않는다.
7. 기본 텍스트 효과는 꺼진 상태로 유지한다. 선택적 효과를 켰다가 끄거나 카드를 재바인딩해도 최신 문자열·색상·레이아웃으로 복원되어야 한다. 매 프레임 ForceMeshUpdate나 레이아웃 재구축으로 문제를 숨기지 않는다.
8. 한글·영문·숫자와 긴 제목·설명을 확인한다. 기존 말줄임 정책은 유지하되 정상 길이의 핵심 문구가 사라지거나 불필요하게 잘리는 문제를 수정한다. 작은 글자로 축소하는 방식만으로 해결하지 않는다.

## 반복 확대·축소 구현 요구사항

### 설정과 계산

기존 프로필과 피드백 제어기를 확장한다. 활성 여부, 최소·최대 배율과 한 주기의 시간을 개발자가 조정할 수 있게 한다. 기본값은 활성, 1.00~1.03, 1.8초이다. 필드명은 기존 관례에 맞추되 Inspector에서 단위와 의미를 명확히 표시한다.

확대와 축소가 부드럽게 이어지는 곡선을 사용한다. 예를 들어 한 주기 동안 `p = (1 - cos(2πt / period)) / 2`, `scale = Lerp(minScale, maxScale, p)`로 계산할 수 있다. 기존 highlightedScale에 반복 배율을 다시 곱하여 의도한 103% 상한을 넘기지 않는다. 프레임 간 변형을 누적하지 않고 기준 배율에서 계산하며 unscaled time을 사용한다.

기간 0·음수·NaN·Infinity, 역전된 범위와 비정상 배율에는 안전한 정규화 또는 정적 대체를 적용한다. 기존 직렬화 프로필의 신규 필드가 0으로 남는 경우에도 오류가 없어야 한다. 기본 자산은 명시적으로 갱신하고 사용자 정의 값을 조용히 초기화하지 않는다.

### 입력과 상태 수명

- hovered와 focused를 별도로 유지하고 유효한 강조 상태가 유지되는 동안만 한 개의 반복 효과를 실행한다. 진입·이탈은 현재 배율에서 부드럽게 연결하며 마지막 강조가 해제되면 기준 배율로 복귀한다.
- 호버와 포커스 중 하나가 사라져도 다른 유효 상태가 남아 있으면 효과를 유지한다. 터치 탭에서 EventSystem 선택이 남았다는 이유만으로 지속 박동을 시작하지 않는다. 현재 입력 방식과 포커스 출처를 기존 Input System 경로로 구분한다.
- 눌림·선택 확정·입력 잠금에서는 반복 효과가 해당 상태의 배율을 덮어쓰지 않는다. 목록이 Button.interactable을 직접 바꾸어도 상태를 갱신한다. 확정 연출 중인 선택 카드와 비활성화된 나머지 카드를 구분하며 기존 단일 선택·잠금 계약을 보존한다.
- 설정 변경 시 단순히 hovered·focused를 지워 실제 입력 상태와 어긋나지 않도록 한다. 효과 끄기 또는 움직임 줄이기는 반복 확대와 공간 움직임을 즉시 중단하고 기준 배율과 정적 강조를 유지한다. 다시 켰을 때 유효한 입력 상태를 재평가한다.
- Unbind·ResetPresentation·Disable·Destroy·Scene 전환·카드 재사용에서는 위상과 작업을 정리하고 기준 크기를 복원한다. 숨겨진 카드에 이전 확대나 늦은 콜백이 남지 않아야 한다.
- 스크롤 드래그·포인터 취소·입력 장치 변경을 처리한다. 박동을 추가하며 클릭 또는 Submit 콜백을 별도로 중복 등록하지 않는다.

### 외형과 성능

CardRoot의 위치·크기와 Layout 계산은 유지하고 MotionRoot의 배율만 담당한다. 기존 리셋·확정 효과와 Transform 소유권을 통합하며 서로 값을 덮어쓰는 별도 루프를 추가하지 않는다. 기준 배율은 해당 계층의 기존 계약을 따른다.

최대 확대에서 제목과 카드 가장자리가 Viewport에 잘리거나 인접 카드와 겹치지 않는지 확인한다. 필요할 때만 해당 목록의 여백을 최소 조정한다. 고정된 클릭 영역과 확대된 외형 때문에 가장자리 호버가 반복 진입·이탈하지 않아야 한다. 효과가 꺼져도 포커스는 색상 외의 정적 테두리·표식으로 식별할 수 있어야 한다.

활성 카드에 필요한 계산만 수행하고 프레임마다 할당·새 Coroutine 생성·Material 인스턴스 생성·전체 Layout 갱신을 하지 않는다. 측정 없이 모바일 성능을 보장하지 않는다.

## 적용과 호환성

구현 시 Unity 연결·컴파일·Play Mode·미저장 변경을 확인한 뒤 필요한 저장 자산을 선별 적용한다. 개발자 소유 Prefab의 전체 재생성을 기본 수단으로 사용하지 않는다. 기존 `Tools > TxT RPG > UI > Exploration > Rebuild Node Choice Card Prefab`은 전체 카드 재생성 메뉴이므로 이번 수정의 필수 실행 절차로 지정하지 않는다. 생성기 자체는 향후 재생성에서도 수정 사항을 유지하도록 맞춘다.

운영 Scene의 프로필·Prefab 연결과 override를 확인하고 필요한 경우에만 수정한다. 자동 저장으로 무관한 사용자 변경을 포함하지 않는다. 새로운 지속 설정이나 저장 키는 추가하지 않고 기존 `TxTRPG.UI.ExplorationCardEffects`, `TxTRPG.UI.ExplorationReduceMotion` 값을 보존한다. 플레이어 저장 데이터와 탐험 진행은 변경하지 않는다.

자동 적용이 불가능하면 원인과 남은 적용 작업을 보고한다. 필요한 경우에만 범위가 제한된 임시 적용 메뉴를 제공하고, 실제 구현한 정확한 메뉴 경로·저장 방식·재실행 안전성·오류 시 중단 절차를 개발 문서와 최종 보고에 남긴다. 메뉴를 제공한 것만으로 적용 완료라고 보고하지 않는다.

## 검증과 완료 기준

- Edit Mode: 기존 ExplorationNodeChoiceCardTests와 관련 테스트를 실행하고, 저장된 프로필·필수 참조, 배율 상한·주기·잘못된 값, 상태 전환·입력 잠금·재사용·설정 변경을 집중 검증한다. 분리 가능한 계산은 결정적인 시간 입력으로 검사한다.
- Play Mode: AppScene에서 운영 TMP_MainScene의 노드 선택 화면까지 진입한다. 최초 표시와 전투 또는 노드 완료 후 갱신된 후보에서 세 텍스트가 읽히는지 확인한다. 실제 기본·강조·눌림·확정·비활성 상태의 전후 화면과 대비 결과를 남긴다.
- 최소 3주기 동안 1.8초 왕복과 100~103% 범위를 확인한다. 빠른 호버 전환, 포커스 이동, 동시 호버·포커스, 클릭 후 이탈, 반복 Submit, 드래그, 목록 잠금과 Scene 종료에서 잔여 효과나 중복 선택이 없어야 한다.
- timeScale=0, 효과 꺼짐, 움직임 줄이기, 표시 중 설정 변경과 재실행 설정 복원을 확인한다. 검증 과정에서 사용자의 PlayerPrefs를 삭제하지 않는다.
- 키보드·마우스, 컨트롤러와 터치를 검증한다. 터치에서 지속 호버가 남지 않고 정적 상태·눌림 피드백과 스크롤·선택이 동작해야 한다.
- 좁은 세로 화면, 휴대폰·태블릿, 일반 데스크톱·울트라와이드, 확대 텍스트·긴 한글·safe area·방향 변경에서 최대 확대 시 잘림과 가독성을 확인한다. 실제 기기 검증과 Editor 시뮬레이션을 구분한다.
- 관련 빌드 대상은 도구가 준비된 범위에서 컴파일한다. 실행하지 못한 테스트·플랫폼·기기는 이유와 함께 보고한다. 소스나 색상 숫자만 확인하고 화면 검증 완료로 보고하지 않는다.
- diff와 Unity 직렬화 변경을 검토하고 무관한 자산 수정이 없는지 확인한다.

## 문서 갱신과 최종 보고

실제 동작에 맞춰 `DOCS/architecture/exploration-node-tree.md`와 `DOCS/development/workflows.md`를 갱신한다. 기존 아키텍처 문서의 정적 표현 설명과 후반 입력 피드백 설명 사이의 불일치는 관련 부분만 정리한다. 주요 책임·경계를 바꾼 경우에만 `DOCS/architecture/project-structure.md`를 갱신한다. 새 문서는 `DOCS/README.md`에 연결한다.

최종 보고에는 재현한 원인과 잠재 위험의 구분, 최종 색상·대비·주기·배율, 수정한 코드·프로필·Prefab·Scene, 실제 수행한 테스트와 화면 검증, 남은 제약을 포함한다. 구현 완료·Editor 적용 완료·미적용 작업·미검증 항목을 구분한다. 사용자 실행이 필요한 Tools 메뉴가 남으면 별도 `사용자가 수행할 적용 절차`에 정확한 순서·전제·부작용·저장·성공 기준·최종 검증을 fenced text 블록으로 제공한다. 남은 수동 Tools 단계가 없으면 없다고 명시한다.
