# 해상도 대응과 화면 프레임 정책

이 문서는 UI 요소를 추가하거나 고칠 때 지켜야 할 해상도 정책을 설명합니다. 정책은 2026-10-19에 사용자가 확정했습니다. 새 Scene, 새 Prefab, 기존 UI의 배치 변경에 모두 이 정책을 적용합니다.

정책을 처음으로 구현하고 적용한 작업은 [화면 프레임과 레터박스 해상도 정책 적용](../instructions/screen-frame-letterbox-resolution-policy.md) 지침서가 정의했고, 2026-10-19에 `Candidate_MainScene`에 적용했습니다. 구성 요소의 책임과 계산식은 [화면 프레임과 레터박스](../architecture/screen-frame.md)에 있습니다.

## 1. 확정된 정책

| 항목 | 정책 |
| --- | --- |
| 기준 해상도 | 1920×1080 Canvas 단위. 모든 게임 화면 UI의 크기와 위치는 이 단위로 설계합니다. |
| 화면 비율 | 16:9 화면 프레임을 항상 유지합니다. 화면 비율이 16:9와 다르면, 남는 위아래 또는 좌우 영역을 레터박스로 채웁니다. 프레임을 늘리거나 찌그러뜨리지 않습니다. |
| 크기 조정 | 화면 프레임 전체가 Safe Area 안에 들어가는 가장 큰 크기로 균일하게 확대·축소합니다. 프레임 내부의 UI 단위는 기기와 무관하게 항상 1920×1080입니다. |
| Safe Area | 화면 프레임은 `Screen.safeArea` 안에 놓입니다. 레터박스는 Safe Area 바깥(노치, 둥근 모서리)까지 화면 전체를 채울 수 있습니다. |
| 레터박스 꾸미기 | 개발자가 레터박스 표시를 단색, 이미지, 텍스처, 애니메이션 등으로 바꿀 수 있어야 합니다. 레터박스는 장식이며, 게임에 필수적인 정보나 조작 요소를 두지 않습니다. |
| 화면 방향 | 가로 고정입니다. 휴대폰과 태블릿은 가로 왼쪽·가로 오른쪽만 허용합니다. |
| 데스크톱 창 | 전체 화면 창 모드가 기본입니다. 창 모드 기본 크기는 1280×720이며, 사용자가 창 크기를 바꿀 수 있습니다. 창 비율이 바뀌면 레터박스가 비율을 지킵니다. |
| 모달과 전환 화면 | 모달 창 본문은 16:9 화면 프레임 안에 나타납니다. 모달 뒤를 어둡게 덮는 배경과 장면 전환 화면은 레터박스를 포함한 화면 전체를 덮습니다. |
| 적용 대상 Scene | `Candidate_MainScene`과 앞으로 추가할 모든 Scene(시작, 로딩 등)입니다. `TMP_MainScene`은 예외로 기존 방식(Constant Pixel Size, `ResponsiveMainScreen`)을 유지합니다. |

### 1.1 Canvas 계층 규칙

화면 프레임 정책을 따르는 Canvas는 다음 계층을 사용합니다. 구성 요소 이름은 2026-10-19 구현과 일치합니다.

```text
Canvas                      Canvas(Screen Space - Overlay), ScreenFrameCanvasScaler, GraphicRaycaster, ScreenFrameController
├── ScreenFrame             항상 1920×1080 Canvas 단위. 게임 화면 UI는 모두 이 아래에 둡니다.
│   ├── (화면 레이아웃 루트, 예: MainArea)
│   └── OverlayLayer        프레임 안에 떠 있는 오버레이(독, 디버그 패널 등)
├── LetterboxLayer          화면 전체. 프레임 밖 영역을 레터박스 표시로 덮습니다. 입력을 받지 않습니다.
└── ScreenOverlayLayer      화면 전체. 모달 배경처럼 레터박스까지 덮어야 하는 요소만 둡니다.
```

- 게임 UI는 `ScreenFrame` 아래에 둡니다. `Canvas` 바로 아래나 `ScreenOverlayLayer`에 두지 않습니다.
- `LetterboxLayer`는 `ScreenFrame`보다 뒤(위에 그려지는 쪽)에 둡니다. 따라서 프레임 밖으로 넘친 그림은 레터박스가 덮습니다. 프레임 밖으로 그려져야 하는 효과는 프레임 안쪽에 여유 공간을 두고 설계합니다([시각 효과 준비](visual-effect-readiness.md)).
- `ScreenOverlayLayer`에 둔 요소 중 본문이 있는 요소(모달 창 등)는 본문을 화면 프레임과 같은 영역에 맞춥니다.

