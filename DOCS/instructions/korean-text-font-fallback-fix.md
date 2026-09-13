# 한국어 표시 오류 진단과 TMP 대체 폰트 적용

## 목적과 사용자 결정

TMP_MainScene을 포함한 관련 UI에서 한국어가 깨지는 원인을 확인하고 코드·폰트 자산·설정·Prefab에 필요한 수정을 실제 적용한다. 사용자가 선택한 방향은 기존 영문 폰트를 유지하고, 배포 가능한 무료 한글 고딕 폰트를 한국어용 대체 폰트로 보완하는 것이다. 전체 UI 폰트를 일괄 교체하지 않는다.

이 문서는 구현 지침이며 원인이 모두 확정되었다는 의미는 아니다. 한국어용 폰트 추가만으로 문자열 인코딩 문제까지 해결되었다고 판단하지 않는다.

## 사전 확인과 근거

AGENTS.md, DOCS/README.md, DOCS/architecture/story-text-panel.md, DOCS/architecture/quick-items-and-game-windows.md, DOCS/architecture/asset-management.md, DOCS/development/workflows.md를 읽는다.

작성 시점에 Assets 내에서 확인한 원본 폰트는 `Assets/TextMesh Pro/Fonts/LiberationSans.ttf`이고, `Assets/TextMesh Pro/Resources/TMP Settings.asset`의 전역 `m_fallbackFontAssets`는 빈 목록이다. 이것만으로 모든 UI가 같은 폰트를 사용하거나 한국어 오류가 모두 글리프 누락 때문이라고 단정하지 않는다. 폰트별 fallback과 실제 런타임 참조도 확인한다.

먼저 프로젝트 Unity/TMP 버전과 관련 폰트 문서를 확인한다. API와 동적 아틀라스·빌드 동작을 구현할 때는 해당 버전의 패키지 소스 또는 공식 Unity 문서를 근거로 사용한다. CodeGraph가 있으면 문자열 공급·폰트 대입 호출자를 먼저 탐색하고 실제 소스로 검증한다.

## 범위와 경로

| 기존 경로 | 확인·변경 대상 |
| --- | --- |
| `Assets/TextMesh Pro/Resources/TMP Settings.asset` | 기본 폰트·전역 대체 폰트·빌드 시 동적 데이터 처리 설정이다. |
| `Assets/TextMesh Pro/Resources/Fonts & Materials/` | 기존 LiberationSans SDF 및 fallback·Material 참조를 확인한다. |
| `Assets/TxTRPG/UI/Runtime/StoryTextPanel.cs` | 전달되는 문자열과 메시지 생성 경로를 확인한다. |
| `Assets/TxTRPG/UI/Prefabs/StoryMessageItem.prefab` | 본문·발화자의 실제 Font Asset과 Material을 확인한다. |
| `Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab` | 메시지 Prefab과 표시 설정을 확인한다. |
| `Assets/TxTRPG/UI/Prefabs/GameMenuPanel.prefab` | 메뉴 버튼과 연결된 View의 한국어를 확인한다. |
| `Assets/TxTRPG/UI/Editor/StoryTextPanelPrefabBuilder.cs` | 재생성으로 폰트 설정이 되돌아가는지 확인한다. |
| `Assets/Scenes/TMP_MainScene.unity` | 실제 증상과 Override를 확인한다. |
| `Assets/Scenes/AppScene.unity` | 기존 초기화 경로로 실행·빌드 검증한다. |

한국어를 표시하는 모달, 가방, 컨텍스트 메뉴, 체력·수량·주사위 결과 등도 실제 관련 Prefab과 생성 경로를 추적한다. 신규 폰트와 라이선스 자산의 제안 위치는 `Assets/TxTRPG/UI/Fonts/Korean/`이다. 기존 폴더 관례가 있으면 재사용하고 실제 경로를 문서화한다.

## 원인 분리

