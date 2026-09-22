# FMV 실사풍 영상 제작 기준

## 요구사항과 현재 상태

- 목적: Unity에서 동영상 재생, 종료, 선택, 분기, 연속 재생을 시연한다.
- 인물이 실제로 움직이는 실사풍 영상이 필요하다. 정지 이미지 확대·이동·전환은 완료 결과로 인정하지 않는다.
- 다섯 장면에서 동일한 인물, 의상, 침실 배치를 유지한다. 대사와 립싱크는 필요하지 않다.
- 기준 이미지: 기본 image_gen 도구로 제작한 `actor-bedroom-reference.png`. 영상 자체가 아닌 인물·공간 참고 이미지다. 한 명의 성인 인물, 의상, 침대·책상 배치와 실사풍 표현을 눈으로 확인했으며 작업 폴더에 복사한 파일의 SHA-256 일치를 확인했다.
- 영상 생성 경로: 사용자가 승인한 Gemini 웹 브라우저 조작. 2026-09-22 Chrome에서 Pro 계정 로그인과 Omni 동영상 생성 화면, 16:9 선택 상태를 확인했다. Runway는 사용하지 않는다.
- 사용자가 Chrome 파일 접근 설정과 Gemini 최초 이미지·파일 생성 동의 절차를 승인했다. `walk` → `computer` → `return` → `wake` 네 원본의 생성과 다운로드를 완료했다. 실제 제출한 문장은 `submitted-prompts.json`에 보존한다. [Gemini 생성 대화](https://gemini.google.com/app/e2ed2409b7bb04d0)에서 원본을 확인할 수 있다.
- 네 원본은 `Raw/`에 보존했다. 각각 1280×720, 24fps, 약 10초, AAC 오디오 포함이다. 납품용 `Ready/` 사본은 30fps·무음으로 변환했다. 서비스에서 삽입한 오른쪽 아래 표시를 보존한다.
- 네 번째 생성 후 Gemini가 한도 도달과 2026-09-22 오전 11:07 재개를 표시했다. 업그레이드하지 않았다. `sleep`은 `Raw/return.mp4`의 5.5초부터 끝까지 정방향으로 잘라 만든 4.5초 영상이며 별도 생성 원본은 없다. 모든 납품 영상에서 실제 인물이 움직인다.
- 다섯 출력 MP4를 `Assets/FMV/Content/Videos`에 교체했다. 기존 영상과 `.meta`는 `.tools/fmv-video-backup/20260922-090921`에 SHA-256 기록과 함께 보존했으며 `.meta` 파일은 바꾸지 않았다.
- 전체 1,335프레임 디코딩과 Unity 임포트·GUID·콘텐츠 참조 검증을 통과했다. 같은 Editor를 다른 작업이 사용 중이어서 이번 교체 뒤 Game 뷰의 두 분기 실행 및 Addressables 재빌드는 수행하지 않았다. 기존 Asset Database 모드는 유지했다.
- 제한: 장면 사이의 의자 위치·모니터 상태·앉는 자세에 약간의 차이가 있고 눕는 장면에서 발이 침대 밖으로 나온다. 동일 인물과 공간을 유지하는 기술 시연용 결과이며 완벽한 장면 접합을 보장하지 않는다.

## 공통 연출

성인 남성 한 명, 짧은 짙은 갈색 머리, 청록색 긴팔 상의, 짙은 회색 바지, 연회색 양말을 유지한다. 침대는 왼쪽, 컴퓨터 책상은 오른쪽이다. 크림색 벽, 테라코타색 이불, 나무 바닥, 중앙 창문과 아침 햇빛을 유지한다. 카메라는 고정하며 인물의 몸과 손, 다리가 실제 동작한다. 자막, 영상 내부 UI, 로고, 추가 인물은 넣지 않는다.

공통 영상 프롬프트:

```text
Single continuous photorealistic live-action shot in the exact bedroom shown in the supplied reference. The same one adult man, same face, short dark brown hair, muted teal long-sleeve top, charcoal trousers and grey socks. Keep the same bed on the left, computer desk on the right, room layout, furniture and soft morning light. Locked tripod camera, natural human anatomy and believable body motion. One complete simple action, no internal cuts, no camera zoom or pan, no extra people, no dialogue, no captions or logos. Finish with a brief settled pose so the next clip or a choice screen can begin cleanly.
```

## 장면별 제작 지시

목표 길이는 모델 확인 전의 연출 기준이다. 영상 생성 모델이 지원하는 길이에 맞춰 조정하며, 기존 임시 영상의 3초를 강제하지 않는다.

| 파일 | 시작 상태 | 실제 동작과 끝 상태 | 목표 길이 |
|---|---|---|---|
| wake.mp4 | 침대에 누워 있음 | 상체를 일으키고 다리를 내려 침대 가장자리에 앉음 | 약 5~8초 |
| walk.mp4 | 침대 가장자리에 앉음 | 일어나 책상까지 걸어가 의자에 앉음 | 약 8~10초 |
| computer.mp4 | 책상 의자에 앉음 | 키보드를 짧게 입력하고 손을 멈춤 | 약 5초 |
| return.mp4 | 책상 의자에 앉음 | 일어나 침대로 돌아와 앉고 누움 | 약 8~10초 |
| sleep.mp4 | 침대 가장자리에 앉음 | 몸을 돌려 다리를 올리고 다시 누움 | 약 5~8초 |

### wake

```text
Start with the man lying comfortably on the bed. He wakes, raises his torso, swings both legs off the near right edge of the bed, and ends seated on the edge with both feet on the floor and his hands resting on his thighs. Keep the physical action smooth and readable. End in the seated pose of the master reference.
```

### walk

```text
Start with the man seated on the edge of the bed, feet on the floor. He leans forward, stands, walks through the open floor space toward the computer desk on the right, turns and sits in the office chair facing the monitor. Finish with both hands resting near the keyboard.
```

### computer

```text
Start with the man seated in the office chair facing the computer. He types briefly with natural small finger and wrist movements, makes a slight head movement toward the screen, then stops typing and rests his hands. He remains seated throughout. The camera and room stay stationary.
```

### return

```text
Start with the man seated at the computer. He takes his hands off the keyboard, stands up from the chair, walks left to the bed, sits on the near edge, then swings his legs onto the mattress and reclines into a comfortable lying pose. Keep each movement sequential and physically believable. End resting on the bed, ready for the wake clip.
```

### sleep

```text
Start with the man seated on the edge of the bed, feet on the floor and hands on his thighs. He turns toward the pillow, lifts his legs onto the mattress, and gently lowers his torso into a comfortable lying pose. End resting on the bed, ready for the wake clip. He does not stand or walk to the desk.
```

## 연속성과 생성 순서

1. 기준 이미지의 인물과 방을 모든 장면의 공통 참조로 사용한다.
2. `walk` → `computer` → `return` → `wake` 순으로 생성한다. 선택한 모델이 지원하면 앞 영상의 마지막 프레임을 다음 영상의 시작 이미지로 사용한다.
3. `sleep`은 생성 한도 때문에 `return`에서 앉아 눕는 구간을 편집했다. `wake` 끝의 앉은 자세와 완전히 같지는 않다.
4. `wake`의 끝과 `walk` / `sleep`의 시작에서 얼굴, 옷, 앉는 위치와 자세가 자연스럽게 이어지는지 확인한다.
5. `return` / `sleep`의 끝과 `wake`의 시작도 누운 자세와 카메라 구도를 대조한다. 필요하면 짧은 편집 컷으로 연결하고, 인물이나 가구가 바뀐 결과는 다시 생성한다.

## 납품 및 검증 기준

- 납품 목표: 1280×720, 30fps 일정 프레임 속도, H.264 MP4, yuv420p, BT.709, 무음. 생성 원본은 별도 보존한다.
- 최종 동영상에서 얼굴·옷·방 배치가 유지되고, 인물 자체가 움직이는지 장면별로 확인한다.
- 전체 프레임 디코딩, 파일 길이·프레임 수·코덱 확인을 기록한다. 출력 프레임 속도 변환은 원본 움직임의 품질을 보장하지 않는다.
- `Tools/verify_videos.py`는 실제 길이·프레임 수를 기록하도록 수정했다. 첫 프레임 대비 SSIM은 움직임의 참고 지표로 남기며 정지 영상이어야 한다는 이전 조건은 제거했다. 납품 대상 다섯 파일만 검사하고 다른 작업의 임시 검증 영상은 보존한다.
- 기존 MP4 교체 시 원본을 보존하고 `.meta` GUID를 유지한다. Addressables 콘텐츠를 다시 빌드하고 두 분기와 자동 연결을 Unity에서 실제 재생한다.
- 파일 디코딩과 Unity 임포트는 완료했다. Unity 실제 재생과 콘텐츠 재빌드는 미실행이며 이전 영상으로 수행한 검증 결과와 구분한다. `Docs/Evidence/media.json`, `Docs/Evidence/photoreal-import.json`, `media-verification.json`에 확인 결과를 기록했다.

## 재생성

프로젝트의 `.tools/python/Scripts/python.exe`로 `Tools/generate_videos.py`를 실행하면 네 원본에서 다섯 납품 파일을 만들고 기존 MP4 경로에 복사한다. `SourceArt/PhotorealDemo/prepare.py`만 실행하면 `Ready/` 사본과 검증 기록만 갱신한다. `Tools/verify_videos.py`는 프로젝트에 들어간 다섯 파일을 디코딩하고 기록한다. 원본 다운로드나 추가 Gemini 요청은 이 스크립트가 수행하지 않는다.

## 기준 이미지 생성 프롬프트

Built-in image_gen, reference input: `SourceArt/Placeholders/wake.png`.

```text
Use case: photorealistic-natural
Asset type: master actor and bedroom continuity reference for a Unity FMV playback technology demo, landscape 16:9.
Input image: the supplied illustration is a ROOM LAYOUT, CLOTHING AND COMPOSITION reference only, not a style reference.
Primary request: Create a single convincing live-action film still of this simple bedroom and its one fictional adult male inhabitant. Convert the illustrated scene into photographic reality, with natural skin texture, real fabric, wood grain, physically plausible soft morning light and ordinary domestic detail.
Room continuity: bed on the left with terracotta duvet, cream walls, wooden floor, large central window with cream curtains, wooden computer desk and muted green office chair on the right, dark computer monitor, small indoor plants. Keep the bed and desk in one wide shot with a clear walkable path between them. Reuse the reference room arrangement, without adding furniture or people.
Actor: one fictional man around 28, short slightly tousled dark brown hair, clean-shaven, natural unretouched face, wearing a plain muted teal long-sleeve crewneck top, dark charcoal lounge trousers and light grey socks. Show his full body and his face clearly. He is seated on the near right edge of the bed, feet planted naturally on the floor, hands resting on his thighs, torso angled slightly toward the camera, a relaxed neutral expression. Arms are down, not stretching.
Camera: fixed tripod, eye-level wide shot with a natural 35 mm lens appearance. Keep the entire bed and computer desk visible. Warm restrained natural colour grade, realistic proportions, enough depth of field to identify the furniture. This is a reference photograph for later actual human movement clips.
Constraints: one continuous full-frame photograph, exactly one adult person; no collage, no panels, no text, no captions, no logos, no watermarks, no illustration, no anime, no 3D-render look. No exaggerated facial expression or unnatural hands.
```
