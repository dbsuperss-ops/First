import { NavLink, Outlet } from "react-router-dom"
import {
  LayoutDashboard,
  Plus,
  Archive,
  Layers,
  User,
  Box,
  ChevronRight,
} from "lucide-react"
import { cn } from "@/lib/utils"

const navItems = [
  { to: "/", icon: LayoutDashboard, label: "대시보드", end: true },
  { to: "/create", icon: Plus, label: "브릭 생성", end: false },
  { to: "/storage", icon: Archive, label: "모듈 창고", end: false },
  { to: "/assemble", icon: Layers, label: "브릭 조립", end: false },
  { to: "/my-bricks", icon: User, label: "내 브릭", end: false },
]

export default function Layout() {
  return (
    <div className="flex h-screen overflow-hidden bg-background">
      {/* Sidebar */}
      <aside className="w-64 flex flex-col bg-sidebar text-sidebar-foreground shrink-0">
        {/* Logo */}
        <div className="flex items-center gap-3 px-6 py-5 border-b border-sidebar-border">
          <div className="flex items-center justify-center w-9 h-9 rounded-xl bg-sidebar-primary">
            <Box className="h-5 w-5 text-sidebar-primary-foreground" />
          </div>
          <div>
            <p className="font-bold text-sm leading-tight">디지털 레고</p>
            <p className="text-xs text-sidebar-foreground/60">Kyungshin ERP</p>
          </div>
        </div>

        {/* Navigation */}
        <nav className="flex-1 px-3 py-4 space-y-1">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                cn(
                  "flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm transition-all duration-150",
                  isActive
                    ? "bg-sidebar-accent text-sidebar-accent-foreground font-medium"
                    : "text-sidebar-foreground/70 hover:bg-sidebar-accent/50 hover:text-sidebar-accent-foreground"
                )
              }
            >
              {({ isActive }) => (
                <>
                  <item.icon className="h-4 w-4 shrink-0" />
                  <span className="flex-1">{item.label}</span>
                  {isActive && <ChevronRight className="h-3 w-3 opacity-50" />}
                </>
              )}
            </NavLink>
          ))}
        </nav>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-sidebar-border">
          <div className="flex items-center gap-2">
            <div className="w-7 h-7 rounded-full bg-sidebar-primary/30 flex items-center justify-center">
              <User className="h-4 w-4" />
            </div>
            <div>
              <p className="text-xs font-medium">경신 사원</p>
              <p className="text-xs text-sidebar-foreground/50">kyungshin.co.kr</p>
            </div>
          </div>
        </div>
      </aside>

      {/* Main content */}
      <main className="flex-1 overflow-auto">
        <Outlet />
      </main>
    </div>
  )
}