1. 문제가 발생한 Game View와 Console을 기록하고 실제 TMP.text 문자열·Unicode 값을 확인한다. `주사위 굴리기`, `가방`, `설정` 등 알려진 문자열로 재현한다.
2. 문자열은 정상인데 네모·누락 글리프 경고가 나타나면 Font Asset, 원본 폰트의 글리프 범위와 fallback 연결을 확인한다.
3. 문자열 자체가 `?`, U+FFFD 또는 잘못된 문자 조합이면 원본 데이터·파일 읽기·JSON·현지화·로그 출력의 인코딩을 추적한다. 폰트로 해결하지 않는다. 셸 출력에서만 깨지는 것인지 게임에서도 깨지는지 구분한다.
4. 일부 글자가 잘리거나 흐릿하거나 다른 색으로 보이면 Atlas, Material, SDF 파라미터, 마스크, 줄 높이·폰트 크기 문제를 구분한다.
5. 텍스트 생성 직후나 Scene 전환 때만 나타나면 초기화·자산 로드·폰트 재할당 순서를 확인한다.

기존 정상 한국어 문자열이나 저장 파일을 일괄 재인코딩하지 않는다. 실제 손상 경로만 수정하고 복구할 수 없는 문자열은 원래 의미를 추측하여 덮어쓰지 않는다.

## 폰트 선택과 자산 관리

- 무료 한글 고딕 폰트 중 게임 배포·상업적 사용·폰트 임베딩이 허용되는 자산을 선정한다. 원저작자 공식 배포처와 라이선스를 확인하고 출처·버전·라이선스 파일을 함께 보관한다. OS 설치 폰트에 의존하거나 라이선스를 추정하지 않는다.
- 한국어 완성형 음절과 일반적으로 사용하는 자모·문장 부호·숫자 혼용 사례를 확인한다. 지원하는 문자 범위를 문서화하고 모든 Unicode 문자를 지원한다고 주장하지 않는다.
- 원본 폰트, TMP Font Asset, Atlas, Material의 참조와 배포 포함 경로를 관리한다. 폰트별 불필요한 복제 및 패널별 개별 Atlas를 만들지 않는다.
- 대체 폰트 적용은 가능한 한 기존 TMP fallback 경계를 활용한다. 기존 영문 Font Asset과 Material Preset을 보존하며 모든 텍스트 컴포넌트의 폰트를 무조건 덮어쓰는 런타임 검색 코드를 추가하지 않는다.
- 전역 fallback이 관련 UI를 충분히 처리하는지 먼저 검증한다. 필요할 때만 폰트별 fallback을 추가하고 순환 참조·중복·검색 순서를 확인한다. 영문 글리프가 원래 폰트로 유지되는지 확인한다.

## Atlas와 런타임 정책

런타임에 새로운 한국어 문구가 들어올 수 있으므로 현재 화면의 몇 글자만 구워 넣고 완료하지 않는다. 원본 폰트가 포함된 동적 fallback 또는 검증된 정적 범위와 동적 보완 등 현재 버전에 적합한 최소 구성을 선택한다. 아틀라스 크기·multi-atlas 여부·문자 범위의 결정 근거를 기록한다.

한글 전체를 매우 큰 단일 텍스처로 만들거나 패널마다 폰트를 복제하지 않는다. 새 글리프 추가 시점의 지연과 Atlas 메모리를 확인한다. multi-atlas를 무제한 메모리 해결책으로 취급하지 않는다. 알려진 공통 문구를 준비 단계에 미리 추가하는 경우 범위와 비용을 제한한다.

Editor에서는 보이지만 Player 빌드에서 동적 원본 폰트가 빠지거나 빌드 과정에서 Atlas가 초기화되는 경우를 검증한다. 배포 후 OS 폰트나 Editor 캐시에 의존하지 않아야 한다. 이미 사용하는 Addressables와 연동된다면 표시 중 자산이 해제되지 않게 수명을 유지하되 이번 작업을 위해 폰트 전체를 새 로딩 체계로 이관하지 않는다.

