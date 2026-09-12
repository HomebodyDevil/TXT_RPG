# 등록 기반 Prefab 일괄 Rebuild 작업 지침

## 목적과 범위

검증된 생성형 Prefab을 의존 순서에 따라 일괄 재생성하는 Editor 도구를 추가합니다. 프로젝트의 모든 .prefab 파일을 찾거나 이름에 Rebuild가 있는 메뉴를 무조건 실행하는 도구가 아닙니다. 수작업 자산과 사용자 설정을 보호하고, 이후 생성기 추가·변경 시 같은 등록 및 검증 체계를 사용하도록 합니다.

이 문서는 구현 계획입니다. 아래 신규 클래스명과 메뉴명은 제안입니다. 이번 범위는 Editor 도구, 기존 생성기의 필요한 분리, 등록, 테스트와 문서입니다. 게임 런타임, 실제 Scene 재배치, 전체 에셋 초기화, Addressables 콘텐츠 빌드는 제외합니다.

## 사전 확인과 확인된 현황

AGENTS.md, DOCS/README.md, DOCS/development/workflows.md, DOCS/architecture/project-structure.md를 읽습니다. 실제 MenuItem은 문자열 상수와 여러 줄 선언도 포함하므로 단일 줄 검색 결과만으로 목록을 확정하지 않습니다.

| 현재 경로 | 검토할 내용 |
| --- | --- |
| Assets/TxTRPG/Editor/Common/Menu/TxTRPGEditorMenuPaths.cs | 공통 메뉴 경로를 재사용합니다. |
| Assets/TxTRPG/Editor/Common/Menu/TxTRPGEditorMenuPriorities.cs | 공통 정렬 우선순위를 재사용합니다. |
| Assets/TxTRPG/Editor/Common/TxTRPG.Editor.Common.asmdef | 등록 계약과 실행기의 Editor 전용 위치 후보입니다. |
| Assets/TxTRPG/UI/Editor/StoryTextPanelPrefabBuilder.cs | 메시지와 패널 외에 기본 BackgroundStyle 생성도 수행합니다. Style 변경 정책을 확인합니다. |
| Assets/TxTRPG/UI/Editor/CharacterDisplayPanelPrefabBuilder.cs | CharacterStatusPrefabBuilder를 내부 호출합니다. 중복 재생성을 방지해야 합니다. |
| Assets/TxTRPG/UI/Editor/CharacterStatusPrefabBuilder.cs | 상태 UI 생성 범위와 다른 생성기의 호출을 확인합니다. |
| Assets/TxTRPG/UI/Editor/ActionGridPrefabBuilder.cs | Grid와 관련 자산의 출력 및 의존성을 조사합니다. |
| Assets/TxTRPG/UI/Editor/EnemyDisplayPanelPrefabBuilder.cs | 출력과 참조하는 View·기본 자산을 조사합니다. |
| Assets/TxTRPG/UI/Editor/FlexibleLayoutPrefabBuilder.cs | 중첩 레이아웃 및 Placeholder 출력 범위를 조사합니다. |
| Assets/TxTRPG/Application/Editor/QuickItemsUiProjectBuilder.cs | Prefab 전용 RebuildPrefabsOnly는 기존 ItemCatalog를 요구합니다. 전체 Rebuild는 Scene·콘텐츠 구성까지 수행하므로 직접 포함하지 않습니다. |
| Assets/TxTRPG/SceneTransition/Editor/SceneTransitionPrefabBuilder.cs | CreateOrUpdateAssets는 AppRoot 외에 AppScene, 초기 콘텐츠 Scene, Build Settings 및 Profile까지 변경합니다. 전체 호출을 포함하지 않습니다. |

위 표는 초기 조사 대상이며 전체 등록 목록이 아닙니다. 구현자는 프로젝트의 모든 자체 생성 메뉴와 비메뉴 생성 진입점을 조사하고, 생성기별 포함·제외·분리 필요 여부 및 이유를 보고해야 합니다. 외부 패키지 도구는 기본 대상이 아닙니다.

## 1. 메뉴와 실행 흐름

다음 메뉴를 제안합니다.

- Tools/TxT RPG/Build/Rebuild Generated Prefabs...
- Tools/TxT RPG/Build/Validate Prefab Rebuild Registry

