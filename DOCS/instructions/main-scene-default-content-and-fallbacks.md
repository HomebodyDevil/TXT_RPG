# TMP_MainScene 기본 콘텐츠와 배포용 대체 표시 구현 지침

## 목적과 범위

현재 TMP_MainScene의 구조, 요소, 배치와 시각 설정을 유지하면서 각 요소에 적합한 기본 데이터를 작성하고 실제 로드·적용 경로에 연결하십시오. 미리보기에서 모습과 동작을 확인할 수 있고 배포 환경에서 데이터 누락이나 시각 자산 로드 실패가 발생해도 안전하게 표시해야 합니다. 이 문서는 후속 구현 명세이며 완료된 동작을 의미하지 않습니다.

프로젝트 수준은 장기 유지보수형 소규모 게임입니다. 필요한 경계는 Scene 초기화, Gameplay 상태, Content 설정 자산, Application의 선택·바인딩, UI 표시입니다. 기존 모듈을 재사용하고 새로운 범용 데이터 프레임워크를 만들지 마십시오.

이번 범위는 기본 자산과 프로필, 타입별 기본값 선택, 기존 씬 연결, 격리된 미리보기, 실패·복구 검증 및 문서입니다. 전투·가방·메뉴의 새 기능 추가와 화면 재디자인은 제외합니다. 실제로 이미 배치된 요소만 대상으로 합니다.

## 사전 확인과 현재 관찰

AGENTS.md, DOCS/README.md와 관련 architecture 문서 및 development/workflows.md를 읽으십시오. CodeGraph가 있으면 먼저 호출 관계를 탐색하고 실제 소스·프리팹·씬으로 확인하십시오. Unity의 현재 열린 씬에 저장되지 않은 변경이 있는지도 확인하십시오. 아래 관찰은 디스크에 저장된 씬 기준이며 Editor 화면을 직접 검증한 결과가 아닙니다.

저장된 Assets/Scenes/TMP_MainScene.unity에서 다음 요소를 확인했습니다.

- Main_FlexibleLayoutPanel, Text_FlexibleLayoutPanel, Character_FlexibleLayoutPanel, TMP_FlexibleLayoutPanel 등 중첩 레이아웃이 있습니다.
- StoryTextPanel, ActionGridPanel, HealthBarPanel은 운영 프리팹을 참조합니다.
- CharacterDisplayPanel에는 ActiveCharacterDisplayBinder와 CharacterDisplayPresenter가 연결되어 있습니다.
- EnemyDisplayPanelDemo는 Assets/TxTRPG/UI/DEMO/EnemyDisplayPanel/EnemyDisplayPanelDemo.prefab을 참조합니다.
- Canvas, EventSystem, Main Camera, Directional Light와 Global Volume이 있습니다.

다음 기존 기본 자산도 확인했습니다. 이름만으로 배포 적합성을 판단하지 말고 실제 데이터와 의존성을 검사하십시오.

- Assets/TxTRPG/Application/Configuration/DefaultNewGameProfile.asset
- Assets/TxTRPG/Content/Characters/DefaultCharacter/DefaultCharacterContent.asset
- Assets/TxTRPG/Content/Characters/DefaultCharacter/DefaultCharacterGameplay.asset
- Assets/TxTRPG/Content/Characters/DefaultCharacter/DefaultCharacterAppearance.asset
- Assets/TxTRPG/Content/Characters/CharacterContentCatalog.asset
- Assets/TxTRPG/UI/Styles/StoryTextPanelDefaultBackgroundStyle.asset
- Assets/TxTRPG/Content/Items/ItemCatalog.asset
- Assets/TxTRPG/Content/Items/MinorHealingPotion.asset

현재 코드에는 QuickItemGridPresenter와 게임 창 시스템도 있지만, 클래스 존재만으로 TMP_MainScene에 연결되어 있다고 판단하지 마십시오. 모든 중첩 프리팹과 비활성 자식까지 조사하여 실제 경로, 로더·바인더, 데이터 참조, 효과와 누락 상태를 표로 기록하십시오.

## 씬 선택과 보존 계약

