# Shared 줄바꿈에서 배우는 알고리즘

[학습 홈](README.md) · [전체 코드 색인](20-code-atlas.md) · [이 영역의 모든 파일](code/shared.md)

Shared는 파일이 적지만 **작은 순수 함수에서 알고리즘·테스트 경계를 익히기 좋은 출발점**입니다. 현재 중심 파일은 SmartTextWrapper와 그 테스트입니다.

## 해결하는 문제

대사 박스는 문자 수가 아니라 실제 글자 폭으로 줄을 나눠야 합니다. 한국어·영문·공백은 폭이 다르며, 긴 단어 하나가 박스보다 넓을 수도 있습니다.

`Wrap(TMP_Text, ...)`는 TextMeshPro의 GetPreferredValues를 측정 함수로 전달합니다. 다른 `Wrap(string, width, Func<string,float>, ...)` 오버로드는 TMP 없이도 동작합니다. 테스트에서는 “글자 하나의 폭은 1” 같은 가짜 측정 함수를 쓸 수 있습니다.

## 알고리즘 세 부분

1. 기존 줄바꿈을 정규화하고 문단을 나눕니다.
2. 단어를 앞에서부터 현재 줄에 넣습니다. 넘으면 새 줄로 보냅니다. 이는 **탐욕적 배치**입니다.
3. 단어 자체가 너무 길면 들어갈 최대 길이를 **이진 탐색**합니다.

```csharp
// TakeLargestFittingChunk의 핵심
int mid = (low + high) / 2;
if (Measure(candidate, measureWidth, widthCache) <= maxWidth)
{
    best = mid;
    low = mid + 1;
}
else
{
    high = mid - 1;
}
```

길이를 늘릴수록 측정 폭이 감소하지 않는다는 전제가 필요합니다. 문자열의 중간을 임의로 끊어도 되는지도 별도 조건입니다. 복합 이모지·서로게이트 페어·리치텍스트 태그까지 완전한 유니코드 줄바꿈 엔진이라고 설명하지 마세요.

## 캐시와 비용

동일 문자열의 폭은 Dictionary에 저장해 다시 측정하지 않습니다. 최종 출력은 StringBuilder로 모읍니다. 하지만 중간 후보 문자열·Substring과 측정 함수에는 비용이 있습니다.

“for문 하나라 전체 O(N)”이라고 단정할 수 없습니다. 길이 M인 단어의 한 조각 길이를 찾는 데 O(log M)번의 폭 조회를 하며, 각 폭 측정과 문자열 생성 비용은 별도입니다. 캐시의 공간도 후보 문자열 수·길이에 영향을 받습니다.

## 현재 코드에서 관찰할 점

Tokenize의 collapseWhitespace 분기는 현재 두 경로가 사실상 같은 Split 정책을 사용합니다. 옵션 이름만 보고 원래 공백 폭을 보존한다고 가정하지 않는 연습이 됩니다. 이 문서 작업에서는 코드를 바꾸지 않았습니다.

## 공부 과제

- [ ] “AAAA BB C”를 폭 6, 문자 폭 1로 배치해 본다.
- [ ] 20자 단어가 폭 5에서 어떻게 나뉘는지 low/high/best 표를 적는다.
- [ ] measureWidth를 외부에서 받는 이유를 테스트 가능성 관점에서 설명한다.
- [ ] 이미 있는 줄바꿈, 공백만 있는 입력, 폭 0을 테스트에서 찾는다.

읽기: `Shared/Runtime/Text/SmartTextWrapper.cs`, `Shared/Tests/Editor/SmartTextWrapperTests.cs`.
