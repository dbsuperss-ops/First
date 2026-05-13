"""
Soccerline 게시글/댓글 분석 UI.

실행:
    python soccerline_analysis_ui.py

필요 패키지:
    pip install pandas openpyxl matplotlib seaborn networkx
"""

from __future__ import annotations

import os
import queue
import threading
import traceback
from pathlib import Path
from tkinter import (
    BOTH,
    DISABLED,
    END,
    LEFT,
    NORMAL,
    RIGHT,
    X,
    Y,
    BooleanVar,
    StringVar,
    Tk,
    filedialog,
    messagebox,
)
from tkinter import ttk

import pandas as pd

from analyze_soccerline_interactions import (
    DEFAULT_FILES,
    DEFAULT_KEYWORDS,
    analyze,
    plot_heatmap,
    plot_network,
    read_exports,
    save_outputs,
    setup_korean_font,
)


APP_TITLE = "Soccerline Interaction Analyzer"


class AnalysisApp:
    def __init__(self, root: Tk) -> None:
        self.root = root
        self.root.title(APP_TITLE)
        self.root.geometry("1320x820")
        self.root.minsize(1080, 700)

        self.files: list[Path] = [path for path in DEFAULT_FILES if path.exists()]
        self.results: dict[str, pd.DataFrame] = {}
        self.worker: threading.Thread | None = None
        self.messages: queue.Queue[tuple[str, object]] = queue.Queue()

        self.keywords_var = StringVar(value=", ".join(DEFAULT_KEYWORDS))
        self.min_posts_var = StringVar(value="2")
        self.window_minutes_var = StringVar(value="5")
        self.top_n_var = StringVar(value="30")
        self.output_dir_var = StringVar(value=str(Path("analysis_outputs").resolve()))
        self.open_when_done_var = BooleanVar(value=False)

        self.status_var = StringVar(value="대기 중")
        self.file_list_var = StringVar(value="")

        self.tables: dict[str, ttk.Treeview] = {}
        self.table_frames: dict[str, ttk.Frame] = {}

        self.configure_style()
        self.build_layout()
        self.refresh_file_list()
        self.root.after(120, self.poll_messages)

    def configure_style(self) -> None:
        style = ttk.Style()
        try:
            style.theme_use("clam")
        except Exception:
            pass
        style.configure("TFrame", background="#111318")
        style.configure("Panel.TFrame", background="#171A21")
        style.configure("TLabel", background="#111318", foreground="#E8EAED")
        style.configure("Muted.TLabel", foreground="#AAB0BD")
        style.configure("Title.TLabel", font=("Malgun Gothic", 18, "bold"))
        style.configure("Section.TLabel", background="#171A21", font=("Malgun Gothic", 11, "bold"))
        style.configure("TButton", padding=(10, 6))
        style.configure("Primary.TButton", padding=(12, 8))
        style.configure("TEntry", fieldbackground="#F7F8FA")
        style.configure("Treeview", rowheight=28, font=("Malgun Gothic", 9))
        style.configure("Treeview.Heading", font=("Malgun Gothic", 9, "bold"))
        style.map("TButton", foreground=[("disabled", "#777777")])

    def build_layout(self) -> None:
        root_frame = ttk.Frame(self.root, padding=16)
        root_frame.pack(fill=BOTH, expand=True)

        header = ttk.Frame(root_frame)
        header.pack(fill=X, pady=(0, 12))
        ttk.Label(header, text=APP_TITLE, style="Title.TLabel").pack(side=LEFT)
        ttk.Label(header, textvariable=self.status_var, style="Muted.TLabel").pack(side=RIGHT)

        body = ttk.PanedWindow(root_frame, orient="horizontal")
        body.pack(fill=BOTH, expand=True)

        left = ttk.Frame(body, style="Panel.TFrame", padding=14)
        right = ttk.Frame(body, padding=(12, 0, 0, 0))
        body.add(left, weight=0)
        body.add(right, weight=1)

        self.build_controls(left)
        self.build_results(right)

    def build_controls(self, parent: ttk.Frame) -> None:
        ttk.Label(parent, text="파일", style="Section.TLabel").pack(anchor="w", pady=(0, 8))
        ttk.Button(parent, text="Excel 파일 추가", command=self.add_files).pack(fill=X, pady=(0, 6))
        ttk.Button(parent, text="선택 초기화", command=self.clear_files).pack(fill=X, pady=(0, 8))

        file_box = ttk.Frame(parent)
        file_box.pack(fill=BOTH, expand=False)
        self.file_list = ttk.Treeview(file_box, columns=("path",), show="headings", height=7)
        self.file_list.heading("path", text="선택된 파일")
        self.file_list.column("path", width=360, stretch=True)
        self.file_list.pack(side=LEFT, fill=BOTH, expand=True)
        file_scroll = ttk.Scrollbar(file_box, orient="vertical", command=self.file_list.yview)
        file_scroll.pack(side=RIGHT, fill=Y)
        self.file_list.configure(yscrollcommand=file_scroll.set)

        ttk.Separator(parent).pack(fill=X, pady=14)

        ttk.Label(parent, text="분석 조건", style="Section.TLabel").pack(anchor="w", pady=(0, 8))
        self.add_labeled_entry(parent, "키워드 (쉼표 구분)", self.keywords_var)
        self.add_labeled_entry(parent, "반복 작성 최소 글 수", self.min_posts_var)
        self.add_labeled_entry(parent, "집중 댓글 기준 시간(분)", self.window_minutes_var)
        self.add_labeled_entry(parent, "상위 표시 건수", self.top_n_var)

        ttk.Separator(parent).pack(fill=X, pady=14)

        ttk.Label(parent, text="출력", style="Section.TLabel").pack(anchor="w", pady=(0, 8))
        out_row = ttk.Frame(parent, style="Panel.TFrame")
        out_row.pack(fill=X, pady=(0, 6))
        ttk.Entry(out_row, textvariable=self.output_dir_var).pack(side=LEFT, fill=X, expand=True)
        ttk.Button(out_row, text="선택", command=self.choose_output_dir).pack(side=RIGHT, padx=(6, 0))
        ttk.Checkbutton(
            parent,
            text="분석 후 결과 폴더 열기",
            variable=self.open_when_done_var,
        ).pack(anchor="w", pady=(0, 12))

        self.run_button = ttk.Button(parent, text="분석 실행", style="Primary.TButton", command=self.run_analysis)
        self.run_button.pack(fill=X, pady=(0, 8))
        ttk.Button(parent, text="결과 폴더 열기", command=self.open_output_dir).pack(fill=X, pady=(0, 6))
        ttk.Button(parent, text="히트맵 열기", command=lambda: self.open_output_file("interaction_heatmap.png")).pack(fill=X, pady=(0, 6))
        ttk.Button(parent, text="네트워크 그래프 열기", command=lambda: self.open_output_file("interaction_network.png")).pack(fill=X)

    def add_labeled_entry(self, parent: ttk.Frame, label: str, variable: StringVar) -> None:
        ttk.Label(parent, text=label, style="Muted.TLabel").pack(anchor="w", pady=(5, 2))
        ttk.Entry(parent, textvariable=variable).pack(fill=X, pady=(0, 4))

    def build_results(self, parent: ttk.Frame) -> None:
        self.notebook = ttk.Notebook(parent)
        self.notebook.pack(fill=BOTH, expand=True)

        for key, title in [
            ("repeated_authors", "반복 작성자"),
            ("top_interactions", "상호작용 Top"),
            ("top_bursts", "집중 댓글 Top"),
            ("keyword_posts", "키워드 게시글"),
        ]:
            frame = ttk.Frame(self.notebook)
            self.notebook.add(frame, text=title)
            self.table_frames[key] = frame
            self.tables[key] = self.create_table(frame)

        log_frame = ttk.Frame(self.notebook)
        self.notebook.add(log_frame, text="로그")
        self.log_text = ttk.Treeview(log_frame, columns=("message",), show="headings")
        self.log_text.heading("message", text="메시지")
        self.log_text.column("message", width=920, stretch=True)
        self.log_text.pack(side=LEFT, fill=BOTH, expand=True)
        log_scroll = ttk.Scrollbar(log_frame, orient="vertical", command=self.log_text.yview)
        log_scroll.pack(side=RIGHT, fill=Y)
        self.log_text.configure(yscrollcommand=log_scroll.set)

    def create_table(self, parent: ttk.Frame) -> ttk.Treeview:
        container = ttk.Frame(parent)
        container.pack(fill=BOTH, expand=True)

        tree = ttk.Treeview(container, show="headings")
        tree.pack(side=LEFT, fill=BOTH, expand=True)
        y_scroll = ttk.Scrollbar(container, orient="vertical", command=tree.yview)
        y_scroll.pack(side=RIGHT, fill=Y)
        x_scroll = ttk.Scrollbar(parent, orient="horizontal", command=tree.xview)
        x_scroll.pack(side="bottom", fill=X)
        tree.configure(yscrollcommand=y_scroll.set, xscrollcommand=x_scroll.set)
        return tree

    def add_files(self) -> None:
        paths = filedialog.askopenfilenames(
            title="Soccerline Export Excel 파일 선택",
            filetypes=[("Excel files", "*.xlsx;*.xlsm;*.xls"), ("All files", "*.*")],
        )
        if not paths:
            return
        existing = {str(path).lower() for path in self.files}
        for raw in paths:
            path = Path(raw)
            if str(path).lower() not in existing:
                self.files.append(path)
                existing.add(str(path).lower())
        self.refresh_file_list()

    def clear_files(self) -> None:
        self.files.clear()
        self.refresh_file_list()

    def refresh_file_list(self) -> None:
        self.file_list.delete(*self.file_list.get_children())
        for path in self.files:
            self.file_list.insert("", END, values=(str(path),))

    def choose_output_dir(self) -> None:
        path = filedialog.askdirectory(title="결과 저장 폴더 선택")
        if path:
            self.output_dir_var.set(path)

    def parse_options(self) -> tuple[list[Path], list[str], int, int, int, Path]:
        if not self.files:
            raise ValueError("분석할 Excel 파일을 1개 이상 선택하세요.")

        keywords = [part.strip() for part in self.keywords_var.get().split(",") if part.strip()]
        if not keywords:
            raise ValueError("키워드를 1개 이상 입력하세요.")

        min_posts = int(self.min_posts_var.get().strip())
        window_minutes = int(self.window_minutes_var.get().strip())
        top_n = int(self.top_n_var.get().strip())
        if min_posts < 1 or window_minutes < 1 or top_n < 1:
            raise ValueError("반복 글 수, 기준 시간, 상위 표시 건수는 모두 1 이상이어야 합니다.")

        output_dir = Path(self.output_dir_var.get().strip()).expanduser()
        return self.files[:], keywords, min_posts, window_minutes, top_n, output_dir

    def run_analysis(self) -> None:
        if self.worker and self.worker.is_alive():
            messagebox.showinfo("분석 진행 중", "이미 분석이 실행 중입니다.")
            return

        try:
            options = self.parse_options()
        except Exception as exc:
            messagebox.showerror("입력 확인", str(exc))
            return

        self.run_button.configure(state=DISABLED)
        self.status_var.set("분석 중...")
        self.clear_tables()
        self.log("분석을 시작합니다.")

        self.worker = threading.Thread(target=self.analysis_worker, args=options, daemon=True)
        self.worker.start()

    def analysis_worker(
        self,
        files: list[Path],
        keywords: list[str],
        min_posts: int,
        window_minutes: int,
        top_n: int,
        output_dir: Path,
    ) -> None:
        try:
            setup_korean_font()
            self.messages.put(("log", f"파일 {len(files)}개를 읽는 중입니다."))
            posts, comments = read_exports(files)
            self.messages.put(("log", f"Posts {len(posts):,}건, Comments {len(comments):,}건 로드 완료"))

            results = analyze(
                posts=posts,
                comments=comments,
                keywords=keywords,
                min_posts=min_posts,
                window_minutes=window_minutes,
                top_n=top_n,
            )
            self.messages.put(("log", "집계표 생성 완료"))

            save_outputs(results, output_dir)
            plot_heatmap(results["interaction_summary"], output_dir, top_n)
            plot_network(results["interaction_summary"], output_dir, top_n)
            self.messages.put(("done", (results, output_dir)))
        except Exception as exc:
            detail = "".join(traceback.format_exception(exc))
            self.messages.put(("error", detail))

    def poll_messages(self) -> None:
        while True:
            try:
                kind, payload = self.messages.get_nowait()
            except queue.Empty:
                break

            if kind == "log":
                self.log(str(payload))
            elif kind == "done":
                results, output_dir = payload  # type: ignore[misc]
                self.results = results
                self.populate_result_tables(results)
                self.run_button.configure(state=NORMAL)
                self.status_var.set("완료")
                self.log(f"CSV/PNG 저장 완료: {output_dir}")
                if self.open_when_done_var.get():
                    self.open_path(output_dir)
                messagebox.showinfo("완료", f"분석이 완료되었습니다.\n{output_dir}")
            elif kind == "error":
                self.run_button.configure(state=NORMAL)
                self.status_var.set("오류")
                self.log(str(payload))
                messagebox.showerror("분석 오류", "분석 중 오류가 발생했습니다. 로그 탭을 확인하세요.")

        self.root.after(120, self.poll_messages)

    def clear_tables(self) -> None:
        for tree in self.tables.values():
            tree.delete(*tree.get_children())
            tree["columns"] = ()

    def populate_result_tables(self, results: dict[str, pd.DataFrame]) -> None:
        for key, tree in self.tables.items():
            df = results.get(key, pd.DataFrame())
            if key == "keyword_posts":
                top_n = int(self.top_n_var.get().strip())
                df = df.head(max(top_n, 100))
            self.populate_table(tree, df)

    def populate_table(self, tree: ttk.Treeview, df: pd.DataFrame) -> None:
        tree.delete(*tree.get_children())
        columns = [str(col) for col in df.columns]
        tree["columns"] = columns

        for col in columns:
            tree.heading(col, text=col)
            tree.column(col, width=self.guess_column_width(col), minwidth=80, stretch=True)

        for _, row in df.iterrows():
            values = [self.format_cell(row[col]) for col in df.columns]
            tree.insert("", END, values=values)

    def guess_column_width(self, col: str) -> int:
        if "Title" in col:
            return 340
        if "Key" in col or "Author" in col or "Commenter" in col:
            return 180
        if "At" in col:
            return 150
        if "Count" in col:
            return 110
        return 130

    def format_cell(self, value: object) -> str:
        if pd.isna(value):
            return ""
        if isinstance(value, pd.Timestamp):
            return value.strftime("%Y-%m-%d %H:%M:%S")
        if isinstance(value, float):
            return f"{value:.3f}".rstrip("0").rstrip(".")
        text = str(value)
        return text.replace("\n", " ")[:500]

    def log(self, message: str) -> None:
        self.log_text.insert("", END, values=(message,))
        children = self.log_text.get_children()
        if children:
            self.log_text.see(children[-1])

    def open_output_dir(self) -> None:
        self.open_path(Path(self.output_dir_var.get().strip()))

    def open_output_file(self, filename: str) -> None:
        path = Path(self.output_dir_var.get().strip()) / filename
        if not path.exists():
            messagebox.showwarning("파일 없음", f"아직 파일이 없습니다.\n{path}")
            return
        self.open_path(path)

    def open_path(self, path: Path) -> None:
        try:
            os.startfile(path)  # type: ignore[attr-defined]
        except Exception as exc:
            messagebox.showerror("열기 실패", str(exc))


def main() -> None:
    root = Tk()
    AnalysisApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
