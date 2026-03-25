import { useState } from "react"
import { Box, Star } from "lucide-react"
import type { Brick } from "@/types"
import { CATEGORY_COLORS, CATEGORY_BADGE_COLORS } from "@/lib/store"
import { Badge } from "@/components/ui/badge"
import { cn } from "@/lib/utils"

interface BrickCardProps {
  brick: Brick
  className?: string
  onClick?: () => void
  onRate?: (rating: number) => void
}

export function BrickCard({ brick, className, onClick, onRate }: BrickCardProps) {
  const gradientClass = CATEGORY_COLORS[brick.category] ?? "from-gray-500 to-gray-400"
  const badgeClass = CATEGORY_BADGE_COLORS[brick.category] ?? "bg-gray-100 text-gray-700"
  const [hoverRating, setHoverRating] = useState(0)

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
      <div className={cn("h-2 w-full bg-gradient-to-r lego-pattern", gradientClass)} />

      <div className="p-4">
        {/* Icon + Title */}
        <div className="flex items-start gap-3 mb-2">
          <div className={cn("p-2 rounded-lg bg-gradient-to-br shrink-0", gradientClass)}>
            <Box className="h-4 w-4 text-white" />
          </div>
          <div className="flex-1 min-w-0">
            <h3 className="font-semibold text-sm leading-tight line-clamp-1">{brick.name}</h3>
            <p className="text-xs text-muted-foreground mt-0.5">{brick.author}</p>
          </div>
        </div>

        {/* Description */}
        <p className="text-xs text-muted-foreground line-clamp-2 mb-3">{brick.description}</p>

        {/* Badges */}
        <div className="flex flex-wrap gap-1.5 mb-3">
          <span className={cn("inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold", badgeClass)}>
            {brick.category}
          </span>
          <Badge variant="secondary" className="text-xs">
            {brick.type}
          </Badge>
          {brick.tags.map((tag) => (
            <Badge key={tag} variant="outline" className="text-xs">
              {tag}
            </Badge>
          ))}
        </div>

        {/* Footer: Rating + Status */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-0.5">
            {Array.from({ length: 5 }).map((_, i) => (
              <Star
                key={i}
                className={cn(
                  "h-3 w-3 transition-transform",
                  i < (hoverRating || brick.rating) ? "fill-yellow-400 text-yellow-400" : "text-muted-foreground",
                  onRate && "cursor-pointer hover:scale-125"
                )}
                onClick={onRate ? (e) => { e.stopPropagation(); onRate(i + 1) } : undefined}
                onMouseEnter={onRate ? () => setHoverRating(i + 1) : undefined}
                onMouseLeave={onRate ? () => setHoverRating(0) : undefined}
              />
            ))}
          </div>
          <span
            className={cn(
              "inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold",
              brick.status === "Published"
                ? "bg-green-100 text-green-700"
                : "bg-yellow-100 text-yellow-700"
            )}
          >
            {brick.status === "Published" ? "발행됨" : "초안"}
          </span>
        </div>
      </div>
    </div>
  )
}
