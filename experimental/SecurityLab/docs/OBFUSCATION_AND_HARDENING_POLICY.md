# 난독화 · 하드닝 정책 (SecurityLab / 향후 제품)

**원칙:** 암호학·접근 통제가 1차 방어. 난독화는 보조이며 보안을 대체하지 않음.

## 허용 (합법 · 권장)

| 항목 | 설명 |
|------|------|
| Release 최적화 | `dotnet publish -c Release`, ReadyToRun 선택 |
| PDB 분리 | 공개 빌드에 심볼 미포함, 내부 심볼 서버만 보관 |
| IL trimming / AOT (해당 시) | 공격 표면 축소, 민감 문자열 노출 감소 |
| Authenticode 서명 | 바이너리 위변조·배포 경로 신뢰 |
| 패키지 해시 검증 | 업데이트 채널 SHA-256 + 서명 |
| 민감 문자열 최소화 | 하드코딩 시크릿 금지, 로그에 비밀 금지 |
| 설정·헤더 MAC | vault header hash, integrity manifest, audit chain |
| 상수시간 비교 | 복구코드·해시·rule pack 서명 |

## 조건부 허용

| 항목 | 조건 |
|------|------|
| 상용 .NET 보호기 | 벤더 신뢰 가능 + AV 오탐 관리 + 소스맵/키 escrow 내부 보관 |
| 문자열 암호화 | **키 하드코딩 금지**; 보호는 암호 경계 밖 보조 수단으로만 |

## 금지

- AV/EDR 우회, 프로세스 은폐, 루트킷, 무단 권한 상승
- “디버거 감지 시 데이터 자동 파괴”
- 난독화로 취약한 암호/키 관리를 가리기
- 서명 없는 원격 rule pack 적용
- 랜섬웨어형 대량 암호화·협박 흐름

## SecurityLab 구현 매핑

- `LabHardeningProbe` — 디버거 **경고만**
- `LabIntegrityManifest` — 파일 해시 스냅샷
- `LabAuditChain` — 감사 로그 해시 체인
- `LabRulePack` — HMAC 서명 없는 정책 거부
- `LabCryptoCompare` — 상수시간 비교
- `ProductFeatureFlags` / `LabProductGate` — 제품 기본 OFF

## 제품 병합 시 체크

1. 이 문서 준수 확인  
2. `SECURITY_AUDIT_CHECKLIST.md` 통과  
3. 플래그 기본 OFF  
4. 서명·업데이트 파이프라인 별도 승인  
