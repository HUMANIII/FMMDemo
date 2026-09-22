# FMV Addressables 운영 안내

기본 실행은 Local / Asset Database이며 서버 없이 사용한다. 원격 연결과 실제 번들 실행이 필요할 때 아래 절차를 따른다.

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

영상은 개별 비압축 번들, 시나리오는 별도 그룹으로 빌드한다. 빌드 결과와 콘텐츠 지문은 `Library/FMV`에 기록한다. 영상·데이터·시간 초과 설정·경로 설정·번들이 달라지면 Existing Build 실행 전에 재빌드를 요구한다. 그래프 노드 위치는 별도 Editor 에셋에 저장하므로 배치만 바꾸면 콘텐츠를 다시 빌드할 필요가 없다. 빌드 기록과 번들은 Git에 넣지 않는다.

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

관련 구조는 [구조와 이식 범위](Architecture.md), 로컬 HTTP 서버와 통합 검사 방법은 [검증 기록](Verification.md)을 참고한다. Unity 캐시·생성된 번들·테스트 로그·로컬 인증값은 Git 추적 대상에서 제외한다.
