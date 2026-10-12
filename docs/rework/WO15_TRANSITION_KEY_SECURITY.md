# WO-15 전환 키 보안

작업 브랜치: `codex/pccare-vault-freshness-recovery-receipts`.
기준 HEAD: `b99d29f1591cb46ce56b299665648e486072d4b8`. 기준 트리: `56f048f3465316147a6bc52b1acb732b8c6825d1`.
검증 소스: `ab2c19f3027a5450a55042915af69220f7ec5ccc`. 소스 트리: `37233729e49b78d1b1dc4bbbe3d5416db72a15e0`.
환경: Windows 11 build 26200, .NET SDK 10.0.401, 논리 CPU 24개.
입력: 합성 LabFast fixture. 실제 사용자 금고와 설치본은 시험에 사용하지 않았다.
판정: **PARTIAL / Phase 1 NO-GO**.
명령과 입력·출력·종료 코드는 `evidence/wo15/`의 항목별 기록에서 확인한다.

## 확인된 재사용
LabV5Transition은 v5 원본과 별도 v6 후보를 보존한다.
v6 후보는 기존 VMK·DEK·VaultId와 암호문 pack을 재사용한다.
복구 코드 재발급은 기존 VMK 유출의 영향을 제거하지 않는다.
구형 VMK 유출은 같은 키를 사용하는 v6 후보에도 영향을 준다.
두 컨테이너는 파일 시스템상 독립 사본이다. 암호학적 독립 컨테이너는 아니다.
전환 probe는 원본 전체 해시와 후보 파일 콘텐츠를 확인했다.
해당 결과를 새 키 계층 검증 PASS로 표시하지 않았다.

## 다음 구현 조건
1. Create a separate fixture with a new VaultId and independent VMK.
2. Generate new content keys and nonces through the existing cryptographic APIs.
3. Reencrypt every authenticated entry without changing the original container.
4. Compare original and exported bytes for every entry.
5. Interrupt every write and verify original recovery in another process.

재암호화 전환은 NOT IMPLEMENTED다. 생산용 자동 전환은 차단 상태다.
LabFast 제한과 실제 기존 바이너리 호환성 검증은 남아 있다.
