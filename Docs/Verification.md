# FMV 데모 검증 기록

검증 환경: Windows, Unity 6000.3.23f1, StandaloneWindows64 콘텐츠 대상. 플레이어 실행 파일 배포가 아닌 Unity Editor Game 뷰 실행을 완료 기준으로 사용한다.

## 구성 확인

- Universal 2D 템플릿, URP 17.3.0, Canvas/uGUI 2.0.0(TMP 포함), Input System 1.20.0.
- Addressables 2.8.1, UniTask 2.5.10, Test Framework 1.6.0. `Packages/manifest.json`과 `packages-lock.json`에 버전 기록.
- Unity CLI 1.0.0-beta.10, Editor 연결용 `com.unity.pipeline` 0.7.0-exp.1.
- 현재 MP4는 1280×720, 30fps, H.264 Constrained Baseline, yuv420p, BT.709, 무음이다. wake/walk/computer/return은 10초·300프레임, sleep은 4.5초·135프레임이다. `Tools/generate_videos.py`로 Gemini 다운로드 원본에서 재생성할 수 있다. 이전 3초·90프레임 그림 영상으로 수행한 검증은 아래 과거 기록으로 구분한다.
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

## 실사풍 인물 영상 교체 — 2026-09-22

- Gemini에서 같은 인물·의상·방을 참조한 10초 원본 4개를 생성·다운로드했다. 생성 한도 도달 뒤 sleep은 return의 5.5~10초 구간으로 구성했다. 역재생이나 정지 이미지 이동을 사용하지 않았다.
- 5개 출력의 모든 프레임을 오류 없이 디코딩했다. [media.json](Evidence/media.json)에 길이·프레임 수·코덱·SHA-256·첫 프레임 대비 SSIM을 기록했다. SSIM은 영상 변화의 수치이며 인체 동작 품질의 자동 판정은 아니다. 주요 프레임에서 기립·보행·타이핑·착석·눕기 동작을 별도로 확인했다.
- Unity에서 다섯 파일을 다시 임포트하고 300/135프레임, 30fps, 720p, 오디오 트랙 0개, 기존 GUID 일치를 확인했다. 콘텐츠 검증 오류는 0개였다. [photoreal-import.json](Evidence/photoreal-import.json) 참고.
- 같은 Editor를 다른 작업이 사용 중이어서 이번 교체 뒤 Game 뷰 두 분기 실행과 Addressables 재빌드는 수행하지 않았다. 기존 Asset Database 모드는 유지했다. Existing Build 사용 전에는 콘텐츠를 재빌드해야 한다. 아래 이전 테스트 성공을 새 영상의 실행 검증으로 간주하지 않는다.
- 반복 재생 테스트의 대기 한도를 네 개의 10초 영상 경로에 맞춰 조정했다. 테스트 전체는 이번 영상 교체에서 다시 실행하지 않았다.
- 제한: 장면 사이에 의자 위치·모니터 상태·앉는 자세의 작은 차이가 있고, 눕는 장면의 발 위치가 다소 어색하다. 서비스 표시를 보존했다. 데모용 인물 연속성 수준이며 매끄러운 영화 편집을 보장하지 않는다.

## 임시 영상 흔들림 수정 — 2026-09-22

