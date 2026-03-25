import { Link } from "react-router-dom"
import { Plus, Box, Layers, Tag, CheckCircle2, TrendingUp } from "lucide-react"
import { getBricks, getAssemblies } from "@/lib/store"
import { BrickCard } from "@/components/BrickCard"
import { AssemblyCard } from "@/components/AssemblyCard"
import { Button } from "@/components/ui/button"

export default function Dashboard() {
  const bricks = getBricks()
  const assemblies = getAssemblies()

  const publishedCount = bricks.filter((b) => b.status === "Published").length
  const complexCount = bricks.filter((b) => b.type === "복합").length
  const categories = new Set(bricks.map((b) => b.category)).size

  const topBricks = bricks
    .filter((b) => b.status === "Published")
    .sort((a, b) => b.rating - a.rating)
    .slice(0, 6)

  const stats = [
    { label: "전체 브릭", value: bricks.length, icon: Box, color: "text-blue-600", bg: "bg-blue-50" },
    { label: "발행됨", value: publishedCount, icon: CheckCircle2, color: "text-green-600", bg: "bg-green-50" },
    { label: "복합 시스템", value: complexCount, icon: Layers, color: "text-purple-600", bg: "bg-purple-50" },
    { label: "카테고리", value: categories, icon: Tag, color: "text-orange-600", bg: "bg-orange-50" },
  ]

  return (
    <div className="p-6 space-y-8">
      {/* Hero Banner */}
      <section className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-primary to-blue-400 p-8 text-white">
        <div className="lego-pattern absolute inset-0 opacity-20" />
        <div className="relative z-10 flex flex-col md:flex-row items-center justify-between gap-6">
          <div>
            <div className="flex items-center gap-2 mb-3">
              <Box className="h-6 w-6 animate-float" />
              <span className="text-sm font-medium opacity-80">Kyungshin 디지털 레고</span>
            </div>
            <h1 className="text-3xl font-bold mb-2 leading-tight">
              나의 업무를<br />블록으로 조립하세요
            </h1>
            <p className="text-white/70 text-sm max-w-sm">
              재사용 가능한 업무 모듈을 만들고, 조합하여<br />
              더 스마트한 업무 환경을 구축하세요.
            </p>
          </div>
          <div className="flex flex-col gap-3 shrink-0">
            <Link to="/create">
              <Button size="lg" className="bg-white text-primary hover:bg-white/90 font-semibold gap-2 w-full">
                <Plus className="h-4 w-4" />
                새 브릭 만들기
              </Button>
            </Link>
            <Link to="/assemble">
              <Button size="lg" variant="outline" className="border-white/30 text-white hover:bg-white/10 w-full gap-2">
                <Layers className="h-4 w-4" />
                브릭 조립하기
              </Button>
            </Link>
          </div>
        </div>
        {/* Decorative bricks */}
        <div className="absolute top-4 right-32 w-8 h-8 rounded-lg bg-white/10 border border-white/20 animate-float" style={{ animationDelay: "0.5s" }} />
        <div className="absolute top-12 right-20 w-6 h-6 rounded-md bg-white/10 border border-white/20 animate-float" style={{ animationDelay: "1s" }} />
        <div className="absolute bottom-6 right-40 w-10 h-6 rounded-lg bg-white/10 border border-white/20 animate-float" style={{ animationDelay: "1.5s" }} />
      </section>

      {/* Stats */}
      <section>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {stats.map((stat) => (
            <div key={stat.label} className="rounded-xl border bg-card p-4 flex items-center gap-3">
              <div className={`p-2.5 rounded-lg ${stat.bg}`}>
                <stat.icon className={`h-5 w-5 ${stat.color}`} />
              </div>
              <div>
                <p className="text-2xl font-bold">{stat.value}</p>
                <p className="text-xs text-muted-foreground">{stat.label}</p>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Popular Bricks */}
      <section>
        <div className="flex items-center justify-between mb-4">
          <div>
            <h2 className="text-lg font-bold flex items-center gap-2">
              <TrendingUp className="h-5 w-5 text-primary" />
              최근 생성된 인기 브릭
            </h2>
            <p className="text-sm text-muted-foreground">별점 높은 발행된 브릭을 확인하세요</p>
          </div>
          <Link to="/storage">
            <Button variant="ghost" size="sm">전체 보기</Button>
          </Link>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {topBricks.map((brick) => (
            <BrickCard key={brick.id} brick={brick} />
          ))}
        </div>
      </section>

      {/* Assemblies */}
      <section>
        <div className="flex items-center justify-between mb-4">
          <div>
            <h2 className="text-lg font-bold flex items-center gap-2">
              <Layers className="h-5 w-5 text-purple-500" />
              가장 많이 조립된 복합 브릭
            </h2>
            <p className="text-sm text-muted-foreground">여러 브릭을 조합한 복합 시스템</p>
          </div>
          <Link to="/assemble">
            <Button variant="ghost" size="sm">조립 시작</Button>
          </Link>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {assemblies.map((assembly) => (
            <AssemblyCard key={assembly.id} assembly={assembly} />
          ))}
        </div>
      </section>
    </div>
  )
}