기존 메뉴 상수 체계에서 적절한 위치를 확인한 뒤 확정합니다. 첫 메뉴는 즉시 덮어쓰지 않고 실행 계획 창을 엽니다.

1. 등록 목록을 읽고 실행 대상을 선택합니다.
2. 출력·입력·의존성·충돌·Editor 상태를 검사합니다.
3. 순서, 변경 예정 경로, 기존 자산 덮어쓰기, 제외 이유를 표시합니다.
4. 사용자가 확인한 계획만 순차 실행합니다.
5. 저장된 결과를 다시 로드하여 검증하고 결과 보고를 제공합니다.

기본 선택은 안전한 운영용 생성 Prefab입니다. Demo/Sample은 별도 선택 항목이며 기본 해제합니다. Scene 변경, Addressables 설정 변경·빌드, 콘텐츠 초기화는 선택 항목으로도 이 도구에 섞지 않습니다.

등록된 모든 적격 작업을 한 번에 선택하는 기능과 개별 선택을 제공합니다. 선행 작업은 자동 포함하되 계획에서 명확히 표시합니다. 실행 계획 미리보기는 자산 생성, 저장, Scene 열기나 재생성 콜백 실행 없이 동작해야 합니다.

## 2. 등록과 확장 구조

권장 책임은 다음과 같습니다.

| 신규 제안 요소 | 책임 |
| --- | --- |
| PrefabRebuildTaskDescriptor | 안정적인 ID, 분류, 입력·출력 경로, 선행 작업 ID, 부수 효과, 기본 포함 여부와 설명을 제공합니다. |
| IPrefabRebuildTaskProvider | 각 모듈이 자신의 작업을 등록합니다. |
| PrefabRebuildRegistry | 작업 수집, 중복 ID·잘못된 경로·출력 충돌·순환 의존성을 검사합니다. |
| PrefabRebuildRunner | 계획 생성, 사전 검사, 순차 실행, 취소와 결과 기록을 담당합니다. |
| PrefabRebuildWindow | 대상 선택과 변경 계획 및 결과를 표시합니다. |

Editor의 명시적 Provider 형식을 검색하는 방식을 사용합니다. 예를 들어 TypeCache로 구현 형식을 발견하되, 모든 MenuItem 메서드를 반사 호출하지 않습니다. 검색 결과 순서에 의존하지 않고 선행 관계와 안정적인 ID 순서로 계획을 고정합니다.

공통 Editor 어셈블리가 UI.Editor나 Application.Editor를 역참조하지 않게 합니다. 각 기능 어셈블리의 Provider가 공통 계약을 참조하고 자기 생성기를 호출하도록 합니다. 불필요한 새 어셈블리와 범용 빌드 프레임워크는 추가하지 않습니다.

필수 등록 정보는 다음과 같습니다.

- 변경되지 않는 작업 ID와 표시 이름입니다.
- 운영용, Demo 등 분류 및 일괄 실행 포함 여부입니다.
- 생성·갱신할 자산의 정확한 경로와 필요한 기존 입력 자산입니다.
- 다른 생성 작업을 요구하는 경우 해당 작업 ID입니다.
- Prefab 외의 보조 자산 생성·수정 여부와 설정 보존 정책입니다.
- 사전 검사, 실행, 저장 후 검증 콜백입니다.
- 제외하는 경우 그 이유입니다.

출력 경로를 실행 중에만 알 수 있다면 계획 단계에서 먼저 확정합니다. 동일한 출력 경로를 두 작업이 소유하면 기본적으로 오류로 처리합니다. 같은 자산을 여러 번 쓰는 복합 생성기는 단일 소유 작업으로 묶거나 역할을 분리합니다.

## 3. 신규 메뉴와 생성기 추가 규칙

새 생성기는 Provider에 명시적으로 등록해야 일괄 계획에 나타납니다. 파일명이나 메뉴명만으로 자동 실행 대상에 포함하지 않습니다.

