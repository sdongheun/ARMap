# Marker To Speech Bubble Plan

## 1. 목적

현재 구조는 다음처럼 분리되어 있다.

- 화면 위 마커
- 하단 `QuickInfoCard`

이 구조는 기능상 동작하지만, 다음 문제가 있다.

- 어떤 마커가 현재 선택되었는지 직관성이 약함
- 마커와 정보 피드백이 분리되어 있음
- 코드와 씬 구조가 이중화되어 있음

따라서 목표는 다음과 같다.

- 기본 상태에서는 단순 마커를 보여준다
- 휴대폰 중앙 포커스에 들어온 마커는 `말풍선형 마커`로 확장된다
- 하단에 별도 카드가 올라오지 않도록 한다
- 선택된 마커 자체가 정보 UI 역할을 하도록 바꾼다

## 2. 최종적으로 바꾸고 싶은 UX

### 기본 상태
- 건물마다 일반 마커 표시
- 비선택 마커는 단순 핀 모양
- 건물 이름은 숨기거나 매우 짧게만 표시

### 포커스 상태
- 화면 중앙에 온 마커만 말풍선 형태로 확장
- 마커 안에 건물명 1줄 표시
- 필요하면 카테고리도 1줄 추가
- 사용자는 말풍선을 탭해서 상세 정보 페이지로 이동

### 선택 해제 상태
- 중앙에서 벗어나면 다시 일반 핀으로 축소
- 다른 마커가 중앙으로 오면 이전 마커는 원래 상태로 돌아감

## 3. 현재 관련 구조

### 씬
- [`GeospatialTestScene.unity`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scenes/GeospatialTestScene.unity)

### 마커/라벨 제어 코드
- [`ARUIManager.cs`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/ARUIManager.cs)
- [`GeospatialManager.cs`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/GeospatialManager.cs)
- [`BuildingMarker.cs`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/BuildingMarker.cs)

### 현재 분리된 UI
- 상단 상태 카드
  - `ScanningCard`
  - `DetectedCard`
- 하단 카드
  - `QuickInfoCard`
- 화면 위 마커 라벨
  - `ScreenMarkerRoot`

## 4. 어떤 방향으로 바꿀 것인가

핵심은 `QuickInfoCard` 역할을 줄이고, `ScreenMarkerRoot` 아래의 마커가 직접 정보를 보여주게 만드는 것이다.

즉 바뀌는 구조는 다음과 같다.

- 기존:
  - 마커 표시
  - 하단 카드 별도 표시
- 변경 후:
  - 마커 자체가 핀 상태와 말풍선 상태를 모두 가짐
  - 중앙 포커스 시 말풍선형으로 확장

## 5. 구현 방식

### 5-1. 유지할 것
- `uGUI`
- `Canvas`
- `ScreenMarkerRoot`
- `GeospatialManager`의 선택 판단 로직
- `ARUIManager`의 화면 마커 관리

### 5-2. 제거하거나 축소할 것
- `QuickInfoCard` 중심 흐름
- 하단 카드 애니메이션
- 하단 카드에 의존하는 상세 진입 흐름

### 5-3. 새로 만들 상태
화면 마커에 두 가지 시각 상태를 둔다.

- `Pin State`
  - 단순 핀 아이콘
  - 최소한의 존재감
- `Bubble State`
  - 말풍선 배경
  - 건물명
  - 선택 강조

## 6. Unity에서 실제로 수정해야 하는 것

## 6-1. 씬 오브젝트 기준

현재는 `Canvas` 아래에 다음이 있다.

- `TopArea`
- `ScanningCard`
- `DetectedCard`
- `QuickInfoCard`

변경 후에는 다음처럼 생각하면 된다.

- `ScanningCard`, `DetectedCard`는 유지 가능
- `QuickInfoCard`는 비활성 또는 제거 후보
- 실제 건물 정보 피드백은 `ScreenMarkerRoot` 아래 런타임 오브젝트가 담당

즉 Unity 에디터에서 직접 많이 만질 대상은 `QuickInfoCard`보다 `ARUIManager`와 런타임 생성 마커 구조이다.

## 6-2. 코드에서 수정할 핵심 함수

### [`ARUIManager.cs`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/ARUIManager.cs)

