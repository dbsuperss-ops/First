import { Puzzle } from "lucide-react"
import type { Assembly } from "@/types"
import { Badge } from "@/components/ui/badge"
import { cn } from "@/lib/utils"

interface AssemblyCardProps {
  assembly: Assembly
  className?: string
  onClick?: () => void
}

export function AssemblyCard({ assembly, className, onClick }: AssemblyCardProps) {
  return (
    <div
      className={cn(
        "rounded-xl border bg-card text-card-foreground shadow-sm overflow-hidden animate-brick-pop transition-all duration-200 hover:shadow-md hover:-translate-y-0.5",
        onClick && "cursor-pointer",
        className
      )}
      onClick={onClick}
    >
      {/* Header bar */}
      <div className="h-2 w-full bg-gradient-to-r from-violet-500 to-pink-400 lego-pattern" />

      <div className="p-4">
        {/* Icon + Title */}
        <div className="flex items-start gap-3 mb-2">
          <div className="p-2 rounded-lg bg-gradient-to-br from-violet-500 to-pink-400 shrink-0">
            <Puzzle className="h-4 w-4 text-white" />
          </div>
          <div className="flex-1 min-w-0">
            <h3 className="font-semibold text-sm leading-tight line-clamp-1">{assembly.name}</h3>
            <p className="text-xs text-muted-foreground mt-0.5">{assembly.author}</p>
          </div>
        </div>

        {/* Description */}
        <p className="text-xs text-muted-foreground line-clamp-2 mb-3">{assembly.description}</p>

        {/* Bricks count */}
        <div className="flex items-center gap-1.5 mb-3">
          <span className="text-xs text-muted-foreground">브릭 {assembly.bricks.length}개</span>
          <div className="flex gap-1">
            {assembly.bricks.slice(0, 3).map((brick) => (
              <span
                key={brick.id}
                className="inline-flex items-center rounded-full bg-secondary px-2 py-0.5 text-xs"
              >
                {brick.name.length > 8 ? brick.name.slice(0, 8) + "…" : brick.name}
              </span>
            ))}
            {assembly.bricks.length > 3 && (
              <span className="inline-flex items-center rounded-full bg-secondary px-2 py-0.5 text-xs">
                +{assembly.bricks.length - 3}
              </span>
            )}
          </div>
        </div>

        {/* Footer: Badges + Status */}
        <div className="flex items-center justify-between">
          <Badge className="bg-violet-100 text-violet-700 border-0 text-xs">복합 시스템</Badge>
          <span
            className={cn(
              "inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold",
              assembly.status === "Published"
                ? "bg-green-100 text-green-700"
                : "bg-yellow-100 text-yellow-700"
            )}
          >
            {assembly.status === "Published" ? "발행됨" : "초안"}
          </span>
        </div>
      </div>
    </div>
  )
}