## 2. 현재 구현 상태 (2026-10-19 적용 후)

| 대상 | 현재 상태 |
| --- | --- |
| `Candidate_MainScene`의 `Canvas` | 1.1절 계층. `ScreenFrameCanvasScaler`(Scale With Screen Size, 1920×1080, `DefaultScreenFrameProfile.asset`)와 `ScreenFrameController`. 결과 모달은 `ScreenOverlayLayer`에 있고, 창 본문은 `ModalWindowHost/FrameArea`(`ScreenFrameAlignedRect`)로 프레임에 맞춥니다. |
| `TMP_MainScene`의 `Canvas` | 예외. `CanvasScaler` Constant Pixel Size, Scale Factor 1과 `ResponsiveMainScreen`을 유지합니다. |
| 공용 자산 | `Assets/TxTRPG/UI/Styles/ScreenFrame/DefaultScreenFrameProfile.asset`, `Assets/TxTRPG/UI/Prefabs/ScreenFrame/DefaultLetterboxPresentation.prefab`, 새 Scene용 `Assets/TxTRPG/UI/Prefabs/ScreenFrame/ScreenFrameCanvas.prefab` |
| `PersistentAppRoot.prefab`, `AppRoot.prefab`(장면 전환 오버레이) | Scale With Screen Size, 1920×1080, Match 0.5. 화면 전체를 덮습니다(정책상 전환 화면은 화면 전체). |
| Safe Area | 화면 프레임이 처리합니다. `SlotReelDock`은 부모 영역이 Safe Area 밖과 겹치는 만큼만 여백을 더하므로, 프레임 안에서는 여백이 0입니다. `ResponsiveMainScreen`(`TMP_MainScene`)은 기존 방식입니다. |
| Player 설정 | Default Orientation Auto Rotation, Landscape Left·Right만 허용, 창 모드 기본 1280×720, 창 크기 조절 켬, Fullscreen Window 유지 |
| 3D 카메라 | `Candidate_MainScene`의 `Main Camera` 영역은 프레임에 맞추지 않습니다(월드 화면 없음). |

`Candidate_MainScene`의 기존 배치 수치는 1920×1080 Game View(Canvas 배율 1)에서 측정되었고, 적용 후 같은 크기에서 영역·독·디버그 패널의 화면 Rect가 적용 전과 같음을 확인했습니다.

## 3. UI를 추가하거나 고칠 때 지킬 규칙

### 3.1 단위와 배치

1. 크기, 여백, 글자 크기, Breakpoint는 모두 1920×1080 Canvas 단위로 정합니다. 물리 픽셀이나 `Screen.width`·`Screen.height`를 기준으로 UI 크기를 계산하지 않습니다.
2. 화면 좌표(`Screen.safeArea`, 포인터 위치)를 UI 계산에 써야 한다면, Canvas의 `scaleFactor`나 `RectTransformUtility`로 Canvas 단위로 변환합니다. Safe Area는 화면 프레임이 이미 처리하므로, 프레임 안의 요소가 Safe Area 여백을 다시 더하지 않습니다.
3. 앵커, 레이아웃 그룹, `FlexibleLayoutPanel`, 내용 기반 크기를 사용합니다. 필수 UI를 고정 오프셋만으로 배치하지 않습니다.
4. 화면 프레임 안의 너비는 항상 1920이므로, 프레임 정책을 따르는 Scene에서는 화면 비율 때문에 배치가 바뀌지 않습니다. 패널 내부의 크기 변화(분할 비율, 접기, 내용 증가)에는 여전히 반응형 배치로 대응합니다.

### 3.2 새 Scene

