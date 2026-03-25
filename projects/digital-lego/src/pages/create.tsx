import { useState, useRef, useEffect } from "react"
import { useNavigate } from "react-router-dom"
import { Bot, Send, User, Loader2, Plus, X, Sparkles } from "lucide-react"
import type { BrickCategory, BrickType, BrickStatus } from "@/types"
import { addBrick, TAGS } from "@/lib/store"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { cn } from "@/lib/utils"

interface ChatMessage {
  role: "user" | "assistant"
  content: string
}

const CATEGORIES: BrickCategory[] = ["재무", "인사", "대시보드", "구매", "품질"]
const TYPES: BrickType[] = ["단일", "복합"]
const STATUSES: BrickStatus[] = ["Draft", "Published"]

const AI_RESPONSES: Record<string, { name: string; description: string; category: BrickCategory; tags: string[] }> = {
  재무: {
    name: "재무 현황 리포트 자동화",
    description: "월별 재무 데이터를 자동으로 집계하고 시각화합니다. 손익계산서, 대차대조표 등 주요 재무 지표를 한눈에 파악할 수 있습니다.",
    category: "재무",
    tags: ["Finance"],
  },
  인사: {
    name: "인사 관리 통합 브릭",
    description: "직원 정보 관리, 근태 기록, 성과 평가를 통합 관리합니다. HR 팀의 반복 업무를 자동화하여 효율을 높입니다.",
    category: "인사",
    tags: ["HR"],
  },
  구매: {
    name: "구매 요청 자동화 워크플로우",
    description: "구매 요청서 작성부터 승인, 발주까지의 전 과정을 자동화합니다. 예산 초과 알림과 공급업체 비교 기능도 포함됩니다.",
    category: "구매",
    tags: ["Purchase"],
  },
  품질: {
    name: "품질 관리 체크 시스템",
    description: "생산 라인별 품질 기준 관리와 불량 이력 추적이 가능합니다. ISO 기준에 맞춘 검사 체크리스트를 자동 생성합니다.",
    category: "품질",
    tags: ["Quality"],
  },
  대시보드: {
    name: "경영 KPI 대시보드",
    description: "회사 전체 KPI를 실시간으로 모니터링하는 대시보드입니다. 각 부서별 목표 달성률을 시각적으로 확인할 수 있습니다.",
    category: "대시보드",
    tags: ["Dashboard"],
  },
}

function getAiResponse(input: string): typeof AI_RESPONSES[string] {
  for (const key of Object.keys(AI_RESPONSES)) {
    if (input.includes(key)) return AI_RESPONSES[key]
  }
  // Default
  return {
    name: "업무 자동화 브릭",
    description: "반복적인 업무 프로세스를 자동화하는 브릭입니다. 데이터 수집, 처리, 보고서 생성을 자동으로 수행합니다.",
    category: "대시보드",
    tags: ["Dashboard"],
  }
}

