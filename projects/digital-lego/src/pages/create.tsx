import { useState, useRef, useEffect } from "react"
import { useNavigate } from "react-router-dom"
import { Bot, Send, User, Loader2, Plus, X, Sparkles, Key, CheckCircle2, Globe, MessageSquare } from "lucide-react"
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

type Provider = "claude" | "gemini" | "chatgpt" | "copilot" | "mimo"
type ChatMode = "api" | "web"

const CATEGORIES: BrickCategory[] = ["재무", "인사", "대시보드", "구매", "품질"]
const TYPES: BrickType[] = ["단일", "복합"]
const STATUSES: BrickStatus[] = ["Draft", "Published"]

const PROVIDERS: { id: Provider; label: string }[] = [
  { id: "claude",  label: "Claude"  },
  { id: "gemini",  label: "Gemini"  },
  { id: "chatgpt", label: "ChatGPT" },
  { id: "copilot", label: "Copilot" },
  { id: "mimo",    label: "Mimo"    },
]

const WEB_URLS: Record<Provider, string> = {
  claude:  "https://claude.ai",
  gemini:  "https://gemini.google.com",
  chatgpt: "https://chatgpt.com",
  copilot: "https://copilot.microsoft.com",
  mimo:    "https://mimo.ai",
}

const STORAGE_KEYS: Record<Provider, string> = {
  claude:  "ai_key_claude",
  gemini:  "ai_key_gemini",
  chatgpt: "ai_key_chatgpt",
  copilot: "ai_key_copilot",
  mimo:    "ai_key_mimo",
}

const AI_RESPONSES: Record<string, { name: string; description: string; category: BrickCategory; tags: string[] }> = {
  재무: {
    name: "재무 현황 리포트 자동화",
    description: "월별 재무 데이터를 자동으로 집계하고 시각화합니다.",
    category: "재무",
    tags: ["Finance"],
  },
  인사: {
    name: "인사 관리 통합 브릭",
    description: "직원 정보 관리, 근태 기록, 성과 평가를 통합 관리합니다.",
    category: "인사",
    tags: ["HR"],
  },
  구매: {
    name: "구매 요청 자동화 워크플로우",
    description: "구매 요청서 작성부터 승인, 발주까지의 전 과정을 자동화합니다.",
    category: "구매",
    tags: ["Purchase"],
  },
  품질: {
    name: "품질 관리 체크 시스템",
    description: "생산 라인별 품질 기준 관리와 불량 이력 추적이 가능합니다.",
    category: "품질",
    tags: ["Quality"],
  },
  대시보드: {
    name: "경영 KPI 대시보드",
    description: "회사 전체 KPI를 실시간으로 모니터링하는 대시보드입니다.",
    category: "대시보드",
    tags: ["Dashboard"],
  },
}

function getKeywordSuggestion(input: string): typeof AI_RESPONSES[string] {
  for (const key of Object.keys(AI_RESPONSES)) {
    if (input.includes(key)) return AI_RESPONSES[key]
  }
  return {
    name: "업무 자동화 브릭",
    description: "반복적인 업무 프로세스를 자동화하는 브릭입니다.",
    category: "대시보드",
    tags: ["Dashboard"],
  }
}

function loadKey(provider: Provider): string {
  return localStorage.getItem(STORAGE_KEYS[provider]) ?? ""
}

function saveKey(provider: Provider, key: string): void {
  if (key) {
    localStorage.setItem(STORAGE_KEYS[provider], key)
  } else {
    localStorage.removeItem(STORAGE_KEYS[provider])
  }
}

