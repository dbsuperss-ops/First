from __future__ import annotations

import json
import re
import sys
from json import JSONDecodeError
from pathlib import Path
from typing import Callable, Iterable

import pandas as pd

ROOT_DIR = Path(__file__).resolve().parents[2]
if str(ROOT_DIR) not in sys.path:
    sys.path.insert(0, str(ROOT_DIR))

from analyze_soccerline_interactions import (  # noqa: E402
    DEFAULT_KEYWORDS,
    plot_heatmap,
    plot_network,
    read_exports,
    setup_korean_font,
)


ProgressCallback = Callable[[str], None]


def analyze_board(
    input_path: str | Path | Iterable[str | Path],
    output_path: str | Path,
    topics_path: str | Path | None = None,
    extra_keywords: str | Iterable[str] | None = None,
    keyword_mode: str = "OR",
    target_authors: str | Iterable[str] | None = None,
    min_comments: int = 3,
    fast_minutes: int = 30,
    top_keywords: int = 30,
    progress: ProgressCallback | None = None,
) -> dict[str, pd.DataFrame | Path]:
    """Analyze Posts/Comments sheets and create an Excel report.

    The report intentionally uses neutral wording such as repeated interaction,
    time overlap, fast comment ratio, and co-activity. It does not assert shared
    identity, collusion, or organized behavior.
    """
    output = Path(output_path).expanduser().resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    files = normalize_input_paths(input_path)
    target_tokens = normalize_tokens(target_authors)
    keyword_mode = normalize_keyword_mode(keyword_mode)
    log = progress or (lambda _: None)

    try:
        keywords = load_topics(topics_path)
    except (FileNotFoundError, ValueError) as exc:
        keywords = list(DEFAULT_KEYWORDS)
        log(f"Keyword JSON was ignored: {exc}")
        log("Using built-in default keywords")
    keywords = merge_keywords(keywords, normalize_tokens(extra_keywords))
    log(f"Active keyword count: {len(keywords)}")
    log(f"Keyword match mode: {keyword_mode}")

    log(f"Input files: {len(files)}")
    log("Reading Posts and Comments sheets")
    raw_summary = build_raw_source_summary(files)
    posts, comments = read_exports(files)
    source_summary = build_source_summary(posts, comments, raw_summary)
    log(f"Unique posts: {len(posts):,}, unique comments: {len(comments):,}")

    log("Calculating keyword share by user")
    keyword_share = build_keyword_share(posts, keywords, keyword_mode)

    log("Calculating post author activity time")
    author_activity = build_author_activity(posts, keywords, keyword_mode)
    author_hourly = build_author_hourly_activity(posts, keywords, keyword_mode)

    log("Calculating commenter activity time")
    merged_comments = build_merged_comments(posts, comments, fast_minutes)
    commenter_activity = build_commenter_activity(merged_comments)

    log("Calculating post/comment time correlation")
    time_correlation = build_time_correlation(posts, merged_comments, min_comments)
    interaction_summary = build_interaction_summary(merged_comments, min_comments)
    fast_patterns = build_fast_comment_patterns(merged_comments, min_comments, fast_minutes)
    target_daily_posts = build_target_daily_posts(posts, target_tokens)

    results: dict[str, pd.DataFrame | Path] = {
        "summary": build_run_summary(
            files=files,
            keywords=keywords,
            keyword_mode=keyword_mode,
            target_tokens=target_tokens,
            min_comments=min_comments,
            fast_minutes=fast_minutes,
            top_keywords=top_keywords,
        ),
        "report_guide": build_report_guide(),
        "source_summary": source_summary,
        "keyword_user_share": keyword_share.head(top_keywords),
        "all_keyword_user_share": keyword_share,
        "author_activity_time": author_activity,
        "author_hourly_activity": author_hourly,
        "commenter_activity_time": commenter_activity,
        "time_correlation": time_correlation,
        "interaction_summary": interaction_summary,
        "fast_comment_patterns": fast_patterns,
        "target_daily_posts": target_daily_posts,
        "merged_time_deltas": merged_comments,
    }

    log("Writing Excel report")
    write_excel_report(output, results)

    log("Creating visualizations")
    setup_korean_font()
    plot_heatmap(interaction_summary, output.parent, top_keywords)
    plot_network(interaction_summary, output.parent, top_keywords)

    log(f"Done: {output}")
    results["output_path"] = output
    return results


