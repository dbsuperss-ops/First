"""YAML 설정 파일을 파싱하여 데이터클래스로 반환합니다."""

import yaml
from dataclasses import dataclass
from typing import Optional, List


@dataclass
class FormatConfig:
    font_name: str
    font_size_pt: float
    bold: bool
    color_hex: str
    align: str
    line_spacing: float
    char_spacing: float
    bg_color_hex: Optional[str]
    border_color_hex: Optional[str]
    border_width_pt: float


@dataclass
class ShapeConfig:
    shape_id: int
    name: str
    include: bool


@dataclass
class SlideConfig:
    slide_index: int  # 1-based (config 기준)
    shapes: List[ShapeConfig]


@dataclass
class FileConfig:
    path: str
    slides: List[SlideConfig]


@dataclass
class AppConfig:
    output_path: str
    format: FormatConfig
    files: List[FileConfig]


def load_config(path: str) -> AppConfig:
    with open(path, encoding="utf-8") as f:
        raw = yaml.safe_load(f)

    fmt = raw["format"]
    format_cfg = FormatConfig(
        font_name=fmt.get("font_name", "맑은 고딕"),
        font_size_pt=float(fmt.get("font_size_pt", 11.0)),
        bold=bool(fmt.get("bold", False)),
        color_hex=fmt.get("color_hex", "#000000"),
        align=fmt.get("align", "left"),
        line_spacing=float(fmt.get("line_spacing", 1.0)),
        char_spacing=float(fmt.get("char_spacing", 0.0)),
        bg_color_hex=fmt.get("bg_color_hex"),
        border_color_hex=fmt.get("border_color_hex"),
        border_width_pt=float(fmt.get("border_width_pt", 0.0)),
    )

    files = []
    for f_raw in raw.get("files", []):
        slides = []
        for s_raw in f_raw.get("slides", []):
            shapes = []
            for sh_raw in s_raw.get("shapes", []):
                shapes.append(ShapeConfig(
                    shape_id=int(sh_raw["shape_id"]),
                    name=sh_raw.get("name", ""),
                    include=bool(sh_raw.get("include", True)),
                ))
            slides.append(SlideConfig(
                slide_index=int(s_raw["slide_index"]),
                shapes=shapes,
            ))
        files.append(FileConfig(
            path=f_raw["path"],
            slides=slides,
        ))

    return AppConfig(
        output_path=raw["output_path"],
        format=format_cfg,
        files=files,
    )
