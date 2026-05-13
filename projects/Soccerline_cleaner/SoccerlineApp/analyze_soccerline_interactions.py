"""
Soccerline 게시글/댓글 상호작용 분석 스크립트.

기능:
- 여러 Excel export 파일의 Posts/Comments 시트를 통합
- PostId 기준 게시글-댓글 병합
- Title/Body 키워드 반복 작성자 식별
- 게시글 작성 시각과 댓글 작성 시각의 시간차 계산
- 작성자-댓글러 상호작용 빈도 및 5분 이내 집중 댓글 패턴 집계
- DataFrame/CSV 출력 및 히트맵/네트워크 그래프 저장

필요 패키지:
    pip install pandas openpyxl matplotlib seaborn networkx

실행 예:
    python analyze_soccerline_interactions.py
    python analyze_soccerline_interactions.py --keywords 이재명 윤석열 한동훈 --min-posts 3 --window-minutes 5
"""

from __future__ import annotations

import argparse
import os
import re
from pathlib import Path
from typing import Iterable

# 일부 샌드박스/자동화 환경에서는 WINDIR가 비어 있어 matplotlib의 Windows
# 폰트 탐색이 실패할 수 있다.
os.environ.setdefault("WINDIR", r"C:\Windows")
os.environ.setdefault("SystemRoot", r"C:\Windows")
os.environ.setdefault("MPLBACKEND", "Agg")

import matplotlib.pyplot as plt
import networkx as nx
import pandas as pd
import seaborn as sns


DEFAULT_FILES: list[Path] = []

DEFAULT_KEYWORDS = [
    # 필요에 맞게 수정하거나 실행 시 --keywords 로 넘겨 쓰세요.
    "이재명",
    "윤석열",
    "한동훈",
    "문재인",
    "김건희",
    "대선",
    "민주당",
    "국민의힘",
]


def setup_korean_font() -> None:
    """Windows/macOS/Linux에서 가능한 한글 폰트를 선택한다."""
    import matplotlib.font_manager as fm

    preferred_fonts = [
        "Malgun Gothic",
        "AppleGothic",
        "NanumGothic",
        "Noto Sans CJK KR",
        "Noto Sans KR",
        "DejaVu Sans",
    ]
    installed = {font.name for font in fm.fontManager.ttflist}
    for font_name in preferred_fonts:
        if font_name in installed:
            plt.rcParams["font.family"] = font_name
            break

    # 마이너스 기호 깨짐 방지
    plt.rcParams["axes.unicode_minus"] = False


def read_exports(file_paths: Iterable[Path]) -> tuple[pd.DataFrame, pd.DataFrame]:
    """Excel export 목록에서 Posts/Comments 시트를 읽어 합친다."""
    posts_frames: list[pd.DataFrame] = []
    comments_frames: list[pd.DataFrame] = []

    for path in file_paths:
        if not path.exists():
            raise FileNotFoundError(f"파일을 찾을 수 없습니다: {path}")

        posts = pd.read_excel(path, sheet_name="Posts", engine="openpyxl")
        comments = pd.read_excel(path, sheet_name="Comments", engine="openpyxl")
        posts["SourceFile"] = path.name
        comments["SourceFile"] = path.name
        posts_frames.append(posts)
        comments_frames.append(comments)

    posts_all = pd.concat(posts_frames, ignore_index=True)
    comments_all = pd.concat(comments_frames, ignore_index=True)

    posts_all = normalize_posts(posts_all)
    comments_all = normalize_comments(comments_all)

    # 여러 export에 같은 글/댓글이 중복 포함될 수 있으므로 중복 제거
    posts_all = posts_all.drop_duplicates(subset=["PostId"], keep="last")
    comments_all = comments_all.drop_duplicates(
        subset=["PostId", "CommentOrder", "CommentCreatedAt", "CommenterKey", "CommentContent"],
        keep="last",
    )

    return posts_all, comments_all