def load_topics(topics_path: str | Path | None) -> list[str]:
    if not topics_path:
        default_path = Path(__file__).resolve().parents[1] / "sample_topics.json"
        topics_path = default_path if default_path.exists() else None

    if not topics_path:
        return list(DEFAULT_KEYWORDS)

    path = Path(topics_path).expanduser()
    if not path.exists():
        raise FileNotFoundError(f"Keyword JSON file was not found: {path}")

    try:
        with path.open("r", encoding="utf-8") as f:
            data = json.load(f)
    except JSONDecodeError as exc:
        raise ValueError(f"Invalid or empty keyword JSON: {path}") from exc

    if isinstance(data, list):
        return sorted({str(item).strip() for item in data if str(item).strip()})

    if isinstance(data, dict):
        keywords: set[str] = set()
        for value in data.values():
            if isinstance(value, list):
                keywords.update(str(item).strip() for item in value if str(item).strip())
            elif isinstance(value, str) and value.strip():
                keywords.add(value.strip())
        return sorted(keywords)

    raise ValueError("Keyword JSON must be a list or an object containing keyword lists.")


def merge_keywords(base_keywords: list[str], extra_keywords: list[str]) -> list[str]:
    merged = []
    seen = set()
    for keyword in [*base_keywords, *extra_keywords]:
        keyword = str(keyword).strip()
        if not keyword:
            continue
        key = keyword.casefold()
        if key in seen:
            continue
        seen.add(key)
        merged.append(keyword)
    if not merged:
        return list(DEFAULT_KEYWORDS)
    return merged


def build_raw_source_summary(files: list[Path]) -> pd.DataFrame:
    rows = []
    for path in files:
        posts = pd.read_excel(path, sheet_name="Posts", usecols=["PostId"], engine="openpyxl")
        comments = pd.read_excel(
            path,
            sheet_name="Comments",
            usecols=["PostId", "Order", "CreatedAt", "Nickname", "Content"],
            engine="openpyxl",
        )
        rows.append(
            {
                "SourceFile": path.name,
                "RawPostRows": len(posts),
                "RawUniquePostIds": posts["PostId"].nunique(dropna=True),
                "RawCommentRows": len(comments),
            }
        )
    return pd.DataFrame(rows)


def build_source_summary(
    posts: pd.DataFrame,
    comments: pd.DataFrame,
    raw_summary: pd.DataFrame,
) -> pd.DataFrame:
    unique_posts = posts.groupby("SourceFile", dropna=False).size().rename("UniquePostsKept")
    unique_comments = comments.groupby("SourceFile", dropna=False).size().rename("UniqueCommentsKept")
    summary = raw_summary.merge(unique_posts, on="SourceFile", how="left")
    summary = summary.merge(unique_comments, on="SourceFile", how="left")
    for col in ["UniquePostsKept", "UniqueCommentsKept"]:
        summary[col] = summary[col].fillna(0).astype(int)
    summary["DroppedDuplicatePostRows"] = summary["RawPostRows"] - summary["UniquePostsKept"]
    summary["DroppedDuplicateCommentRows"] = summary["RawCommentRows"] - summary["UniqueCommentsKept"]
    return summary.sort_values("SourceFile")


