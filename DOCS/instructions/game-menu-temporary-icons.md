# System·Bag·Status·행동 버튼의 임시 이미지 적용

## 1. 목적과 확정한 방향

현재 메뉴의 System, Bag, Status, 행동 버튼을 기본적으로 텍스트 대신 이미지로 표시한다. 네 개의 임시 아이콘을 실제 저장 자산으로 제공하고 운영 화면에 연결하며, 개발자가 코드를 수정하지 않고 각 이미지를 변경할 수 있게 한다. 이 문서는 후속 구현 지침이며 이미지 제작·Unity 적용·실행 검증이 완료되었다는 기록이 아니다.

사용자가 확정한 방향은 다음과 같다.

| 버튼 | 임시 이미지 | 유지할 기능 식별자 |
| --- | --- | --- |
| System | 단순한 단색 톱니바퀴 | 페이지 ID `system` |
| Bag | 단순한 단색 가방 | 페이지 ID `inventory` |
| Status | 단순한 단색 인물 실루엣 | 페이지 ID `status` |
| 행동 | 단순한 단색 주사위 | 명령 ID `temporary-dice.roll-all` |

네 아이콘은 선 굵기, 시각적 크기, 여백과 색조가 일관되어야 한다. 정상 상태는 이미지 전용이며 글자를 이미지 안에 구워 넣지 않는다. 이미지 누락 시에는 기존 문구를 대체 표시한다. 이번에는 새 버튼 기능, 창 내용, 행동 규칙, 버튼 순서·크기·배치 정책을 변경하지 않는다.

## 2. 선행 조사와 현재 확인한 구성

먼저 `AGENTS.md`, `DOCS/README.md`, `DOCS/architecture/quick-items-and-game-windows.md`, `DOCS/architecture/temporary-combat.md`, `DOCS/development/workflows.md`를 읽는다. 선행 `DOCS/instructions/image-menu-and-inventory-window.md`는 이미지 메뉴의 의도를 참고하는 문서이며, 이번에 가방의 필터·페이지·아이템 명령까지 다시 구현하는 근거로 사용하지 않는다.

작업 시작 시 Git 상태와 관련 변경을 확인하여 기존 사용자 작업을 보존한다. `.codegraph/`가 있으면 호출·영향 범위를 먼저 탐색하고 실제 소스·Prefab·Scene과 대조한다. 임의로 재색인하지 않는다.

작성 시 확인한 사실은 다음과 같다.

- `GameMenuButtonView`는 `ImageOnly`, `ImageWithLabel`, `SetIcon`, `ConfigureDisplay`를 이미 제공한다. Sprite 누락 시 Label을 표시하는 경로와 비율 유지 처리가 있다.
- `GameMenuPanel.prefab`의 System·Inventory·Status는 저장된 `displayMode`가 1이며, 이는 현재 enum의 `ImageWithLabel`이다. 운영 Scene의 `TemporaryDiceRoll` 역시 같은 값이다.
- `QuickItemsUiProjectBuilder`는 Bag에만 `DefaultBagIcon.asset`의 Sprite를 지정하고 `ImageWithLabel`을 설정한다. 다른 버튼에도 동일한 이미지 전용 정책을 연결해야 한다.
- 행동 버튼은 `TemporaryDiceMenuBuilder.EnsureActionButton`이 연결하며 같은 `GameMenuButtonView`를 사용한다. 생성 시 Icon 오브젝트는 비활성이다.
- 행동은 창을 여는 페이지 버튼이 아니라 `TemporaryDiceRollMenuController.RollAllCommandId`를 실행하는 명령 버튼이다. 외형 변경으로 페이지 버튼으로 전환해서는 안 된다.
- 실제 실행 화면의 이미지·입력 상태는 이번 문서 작성에서 검증하지 않았다. 구현 시 AppScene 시작 경로로 확인한다.

### 영향 경로

