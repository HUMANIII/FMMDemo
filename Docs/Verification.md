# FMV 데모 검증 기록

검증 환경: Windows, Unity 6000.3.23f1, StandaloneWindows64 콘텐츠 대상. 플레이어 실행 파일 배포가 아닌 Unity Editor Game 뷰 실행을 완료 기준으로 사용한다.

## 구성 확인

- Universal 2D 템플릿, URP 17.3.0, Canvas/uGUI 2.0.0(TMP 포함), Input System 1.20.0.
- Addressables 2.8.1, UniTask 2.5.10, Test Framework 1.6.0. `Packages/manifest.json`과 `packages-lock.json`에 버전 기록.
- Unity CLI 1.0.0-beta.10, Editor 연결용 `com.unity.pipeline` 0.7.0-exp.1.
- 원본 PNG 5개와 MP4 5개. 1280×720, 30fps, 90프레임, 3초, H.264 Constrained Baseline, yuv420p, BT.709, 무음. `Tools/generate_videos.py`로 재생성 가능.
- 로컬 Git main 브랜치, MP4/MOV/WebM용 Git LFS. 캐시·번들·로그·로컬 인증 설정은 제외.

## 자동 검증

검증 결과와 실행 시간은 최종 실행 후 아래 표와 `Evidence/test-summary.json`에 기록한다. 원본 NUnit XML과 상세 Editor 로그는 각각 `TestResults`, `Logs`에 남으며 Git 추적 대상은 아니다.

| 실행 | 결과 | 검증 범위 |
|---|---|---|
| EditMode | 11/11 통과 | 데이터 연결, 공유 영상, 이동·삭제, 반복 동기화, 동일 GUID 영상 교체, 빌드 지문 |
| Asset Database PlayMode | 10/10 통과 | 실제 MP4 프레임·종료, 각 분기 10회, 중복 클릭, 손상 영상, 취소·타임아웃, 참조 해제 |
| Existing Build PlayMode | 10/10 통과 | 실제 로컬 번들을 사용하는 동일 재생 검사 |
| Remote PlayMode | 1/1 통과 | v1 → v2 카탈로그 갱신, 영상 다운로드·재생, 캐시 삭제·재다운로드 |

반복 테스트는 `VideoPlayer.frameReady`의 프레임 번호와 `loopPointReached` 이후 노드 순서를 검사한다. 각 선택지의 uGUI 클릭 이벤트를 호출하고, 이어지는 중복 선택이 거부되는지 검사한다. 매 루프에서 시나리오와 현재 영상의 핸들 2개만 남아야 하며 종료 후 0개여야 한다. 실제 마우스 입력은 별도 Game 뷰 확인으로 구분한다.

초기 반복 테스트는 NUnit의 기본 180초 제한으로 종료되어 한도를 420초로 조정했다. UniTask의 `AsTask` 취소가 faulted Task의 `OperationCanceledException`으로 전달되는 경로도 검사에 반영했다. 최종 결과에는 수정 후 실행 결과만 사용한다.

첫 번들 반복 검사에서는 영상 종료 대기가 한 차례 시간 초과되었다. 종료 콜백 이후 다음 프레임에 자동 전환하도록 보강하고, 재생 시계와 60fps 목표를 명시한 뒤 전체 검사를 다시 실행해 통과했다. 최초 실패가 없었던 것처럼 취급하지 않기 위해 이 기록을 남긴다.

## 독립 실행 확인