1. 새 Scene의 UI Canvas는 화면 프레임 Canvas Prefab `Assets/TxTRPG/UI/Prefabs/ScreenFrame/ScreenFrameCanvas.prefab`으로 시작합니다. 같은 구성 요소를 직접 조립하지 않습니다. 화면 UI는 `ScreenFrame` 아래(레이아웃 루트와 `OverlayLayer`)에 둡니다.
2. 기본 화면 프레임 설정 자산 `Assets/TxTRPG/UI/Styles/ScreenFrame/DefaultScreenFrameProfile.asset`을 사용합니다. Scene마다 다른 레터박스 표시가 필요하면 설정 자산을 새로 만들어 연결하고, 구성 요소 코드를 바꾸지 않습니다.
3. 로딩·시작 화면처럼 짧게 표시되는 Scene도 같은 정책을 따릅니다. 화면 전체를 덮는 장면 전환 효과는 예외로 화면 전체에 그립니다.
4. 3D·2D 월드 화면을 추가하면, 카메라가 그리는 영역도 화면 프레임과 같은 16:9 영역으로 맞출지 먼저 정하고 문서에 기록합니다. 카메라 설정에 맡겨 결과가 우연히 정해지지 않게 합니다.

### 3.3 레터박스

1. 레터박스 표시는 설정 자산에 연결한 표시 Prefab으로 바꿉니다. 표시 Prefab은 레터박스 영역(위·아래·왼쪽·오른쪽 막대와 화면 프레임 영역)을 전달받아 이미지, 텍스처, 애니메이션을 배치할 수 있습니다.
2. 레터박스에는 필수 정보나 조작 요소를 두지 않습니다. 화면 비율에 따라 레터박스 크기가 0이 될 수 있기 때문입니다.
3. 레터박스 표시는 입력 대상이 아닙니다(`raycastTarget` 꺼짐).
4. 애니메이션이나 효과를 넣을 때는 효과 감소 설정과 성능 등급을 고려합니다. 레터박스 효과를 꺼도 게임 정보가 사라지지 않아야 합니다.

레터박스 표시 Prefab을 만드는 방법:

1. Prefab 루트에 `ILetterboxPresentation`을 구현한 구성 요소를 둡니다. 기본 막대를 바꾸는 정도라면 `DefaultLetterboxPresentation.prefab`의 Variant를 만들고 막대 Image의 Sprite·색만 바꿉니다.
2. `ApplyLayout(ScreenFrameLayout)`은 `TopBar`·`BottomBar`·`LeftBar`·`RightBar`·`FrameRect`·`CanvasSize`(Canvas 단위, 원점은 왼쪽 아래)를 받습니다. Prefab 루트는 `LetterboxLayer`(화면 전체) 아래에 Stretch로 생성되므로, 자식 RectTransform은 앵커·피벗 (0, 0)에 `anchoredPosition`·`sizeDelta`로 놓으면 됩니다. 같은 배치가 반복해서 와도 결과가 같아야 하고, 크기 0인 막대는 숨깁니다.
3. 배치되는 RectTransform과 효과를 적용하는 `VisualRoot`를 분리합니다([시각 효과 준비](visual-effect-readiness.md)).
4. 모든 Graphic의 `raycastTarget`을 끕니다. `LetterboxView`도 생성 시 끄지만, Prefab 자체에서 꺼 두는 것을 원칙으로 합니다.
5. 애니메이션은 효과 감소 설정이 켜지면 멈추거나 정지 이미지로 바뀌어야 합니다(설정 연결은 아직 없음, 표시 Prefab이 직접 처리).
6. 설정 자산의 `Letterbox Presentation`에 Prefab을 연결합니다. 실행 중 교체는 `LetterboxView.SetPresentation(prefab)`입니다. 루트에 구현이 없거나 `ApplyLayout`이 예외를 던지면 검은 대체 막대로 바뀝니다.

### 3.4 모달, 팝업, 툴팁

1. 모달 창 본문, 팝업, 툴팁은 화면 프레임 안에 나타나야 합니다. 프레임 가장자리에서 잘리지 않도록 위치를 프레임 영역 안으로 제한합니다.
2. 모달 뒤를 어둡게 덮는 배경은 `ScreenOverlayLayer`에 두어 레터박스까지 덮습니다. 이때 창 본문은 화면 프레임에 맞춘 영역(`ScreenFrameAlignedRect`) 아래에 둡니다.

### 3.5 가독성과 터치 영역

화면 프레임이 기기 화면에 맞춰 축소되므로, 같은 Canvas 단위라도 휴대폰에서는 물리적으로 작게 보입니다. 다음 값을 판단 기준으로 사용합니다.

| 화면 | Canvas 1단위의 화면 크기 |
| --- | --- |
| 1920×1080 데스크톱 | 1픽셀 |
| 1280×720 데스크톱 창 | 약 0.67픽셀 |
| 2400×1080(20:9) 휴대폰 가로 | 약 1픽셀. 6.5인치 기기 기준으로 Canvas 높이 1080이 약 6.8cm이므로, 10단위는 약 0.63mm입니다. |