기본 선택은 현재 TMP_MainScene을 유지하는 것입니다. 운영과 미리보기는 같은 화면에 다른 데이터 프로필을 주입하여 비교하십시오. 이번 요구만으로 새 운영 씬을 복제하거나 AppRoot의 초기 Scene 및 Build Settings를 변경할 필요는 없습니다.

기존 데모 컴포넌트 때문에 저장 데이터 오염이나 운영 초기화 충돌을 프로필 분리로 막을 수 없는 경우에만 별도 검증 Scene을 생성할 수 있습니다. 이 경우 새 Scene은 현재 화면에서 재현 가능하게 만들고 계층·컴포넌트·RectTransform·레이어 순서·스타일 동일성을 비교하십시오. 배포 대상에서 제외하고 기본 시작 씬을 바꾸지 마십시오. 단순 복제 후 두 화면을 수동 관리하는 방식은 피하십시오.

변경 전 계층과 RectTransform, Canvas Scaler, FlexibleLayoutItem, Padding·Alignment, 스크롤 정책, 배경·Border·효과 설정, Prefab Override를 기록하십시오. 변경 후 차이를 검토하여 데이터 연결 외의 시각 변경이 없는지 확인하십시오. 필요한 Loader 교체와 참조 추가는 허용하지만 루트 교체, Unpack, 자식 이동과 생성기 전체 재실행으로 사용자 구성을 소실하지 마십시오. 현재 씬에서 제거된 요소를 과거 문서에 따라 다시 추가하지 마십시오.

## 기본 데이터의 세 가지 용도

| 종류 | 적용 조건 | 금지 사항 |
| --- | --- | --- |
| 새 게임 초기값 | 유효한 저장이 없고 새 게임을 시작할 때 | 손상 저장을 새 게임으로 조용히 대체하지 않습니다. |
| 배포용 대체 표시 | 표시 데이터 미설정 또는 시각 자산 실패 | 체력·아이템·적 등 실제 게임 상태를 만들거나 저장하지 않습니다. |
| 미리보기 데이터 | 개발자가 명시적으로 Preview를 선택할 때 | 운영 저장에 접근하거나 배포에서 자동 활성화하지 않습니다. |

빈 목록과 수량 0은 정상 데이터입니다. 이를 누락으로 취급하지 마십시오. Loading, ReadyWithData, ReadyEmpty, MissingConfiguration, Failed를 구분하는 타입별 결과를 사용하고, null이나 목록 길이만으로 판단하지 마십시오.

표시 우선순위는 유효한 런타임 데이터, 명시적으로 설정한 초기 데이터, 배포 기본 표시, 코드의 최소 안전 표시입니다. 다만 런타임 결과가 ReadyEmpty이면 그대로 유지합니다. 유효한 데이터가 로드 중이면 Loading을 보여주고 가짜 데이터로 먼저 상호작용을 허용하지 마십시오. 이미 정상 표시된 값이 있는 갱신 실패에서는 유지 여부와 오래된 값 표시 정책을 정의하십시오.

## 요소별 데이터와 기대 표시

| 요소 | 배포용 기본 표시 | 명시적 Preview에서 확인할 데이터 |
| --- | --- | --- |
| FlexibleLayoutPanel 배경·테두리 | 현재 색·스타일 유지, 이미지 실패 시 지정 단색 | 같은 스타일에서 이미지 정상·실패 비교 |
| StoryTextPanel | 미설정이면 현지화된 중립 안내 한 줄, 정상 빈 기록은 유지 | 짧고 긴 문장, 발화자 유무와 여러 줄로 스크롤·투명도 확인 |
| CharacterDisplayPanel | 실제 캐릭터 상태는 유지하고 누락 외형만 기본 초상으로 대체 | 기존 DefaultCharacterContent의 외형과 정상 변형 |
| HealthBarPanel | 상태 없으면 알 수 없음 문구와 비활성 표시, 100% 체력으로 위장하지 않음 | 100/100, 40/100, 0/100, 최대치 변경 |
| ActionGridPanel | 기존 용량의 빈 슬롯 또는 실제 등록 유지, 아이콘만 대체 가능 | 회복 아이템 등록·수량·품절·빈 슬롯과 사용 결과 |
| EnemyDisplayPanel | 전투 대상 없음이면 비어 있음, 실제 적의 외형만 누락 시 대체 | 단일 적·복수 적·이미지 누락 |
| 메뉴·모달이 실제 존재할 경우 | 현재 등록된 페이지 유지, 누락 페이지는 비활성 또는 오류 안내 | 실제 가방·Status 표시와 열기·닫기 |