- 새 영상 참조만 지정한 뒤 일반 Play 진입으로 자동 등록: 통과. 진입 전 미등록 상태, 진입 후 GUID 주소 등록 및 89번 프레임 재생을 확인했다. `editor-PrepareAuto.json`, `editor-CheckAuto.json` 참고.
- Addressables 빌드 API가 미등록 영상을 자동 수집: 통과. 수동 동기화 없이 빌더 전처리로 등록되었으며 실제 출력 11개가 기록되었다. `editor-BuildAuto.json` 참고.
- 오래된 빌드의 Play 차단: 통과. ExitingEditMode 진입 이벤트를 관찰한 뒤 Play가 취소되었고 빌드 최신 판정도 실패했다. `editor-CheckStale.json` 참고.
- Game 뷰 한국어와 16:9 영상 비율: 화면 확인 완료. [선택 화면](Evidence/game-choice.png) 참고. 물리 Esc 입력으로 Computer Use가 중지되어 **실제 마우스 클릭 검증은 미완료**다. uGUI 클릭 이벤트를 통한 두 분기 반복은 자동 테스트에서 통과했다.
- loopback HTTP 서버의 multipart `file`, `x-upload-password`, 번들 → 카탈로그 → 해시 업로드 순서: 통과. HTTP 500 이후 나머지 전송 중단, 잘못된 인증값의 HTTP 403, 실패 후 이전 프로필 복구를 확인했다. `remote-protocol.json`, `remote-http.json` 참고.
- 로컬 v1 카탈로그에서 서버 v2 카탈로그를 내려받고 변경된 시나리오·영상 재생: 통과. `[remote v2]` 제목과 89번 프레임을 확인했다. `remote-playback.json` 참고.
- FMV 캐시 삭제·재다운로드: 통과. 다운로드 후 필요 용량 0바이트, 삭제 후 3,862,284바이트, 재다운로드 후 v2 재생을 확인했다. 첫 통합 검사에서 확인한 비동기 번들 해제와 캐시 삭제의 순서 문제를 수정한 후 통과했다.

자동 테스트는 합계 32개 통과했다. 두 분기는 두 재생 모드에서 각각 10회씩, 총 40회 순환했다. 단순 핸들 수 확인 외에 실제 MP4 디코더 프레임과 종료 이벤트로 흐름을 확인했다. 최종 기본값은 Local / Asset Database이며 원격 테스트의 임시 시나리오·URL·인증값은 복구했다.

## 재실행

이 프로젝트의 Editor를 닫은 뒤 PowerShell에서 실행한다. 다른 프로젝트의 Editor를 종료할 필요는 없다.

```powershell
cd E:\02.Unity\FMVDemo
./Tools/verify.ps1 -Mode All
# 개별 실행: -Mode EditMode, Fast, Packed
```

loopback 통합 검증은 Editor를 연 상태에서 별도로 실행한다.

자동 등록의 독립 검증은 열린 Editor에서 `Tools/verify_editor.ps1`의 PrepareAuto → 일반 Play → CheckAuto → Play 종료 → RestoreAuto 순서로 진행한다. 준비 단계는 영상 참조만 바꾸고 아직 미등록인지 확인한다. 콘텐츠 빌드는 CLI eval 안에서 실행할 때 연결 응답이 멈춘 사례가 있어 배치 Editor에서 검사한다. Editor를 닫고 `Tools/verify_integration.ps1 -Mode Build`를 실행하면 미등록 영상 빌드와 오래된 빌드의 실제 Play 차단을 검사하고 복구한다.

```powershell
python Tools/remote_test_server.py --root .tools/remote-server --port 18084
```

Unity CLI의 `eval`로 `FmvDemo.Editor.FmvRemoteVerification.Prepare();`를 호출한다. `.tools/remote-verification/ready.json`이 생기면 Play하여 `[remote v2]` 제목과 실제 영상 프레임을 확인한다. Play 종료 후 `FmvDemo.Editor.FmvRemoteVerification.Restore();`를 호출한다. 서버는 이 검증을 위한 loopback 전용 도구이며 실제 서비스 서버가 아니다.

자동 원격 검증은 서버 실행 후 Editor를 닫고 `Tools/verify_integration.ps1 -Mode Remote`로 수행한다. 빌드·원격 검증을 함께 수행하려면 `-Mode All`을 사용한다. 원격 테스트 어셈블리는 전용 준비 상태가 아니면 건너뛰므로 일반 테스트 실행에 서버가 필요하지 않다.

## 검증 범위의 한계

외부 서버 배포, Windows 독립 실행 빌드, 모바일/WebGL, 실제 촬영 영상의 코덱·음성 재생은 이번 범위에 포함되지 않는다. 음성 출력 코드는 준비되어 있지만 임시 영상은 무음이므로 실제 오디오 파일을 받은 뒤 확인해야 한다.

구현 API는 [Unity Addressables 빌드 확장 안내](https://docs.unity3d.com/Packages/com.unity.addressables@2.8/manual/build-scripting-builds.html)를 참고했다. 영상 형식은 [Unity Editor 영상 호환성 안내](https://docs.unity3d.com/6000.0/Documentation/Manual/VideoSources-FileCompatibility.html)를 기준으로 선택하고 이 PC에서 실제 디코딩으로 확인했다.
