# React + TypeScript + Vite

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the ESLint configuration

If you are developing a production application, we recommend updating the configuration to enable type-aware lint rules:

```js
export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...

      // Remove tseslint.configs.recommended and replace with this
      tseslint.configs.recommendedTypeChecked,
      // Alternatively, use this for stricter rules
      tseslint.configs.strictTypeChecked,
      // Optionally, add this for stylistic rules
      tseslint.configs.stylisticTypeChecked,

      // Other configs...
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])
```

You can also install [eslint-plugin-react-x](https://github.com/Rel1cx/eslint-react/tree/main/packages/plugins/eslint-plugin-react-x) and [eslint-plugin-react-dom](https://github.com/Rel1cx/eslint-react/tree/main/packages/plugins/eslint-plugin-react-dom) for React-specific lint rules:

```js
// eslint.config.js
import reactX from 'eslint-plugin-react-x'
import reactDom from 'eslint-plugin-react-dom'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...
      // Enable lint rules for React
      reactX.configs['recommended-typescript'],
      // Enable lint rules for React DOM
      reactDom.configs.recommended,
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])
```
# 🧱 경신 디지털 레고 (Kyungshin Digital LEGO)

> **사내 업무 자동화 모듈을 레고 블록처럼 생성·저장·조립하는 내부 플랫폼**  
> Kyungshin AI TFT — 2026.03

---

## 📌 목차

1. [프로젝트 개요](#1-프로젝트-개요)
2. [주요 기능](#2-주요-기능)
3. [화면 구성](#3-화면-구성)
4. [사용 프로세스](#4-사용-프로세스)
5. [브릭 개념 정의](#5-브릭-개념-정의)
6. [기술 스택](#6-기술-스택)
7. [프로젝트 구조](#7-프로젝트-구조)
8. [환경 설정](#8-환경-설정)
9. [실행 방법](#9-실행-방법)
10. [디자인 시스템](#10-디자인-시스템)
11. [추진 계획](#11-추진-계획)

---

## 1. 프로젝트 개요

### 배경

사내 업무 자동화 산출물(Excel 리포트, Power BI 대시보드, Python 스크립트, PPTX 보고서 등)이 부서별로 분산 관리되고 있으며, 동일·유사한 작업이 중복 개발되는 비효율이 반복되고 있다.

### 목표

| # | 목표 |
|---|------|
| 1 | 업무 자동화 모듈의 사내 표준화 및 재사용성 확보 |
| 2 | 부서 간 산출물 공유 체계 구축으로 중복 개발 비용 절감 |
| 3 | AI 코파일럿 연동을 통한 모듈 생성 지원 및 조합 추천 |
| 4 | KSC Settlement Refiner 등 기존 자동화 자산의 플랫폼 통합 기반 마련 |

### 기대 효과

| 구분 | 현재 | 도입 후 |
|------|------|---------|
| 모듈 관리 | 부서별 분산, OneDrive/로컬 혼재 | 중앙 창고에서 통합 검색·사용 |
| 재사용성 | 구전(口傳)·개인 공유 의존 | 태그·카테고리 기반 즉시 탐색 |
| 신규 개발 | 처음부터 개발, 레퍼런스 부재 | AI 코파일럿으로 기존 브릭 조합·생성 |
| 품질 관리 | 개인 역량 의존, 검증 프로세스 없음 | 별점·상태(Published/Draft) 기반 관리 |

---

## 2. 주요 기능

### ① 브릭 생성 — AI 코파일럿

자연어로 업무 기능을 설명하면 AI 코파일럿이 브릭 설명·태그·기능 목록 초안을 자동 생성한다.  
사용자는 생성된 초안을 확인·수정 후 `Draft` 또는 `Published` 상태로 등록한다.

### ② 모듈 창고 — 검색 및 탐색

등록된 모든 브릭을 카테고리 필터, 브릭 유형 필터(단일/복합), 텍스트 검색, 별점으로 탐색한다.  
Grid / List 뷰 전환을 지원한다.

### ③ 브릭 조립 — 복합 시스템 구성

단일 브릭을 조립 캔버스에 배치하고 이름·설명을 부여하면 복합 브릭(조립 시스템)이 생성된다.  
비개발 직군도 활용 가능한 드래그 앤드 드롭 인터랙션을 지향한다.

### ④ 나의 브릭 — 개인 자산 관리

내가 생성한 단일 브릭과 조립한 복합 브릭을 한 곳에서 관리한다.  
수정·삭제·상태 변경이 가능하며, 전체/발행됨/초안/조립 시스템별 통계를 제공한다.

### ⑤ 대시보드 — 현황 한눈에 파악

최근 인기 브릭, 가장 많이 조립된 복합 시스템, Quick Stats(전체 브릭 수·발행 수·카테고리 수)를 메인 화면에서 즉시 확인한다.

---

## 3. 화면 구성

| # | 화면명 | 경로 | 주요 역할 |
|---|--------|------|-----------|
| 1 | 대시보드 | `/` | 전체 현황 요약, 인기 브릭·복합 시스템 목록, Quick Stats |
| 2 | 브릭 생성 | `/create` | AI 코파일럿 연동 브릭 생성, 카테고리·태그·상태 설정 |
| 3 | 모듈 창고 | `/storage` | 전체 브릭 검색·필터·탐색, Grid/List 뷰 전환 |
| 4 | 브릭 조립 | `/assemble` | 단일 브릭 조합 → 복합 브릭(조립 시스템) 생성 |
| 5 | 나의 브릭 | `/my-bricks` | 내 브릭 목록 관리, 수정·삭제·발행 상태 변경 |

---

## 4. 사용 프로세스

```
① 생성  →  ② 저장  →  ③ 조합  →  ④ 공유  →  ⑤ 재활용
```

| 단계 | 화면 | 주요 행동 | 결과 |
|------|------|-----------|------|
| ① 생성 | `/create` | AI 코파일럿에 업무 내용 입력 → 초안 확인 → 카테고리·태그·상태 설정 | 단일 브릭 등록 |
| ② 저장 | `/storage` | 등록된 브릭 검색·탐색, 별점 확인 | 사용할 브릭 선정 |
| ③ 조합 | `/assemble` | 조립 캔버스에 브릭 2개 이상 추가 → 이름·설명 입력 → 저장 | 복합 브릭 생성 |
| ④ 공유 | `/my-bricks` | 브릭 관리, Published 상태로 변경 | 전체 구성원 공유 |
| ⑤ 재활용 | 대시보드 + 창고 | 다른 구성원이 공개 브릭을 조립에 재활용 | 지식 자산 누적 |

### 브릭 생성 상세 흐름

1. `/create` 진입 → AI 코파일럿 채팅창에 업무 내용 입력
2. AI가 브릭 이름·설명·예상 기능 목록 초안 자동 생성
3. 우측 패널에서 카테고리 / 브릭 유형 / 태그 / 상태 선택
4. **브릭 생성** 버튼 클릭 → 저장 완료 → 모듈 창고 자동 반영

### 브릭 조립 상세 흐름

1. 좌측 패널: 등록된 단일 브릭 목록 표시
2. 원하는 브릭 클릭 → 중앙 조립 캔버스에 순서대로 추가 (최소 2개)
3. 각 브릭은 색상 보더와 연결선으로 시각화, 순서 확인 가능
4. 우측 패널: 조립 이름·설명 입력 후 **조립 저장** 클릭
5. 복합 브릭 생성 완료 → 나의 브릭 및 대시보드 반영

---

## 5. 브릭 개념 정의

**브릭(Brick)** 은 하나의 업무 자동화 단위 모듈을 지칭하는 플랫폼 내 표준 용어다.  
레고 블록처럼 독립적으로 사용할 수 있으며, 다른 브릭과 조합하여 더 복잡한 업무 시스템을 구성할 수 있다.

### 브릭 유형

| 유형 | 대상 | 예시 |
|------|------|------|
| **단일 브릭** | 단일 기능 모듈 | 해외법인 결산 파서, 부서별 KPI 추출 스크립트 |
| **복합 브릭** | 2개 이상 브릭 조합 | KSC Settlement Refiner (파서 + DB + PPTX 생성) |

### 카테고리 체계

| 카테고리 | 색상 | 주요 대상 업무 |
|----------|------|----------------|
| 재무 (Finance) | 🔵 블루 | 결산, 예산, 법인 실적 통합 |
| 인사 (HR) | 🟢 그린 | 근태, 급여, 인원 현황 |
| 대시보드 | 🟠 오렌지 | 경영진 리포트, KPI 시각화 |
| 구매 (Purchase) | 🟣 퍼플 | 발주, 협력사 관리, 단가 분석 |
| 품질 (Quality) | 🔴 레드 | 불량률, 공정 품질 데이터 처리 |

### 브릭 데이터 스키마 (예시)

```json
{
  "id": "brick-001",
  "name": "해외법인 결산 파서",
  "description": "KCTR/KSCCZ/KSCE/KSCI/KSCP 결산 Excel 자동 파싱 모듈",
  "category": "finance",
  "type": "single",
  "tags": ["Finance", "Excel", "Parser"],
  "status": "published",
  "rating": 4.5,
  "author": "고태호",
  "createdAt": "2026-03-01T09:00:00Z",
  "updatedAt": "2026-03-15T14:30:00Z"
}
```

---

## 6. 기술 스택

| 구분 | 기술 | 비고 |
|------|------|------|
| 프레임워크 | React + TypeScript | TanStack Router 기반 SPA |
| 스타일 | Tailwind CSS v4 + shadcn/ui | OKLCH 색상 토큰 사용 |
| AI 연동 | Google Gemini / Azure OpenAI | 브릭 생성 코파일럿 |
| 아이콘 | Lucide React | Box, Puzzle, Sparkles 등 |
| 애니메이션 | tw-animate-css + Custom CSS | brick-pop, float 효과 |

---

## 7. 프로젝트 구조

```
src/
├── pages/
│   ├── _layout.tsx        # 메인 레이아웃 (사이드바 + 헤더)
│   ├── index.tsx          # 대시보드
│   ├── create.tsx         # 브릭 생성 (AI 코파일럿)
│   ├── storage.tsx        # 모듈 창고
│   ├── assemble.tsx       # 브릭 조립
│   ├── my-bricks.tsx      # 나의 브릭
│   └── not-found.tsx      # 404
├── components/
│   ├── ui/                # shadcn/ui 기본 컴포넌트
│   ├── BrickCard.tsx      # 브릭 카드 공통 컴포넌트
│   └── AssemblyCard.tsx   # 조립 카드 공통 컴포넌트
├── lib/
│   └── utils.ts           # cn() 유틸리티
└── index.css              # 테마 / 디자인 토큰
```

---

## 8. 환경 설정

프로젝트 루트에 `.env` 파일을 생성하고 아래 값을 설정한다.

```env
# AI 코파일럿 — Gemini 또는 Azure OpenAI 중 사용하는 쪽만 설정
VITE_GEMINI_API_KEY=your_gemini_api_key
VITE_AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
VITE_AZURE_OPENAI_API_KEY=your_azure_api_key
VITE_AZURE_OPENAI_DEPLOYMENT=your_deployment_name
```

> ⚠️ `.env` 파일은 절대 Git에 커밋하지 않는다. `.gitignore`에 등록 필수.

---

## 9. 실행 방법

```bash
# 의존성 설치
npm install

# 개발 서버 실행
npm run dev

# 빌드
npm run build

# 빌드 결과 미리보기
npm run preview
```

---

## 10. 디자인 시스템

### 색상 토큰

| 토큰 | 값 (OKLCH) | 용도 |
|------|-----------|------|
| `--primary` | `oklch(0.50 0.18 250)` | 경신 블루 — 주요 액션 |
| `--accent` | `oklch(0.70 0.18 155)` | 경신 그린 — 강조 |
| `--background` | `oklch(0.98 0.005 240)` | 페이지 배경 |
| `--sidebar` | `oklch(0.25 0.05 250)` | 사이드바 배경 (다크) |

### 브릭 색상 클래스

```css
.brick-blue   { background: oklch(0.55 0.20 250); } /* 재무 */
.brick-green  { background: oklch(0.65 0.20 155); } /* 인사 */
.brick-orange { background: oklch(0.70 0.18 45);  } /* 대시보드 */
.brick-purple { background: oklch(0.60 0.22 320); } /* 구매 */
.brick-red    { background: oklch(0.65 0.20 25);  } /* 품질 */
```

### 커스텀 애니메이션

| 클래스 | 효과 |
|--------|------|
| `.animate-float` | 부유 효과 (3초 주기) |
| `.animate-brick-pop` | 브릭 등장 효과 (0.3초) |

### 모션 토큰

```css
--dur-fast:   120ms;
--dur-medium: 200ms;
--dur-slow:   360ms;
```

---

## 11. 추진 계획

| Phase | 기간 (안) | 주요 내용 | 산출물 |
|-------|-----------|-----------|--------|
| Phase 1 | 2026. 4월 | 브릭 데이터 스키마 확정, 백엔드 API 설계, 프론트 기본 레이아웃 구현 | DB 스키마 문서, 레이아웃 프로토타입 |
| Phase 2 | 2026. 5월 | 브릭 생성·창고·나의 브릭 화면 구현, AI 코파일럿 연동 | 단일 브릭 CRUD 완성 |
| Phase 3 | 2026. 6월 | 조립 기능 구현, 복합 브릭 생성·관리, 대시보드 완성 | 복합 브릭 조립 시스템 완성 |
| Phase 4 | 2026. 7월~ | KSC Settlement Refiner 등 기존 자산 브릭 등록, 사내 파일럿 운영 | 파일럿 운영 보고서 |

---

> 경신 디지털 레고는 단순 도구 도입이 아니라, 사내 업무 자동화 자산을 조직 공유 지식으로 전환하는 플랫폼이다.  
> TFT 주도로 파일럿을 진행하며 점진적으로 확산하는 방향을 권고한다.

---

*경신 사내 AI 활용 TFT · 2026.03*

