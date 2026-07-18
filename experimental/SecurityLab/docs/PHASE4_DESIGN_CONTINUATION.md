# Phase 4 — 설계 연속 + 보완 (패키지 보류)

## 방향 (판단)

1. **Concealed/pack 입문** — 소형 객체를 pack 파일에 모아 개수·크기 추론 완화  
2. **저널 orphan 실제 삭제** — ObjectsReady 후 미커밋 객체 제거  
3. **Parser bounds** — 헤더/메타/객체 크기 상한 fail-closed  
4. **무결성** — unlock/verify 시 content hash 재검증 옵션  
5. **UI** — 읽기 전용 열기  
6. **개선** — generation 메타 커밋 연동, 감사 이벤트 보강  

AV3 ProductionWriter / Setup 패키지: **여전히 보류**

## 본 라운드 결과

| 항목 | 상태 |
|------|------|
| LabPackStore ≤256KiB | ✅ |
| Journal orphan shred | ✅ |
| LabParserGuard | ✅ |
| VerifyAllContentHashes | ✅ |
| UI 읽기 전용 열기 | ✅ |
| 테스트 | **16/16** |
| App 빌드 | ✅ |
| Setup 패키지 | **생성 안 함** |
