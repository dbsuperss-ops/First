import { Link } from "react-router-dom"
import { Box, Home } from "lucide-react"
import { Button } from "@/components/ui/button"

export default function NotFound() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="text-center">
        <div className="flex justify-center mb-6">
          <div className="w-20 h-20 rounded-2xl bg-muted flex items-center justify-center">
            <Box className="h-10 w-10 text-muted-foreground" />
          </div>
        </div>
        <h1 className="text-6xl font-bold text-primary mb-4">404</h1>
        <h2 className="text-xl font-semibold mb-2">페이지를 찾을 수 없습니다</h2>
        <p className="text-muted-foreground mb-8">요청하신 페이지가 존재하지 않거나 이동되었습니다.</p>
        <Link to="/">
          <Button size="lg" className="gap-2">
            <Home className="h-4 w-4" />
            대시보드로 돌아가기
          </Button>
        </Link>
      </div>
    </div>
  )
}
