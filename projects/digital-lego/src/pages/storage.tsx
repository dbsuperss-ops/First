import { useState, useEffect } from "react"
import { Search, Grid3X3, List, Box } from "lucide-react"
import { getBricks, updateBrick } from "@/lib/store"
import type { Brick, BrickCategory, BrickType } from "@/types"
import { BrickCard } from "@/components/BrickCard"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"

const CATEGORIES: (BrickCategory | "전체")[] = ["전체", "재무", "인사", "대시보드", "구매", "품질"]
const TYPES: (BrickType | "전체")[] = ["전체", "단일", "복합"]

export default function Storage() {
  const [bricks, setBricks] = useState<Brick[]>([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState("")
  const [selectedCategory, setSelectedCategory] = useState<BrickCategory | "전체">("전체")
  const [selectedType, setSelectedType] = useState<BrickType | "전체">("전체")
  const [viewMode, setViewMode] = useState<"grid" | "list">("grid")

  useEffect(() => {
    const timer = setTimeout(() => {
      setBricks(getBricks())
      setLoading(false)
    }, 600)
    return () => clearTimeout(timer)
  }, [])

  const filtered = bricks.filter((b) => {
    const matchesSearch =
      !search ||
      b.name.toLowerCase().includes(search.toLowerCase()) ||
      b.description.toLowerCase().includes(search.toLowerCase()) ||
      b.author.toLowerCase().includes(search.toLowerCase())
    const matchesCategory = selectedCategory === "전체" || b.category === selectedCategory
    const matchesType = selectedType === "전체" || b.type === selectedType
    return matchesSearch && matchesCategory && matchesType
  })

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold">모듈 창고</h1>
        <p className="text-muted-foreground text-sm mt-1">모든 브릭을 검색하고 찾아보세요</p>
      </div>

      {/* Controls */}
      <div className="flex flex-col md:flex-row gap-3">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="브릭 이름, 설명, 작성자 검색..."
            className="pl-9"
          />
        </div>
        <div className="flex items-center gap-1 rounded-lg border bg-background p-1">
          <button
            onClick={() => setViewMode("grid")}
            className={cn(
              "p-2 rounded-md transition-all",
              viewMode === "grid" ? "bg-primary text-primary-foreground" : "hover:bg-secondary"
            )}
          >
            <Grid3X3 className="h-4 w-4" />
          </button>
          <button
            onClick={() => setViewMode("list")}
            className={cn(
              "p-2 rounded-md transition-all",
              viewMode === "list" ? "bg-primary text-primary-foreground" : "hover:bg-secondary"
            )}
          >
            <List className="h-4 w-4" />
          </button>
        </div>
      </div>

      {/* Filters */}
      <div className="flex flex-col gap-3">
        {/* Category tabs */}
        <div className="flex flex-wrap gap-2">
          {CATEGORIES.map((cat) => (
            <button
              key={cat}
              onClick={() => setSelectedCategory(cat)}
              className={cn(
                "px-3 py-1.5 rounded-lg text-sm font-medium border transition-all",
                selectedCategory === cat
                  ? "bg-primary text-primary-foreground border-primary"
                  : "bg-background border-border hover:bg-secondary"
              )}
            >
              {cat}
            </button>
          ))}
        </div>
        {/* Type filter */}
        <div className="flex gap-2 items-center">
          <span className="text-sm text-muted-foreground">유형:</span>
          {TYPES.map((t) => (
            <button
              key={t}
              onClick={() => setSelectedType(t)}
              className={cn(
                "px-2.5 py-1 rounded-full text-xs font-medium border transition-all",
                selectedType === t
                  ? "bg-accent text-accent-foreground border-accent"
                  : "bg-background border-border hover:bg-secondary"
              )}
            >
              {t}
            </button>
          ))}
        </div>
      </div>

      {/* Results count */}
      {!loading && (
        <p className="text-sm text-muted-foreground">
          {filtered.length}개의 브릭
          {search && ` - "${search}" 검색 결과`}
        </p>
      )}

      {/* Grid/List */}
      {loading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="rounded-xl border overflow-hidden">
              <Skeleton className="h-2 w-full" />
              <div className="p-4 space-y-3">
                <div className="flex gap-3">
                  <Skeleton className="h-10 w-10 rounded-lg" />
                  <div className="flex-1 space-y-1.5">
                    <Skeleton className="h-4 w-3/4" />
                    <Skeleton className="h-3 w-1/2" />
                  </div>
                </div>
                <Skeleton className="h-3 w-full" />
                <Skeleton className="h-3 w-5/6" />
                <div className="flex gap-1.5">
                  <Skeleton className="h-5 w-12 rounded-full" />
                  <Skeleton className="h-5 w-10 rounded-full" />
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-center">
          <Box className="h-12 w-12 text-muted-foreground/50 mb-3" />
          <h3 className="font-semibold text-lg">브릭을 찾을 수 없습니다</h3>
          <p className="text-muted-foreground text-sm mt-1">검색 조건을 변경하거나 새 브릭을 만들어보세요</p>
        </div>
      ) : viewMode === "grid" ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {filtered.map((brick) => (
            <BrickCard
              key={brick.id}
              brick={brick}
              onRate={(rating) => {
                updateBrick(brick.id, { rating })
                setBricks(getBricks())
              }}
            />
          ))}
        </div>
      ) : (
        <div className="space-y-3">
          {filtered.map((brick) => (
            <div
              key={brick.id}
              className="flex items-center gap-4 p-4 rounded-xl border bg-card hover:shadow-sm transition-all"
            >
              <div className="shrink-0 w-10 h-10 rounded-lg bg-gradient-to-br from-primary to-blue-400 flex items-center justify-center">
                <Box className="h-5 w-5 text-white" />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <h3 className="font-semibold text-sm truncate">{brick.name}</h3>
                  <span className={cn(
                    "inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold shrink-0",
                    brick.status === "Published" ? "bg-green-100 text-green-700" : "bg-yellow-100 text-yellow-700"
                  )}>
                    {brick.status === "Published" ? "발행됨" : "초안"}
                  </span>
                </div>
                <p className="text-xs text-muted-foreground truncate">{brick.description}</p>
              </div>
              <div className="shrink-0 flex items-center gap-2 text-xs text-muted-foreground">
                <span>{brick.author}</span>
                <span>·</span>
                <span>{brick.category}</span>
                <span>·</span>
                <span>{brick.type}</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
