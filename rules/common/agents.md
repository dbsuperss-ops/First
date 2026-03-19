# 에이전트 오케스트레이션 (Agent Orchestration)

## 사용 가능한 에이전트

`~/.claude/agents/`에 위치함:

| 에이전트 | 목적 | 사용 시점 |
|-------|---------|-------------|
| planner | 구현 계획 수립 | 복잡한 기능, 리팩토링 |
| architect | 시스템 설계 | 아키텍처 결정 |
| tdd-guide | 테스트 주도 개발 | 새로운 기능, 버그 수정 |
| code-reviewer | 코드 리뷰 | 코드 작성 후 |
| security-reviewer | 보안 분석 | 커밋 전 |
| build-error-resolver | 빌드 에러 해결 | 빌드 실패 시 |
| e2e-runner | E2E 테스트 | 핵심 사용자 흐름 |
| refactor-cleaner | 데드 코드 정리 | 코드 유지보수 |
| doc-updater | 문서 업데이트 | 문서 갱신 |

## 즉각적인 에이전트 활용

사용자 프롬프트 없이도 다음 상황에서 에이전트를 활용하십시오:
1. 복잡한 기능 요청 - **planner** 에이전트 사용
2. 코드 작성/수정 직후 - **code-reviewer** 에이전트 사용
3. 버그 수정 또는 신규 기능 - **tdd-guide** 에이전트 사용
4. 아키텍처 결정 - **architect** 에이전트 사용

## 병렬 작업 실행

독립적인 작업에는 항상 병렬 실행을 사용하십시오:

```markdown
# 좋음: 병렬 실행
Launch 3 agents in parallel:
1. 에이전트 1: 인증 모듈 보안 분석
2. 에이전트 2: 캐시 시스템 성능 리뷰
3. 에이전트 3: 유틸리티 타입 체크

# 나쁨: 불필요한 순차 실행
First 에이전트 1, then 에이전트 2, then 에이전트 3
```

## 다각도 분석

복잡한 문제의 경우, 역할을 나눈 서브 에이전트들을 활용하십시오:
- 사실 관계 리뷰어 (Factual reviewer)
- 시니어 엔지니어 (Senior engineer)
- 보안 전문가 (Security expert)
- 일관성 리뷰어 (Consistency reviewer)
- 중복 체크 전문가 (Redundancy checker)
