import { Brick, Assembly } from "@/types";

export const CATEGORY_COLORS: Record<string, string> = {
  재무: "from-blue-500 to-blue-400",
  인사: "from-green-500 to-green-400",
  대시보드: "from-orange-500 to-orange-400",
  구매: "from-purple-500 to-purple-400",
  품질: "from-red-500 to-red-400",
};

export const CATEGORY_DOT_COLORS: Record<string, string> = {
  재무: "bg-blue-500",
  인사: "bg-green-500",
  대시보드: "bg-orange-500",
  구매: "bg-purple-500",
  품질: "bg-red-500",
};

export const CATEGORY_BADGE_COLORS: Record<string, string> = {
  재무: "bg-blue-100 text-blue-700",
  인사: "bg-green-100 text-green-700",
  대시보드: "bg-orange-100 text-orange-700",
  구매: "bg-purple-100 text-purple-700",
  품질: "bg-red-100 text-red-700",
};

export const TAGS = ["Finance", "HR", "Purchase", "Quality", "Dashboard"];

let bricks: Brick[] = [
  {
    id: "1",
    name: "월간 손익계산서 리포트",
    author: "김재무",
    description: "매월 자동으로 손익계산서를 집계하고 시각화하는 브릭입니다. ERP 데이터와 연동됩니다.",
    category: "재무",
    type: "단일",
    tags: ["Finance"],
    rating: 5,
    status: "Published",
    createdAt: "2024-01-15",
  },
  {
    id: "2",
    name: "신규 입사자 온보딩 체크리스트",
    author: "이인사",
    description: "신규 직원의 온보딩 프로세스를 자동화합니다. IT 계정 발급부터 교육 일정 관리까지.",
    category: "인사",
    type: "단일",
    tags: ["HR"],
    rating: 4,
    status: "Published",
    createdAt: "2024-01-20",
  },
  {
    id: "3",
    name: "KPI 대시보드 위젯",
    author: "박대시",
    description: "주요 KPI 지표를 실시간으로 모니터링하는 대시보드 위젯입니다.",
    category: "대시보드",
    type: "복합",
    tags: ["Dashboard"],
    rating: 5,
    status: "Published",
    createdAt: "2024-01-22",
  },
  {
    id: "4",
    name: "구매 발주 승인 워크플로우",
    author: "최구매",
    description: "구매 요청부터 발주까지의 승인 프로세스를 자동화합니다.",
    category: "구매",
    type: "단일",
    tags: ["Purchase"],
    rating: 4,
    status: "Draft",
    createdAt: "2024-01-25",
  },
  {
    id: "5",
    name: "품질 검사 체크시트",
    author: "강품질",
    description: "생산 라인별 품질 검사 항목을 관리하고 불량률을 추적합니다.",
    category: "품질",
    type: "단일",
    tags: ["Quality"],
    rating: 3,
    status: "Published",
    createdAt: "2024-01-28",
  },
  {
    id: "6",
    name: "급여 명세서 자동 생성",
    author: "김재무",
    description: "매월 급여 데이터를 기반으로 개인별 명세서를 자동 생성하고 발송합니다.",
    category: "재무",
    type: "복합",
    tags: ["Finance", "HR"],
    rating: 5,
    status: "Published",
    createdAt: "2024-02-01",
  },
];

let assemblies: Assembly[] = [
  {
    id: "a1",
    name: "월간 재무 보고 시스템",
    author: "김재무",
    description: "손익계산서와 급여 명세서를 통합한 완전한 월간 재무 보고 시스템",
    bricks: [bricks[0], bricks[5]],
    status: "Published",
    createdAt: "2024-02-05",
  },
  {
    id: "a2",
    name: "HR 통합 관리 시스템",
    author: "이인사",
    description: "신규 입사자 온보딩과 급여 처리를 통합 관리",
    bricks: [bricks[1], bricks[5]],
    status: "Published",
    createdAt: "2024-02-08",
  },
];

export function getBricks(): Brick[] {
  return [...bricks];
}

export function getAssemblies(): Assembly[] {
  return [...assemblies];
}

export function addBrick(brick: Omit<Brick, "id" | "createdAt">): Brick {
  const newBrick: Brick = {
    ...brick,
    id: String(Date.now()),
    createdAt: new Date().toISOString().split("T")[0],
  };
  bricks = [...bricks, newBrick];
  return newBrick;
}

export function updateBrick(id: string, updates: Partial<Brick>): void {
  bricks = bricks.map((b) => (b.id === id ? { ...b, ...updates } : b));
}

export function deleteBrick(id: string): void {
  bricks = bricks.filter((b) => b.id !== id);
}

export function addAssembly(assembly: Omit<Assembly, "id" | "createdAt">): Assembly {
  const newAssembly: Assembly = {
    ...assembly,
    id: String(Date.now()),
    createdAt: new Date().toISOString().split("T")[0],
  };
  assemblies = [...assemblies, newAssembly];
  return newAssembly;
}