def build_keyword_share(posts: pd.DataFrame, keywords: list[str], keyword_mode: str) -> pd.DataFrame:
    tagged = posts.copy()
    tagged["MatchedKeywords"] = tagged["PostText"].apply(lambda text: match_keywords(text, keywords))
    tagged["HasKeyword"] = tagged["MatchedKeywords"].apply(
        lambda matched: keyword_hit(matched, keywords, keyword_mode)
    )

    grouped = (
        tagged.groupby("PostAuthorKey")
        .agg(
            TotalPosts=("PostId", "nunique"),
            KeywordPosts=("HasKeyword", "sum"),
            FirstPostAt=("PostCreatedAt", "min"),
            LastPostAt=("PostCreatedAt", "max"),
            AuthorNames=("Author", join_unique),
            AuthorIds=("AuthorId", join_unique),
            AuthorIps=("AuthorIp", join_unique),
            Keywords=(
                "MatchedKeywords",
                lambda values: ", ".join(
                    sorted({kw for items in values for kw in items if str(kw).strip()})
                ),
            ),
        )
        .reset_index()
    )
    grouped["KeywordPostRatio"] = grouped["KeywordPosts"] / grouped["TotalPosts"]
    return grouped.sort_values(
        ["KeywordPostRatio", "KeywordPosts", "TotalPosts"],
        ascending=[False, False, False],
    )


def build_author_activity(posts: pd.DataFrame, keywords: list[str], keyword_mode: str) -> pd.DataFrame:
    tagged = posts.copy()
    tagged["MatchedKeywords"] = tagged["PostText"].apply(lambda text: match_keywords(text, keywords))
    tagged["HasKeyword"] = tagged["MatchedKeywords"].apply(
        lambda matched: keyword_hit(matched, keywords, keyword_mode)
    )
    tagged["Date"] = tagged["PostCreatedAt"].dt.date
    tagged["Hour"] = tagged["PostCreatedAt"].dt.hour
    tagged["Weekday"] = tagged["PostCreatedAt"].dt.day_name()
    return (
        tagged.dropna(subset=["PostCreatedAt"])
        .groupby(["PostAuthorKey", "Date", "Hour", "Weekday"])
        .agg(
            PostCount=("PostId", "nunique"),
            KeywordPostCount=("HasKeyword", "sum"),
            AuthorNames=("Author", join_unique),
        )
        .reset_index()
        .sort_values(["PostAuthorKey", "Date", "Hour"])
    )


def build_author_hourly_activity(posts: pd.DataFrame, keywords: list[str], keyword_mode: str) -> pd.DataFrame:
    tagged = posts.copy()
    tagged["MatchedKeywords"] = tagged["PostText"].apply(lambda text: match_keywords(text, keywords))
    tagged["HasKeyword"] = tagged["MatchedKeywords"].apply(
        lambda matched: keyword_hit(matched, keywords, keyword_mode)
    )
    tagged["Hour"] = tagged["PostCreatedAt"].dt.hour
    return (
        tagged.dropna(subset=["PostCreatedAt"])
        .groupby(["PostAuthorKey", "Hour"])
        .agg(
            PostCount=("PostId", "nunique"),
            KeywordPostCount=("HasKeyword", "sum"),
            AuthorNames=("Author", join_unique),
        )
        .reset_index()
        .sort_values(["PostAuthorKey", "Hour"])
    )


def build_merged_comments(
    posts: pd.DataFrame,
    comments: pd.DataFrame,
    fast_minutes: int,
) -> pd.DataFrame:
    post_cols = [
        "PostId",
        "PostCreatedAt",
        "Title",
        "Author",
        "AuthorId",
        "AuthorIp",
        "PostAuthorKey",
        "SourceFile",
        "Link",
    ]
    comment_cols = [
        "PostId",
        "CommentOrder",
        "CommentCreatedAt",
        "CommentContent",
        "CommentNickname",
        "CommentUserId",
        "CommentIpParsed",
        "CommenterKey",
        "SourceFile",
    ]
    merged = posts[post_cols].merge(
        comments[comment_cols],
        on="PostId",
        how="left",
        suffixes=("_Post", "_Comment"),
    )
    merged["TimeDelta"] = merged["CommentCreatedAt"] - merged["PostCreatedAt"]
    merged["TimeDeltaMinutes"] = merged["TimeDelta"].dt.total_seconds() / 60
    merged["IsFastComment"] = merged["TimeDeltaMinutes"].between(0, fast_minutes, inclusive="both")
    merged["PostHourBucket"] = merged["PostCreatedAt"].dt.floor("h")
    merged["CommentHourBucket"] = merged["CommentCreatedAt"].dt.floor("h")
    merged["CommentDate"] = merged["CommentCreatedAt"].dt.date
    merged["CommentHour"] = merged["CommentCreatedAt"].dt.hour
    return merged


