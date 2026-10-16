# 전투 말풍선 아이콘·후열 교체 연출

브랜치: `codex/gameplayEdit`. 커밋·푸시 없음. 기존 수정 사항과 씬/프리팹/콘텐츠 데이터는 보존합니다.

## 말풍선 제작

캐릭터에 연결된 `BattleSpeechConfig` 또는 `BattleSpeechBubble`의 기존 Rules 항목에서 **표시 내용**을 설정합니다.

1. `표시 방식`: 텍스트만 / 아이콘만 / 텍스트 + 아이콘.
2. `도트 아이콘`: Project의 Sprite를 끌어 넣습니다. TMP Sprite Asset을 별도로 만들 필요 없습니다.
3. `아이콘 높이`: 640×480 기준 크기, 기본 24. 원본 비율을 유지합니다. 도트 원본은 Point 필터, 압축 없음과 원본 픽셀의 정수 배율 크기를 권장합니다. 공용 텍스처 임포트 설정을 코드가 임의 변경하지는 않습니다.
4. 기존 발동 조건·확률·텍스트 치환·유지 시간은 그대로입니다. 아이콘 미지정이면 텍스트로 폴백합니다. 혼합은 왼쪽 아이콘, 오른쪽 텍스트이며 아이콘 폭을 제외하고 줄바꿈합니다.

기존 대사는 기본값 `텍스트만`이므로 변경할 필요가 없습니다. 아이콘 Image는 최초 필요 시 한 번 만들고 재사용합니다. 아이콘 전용→텍스트 전용으로 바꿔 표시해도 이전 아이콘이 남지 않습니다. 텍스트 크기 측정 뒤 타이핑을 시작하도록 호출 순서도 정리했습니다.

기본 설정 예시 위치:
- `Assets/_Game/Content/Dialogue/Data/BattleSpeech/BattleSpeechConfig_Enemy_Default.asset`
- `Assets/_Game/Content/Dialogue/Data/BattleSpeech/BattleSpeechConfig_Player_Default.asset`

**글자 깨짐은 미확인입니다.** 사용자에게 윤곽 흐림/떨림인지, 글리프 누락/잘림인지와 화면을 요청했습니다. 현재 공용 프리팹은 World Space Canvas + Silver SDF, 소스 크기64를 기준 화면14로 표시하며 등장 크기 반응도 있습니다. 이 설정 사실만으로 원인을 확정할 수 없어 폰트·재질·Pixel Perfect 카메라는 바꾸지 않았습니다. 이번 작업을 해당 시각 문제의 해결로 간주하지 마세요.

## 전열 전멸 → 후열 진입

전열 전체 사망 + 살아 있는 후열 존재 시에만 다음 흐름을 사용합니다.

1. 전열의 SpriteRenderer 알파를 **0.5초** 동안 0으로 만듭니다.
2. 기존 승계 규칙으로 전열을 비활성화하고 준비된 후열을 활성화합니다. 새 캐릭터를 생성하지 않습니다.
3. 후열은 현재 카메라의 왼쪽 경계 밖에서 **BattleMove** 상태로 출발합니다. 각 슬롯의 Y/Z는 유지합니다.
4. **0.55초 / OutCubic**으로 각 전투 위치에 들어오고 **BattleIdle**로 전환합니다.
5. 기존 합류 안내 후 턴 계산을 재개합니다.

조절 위치: BattleManager Inspector → **후열 교체 연출** → 전열 사라지는 시간 / 후열 진입 시간 / 화면 밖 여유 거리.

전환 중에는 전투 명령과 실시간 방어 입력을 받지 않습니다. HP, 후열 편성, 승계 순서, 턴 계산 공식은 변경하지 않습니다. 유효 후열이 없으면 기존 패배 흐름입니다. 중단·전투 종료·씬 정리 시 소유한 Sequence를 Kill(false)하고 원색/알파, 후열 좌표/방어 기준점/Idle을 복원합니다. null/파괴된 렌더러·캐릭터는 건너뜁니다.

## 파일

- 수정: `BattleSpeechBubble.cs` — 기존 Rule/호출 API 유지, 선택적 아이콘 데이터와 뷰.
- 수정: `BattleSpeechBubbleLayout.cs` — 독립적인 텍스트/아이콘 내부 배치.
- 신규: `BattlePartyWavePresentation.cs` (+meta) — 퇴장/진입 DOTween과 정리 소유.
- 수정: `BattleManager.cs` — 즉시 승계 대신 연출 순서와 전환 입력 가드.
- 회귀 코드: `BattleSpeechBubbleLayoutTests.cs`, `BattlePartyWaveRuntimeTests.cs`.
- 문서: 당일 기록/색인/전투·대사 규칙/구현 계획.

추가 Hierarchy 참조는 없습니다. 기존 자산에는 새 필드의 기본값을 사용하며, 별도 씬·프리팹 재작성이나 재생성은 하지 않았습니다.

## 검증

- Runtime/Editor CLI 빌드 오류0, 기존 경고3(Febucci1, ConfigPanelUI2).
- 실제 `CalculateContent` 소스와 관리형 수학 대역으로 1,000개 크기/비율 조합, 6,002개 조건 확인 통과. 아이콘 비율·경계·텍스트와의 분리·기존 텍스트 영역을 검사했습니다. 엔진 렌더링 검증은 아닙니다.
- Unity 회귀 코드 추가: Rule 복제, 아이콘/혼합 배치, 0.5초 중간 알파, 후열 활성화 시점, 화면 밖 진입 좌표, 중단·파괴 정리. 컴파일만 했으며 NUnit 실행하지 않았습니다.
- Unity Play/Refresh/Reimport/씬 저장하지 않았습니다. 글씨 실제 선명도와 이동 애니메이션은 수동 확인 대상입니다.

수동 확인: 실험실의 기존 후열 전멸 실습(3+3)에서 전열 전멸 → 동시 페이드 → 왼쪽 진입 → Idle/다음 턴. 말풍선은 같은 규칙에서 텍스트/아이콘/혼합을 번갈아 표시하고, 긴 한글/넓은 아이콘/다음 전투 재사용을 확인합니다.

## 작업 절차 메모

Unity가 생성한 이전 임시 MSBuild include가 `Temp`에서 사라져 재사용할 수 없었습니다. 현재 csproj의 포함 목록을 먼저 확인한 뒤 이번 신규 소스만 `Temp/SpeechWaveIncludes.targets`로 추가하여 컴파일했습니다. Temp 파일은 영구 산출물로 가정하지 않으며 재생성 시 중복 Compile 항목을 제외합니다. 이전 파일 부재 원인은 확정하지 않았습니다.