- 개별 메뉴와 일괄 도구는 같은 생성 핵심 로직을 사용합니다.
- 개별 메뉴를 통해 일괄 실행기를 다시 재귀 호출하지 않습니다.
- CharacterDisplayPanel처럼 내부에서 다른 Builder를 호출하는 경우, 일괄 Runner가 의존성을 관리하는 핵심 메서드와 기존 개별 메뉴 wrapper를 분리하거나 복합 작업으로 등록합니다.
- 전체 실행에서 각 작업 ID는 한 번만 실행합니다.
- 신규 생성 메뉴에는 작업 ID 또는 일괄 제외 사유를 대응시킵니다.
- 등록 감사 테스트는 프로젝트 Rebuild 메뉴를 찾아 미분류 항목을 검출합니다. 메뉴 검색은 감사용이며 실행용이 아닙니다.
- 이름에 Rebuild가 없는 생성 진입점도 등록 규칙의 대상입니다. 코드 리뷰와 문서 체크리스트를 함께 사용하여 이름 기반 감사의 한계를 보완합니다.
- 새 작업이 Scene 변경이나 콘텐츠 초기화를 요구한다면 기본 등록에서 제외하고, 안전한 Prefab-only 메서드를 분리한 후 편입합니다.

구현 시 AGENTS.md에 위 등록·출력 선언·제외 사유·테스트·문서 갱신 의무를 추가합니다. 이 지침서 작성 단계에서는 해당 구현이 이미 존재하는 것처럼 AGENTS.md를 변경하지 않습니다.

## 4. 포함 기준과 자산 보호

다음 조건을 충족하는 작업만 기본 포함합니다.

- 코드로 재현 가능한 생성 자산이며 출력 소유권이 명확합니다.
- Scene, Build Settings, 플레이어 저장 데이터 및 운영 콘텐츠 값을 변경하지 않습니다.
- 입력이 없을 때 명확히 실패하며, 임의의 Demo 데이터로 대체하지 않습니다.
- 반복 실행 시 의미 없는 ID·순서·자산 변경을 만들지 않습니다.
- 기존 GUID와 필요한 참조를 보존하고 저장 후 검증이 가능합니다.

Prefab에 필수인 보조 Style 등이 없다면 명시적으로 선언한 경로에 생성할 수 있습니다. 그러나 기존 개발자 설정을 기본값으로 초기화하지 않습니다. 기존 보조 자산 수정이 필요한 마이그레이션은 별도 작업으로 분리합니다.

AppRoot는 안전한 Prefab-only 경로를 분리할 수 있을 때만 포함합니다. AppScene 전체 재생성을 우회 호출하지 않습니다. Demo 생성기가 운영 콘텐츠를 초기화한다면 분리 전에는 제외합니다.

수작업 Prefab, 사용자 Variant, 외부 패키지 자산과 생성 근거가 없는 자산은 제외합니다. 출력 경로는 Assets 아래의 선언된 프로젝트 영역으로 제한하며 상위 경로 이동과 경로 충돌을 검증합니다. 임의 폴더 전체 삭제를 하지 않습니다.

Prefab 재생성은 원본 자산의 사용자 편집을 덮어쓸 수 있습니다. 실행 전 이를 명시하고 Variant·별도 Style 등 사용자 편집 경계를 문서화합니다. GUID 유지가 자식 local file ID나 모든 Scene override 보존을 자동 보장하지 않으므로 실제 참조와 override 회귀 테스트를 수행합니다.

## 5. Editor 안전성과 실패 처리

- Play Mode, 전환 중, 컴파일 중, 실행 중에는 재진입을 차단합니다.
- 미저장 Scene·Prefab Stage·수정 대상의 메모리 변경을 사전 확인합니다. 충돌하는 상태는 사용자가 저장 또는 취소할 때까지 중단합니다.
- Scene이 dirty라는 이유로 자동 저장하거나 닫지 않습니다. Prefab-only 실행이라도 열린 인스턴스에 변경이 반영될 수 있음을 안내합니다.
- 기존 Builder의 SaveAssets가 관련 없는 dirty 자산까지 저장하지 않게 실행 범위를 정리합니다.
- 실행 전에 전체 계획의 알려진 오류를 수집하여 쓰기 전에 차단합니다. 실행 도중 예상하지 못한 실패 가능성은 별도로 처리합니다.
- 실행은 Unity 메인 스레드에서 순차 수행합니다. AssetDatabase 작업을 병렬화하지 않습니다.
- 긴 작업은 진행률을 표시하고 안전한 작업 경계에서 취소합니다. 실행 중인 비취소 작업을 강제 중단하지 않습니다.
- 기본 정책은 실패 즉시 남은 작업 중단입니다. 부분 완료를 성공으로 표시하지 않습니다.
- 모든 경로에서 임시 객체, Prefab 편집 컨텍스트, 진행 UI와 실행 잠금을 finally로 정리합니다.
- 전체 AssetDatabase를 긴 StartAssetEditing 블록으로 묶지 않습니다. 뒤 작업이 앞 작업의 import 결과를 필요로 할 수 있으므로 저장·import 경계를 확인합니다.
- 일괄 실행은 자동 트랜잭션이 아닙니다. 이미 저장한 자산을 Unity Undo로 모두 복구할 수 있다고 약속하지 않습니다.
- 기존 파일 덮어쓰기 전에 복구 가능한 백업 또는 확인된 버전 관리 체크포인트 정책을 제공합니다. git reset이나 사용자 변경의 자동 삭제는 금지합니다.
- 실패 보고에는 완료·실패·취소·미실행 작업, 변경된 경로, 복구 방법을 기록합니다. 도중 자동 재시도로 반복적인 파괴 작업을 수행하지 않습니다.