export default function Create() {
  const navigate = useNavigate()

  // Provider / mode state
  const [provider, setProvider] = useState<Provider>(
    () => (localStorage.getItem("ai_provider") as Provider | null) ?? "claude"
  )
  const [chatMode, setChatMode] = useState<ChatMode>("api")

  // API key state per provider
  const [apiKey, setApiKey] = useState(() => loadKey(provider))
  const [apiKeyInput, setApiKeyInput] = useState("")
  const [showKeyInput, setShowKeyInput] = useState(false)

  // Mimo endpoint
  const [mimoEndpoint, setMimoEndpoint] = useState(
    () => localStorage.getItem("ai_endpoint_mimo") ?? ""
  )
  const [mimoEndpointInput, setMimoEndpointInput] = useState("")

  // Chat state
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

  // When provider changes, reload the saved key
  const handleProviderChange = (p: Provider) => {
    setProvider(p)
    localStorage.setItem("ai_provider", p)
    setApiKey(loadKey(p))
    setApiKeyInput("")
    setShowKeyInput(false)
  }

  const saveApiKey = () => {
    const trimmed = apiKeyInput.trim()
    saveKey(provider, trimmed)
    setApiKey(trimmed)
    setApiKeyInput("")
    setShowKeyInput(false)
    if (provider === "mimo") {
      const ep = mimoEndpointInput.trim()
      localStorage.setItem("ai_endpoint_mimo", ep)
      setMimoEndpoint(ep)
      setMimoEndpointInput("")
    }
  }

  const clearApiKey = () => {
    saveKey(provider, "")
    setApiKey("")
    if (provider === "mimo") {
      localStorage.removeItem("ai_endpoint_mimo")
      setMimoEndpoint("")
    }
  }

  const handleSendMessage = async () => {
    const trimmed = inputValue.trim()
    if (!trimmed || isLoading) return

    const userMessage: ChatMessage = { role: "user", content: trimmed }
    setMessages((prev) => [...prev, userMessage])
    setInputValue("")
    setIsLoading(true)

    try {
      if (window.electronAPI && apiKey) {
        const raw = await window.electronAPI.callAI({
          provider,
          apiKey,
          messages: [{ role: "user", content: trimmed }],
          endpoint: provider === "mimo" ? mimoEndpoint || undefined : undefined,
        })

        let suggestion: { name: string; description: string; category: BrickCategory; tags: string[]; message?: string }
        try {
          const jsonStr = raw.replace(/```json\n?/g, "").replace(/```\n?/g, "").trim()
          suggestion = JSON.parse(jsonStr)
        } catch {
          setMessages((prev) => [...prev, { role: "assistant", content: raw }])
          setIsLoading(false)
          return
        }

        const aiMessage = suggestion.message
          ?? `"${suggestion.name}" 브릭을 추천드립니다!\n\n${suggestion.description}\n\n오른쪽 폼에 내용을 자동으로 채워드렸습니다.`

        setMessages((prev) => [...prev, { role: "assistant", content: aiMessage }])
        setName(suggestion.name)
        setDescription(suggestion.description)
        if (CATEGORIES.includes(suggestion.category as BrickCategory)) {
          setCategory(suggestion.category as BrickCategory)
        }
        setSelectedTags(suggestion.tags.filter((t) => TAGS.includes(t)))
      } else {
        await new Promise((res) => setTimeout(res, 1500))
        const suggestion = getKeywordSuggestion(trimmed)
        setMessages((prev) => [
          ...prev,
          {
            role: "assistant",
            content: `좋은 아이디어네요! "${suggestion.name}" 브릭을 추천드립니다.\n\n${suggestion.description}\n\n오른쪽 폼에 내용을 자동으로 채워드렸습니다.`,
          },
        ])
        setName(suggestion.name)
        setDescription(suggestion.description)
        setCategory(suggestion.category)
        setSelectedTags(suggestion.tags)
      }
    } catch (err) {
      const msg = err instanceof Error ? err.message : "알 수 없는 오류가 발생했습니다."
      setMessages((prev) => [
        ...prev,
        { role: "assistant", content: `오류가 발생했습니다: ${msg}` },
      ])
    }

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

  const providerLabel = PROVIDERS.find((p) => p.id === provider)?.label ?? provider

  return (
    <div className="p-6 h-full">
      <div className="mb-6">
        <h1 className="text-2xl font-bold">새 브릭 만들기</h1>
        <p className="text-muted-foreground text-sm mt-1">AI 어시스턴트와 대화하며 업무 브릭을 생성하세요</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 h-[calc(100vh-180px)]">
        {/* AI Chat Panel */}
        <div className="flex flex-col rounded-xl border bg-card overflow-hidden">

          {/* Header: provider tabs + mode toggle */}
          <div className="flex flex-col border-b bg-muted/30">
            <div className="flex items-center gap-1 px-3 pt-2.5 pb-0">
              {PROVIDERS.map((p) => (
                <button
                  key={p.id}
                  onClick={() => handleProviderChange(p.id)}
                  className={cn(
                    "px-3 py-1.5 text-xs font-medium rounded-t-md border-b-2 transition-all",
                    provider === p.id
                      ? "border-primary text-primary bg-background"
                      : "border-transparent text-muted-foreground hover:text-foreground"
                  )}
                >
                  {p.label}
                </button>
              ))}
              <div className="ml-auto flex items-center gap-1 mb-1">
                <button
                  onClick={() => setChatMode("api")}
                  className={cn(
                    "flex items-center gap-1 px-2.5 py-1 text-xs rounded-md border transition-all",
                    chatMode === "api"
                      ? "bg-primary text-primary-foreground border-primary"
                      : "border-border text-muted-foreground hover:text-foreground"
                  )}
                >
                  <MessageSquare className="h-3 w-3" />
                  API
                </button>
                <button
                  onClick={() => setChatMode("web")}
                  className={cn(
                    "flex items-center gap-1 px-2.5 py-1 text-xs rounded-md border transition-all",
                    chatMode === "web"
                      ? "bg-primary text-primary-foreground border-primary"
                      : "border-border text-muted-foreground hover:text-foreground"
                  )}
                >
                  <Globe className="h-3 w-3" />
                  웹
                </button>
              </div>
            </div>

            {/* Sub-header: key status + sparkle */}
            <div className="flex items-center gap-2 px-4 py-2">
              <div className="w-6 h-6 rounded-full bg-primary flex items-center justify-center">
                <Bot className="h-3.5 w-3.5 text-primary-foreground" />
              </div>
              <p className="text-xs font-medium">{providerLabel} AI 어시스턴트</p>
              <div className="ml-auto flex items-center gap-2">
                {apiKey ? (
                  <button
                    onClick={clearApiKey}
                    className="flex items-center gap-1 text-xs text-green-600 hover:text-red-500 transition-colors"
                    title="클릭하여 API 키 제거"
                  >
                    <CheckCircle2 className="h-3.5 w-3.5" />
                    <span>{providerLabel} 연결됨</span>
                  </button>
                ) : (
                  chatMode === "api" && (
                    <button
                      onClick={() => setShowKeyInput((v) => !v)}
                      className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
                    >
                      <Key className="h-3.5 w-3.5" />
                      <span>API 키 설정</span>
                    </button>
                  )
                )}
                <Sparkles className="h-3.5 w-3.5 text-yellow-500" />
              </div>
            </div>
          </div>

          {/* API key input area */}
          {showKeyInput && !apiKey && chatMode === "api" && (
            <div className="px-4 py-2.5 border-b bg-muted/20 space-y-2">
              <Input
                type="password"
                value={apiKeyInput}
                onChange={(e) => setApiKeyInput(e.target.value)}
                onKeyDown={(e) => { if (e.key === "Enter" && provider !== "mimo") saveApiKey() }}
                placeholder={provider === "claude" ? "sk-ant-..." : provider === "gemini" ? "AIza..." : "API 키 입력"}
                className="text-xs h-8"
              />
              {provider === "mimo" && (
                <Input
                  type="text"
                  value={mimoEndpointInput}
                  onChange={(e) => setMimoEndpointInput(e.target.value)}
                  placeholder="엔드포인트 URL (예: https://api.mimo.ai/v1/chat/completions)"
                  className="text-xs h-8"
                />
              )}
              <Button
                size="sm"
                className="h-8 text-xs w-full"
                onClick={saveApiKey}
                disabled={!apiKeyInput.trim()}
              >
                저장
              </Button>
            </div>
          )}

          {/* Webview mode */}
          {chatMode === "web" ? (
            <webview
              src={WEB_URLS[provider]}
              className="flex-1 w-full"
              style={{ border: "none" }}
            />
          ) : (
            <>
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
            </>
          )}
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