def normalize_posts(posts: pd.DataFrame) -> pd.DataFrame:
    posts = posts.copy()
    required = ["PostId", "CreatedAt", "Title", "Author", "AuthorIp", "Body"]
    missing = [col for col in required if col not in posts.columns]
    if missing:
        raise ValueError(f"Posts 시트에 필요한 컬럼이 없습니다: {missing}")

    posts["PostId"] = posts["PostId"].astype("Int64")
    posts["PostCreatedAt"] = parse_datetime_series(posts["CreatedAt"])
    posts["Title"] = posts["Title"].fillna("").astype(str)
    posts["Body"] = posts["Body"].fillna("").astype(str)
    posts["Author"] = posts["Author"].fillna("").astype(str).str.strip()
    posts["AuthorIp"] = posts["AuthorIp"].fillna("").astype(str).str.strip()
    posts["AuthorId"] = posts.get("AuthorId", "").fillna("").astype(str).str.strip()
    posts["PostText"] = (posts["Title"] + "\n" + posts["Body"]).str.strip()
    posts["PostAuthorKey"] = posts.apply(
        lambda row: make_user_key(row["Author"], row["AuthorId"], row["AuthorIp"]),
        axis=1,
    )
    return posts


def normalize_comments(comments: pd.DataFrame) -> pd.DataFrame:
    comments = comments.copy()
    required = ["PostId", "Nickname", "AuthorIp", "CreatedAt", "Content"]
    missing = [col for col in required if col not in comments.columns]
    if missing:
        raise ValueError(f"Comments 시트에 필요한 컬럼이 없습니다: {missing}")

    parsed = comments["Nickname"].fillna("").astype(str).apply(parse_comment_nickname)
    parsed_df = pd.DataFrame(parsed.tolist(), index=comments.index)

    comments["PostId"] = comments["PostId"].astype("Int64")
    comments["CommentOrder"] = comments.get("Order", pd.NA)
    comments["CommentCreatedAt"] = parse_datetime_series(comments["CreatedAt"])
    comments["CommentContent"] = comments["Content"].fillna("").astype(str)
    comments["CommentNicknameRaw"] = comments["Nickname"].fillna("").astype(str).str.strip()
    comments["CommentNickname"] = parsed_df["nickname"].fillna("").astype(str).str.strip()
    comments["CommentUserIdParsed"] = parsed_df["user_id"].fillna("").astype(str).str.strip()
    comments["CommentIpParsed"] = parsed_df["ip"].fillna("").astype(str).str.strip()
    comments["CommentAuthorIp"] = comments["AuthorIp"].fillna("").astype(str).str.strip()
    comments["CommentUserId"] = comments.get("UserID", "").fillna("").astype(str).str.strip()
    comments["CommenterKey"] = comments.apply(
        lambda row: make_user_key(
            row["CommentNickname"] or row["CommentNicknameRaw"],
            row["CommentUserId"] or row["CommentUserIdParsed"],
            row["CommentAuthorIp"] or row["CommentIpParsed"],
        ),
        axis=1,
    )
    return comments


def parse_datetime_series(series: pd.Series) -> pd.Series:
    """날짜만 있는 값과 초 단위 시간이 있는 값이 섞인 컬럼을 안정적으로 파싱한다."""
    try:
        return pd.to_datetime(series, errors="coerce", format="mixed")
    except (TypeError, ValueError):
        return pd.to_datetime(series, errors="coerce")


def parse_comment_nickname(raw: str) -> dict[str, str]:
    """
    '닉네임(userid, 123.***.***.1)' 형태에서 닉네임/아이디/IP를 추출한다.
    형식이 다르면 원문을 닉네임으로 사용한다.
    """
    value = str(raw).strip()
    match = re.match(r"^(?P<nickname>.*?)\((?P<inside>.*)\)$", value)
    if not match:
        return {"nickname": value, "user_id": "", "ip": ""}

    nickname = match.group("nickname").strip()
    inside = match.group("inside").strip()
    parts = [part.strip() for part in inside.split(",")]
    user_id = parts[0] if parts else ""
    ip = parts[-1] if len(parts) >= 2 else ""
    return {"nickname": nickname, "user_id": user_id, "ip": ip}


