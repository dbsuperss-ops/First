import { useState } from "react"
import { Plus, X, Layers, Box, ArrowRight, Save, CheckCircle2 } from "lucide-react"
import { getBricks, addAssembly } from "@/lib/store"
import type { Brick, BrickStatus } from "@/types"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { CATEGORY_COLORS } from "@/lib/store"
import { cn } from "@/lib/utils"

export default function Assemble() {
  const allBricks = getBricks().filter((b) => b.status === "Published")
  const [selectedBricks, setSelectedBricks] = useState<Brick[]>([])
  const [assemblyName, setAssemblyName] = useState("")
  const [assemblyDesc, setAssemblyDesc] = useState("")
  const [assemblyStatus, setAssemblyStatus] = useState<BrickStatus>("Draft")
  const [saved, setSaved] = useState(false)

  const addToAssembly = (brick: Brick) => {
    if (!selectedBricks.find((b) => b.id === brick.id)) {
      setSelectedBricks((prev) => [...prev, brick])
    }
  }

  const removeFromAssembly = (brickId: string) => {
    setSelectedBricks((prev) => prev.filter((b) => b.id !== brickId))
  }

  const handleSave = () => {
    if (!assemblyName.trim() || selectedBricks.length < 2) return
    addAssembly({
      name: assemblyName.trim(),
      author: "경신 사원",
      description: assemblyDesc.trim(),
      bricks: selectedBricks,
      status: assemblyStatus,
    })
    setSaved(true)
    setTimeout(() => {
      setSaved(false)
      setSelectedBricks([])
      setAssemblyName("")
      setAssemblyDesc("")
    }, 2000)
  }

  return (
    <div className="p-6 h-full">
      <div className="mb-6">
        <h1 className="text-2xl font-bold">브릭 조립</h1>
        <p className="text-muted-foreground text-sm mt-1">여러 브릭을 조합해 복합 시스템을 만드세요</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 h-[calc(100vh-180px)]">
        {/* Left: Available bricks */}
        <div className="flex flex-col rounded-xl border bg-card overflow-hidden">
          <div className="px-4 py-3 border-b bg-muted/30">
            <p className="text-sm font-semibold flex items-center gap-2">
              <Box className="h-4 w-4" />
              사용 가능한 브릭 ({allBricks.length})
            </p>
            <p className="text-xs text-muted-foreground">클릭하여 추가</p>
          </div>
          <div className="flex-1 overflow-auto p-3 space-y-2">
            {allBricks.map((brick) => {
              const isSelected = !!selectedBricks.find((b) => b.id === brick.id)
              const gradientClass = CATEGORY_COLORS[brick.category] ?? "from-gray-500 to-gray-400"
              return (
                <button
                  key={brick.id}
                  onClick={() => addToAssembly(brick)}
                  disabled={isSelected}
                  className={cn(
                    "w-full text-left rounded-lg border p-3 transition-all",
                    isSelected
                      ? "opacity-40 cursor-not-allowed bg-muted"
                      : "hover:bg-secondary hover:border-primary/30 hover:shadow-sm"
                  )}
                >
                  <div className="flex items-center gap-2">
                    <div className={cn("w-2 h-2 rounded-full bg-gradient-to-br shrink-0", gradientClass)} />
                    <span className="text-sm font-medium truncate">{brick.name}</span>
                    {!isSelected && <Plus className="h-3.5 w-3.5 ml-auto shrink-0 text-muted-foreground" />}
                    {isSelected && <CheckCircle2 className="h-3.5 w-3.5 ml-auto shrink-0 text-green-500" />}
                  </div>
                  <p className="text-xs text-muted-foreground mt-1 ml-4 line-clamp-1">{brick.description}</p>
                  <div className="flex items-center gap-1.5 mt-1.5 ml-4">
                    <span className="text-xs text-muted-foreground">{brick.category}</span>
                    <span className="text-xs text-muted-foreground">·</span>
                    <span className="text-xs text-muted-foreground">{brick.type}</span>
                  </div>
                </button>
              )
            })}
          </div>
        </div>

        {/* Center: Assembly canvas */}
        <div className="flex flex-col rounded-xl border bg-card overflow-hidden">
          <div className="px-4 py-3 border-b bg-muted/30">
            <p className="text-sm font-semibold flex items-center gap-2">
              <Layers className="h-4 w-4 text-purple-500" />
              조립 캔버스
            </p>
            <p className="text-xs text-muted-foreground">
              {selectedBricks.length}개 선택됨 {selectedBricks.length < 2 && "(최소 2개 필요)"}
            </p>
          </div>

          <div className="flex-1 overflow-auto p-4">
            {selectedBricks.length === 0 ? (
              <div className="flex flex-col items-center justify-center h-full text-center text-muted-foreground">
                <div className="w-16 h-16 rounded-2xl border-2 border-dashed border-border flex items-center justify-center mb-3">
                  <Layers className="h-6 w-6 opacity-30" />
                </div>
                <p className="text-sm font-medium">왼쪽에서 브릭을 선택하세요</p>
                <p className="text-xs mt-1 opacity-70">브릭을 조립해 복합 시스템을 만드세요</p>
              </div>
            ) : (
              <div className="space-y-2">
                {selectedBricks.map((brick, idx) => {
                  const gradientClass = CATEGORY_COLORS[brick.category] ?? "from-gray-500 to-gray-400"
                  return (
                    <div key={brick.id}>
                      <div className="flex items-center gap-2 p-3 rounded-lg border bg-background animate-brick-pop">
                        <div className={cn("w-8 h-8 rounded-lg bg-gradient-to-br flex items-center justify-center shrink-0", gradientClass)}>
                          <Box className="h-4 w-4 text-white" />
                        </div>
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium truncate">{brick.name}</p>
                          <p className="text-xs text-muted-foreground">{brick.category} · {brick.type}</p>
                        </div>
                        <button
                          onClick={() => removeFromAssembly(brick.id)}
                          className="p-1 rounded-md hover:bg-destructive/10 hover:text-destructive transition-colors"
                        >
                          <X className="h-3.5 w-3.5" />
                        </button>
                      </div>
                      {idx < selectedBricks.length - 1 && (
                        <div className="flex items-center justify-center py-1">
                          <ArrowRight className="h-4 w-4 text-muted-foreground rotate-90" />
                        </div>
                      )}
                    </div>
                  )
                })}

                {/* Visual connection indicator */}
                {selectedBricks.length >= 2 && (
                  <div className="mt-4 p-3 rounded-lg bg-violet-50 border border-violet-200">
                    <div className="flex items-center gap-2 text-violet-700">
                      <Layers className="h-4 w-4" />
                      <span className="text-xs font-medium">복합 시스템 구성 완료</span>
                    </div>
                    <p className="text-xs text-violet-600 mt-1">
                      {selectedBricks.length}개의 브릭이 연결되었습니다
                    </p>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Right: Assembly info */}
        <div className="flex flex-col rounded-xl border bg-card overflow-hidden">
          <div className="px-4 py-3 border-b bg-muted/30">
            <p className="text-sm font-semibold">조립체 정보</p>
            <p className="text-xs text-muted-foreground">복합 브릭 시스템 정보를 입력하세요</p>
          </div>

          <div className="flex-1 overflow-auto p-4 space-y-4">
            <div>
              <label className="text-sm font-medium mb-1.5 block">시스템 이름 *</label>
              <Input
                value={assemblyName}
                onChange={(e) => setAssemblyName(e.target.value)}
                placeholder="예: 월간 재무 보고 시스템"
              />
            </div>

            <div>
              <label className="text-sm font-medium mb-1.5 block">설명</label>
              <Textarea
                value={assemblyDesc}
                onChange={(e) => setAssemblyDesc(e.target.value)}
                placeholder="이 복합 시스템의 목적과 기능을 설명하세요"
                rows={3}
              />
            </div>

            <div>
              <label className="text-sm font-medium mb-1.5 block">상태</label>
              <div className="flex gap-2">
                {(["Draft", "Published"] as BrickStatus[]).map((s) => (
                  <button
                    key={s}
                    onClick={() => setAssemblyStatus(s)}
                    className={cn(
                      "px-3 py-1.5 rounded-lg text-sm font-medium border transition-all",
                      assemblyStatus === s
                        ? "bg-primary text-primary-foreground border-primary"
                        : "bg-background border-border hover:bg-secondary"
                    )}
                  >
                    {s === "Published" ? "발행" : "초안"}
                  </button>
                ))}
              </div>
            </div>

            {/* Summary */}
            {selectedBricks.length > 0 && (
              <div className="rounded-lg border bg-muted/30 p-3">
                <p className="text-xs font-medium text-muted-foreground mb-2">포함된 브릭</p>
                <div className="space-y-1">
                  {selectedBricks.map((b) => (
                    <div key={b.id} className="flex items-center gap-2 text-xs">
                      <div className="w-1.5 h-1.5 rounded-full bg-primary" />
                      <span className="truncate">{b.name}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>

          <div className="p-4 border-t">
            {saved ? (
              <div className="flex items-center justify-center gap-2 py-2.5 text-green-600 font-medium text-sm">
                <CheckCircle2 className="h-4 w-4" />
                저장 완료!
              </div>
            ) : (
              <Button
                className="w-full"
                size="lg"
                onClick={handleSave}
                disabled={!assemblyName.trim() || selectedBricks.length < 2}
              >
                <Save className="h-4 w-4" />
                조립체 저장
              </Button>
            )}
            {selectedBricks.length < 2 && selectedBricks.length > 0 && (
              <p className="text-xs text-muted-foreground text-center mt-2">최소 2개의 브릭이 필요합니다</p>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
