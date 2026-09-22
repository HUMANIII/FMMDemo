# FMV 구조와 이식 범위

## 실행 경로

`FmvRuntimeSettings → FmvPlayer → FmvAddressableManager → VideoPlayer → RenderTexture → FmvView`

씬은 작은 실행 설정만 직접 참조한다. 시나리오와 영상은 Addressables로 읽는다. 시나리오 데이터는 `FmvSequenceDefinition`, 노드는 `FmvNode`, 선택지는 `FmvChoice`다. `VideoReference`는 `AssetReferenceT<VideoClip>`을 상속한다.

재생 상태는 Stopped, Loading, Preparing, Playing, AwaitingChoice, Completed, Error다. 영상 종료 이벤트가 다음 노드 또는 선택 대기를 결정한다. 영상 길이를 타이머로 흉내 내지 않는다. VideoPlayer의 출력용 텍스처와 화면 유지용 텍스처를 분리하여 선택 대기와 로딩 중 마지막 그림을 유지한다.

자동 전환은 종료 콜백이 반환된 다음 프레임에 시작한다. 네이티브 디코더의 종료 처리 안에서 다음 클립으로 교체하지 않도록 한 것이다. 재생 시간은 UnscaledGameTime을 사용하고 데모의 목표 프레임 속도는 60fps다. 마지막 노드에서는 표시용 텍스처만 유지하고 시나리오·영상 핸들을 해제한다.

`StartSequence`, `Choose`, `Restart`, `Retry`, `StopSequence`로 제어하고 `Changed`, `NodeCompleted`로 상태를 관찰한다. 선택은 AwaitingChoice에서 한 번만 받아들인다. 세션과 노드의 취소 범위를 분리해 재시작한 이전 작업이 새 화면을 덮지 않도록 한다.

시간 초과는 `FmvPlayer.Update`가 선택 대기 중에만 `Time.unscaledDeltaTime`으로 계산한다. Editor Pause 변경 후 첫 프레임을 건너뛰어 정지 시간이 카운트다운에 포함되지 않도록 한다. `Choose`와 시간 만료는 같은 전환 함수에서 먼저 확정 여부를 설정하므로 한 번만 전환한다. 모든 노드 취소 경로에서 잔여 시간을 지운다. 일반 선택지의 자동 대상은 `FmvChoice.isTimeoutDefault`, 숨겨진 분기는 `FmvNode.timeoutTargetNodeId`로 구분한다.

`FmvView`는 선택지 수에 맞춰 버튼 풀을 확장하고 두 열의 스크롤 영역에 배치한다. 타이머 영역은 선택 대기와 시간 초과 활성 상태에서만 표시한다. 기존 두 버튼 배열은 호환성을 위해 유지하되 필요한 수만큼 확장한다.

## 시나리오 편집

`FmvSequenceEditor`는 UI Toolkit/GraphView 창이며 `FmvSequenceEditService`가 Undo 기록과 데이터 수정을 담당한다. GraphView 실험 API는 창 코드에만 포함된다. VideoClip 이름 버튼은 GUID로 실제 에셋을 조회하여 `Selection.activeObject`와 `PingObject`를 사용한다. 전역 Selection 변경을 시나리오 전환으로 처리하지 않으므로 영상 선택 시 창의 시나리오와 노드 편집 상태를 유지한다.

`FmvGraphLayout`은 Editor 폴더에 시나리오 GUID별 위치를 저장하며 런타임 시나리오를 직접 참조하지 않는다. 콘텐츠 수집은 기존 `Assets/FMV/Content` 범위를 유지한다. 위치만 변경하면 Addressables 콘텐츠 지문이 유지된다. 수정된 시나리오와 배치는 저장 버튼, 창 종료, 재컴파일 및 Play·빌드 전처리에서 저장한다.

검증은 오류와 경고를 구분한다. 시나리오의 연결·시간 초과 오류는 기존 런타임 및 빌드 검증으로 전달한다. Editor에서는 실제 영상 에셋의 존재도 확인한다. 시작점에서 도달할 수 없는 노드는 경고로 표시하고 루프는 허용한다. 데이터가 불완전해도 편집·저장은 가능하다.

## 기존 프로젝트에서 가져온 구조

참고 원본은 `E:/02.Unity/MergeConflicct/Assets/01.Scripts` 아래의 AddressableManager, AddressableEditorManager와 DataRefineBeforeBuild 계열이다. 원본 프로젝트는 수정하지 않았다.

- `DataRefineBeforeBuildBase`의 전처리 목록 구조와 Fast/Packed/Existing Build 확장을 유지했다.
- 스프라이트 아틀라스 생성 대신 시나리오 참조 영상의 GUID·그룹·라벨 동기화를 수행한다.
- 라벨 로드·조회·해제 사용 방식을 유지하고 VideoClip과 일반 Unity Object를 지원한다.
- 기존의 개별 핸들 대신 딕셔너리를 해제하던 처리, 감소 전 참조 횟수 검사, 중복 로드 핸들 누수, 실패 후 상태 잔류를 수정했다.
- 서버 상수와 인증값은 FMV 전용 Editor 설정으로 분리했다. 게임 재화·대화·아틀라스 의존성은 이식 대상이 아니다.

런타임과 Editor 코드는 별도 asmdef에 둔다. 서버 인증값을 다루는 클래스와 업로드 코드는 Editor 어셈블리에만 포함된다.

캐시 삭제는 재생기의 핸들을 해제한 뒤 Addressables 2.8의 비동기 번들 해제가 완료될 때까지 기다린다. `AssetBundleProvider`의 protected `UnloadingBundles`를 상속 클래스에서 조회하고, 실제 삭제 대상은 `fmv` 라벨의 의존성으로 제한한다. Addressables를 업그레이드할 때는 원격 캐시 통합 테스트를 함께 실행한다.