| 경로 | 역할과 작업 관점 |
| --- | --- |
| `Assets/TxTRPG/UI/Runtime/Windows/GameMenuButtonView.cs` | 기존 Sprite·표시 모드·Label 대체 동작을 재사용하고 필요한 결함만 수정한다. |
| `Assets/TxTRPG/UI/Runtime/Windows/GameMenuPanel.cs` | 페이지·명령 바인딩, 활성 상태, 입력과 포커스를 보존한다. |
| `Assets/TxTRPG/Application/Editor/QuickItemsUiProjectBuilder.cs` | 기본 메뉴 생성 시 세 메뉴 아이콘을 연결하고 교체한 사용자 이미지의 보존 정책을 반영한다. |
| `Assets/TxTRPG/Application/Editor/TemporaryDiceMenuBuilder.cs` | 행동 버튼의 생성·선별 적용 시 주사위 Sprite와 이미지 전용 설정을 연결한다. |
| `Assets/TxTRPG/Application/Runtime/Dice/TemporaryDiceRollMenuController.cs` | 행동 실행 가능 여부와 전투 실행 경로의 회귀 검증 대상이다. |
| `Assets/TxTRPG/UI/Prefabs/GameMenuPanel.prefab` | 공용 세 메뉴 버튼의 기본 표시를 저장한다. |
| `Assets/TxTRPG/UI/Prefabs/GameMenuScreen.prefab` | 중첩 메뉴의 상속·override와 서비스 연결을 확인한다. |
| `Assets/TxTRPG/Content/Items/DefaultBagIcon.asset` | 기존 가방 기본 이미지이다. 새 네 아이콘과 일관성이 있는지 확인하고 참조·GUID를 보존한다. |
| `Assets/Scenes/TMP_MainScene.unity` | 실제 메뉴와 Scene에 추가된 행동 버튼까지 선별 적용한다. |
| `Assets/Scenes/AppScene.unity` | 세션 초기화를 거치는 실행 검증의 시작 Scene이다. |
| `Assets/TxTRPG/UI/Tests/Editor/GameMenuLayoutTests.cs` | 기존 레이아웃·입력 영역 계약의 회귀 검증 대상이다. |
| `Assets/TxTRPG/Application/Tests/Editor/QuickItemsUiProjectBuilderTests.cs` | 저장 자산·생성 경로와 연결 검증을 확장한다. |
| `Assets/TxTRPG/UI/Tests/PlayMode/MainSceneMenuInputPlayModeTests.cs` | 실제 메뉴 클릭·창·명령 입력 회귀 검증의 확장 지점이다. |

## 3. 이미지 자산과 교체 방식

### 임시 자산

실제로 식별 가능한 네 아이콘을 제작하거나 프로젝트가 소유한 적절한 자산을 재사용한다. 모두 같은 사각형이나 기본 흰색 Sprite로 채우고 완료 처리하지 않는다. 톱니바퀴·가방·인물·주사위를 작은 화면에서도 형태로 구분할 수 있어야 한다. 불필요한 광택·입체 효과·배경 판·텍스트는 넣지 않는다.

단색 도형과 투명 배경을 사용하고 현재 메뉴 배경에서 명확히 보이는 밝은 기본 tint를 설정한다. Icon의 비율을 유지하며 크기와 여백을 맞춘다. 새로운 래스터 파일을 사용한다면 예를 들어 128×128 정도의 작은 원본으로 시작하여 실제 출력 크기에서 검증한다. Unity에서 Sprite로 가져오고 알파·필터·플랫폼 압축 후 가장자리가 깨지거나 흐려지지 않는지 확인한다. 영구적인 런타임 Texture 생성으로 임시 이미지를 공급하지 않는다.

새 자산은 기존 UI 디렉터리 관례를 확인하여 한 위치에 모으고 최종 경로를 문서화한다. 새 위치는 구현 시 생성하는 경로이며 현재 존재하는 경로로 서술하지 않는다. 기존 가방 자산은 불필요하게 이동·삭제하지 않는다. 새 가방 이미지가 필요해도 기존 참조를 깨뜨리지 않는다. 외부 자산을 재사용한다면 라이선스·출처를 함께 보관한다.

### 개발자 교체 계약

- 기본 교체 지점은 각 버튼의 `GameMenuButtonView`가 참조하는 `VisualRoot/Icon`의 `Image.sprite`이다. 이미 존재하는 Inspector 참조를 사용하고 네 버튼만을 위해 별도의 전역 이미지 관리 시스템을 만들지 않는다.
- 표시 모드는 `ImageOnly`를 기본으로 저장한다. Label의 문자열과 참조는 삭제하지 않으며 이미지 누락 시 System, Bag, Status, 행동 문구가 복구되어야 한다. 기존 현지화 경로가 있으면 유지한다.
- 개발자가 임의 비율의 Sprite를 교체해도 원본 비율·버튼 입력 영역·순서·명령 바인딩이 유지되어야 한다. tint와 Padding을 Inspector에서 조절할 수 있게 한다. 다색 이미지로 교체하는 경우 흰색 tint로 원본 색을 보존하는 방법을 문서화한다.
- Sprite를 지정했다는 이유만으로 비활성 Icon이 계속 숨겨져 있거나 Label도 함께 사라지지 않도록 활성 상태를 일치시킨다. 참조 누락·null Sprite·명시적 이미지 숨김 상태에서 빈 버튼이 되지 않는지 기존 ApplyDisplayMode와 SetIcon을 확인한다.
- Inspector 변경, 다시 활성화, Scene 재로드와 런타임 SetIcon 교체에서 동일한 표시 규칙을 사용한다. Editor 갱신이 필요하면 해당 View의 표시만 갱신하며 OnValidate에서 자산 재생성을 실행하지 않는다.
- 런타임 초기화가 개발자가 저장한 Sprite를 기본 이미지로 매번 덮어쓰지 않게 한다. 선별 적용 도구를 다시 실행해도 사용자 교체 Sprite를 보존한다. 최초 적용은 비어 있거나 확인된 기존 기본값인 항목만 대상으로 하며 별도 커스텀 이미지가 있으면 덮어쓰지 않는다.
- 명시적 전체 Prefab 재생성은 기존 계약상 외형을 덮어쓸 수 있으므로 이미지 교체 절차의 일부로 요구하지 않는다. 재생성 작업에서도 커스텀 값을 보존해야 한다면 기존 생성기 설정 경로를 재사용하거나 최소한의 명시적 입력으로 분리하고, 재생성 가능한 기본값과 개발자 소유 override의 경계를 설명한다.