def build_commenter_activity(merged_comments: pd.DataFrame) -> pd.DataFrame:
    comments_only = merged_comments.dropna(subset=["CommentCreatedAt"]).copy()
    if comments_only.empty:
        return pd.DataFrame()
    comments_only["Date"] = comments_only["CommentCreatedAt"].dt.date
    comments_only["Hour"] = comments_only["CommentCreatedAt"].dt.hour
    comments_only["Weekday"] = comments_only["CommentCreatedAt"].dt.day_name()
    return (
        comments_only.groupby(["PostAuthorKey", "CommenterKey", "Date", "Hour", "Weekday"])
        .agg(
            CommentCount=("PostId", "count"),
            FastCommentCount=("IsFastComment", "sum"),
            UniquePostsCommented=("PostId", "nunique"),
            CommenterNames=("CommentNickname", join_unique),
        )
        .reset_index()
        .sort_values(["PostAuthorKey", "CommenterKey", "Date", "Hour"])
    )


def build_time_correlation(
    posts: pd.DataFrame,
    merged_comments: pd.DataFrame,
    min_comments: int,
) -> pd.DataFrame:
    posts_by_hour = (
        posts.dropna(subset=["PostCreatedAt"])
        .assign(HourBucket=lambda df: df["PostCreatedAt"].dt.floor("h"))
        .groupby(["PostAuthorKey", "HourBucket"])
        .size()
        .rename("AuthorPostCount")
        .reset_index()
    )
    comments_by_hour = (
        merged_comments.dropna(subset=["CommentCreatedAt"])
        .assign(HourBucket=lambda df: df["CommentCreatedAt"].dt.floor("h"))
        .groupby(["PostAuthorKey", "CommenterKey", "HourBucket"])
        .agg(
            CommentCount=("PostId", "count"),
            FastCommentCount=("IsFastComment", "sum"),
        )
        .reset_index()
    )

    rows = []
    for (post_author, commenter), pair_comments in comments_by_hour.groupby(
        ["PostAuthorKey", "CommenterKey"]
    ):
        if pair_comments["CommentCount"].sum() < min_comments:
            continue

        pair_posts = posts_by_hour[posts_by_hour["PostAuthorKey"] == post_author]
        timeline = pair_posts[["HourBucket", "AuthorPostCount"]].merge(
            pair_comments[["HourBucket", "CommentCount", "FastCommentCount"]],
            on="HourBucket",
            how="outer",
        )
        timeline[["AuthorPostCount", "CommentCount", "FastCommentCount"]] = timeline[
            ["AuthorPostCount", "CommentCount", "FastCommentCount"]
        ].fillna(0)

        corr = safe_correlation(timeline["AuthorPostCount"], timeline["CommentCount"])
        rows.append(
            {
                "PostAuthorKey": post_author,
                "CommenterKey": commenter,
                "HourlyCorrelation": corr,
                "OverlapHourCount": int(
                    ((timeline["AuthorPostCount"] > 0) & (timeline["CommentCount"] > 0)).sum()
                ),
                "AuthorPostHourCount": int((timeline["AuthorPostCount"] > 0).sum()),
                "CommentHourCount": int((timeline["CommentCount"] > 0).sum()),
                "TotalComments": int(timeline["CommentCount"].sum()),
                "FastCommentCount": int(timeline["FastCommentCount"].sum()),
                "FastCommentRatio": safe_ratio(
                    timeline["FastCommentCount"].sum(),
                    timeline["CommentCount"].sum(),
                ),
            }
        )

    if not rows:
        return pd.DataFrame()

    return pd.DataFrame(rows).sort_values(
        ["HourlyCorrelation", "OverlapHourCount", "TotalComments"],
        ascending=[False, False, False],
    )


