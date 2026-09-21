# FMV 구조와 이식 범위

## 실행 경로

`FmvRuntimeSettings → FmvPlayer → FmvAddressableManager → VideoPlayer → RenderTexture → FmvView`

씬은 작은 실행 설정만 직접 참조한다. 시나리오와 영상은 Addressables로 읽는다. 시나리오 데이터는 `FmvSequenceDefinition`, 노드는 `FmvNode`, 선택지는 `FmvChoice`다. `VideoReference`는 `AssetReferenceT<VideoClip>`을 상속한다.

재생 상태는 Stopped, Loading, Preparing, Playing, AwaitingChoice, Completed, Error다. 영상 종료 이벤트가 다음 노드 또는 선택 대기를 결정한다. 영상 길이를 타이머로 흉내 내지 않는다. VideoPlayer의 출력용 텍스처와 화면 유지용 텍스처를 분리하여 선택 대기와 로딩 중 마지막 그림을 유지한다.

자동 전환은 종료 콜백이 반환된 다음 프레임에 시작한다. 네이티브 디코더의 종료 처리 안에서 다음 클립으로 교체하지 않도록 한 것이다. 재생 시간은 UnscaledGameTime을 사용하고 데모의 목표 프레임 속도는 60fps다. 마지막 노드에서는 표시용 텍스처만 유지하고 시나리오·영상 핸들을 해제한다.

`StartSequence`, `Choose`, `Restart`, `Retry`, `StopSequence`로 제어하고 `Changed`, `NodeCompleted`로 상태를 관찰한다. 선택은 AwaitingChoice에서 한 번만 받아들인다. 세션과 노드의 취소 범위를 분리해 재시작한 이전 작업이 새 화면을 덮지 않도록 한다.

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

## 임시 그림

OpenAI의 기본 image_gen 도구로 생성한 장면 5개를 사용한다. 공유 프롬프트는 간단한 16:9 침실 그림, 왼쪽 침대·오른쪽 컴퓨터, 청록색 상의와 짙은 바지의 성인, 따뜻한 햇빛, 크림색 벽과 테라코타색 이불, 글자·워터마크 없는 구성이었다.

각 장면 요청은 일어나 스트레칭하기, 컴퓨터로 걷기, 컴퓨터 사용하기, 침대 가장자리에 돌아와 앉기, 다시 누워 쉬기다. MP4 변환 도구는 원본 PNG를 변경하지 않고 짧은 카메라 확대 효과와 무음 H.264 영상을 만든다. 실사 촬영 영상 제공 후 같은 참조를 교체한다.
