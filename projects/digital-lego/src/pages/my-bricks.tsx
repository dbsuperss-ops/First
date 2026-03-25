import { useState, useEffect } from "react"
import { Pencil, Trash2, Box, Layers, CheckCircle2, FileEdit, Plus } from "lucide-react"
import { Link } from "react-router-dom"
import { getBricks, getAssemblies, updateBrick, deleteBrick, updateAssembly, deleteAssembly } from "@/lib/store"
import type { Brick, Assembly, BrickCategory, BrickType, BrickStatus } from "@/types"
import { BrickCard } from "@/components/BrickCard"
import { AssemblyCard } from "@/components/AssemblyCard"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog"
import { cn } from "@/lib/utils"

const CATEGORIES: BrickCategory[] = ["재무", "인사", "대시보드", "구매", "품질"]
const TYPES: BrickType[] = ["단일", "복합"]
const STATUSES: BrickStatus[] = ["Draft", "Published"]
const MY_AUTHOR = "경신 사원"

export default function MyBricks() {
  const [bricks, setBricks] = useState<Brick[]>([])
  const [assemblies, setAssemblies] = useState<Assembly[]>([])
  const [editingBrick, setEditingBrick] = useState<Brick | null>(null)
  const [deletingBrick, setDeletingBrick] = useState<Brick | null>(null)
  const [editingAssembly, setEditingAssembly] = useState<Assembly | null>(null)
  const [deletingAssembly, setDeletingAssembly] = useState<Assembly | null>(null)

  // Brick edit form state
  const [editName, setEditName] = useState("")
  const [editDescription, setEditDescription] = useState("")
  const [editCategory, setEditCategory] = useState<BrickCategory>("재무")
  const [editType, setEditType] = useState<BrickType>("단일")
  const [editStatus, setEditStatus] = useState<BrickStatus>("Draft")

  // Assembly edit form state
  const [editAssemblyName, setEditAssemblyName] = useState("")
  const [editAssemblyDesc, setEditAssemblyDesc] = useState("")
  const [editAssemblyStatus, setEditAssemblyStatus] = useState<BrickStatus>("Draft")

  const refresh = () => {
    setBricks(getBricks())
    setAssemblies(getAssemblies())
  }

  useEffect(() => {
    refresh()
  }, [])

  const myBricks = bricks.filter((b) => b.author === MY_AUTHOR)
  const myAssemblies = assemblies.filter((a) => a.author === MY_AUTHOR)

  const publishedCount = myBricks.filter((b) => b.status === "Published").length
  const draftCount = myBricks.filter((b) => b.status === "Draft").length

  const openEdit = (brick: Brick) => {
    setEditingBrick(brick)
    setEditName(brick.name)
    setEditDescription(brick.description)
    setEditCategory(brick.category)
    setEditType(brick.type)
    setEditStatus(brick.status)
  }

  const handleSaveEdit = () => {
    if (!editingBrick || !editName.trim()) return
    updateBrick(editingBrick.id, {
      name: editName.trim(),
      description: editDescription.trim(),
      category: editCategory,
      type: editType,
      status: editStatus,
    })
    setEditingBrick(null)
    refresh()
  }

  const handleDelete = () => {
    if (!deletingBrick) return
    deleteBrick(deletingBrick.id)
    setDeletingBrick(null)
    refresh()
  }

  const openEditAssembly = (assembly: Assembly) => {
    setEditingAssembly(assembly)
    setEditAssemblyName(assembly.name)
    setEditAssemblyDesc(assembly.description)
    setEditAssemblyStatus(assembly.status)
  }

  const handleSaveAssemblyEdit = () => {
    if (!editingAssembly || !editAssemblyName.trim()) return
    updateAssembly(editingAssembly.id, {
      name: editAssemblyName.trim(),
      description: editAssemblyDesc.trim(),
      status: editAssemblyStatus,
    })
    setEditingAssembly(null)
    refresh()
  }

  const handleDeleteAssembly = () => {
    if (!deletingAssembly) return
    deleteAssembly(deletingAssembly.id)
    setDeletingAssembly(null)
    refresh()
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">내 브릭</h1>
          <p className="text-muted-foreground text-sm mt-1">내가 만들고 조립한 브릭을 관리하세요</p>
        </div>
        <Link to="/create">
          <Button size="sm" className="gap-2">
            <Plus className="h-4 w-4" />
            새 브릭 만들기
          </Button>
        </Link>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div className="rounded-xl border bg-card p-4 flex items-center gap-3">
          <div className="p-2.5 rounded-lg bg-blue-50">
            <Box className="h-5 w-5 text-blue-600" />
          </div>
          <div>
            <p className="text-2xl font-bold">{myBricks.length}</p>
            <p className="text-xs text-muted-foreground">내 브릭</p>
          </div>
        </div>
        <div className="rounded-xl border bg-card p-4 flex items-center gap-3">
          <div className="p-2.5 rounded-lg bg-green-50">
            <CheckCircle2 className="h-5 w-5 text-green-600" />
          </div>
          <div>
            <p className="text-2xl font-bold">{publishedCount}</p>
            <p className="text-xs text-muted-foreground">발행됨</p>
          </div>
        </div>
        <div className="rounded-xl border bg-card p-4 flex items-center gap-3">
          <div className="p-2.5 rounded-lg bg-yellow-50">
            <FileEdit className="h-5 w-5 text-yellow-600" />
          </div>
          <div>
            <p className="text-2xl font-bold">{draftCount}</p>
            <p className="text-xs text-muted-foreground">초안</p>
          </div>
        </div>
        <div className="rounded-xl border bg-card p-4 flex items-center gap-3">
          <div className="p-2.5 rounded-lg bg-purple-50">
            <Layers className="h-5 w-5 text-purple-600" />
          </div>
          <div>
            <p className="text-2xl font-bold">{myAssemblies.length}</p>
            <p className="text-xs text-muted-foreground">조립체</p>
          </div>
        </div>
      </div>

      {/* My Bricks */}
      <section>
        <h2 className="text-lg font-bold mb-4 flex items-center gap-2">
          <Box className="h-5 w-5 text-primary" />
          내가 만든 브릭
        </h2>
        {myBricks.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center border rounded-xl bg-card">
            <Box className="h-10 w-10 text-muted-foreground/40 mb-3" />
            <p className="text-muted-foreground font-medium">아직 만든 브릭이 없습니다</p>
            <Link to="/create">
              <Button variant="link" className="mt-2 text-sm">첫 번째 브릭 만들기 →</Button>
            </Link>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {myBricks.map((brick) => (
              <div key={brick.id} className="group relative">
                <BrickCard
                  brick={brick}
                  onRate={(rating) => { updateBrick(brick.id, { rating }); refresh() }}
                />
                {/* Action overlay */}
                <div className="absolute top-3 right-3 flex gap-1.5 opacity-0 group-hover:opacity-100 transition-opacity">
                  <button
                    onClick={() => openEdit(brick)}
                    className="p-1.5 rounded-lg bg-white/90 border shadow-sm hover:bg-white transition-all"
                    title="편집"
                  >
                    <Pencil className="h-3.5 w-3.5 text-foreground" />
                  </button>
                  <button
                    onClick={() => setDeletingBrick(brick)}
                    className="p-1.5 rounded-lg bg-white/90 border shadow-sm hover:bg-destructive hover:text-white transition-all"
                    title="삭제"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* My Assemblies */}
      <section>
        <h2 className="text-lg font-bold mb-4 flex items-center gap-2">
          <Layers className="h-5 w-5 text-purple-500" />
          내가 조립한 브릭
        </h2>
        {myAssemblies.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center border rounded-xl bg-card">
            <Layers className="h-10 w-10 text-muted-foreground/40 mb-3" />
            <p className="text-muted-foreground font-medium">아직 조립한 브릭이 없습니다</p>
            <Link to="/assemble">
              <Button variant="link" className="mt-2 text-sm">브릭 조립하러 가기 →</Button>
            </Link>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {myAssemblies.map((assembly) => (
              <div key={assembly.id} className="group relative">
                <AssemblyCard assembly={assembly} />
                <div className="absolute top-3 right-3 flex gap-1.5 opacity-0 group-hover:opacity-100 transition-opacity">
                  <button
                    onClick={() => openEditAssembly(assembly)}
                    className="p-1.5 rounded-lg bg-white/90 border shadow-sm hover:bg-white transition-all"
                    title="편집"
                  >
                    <Pencil className="h-3.5 w-3.5 text-foreground" />
                  </button>
                  <button
                    onClick={() => setDeletingAssembly(assembly)}
                    className="p-1.5 rounded-lg bg-white/90 border shadow-sm hover:bg-destructive hover:text-white transition-all"
                    title="삭제"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* Brick Edit Dialog */}
      <Dialog open={!!editingBrick} onOpenChange={(open) => !open && setEditingBrick(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>브릭 편집</DialogTitle>
            <DialogDescription>브릭 정보를 수정하세요</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div>
              <label className="text-sm font-medium mb-1.5 block">브릭 이름 *</label>
              <Input value={editName} onChange={(e) => setEditName(e.target.value)} />
            </div>
            <div>
              <label className="text-sm font-medium mb-1.5 block">설명</label>
              <Textarea
                value={editDescription}
                onChange={(e) => setEditDescription(e.target.value)}
                rows={3}
              />
            </div>
            <div>
              <label className="text-sm font-medium mb-1.5 block">카테고리</label>
              <div className="flex flex-wrap gap-2">
                {CATEGORIES.map((cat) => (
                  <button
                    key={cat}
                    onClick={() => setEditCategory(cat)}
                    className={cn(
                      "px-3 py-1.5 rounded-lg text-sm font-medium border transition-all",
                      editCategory === cat
                        ? "bg-primary text-primary-foreground border-primary"
                        : "bg-background border-border hover:bg-secondary"
                    )}
                  >
                    {cat}
                  </button>
                ))}
              </div>
            </div>
            <div>
              <label className="text-sm font-medium mb-1.5 block">유형</label>
              <div className="flex gap-2">
                {TYPES.map((t) => (
                  <button
                    key={t}
                    onClick={() => setEditType(t)}
                    className={cn(
                      "px-3 py-1.5 rounded-lg text-sm font-medium border transition-all",
                      editType === t
                        ? "bg-primary text-primary-foreground border-primary"
                        : "bg-background border-border hover:bg-secondary"
                    )}
                  >
                    {t}
                  </button>
                ))}
              </div>
            </div>
            <div>
              <label className="text-sm font-medium mb-1.5 block">상태</label>
              <div className="flex gap-2">
                {STATUSES.map((s) => (
                  <button
                    key={s}
                    onClick={() => setEditStatus(s)}
                    className={cn(
                      "px-3 py-1.5 rounded-lg text-sm font-medium border transition-all",
                      editStatus === s
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

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditingBrick(null)}>취소</Button>
            <Button onClick={handleSaveEdit} disabled={!editName.trim()}>저장</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Brick Delete Confirmation Dialog */}
      <Dialog open={!!deletingBrick} onOpenChange={(open) => !open && setDeletingBrick(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>브릭 삭제</DialogTitle>
            <DialogDescription>
              <span className="font-semibold text-foreground">"{deletingBrick?.name}"</span>을(를) 삭제하시겠습니까?
              이 작업은 되돌릴 수 없습니다.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeletingBrick(null)}>취소</Button>
            <Button variant="destructive" onClick={handleDelete}>삭제</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Assembly Edit Dialog */}
      <Dialog open={!!editingAssembly} onOpenChange={(open) => !open && setEditingAssembly(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>조립체 편집</DialogTitle>
            <DialogDescription>조립체 정보를 수정하세요 (브릭 구성 변경은 조립 페이지에서)</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div>
              <label className="text-sm font-medium mb-1.5 block">시스템 이름 *</label>
              <Input value={editAssemblyName} onChange={(e) => setEditAssemblyName(e.target.value)} />
            </div>
            <div>
              <label className="text-sm font-medium mb-1.5 block">설명</label>
              <Textarea
                value={editAssemblyDesc}
                onChange={(e) => setEditAssemblyDesc(e.target.value)}
                rows={3}
              />
            </div>
            <div>
              <label className="text-sm font-medium mb-1.5 block">상태</label>
              <div className="flex gap-2">
                {STATUSES.map((s) => (
                  <button
                    key={s}
                    onClick={() => setEditAssemblyStatus(s)}
                    className={cn(
                      "px-3 py-1.5 rounded-lg text-sm font-medium border transition-all",
                      editAssemblyStatus === s
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

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditingAssembly(null)}>취소</Button>
            <Button onClick={handleSaveAssemblyEdit} disabled={!editAssemblyName.trim()}>저장</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Assembly Delete Confirmation Dialog */}
      <Dialog open={!!deletingAssembly} onOpenChange={(open) => !open && setDeletingAssembly(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>조립체 삭제</DialogTitle>
            <DialogDescription>
              <span className="font-semibold text-foreground">"{deletingAssembly?.name}"</span>을(를) 삭제하시겠습니까?
              이 작업은 되돌릴 수 없습니다.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeletingAssembly(null)}>취소</Button>
            <Button variant="destructive" onClick={handleDeleteAssembly}>삭제</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
