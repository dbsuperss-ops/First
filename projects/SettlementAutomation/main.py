import os
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
from pathlib import Path

class SettlementAutomationApp:
    def __init__(self, root):
        self.root = root
        self.root.title("결산 데이터 정제 자동화 시스템 v1.0")
        self.root.geometry("800QC00")
        
        # 상태 변수
        self.selected_path = tk.StringVar()
        self.file_list = []
        
        self._setup_ui()

    def _setup_ui(self):
        # 상단: 경로 선택 영역
        top_frame = ttk.Frame(self.root, padding="10")
        top_frame.pack(fill=tk.X)
        
        ttk.Label(top_frame, text="대상 폴더/파일:").pack(side=tk.LEFT)
        ttk.Entry(top_frame, textvariable=self.selected_path, width=60).pack(side=tk.LEFT, padx=5)
        ttk.Button(top_frame, text="폴더 선택", command=self._select_folder).pack(side=tk.LEFT, padx=2)
        ttk.Button(top_frame, text="파일 선택", command=self._select_files).pack(side=tk.LEFT, padx=2)

        # 중앙: 파일 목록 영역
        mid_frame = ttk.LabelFrame(self.root, text="탐지된 파일 리스트 (하위 폴더 포함)", padding="10")
        mid_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)
        
        self.tree = ttk.Treeview(mid_frame, columns=("path", "size", "type"), show="headings")
        self.tree.heading("path", text="파일 경로")
        self.tree.heading("size", text="크기")
        self.tree.heading("type", text="확장자")
        self.tree.column("path", width=500)
        self.tree.column("size", width=100)
        self.tree.column("type", width=100)
        self.tree.pack(fill=tk.BOTH, expand=True)
        
        # 스크롤바 추가
        scrollbar = ttk.Scrollbar(self.tree, orient=tk.VERTICAL, command=self.tree.yview)
        self.tree.configure(yscroll=scrollbar.set)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

        # 하단: 실행 버튼 영역
        bottom_frame = ttk.Frame(self.root, padding="10")
        bottom_frame.pack(fill=tk.X)
        
        self.btn_run = ttk.Button(bottom_frame, text="데이터 정제 시작", command=self._run_refinement, state=tk.DISABLED)
        self.btn_run.pack(side=tk.RIGHT, padx=5)
        
        ttk.Button(bottom_frame, text="목록 새로고침", command=self._refresh_list).pack(side=tk.RIGHT, padx=5)

    def _select_folder(self):
        folder = filedialog.askdirectory()
        if folder:
            self.selected_path.set(folder)
            self._refresh_list()

    def _select_files(self):
        files = filedialog.askopenfilenames(filetypes=[("Excel files", "*.xlsx *.xls"), ("CSV files", "*.csv")])
        if files:
            self.selected_path.set(";".join(files))
            self._refresh_list()

    def _refresh_list(self):
        # 기존 목록 삭제
        for i in self.tree.get_children():
            self.tree.delete(i)
        
        path_str = self.selected_path.get()
        if not path_str:
            return

        self.file_list = []
        
        # 여러 파일이 선택된 경우 (세미콜론으로 구분)
        if ";" in path_str:
            paths = path_str.split(";")
            for p in paths:
                self._add_file_to_tree(Path(p))
        else:
            root_path = Path(path_str)
            if root_path.is_dir():
                # 하위 폴더 포함 모든 엑셀/CSV 탐색
                for ext in ['*.xlsx', '*.xls', '*.csv']:
                    for file_path in root_path.rglob(ext):
                        self._add_file_to_tree(file_path)
            elif root_path.is_file():
                self._add_file_to_tree(root_path)

        if self.file_list:
            self.btn_run.config(state=tk.NORMAL)
        else:
            self.btn_run.config(state=tk.DISABLED)

    def _add_file_to_tree(self, file_path):
        if not file_path.exists():
            return
            
        stats = file_path.stat()
        size_kb = f"{stats.st_size / 1024:.1f} KB"
        ext = file_path.suffix
        
        self.tree.insert("", tk.END, values=(str(file_path), size_kb, ext))
        self.file_list.append(file_path)

    def _run_refinement(self):
        # 2단계에서 구현할 정제 로직의 입구
        messagebox.showinfo("알림", f"{len(self.file_list)}개의 파일에 대해 정제 로직을 구성합니다.\n다음 단계에서 데이터 처리 엔진을 연결하겠습니다.")

if __name__ == "__main__":
    root = tk.Tk()
    app = SettlementAutomationApp(root)
    root.mainloop()
