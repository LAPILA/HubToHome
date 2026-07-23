# HUBTOHOME-93 오디오 라우팅 및 심리스 BGM 복귀

## 변경 사항

- `AudioManager` 프리팹에 UI와 Ambience 전용 `AudioSource`를 추가했다.
- `GameAudioMixer`를 `Master > BGM`, `Master > SFX > UI/Voice/Ambience` 구조로 정리했다.
- UI 조작음은 `PlayUISFX`, 맵 환경음은 `PlayAmbience`와 `StopAmbience`를 통해 재생한다.
- Voice 소스 누락 시 SFX로 안전하게 대체하되 공용 SFX pitch는 변경하지 않는다.
- 심리스 전투 시작 전 맵 BGM 상태를 저장하고 승리, 도주, 강제 중단 뒤 원래 재생 위치와 볼륨으로 복원한다.
- 중복 BGM 전환은 가장 최근 요청만 유지하며 BGM ducking과 전환 tween을 수명주기에서 정리한다.
- 전체 테스트 순서에서 남은 DOTween이 TestMap에 유입되지 않도록 전투 픽스처 시작 시 전역 tween 상태를 초기화한다.

## 직렬화 자산

- `AudioManager.prefab`: BGM A/B, SFX, UI, Voice, Ambience 소스 참조와 각 Mixer Group 연결.
- `GameAudioMixer.mixer`: UI, Voice, Ambience 그룹 추가. 세 그룹은 현재 설정 UI의 SFX 볼륨을 상속한다.
- `MapSettings`: 선택형 Ambience Clip, Volume, Fade Duration 필드 추가. 기존 맵은 필드가 비어 있어 호환된다.

## 검증

- AudioManager BGM/Voice 상태 테스트: 5/5
- AudioManager 프리팹 라우팅 테스트: 6/6
- TestMap 심리스 전투 테스트: 6/6
- Unity 전체 EditMode: 804/804
- Project Content Validation: 오류 0건, 기존 선택 아트 경고 10건
- `Assets/_Game` Prefab 59개, 하위 오브젝트 740개: Missing Script 0건
- 사용자 수정 중인 `TestMap.unity` 해시 유지

## 후속 범위

- 개별 Voice/환경음 볼륨 슬라이더가 필요해질 때만 Mixer 파라미터와 설정 저장 스키마를 확장한다.
- 맵별 실제 Ambience Clip 배치는 사운드 자산과 지역 연출 방향이 확정된 뒤 진행한다.