def make_user_key(nickname: object, user_id: object = "", ip: object = "") -> str:
    """작성자 식별 키. ID가 있으면 우선 사용하고, 없으면 닉네임/IP를 조합한다."""
    nickname_s = clean_scalar(nickname)
    user_id_s = clean_scalar(user_id)
    ip_s = clean_scalar(ip)

    if user_id_s:
        return f"id:{user_id_s}"
    if nickname_s and ip_s:
        return f"nameip:{nickname_s}|{ip_s}"
    if nickname_s:
        return f"name:{nickname_s}"
    if ip_s:
        return f"ip:{ip_s}"
    return "unknown"


def clean_scalar(value: object) -> str:
    if pd.isna(value):
        return ""
    value_s = str(value).strip()
    if value_s.lower() in {"nan", "none", "<na>"}:
        return ""
    return value_s


def build_keyword_pattern(keywords: list[str]) -> re.Pattern[str]:
    escaped = [re.escape(keyword.strip()) for keyword in keywords if keyword.strip()]
    if not escaped:
        raise ValueError("키워드는 1개 이상 필요합니다.")
    return re.compile("|".join(escaped), flags=re.IGNORECASE)


def tag_keywords(text: str, keywords: list[str]) -> list[str]:
    found = []
    text_s = str(text)
    for keyword in keywords:
        if keyword and re.search(re.escape(keyword), text_s, flags=re.IGNORECASE):
            found.append(keyword)
    return found


def analyze(
    posts: pd.DataFrame,
    comments: pd.DataFrame,
    keywords: list[str],
    min_posts: int,
    window_minutes: int,
    top_n: int,
) -> dict[str, pd.DataFrame]:
    keyword_pattern = build_keyword_pattern(keywords)

    filtered_posts = posts[
        posts["PostText"].str.contains(keyword_pattern, na=False)
    ].copy()
    filtered_posts["MatchedKeywords"] = filtered_posts["PostText"].apply(
        lambda text: ", ".join(tag_keywords(text, keywords))
    )

    repeated_authors = (
        filtered_posts.groupby("PostAuthorKey")
        .agg(
            KeywordPostCount=("PostId", "nunique"),
            FirstPostAt=("PostCreatedAt", "min"),
            LastPostAt=("PostCreatedAt", "max"),
            AuthorNames=("Author", lambda s: ", ".join(sorted(set(filter(None, s)))[:5])),
            AuthorIps=("AuthorIp", lambda s: ", ".join(sorted(set(filter(None, s)))[:5])),
            Keywords=("MatchedKeywords", lambda s: ", ".join(sorted(set(", ".join(s).split(", "))))),
        )
        .reset_index()
        .query("KeywordPostCount >= @min_posts")
        .sort_values(["KeywordPostCount", "LastPostAt"], ascending=[False, False])
    )

    target_posts = filtered_posts[
        filtered_posts["PostAuthorKey"].isin(repeated_authors["PostAuthorKey"])
    ].copy()

    merged = target_posts.merge(
        comments,
        on="PostId",
        how="left",
        suffixes=("_Post", "_Comment"),
    )
    merged["TimeDelta"] = merged["CommentCreatedAt"] - merged["PostCreatedAt"]
    merged["TimeDeltaMinutes"] = merged["TimeDelta"].dt.total_seconds() / 60
    merged["WithinWindow"] = merged["TimeDeltaMinutes"].between(0, window_minutes, inclusive="both")
    merged["SelfComment"] = merged["PostAuthorKey"] == merged["CommenterKey"]

    interaction_summary = (
        merged.dropna(subset=["CommentCreatedAt"])
        .groupby(["PostAuthorKey", "CommenterKey"])
        .agg(
            InteractionCount=("PostId", "count"),
            UniquePostsCommented=("PostId", "nunique"),
            WithinWindowCount=("WithinWindow", "sum"),
            MedianDeltaMinutes=("TimeDeltaMinutes", "median"),
            MinDeltaMinutes=("TimeDeltaMinutes", "min"),
            SelfCommentCount=("SelfComment", "sum"),
        )
        .reset_index()
        .sort_values(
            ["WithinWindowCount", "InteractionCount", "UniquePostsCommented"],
            ascending=[False, False, False],
        )
    )

    post_bursts = (
        merged.dropna(subset=["CommentCreatedAt"])
        .groupby(["PostId", "Title", "PostAuthorKey", "PostCreatedAt"])
        .agg(
            CommentCount=("CommenterKey", "count"),
            UniqueCommenters=("CommenterKey", "nunique"),
            WithinWindowComments=("WithinWindow", "sum"),
            FirstCommentAt=("CommentCreatedAt", "min"),
            LastCommentAt=("CommentCreatedAt", "max"),
            MinDeltaMinutes=("TimeDeltaMinutes", "min"),
            MedianDeltaMinutes=("TimeDeltaMinutes", "median"),
        )
        .reset_index()
    )
    post_bursts["BurstRatio"] = post_bursts["WithinWindowComments"] / post_bursts["CommentCount"]
    post_bursts = post_bursts.sort_values(
        ["WithinWindowComments", "BurstRatio", "CommentCount"],
        ascending=[False, False, False],
    )

    keyword_post_table = target_posts[
        [
            "PostId",
            "PostCreatedAt",
            "Title",
            "Author",
            "AuthorId",
            "AuthorIp",
            "PostAuthorKey",
            "MatchedKeywords",
            "Link",
        ]
    ].sort_values(["PostAuthorKey", "PostCreatedAt"])

    return {
        "repeated_authors": repeated_authors,
        "keyword_posts": keyword_post_table,
        "merged_time_deltas": merged,
        "interaction_summary": interaction_summary,
        "post_bursts": post_bursts,
        "top_interactions": interaction_summary.head(top_n),
        "top_bursts": post_bursts.head(top_n),
    }


