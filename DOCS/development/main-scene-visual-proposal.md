# Main Scene 시각 개선 제안안

## 목적과 격리 범위

`Assets/Scenes/MainScene_VisualProposal.unity`는 `TMP_MainScene.unity`를 대체하지 않는 비교용 Scene입니다.
원본의 패널 프리팹 연결, 메뉴·모달 구성, 입력 시스템과 서비스 연결을 유지하면서 화면의 정보 위계와 여백, 색상만 제안안으로 조정합니다. `AppSceneRoot.initialContentScenePath`는 계속 `Assets/Scenes/TMP_MainScene.unity`를 가리킵니다.

제안안 전용 자산은 `Assets/TxTRPG/UI/VisualProposals/MainScene/`에만 저장합니다. 공용 프리팹과 공용 스타일을 수정하지 않으므로 제안안을 삭제해도 운영 화면에는 영향을 주지 않습니다.

## 화면 구성

| 영역 | 제안안 |
| --- | --- |
| 전체 | 빈 왼쪽 Placeholder를 숨기고, 본문과 캐릭터 열을 `1.55 : 0.95`로 배분합니다. |
| 본문 열 | 적 표시보다 StoryTextPanel에 더 많은 세로 공간을 배정하여 서사를 우선합니다. |
| 캐릭터 열 | 캐릭터를 가장 크게 유지하고, 체력은 76px, 메뉴는 92px의 안정적인 고정 영역으로 둡니다. ActionGridPanel은 남은 공간을 사용합니다. |
| 배경 | 저채도 청회색을 기본으로 사용하고, Story 쪽에는 차가운 강조색, 캐릭터 쪽에는 약한 온색을 사용합니다. |
| 반응형 | 전체 패널 너비가 920px보다 작으면 두 열을 세로로 전환합니다. 각 패널의 기존 최소 크기와 내부 반응형 정책은 그대로 사용합니다. |

## 데이터와 실행 경로

제안 Scene의 `MainScenePresentationController`는 `Assets/TxTRPG/UI/VisualProposals/MainScene/` 아래의 전용 기본 프로필과 Preview 프로필을 사용합니다. Preview 프로필은 기존 Story·Enemy Demo Data만 읽으며 플레이어 저장 데이터를 수정하지 않습니다. `MainScenePreviewProfile`은 Unity가 ScriptableObject를 안정적으로 직렬화하도록 동일 이름의 파일로 분리했습니다.

Editor에서 다음 메뉴를 사용합니다.

1. `Tools > TxT RPG > UI > Visual Proposals > Rebuild Main Scene Visual Proposal`은 원본을 복사하여 제안 Scene을 만들고 생성기가 담당하는 설정만 갱신합니다.
2. `Tools > TxT RPG > UI > Visual Proposals > Play Main Scene Visual Proposal`은 현재 편집 Scene을 바꾸지 않고 Play Mode를 시작합니다.
3. 기존 Editor Play 정책에 따라 `AppScene`이 먼저 실행되고 `TMP_MainScene` 초기화가 끝난 뒤, 같은 `SceneTransitionService`로 제안 Scene을 교체 로드합니다.
4. Play Mode가 끝나면 임시 요청은 `SessionState`에서 제거됩니다. AppScene 프리팹과 기본 시작 Scene 설정은 변경되지 않습니다.

`MainScene_VisualProposal.unity`는 Build Settings 끝에 활성 Scene으로 추가됩니다. 기존 순서와 첫 Scene인 `AppScene`은 유지합니다.

## 생성기 소유 범위와 수동 수정

생성기는 다음 항목을 소유합니다.

- 세 개 `FlexibleLayoutPanel`의 방향, 간격, Padding, 정렬과 Breakpoint
- 주요 `FlexibleLayoutItem`의 가중치·최소 크기·고정 크기
- `TMP_FlexibleLayoutPanel`의 비활성 상태
- `VisualProposalStoryGlow`, `VisualProposalCharacterWash`
- 제안안 전용 배경 스타일 세 개와 주요 Border 색상
- 제안 Scene의 Preview 프로필 연결과 `MainSceneVisualProposalMarker`

같은 항목을 Inspector에서 수동 변경한 뒤 Rebuild를 실행하면 제안 기본값으로 돌아갑니다. 다른 Scene 오브젝트, 사용자 추가 컴포넌트와 공용 프리팹 자산은 생성기가 변경하지 않습니다.

## 비교 자료와 검증 범위

실제 Play Mode에서 AppScene 경유 로드를 확인한 비교 자료는 다음과 같습니다.

- `Assets/Screenshots/main-scene-original-1920x1080.png`
- `Assets/Screenshots/main-scene-visual-proposal-1920x1080.png`
- `Assets/Screenshots/main-scene-comparison-1920x1080.png`

Unity 자동화 캡처의 크기 인자는 현재 Game View 종횡비를 변경하지 않았습니다. 따라서 1280×720, 2560×1080, 1080×1920은 잘못된 검증 결과를 남기지 않기 위해 비교 자료에서 제외했습니다. 해당 크기는 Game View 해상도를 직접 선택하여 다음을 추가 확인해야 합니다.

- 920px Breakpoint 전후의 가로·세로 전환
- 모바일 세로 화면의 전체 콘텐츠 스크롤 또는 잘림 여부
- 울트라와이드에서 Story 열의 과도한 확장 여부
- 키보드·마우스, 게임패드와 터치의 메뉴·모달 입력 및 포커스 복귀

## 제거 방법

제안안을 폐기하려면 Build Settings에서 `MainScene_VisualProposal` 항목을 제거한 뒤 다음 자산을 삭제합니다.

- `Assets/Scenes/MainScene_VisualProposal.unity`
- `Assets/TxTRPG/UI/VisualProposals/MainScene/`
- `Assets/Screenshots/main-scene-*`

원본 `TMP_MainScene.unity`, AppScene 설정과 공용 프리팹은 별도의 복구 작업이 필요하지 않습니다.