- 위 휴대폰 기준에서 일반적인 터치 목표 권장 크기(약 9mm)는 Canvas 단위로 약 140에 해당합니다. 현재 `Candidate_MainScene`의 토글 버튼 최소 높이 48은 이보다 작습니다.
- 2026-10-19 적용 후 `Candidate_MainScene`에서 측정한 Canvas 단위와 위 휴대폰 기준(1단위 약 0.063mm)의 계산값입니다. Safe Area가 있는 기기에서는 배율이 더 작아집니다(예: Safe Area 높이 1017이면 배율 0.942).

| 요소 | Canvas 단위 | 휴대폰 계산값 | 1280×720 창 |
| --- | --- | --- | --- |
| 디버그 패널 라벨·값 글자 | 14 | 약 0.9mm | 약 9.3px |
| 디버그 패널 제목·버튼 글자 | 16 | 약 1.0mm | 약 10.7px |
| 요소 토글 글자(`룬`) | 20 | 약 1.3mm | 약 13.3px |
| 결과 모달 제목 | 24 | 약 1.5mm | 16px |
| 슬롯 Reel 숫자 | 29 | 약 1.8mm | 약 19.3px |
| 합계 값 | 46.2 | 약 2.9mm | 약 30.8px |
| 요소 토글 | 120×48 | 약 7.5×3.0mm | 80×32px |
| 디버그 패널 이벤트 발생·기본값으로 버튼 | 147×48 | 약 9.2×3.0mm | 98×32px |
| 디버그 패널 접기 버튼 | 72×44 | 약 4.5×2.8mm | 48×29px |
| 디버그 패널 슬라이더 | 144×36 | 약 9.0×2.3mm | 96×24px |
| 툴팁 본문 글자(2026-10-25 추가) | 22 | 약 1.4mm | 약 14.7px |
| 툴팁 제목 글자(굵게) | 26 | 약 1.6mm | 약 17.3px |
| 툴팁 최대·최소 너비 | 420 / 160 | 약 26.5 / 10.1mm | 280 / 107px |
| 결과 모달 닫기 버튼 | 72×48 | 약 4.5×3.0mm | 48×32px |
- 휴대폰용 글자 크기와 터치 영역의 최솟값은 아직 정하지 않았습니다. 새 UI를 만들 때 위 표로 실제 크기를 계산해 보고, 작아 보이는 요소는 결과 보고에 남깁니다. 기준값을 정하면 이 절을 갱신합니다.
- 이 표는 계산값입니다. 실제 기기에서 확인한 결과로 간주하지 않습니다.
- 창 크기나 Safe Area가 바뀌어 배율이 바뀌면 `ScreenFrameController`가 Canvas 아래 TMP 글자를 다시 만듭니다([화면 프레임과 레터박스](../architecture/screen-frame.md#배율-변경과-tmp-글자-선명도)). 프레임 Canvas 아래에 새로 둔 TMP 글자는 따로 처리하지 않아도 됩니다.

### 3.6 검증

UI를 추가하거나 배치를 바꾸면 Game View에서 다음 화면 크기를 확인합니다. 임시로 추가한 Game View 크기는 확인 후 제거합니다.

| 화면 크기 | 확인 목적 |
| --- | --- |
| 1920×1080 | 기준 해상도. 레터박스가 없어야 합니다. |
| 1280×720 | 작은 16:9 창. 글자와 버튼이 읽히는지 확인합니다. |
| 2560×1080, 2400×1080 | 울트라와이드, 20:9 휴대폰 가로. 좌우 레터박스를 확인합니다. |
| 1024×768, 2048×1536 | 4:3 창과 태블릿. 위아래 레터박스를 확인합니다. |
| Device Simulator의 노치 기기 하나 이상(가로) | Safe Area 안에 프레임이 들어가는지 확인합니다. |

- 확인 결과를 보고할 때는 Editor 확인과 실제 기기 확인을 구분합니다.
- 프레임 정책을 따르는 Scene에서 세로 화면(예: 1080×1920)은 지원 대상이 아닙니다. Editor에서 세로 크기를 확인했다면, 깨지지 않고 위아래 레터박스로 표시되는지만 확인합니다.
