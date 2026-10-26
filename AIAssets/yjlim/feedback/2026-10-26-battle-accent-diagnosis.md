# 전투 중 확대·깜빡임 진단

## 결론

가장 직접적인 원인은 새 공용 강조 연출의 **자동 확대 + 너무 잦고 짧은 배치**다. 현재 코드·자료는 증상을 설명하지만 실제 사용자 화면은 아직 재현하지 않았으므로 별도 카메라/HUD 결함까지 배제한 결과는 아니다.

## 확인한 사실

| 항목 | 현재 구성 |
| --- | --- |
| 캐릭터 컷인 | 15스킬 중8개,0.23~0.32초 |
| 확대 실루엣 | 9개,0.10~0.18초 |
| 화면 점멸 | 11개,0.07~0.12초 |
| 위젤 초상 | 실제64×64그림을192×192로 표시 |
| 원본 캐릭터/HUD | 새 블록은 이들의 Transform/Lens를 바꾸지 않고 별도 Image를 표시 |

`BattleSkillAccentPresentation.Show`는 명시 그림을 우선하고, 없으면 컷인에서 CharacterData.Portrait, 이후 현재 SpriteRenderer.sprite를 사용한다. `PixelPortraitSize`는 두 모드 모두 같은 정수 확대 정책을 적용하므로 작은 전신 프레임도 크게 보인다. `EvaluateEnvelope`가 짧은 출현·퇴장을 만든다. 제브의 현재 프레임마다 원본 rect가 달라 확대 배율이 달라질 수 있다. 제브 CutIn의 정확한 Aseprite subasset 매핑은 meta만으로 확정하지 않았다.

생산 함수 하네스에서64×64→192×192,32×32→224×224를 확인했다. 스킬 자료에서 총28개 강조 블록을 확인했으며, RapidStrikes/AerialCrossSlash 내부에 새 강조 블록의 타격별 재호출은 없다. 소유 토큰/종료는 별도 정리되지만 실제 Canvas/엔진 수명 재생 검증은 하지 않았다.

## 조정 권장안

일반 공격에서는 큰 얼굴·확대 전신을 띄우지 않고, 컷인은 일부 강한 스킬 시작에만 제한한다. 실루엣 자동 확대는 제거하거나 전용 그림을 명시한 특수 연출로 제한한다. 명중 섬광·피해 숫자·이동 잔상·화면 절단을 함께 제거할 필요는 없다.

이번 요청은 원인 질문이므로 **코드·스킬 데이터는 변경하지 않았다.** Unity Play/강제 Refresh/실제 저장 I/O/커밋·푸시도 수행하지 않았다. 브랜치 `gameplayEdit`.

[공용 표시 코드](../../../Assets/_Game/Scripts/UI/Runtime/BattleSkillAccentPresentation.cs) · [연출 블록](../../../Assets/_Game/Scripts/Battle/Data/Action_PresentationAccent.cs) · [실험실 사용법](../../../docs/bunny-slime-battle-lab.md) · [당일 기록](../../2026-10-26-update.md)