#### `UpdateScreenMarkers(...)`
- 어떤 마커가 선택 상태인지 받음
- 여기서 `isSelected` 에 따라
  - 핀 상태
  - 말풍선 상태
  로 나눠서 보여주게 수정

#### `GetOrCreateScreenMarkerView(...)`
- 현재는 핀 이미지 + 라벨 배경 구조
- 이 부분을 `말풍선 상태까지 포함한 구조`로 다시 잡아야 함

예상 구성:
- `PinImage`
- `BubbleBackground`
- `BubbleTitleText`
- 필요 시 `BubbleCategoryText`

#### `CreateScreenMarkerBackground(...)`
- 현재 라벨 배경 생성용
- 말풍선 배경으로 재사용 가능
- 필요하면 새 함수로 분리

### [`GeospatialManager.cs`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/GeospatialManager.cs)

#### `UpdateScreenSpaceMarkers(...)`
- 현재 선택된 건물을 `isSelected` 로 넘김
- 이 흐름은 그대로 유지 가능

즉 `선택 판단`은 `GeospatialManager`
`시각 표현`은 `ARUIManager`
로 유지하는 것이 가장 안전하다.

## 7. 실제 Unity 수정 순서

### 1단계. 기존 하단 카드 의존도 끊기

목표:
- 중앙 선택 시 `QuickInfoCard`가 아니라 마커만 확장되게 준비

해야 할 일:
- [`ARUIManager.cs`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/ARUIManager.cs) 에서 `ShowQuickInfo()` 흐름 축소 또는 비활성
- `QuickInfoCard`를 더 이상 메인 UX로 보지 않도록 정리

Unity에서 확인:
- `Canvas > TopArea > QuickInfoCard` 는 남겨두더라도 비활성 가능
- 최종적으로 안정화되면 제거 가능

### 2단계. 마커 오브젝트 구조 확장

목표:
- 각 `ScreenMarker_{id}`가 핀 상태와 말풍선 상태를 동시에 가질 수 있게 함

구조 예시:
- `ScreenMarker_{id}`
  - `PinImage`
  - `BubbleBackground`
  - `BubbleTitleText`

초기 상태:
- `PinImage` 활성
- `BubbleBackground` 비활성
- `BubbleTitleText` 비활성

선택 상태:
- `PinImage`는 작게 남기거나 숨김
- `BubbleBackground` 활성
- `BubbleTitleText` 활성

### 3단계. 애니메이션 추가

목표:
- 마커가 갑자기 바뀌지 않고 자연스럽게 확장되게 함

추천 효과:
- 스케일 업
- 알파 페이드
- 배경 width 확장

예시:
- 비선택:
  - `PinImage.scale = 1`
  - `Bubble.alpha = 0`
- 선택:
  - `PinImage.scale = 0.9`
  - `Bubble.alpha = 1`
  - `Bubble.scale = 1`

이건 `Coroutine` 또는 `Update/Lerp`로 구현 가능

### 4단계. 탭 영역 연결

목표:
- 말풍선 자체를 탭해서 상세정보로 이동

방법:
- `ScreenMarker_{id}` 루트에 `Button` 또는 `Image + EventTrigger` 추가
- 선택된 말풍선 탭 시 상세정보 열기

주의:
- 현재처럼 하단 카드 버튼 대신, 마커 자체를 탭 타겟으로 바꾸는 방향

## 8. Unity 에디터에서 무엇을 보면 되는가

### Hierarchy
- Play 중 `Canvas` 아래에 생성되는
  - `ScreenMarkerRoot`
  - `ScreenMarker_{id}`
를 확인

### Inspector
- `RectTransform`
  - 위치
  - 크기
- `Image`
  - 핀 sprite
  - 말풍선 배경 sprite 또는 색
- `TextMeshProUGUI`
  - 건물명
  - 정렬
  - 폰트 크기

즉 이 작업은 씬에 고정 UI를 배치하는 것보다, **Play 중 생성된 런타임 마커 오브젝트를 보면서 튜닝하는 작업**에 가깝다.

## 9. 왜 이 방식이 현재 구조보다 나은가

- 현재 어떤 마커가 선택되었는지 더 즉시 알 수 있음
- 하단 카드와 마커가 분리된 이중 구조를 줄일 수 있음
- 사용자가 시선과 정보 피드백을 같은 위치에서 받음
- 상세 진입 흐름이 더 자연스러워짐