def build_interaction_summary(merged_comments: pd.DataFrame, min_comments: int) -> pd.DataFrame:
    comments_only = merged_comments.dropna(subset=["CommentCreatedAt"])
    if comments_only.empty:
        return pd.DataFrame()
    summary = (
        comments_only.groupby(["PostAuthorKey", "CommenterKey"])
        .agg(
            InteractionCount=("PostId", "count"),
            UniquePostsCommented=("PostId", "nunique"),
            WithinWindowCount=("IsFastComment", "sum"),
            MedianDeltaMinutes=("TimeDeltaMinutes", "median"),
            MinDeltaMinutes=("TimeDeltaMinutes", "min"),
        )
        .reset_index()
    )
    summary = summary[summary["InteractionCount"] >= min_comments].copy()
    summary["FastCommentRatio"] = summary["WithinWindowCount"] / summary["InteractionCount"]
    return summary.sort_values(
        ["InteractionCount", "WithinWindowCount", "UniquePostsCommented"],
        ascending=[False, False, False],
    )


def build_fast_comment_patterns(
    merged_comments: pd.DataFrame,
    min_comments: int,
    fast_minutes: int,
) -> pd.DataFrame:
    comments_only = merged_comments.dropna(subset=["CommentCreatedAt"])
    if comments_only.empty:
        return pd.DataFrame()
    patterns = (
        comments_only.groupby(["PostId", "Title", "PostAuthorKey", "PostCreatedAt"])
        .agg(
            CommentCount=("CommenterKey", "count"),
            UniqueCommenters=("CommenterKey", "nunique"),
            FastCommentCount=("IsFastComment", "sum"),
            FirstCommentAt=("CommentCreatedAt", "min"),
            LastCommentAt=("CommentCreatedAt", "max"),
            MinDeltaMinutes=("TimeDeltaMinutes", "min"),
            MedianDeltaMinutes=("TimeDeltaMinutes", "median"),
        )
        .reset_index()
    )
    patterns = patterns[patterns["CommentCount"] >= min_comments].copy()
    patterns["FastCommentRatio"] = patterns["FastCommentCount"] / patterns["CommentCount"]
    patterns["FastWindowMinutes"] = fast_minutes
    return patterns.sort_values(
        ["FastCommentCount", "FastCommentRatio", "CommentCount"],
        ascending=[False, False, False],
    )


def build_target_daily_posts(posts: pd.DataFrame, target_tokens: list[str]) -> pd.DataFrame:
    if not target_tokens:
        filtered = posts.copy()
        scope = "All users"
    else:
        filtered = filter_posts_by_tokens(posts, target_tokens)
        scope = "Target users"

    if filtered.empty:
        return pd.DataFrame({"Message": ["No posts matched the selected scope."]})

    filtered = filtered.copy()
    filtered["Date"] = filtered["PostCreatedAt"].dt.date
    result = (
        filtered.dropna(subset=["PostCreatedAt"])
        .groupby(["PostAuthorKey", "Date"])
        .agg(
            DailyPostCount=("PostId", "nunique"),
            AuthorNames=("Author", join_unique),
            AuthorIds=("AuthorId", join_unique),
            AuthorIps=("AuthorIp", join_unique),
        )
        .reset_index()
        .sort_values(["PostAuthorKey", "Date"])
    )
    result.insert(0, "Scope", scope)
    return result


def build_run_summary(
    files: list[Path],
    keywords: list[str],
    keyword_mode: str,
    target_tokens: list[str],
    min_comments: int,
    fast_minutes: int,
    top_keywords: int,
) -> pd.DataFrame:
    return pd.DataFrame(
        [
            ["Input files", "\n".join(str(path) for path in files)],
            ["Keyword count", len(keywords)],
            ["Keyword match mode", keyword_mode],
            ["Keywords", ", ".join(keywords)],
            ["Target user tokens", ", ".join(target_tokens) if target_tokens else "(not specified)"],
            ["Minimum comments", min_comments],
            ["Fast comment threshold minutes", fast_minutes],
            ["Top row limit", top_keywords],
            [
                "Neutral wording",
                "repeated interaction, time overlap, fast comment ratio, co-activity",
            ],
        ],
        columns=["Item", "Value"],
    )