def save_outputs(results: dict[str, pd.DataFrame], output_dir: Path) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    for name, df in results.items():
        # 전체 병합 데이터는 컬럼이 많으므로 CSV로만 저장해도 충분하다.
        df.to_csv(output_dir / f"{name}.csv", index=False, encoding="utf-8-sig")


def plot_heatmap(interactions: pd.DataFrame, output_dir: Path, top_n: int) -> None:
    if interactions.empty:
        print("[시각화 생략] interaction_summary가 비어 있습니다.")
        return

    top_pairs = interactions.head(top_n)
    matrix = top_pairs.pivot_table(
        index="PostAuthorKey",
        columns="CommenterKey",
        values="InteractionCount",
        aggfunc="sum",
        fill_value=0,
    ).astype(float)

    width = max(10, min(24, 0.45 * len(matrix.columns) + 4))
    height = max(6, min(18, 0.45 * len(matrix.index) + 4))

    plt.figure(figsize=(width, height))
    sns.heatmap(matrix, annot=True, fmt=".0f", cmap="YlOrRd", linewidths=0.5)
    plt.title("게시글 작성자-댓글 작성자 상호작용 히트맵")
    plt.xlabel("댓글 작성자")
    plt.ylabel("게시글 작성자")
    plt.tight_layout()
    plt.savefig(output_dir / "interaction_heatmap.png", dpi=180)
    plt.close()


