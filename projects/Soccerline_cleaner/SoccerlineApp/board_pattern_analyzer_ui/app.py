from __future__ import annotations

import json
import os
import queue
import subprocess
import sys
import threading
import traceback
from pathlib import Path
from tkinter import filedialog, messagebox

import customtkinter as ctk

try:
    from core.board_pattern_analyzer import analyze_board, load_or_generate_salt, regenerate_salt
except ModuleNotFoundError:
    from board_pattern_analyzer_ui.core.board_pattern_analyzer import (
        analyze_board,
        load_or_generate_salt,
        regenerate_salt,
    )


if getattr(sys, "frozen", False):
    APP_DIR = Path(sys.executable).resolve().parent
    RESOURCE_DIR = Path(getattr(sys, "_MEIPASS", APP_DIR))
else:
    APP_DIR = Path(__file__).resolve().parent
    RESOURCE_DIR = APP_DIR

CONFIG_PATH = APP_DIR / "app_config.json"
DEFAULT_OUTPUT = APP_DIR / "output" / "board_analysis_report.xlsx"
DEFAULT_TOPICS = RESOURCE_DIR / "sample_topics.json"
DEFAULT_INPUTS: list[Path] = []


class BoardPatternAnalyzerApp(ctk.CTk):
    def __init__(self) -> None:
        super().__init__()
        self.title("DataForge Board Pattern Analyzer")
        self.geometry("1160x760")
        self.minsize(980, 680)

        ctk.set_appearance_mode("light")
        ctk.set_default_color_theme("blue")

        self.worker: threading.Thread | None = None
        self.messages: queue.Queue[tuple[str, str]] = queue.Queue()

        config = self.load_config()
        saved_inputs = [Path(path) for path in config.get("input_paths", []) if Path(path).exists()]
        self.input_paths: list[Path] = saved_inputs or [path for path in DEFAULT_INPUTS if path.exists()]

        self.output_path_var = ctk.StringVar(value=config.get("output_path") or str(DEFAULT_OUTPUT))
        self.topics_path_var = ctk.StringVar(value=config.get("topics_path") or str(DEFAULT_TOPICS))
        self.extra_keywords_var = ctk.StringVar(value=config.get("extra_keywords") or "")
        self.keyword_mode_var = ctk.StringVar(value=config.get("keyword_mode") or "OR")
        self.target_authors_var = ctk.StringVar(value=config.get("target_authors") or "")
        self.min_comments_var = ctk.StringVar(value=str(config.get("min_comments") or 3))
        self.fast_minutes_var = ctk.StringVar(value=str(config.get("fast_minutes") or 30))
        self.top_keywords_var = ctk.StringVar(value=str(config.get("top_keywords") or 30))
        self.anonymize_var = ctk.BooleanVar(value=config.get("anonymization_enabled", True))
        self.status_var = ctk.StringVar(value="Ready")

        self.configure_grid()
        self.build_ui()
        self.refresh_input_list()
        self.after(120, self.poll_messages)

    def configure_grid(self) -> None:
        self.configure(fg_color="#f7f9fb")
        self.grid_columnconfigure(1, weight=1)
        self.grid_rowconfigure(0, weight=1)

    def build_ui(self) -> None:
        self.sidebar = ctk.CTkFrame(self, width=240, fg_color="#f2f4f6", corner_radius=0)
        self.sidebar.grid(row=0, column=0, sticky="nsew")
        self.sidebar.grid_propagate(False)
        self.sidebar.grid_rowconfigure(6, weight=1)

        ctk.CTkLabel(
            self.sidebar,
            text="DataForge",
            text_color="#0f172a",
            font=ctk.CTkFont(size=24, weight="bold"),
        ).grid(row=0, column=0, sticky="w", padx=24, pady=(28, 6))
        ctk.CTkLabel(
            self.sidebar,
            text="Board analytics",
            text_color="#64748b",
            font=ctk.CTkFont(size=13),
        ).grid(row=1, column=0, sticky="w", padx=24, pady=(0, 24))

        ctk.CTkButton(
            self.sidebar,
            text="Clear Source Files",
            fg_color="#0f172a",
            hover_color="#1e293b",
            corner_radius=4,
            command=self.button_action("Clear Source Files", self.clear_inputs),
        ).grid(row=2, column=0, sticky="ew", padx=24, pady=(8, 4))

        ctk.CTkButton(
            self.sidebar,
            text="Load Default Files",
            fg_color="#0f172a",
            hover_color="#1e293b",
            corner_radius=4,
            command=self.button_action("Load Default Files", self.load_default_inputs),
        ).grid(row=3, column=0, sticky="ew", padx=24, pady=(8, 4))

        ctk.CTkButton(
            self.sidebar,
            text="Open Output Folder",
            fg_color="#475569",
            hover_color="#334155",
            corner_radius=4,
            command=self.button_action("Open Output Folder", self.open_output_folder),
        ).grid(row=4, column=0, sticky="ew", padx=24, pady=(8, 12))

        # --- Anonymization controls ---
        anon_frame = ctk.CTkFrame(self.sidebar, fg_color="#f2f4f6")
        anon_frame.grid(row=5, column=0, sticky="new", padx=24, pady=(8, 4))
        ctk.CTkLabel(
            anon_frame,
            text="Privacy",
            text_color="#0f172a",
            font=ctk.CTkFont(size=14, weight="bold"),
        ).pack(anchor="w", pady=(0, 6))
        self.anon_checkbox = ctk.CTkCheckBox(
            anon_frame,
            text="Anonymization Mode",
            variable=self.anonymize_var,
            command=self.on_anonymize_toggle,
        )
        self.anon_checkbox.pack(anchor="w")
        ctk.CTkButton(
            anon_frame,
            text="Regenerate Salt",
            fg_color="#475569",
            hover_color="#334155",
            corner_radius=4,
            height=28,
            command=self.button_action("Regenerate Salt", self.on_regenerate_salt),
        ).pack(anchor="w", pady=(8, 0))

        main = ctk.CTkFrame(self, fg_color="#f7f9fb", corner_radius=0)
        main.grid(row=0, column=1, sticky="nsew")
        main.grid_columnconfigure(0, weight=1)
        main.grid_rowconfigure(4, weight=1)

        self.build_top_nav(main)
        self.build_file_section(main)
        self.build_options_section(main)
        self.build_action_section(main)
        self.build_log_section(main)

    def build_top_nav(self, parent: ctk.CTkFrame) -> None:
        top = ctk.CTkFrame(parent, fg_color="#f7f9fb", height=64, corner_radius=0)
        top.grid(row=0, column=0, sticky="ew", padx=32, pady=(24, 8))
        top.grid_columnconfigure(0, weight=1)
        ctk.CTkLabel(
            top,
            text="Dashboard",
            text_color="#0f172a",
            font=ctk.CTkFont(size=24, weight="bold"),
        ).grid(row=0, column=0, sticky="w")
        ctk.CTkLabel(top, textvariable=self.status_var, text_color="#64748b").grid(
            row=0,
            column=1,
            sticky="e",
        )

    def build_file_section(self, parent: ctk.CTkFrame) -> None:
        grid = ctk.CTkFrame(parent, fg_color="#f7f9fb")
        grid.grid(row=1, column=0, sticky="ew", padx=32, pady=8)
        grid.grid_columnconfigure(0, weight=1)
        grid.grid_columnconfigure(1, weight=1)

        file_card = self.card(grid, "File Selection")
        file_card.grid(row=0, column=0, sticky="nsew", padx=(0, 12))
        file_card.grid_columnconfigure(0, weight=1)

        ctk.CTkLabel(
            file_card,
            text="Select Excel exports. Each file must contain Posts and Comments sheets.",
            text_color="#64748b",
            anchor="w",
        ).grid(row=1, column=0, sticky="ew", padx=16, pady=(0, 8))

        self.input_list = ctk.CTkTextbox(file_card, height=94, fg_color="#ffffff", text_color="#1e293b")
        self.input_list.grid(row=2, column=0, sticky="ew", padx=16, pady=(0, 12))
        self.input_list.configure(state="disabled")

        ctk.CTkButton(
            file_card,
            text="Browse Excel Files",
            fg_color="#0f172a",
            hover_color="#1e293b",
            corner_radius=4,
            command=self.button_action("Browse Excel Files", self.choose_input_files),
        ).grid(row=3, column=0, sticky="ew", padx=16, pady=(0, 16))

        storage_card = self.card(grid, "Storage Settings")
        storage_card.grid(row=0, column=1, sticky="nsew", padx=(12, 0))
        storage_card.grid_columnconfigure(0, weight=1)

        self.path_entry(storage_card, 1, "Output Excel Path", self.output_path_var, self.choose_output_path)
        self.path_entry(storage_card, 2, "Topic Keyword JSON", self.topics_path_var, self.choose_topics_path)

    def build_options_section(self, parent: ctk.CTkFrame) -> None:
        card = self.card(parent, "Analysis Options")
        card.grid(row=2, column=0, sticky="ew", padx=32, pady=8)
        for i in range(4):
            card.grid_columnconfigure(i, weight=1)

        self.option_entry(
            card,
            1,
            0,
            "Target user",
            self.target_authors_var,
            "Author, AuthorId, or IP. Empty = all users.",
        )
        self.option_entry(card, 1, 1, "Minimum comments", self.min_comments_var, "Default 3")
        self.option_entry(card, 1, 2, "Fast comment minutes", self.fast_minutes_var, "Default 30")
        self.option_entry(card, 1, 3, "Top rows", self.top_keywords_var, "Default 30")

        keyword_frame = ctk.CTkFrame(card, fg_color="#ffffff")
        keyword_frame.grid(row=2, column=0, columnspan=4, sticky="ew", padx=16, pady=(0, 16))
        keyword_frame.grid_columnconfigure(0, weight=1)
        ctk.CTkLabel(
            keyword_frame,
            text="Additional keywords",
            text_color="#64748b",
        ).grid(row=0, column=0, sticky="w")
        ctk.CTkEntry(
            keyword_frame,
            textvariable=self.extra_keywords_var,
            placeholder_text="쉼표로 구분해 추가하세요. 예: 이재명, 윤석열, 한동훈",
            fg_color="#f7f9fb",
            border_color="#e2e8f0",
        ).grid(row=1, column=0, sticky="ew", pady=(4, 0))
        ctk.CTkLabel(
            keyword_frame,
            text="JSON 키워드에 이 입력값을 추가로 합쳐서 분석합니다.",
            text_color="#64748b",
            font=ctk.CTkFont(size=12),
        ).grid(row=2, column=0, sticky="w", pady=(4, 0))

        mode_frame = ctk.CTkFrame(card, fg_color="#ffffff")
        mode_frame.grid(row=3, column=0, columnspan=4, sticky="ew", padx=16, pady=(0, 16))
        mode_frame.grid_columnconfigure(1, weight=1)
        ctk.CTkLabel(
            mode_frame,
            text="Keyword match mode",
            text_color="#64748b",
        ).grid(row=0, column=0, sticky="w", padx=(0, 12))
        ctk.CTkSegmentedButton(
            mode_frame,
            values=["OR", "AND"],
            variable=self.keyword_mode_var,
            command=lambda _: self.save_config(),
        ).grid(row=0, column=1, sticky="w")
        ctk.CTkLabel(
            mode_frame,
            text="OR: 하나라도 포함 / AND: 모든 키워드 포함",
            text_color="#64748b",
            font=ctk.CTkFont(size=12),
        ).grid(row=1, column=0, columnspan=2, sticky="w", pady=(6, 0))

    def build_action_section(self, parent: ctk.CTkFrame) -> None:
        hero = ctk.CTkFrame(parent, fg_color="#ffffff", border_color="#e2e8f0", border_width=1, corner_radius=8)
        hero.grid(row=3, column=0, sticky="ew", padx=32, pady=8)
        hero.grid_columnconfigure(0, weight=1)

        ctk.CTkLabel(
            hero,
            text="Start board pattern report",
            text_color="#0f172a",
            font=ctk.CTkFont(size=18, weight="bold"),
        ).grid(row=0, column=0, sticky="w", padx=24, pady=(22, 4))
        ctk.CTkLabel(
            hero,
            text="Outputs keyword share by user, activity time, commenter activity, time correlation, and target daily post counts.",
            text_color="#64748b",
        ).grid(row=1, column=0, sticky="w", padx=24, pady=(0, 18))

        buttons = ctk.CTkFrame(hero, fg_color="#ffffff")
        buttons.grid(row=0, column=1, rowspan=2, sticky="e", padx=24, pady=20)
        self.run_button = ctk.CTkButton(
            buttons,
            text="Run Analysis",
            width=160,
            height=42,
            fg_color="#0f172a",
            hover_color="#1e293b",
            corner_radius=4,
            command=self.button_action("Run Analysis", self.run_analysis),
        )
        self.run_button.grid(row=0, column=0, padx=(0, 8))
        ctk.CTkButton(
            buttons,
            text="Open Report",
            width=120,
            height=42,
            fg_color="#475569",
            hover_color="#334155",
            corner_radius=4,
            command=self.button_action("Open Report", self.open_output_file),
        ).grid(row=0, column=1, padx=4)
        ctk.CTkButton(
            buttons,
            text="Open Folder",
            width=120,
            height=42,
            fg_color="#475569",
            hover_color="#334155",
            corner_radius=4,
            command=self.button_action("Open Folder", self.open_output_folder),
        ).grid(row=0, column=2, padx=(4, 0))

    def build_log_section(self, parent: ctk.CTkFrame) -> None:
        log_card = self.card(parent, "System Status Console")
        log_card.grid(row=4, column=0, sticky="nsew", padx=32, pady=(8, 32))
        log_card.grid_columnconfigure(0, weight=1)
        log_card.grid_rowconfigure(1, weight=1)

        self.log_box = ctk.CTkTextbox(
            log_card,
            fg_color="#0f172a",
            text_color="#dbeafe",
            height=260,
            wrap="word",
        )
        self.log_box.grid(row=1, column=0, sticky="nsew", padx=16, pady=(0, 16))
        self.log_box.configure(state="disabled")

    def card(self, parent: ctk.CTkFrame, title: str) -> ctk.CTkFrame:
        frame = ctk.CTkFrame(parent, fg_color="#ffffff", border_color="#e2e8f0", border_width=1, corner_radius=8)
        ctk.CTkLabel(
            frame,
            text=title,
            text_color="#1e293b",
            font=ctk.CTkFont(size=18, weight="bold"),
        ).grid(row=0, column=0, sticky="w", padx=16, pady=(16, 10))
        return frame

    def path_entry(
        self,
        parent: ctk.CTkFrame,
        row: int,
        label: str,
        variable: ctk.StringVar,
        command,
    ) -> None:
        ctk.CTkLabel(parent, text=label, text_color="#64748b").grid(
            row=row * 2 - 1,
            column=0,
            sticky="w",
            padx=16,
            pady=(0, 4),
        )
        line = ctk.CTkFrame(parent, fg_color="#ffffff")
        line.grid(row=row * 2, column=0, sticky="ew", padx=16, pady=(0, 12))
        line.grid_columnconfigure(0, weight=1)
        ctk.CTkEntry(line, textvariable=variable, fg_color="#f7f9fb", border_color="#e2e8f0").grid(
            row=0,
            column=0,
            sticky="ew",
            padx=(0, 8),
        )
        ctk.CTkButton(
            line,
            text="Browse",
            width=90,
            corner_radius=4,
            command=self.button_action(f"Browse {label}", command),
        ).grid(row=0, column=1)

    def button_action(self, name: str, action):
        def wrapped() -> None:
            try:
                self.log(f"Button clicked: {name}")
                action()
            except Exception as exc:
                self.log(f"Button failed: {name} ({exc})")
                messagebox.showerror("Button failed", f"{name}\n\n{exc}")

        return wrapped

    def option_entry(
        self,
        parent: ctk.CTkFrame,
        row: int,
        column: int,
        label: str,
        variable: ctk.StringVar,
        placeholder: str,
    ) -> None:
        frame = ctk.CTkFrame(parent, fg_color="#ffffff")
        frame.grid(row=row, column=column, sticky="ew", padx=16 if column == 0 else 8, pady=(0, 16))
        ctk.CTkLabel(frame, text=label, text_color="#64748b").pack(anchor="w")
        ctk.CTkEntry(
            frame,
            textvariable=variable,
            placeholder_text=placeholder,
            fg_color="#f7f9fb",
            border_color="#e2e8f0",
        ).pack(fill="x", pady=(4, 0))

    def choose_input_files(self) -> None:
        paths = filedialog.askopenfilenames(
            title="Select source Excel files",
            filetypes=[("Excel files", "*.xlsx"), ("All files", "*.*")],
            initialdir=self.get_initial_dir("input"),
        )
        if not paths:
            return
        self.input_paths = [Path(path) for path in paths]
        self.refresh_input_list()
        self.save_config()

    def choose_output_path(self) -> None:
        current = Path(self.output_path_var.get()).expanduser()
        path = filedialog.asksaveasfilename(
            title="Choose output Excel report",
            defaultextension=".xlsx",
            filetypes=[("Excel files", "*.xlsx")],
            initialfile=current.name or "board_analysis_report.xlsx",
            initialdir=str(self.existing_dir(current.parent)),
        )
        if path:
            self.output_path_var.set(path)
            self.save_config()

    def choose_topics_path(self) -> None:
        path = filedialog.askopenfilename(
            title="Choose topic keyword JSON",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")],
            initialdir=self.get_initial_dir("topics"),
        )
        if path:
            self.topics_path_var.set(path)
            self.save_config()

    def get_initial_dir(self, kind: str) -> str:
        if kind == "input" and self.input_paths:
            return str(self.existing_dir(self.input_paths[0].parent))
        if kind == "topics":
            return str(self.existing_dir(Path(self.topics_path_var.get()).expanduser().parent))
        return str(self.existing_dir(APP_DIR))

    def existing_dir(self, path: Path) -> Path:
        path = path.expanduser()
        if path.exists() and path.is_dir():
            return path
        for parent in [path, *path.parents]:
            if parent.exists() and parent.is_dir():
                return parent
        return APP_DIR

    def refresh_input_list(self) -> None:
        self.input_list.configure(state="normal")
        self.input_list.delete("1.0", "end")
        if not self.input_paths:
            self.input_list.insert("end", "No files selected.\n")
        else:
            for idx, path in enumerate(self.input_paths, start=1):
                self.input_list.insert("end", f"{idx}. {path}\n")
        self.input_list.configure(state="disabled")

    def clear_inputs(self) -> None:
        self.input_paths = []
        self.refresh_input_list()
        self.clear_log()
        self.status_var.set("Ready")
        self.save_config()

    def on_anonymize_toggle(self) -> None:
        if not self.anonymize_var.get():
            confirmed = messagebox.askyesno(
                "익명화 해제 경고",
                "익명화를 끄면 결과 엑셀에 닉네임, ID, IP가 평문으로 포함됩니다.\n"
                "이 파일이 외부로 유출될 경우 게시판 이용자의 신상이 노출될 수 있습니다.\n\n"
                "그래도 진행하시겠습니까?",
            )
            if not confirmed:
                self.anonymize_var.set(True)
                return
        self.save_config()

    def on_regenerate_salt(self) -> None:
        confirmed = messagebox.askyesno(
            "솔트 재생성",
            "솔트를 재생성하면 이전 분석 결과와 해시가 달라집니다.\n진행하시겠습니까?",
        )
        if not confirmed:
            return
        regenerate_salt(CONFIG_PATH)
        self.log("Anonymization salt regenerated")
        messagebox.showinfo("완료", "새 솔트가 생성되었습니다.")

    def load_default_inputs(self) -> None:
        self.input_paths = [path for path in DEFAULT_INPUTS if path.exists()]
        self.refresh_input_list()
        self.save_config()
        if not self.input_paths:
            messagebox.showwarning("Default files missing", "Default source files were not found.")

    def run_analysis(self) -> None:
        if self.worker and self.worker.is_alive():
            messagebox.showinfo("Running", "Analysis is already running.")
            return
        try:
            input_paths = self.validate_inputs()
        except Exception as exc:
            messagebox.showerror("Input check", str(exc))
            return

        self.save_config()
        self.set_running(True)
        self.clear_log()
        self.log("Starting analysis")
        self.worker = threading.Thread(target=self.worker_run, args=(input_paths,), daemon=True)
        self.worker.start()

    def validate_inputs(self) -> list[Path]:
        if len(self.input_paths) < 1:
            raise ValueError("Select at least one source Excel file.")
        for path in self.input_paths:
            if not path.exists():
                raise FileNotFoundError(f"Input file was not found: {path}")
            if path.suffix.lower() != ".xlsx":
                raise ValueError("Only .xlsx files are supported.")

        output_path = Path(self.output_path_var.get()).expanduser()
        if output_path.suffix.lower() != ".xlsx":
            raise ValueError("Output path must end with .xlsx.")

        self.parse_positive_int(self.min_comments_var, "Minimum comments", 3)
        self.parse_positive_int(self.fast_minutes_var, "Fast comment minutes", 30)
        self.parse_positive_int(self.top_keywords_var, "Top rows", 30)
        self.keyword_mode_var.set("AND" if self.keyword_mode_var.get().upper() == "AND" else "OR")
        return self.input_paths[:]

    def parse_positive_int(self, variable: ctk.StringVar, label: str, default: int) -> int:
        raw = variable.get().strip()
        if not raw:
            variable.set(str(default))
            return default
        try:
            value = int(raw)
        except ValueError as exc:
            raise ValueError(f"{label} must be a number.") from exc
        if value < 1:
            raise ValueError(f"{label} must be at least 1.")
        variable.set(str(value))
        return value

    def worker_run(self, input_paths: list[Path]) -> None:
        try:
            anon_enabled = self.anonymize_var.get()
            salt = load_or_generate_salt(CONFIG_PATH) if anon_enabled else None
            analyze_board(
                input_path=input_paths,
                output_path=self.output_path_var.get(),
                topics_path=self.topics_path_var.get() or str(DEFAULT_TOPICS),
                extra_keywords=self.extra_keywords_var.get(),
                keyword_mode=self.keyword_mode_var.get(),
                target_authors=self.target_authors_var.get(),
                min_comments=self.parse_positive_int(self.min_comments_var, "Minimum comments", 3),
                fast_minutes=self.parse_positive_int(self.fast_minutes_var, "Fast comment minutes", 30),
                top_keywords=self.parse_positive_int(self.top_keywords_var, "Top rows", 30),
                anonymize=anon_enabled,
                salt=salt,
                progress=lambda msg: self.messages.put(("log", msg)),
            )
            self.messages.put(("done", self.output_path_var.get()))
        except Exception as exc:
            self.messages.put(("error", "".join(traceback.format_exception(exc))))

    def poll_messages(self) -> None:
        while True:
            try:
                kind, payload = self.messages.get_nowait()
            except queue.Empty:
                break

            if kind == "log":
                self.log(payload)
            elif kind == "done":
                self.log(f"Complete: {payload}")
                self.set_running(False)
                messagebox.showinfo("Complete", f"Report created:\n{payload}")
            elif kind == "error":
                self.log(payload)
                self.set_running(False)
                messagebox.showerror("Error", "Analysis failed. Check the console log.")

        self.after(120, self.poll_messages)

    def set_running(self, running: bool) -> None:
        self.run_button.configure(state="disabled" if running else "normal")
        self.status_var.set("Processing" if running else "Ready")

    def log(self, message: str) -> None:
        self.log_box.configure(state="normal")
        self.log_box.insert("end", f"> {message}\n")
        self.log_box.see("end")
        self.log_box.configure(state="disabled")

    def clear_log(self) -> None:
        self.log_box.configure(state="normal")
        self.log_box.delete("1.0", "end")
        self.log_box.configure(state="disabled")

    def open_output_file(self) -> None:
        path = Path(self.output_path_var.get()).expanduser()
        if not path.exists():
            messagebox.showwarning("Missing file", f"Report does not exist yet:\n{path}")
            return
        self.open_path(path)

    def open_output_folder(self) -> None:
        folder = Path(self.output_path_var.get()).expanduser().parent
        folder.mkdir(parents=True, exist_ok=True)
        self.open_path(folder)

    def open_path(self, path: Path) -> None:
        try:
            os.startfile(path)  # type: ignore[attr-defined]
            self.log(f"Opened: {path}")
        except Exception as exc:
            self.log(f"os.startfile failed: {path} ({exc})")
            try:
                if path.is_dir():
                    subprocess.Popen(["explorer", str(path)])
                else:
                    subprocess.Popen(["explorer", "/select,", str(path)])
                self.log(f"Opened with Explorer fallback: {path}")
            except Exception as fallback_exc:
                self.log(f"Open failed: {path} ({fallback_exc})")
                messagebox.showerror(
                    "Open failed",
                    f"Could not open:\n{path}\n\n{fallback_exc}",
                )

    def load_config(self) -> dict:
        if not CONFIG_PATH.exists():
            return {}
        try:
            with CONFIG_PATH.open("r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            return {}

    def save_config(self) -> None:
        data = {
            "input_paths": [str(path) for path in self.input_paths],
            "output_path": self.output_path_var.get(),
            "topics_path": self.topics_path_var.get(),
            "extra_keywords": self.extra_keywords_var.get(),
            "keyword_mode": self.keyword_mode_var.get(),
            "target_authors": self.target_authors_var.get(),
            "min_comments": self.min_comments_var.get(),
            "fast_minutes": self.fast_minutes_var.get(),
            "top_keywords": self.top_keywords_var.get(),
            "anonymization_enabled": self.anonymize_var.get(),
        }
        CONFIG_PATH.parent.mkdir(parents=True, exist_ok=True)
        with CONFIG_PATH.open("w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)


def main() -> None:
    if "--smoke-test" in sys.argv:
        return
    (APP_DIR / "input").mkdir(exist_ok=True)
    (APP_DIR / "output").mkdir(exist_ok=True)
    app = BoardPatternAnalyzerApp()
    app.mainloop()


if __name__ == "__main__":
    main()