def build_report_guide() -> pd.DataFrame:
    rows = [
        (
            "읽는 순서",
            "1",
            "Summary",
            "분석 조건과 사용된 키워드를 먼저 확인합니다.",
        ),
        (
            "읽는 순서",
            "2",
            "SourceFileCounts",
            "두 원천 파일의 원본 행 수와 중복 제거 후 남은 행 수를 확인합니다.",
        ),
        (
            "핵심 지표",
            "3",
            "KeywordShareByUser",
            "사용자별 전체 글 수, 키워드 포함 글 수, 키워드 글 비중을 봅니다. OR 모드는 키워드 중 하나라도 포함되면 집계하고, AND 모드는 모든 키워드가 포함된 글만 집계합니다.",
        ),
        (
            "활동 시간",
            "4",
            "AuthorActivityTime",
            "게시글 작성자가 어느 날짜와 시간대에 글을 썼는지 보여줍니다.",
        ),
        (
            "활동 시간",
            "5",
            "AuthorHourlyActivity",
            "게시글 작성자의 시간대별 활동을 요약합니다. 일자 구분 없이 0~23시 기준입니다.",
        ),
        (
            "댓글 활동",
            "6",
            "CommenterActivityTime",
            "댓글 작성자가 특정 게시글 작성자의 글에 언제 댓글을 달았는지 보여줍니다.",
        ),
        (
            "상관 관계",
            "7",
            "TimeCorrelation",
            "게시글 작성 시간대와 댓글 작성 시간대가 얼마나 같이 움직이는지 계산합니다. 1에 가까울수록 같은 시간대에 함께 증가한 패턴입니다.",
        ),
        (
            "상호작용",
            "8",
            "Interactions",
            "게시글 작성자와 댓글 작성자 조합별 댓글 빈도, 빠른 댓글 수, 중앙 시간차를 보여줍니다.",
        ),
        (
            "빠른 댓글",
            "9",
            "FastCommentPatterns",
            "글 작성 후 지정한 분 이내에 댓글이 몰린 게시글을 보여줍니다.",
        ),
        (
            "지정 사용자",
            "10",
            "TargetDailyPosts",
            "Target user를 입력하면 해당 사용자만, 비워두면 전체 사용자의 일자별 글 작성 수를 보여줍니다.",
        ),
        (
            "원자료",
            "11",
            "TimeDeltas",
            "게시글과 댓글을 PostId로 연결한 원자료입니다. 글 작성 시각과 댓글 작성 시각의 차이를 포함합니다.",
        ),
        (
            "주의",
            "-",
            "전체",
            "이 리포트는 반복 상호작용, 시간대 겹침, 빠른 댓글 비율 같은 관찰값만 제공합니다. 동일인, 공모, 조직적 활동을 단정하지 않습니다.",
        ),
    ]
    return pd.DataFrame(rows, columns=["Section", "Step", "Sheet", "Meaning"])


def write_excel_report(output_path: Path, results: dict[str, pd.DataFrame | Path]) -> None:
    sheet_map = {
        "ReportGuide": results["report_guide"],
        "Summary": results["summary"],
        "SourceFileCounts": results["source_summary"],
        "KeywordShareByUser": results["all_keyword_user_share"],
        "AuthorActivityTime": results["author_activity_time"],
        "AuthorHourlyActivity": results["author_hourly_activity"],
        "CommenterActivityTime": results["commenter_activity_time"],
        "TimeCorrelation": results["time_correlation"],
        "Interactions": results["interaction_summary"],
        "FastCommentPatterns": results["fast_comment_patterns"],
        "TargetDailyPosts": results["target_daily_posts"],
        "TimeDeltas": results["merged_time_deltas"],
    }

    with pd.ExcelWriter(
        output_path,
        engine="xlsxwriter",
        engine_kwargs={"options": {"strings_to_urls": False}},
    ) as writer:
        for sheet_name, df in sheet_map.items():
            assert isinstance(df, pd.DataFrame)
            safe_df = excel_safe_dataframe(df)
            safe_df.to_excel(writer, sheet_name=sheet_name[:31], index=False)
            worksheet = writer.sheets[sheet_name[:31]]
            worksheet.freeze_panes(1, 0)
            if len(safe_df.columns) > 0:
                worksheet.autofilter(0, 0, max(len(safe_df), 1), len(safe_df.columns) - 1)
            for idx, col in enumerate(safe_df.columns):
                width = min(max(len(str(col)) + 2, 12), 46)
                worksheet.set_column(idx, idx, width)