## 4. 표시·입력·접근성 보존

네 버튼의 루트 Button과 기존 onClick·페이지·명령 바인딩을 유지한다. Icon·장식 Graphic은 입력을 가로채지 않는다. 기존 Button의 배경·테두리·선택 피드백을 유지하고 일반·포커스·눌림·비활성 상태에서 아이콘이 배경에 묻히지 않게 한다. 비활성 상태를 표현한다고 이미지 전체를 식별 불가능하게 흐리게 만들지 않는다.

현재 버튼의 클릭·터치 영역, GameMenuLayoutGroup 배치, HorizontalScroll·Wrap, 스크롤·포커스 복귀를 유지한다. 이미지가 작다는 이유로 버튼의 입력 영역까지 축소하지 않는다. 키보드·컨트롤러의 Navigate·Submit·Cancel과 터치 조작은 기존 Input System 경로를 사용한다. 새로운 물리 키 판정을 추가하지 않는다.

Label을 숨겨도 이름 데이터는 보존하고 기존 툴팁·접근성 이름 제공 경로가 있으면 연결을 유지한다. 이번 범위만을 위해 별도의 전역 툴팁·스크린리더 시스템은 도입하지 않는다. 아이콘 의미 전달을 색상이나 호버에만 의존하지 않는다.

System·Bag·Status의 서비스·페이지 가용성에 따른 활성 상태와 행동의 전투 중 실행 가능 조건을 유지한다. 이미지 적용 과정에서 비활성 버튼을 임의로 활성화하거나 미구현 Status 기능을 새로 만들지 않는다. 플레이어 데이터·설정·인벤토리·전투·저장 형식은 변경하지 않는다.

## 5. 실제 적용과 생성 경로

1. Unity 연결, 컴파일 상태, Play Mode와 미저장 Scene·Prefab 변경을 확인한다. 무관한 사용자 작업을 저장하거나 폐기하지 않는다.
2. 네 임시 Sprite를 저장하고 임포트 결과를 확인한다. 각 버튼의 실제 Icon 참조와 Sprite를 연결한다.
3. 공유 GameMenuPanel의 세 메뉴 버튼, GameMenuScreen의 관련 중첩 참조·override, TMP_MainScene의 실제 세 메뉴와 행동 버튼을 확인하여 필요한 부분만 적용한다. 서비스·부모·위치·크기·형제 순서·기존 override를 보존한다.
4. 기존 생성기에서도 새 기본 외형이 유지되도록 최소한으로 갱신한다. 행동 버튼은 기존 생성 경로에만 유지하며 공용 Prefab에 무조건 추가하지 않는다.
5. 저장 후 자산을 다시 로드하여 Sprite·표시 모드·필수 참조를 확인한다. Play Mode에서만 바꾼 임시 상태로 완료 처리하지 않는다.

기존 `Tools > TxT RPG > Application > Temporary > Apply Player Dice Roll Menu`는 탐험·전투·설정 연결도 갱신하는 넓은 작업이므로 이미지 적용만을 위해 무조건 실행하지 않는다. 메뉴 전체 재생성도 기본 적용 수단으로 삼지 않는다. 필요하면 네 버튼의 표시만 변경하는 좁은 임시 적용 메뉴를 제공한다. 상시 도구나 새 Prefab 생성기는 실제 반복 필요가 있을 때만 만들며, 생성기를 추가한다면 저장소의 등록·검증 규칙을 따른다.