## 자동 관리와 빌드

Play 진입 훅과 커스텀 빌더가 같은 동기화 함수를 호출한다. 데이터 오류를 먼저 수집하고, 유효한 시나리오에 대해서만 등록한다. 중복 영상은 GUID로 합친다. 주소는 이름이나 파일 경로에 의존하지 않는다.

빌드 영수증은 콘텐츠·그룹·프로필 설정 지문과 실제 출력 파일의 SHA-256을 담는다. Existing Build 모드는 변경된 콘텐츠와 누락·변조된 출력을 거부한다. 자동 빌드 성공으로 실제 재생 성공을 대신 판정하지 않는다.

AssetReference의 GUID 문자열은 일반 Unity 에셋 참조와 다르므로, 시나리오 해시에 더해 참조 영상 각각의 임포트 의존성 해시를 따로 수집한다. 같은 파일 경로와 `.meta`를 유지한 영상 교체도 이 검사에 포함된다.

## 원격 검증 도구

`FmvRemoteVerification.Prepare()`는 loopback 서버가 실행 중일 때 명시적으로 호출하는 검증 도구다. 업로드 계약·인증 실패·중간 실패·프로필 복구를 검사하고, 서버에는 v2를 게시한 뒤 로컬 초기 카탈로그는 v1으로 복구한다. 실제 Play에서 v2 제목과 영상이 나와야 카탈로그 업데이트 경로가 성공한 것이다.

`FmvRemoteVerification.Restore()`는 원래 시나리오·프로필·로컬 서버 설정을 되돌린다. 원격 테스트 중 원래 값은 `.tools/remote-verification/original.json`에 보존되므로 도중에 에디터를 종료해도 복구할 수 있다. 정상 실행에서는 이 도구를 호출하지 않는다.

## 데모 영상

OpenAI의 기본 image_gen 도구로 생성한 장면 5개를 사용한다. 공유 프롬프트는 간단한 16:9 침실 그림, 왼쪽 침대·오른쪽 컴퓨터, 청록색 상의와 짙은 바지의 성인, 따뜻한 햇빛, 크림색 벽과 테라코타색 이불, 글자·워터마크 없는 구성이었다.

이 그림들은 이전 임시 영상의 원본으로 보존한다. 현재 MP4는 실사풍 기준 이미지를 Gemini에 제공해 만든 실제 인물 동작 영상이다. `walk` → `computer` → `return` → `wake`의 마지막 프레임을 이어서 참조했으며, `sleep`은 `return`의 5.5초 이후를 정방향으로 잘라 쓴다. 네 원본은 각각 10초이고 sleep은 4.5초다. 새 생성 영상도 완벽한 자세·소품 연속성은 보장하지 않는다.

`Tools/generate_videos.py`는 `SourceArt/PhotorealDemo/Raw`의 네 원본을 720p30·무음 H.264로 변환하고 기존 MP4를 교체한다. `.meta`와 시나리오 GUID 참조는 유지한다. `--placeholders`는 이전 그림을 3초간 고정한 영상을 재생성하는 명시적 복구 옵션이다. 상세 출처와 프롬프트는 [영상 제작 기록](../SourceArt/PhotorealDemo/generation-brief.md)에 있다.

## 주요 코드 위치

| 경로 | 역할 |
|---|---|
| [FmvSequenceDefinition.cs](../Assets/FMV/Runtime/FmvSequenceDefinition.cs) | 노드·영상 참조·선택지·시간 초과 데이터와 검증 |
| [FmvPlayer.cs](../Assets/FMV/Runtime/FmvPlayer.cs) | 영상 로드·재생·분기·타이머, `StartSequence`·`Choose`·`Restart` |
| [FmvView.cs](../Assets/FMV/Runtime/FmvView.cs) | 영상 화면·가변 선택지 버튼·카운트다운·오류 UI |
| [FmvAddressableManager.cs](../Assets/FMV/Runtime/FmvAddressableManager.cs) | 공유 로드·참조 횟수·핸들 해제 |
| [FmvSequenceEditService.cs](../Assets/FMV/Editor/FmvSequenceEditService.cs) | 화면과 분리된 데이터 수정·Undo·저장·진단 |
| [FmvSequenceEditor.cs](../Assets/FMV/Editor/FmvSequenceEditor.cs) | GraphView·오른쪽 편집 패널·Inspector 연계 |
| [FmvGraphLayout.cs](../Assets/FMV/Editor/FmvGraphLayout.cs) | 콘텐츠와 분리된 노드 배치 저장 |
| [FmvVideoPreview.cs](../Assets/FMV/Editor/FmvVideoPreview.cs) | 도구 창 내부 영상 미리보기·자원 정리 |
| [FmvContentPipeline.cs](../Assets/FMV/Editor/FmvContentPipeline.cs) | Addressables 자동 등록·콘텐츠 지문·빌드 전처리 |

`FmvAddressableManager.LoadAsync<T>`는 해제 가능한 lease를 반환한다. 같은 주소의 동시 요청은 핸들을 공유하며 마지막 소유자가 해제할 때 실제 핸들을 해제한다. 기존 방식의 `LoadAddressableData(label)`, `Get<T>(address)`, `Release(label)`도 제공한다. 에셋 로딩과 영상 준비의 기본 제한 시간은 각각 30초다.

도구 창 미리보기는 `FmvVideoPreview`가 담당하며 Unity 내부 Editor 영상 API를 감싼다. 게임 재생기와 별도로 동작하고 창 닫기·재컴파일·Play 모드 전환 때 정리한다. Unity 버전을 변경할 때 호환성을 다시 확인한다.
