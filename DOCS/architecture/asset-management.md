# Addressables 에셋 관리

## 런타임 경계

게임과 UI 코드는 `Addressables` 정적 API를 직접 호출하지 않습니다.

```text
UI 및 화면
└── IAssetProvider
    └── AddressablesAssetProvider
        └── Addressables
```

`IAssetProvider.LoadAsync<T>`는 `AssetLease<T>`를 반환합니다. Lease를 해제하면 대응하는 Addressables Handle도 정확히 한 번 해제됩니다. `AssetScope`는 화면이나 목록에서 로드한 Lease를 보관하고 Dispose 시 역순으로 모두 해제합니다. 병렬 로드와 Dispose가 경쟁하면 늦게 도착한 Lease를 즉시 해제하고 `ObjectDisposedException`으로 완료하므로 소유권이 Scope 밖에 남지 않습니다.

취소 또는 실패가 발생하면 Provider가 생성된 Handle을 해제합니다. 새 화면 자산은 모두 준비된 뒤 기존 Scope와 교체하므로 로딩 도중 기존 이미지가 사라지지 않습니다.

`Character2DView.ApplyArtworkAsync`, `ActionGridPanel.LoadIconsAsync`, `FlexibleLayoutBackground.LoadAndApplyAsync`는 완료를 기다릴 수 있는 `Task`를 반환합니다. Unity 수명 주기처럼 기다릴 수 없는 호출 지점만 관찰 래퍼를 사용하여 취소를 무시하고 예외를 기록합니다. 새 Lease는 필드로 소유권이 이전되기 전까지 지역 변수가 `finally`에서 책임집니다.

## ID와 직접 참조

저장 데이터와 게임 모델에는 `ui/characters/mira/default` 같은 안정적인 ID만 기록합니다. Sprite, Material과 Prefab 경로는 기록하지 않습니다.

현재 정의 자산의 직접 Sprite 참조는 Edit Mode 미리보기와 이전 직렬화 자산의 fallback으로만 유지합니다. Play Mode에서 Addressables ID가 있으면 Addressables 결과를 우선합니다.

| 사용처 | Addressables 필드와 수명 |
| --- | --- |
| `CharacterAppearanceDefinition` | 외형별 또는 fallback Sprite ID를 보관하고 `Character2DView`가 현재 캐릭터 Lease를 소유합니다. |
| `ActionGridEntry` | `IconAssetId`를 보관하고 `ActionGridPanel`의 목록 Scope가 모든 아이콘 Lease를 소유합니다. |
| `FlexibleLayoutBackgroundStyle` | 배경·효과 Sprite와 Material ID를 보관하고 `FlexibleLayoutBackground`가 현재 스타일 Scope를 소유합니다. |
| UI Prefab | `Gameplay_Common` 그룹에 등록하며 화면 조합 시스템이 `IAssetProvider`로 로드해야 합니다. |

## 그룹 정책

그룹은 파일 확장자가 아니라 함께 로드하고 해제하는 수명을 기준으로 나눕니다.

| 그룹 | 현재 용도 |
| --- | --- |
| `SharedUI` | Action 아이콘 Atlas와 공통 배경 스타일입니다. |
| `Gameplay_Common` | Story, Character, Action과 Flexible Layout 운영용 Prefab입니다. |
| `Character_Demo` | 데모 캐릭터 일러스트입니다. 실제 콘텐츠는 캐릭터 또는 챕터별 그룹으로 분리합니다. |

Addressables 그룹과 주소는 `AddressableAssetEditor`가 생성합니다. 생성기가 만든 Sprite subasset은 `base-address[SpriteName]` 형식으로 참조합니다.

등록 도구가 생성하거나 갱신하는 로컬 그룹은 `Pack Together`, LZ4, Include In Build와 Prevent Updates 설정을 명시합니다. 그룹 분리는 Build Layout Report와 실제 기기 메모리 측정 후 결정하며, 현재 도구가 알지 못하는 기존 그룹을 일괄 변경하지 않습니다.

## 병렬 로드와 실패 정책

`ActionGridPanel`은 동일한 `IconAssetId`를 하나의 요청으로 통합하고 Inspector의 `Max Concurrent Icon Loads` 한도 안에서 병렬 로드합니다. 결과는 같은 ID를 사용하는 현재 Entry 모두에 적용합니다. 개별 아이콘 실패는 경고 후 건너뛰며 다른 아이콘 로드를 계속합니다.

`FlexibleLayoutBackground`은 배경과 효과의 Sprite·Material을 동시에 요청합니다. 선택 에셋 하나가 실패하면 Editor fallback 또는 기본 표현을 사용하고 나머지 에셋은 계속 적용합니다. 캐릭터 이미지는 실패 시 기존 표시를 유지합니다.

Lease를 해제하기 전에 `Image.sprite`와 `Image.material` 참조를 먼저 제거합니다. 참조 횟수가 0이어도 같은 번들의 다른 에셋이나 Unity 내부 캐시 때문에 메모리가 즉시 감소한다고 가정하지 않습니다.

## Editor 검증

`Tools > TxT RPG > Addressables > Validate Settings`는 빈 주소, 대소문자만 다른 중복 주소, 존재하지 않는 GUID와 필수 그룹 스키마 누락을 검사합니다. `Build Player Content`도 같은 검증을 먼저 실행하며 오류가 있으면 빌드를 중단합니다. 그룹별 번들 크기와 암시적 종속성은 Addressables Build Layout Report로 별도 검토합니다.

## 외부 로더와 실패 처리

`SetAssetProvider`로 테스트용 또는 플랫폼별 Provider를 주입할 수 있습니다. 기본값은 상태를 보관하지 않는 `AddressablesAssetProvider.Shared`입니다. 네트워크 재시도, 원격 카탈로그 정책과 다운로드 UI는 별도 플랫폼·콘텐츠 서비스에서 처리해야 합니다.

Addressables를 사용해도 자동으로 메모리가 절약되지는 않습니다. 실제 기기에서 Addressables Event Viewer와 Memory Profiler로 그룹 크기, 중복 종속성, 로드·해제 시점과 텍스처 메모리를 측정해야 합니다.
