# 작은 하루 · FMV Demo

Unity에서 영상을 보고 선택하며 진행하는 FMV 데모다. 노드 편집기로 분기를 만들고, 참조 영상은 Addressables로 자동 관리한다.

## 실행

1. Unity **6000.3.23f1**로 프로젝트를 연다.
2. `Assets/FMV/Scenes/FmvDemo.unity`를 열고 **Play**를 누른다.
3. 일어나는 영상이 끝나면 **컴퓨터로 간다** 또는 **다시 눕는다**를 선택한다.

두 경로 모두 침대로 돌아와 반복한다. 선택지를 띄운 뒤 **10초 동안 입력이 없으면 다시 눕는다**. 기본 설정은 **Local / Asset Database**이므로 서버 없이 실행할 수 있다.

## 영상과 분기 편집

**Tools > FMV > Sequence Editor**를 열거나 `BedroomLoop.asset`을 더블클릭한다.

- 노드를 선택해 오른쪽에서 **영상·선택지·제한 시간**을 수정한다.
- 출력점을 다른 노드의 입력점으로 연결해 진행 순서를 만든다.
- 오른쪽 아래 **영상 미리보기**에서 재생·일시정지·처음부터 재생한다.
- 노드의 영상 이름을 클릭하면 해당 영상이 **Inspector**에 선택된다.
- **저장 / Ctrl+S**, Undo/Redo를 지원한다.

시간 초과는 **사용 안 함 / 일반 선택지 자동 실행 / 전용 분기** 중에서 설정한다. 자세한 조작법은 [노드 편집기 사용법](Docs/SequenceEditor.md)을 참고한다.

## 영상 교체와 빌드

기존 MP4를 같은 경로에서 교체하고 `.meta`를 유지하거나, 새 영상을 가져와 노드에 지정한다. Play·콘텐츠 빌드 전에 필요한 영상이 자동 등록된다.

실제 번들로 실행하려면 **Tools > FMV > Build Content** 후 **Play Mode > Existing Build**를 선택한다. 영상이나 분기를 수정했다면 다시 빌드한다. 다른 PC에서 내려받을 때는 `git lfs pull`로 영상 파일을 받는다.

## 자세한 내용

| 필요한 내용 | 문서 |
|---|---|
| 노드 연결·미리보기·시간 초과·영상 교체 | [편집기 사용법](Docs/SequenceEditor.md) |
| 자동 등록·빌드·원격 서버 설정 | [Addressables 운영 안내](Docs/Addressables.md) |
| 재생 구조·주요 코드 위치 | [구조와 확장 지점](Docs/Architecture.md) |
| 테스트 결과·실행 방법·남은 검증 | [검증 기록](Docs/Verification.md) |
| 현재 영상의 원본·규격·재생성 | [영상 제작 기록](SourceArt/PhotorealDemo/generation-brief.md) |

현재 범위는 Unity Editor 데모다. **최근 교체한 영상의 Game 뷰 재생과 도구 창 미리보기의 실제 조작 검증은 남아 있다.** 이전 영상으로 수행한 테스트와 구분해 [검증 기록](Docs/Verification.md)에 정리했다.