캐릭터 신규 생성은 DefaultNewGameProfile을 통해서만 수행하고 UI가 ScriptableObject에서 별도의 플레이어를 생성하지 마십시오. 체력, 슬롯 수량, 가방과 Status는 동일한 런타임 상태를 관찰해야 합니다.

EnemyDisplayPanelDemo의 샘플 적과 DemoLoader는 운영 대체 데이터가 아닙니다. 현재 시각 구성은 보존하면서 명시적인 Preview 전용으로 격리하거나 운영 바인더로 전환하십시오. 적 목록이 비어 있다고 샘플 적을 자동 생성해서는 안 됩니다. 기존 운영 세션을 사용하는 ActionGridPanel에도 테스트 아이템을 자동 지급하지 마십시오.

기본 Story 안내는 실제 진행 기록과 구분한 임시 표시로 관리하고, 정상 기록 도착 시 교체하십시오. 재활성화마다 중복 추가하거나 진행 저장에 안내 문구를 기록하지 마십시오. 표의 문구·색·대체 이미지 자체는 개발자가 자산으로 편집할 수 있어야 합니다.

## 자산과 코드 구성

새 프로필 이름 예시는 MainSceneDefaultPresentationProfile이며 신규 타입입니다. Assets/TxTRPG/Application/Configuration/ 아래에서 타입별 기존 콘텐츠·스타일 자산을 참조하도록 설계할 수 있습니다. 단일 거대 SO에 모든 패널 데이터를 복사하지 마십시오. 이미 존재하는 자산을 우선 재사용하고 배포용 대체 자산은 DEMO 폴더와 Editor 어셈블리에 의존하지 않게 하십시오.

기본 스타일은 기존 시각 설정을 존중합니다. 이미지 실패 시 사용하는 단색·실루엣·아이콘은 배포 권리가 명확한 자산을 사용하고 누락된 글리프와 현지화 키 노출을 확인하십시오. 기존 기본 캐릭터 그림이 검증용임을 발견하면 실제 배포 자산으로 적합한지 명시하고 필요한 최소 중립 이미지를 제공하십시오.

Application의 초기화·바인더가 데이터 선택을 결정하고 UI는 전달받은 표시 모델을 그립니다. 기존 PanelInitialDataLoader, PanelStartupController, 각 Presenter와 WhenAssetsReady 계약을 조사하여 책임을 겹치지 않게 하십시오. 동일 패널에 운영 바인더와 DemoLoader가 동시에 쓰지 않게 하십시오.

Addressables 공급자와 Lease 수명은 기존 경로를 재사용합니다. 최후 대체 표시는 실패한 원격 그룹을 다시 로드해야만 나타나는 구조로 만들지 마십시오. 작은 로컬 기본 자산 또는 코드 단색 표시를 부트스트랩에 포함하고, 직접 참조에 따른 의존성 및 중복 번들 포함 여부를 검사하십시오. 모든 Preview 이미지를 운영 시작 시 로드하지 마십시오.

비동기 요청은 취소와 최신 요청 우선 정책을 지키고 뒤늦은 실패가 이미 표시된 정상 데이터를 대체하지 않게 하십시오. 기본 자산까지 실패하면 최소 안전 표시로 종료하여 무한 재시도를 막으십시오. 로그에는 패널, 콘텐츠 ID와 원인을 기록하되 반복 프레임 로그를 피하십시오.

## 초기화와 Preview