초기 영상에 넣었던 미세 확대 효과를 제거하고 그림을 고정한 MP4 5개를 다시 생성했다. 확대 필터의 정수 좌표 처리로 작은 위치 변화가 생기는 구조는 [FFmpeg zoompan 구현](https://github.com/FFmpeg/FFmpeg/blob/master/libavfilter/vf_zoompan.c)에서 확인했다. 원본 그림과 영상의 `.meta`는 유지했다.

다섯 영상을 끝까지 디코딩해 각 90프레임·3초·1280×720·30fps·무음을 확인했다. 모든 프레임을 첫 프레임과 비교한 SSIM 최솟값은 영상별 0.997872~0.999091이었다. 기존 wake 영상은 같은 비교에서 0.67984였다. 손실 압축의 미세한 차이를 허용하는 기준 0.995를 적용했으며, 결과는 `Evidence/media.json`에 기록했다.

이번 수정의 Unity 재생 결과는 `Evidence/jitter-fix.json`에 별도로 기록한다. 위의 32개 테스트와 40회 반복 결과는 초기 구현 검증이며, 영상 교체 후 전체 테스트를 다시 실행한 결과가 아니다. 기존 번들은 교체 전 영상을 포함하므로 Existing Build 모드를 사용하려면 콘텐츠를 다시 빌드해야 한다. 기본 Asset Database 모드에는 교체한 영상이 바로 반영된다.

## 노드 편집기·시간 초과·Inspector 연계 — 2026-09-22

이번 검사는 흔들림을 제거한 기존 MP4 5개와 새 그래프/타이머 구현을 대상으로 한다. 앞의 초기 데모 검증 기록과 별도로 실행했다.

| 실행 | 결과 | 증거 |
|---|---|---|
| EditMode | 25/25 통과 | `Evidence/graph-editmode.json` |
| Asset Database 시간 초과·가변 선택지 | 8/8 통과 | `Evidence/graph-fast-timeout.json` |
| Asset Database 기존 재생 회귀 | 10/10 통과, 두 분기 각 10회 | `Evidence/graph-fast-regression.json` |
| Local 콘텐츠 빌드 | 성공, 산출물 11개 및 지문 검증 | `Evidence/graph-local-build.json` |
| Existing Build 전체 재생 | 18/18 통과, 두 분기 각 10회 | `Evidence/graph-packed-playmode.json` |

EditMode는 노드 ID, 진행 방식 정리와 Undo/Redo, 노드 삭제 시 유입 연결·시작점 정리, 자동 대상 순서 변경·삭제, 잘못된 제한 시간, 자동 대상 누락·중복, 루프·자기 연결·도달 불가 경고를 검사했다. 공유 영상 선택, 이동한 영상의 GUID 추적, 삭제된 영상 처리, 저장된 레이아웃과 콘텐츠 지문의 분리, 창 재열기와 데이터 보존도 확인했다.

재생 검사는 실제 VideoPlayer가 영상 종료로 선택 대기에 진입한 뒤 수행했다. 무제한 대기, 전용 분기 만료, 순서를 바꾼 일반 선택지의 자동 실행, 수동 선택과 만료 경합, 재시작·정지·오류·재시도, 노드 재진입, `timeScale=0`, 세 번째 버튼 클릭과 11개 선택지 스크롤을 검사했다. 각 검사 종료 시 미해제 핸들은 0개였다.

실제 마우스로 노드 제목을 끌어 위치 변경과 저장을 확인했다. 배치를 변경해도 콘텐츠 지문은 유지됐고, 시간 초과 연결점을 sleep에서 computer로 끌어 옮기면 지문이 바뀌었다. 한 번의 Undo로 기존 연결과 지문을 복구했다. 첫 확인에서는 연결 교체 Undo가 두 단계였으므로 GraphView의 삭제·생성 콜백을 같은 Undo 그룹에 기록하도록 수정하고 실제 드래그를 다시 검증했다. `Evidence/graph-interaction.json`, [연결 조작 화면](Evidence/graph-connect.jpg) 참고.

그래프에서 wake 노드를 편집하는 상태로 walk의 영상 이름을 클릭했다. Inspector가 walk VideoClip의 임포트 설정과 미리보기를 표시했고, 그래프 화면 위치와 오른쪽 wake 편집 패널은 유지됐다. 재컴파일 후에도 열린 시나리오·선택·화면 위치가 복원됐다. [Inspector 연계 화면](Evidence/graph-inspector.jpg) 참고.

일반 Play의 자동 등록도 새 영상 복사본으로 다시 확인했다. 진입 전 미등록이었던 GUID가 Play 전처리에서 등록됐고, 해당 영상의 89번 프레임까지 재생했다. 별도 수동 동기화는 호출하지 않았다. Play 중에는 노드 이동이 비활성화되고 영상 이름 클릭은 Inspector 선택을 유지했다. `Evidence/graph-autoreg-before.json`, `Evidence/graph-pause-autoreg.json`, [Play 중 Inspector](Evidence/graph-play-inspector.jpg) 참고.

Editor Pause를 약 59.8초 유지하는 동안 남은 시간은 7.98517561초로 동일했다. 재개 후 8.285초에 sleep 영상의 2번 프레임을 확인했다. 남은 선택 시간에 다음 영상 준비·초기 디코딩이 더해진 시간이다. 전환 후 타이머는 비활성화됐고 정지 후 핸들은 0개였다. `Evidence/graph-pause-resume.json`, [카운트다운 화면](Evidence/graph-game-countdown.jpg) 참고.

실제 마우스로 세 번째 버튼을 클릭해 computer 노드와 디코딩 프레임을 확인했다. 이 검증은 저장된 시나리오를 바꾸지 않는 메모리 복사본으로 실행했다. [세 선택지 화면](Evidence/graph-game-three-choices.jpg), [전환 후 영상](Evidence/graph-third-button-playback.jpg), `Evidence/graph-third-button.json` 참고. 앞의 초기 검증에서 미완료였던 실제 마우스 입력은 이번 화면 검증에서 별도로 수행한 것이다.

최종 자동 검사는 EditMode 25개, Asset Database 18개, Existing Build 18개로 **61개 통과**했다. 두 분기는 두 재생 모드에서 각각 10회씩, 총 40회 순환했다. 검증용 영상·선택지·라벨은 정리했고 Local / Asset Database와 침실 데모의 10초 전용 분기를 복원했다. `Evidence/graph-restored.json`에 기존 번들의 최신 판정도 기록했다.

이 기능의 검증 중 기존 영상 5개의 SHA-256은 흔들림 수정 결과와 일치했다. 테스트가 동적으로 추가한 TMP 글리프 캐시는 작업 전 백업으로 복원했고, 기존 글꼴 에셋의 SHA-256 `9EEDA8BE22E8BCECD67C7877DB6DF875799DFF301886A2EED68F225C2384F5BD`를 확인했다. SampleScene 삭제 상태와 별도 진행 중인 실사 영상 작업은 보존했다. 소스·문서의 공백 검사는 통과했으며, 씬 YAML의 빈 `m_Name: ` 값에 붙는 Unity 직렬화 공백은 그대로 두었다.

사용법과 저장 규칙은 [SequenceEditor.md](SequenceEditor.md)에 정리했다. 외부 프로그램 연동과 가져오기·내보내기는 후속 작업이다. 기존 원격 업로드/카탈로그 코드는 이번에 변경하지 않았으며 외부 서버 검증을 새로 수행한 결과로 간주하지 않는다.

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

## 도구 창 하단 영상 미리보기 — 2026-09-22

Unity 컴파일과 오른쪽 패널의 생성, 선택 노드 연결을 확인했다. 화면 제어가 중단되어 재생·일시정지·처음부터 버튼, 영상 전환과 창 닫기 시 정리의 실제 화면 검증은 완료하지 못했다. 앞의 노드 편집기 테스트 결과는 이 미리보기 기능의 검증 결과가 아니다.

## 검증 범위의 한계

외부 서버 배포, Windows 독립 실행 빌드, 모바일/WebGL, 실제 촬영 영상의 코덱·음성 재생은 이번 범위에 포함되지 않는다. 음성 출력 코드는 준비되어 있지만 임시 영상은 무음이므로 실제 오디오 파일을 받은 뒤 확인해야 한다.

구현 API는 [Unity Addressables 빌드 확장 안내](https://docs.unity3d.com/Packages/com.unity.addressables@2.8/manual/build-scripting-builds.html)를 참고했다. 영상 형식은 [Unity Editor 영상 호환성 안내](https://docs.unity3d.com/6000.0/Documentation/Manual/VideoSources-FileCompatibility.html)를 기준으로 선택하고 이 PC에서 실제 디코딩으로 확인했다.
