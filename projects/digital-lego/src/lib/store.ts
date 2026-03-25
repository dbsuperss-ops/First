import type { Brick, Assembly } from "@/types";

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

let bricks: Brick[] = [];
let assemblies: Assembly[] = [];

export async function initStore(): Promise<void> {
  if (window.electronAPI) {
    const b = await window.electronAPI.readStore('bricks');
    const a = await window.electronAPI.readStore('assemblies');
    bricks = Array.isArray(b) ? (b as Brick[]) : [];
    assemblies = Array.isArray(a) ? (a as Assembly[]) : [];
  }
}

function persist(): void {
  void window.electronAPI?.writeStore('bricks', bricks);
  void window.electronAPI?.writeStore('assemblies', assemblies);
}

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
  persist();
  return newBrick;
}

export function updateBrick(id: string, updates: Partial<Brick>): void {
  bricks = bricks.map((b) => (b.id === id ? { ...b, ...updates } : b));
  persist();
}

export function deleteBrick(id: string): void {
  bricks = bricks.filter((b) => b.id !== id);
  persist();
}

export function addAssembly(assembly: Omit<Assembly, "id" | "createdAt">): Assembly {
  const newAssembly: Assembly = {
    ...assembly,
    id: String(Date.now()),
    createdAt: new Date().toISOString().split("T")[0],
  };
  assemblies = [...assemblies, newAssembly];
  persist();
  return newAssembly;
}

export function updateAssembly(id: string, updates: Partial<Assembly>): void {
  assemblies = assemblies.map((a) => (a.id === id ? { ...a, ...updates } : a));
  persist();
}

export function deleteAssembly(id: string): void {
  assemblies = assemblies.filter((a) => a.id !== id);
  persist();
}