def plot_network(interactions: pd.DataFrame, output_dir: Path, top_n: int) -> None:
    if interactions.empty:
        print("[시각화 생략] interaction_summary가 비어 있습니다.")
        return

    graph = nx.DiGraph()
    for row in interactions.head(top_n).itertuples(index=False):
        graph.add_edge(
            row.PostAuthorKey,
            row.CommenterKey,
            weight=float(row.InteractionCount),
            within_window=float(row.WithinWindowCount),
        )

    weights = [graph[u][v]["weight"] for u, v in graph.edges()]
    edge_widths = [1.0 + weight * 0.35 for weight in weights]
    node_sizes = [
        800 + 180 * (graph.in_degree(node, weight="weight") + graph.out_degree(node, weight="weight"))
        for node in graph.nodes()
    ]

    plt.figure(figsize=(14, 10))
    pos = nx.spring_layout(graph, seed=42, k=0.8)
    nx.draw_networkx_nodes(
        graph,
        pos,
        node_size=node_sizes,
        node_color="#BFE3C0",
        edgecolors="#2E5E3E",
        linewidths=1.2,
    )
    nx.draw_networkx_edges(
        graph,
        pos,
        width=edge_widths,
        alpha=0.55,
        arrows=True,
        arrowstyle="-|>",
        arrowsize=14,
        edge_color="#5B6C8A",
    )
    nx.draw_networkx_labels(graph, pos, font_size=9)
    edge_labels = {(u, v): int(data["weight"]) for u, v, data in graph.edges(data=True)}
    nx.draw_networkx_edge_labels(graph, pos, edge_labels=edge_labels, font_size=8)
    plt.title("상호작용 네트워크 그래프")
    plt.axis("off")
    plt.tight_layout()
    plt.savefig(output_dir / "interaction_network.png", dpi=180)
    plt.close()


def print_tables(results: dict[str, pd.DataFrame], top_n: int) -> None:
    pd.set_option("display.max_columns", 40)
    pd.set_option("display.width", 220)
    pd.set_option("display.max_colwidth", 80)

    print("\n=== 반복 키워드 게시글 작성자 ===")
    print(results["repeated_authors"].head(top_n).to_string(index=False))

    print("\n=== 상호작용 빈도 Top ===")
    print(results["top_interactions"].to_string(index=False))

    print("\n=== 글 작성 후 단시간 집중 댓글 패턴 Top ===")
    burst_cols = [
        "PostId",
        "Title",
        "PostAuthorKey",
        "PostCreatedAt",
        "CommentCount",
        "UniqueCommenters",
        "WithinWindowComments",
        "BurstRatio",
        "MinDeltaMinutes",
        "MedianDeltaMinutes",
    ]
    print(results["top_bursts"][burst_cols].to_string(index=False))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Soccerline 게시글/댓글 상호작용 분석")
    parser.add_argument(
        "--files",
        nargs="+",
        type=Path,
        required=True,
        help="분석할 Excel export 파일 경로 목록",
    )
    parser.add_argument(
        "--keywords",
        nargs="+",
        default=DEFAULT_KEYWORDS,
        help="Title/Body에서 찾을 키워드 목록",
    )
    parser.add_argument(
        "--min-posts",
        type=int,
        default=2,
        help="반복 작성자로 볼 최소 키워드 게시글 수",
    )
    parser.add_argument(
        "--window-minutes",
        type=int,
        default=5,
        help="비정상적으로 짧은 댓글 집중 구간 기준(분)",
    )
    parser.add_argument(
        "--top-n",
        type=int,
        default=30,
        help="콘솔 출력 및 시각화에 사용할 상위 행 수",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path("analysis_outputs"),
        help="CSV/PNG 저장 폴더",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    setup_korean_font()

    posts, comments = read_exports(args.files)
    print(f"Posts: {len(posts):,} rows, Comments: {len(comments):,} rows")
    print(f"기간: {posts['PostCreatedAt'].min()} ~ {posts['PostCreatedAt'].max()}")
    print(f"키워드: {', '.join(args.keywords)}")

    results = analyze(
        posts=posts,
        comments=comments,
        keywords=args.keywords,
        min_posts=args.min_posts,
        window_minutes=args.window_minutes,
        top_n=args.top_n,
    )

    print_tables(results, args.top_n)
    save_outputs(results, args.output_dir)
    plot_heatmap(results["interaction_summary"], args.output_dir, args.top_n)
    plot_network(results["interaction_summary"], args.output_dir, args.top_n)

    print(f"\nCSV/PNG 결과 저장 위치: {args.output_dir.resolve()}")


if __name__ == "__main__":
    main()
