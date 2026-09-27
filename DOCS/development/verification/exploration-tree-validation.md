# 탐험 기록 트리 검증 (2026-09-27)

환경은 Unity 6000.3.22f1 Windows Editor입니다. 저장된 TMP_MainScene을 재로드하고 AppScene 시작 경로로 실행했습니다. 사용자 NodeTreePanel의 이름·활성화 상태를 보존했고, 대상 하위 UI와 Controller 참조만 추가했습니다. 이후 사용자 확정에 따라 후보 선택 창을 중앙 패널 안으로 제한했습니다.

## 실행한 검사

- Unity Edit Mode: ExplorationTreeProjectionTests 11개 및 ExplorationRunStateTests 7개 통과.
- Play Mode 코루틴 어댑터: 재사용·스크롤·새 Run·크기 변경·효과 초기화, 100/500단계 가상화, Sprite 자산 저장·재로드·긴 이름·선 끝점, Controller의 Active/Completed/Failed 갱신·기존 세션 재바인딩, 가상 Keyboard/Gamepad/Mouse/Touch 입력 및 실제 카드→트리 방향 탐색·모달 우선순위 및 좁은 페이지 배치·가상 터치 전달·메뉴 포커스 표시 등 8개 통과.
- Sprite 교체는 별도 임시 Style 자산으로 수행하고 삭제했습니다. 원본 Style은 유지했습니다.
- Scene 저장 후 재로드 및 Prefab·Style·Controller 연결을 검증했습니다. 컴파일/메뉴 실행 성공만으로 판정하지 않았습니다.

Windows Standalone64와 Android Player 스크립트 컴파일도 각각 39개 어셈블리로 통과했습니다. 배포 패키지와 IL2CPP 빌드를 실행했다는 의미는 아닙니다.

## 긴 기록 측정

| 조건 | 기록 수 | 선 수 | 투영+배치 | View 갱신 | 최신 화면의 활성 노드/선 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 100단계, 후보 3개 | 301 | 300 | 0.889 ms | 7.351 ms | 15/15 |
| 500단계, 후보 3개 | 1501 | 1500 | 2.368 ms | 3.412 ms | 15/15 |

투영과 View 갱신은 서로 다른 검사에서 측정했습니다. View 수치는 360×600 UI 테스트 패널에서 한 번 Show를 호출한 시간이며 GPU 프레임 시간이나 지속 성능 수치가 아닙니다. GC.GetAllocatedBytesForCurrentThread는 이 Editor 환경에서 모든 검사에 0을 반환하므로 할당량 증거로 사용하지 않습니다. 모바일 메모리·열·프레임 성능은 실기기 Profiler 검증이 남아 있습니다.

## 화면 관찰

`exploration-tree-screens.txt`와 `Assets/Screenshots/exploration-tree-*.png`에 6개 조건을 기록했습니다. desktop 1920×1080, phone portrait 390×844, phone landscape 844×390, tablet 1024×768, ultrawide 2560×1080 및 390×844의 Safe Area 인셋 모사입니다. Safe Area 모사는 운영체제의 실제 Screen.safeArea 검증을 대체하지 않습니다.

초기 관찰에서 휴대폰 세로는 기존 부모의 가로 3열 배치 때문에 트리 폭이 4, Safe Area 조건은 0이었습니다. 사용자가 좁은 화면의 세로 배치·전체 스크롤을 확정하여 ResponsiveMainScreen과 중첩 스크롤 연결을 추가했습니다. 재검사에서 휴대폰 세로 트리 폭은 332, Safe Area 조건은 284로 확보됐습니다. 데스크톱에서는 중앙으로 제한한 선택 창이 트리를 덮지 않습니다.

화면 회전 직후 기존 GameMenuPanel이 이전 높이로 계산하는 프레임에서 공간 부족 경고가 나타날 수 있습니다. 새 높이를 반영한 뒤 LayoutInsufficientSpace가 false이고 모든 메뉴 버튼이 표시되는 것을 테스트로 확인했습니다.

## 적용과 남은 검증

필수 트리 자산과 Scene 연결은 적용되어 사용자가 실행해야 할 트리 적용 메뉴는 없습니다. 실제 컨트롤러 연결/해제, 실기기 터치, 모바일 Safe Area, Player 패키징·IL2CPP·실기기 성능은 별도 검증이 필요합니다. 현재 자동 입력 검사는 Input System 가상 장치로 수행했습니다.
