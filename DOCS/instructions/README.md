# 작업 지침서 목록

이 문서는 기능별 작업 지침서와 각 문서의 목적을 안내합니다. 지침서는 구현 요구와 검증 기준을 담고 있으며, 목록에 있다는 사실만으로 해당 작업이 완료되었음을 의미하지 않습니다. 현재 구현 상태는 [프로젝트 문서](../README.md)의 아키텍처 문서와 실제 코드에서 볼 수 있습니다.

## 지침서 목록

| 문서 | 설명 |
| --- | --- |
| [Candidate_MainScene 초기 콘텐츠 전환과 FlexibleLayout 영역 구성](candidate-main-scene-initial-content-and-layout.md) | AppScene의 초기 콘텐츠를 `Candidate_MainScene`으로 바꾸고 검증·기본값·테스트를 함께 갱신하며, `MainArea`와 세 Area를 `FlexibleLayoutPanel.prefab` 인스턴스(1:3:1, 좁으면 세로)로 구성합니다. 삭제된 `MainScene_VisualProposal`의 Build Settings 항목과 생성기·테스트·자산·문서를 정리합니다. |
| [Actions 영역의 Cell 기준 높이와 CharacterDisplayPanel 재배분](action-grid-content-fit-height.md) | Actions 높이를 Cell 높이와 Cell·Viewport 사이의 상하 `Gap`(기본 12)으로 계산해 부모 레이아웃에 공급하고, HealthBarPanel을 고정하여 남는 높이를 CharacterDisplayPanel만 받게 합니다. 선택한 Actions에 적용하는 재사용 가능한 메뉴를 둡니다. |
| [탐험 이벤트 노드와 확장 가능한 이벤트 시스템](exploration-event-nodes.md) | 노드 종류 `event`를 추가해 후보 생성 때 이벤트를 확정하고, ScriptableObject 이벤트(페이지·선택지·슬롯 구간 판정·조건·효과)와 상태 기계로 체력·Reel·Fortune·룬 변화를 처리하며 테스트 이벤트 5개와 노드 패널 안 이벤트 화면을 구현합니다. 아이템·룬 강화·합성은 확장 지점만 둡니다. |
| [슬롯머신 Fortune Critical](slot-fortune-critical.md) | Reel별(ID별 설정)·합계에 독립적으로 정수 배수를 판정해 곱셈 중첩·유효 배수 상한을 적용하고, 등록형 가중치 함수(기본 기하 r=0.7)로 배수 분포를 설정하며 실행 중 변경 API와 배수 표시·Story 기록을 구현합니다. 분포 함수 비교 분석을 부록으로 담습니다. |
| [SlotMachinePanel 스킨과 Payline 이미지](slot-machine-panel-skin.md) | 패널 이미지 묶음을 `SlotMachinePanelSkin` 자산과 카탈로그로 분리해 개발자가 고르고 실행 중 교체하며, Payline 이미지가 없으면 코드로 그립니다. 플레이어 선택은 설계만 둡니다. |
| [SlotMachinePanel 외형 개편과 Reel 회전 연출](slot-machine-panel-visual-and-spin-motion.md) | 목표 이미지처럼 Reel 기계·합계·룬 줄 표로 패널을 바꾸고, 설정 자산으로 easing(기본 InOutElastic)·Reel별 시차와 편차·고속 잔상 연출을 조정하게 합니다. |
| [슬롯머신과 룬 기반 전투 행동](slot-machine-and-rune-combat.md) | 주사위를 보류하고 3개 Reel 슬롯머신의 Payline 총합을 룬 줄 가중치로 자동 분배해 행동하며, `OwnedDicePanel` 자리에 2D `SlotMachinePanel`과 회전 연출을 배치합니다. |
| [Edit Mode 테스트 실패 5건 정리](editmode-test-failure-cleanup.md) | 멈춤 수정 후 드러난 SceneTransition·Main Scene·StoryTextPanel·메뉴 테스트 실패의 원인을 확정하고 테스트·메뉴·자산을 정리합니다. |
| [TMP_MainScene의 Story·주사위 패널 위치 교체 후속 정리](main-scene-story-dice-panel-swap.md) | 사용자가 바꾼 계층을 유지한 채 행동 버튼을 적·주사위 패널 경계로 옮기고, 작아진 Story의 너비 기반 글자 크기와 이름 기반 도구·좁은 화면·탐색 부수 효과를 정리합니다. |
| [전투 행동 버튼의 패널 경계선 배치](combat-action-button-on-panel-boundary.md) | 행동 버튼을 메뉴 영역에서 제거하고 EnemyDisplayPanel·StoryTextPanel 경계선 가운데에 레이아웃과 무관한 오버레이로 배치하며, 모양 선택과 효과 확장 지점을 둡니다. |
| [보유 주사위 가장 가까운 가장자리 회수와 던지기 전 지연](owned-dice-nearest-edge-recall-and-throw-delay.md) | 각 주사위를 위·아래·왼쪽·오른쪽 중 가장 가까운 화면 가장자리로 회수하고, 회수 후 고정 지연 시간이 지나면 던지기를 시작합니다. |
| [보유 주사위 화면 밖 던지기와 정지 후 결과](owned-dice-offscreen-throw-and-settled-results.md) | 이전 주사위를 화면 아래쪽 밖으로 회수하고 시야 밖에서 던져 넣으며, 모든 주사위가 멈추고 짧게 대기한 뒤에만 결과를 적용합니다. |
| [보유 주사위 던지기 연출](owned-dice-physics-throw.md) | 확정 결과를 유지한 채 표시 영역 안으로 주사위를 던지는 물리 사전 계산·보정 재생과 윗면 평행 조건, 기본 D4의 D6 교체를 구현합니다. |
| [보유 주사위 3D 패널과 굴림 결과](owned-dice-3d-panel-and-roll-results.md) | 트리 부모 레이아웃, D4·D6·D8 보유 표시, 확률 기반 굴림 연출과 전투별 결과 모달을 구현합니다. |
| [Editor 레이아웃·자동화 경고 수정](editor-console-layout-and-automation-warnings.md) | OnValidate의 안전한 UI 갱신, 메뉴 준비 상태 판정과 Pipeline·UnitySkills 연결 진단을 정의합니다. |
| [탐험 기록 트리 패널](exploration-node-tree-panel.md) | 선택·미선택 후보와 실제 진행 경로를 세로 트리로 표시하고 노드·선 이미지 교체 및 효과 확장을 지원합니다. |
| [노드 카드 초기 자동 선택 제거](exploration-card-no-initial-selection.md) | 새 후보의 자동 포커스를 제거하고 첫 방향 입력으로 탐색을 시작하며 초기 확인 입력의 진행을 차단합니다. |
| [Actions 한 줄 탐색과 Header 정렬](actions-single-row-scroll-and-header-layout.md) | 기본 4개까지 중앙·5개부터 양 끝 배치, overflow의 가로 탐색·왼쪽 시작과 Header·Scrollbar 숨김 여백을 구현합니다. |
| [Actions 슬롯 간격과 표시 방식](actions-grid-spacing-and-display-presets.md) | Actions에 균형형 배치를 적용하고 개발자가 간격 배분·큰 슬롯·수동 설정으로 전환하도록 구현합니다. |
| [Bag 빈 상태와 모달 바깥 클릭 닫기](inventory-empty-state-and-modal-outside-close.md) | 표시 슬롯이 있을 때 빈 문구를 숨기고 공통 모달의 바깥 클릭·탭 닫기와 입력 차단을 구현합니다. |
| [메뉴 버튼 임시 이미지 적용](game-menu-temporary-icons.md) | System·Bag·Status·행동을 교체 가능한 단색 아이콘으로 표시하고 기존 기능과 이미지 누락 시 대체 표시를 유지합니다. |
| [노드 선택 카드 가독성과 느린 박동 효과](exploration-card-readability-and-hover-pulse.md) | 텍스트 표시 원인 수정, 고대비 기본값과 호버·포커스의 1.8초 반복 확대·축소를 구현하는 지침입니다. |
| [탐험 카드 피드백·텍스트 효과](exploration-card-feedback-and-text-effects.md) | 입력 피드백, 선택 확정 연출, 텍스트 효과·색상과 플레이어 표시 설정을 구현하는 지침입니다. |
| [탐험 카드 정렬·간격·도형](exploration-card-layout-and-shapes.md) | 가로·세로 정렬, Padding·간격, 절차적 도형과 이미지 마스킹을 확장하는 작업 지침입니다. |
| [탐험 노드 선택 카드 UI](exploration-node-choice-cards.md) | 선택 버튼을 줄바꿈 가능한 세로 카드로 교체하고 이미지·회전·애니메이션·효과 확장 경계를 구성합니다. |
| [점진 생성 노드 트리와 탐험 흐름](incremental-run-node-tree.md) | 무작위 후보 선택·노드 완료·탐험 상태 연속성과 선택/미선택 분기 기록을 구현합니다. |
| [행동 버튼과 임시 적 전투](temporary-enemy-action-combat.md) | 격리된 전투 상태에서 플레이어 주사위와 적의 교대 행동, 체력 바·행동 예고·기록을 연결합니다. |
| [주사위 효과 면과 결과 기록](dice-effect-faces-and-story-results.md) | 공격·회복과 수치로 면을 전환하고 Story 기록 및 추후 3D 판정 연결 경계를 구성합니다. |
| [StoryTextPanel 레이아웃 수명주기 오류 수정](story-text-layout-lifecycle-null-fix.md) | 초기화 중 참조 누락과 레이아웃 재진입을 구분하고 크기 변경 콜백의 예외를 수정·검증합니다. |
| [한국어 표시와 TMP 대체 폰트 수정](korean-text-font-fallback-fix.md) | 한국어 깨짐의 원인을 구분하고 기존 영문 폰트를 유지한 채 한글 fallback과 빌드 검증을 적용합니다. |
| [임시 주사위 보유와 메뉴 굴림](temporary-player-dice-roll-menu.md) | 세션에 D4·D6·D8을 보유하고 메뉴 버튼으로 굴린 개별 결과를 StoryTextPanel에 누적 표시합니다. |
| [설정 가능한 주사위 도메인](configurable-dice-domain.md) | 중복 정수 면, 균등 면 추첨, 런타임 구성 변경과 추후 소유·장착 연결 경계를 구현합니다. |
| [Grid 표시 크기의 비율·고정 상한](action-grid-ratio-capped-sizing.md) | Icon과 EmptySlot에 비율 계산값과 고정 최대 크기 중 작은 값을 축별로 적용합니다. |
| [Grid 빈 슬롯 표시 크기](action-grid-empty-slot-sizing.md) | EmptySlot에 독립적인 비율·고정 여백 설정을 제공하고 아이템 Icon 설정과 구분합니다. |
| [Grid Cell 반응형 아이콘 크기](action-grid-cell-responsive-icon-sizing.md) | 비율 기반 아이콘 영역과 기존 고정 여백 모드를 제공하고 TMP_MainScene의 작은 아이콘 표시를 개선합니다. |
| [메인 Scene 시각적 개선안 제작](main-scene-visual-improvement-proposal.md) | 기존 TMP_MainScene과 공용 자산을 보존하고, 기존 패널과 필요한 리소스로 실행 가능한 별도 개선안 Scene을 제작합니다. 결과물(제안 Scene, 생성기, 테스트, 자산)은 `candidate-main-scene-initial-content-and-layout.md`에 따라 제거되었고 기록으로만 남습니다. |
| [TMP_MainScene Play 시작 경로 통일 지침](editor-play-through-app-scene.md) | Editor에서 AppScene을 통해 실행하고 기존 초기화·편집 상태를 보존하는 기준입니다. |
| [타입 기반 모달 요청과 단일 담당자 지침](typed-modal-request-coordinator.md) | 단일 개방 경로, 본문 타입·설정·데이터 공급자와 카테고리별 번호 페이지 구성을 정의합니다. |
| [Bag 오류 표시와 설정 기본값 복구 지침](inventory-error-layout-and-settings-fallback.md) | 필수 참조 복구, 세로 오류 문구 수정 및 안전한 표시 설정 대체 정책을 정의합니다. |
| [모달 콘텐츠 영역과 카테고리형 Grid 설정 지침](modal-content-container-and-grid-configuration.md) | 공통 본문 Container, 슬롯 수·열 수 설정과 카테고리별 페이지·스크롤 표시를 정의합니다. |
| [Inventory 입력 수정과 시스템 설정 모달 지침](main-scene-menu-input-and-settings-modal.md) | 기존 메뉴의 클릭 실패 진단, 빈 설정 모달 연결 및 직접 Editor 적용·검증 기준입니다. |
| [TMP_MainScene 가방 메뉴 연결 지침](main-scene-inventory-menu-integration.md) | 기존 메뉴·모달·플레이어 세션을 연결하여 최근 구현한 가방 창을 사용하는 작업 기준입니다. |
| [이미지 메뉴와 카테고리형 가방 창 지침](image-menu-and-inventory-window.md) | 이미지 버튼, 가방 필터, 스크롤·번호 페이지 및 잠정 컨텍스트 명령의 구현 기준입니다. |
| [ActionGridPanel 최초 스크롤 상단 표시 지침](action-grid-initial-scroll-top.md) | 초기 준비 후 첫 행 표시, 지연 로드와 레이아웃 순서 및 사용자 스크롤 보존을 정의합니다. |
| [등록 기반 Prefab 일괄 Rebuild 지침](registered-prefab-batch-rebuild.md) | 안전한 생성기 선별, 의존 순서, 실행 전 검증과 신규 도구 등록 규칙을 정의합니다. |
| [GameMenuPanel 기본 구성과 서비스 연결 지침](game-menu-prefab-default-composition.md) | 내부 참조 완성, 통합 프리팹, 외부 서비스 연결과 안전한 마이그레이션 기준입니다. |
| [GameMenuPanel 버튼 확장 및 반응형 배치 지침](game-menu-responsive-buttons.md) | 수평 스크롤과 줄바꿈, 열 정책, 버튼 외형·명령 분리 및 기존 Scene 보존 기준입니다. |
| [TMP_MainScene 기본 콘텐츠와 대체 표시 지침](main-scene-default-content-and-fallbacks.md) | 현재 화면 보존, 배포 기본값, 실패 표시 및 격리된 미리보기의 구현 기준입니다. |
| [빠른 아이템·메뉴·모달 창 구현 지침](quick-items-navigation-and-modal-windows.md) | 빠른 슬롯과 소유 데이터 연결, 교체 가능한 메뉴 배치, 공통 모달 호스트 및 잠정 정책을 정의합니다. |
| [HealthBarPanel 정렬 및 효과 확장 지침](health-bar-alignment-and-effect-roots.md) | 배경 기준 중앙 정렬, 런타임 배치 설정, 효과용 계층과 수명 관리에 대한 구현 명세입니다. |
| [HealthBarPanel Padding 기반 크기 지침](health-bar-padding-driven-sizing.md) | 기존 고정 크기 기본값을 대체하는 자동 크기 계산, Alignment 의미 및 검증 기준입니다. |
| [FlexibleLayout Placeholder 및 BackgroundContentLayer 구현 지침](flexible-layout-placeholder-and-background-content.md) | 빈 공간용 프리팹과 두 번째 콘텐츠 레이어의 구현 목표, 호환성 및 검증 기준입니다. |