## 10. 주의할 점

- 한 화면에 마커가 너무 많으면 말풍선이 겹칠 수 있음
- 따라서 선택된 마커 하나만 말풍선으로 확장하는 것이 좋음
- 비선택 마커는 단순 핀으로 유지해야 함
- 처음부터 `QuickInfoCard`를 바로 삭제하지 말고, 말풍선 UX가 안정화된 뒤 제거하는 것이 안전함

## 11. 추천 작업 순서 요약

1. `QuickInfoCard`를 유지하되 숨길 준비만 한다
2. `ScreenMarker_{id}` 구조를 핀 + 말풍선형으로 확장한다
3. 선택 상태 애니메이션과 닫힘 애니메이션을 안정화한다
4. 긴 텍스트와 내부 레이아웃을 화면 기준으로 다시 튜닝한다

## 12. 실제 진행한 구현 과정

이번 작업은 처음부터 완성형 구조로 가지 않고, 여러 단계를 거치며 조정했다.

### 12-1. 초기 접근

처음에는 다음 구조로 시작했다.

- 비선택 마커: 기존 핀 PNG
- 선택 마커: 별도 선택 PNG
- 하단 `QuickInfoCard` 유지

이 구조는 동작은 했지만, 다음 문제가 있었다.

- 어떤 마커가 선택되었는지 즉시 이해하기 어려웠음
- 마커와 정보 위치가 분리되어 있었음
- 하단 카드와 화면 마커를 동시에 관리해야 해서 구조가 이중화되었음

### 12-2. 말풍선 PNG 직접 사용 시도

다음 단계에서는 `speech-bubble-svgrepo-com.png` 를 사용해 선택된 마커를 말풍선으로 확장하려고 했다.

이때 적용한 방식:

- 비선택 마커: `none_select_marker.png`
- 선택 상태: 말풍선 PNG를 직접 배경으로 사용
- 말풍선 안에
  - 건물 아이콘
  - 건물명
  - 카테고리
  - 주소
  를 코드로 배치

### 12-3. 9-slice 적용 시도

말풍선 길이가 건물명/주소에 따라 달라져야 했기 때문에 9-slice를 적용했다.

하지만 이 방식은 아래 문제가 있었다.

- 꼬리가 포함된 말풍선 이미지는 9-slice에 적합하지 않았음
- 말풍선 상단과 본체는 늘어나지만 꼬리의 비율이 어색해짐
- 사용자가 보기에는 `마커가 말풍선으로 변하는 느낌`보다 `핀 위에 말풍선 이미지를 얹은 느낌`이 강했음

즉, 기술적으로는 가능했지만 시각적으로는 만족도가 낮았다.

### 12-4. 흰 배경 카드 재사용 방식으로 전환

이후 방향을 바꿨다.

- 별도 말풍선 PNG 대신
- 기존 `QuickInfoCard` 에서 쓰던 흰 배경 스프라이트를 재사용
- 핀이 사라지며 그 위에 흰 카드형 배경이 올라오는 구조로 변경

이 방식의 장점:

- 기존 프로젝트 톤과 자연스럽게 맞음
- 9-slice 왜곡 문제가 줄어듦
- 내부 텍스트 정렬과 크기 계산이 쉬워짐
- 배경 카드의 확장/축소 애니메이션이 더 안정적으로 보임

## 13. 시행착오 정리

### 13-1. 선택 상태가 계속 유지되던 문제

문제:

- 중앙에서 벗어나도 말풍선이 닫히지 않음
- 다른 곳을 봤다가 돌아와도 열린 상태로 남음

원인:

- `GeospatialManager` 에서 중앙 기준 선택이 없어도
- 화면에 마커가 보이면 fallback으로 다시 선택 대상을 만들어주고 있었음

해결:

- 중앙 기준 선택만 사용하도록 fallback 선택 경로 제거
- 중앙에 들어오면 열리고, 중앙에서 벗어나면 닫히는 흐름으로 수정

### 13-2. 하단 카드 참조가 끊겨 라벨이 안 뜨던 문제

문제:

- 마커는 보이는데 하단 카드 또는 요약 정보가 뜨지 않음

원인:

- 오브젝트 이름 최신화 과정에서
  - `quickBuildingNameText`
  - `quickDistanceText`
  - `quickInfoTapTarget`
  참조가 씬에서 끊어졌음

해결:

- `GeospatialTestScene.unity` 의 `ARUIManager` 직렬화 참조 복구
- 이후 화면 라벨/선택 흐름 정상화

### 13-3. 말풍선이 열릴 때 핀 그림자가 어색하던 문제

문제:

- 핀은 줄어드는데 그림자는 그대로 남아서 분리되어 보임

원인:

- 선택 애니메이션에서 핀만 scale/alpha 변경
- 그림자 레이어는 같은 축으로 움직이지 않았음

해결:

- 핀과 그림자를 동시에 이동/축소
- 그림자 알파도 같이 줄여 전환감을 맞춤

### 13-4. 긴 텍스트가 `...` 으로 잘리던 문제

문제:

- 건물명이 길면 카드 폭은 늘어나지만
- 일정 길이 이상에서는 `...` 처리됨

원인:

- 제목 텍스트가 1줄 기준 `Ellipsis` 상태였음

해결:

- 제목 텍스트를 여러 줄 허용으로 변경
- 배경 높이가 제목 높이에 맞춰 늘어나도록 수정

### 13-5. 텍스트가 배경 아래로 삐져나오던 문제

문제:

- 긴 제목/주소가 들어오면 하단 텍스트가 배경 밖으로 보였음

원인:

- 카드 전체 rect와 실제 보이는 흰 카드 영역이 달랐음
- 재사용한 흰 배경 스프라이트에는 그림자와 여백이 포함되어 있었는데
- 내부 텍스트는 rect 가장자리 기준으로 너무 바깥쪽에 배치되어 있었음

해결:

- 내부 패딩을 더 크게 재설정
- 전체 텍스트 블록 높이 기준으로 아이콘과 텍스트 시작선을 다시 계산

## 14. 현재 사용 중인 방식

현재는 다음 구조를 사용하고 있다.

### 14-1. 기본 마커 상태

- 비선택 마커는 `none_select_marker.png`
- 화면 위에서 단순 핀 형태로 표시

### 14-2. 선택 마커 상태

- 중앙 포커스에 들어오면 선택
- 핀은 약간 줄고 위로 이동
- 그림자도 같이 이동/축소
- `QuickInfoCard` 의 흰 배경 스프라이트를 재사용한 카드가 위로 확장
- 카드 안에는 다음 정보 표시
  - 건물 아이콘
  - 건물명
  - 카테고리
  - 주소

### 14-3. 사용된 기술 방식

- `uGUI` 기반
- `ARUIManager.cs` 에서 런타임으로 `ScreenMarker_{id}` 생성
- `Image + RectTransform + TextMeshProUGUI + CanvasGroup + Button` 조합
- 선택 여부는 `GeospatialManager.cs` 에서 계산
- 시각 표현과 애니메이션은 `ARUIManager.cs` 에서 처리

### 14-4. 현재 구조의 장점

- 기존 프로젝트 구조를 크게 깨지 않음
- 상세 정보 진입도 기존 흐름을 재사용 가능
- AR 위에서 선택된 대상이 더 분명하게 보임
- 하단 카드 의존도가 줄어듦

### 14-5. 현재 구조의 한계

- 재사용한 흰 배경 이미지가 원래 하단 카드용 자산이라 내부 여백 보정이 필요함
- 매우 긴 건물명/주소에 대해서는 계속 레이아웃 튜닝이 필요함
- 마커가 많을 때 카드끼리 겹칠 가능성은 여전히 있음
3. 선택 마커만 말풍선으로 바뀌게 한다
4. 말풍선 탭으로 상세정보 연결
5. 충분히 안정화되면 `QuickInfoCard` 제거

## 12. 결론

현재 프로젝트에서는 `uGUI`로 충분히 구현 가능하다.

중요한 점은:
- 새로운 시스템으로 바꾸는 것이 아니라
- 현재 `ScreenMarkerRoot` 기반 구조를 확장하는 방향으로 가는 것이 가장 안전하다는 점이다.

즉 이 작업은 "유니티에서 완전히 새 UI를 만드는 것"보다는
"기존 마커 오브젝트를 상태형 UI로 재구성하는 작업"으로 이해하면 된다.