운영 실행은 기존 AppScene의 PlayerSessionHost와 SceneTransitionService 초기화 경로를 따릅니다. 패널의 정상 데이터 또는 안전한 대체 표시가 준비된 뒤 Scene 준비 상태가 완료되어야 합니다. 장식 자산 실패 때문에 영원히 Fade 화면 뒤에 남지 않게 하고 필수 저장·도메인 초기화 실패는 복구 가능한 오류 상태로 처리하십시오.

Editor 미리보기는 Edit Mode에서 배치와 대표 문구를 확인하고, Play Mode의 명시적인 Preview 모드에서 실제 바인더와 명령 흐름을 검증할 수 있어야 합니다. Preview에서는 메모리 저장소 또는 분리된 저장 경로를 주입하고 일반 저장 파일에 읽기·쓰기·삭제를 수행하지 마십시오. Preview 프로필과 구성은 코드 이름 예시이며 이미 존재한다고 가정하지 마십시오.

최소 Preview 시나리오는 정상 데이터, 완전 미설정, 정상 빈 목록, 부분 이미지 실패, 지연 로드 후 복구, 필수 초기화 실패입니다. 대표 데이터는 적어도 Story 여러 줄, 실제 기본 캐릭터, 중간 체력, 회복 아이템과 여러 적을 포함하되 각각을 독립 선택할 수 있게 하십시오. 예상 수치와 실제 사용 결과를 README 또는 시나리오 표에 기재하십시오.

## 구현 및 검증 순서

1. 현재 Editor 및 디스크 상태를 구분해 계층·데이터·쓰기 주체 목록을 작성합니다.
2. 요소별 기존 자산의 유효성과 배포 적합성을 확인하고 필요한 최소 기본 자산을 만듭니다.
3. 타입별 누락·빈 상태·실패 정책을 구현하고 실제 씬의 참조를 연결합니다.
4. 명시적인 Preview 프로필 및 실패 주입 경로를 연결합니다. 새 Scene은 앞의 조건이 충족될 때만 생성합니다.
5. 정상·빈 상태·실패·복구에서 실제 화면과 명령을 검증합니다.

수용 기준은 다음과 같습니다.

- 변경 전후의 계층, 레이아웃, 크기, 레이어 순서, 시각 설정과 Override 보존을 비교합니다.
- 정상 빈 슬롯과 적 없음이 샘플 콘텐츠로 채워지지 않습니다.
- 그림만 실패해도 실제 캐릭터 체력·슬롯 수량이 유지됩니다.
- 필수 저장 실패가 새 캐릭터나 새 저장으로 대체되지 않습니다.
- 재활성화, 취소, Scene 이동과 늦은 로드 후 중복 문구·잘못된 대체·자원 누수가 없습니다.
- Preview에서 아이템을 사용하면 Preview 체력과 수량만 변경되고 운영 저장은 그대로입니다.
- 좁은 모바일·넓은 데스크톱, 텍스트 확대, 마우스·터치·컨트롤러에서 읽기 및 입력을 확인합니다.
- Addressables 초기 로드 실패와 기본 자산 실패도 실제 빌드 또는 가능한 테스트 경로에서 검증합니다.

의미 있는 Edit Mode 및 Play Mode 테스트와 관련 컴파일을 수행하십시오. 실제 화면 캡처와 예상/관찰 결과를 남기고 실행하지 않은 대상 기기·빌드 검증은 명시하십시오. 현재 Unity Editor가 열려 있으면 사용 가능한 Editor 실행 경로를 활용하고 임의 종료나 미저장 씬 덮어쓰기를 하지 마십시오.

## 문서 및 최종 보고

DOCS/architecture/의 해당 기능 문서와 DOCS/development/workflows.md에 실제 적용 데이터, 우선순위, Preview 실행법과 오류 검증법을 기록하십시오. 새 모듈 또는 Scene이 생기면 project-structure.md 및 README.md도 갱신하십시오.

최종 보고에는 요소별 자산 경로·적용 주체·화면과 동작, 배포용과 Preview의 차이, 씬 유지 또는 신규 생성 이유, 보존 여부, 실제 검증 및 미검증 항목을 포함하십시오. 본 작업의 목표는 기본 자산 생성뿐 아니라 각 요소에 실제 연결하여 확인 가능한 상태를 만드는 것입니다.