## 기존 UI와 효과 유지

한국어·영문 혼합 텍스트의 기준선, 크기, 두께, 자간과 줄 높이를 확인한다. fallback이 생성하는 하위 텍스트 렌더러에도 색상·투명도·Outline·Mask가 적절히 적용되는지 확인한다. StoryTextPanel의 오래된 메시지 Fade가 한글과 영문에서 다르게 보이지 않아야 한다.

한국어 추가로 문장이 잘리면 해당 영역의 wrap·높이·overflow 설정을 확인한다. 전체 UI를 축소하거나 폰트 크기를 일괄 줄이는 방식으로 숨기지 않는다. 큰 레이아웃 재설계와 번역 시스템 전면 구축은 범위에서 제외한다.

## 적용 절차와 검증

Editor 연결·컴파일·미저장 변경을 확인한 뒤 필요한 설정과 자산만 적용·저장한다. Prefab 전체 재생성이나 TMP 패키지 자산 삭제를 기본 방법으로 사용하지 않는다. 생성기가 관련 설정을 덮어쓴다면 최소 수정하고 재생성 결과의 일관성을 검증한다.

다음 항목을 자동 검증과 실제 화면 검증으로 구분하여 수행한다.

1. 정상 문자열이 UI에 도달하는지 검사하고 인코딩 회귀를 확인한다.
2. 고정 문구 외에 처음 표시하는 한국어, 완성형 음절, 자모, 한국어·영문·숫자 혼합 문자열을 확인한다. 입력 테스트에는 `주사위 결과: D6 → 5`, `가방 · 설정`, `가나다라마바사`, `ㄱㄴㄷ ㅏㅑㅓ` 등을 포함한다. 특정 기호가 미지원이면 한국어 문제와 구분한다.
3. Story 본문·발화자, GameMenuPanel, 모달·가방·컨텍스트 메뉴의 한글과 기존 영문을 확인한다. 주사위 임시 버튼이 구현되어 있다면 클릭 결과도 확인한다.
4. 작은 화면·큰 화면·확대된 텍스트·스크롤·오래된 메시지 Fade·마스크에서 누락과 잘림을 확인한다.
5. Scene 재로드·Play 재시작·저장된 자산 재열기 후 재현성을 확인한다. 정상 문구에서 missing glyph 경고가 없는지 확인한다. 경고를 비활성화하는 방식으로 통과시키지 않는다.
6. 해당 Unity 버전의 Font Asset 검사 API를 활용해 fallback을 포함한 글리프 커버리지를 검증한다. API 검사 성공만으로 화면 확인을 대체하지 않는다.
7. 가능하면 실제 Player 빌드에서 실행하여 Editor 캐시 없이 표시되는지 확인한다. 수행한 빌드 타깃과 모바일 미검증 여부를 명시한다. 새 글리프 추가 전후 Atlas 개수·크기와 확인 가능한 비용을 기록하고 측정 없이 저메모리·무지연을 주장하지 않는다.

## 문서와 완료 보고

실제 정책을 `DOCS/architecture/korean-font-fallback.md`에 작성하고 DOCS/README.md에 연결한다. 폰트 출처·라이선스·지원 범위·fallback 연결·Atlas 정책·빌드 포함·교체 방법과 제한 사항을 기록한다. 주요 자산 구조는 project-structure.md, 제작·검증 절차는 development/workflows.md에 반영한다.

최종 보고에 확인된 원인과 추정을 구분하고, 변경 자산, 실제 화면 전후 비교, 영문 유지 여부, 테스트·빌드 결과와 미검증 항목을 포함한다. 필수 수동 Tools 단계가 남으면 AGENTS.md의 별도 `사용자가 수행할 적용 절차`에 정확한 순서·메뉴·저장·성공 확인을 코드 블록으로 제시한다. 실제 적용하지 않은 도구 작성만으로 수정 완료라고 보고하지 않는다.
