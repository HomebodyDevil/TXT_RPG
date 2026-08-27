# 개발 및 검증 절차

## StoryTextPanel을 씬에서 사용하기

1. 씬에 Canvas를 준비합니다.
2. `Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab`을 Canvas 아래에 배치합니다.
3. 앵커와 크기는 부모 화면 레이아웃 정책에 맞게 설정합니다.
4. 게임 또는 스토리 컴포넌트에서 `StoryTextPanel` 참조를 직렬화합니다.
5. 현지화가 완료된 문자열로 `StoryMessage`를 생성하여 `AddMessage`를 호출합니다.

```csharp
[SerializeField] private StoryTextPanel storyTextPanel;

public void ShowNarration(string localizedBody)
{
    storyTextPanel.AddMessage(new StoryMessage(localizedBody));
}

public void ShowDialogue(string localizedSpeaker, string localizedBody)
{
    storyTextPanel.AddMessage(new StoryMessage(localizedBody, localizedSpeaker));
}
```

## Edit Mode에서 설정 확인하기

1. `Assets/TxTRPG/UI/DEMO/StoryTextPanelDemo.prefab`을 Prefab Mode로 엽니다.
2. 중첩된 `StoryTextPanel` 오브젝트를 선택합니다.
3. 스크롤바 위치·배경·손잡이 크기와 투명도 설정을 변경합니다.
4. Prefab 또는 Scene View에서 최신 메시지가 아래쪽에 배치된 모습을 확인합니다.

데모 데이터의 문구를 수정한 뒤 다음 메뉴를 실행하면 미리보기 항목이 다시 생성됩니다.

```text
Tools > TxT RPG > Refresh Story Text Panel Edit Mode Preview
```

## 자산 재생성 메뉴

| 메뉴 | 효과 |
| --- | --- |
| `Tools > TxT RPG > Rebuild Story Text Panel Prefabs` | 운영용 메시지와 패널 프리팹을 기본 구조로 다시 생성합니다. |
| `Tools > TxT RPG > Rebuild Story Text Panel Demo` | 데모 데이터와 데모 프리팹을 기본 상태로 다시 생성합니다. |
| `Tools > TxT RPG > Refresh Story Text Panel Edit Mode Preview` | 현재 데모 데이터로 미리보기 항목을 다시 생성합니다. |

운영용 프리팹 재생성은 수동으로 적용한 프리팹 변경을 덮어쓸 수 있습니다. 생성기 코드가 권위 있는 구조인지 확인한 뒤 실행하십시오.

데모 프리팹을 다시 생성하면 기존 미리보기 항목이 제거될 수 있습니다. 데모 재생성 후에는 Edit Mode 미리보기 새로고침 메뉴도 실행하십시오.

## 테스트

Edit Mode 테스트는 `Assets/TxTRPG/UI/Tests/Editor`에 있습니다.

| 테스트 클래스 | 검증 범위 |
| --- | --- |
| `StoryTextPanelTests` | 투명도 경계값과 운영용 프리팹 필수 참조를 검증합니다. |
| `StoryTextPanelDemoTests` | 데모 데이터의 양, 발화자 조합, 로더와 패널 연결을 검증합니다. |
| `StoryTextPanelEditModePreviewTests` | 데모 데이터 개수와 직렬화된 미리보기 항목 개수가 일치하는지 검증합니다. |

관련 변경 후에는 다음 항목을 확인합니다.

1. `TxTRPG.UI`, `TxTRPG.UI.Editor`, `TxTRPG.UI.Tests.Editor` 어셈블리가 오류 없이 컴파일되는지 확인합니다.
2. 관련 Edit Mode 테스트를 실행합니다.
3. 데모 프리팹을 Prefab Mode에서 열어 최신 메시지 하단 정렬과 상단 투명도 감쇠를 확인합니다.
4. Play Mode에서 미리보기 항목이 제거되고 런타임 메시지만 생성되는지 확인합니다.
5. 스크롤바 위쪽 끝과 아래쪽 끝의 정렬을 확인합니다.
6. `Allow User Scrolling`을 끈 상태에서 스크롤바와 사용자 스크롤이 비활성화되는지 확인합니다.
7. 왼쪽·오른쪽 스크롤바와 배경 표시·숨김 조합을 확인합니다.
8. 긴 문장, 빈 발화자, 여러 줄 본문을 확인합니다.

## 변경 영향 확인표

| 변경 대상 | 함께 확인할 대상 |
| --- | --- |
| `StoryMessage` | `StoryMessageItem.Bind`, 데모 데이터, 호출자, 저장·현지화 경계 |
| `StoryMessageItem` | 메시지 프리팹 생성기, Content 레이아웃, 투명도 적용 |
| `StoryTextPanel` | 운영 프리팹, 데모 중첩 프리팹, 스크롤 동기화, 테스트 |
| 스크롤바 또는 Viewport 구조 | 생성기, 직렬화 참조, 좌우 배치, 입력 방식 |
| 데모 데이터 | 런타임 로더, Edit Mode 미리보기 재생성, 미리보기 테스트 |
| Editor 생성기 경로·메뉴 | 이 문서와 `DOCS/architecture/story-text-panel.md` |
| 어셈블리 정의 | 프로젝트 구조 문서, 플레이어 빌드 포함 여부, 테스트 참조 |

## 다국어 글꼴 확인

현재 TextMeshPro 필수 리소스의 기본 Liberation Sans는 한국어 전체 글리프를 제공하지 않습니다. 다국어 기능을 구현할 때 다음 작업이 필요합니다.

- 지원 언어의 글리프를 포함하는 TMP Font Asset을 준비합니다.
- 언어별 또는 공통 fallback 목록을 설정합니다.
- 동적 폰트 사용 시 빌드 크기, 메모리와 플랫폼별 폰트 가용성을 확인합니다.
- 한국어, 영어, 일본어와 긴 문자열 언어에서 줄바꿈과 레이아웃을 검증합니다.

## 문서 갱신 확인

구조 변경을 완료하기 전에 다음 질문을 확인합니다.

- 새 클래스나 자산의 책임이 기존 문서에서 설명되는가?
- 클래스 또는 어셈블리 의존 방향이 바뀌었는가?
- 프리팹 계층이나 직렬화 참조가 바뀌었는가?
- 생성·검증 메뉴 또는 실행 절차가 바뀌었는가?
- 현재 구현과 향후 계획이 문서에서 명확하게 구분되는가?

하나라도 해당하면 같은 변경에서 `DOCS` 문서를 갱신합니다.