## 6. 구현 순서

1. 모든 자체 생성 메뉴, 핵심 메서드, 호출 관계와 실제 출력·부수 효과를 조사합니다.
2. 포함·제외·분리 필요 목록을 문서화합니다.
3. Editor.Common에 작은 등록 계약과 계획 검증기를 구현합니다.
4. 안전한 기존 작업부터 Provider에 연결하고 중복 호출을 제거합니다.
5. 실행 계획 창과 순차 Runner, 결과 보고를 구현합니다.
6. 저장된 자산 검증 및 등록 누락 감사 테스트를 추가합니다.
7. 개별 메뉴의 기존 경로를 유지하고 동일 핵심 생성 코드를 호출하게 합니다.
8. 작업 등록 규칙을 AGENTS.md와 개발 문서에 반영합니다.

개발 검증은 격리된 테스트 출력 또는 별도 검증용 프로젝트 사본에서 먼저 수행합니다. 현재 작업 트리에 대한 전체 Rebuild를 테스트라는 이유로 무조건 실행하지 않습니다.

## 7. 테스트와 완료 기준

- 중복 ID, 누락 의존성, 순환 의존성, 출력 경로 충돌, 허용 영역 밖 경로를 쓰기 전에 차단합니다.
- 순서가 매번 같고 공통 선행 작업이 한 번만 실행되는지 검증합니다.
- 새 테스트 Provider가 명시적 등록을 통해 계획에 나타나는지 확인합니다.
- 미분류 Rebuild 메뉴와 제외 사유 누락을 감사 테스트가 검출하는지 확인합니다.
- 미리보기와 등록 검증은 파일 및 Scene을 변경하지 않아야 합니다.
- 누락된 카탈로그·폰트·필수 Profile 등의 입력을 명확히 보고합니다.
- 중간 실패·취소·재진입에서 정리와 결과 보고가 정상인지 확인합니다.
- 저장된 Prefab을 다시 로드하여 Missing Script, 내부 필수 참조, 중첩 Prefab과 외부 Scene 참조 누락을 검사합니다.
- 두 번 실행한 결과의 GUID와 직렬화 차이를 비교하고 불필요한 변경을 제거합니다.
- Scene, Build Settings, 기존 콘텐츠·Style 값, 사용자 Variant와 관련 없는 dirty 자산을 변경하지 않는지 비교합니다.
- 기존 개별 생성 메뉴와 관련 테스트가 유지되는지 검사합니다.
- 실제 Unity 검증 여부와 미검증 항목을 명확히 보고합니다. 코드 컴파일만으로 자산 재생성이 완료되었다고 보고하지 않습니다.

## 문서와 최종 보고

DOCS/development/workflows.md에 확정 메뉴, 실행 전 확인, 복구 절차 및 신규 작업 등록 예시를 추가합니다. 공통 Editor 구조가 확장되므로 DOCS/architecture/project-structure.md와 관련 기능 문서를 갱신합니다. 필요하면 현재 구현을 설명하는 별도 Editor 도구 문서를 만들고 DOCS/README.md에서 연결합니다.

최종 보고에는 실제 일괄 포함 생성기와 출력 자산 목록, 제외 이유, 분리한 부수 효과, 확정 메뉴 경로, 새 생성기 등록 절차, 수행한 테스트, 실제 Rebuild로 변경한 자산 및 미검증 사항을 포함합니다.

