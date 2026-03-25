export type BrickCategory = "재무" | "인사" | "대시보드" | "구매" | "품질";
export type BrickType = "단일" | "복합";
export type BrickStatus = "Draft" | "Published";

export interface Brick {
  id: string;
  name: string;
  author: string;
  description: string;
  category: BrickCategory;
  type: BrickType;
  tags: string[];
  rating: number;
  status: BrickStatus;
  createdAt: string;
}

export interface Assembly {
  id: string;
  name: string;
  author: string;
  description: string;
  bricks: Brick[];
  status: BrickStatus;
  createdAt: string;
}