Editor 자동화가 막히면 정확한 원인과 완료한 코드·자산 작업, 남은 적용·검증 절차를 구분한다. 사용자가 실행할 메뉴가 남으면 실제 구현한 정확한 경로와 전제·저장 방식·재실행 안전성·중단 조건을 문서화한다. 도구만 제공하고 실제 적용이 끝났다고 보고하지 않는다.

## 6. 검증과 완료 기준

- 저장된 네 대상 버튼에 서로 구분되는 올바른 Sprite가 연결되고 기본 표시가 ImageOnly인지 확인한다. 기존 라벨은 문자열을 보존한 채 정상 표시에서 숨겨져야 한다.
- Sprite를 다른 이미지로 교체하고 저장·Scene 재로드·Play 재실행 후에도 교체값이 유지되는지 검증한다. 선별 적용 도구를 반복 실행해도 이미지가 기본값으로 돌아가지 않아야 한다.
- Sprite 제거·참조 누락에서 기존 문구로 복구되고 빈 버튼이 되지 않는지 확인한다. Sprite를 다시 지정하면 이미지가 활성화되고 라벨이 숨겨지는지도 검사한다. 세로·가로로 긴 이미지와 투명 여백이 있는 이미지로 왜곡·잘림을 확인한다.
- AppScene에서 시작하여 TMP_MainScene의 System·Bag·Status를 기존 서비스 등록 상태에 맞춰 조작한다. 가능한 창은 이전과 같은 페이지를 열고 닫은 뒤 포커스를 복원해야 한다. 미지원 기능의 기존 비활성 동작은 유지한다.
- 전투 노드에 진입하여 행동 아이콘을 누르고 기존 행동이 정확히 한 번 실행되는지 확인한다. 비전투·처리 중 상태에서 실행 제한이 유지되어야 한다.
- 마우스·터치·키보드·컨트롤러, 좁은 화면·휴대폰·태블릿·데스크톱·울트라와이드, safe area·방향 변경에서 네 버튼의 접근성과 식별성을 확인한다. 정상·포커스·눌림·비활성 화면을 캡처한다.
- 기존 관련 Edit Mode·Play Mode 테스트를 실행한다. 필요한 추가 테스트는 저장 Sprite·대체 표시·재실행 보존·바인딩 보존처럼 실제 회귀 위험을 검증하며 구현 내용을 그대로 반복하는 테스트는 피한다.
- Unity 직렬화 diff에서 무관한 Scene·Prefab·설정 변경을 확인한다. 가능한 대상 빌드에서 Sprite 포함과 컴파일을 확인하고 실제 기기·빌드·입력 중 미검증 항목을 명시한다.

## 7. 문서 갱신과 개발자 교체 절차

구현 완료 시 `DOCS/architecture/quick-items-and-game-windows.md`에는 적용된 기본 이미지·표시 정책·누락 시 동작을 반영한다. `DOCS/development/workflows.md`에는 네 이미지의 최종 경로와 각 실제 버튼의 Inspector 교체 위치를 기록한다. 주요 디렉터리·책임 경계가 생기면 `DOCS/architecture/project-structure.md`를 갱신한다. 새 문서는 `DOCS/README.md`의 작업 지침서 영역과 `DOCS/instructions/README.md`에서 찾을 수 있게 한다.

개발자용 교체 절차에는 다음을 실제 경로와 필드명으로 구체화한다.

1. Play Mode를 종료하고 교체할 이미지를 Sprite로 임포트한다.
2. 공유 메뉴의 해당 버튼 또는 운영 Scene의 행동 버튼을 열고 GameMenuButtonView가 참조하는 Icon의 Source Image를 변경한다.
3. ImageOnly, 흰색 기본 tint, 적절한 Padding과 Preserve Aspect를 확인한다. 기존 Label은 삭제하지 않는다.
4. 공유 변경과 Scene별 override 중 의도한 범위에만 저장한다. 해당 Sprite만 바꾸기 위해 전체 생성기를 실행하지 않는다.
5. AppScene에서 실행하여 이미지, 기능, 비활성 상태와 포커스 복귀를 확인한다.

## 8. 최종 보고

실제 생성·재사용한 네 이미지와 교체 위치, 코드·Prefab·Scene 적용 결과, 입력·기능 회귀 검증, 남은 제한을 보고한다. 구현 완료, Editor 적용 완료, 미적용 작업, 미검증 항목을 구분한다.

수동 Tools 단계가 남으면 별도 `사용자가 수행할 적용 절차`에 정확한 메뉴 경로와 순서, 사전 조건, 덮어쓰기·저장·복구 방식, 성공 기준과 최종 실행 검증을 번호가 있는 fenced text 블록으로 제공한다. 같은 절차를 개발 문서에도 남긴다. 남은 수동 Tools 단계가 없으면 없다고 명시한다.