def excel_safe_dataframe(df: pd.DataFrame) -> pd.DataFrame:
    if df is None or df.empty:
        return pd.DataFrame({"Message": ["No rows"]})

    safe = df.copy()
    if len(safe) > 100_000:
        safe = safe.head(100_000)
    for col in safe.columns:
        if pd.api.types.is_timedelta64_dtype(safe[col]):
            safe[col] = safe[col].astype(str)
        elif pd.api.types.is_datetime64_any_dtype(safe[col]):
            safe[col] = safe[col].dt.strftime("%Y-%m-%d %H:%M:%S")
    return safe


def match_keywords(text: object, keywords: list[str]) -> list[str]:
    value = "" if pd.isna(text) else str(text)
    matched = []
    for keyword in keywords:
        if keyword and re.search(re.escape(keyword), value, flags=re.IGNORECASE):
            matched.append(keyword)
    return matched


def keyword_hit(matched_keywords: list[str], keywords: list[str], keyword_mode: str) -> bool:
    if not keywords:
        return False
    matched_keys = {keyword.casefold() for keyword in matched_keywords}
    required_keys = {keyword.casefold() for keyword in keywords if keyword.strip()}
    if not required_keys:
        return False
    if keyword_mode == "AND":
        return required_keys.issubset(matched_keys)
    return bool(matched_keys)


def normalize_keyword_mode(keyword_mode: str) -> str:
    mode = str(keyword_mode or "OR").strip().upper()
    return "AND" if mode == "AND" else "OR"


def filter_posts_by_tokens(posts: pd.DataFrame, tokens: list[str]) -> pd.DataFrame:
    haystack_cols = ["Author", "AuthorId", "AuthorIp", "PostAuthorKey"]
    mask = pd.Series(False, index=posts.index)
    for token in tokens:
        token_lower = token.lower()
        token_mask = pd.Series(False, index=posts.index)
        for col in haystack_cols:
            if col in posts.columns:
                token_mask |= posts[col].fillna("").astype(str).str.lower().str.contains(
                    token_lower,
                    regex=False,
                )
        mask |= token_mask
    return posts[mask].copy()


def join_unique(values: pd.Series) -> str:
    cleaned = []
    for value in values:
        if pd.isna(value):
            continue
        text = str(value).strip()
        if text and text.lower() not in {"nan", "none", "<na>"}:
            cleaned.append(text)
    return ", ".join(sorted(set(cleaned))[:8])


def safe_ratio(numerator: float, denominator: float) -> float:
    return float(numerator / denominator) if denominator else 0.0


def safe_correlation(left: pd.Series, right: pd.Series) -> float:
    if len(left) < 2 or len(right) < 2:
        return 0.0
    if left.nunique(dropna=True) < 2 or right.nunique(dropna=True) < 2:
        return 0.0
    corr = left.corr(right)
    return float(corr) if pd.notna(corr) else 0.0


def normalize_input_paths(input_path: str | Path | Iterable[str | Path]) -> list[Path]:
    if isinstance(input_path, (str, Path)):
        raw_paths = [input_path]
    else:
        raw_paths = list(input_path)
    paths = [Path(path).expanduser().resolve() for path in raw_paths]
    missing = [path for path in paths if not path.exists()]
    if missing:
        raise FileNotFoundError(f"Input file was not found: {missing[0]}")
    return paths


def normalize_tokens(target_authors: str | Iterable[str] | None) -> list[str]:
    if not target_authors:
        return []
    if isinstance(target_authors, str):
        parts = target_authors.replace("\n", ",").split(",")
    else:
        parts = list(target_authors)
    return [str(part).strip() for part in parts if str(part).strip()]
