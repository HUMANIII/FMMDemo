# 작은 하루 · FMV Demo

Unity Game 뷰에서 짧은 영상을 보고 선택하는 데모다. 침대에서 일어난 뒤 컴퓨터로 가거나 다시 눕고, 두 경로 모두 다시 일어나는 장면으로 돌아온다.

## 실행

1. Unity **6000.3.23f1**로 이 폴더를 연다.
2. `Assets/FMV/Scenes/FmvDemo.unity`를 연다.
3. Play를 누른다. 처음에는 **Local / Asset Database** 모드로 실행된다.
4. 일어나는 영상이 끝나면 **컴퓨터로 간다** 또는 **다시 눕는다**를 선택한다.

영상 준비 중에는 상태가 표시되고, 실패하면 **재시도** 또는 **처음부터**로 복구할 수 있다. 선택에는 제한 시간이 없다. 컴퓨터 사용과 침대 복귀는 자동으로 이어진다.

## 영상과 분기 편집

`Assets/FMV/Content/Sequences/BedroomLoop.asset`의 Inspector에서 노드를 편집한다.

| 필드 | 용도 |
|---|---|
| Start Node Id | 시작할 노드 |
| Id / Title | 고유 노드 ID / 화면 제목 |
| Video | 재생할 VideoClip의 Addressables 참조 |
| Next Node Id | 영상 종료 후 자동으로 이동할 노드 |
| Choices | 선택지 문구와 이동할 노드 ID |

`Next Node Id`와 `Choices`는 동시에 지정하지 않는다. 둘 다 비워 두면 해당 영상에서 끝난다. 루프는 허용한다. 새 시나리오도 `Assets/FMV/Content` 아래에 저장한다. 시작 시나리오는 `Assets/FMV/Settings/FmvRuntimeSettings.asset`에서 선택한다.

실사 영상을 넣는 방법은 두 가지다.

- 기존 MP4를 같은 경로에서 교체하면서 `.meta` 파일을 유지한다.
- 새 영상을 Assets에 가져온 뒤 노드의 Video 참조를 교체한다.

영상의 Addressable 체크·그룹·라벨을 손으로 관리할 필요는 없다. Play 진입과 콘텐츠 빌드 전에 시나리오가 참조하는 영상을 자동 등록한다. 제목과 선택지는 한국어 Noto CJK 글꼴을 사용한다.

권장 영상 형식은 H.264 MP4, yuv420p, 일정한 프레임 속도다. 임시 영상은 1280×720, 30fps, 3초, 무음이며 화면이 조금 확대된다. 음성이 포함된 실사 영상은 첫 오디오 트랙을 AudioSource로 출력한다. 표시 영역은 16:9이고 다른 비율의 영상은 비율을 유지해 맞춘다.

## Addressables 자동 관리

머지 프로젝트 `MergeConflicct`의 빌드 전처리와 라벨 기반 로드 구조를 FMV용으로 이식했다.

| 메뉴 | 기능 |
|---|---|
| Tools > FMV > Synchronize and Validate | 등록·그룹·주소·라벨 동기화와 검증 |
| Validate Only | 변경 없는 데이터 검증 |
| Build Content | 현재 프로필의 콘텐츠 빌드 |
| Play Mode > Asset Database | 에디터 원본 에셋으로 빠르게 실행 |
| Play Mode > Existing Build | 실제 번들 사용, 빌드 최신 여부 검사 |
| Profile > Local / Test / Release | 콘텐츠 경로와 원격 업데이트 방식 전환 |
| Clear FMV Cache (Play Mode) | 재생 정지 후 FMV 라벨의 다운로드 캐시만 삭제 |
| Create Missing Demo Assets | 누락된 기본 데모 에셋 생성; 기존 시나리오·씬은 보존 |

영상 주소는 `fmv/video/<GUID>`, 시나리오 주소는 `fmv/sequence/<GUID>`다. 공통 라벨 `fmv`와 시나리오별 라벨을 자동 지정한다. 공유 영상은 한 번만 등록하고 사용 시나리오의 라벨을 합친다. 관리 대상에서 제외된 항목은 FMV 전용 그룹에서 정리한다.

영상은 개별 비압축 번들, 시나리오는 별도 그룹으로 빌드한다. 빌드 결과와 콘텐츠 지문은 `Library/FMV`에 기록한다. 영상·데이터·경로 설정·번들이 달라지면 Existing Build 실행 전에 재빌드를 요구한다. 이 기록과 번들은 Git에 넣지 않는다.

재생기는 현재 영상과 시나리오만 보유한다. `FmvAddressableManager.LoadAsync<T>`는 해제 가능한 lease를 반환한다. 같은 주소의 동시 요청은 핸들을 공유하며 마지막 소유자가 해제할 때 실제 핸들을 해제한다. 기존 방식의 `LoadAddressableData(label)`, `Get<T>(address)`, `Release(label)`도 제공한다.

## 원격 기능

초기 실행은 **Local**이며 서버가 필요하지 않다. 실제 서비스 연결은 다음 설정 후 사용한다.

1. Project Settings > **FMV Deployment**에서 Test 또는 Release의 다운로드 폴더 URL, 업로드 URL, 비밀번호를 입력한다.
2. 다운로드 폴더 URL은 해당 플랫폼의 번들과 카탈로그가 제공되는 실제 폴더를 가리켜야 한다.
3. Tools > FMV > Upload에서 해당 프로필의 **Build and Upload**를 실행한다.
4. 해당 프로필과 Existing Build 모드를 선택해 실행한다.

업로드 서버는 multipart 필드 `file`과 `x-upload-password` 헤더를 받는 기존 계약을 사용한다. 각 파일의 이름을 유지해 지정된 다운로드 폴더에서 제공해야 한다. 프로필·플랫폼별 URL을 분리한다. 전송은 번들 → 카탈로그 → 해시 순서다. 실패하면 남은 파일 전송을 중단하고 원래 프로필을 복구한다.

초기 원격 카탈로그 이름은 `fmv-v1`로 고정한다. 같은 플레이어 버전의 콘텐츠 갱신에서는 이 값을 유지한다. 실행 시 카탈로그 변경을 확인하고 FMV 의존성의 다운로드 크기와 진행률을 표시한다. 다운로드 캐시와 현재 메모리에 로드된 에셋의 해제는 별개다.

인증 설정은 `UserSettings/FmvDeploymentSettings.asset`에 저장되며 Git과 플레이어 빌드에 포함되지 않는다. 공개 다운로드 URL은 Addressables 프로필·빌드 카탈로그에 포함된다. 실제 외부 서버 운영은 별도 연결·검증 대상이다.

## 개발·검증 자료

- [검증 기록](Docs/Verification.md)
- [구조와 이식 범위](Docs/Architecture.md)
- 원본 그림: `SourceArt/Placeholders`
- MP4 재생성: `Tools/generate_videos.py` (`imageio-ffmpeg==0.6.0` 필요)
- 영상 규격·전체 프레임 확인: `Tools/verify_videos.py`
- 로컬 HTTP 검증 서버: `Tools/remote_test_server.py`
- 자동 테스트: `Tools/verify.ps1`, 빌드·원격 통합 검사: `Tools/verify_integration.ps1`

프로젝트는 로컬 Git과 영상용 Git LFS를 사용한다. 다른 PC에서는 `git lfs pull`로 실제 영상 파일을 받아야 한다. Unity 캐시, 생성된 번들, 테스트 로그, 로컬 인증값은 추적하지 않는다.
