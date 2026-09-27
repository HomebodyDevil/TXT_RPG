# 작업 지침서 목록

이 문서는 기능별 작업 지침서와 각 문서의 목적을 안내합니다. 지침서는 구현 요구와 검증 기준을 담고 있으며, 목록에 있다는 사실만으로 해당 작업이 완료되었음을 의미하지 않습니다. 현재 구현 상태는 [프로젝트 문서](../README.md)의 아키텍처 문서와 실제 코드에서 볼 수 있습니다.

## 지침서 목록

| 문서 | 설명 |
| --- | --- |
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
| [메인 Scene 시각적 개선안 제작](main-scene-visual-improvement-proposal.md) | 기존 TMP_MainScene과 공용 자산을 보존하고, 기존 패널과 필요한 리소스로 실행 가능한 별도 개선안 Scene을 제작합니다. |
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