export default function Create() {
  const navigate = useNavigate()
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      role: "assistant",
      content: "안녕하세요! 어떤 업무 브릭을 만들고 싶으신가요? 예를 들어 '재무 보고서 자동화 브릭을 만들고 싶어요' 처럼 말씀해 주세요.",
    },
  ])
  const [inputValue, setInputValue] = useState("")
  const [isLoading, setIsLoading] = useState(false)
  const chatEndRef = useRef<HTMLDivElement>(null)

  // Form state
  const [name, setName] = useState("")
  const [description, setDescription] = useState("")
  const [category, setCategory] = useState<BrickCategory>("재무")
  const [type, setType] = useState<BrickType>("단일")
  const [selectedTags, setSelectedTags] = useState<string[]>([])
  const [status, setStatus] = useState<BrickStatus>("Draft")

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: "smooth" })
  }, [messages])

  const handleSendMessage = async () => {
    const trimmed = inputValue.trim()
    if (!trimmed || isLoading) return

    const userMessage: ChatMessage = { role: "user", content: trimmed }
    setMessages((prev) => [...prev, userMessage])
    setInputValue("")
    setIsLoading(true)

    await new Promise((res) => setTimeout(res, 1500))

    const suggestion = getAiResponse(trimmed)
    const aiResponse: ChatMessage = {
      role: "assistant",
      content: `좋은 아이디어네요! "${suggestion.name}" 브릭을 추천드립니다.\n\n${suggestion.description}\n\n오른쪽 폼에 내용을 자동으로 채워드렸습니다. 필요에 따라 수정하세요!`,
    }

    setMessages((prev) => [...prev, aiResponse])
    setName(suggestion.name)
    setDescription(suggestion.description)
    setCategory(suggestion.category)
    setSelectedTags(suggestion.tags)
    setIsLoading(false)
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault()
      void handleSendMessage()
    }
  }

  const toggleTag = (tag: string) => {
    setSelectedTags((prev) =>
      prev.includes(tag) ? prev.filter((t) => t !== tag) : [...prev, tag]
    )
  }

  const handleCreate = () => {
    if (!name.trim()) return
    addBrick({
      name: name.trim(),
      author: "경신 사원",
      description: description.trim(),
      category,
      type,
      tags: selectedTags,
      rating: 3,
      status,
    })
    navigate("/my-bricks")
  }

  return (
    <div className="p-6 h-full">
      <div className="mb-6">
        <h1 className="text-2xl font-bold">새 브릭 만들기</h1>
        <p className="text-muted-foreground text-sm mt-1">AI 어시스턴트와 대화하며 업무 브릭을 생성하세요</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 h-[calc(100vh-180px)]">
        {/* AI Chat */}
        <div className="flex flex-col rounded-xl border bg-card overflow-hidden">
          <div className="flex items-center gap-2 px-4 py-3 border-b bg-muted/30">
            <div className="w-7 h-7 rounded-full bg-primary flex items-center justify-center">
              <Bot className="h-4 w-4 text-primary-foreground" />
            </div>
            <div>
              <p className="text-sm font-semibold">AI 브릭 어시스턴트</p>
              <p className="text-xs text-muted-foreground">업무 브릭 생성을 도와드립니다</p>
            </div>
            <div className="ml-auto">
              <Sparkles className="h-4 w-4 text-yellow-500" />
            </div>
          </div>

          {/* Messages */}
          <div className="flex-1 overflow-auto p-4 space-y-4">
            {messages.map((msg, idx) => (
              <div
                key={idx}
                className={cn("flex gap-3", msg.role === "user" ? "flex-row-reverse" : "flex-row")}
              >
                <div
                  className={cn(
                    "w-7 h-7 rounded-full flex items-center justify-center shrink-0",
                    msg.role === "user"
                      ? "bg-primary text-primary-foreground"
                      : "bg-accent text-accent-foreground"
                  )}
                >
                  {msg.role === "user" ? <User className="h-4 w-4" /> : <Bot className="h-4 w-4" />}
                </div>
                <div
                  className={cn(
                    "max-w-[80%] rounded-xl px-3.5 py-2.5 text-sm whitespace-pre-wrap",
                    msg.role === "user"
                      ? "bg-primary text-primary-foreground"
                      : "bg-muted text-foreground"
                  )}
                >
                  {msg.content}
                </div>
              </div>
            ))}
            {isLoading && (
              <div className="flex gap-3">
                <div className="w-7 h-7 rounded-full bg-accent flex items-center justify-center shrink-0">
                  <Bot className="h-4 w-4 text-accent-foreground" />
                </div>
                <div className="bg-muted rounded-xl px-3.5 py-2.5 flex items-center gap-2">
                  <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
                  <span className="text-sm text-muted-foreground">분석 중...</span>
                </div>
              </div>
            )}
            <div ref={chatEndRef} />
          </div>

          {/* Input */}
          <div className="p-4 border-t">
            <div className="flex gap-2">
              <Input
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="어떤 브릭을 만들고 싶으신가요?"
                disabled={isLoading}
                className="flex-1"
              />
              <Button
                size="icon"
                onClick={() => void handleSendMessage()}
                disabled={!inputValue.trim() || isLoading}
              >
                <Send className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </div>

        {/* Form */}
        <div className="flex flex-col rounded-xl border bg-card overflow-auto">
          <div className="px-4 py-3 border-b bg-muted/30">
            <p className="text-sm font-semibold">브릭 설정</p>
            <p className="text-xs text-muted-foreground">브릭의 상세 정보를 설정하세요</p>
          </div>

          <div className="flex-1 overflow-auto p-4 space-y-4">
            {/* Name */}
            <div>
              <label className="text-sm font-medium mb-1.5 block">브릭 이름 *</label>
              <Input
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="예: 월간 손익계산서 리포트"
              />
            </div>

            {/* Description */}
            <div>
              <label className="text-sm font-medium mb-1.5 block">설명</label>
              <Textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="이 브릭이 어떤 역할을 하는지 설명해주세요"
                rows={3}
              />
            </div>

            {/* Category */}
            <div>
              <label className="text-sm font-medium mb-1.5 block">카테고리</label>
              <div className="flex flex-wrap gap-2">
                {CATEGORIES.map((cat) => (
                  <button
                    key={cat}
                    onClick={() => setCategory(cat)}
                    className={cn(
                      "px-3 py-1.5 rounded-lg text-sm font-medium border transition-all",
                      category === cat
                        ? "bg-primary text-primary-foreground border-primary"
                        : "bg-background border-border hover:bg-secondary"
                    )}
                  >
                    {cat}
                  </button>
                ))}
              </div>
            </div>

            {/* Type */}
            <div>
              <label className="text-sm font-medium mb-1.5 block">유형</label>
              <div className="flex gap-2">
                {TYPES.map((t) => (
                  <button
                    key={t}
                    onClick={() => setType(t)}
                    className={cn(
                      "px-3 py-1.5 rounded-lg text-sm font-medium border transition-all",
                      type === t
                        ? "bg-primary text-primary-foreground border-primary"
                        : "bg-background border-border hover:bg-secondary"
                    )}
                  >
                    {t}
                  </button>
                ))}
              </div>
            </div>

            {/* Tags */}
            <div>
              <label className="text-sm font-medium mb-1.5 block">태그</label>
              <div className="flex flex-wrap gap-2">
                {TAGS.map((tag) => (
                  <button
                    key={tag}
                    onClick={() => toggleTag(tag)}
                    className={cn(
                      "inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium border transition-all",
                      selectedTags.includes(tag)
                        ? "bg-accent text-accent-foreground border-accent"
                        : "bg-background border-border hover:bg-secondary"
                    )}
                  >
                    {selectedTags.includes(tag) ? (
                      <X className="h-3 w-3" />
                    ) : (
                      <Plus className="h-3 w-3" />
                    )}
                    {tag}
                  </button>
                ))}
              </div>
            </div>

            {/* Status */}
            <div>
              <label className="text-sm font-medium mb-1.5 block">상태</label>
              <div className="flex gap-2">
                {STATUSES.map((s) => (
                  <button
                    key={s}
                    onClick={() => setStatus(s)}
                    className={cn(
                      "px-3 py-1.5 rounded-lg text-sm font-medium border transition-all",
                      status === s
                        ? "bg-primary text-primary-foreground border-primary"
                        : "bg-background border-border hover:bg-secondary"
                    )}
                  >
                    {s === "Published" ? "발행" : "초안"}
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className="p-4 border-t">
            <Button
              className="w-full"
              size="lg"
              onClick={handleCreate}
              disabled={!name.trim()}
            >
              <Plus className="h-4 w-4" />
              브릭 생성하기
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}
