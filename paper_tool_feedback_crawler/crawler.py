from __future__ import annotations

import csv
import json
import functools
import math
import re
import threading
import time
from zipfile import ZipFile
from collections import Counter
from concurrent.futures import Future, ThreadPoolExecutor, as_completed
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import parse_qsl, urlencode, urljoin, urlparse, urlunparse

import requests
from bs4 import BeautifulSoup


DEFAULT_HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (X11; Linux x86_64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/124.0.0.0 Safari/537.36"
    )
}

POSITIVE_MARKERS = (
    "pro",
    "pros",
    "great",
    "good",
    "helpful",
    "easy",
    "useful",
    "love",
    "like",
    "works great",
    "works well",
    "robust",
    "reliable",
    "stable",
    "faster",
    "happy",
    "user-friendly",
    "best",
    "excellent",
    "smooth",
    "好用",
    "很好",
    "不错",
    "稳定",
    "方便",
    "推荐",
    "喜欢",
    "顺手",
)

NEGATIVE_MARKERS = (
    "annoying",
    "bad",
    "bug",
    "buggy",
    "hate",
    "useless",
    "problem",
    "issue",
    "wrong",
    "slow",
    "confusing",
    "missing",
    "inconsistent",
    "clunky",
    "time-consuming",
    "limited",
    "manual",
    "expensive",
    "worse",
    "awful",
    "pain",
    "fault",
    "不好用",
    "麻烦",
    "卡",
    "差",
    "问题",
    "不稳定",
    "缺点",
    "糟糕",
)

RECOMMENDATION_MARKERS = (
    "recommend",
    "worth it",
    "go with",
    "better than",
    "switched",
    "switch to",
    "prefer",
    "should use",
    "推荐",
    "值得",
    "建议用",
    "更推荐",
)

EXPERIENCE_MARKERS = (
    "i use",
    "i'm using",
    "i am using",
    "i've used",
    "i have used",
    "i used",
    "my experience",
    "works for me",
    "worked for me",
    "i find",
    "i found",
    "for me",
    "we use",
    "i switched",
    "我在用",
    "我一直在用",
    "我用了",
    "我用过",
    "对我来说",
    "我的体验",
)

QUESTION_MARKERS = (
    "what do you use",
    "any ideas",
    "should i",
    "is there",
    "any thoughts",
    "does anyone",
    "can anyone",
    "what are your",
    "which should",
    "有人用过",
    "有没有",
    "哪个好",
    "怎么选",
    "求推荐",
)

DESIRED_FEATURE_MARKERS = (
    "need",
    "needs",
    "want",
    "wanted",
    "wish",
    "looking for",
    "would like",
    "would love",
    "should have",
    "should support",
    "could use",
    "feature request",
    "希望",
    "需要",
    "想要",
    "最好能",
    "最好",
    "希望有",
    "希望支持",
    "应该支持",
    "能不能",
    "能否",
)

PAIN_POINT_MARKERS = (
    *NEGATIVE_MARKERS,
    "hard",
    "difficult",
    "friction",
    "switch",
    "switching",
    "fall short",
    "doesn't work",
    "does not work",
    "can't",
    "cannot",
    "incompatible",
    "compatibility issue",
    "no support",
    "lack",
    "lacking",
    "hassle",
    "pain point",
    "切换",
    "来回切换",
    "门槛高",
    "兼容性问题",
    "不支持",
    "做不到",
    "痛点",
    "困难",
)

CONSTRAINT_MARKERS = (
    "offline",
    "local",
    "privacy",
    "private",
    "cloud",
    "upload",
    "on-device",
    "without internet",
    "no internet",
    "linux",
    "windows",
    "ubuntu",
    "docx",
    "word",
    "cross-platform",
    "compatibility",
    "16gb",
    "8gb",
    "cpu",
    "gpu",
    "avx2",
    "本地",
    "离线",
    "隐私",
    "不联网",
    "兼容",
    "跨平台",
    "内存",
    "显卡",
)

USAGE_SCENARIO_MARKERS = (
    *EXPERIENCE_MARKERS,
    "workflow",
    "research workflow",
    "daily workflow",
    "literature review",
    "systematic review",
    "paper writing",
    "writing papers",
    "thesis",
    "dissertation",
    "course report",
    "google docs",
    "word plugin",
    "markdown",
    "pdf",
    "bibtex",
    "citation",
    "annotate",
    "annotation",
    "export",
    "导出",
    "写论文",
    "文献综述",
    "课程报告",
    "工作流",
    "阅读论文",
    "批注",
)

EVIDENCE_STOPWORDS = {
    "a", "an", "and", "are", "as", "at", "be", "but", "by", "for", "from", "i", "im", "i'm", "i’ve", "i've", "in", "into", "is", "it", "its", "me", "my", "of", "on", "or", "that", "the", "their", "them", "this", "to", "use", "used", "using", "very", "was", "with", "works",
    "trying", "pain", "point", "cannot", "can't", "figure", "out", "problem", "issue", "experiencing", "better", "interested", "process", "now", "doesn", "doesn't", "work", "anymore", "large", "working", "slow", "unfortunately", "experienced", "buggy", "were", "switched", "switch", "switching", "hate", "resort", "last", "awful", "really", "found", "did", "when", "bugging", "have", "has", "had", "been", "can", "could", "would", "should", "will", "we", "you", "they", "he", "she", "so", "if", "then", "than", "just", "like", "some", "any", "all", "one", "thing", "things", "about", "what", "which", "who", "why", "how", "there", "here", "where", "much", "too", "also", "only", "well", "even", "does", "having", "because", "since", "while", "during", "always", "never", "often", "sometimes", "usually", "almost", "enough", "many", "most", "other", "another", "such", "same", "different", "own", "good", "bad", "great", "best", "worst", "new", "old", "right", "wrong", "sure", "certain", "possible", "impossible", "easy", "hard", "simple", "complex", "clear", "unclear", "true", "false", "real", "fake", "full", "empty", "high", "low", "big", "small", "long", "short", "early", "late", "next", "previous", "first", "find", "finding", "get", "getting", "got", "take", "taking", "took", "make", "making", "made", "do", "doing", "done", "know", "knowing", "knew", "think", "thinking", "thought", "see", "seeing", "saw", "look", "looking", "looked", "want", "wanting", "wanted", "need", "needing", "needed", "try", "tried", "help", "helping", "helped", "ask", "asking", "asked", "tell", "telling", "told", "say", "saying", "said", "call", "calling", "called", "feel", "feeling", "felt", "become", "becoming", "became", "leave", "leaving", "left", "put", "putting", "mean", "meaning", "meant", "keep", "keeping", "kept", "let", "letting", "begin", "beginning", "began", "seem", "seeming", "seemed", "help", "show", "showing", "showed", "hear", "hearing", "heard", "play", "playing", "played", "run", "running", "ran", "move", "moving", "moved", "live", "living", "lived", "believe", "believing", "believed", "bring", "bringing", "brought", "happen", "happening", "happened", "must", "might", "may", "shall", "ought", "cannot", "can't", "couldn't", "shouldn't", "wouldn't", "isn't", "aren't", "wasn't", "weren't", "hasn't", "haven't", "hadn't", "doesn't", "don't", "didn't", "won't", "werent", "wasnt", "hasnt", "havent", "hadnt", "doesnt", "dont", "didnt", "wont", "wouldnt", "shouldnt", "couldnt", "cant", "isnt", "arent"
}

ASPECT_RULES: tuple[tuple[str, tuple[str, ...]], ...] = (
    (
        "引用/写作流程",
        (
            "citation",
            "cite",
            "citing",
            "bibliography",
            "reference",
            "references",
            "word plugin",
            "google docs",
            "latex",
            "bibtex",
            "引用",
            "引文",
            "参考文献",
            "写作",
            "插入引用",
        ),
    ),
    (
        "PDF阅读/批注",
        (
            "pdf",
            "annotation",
            "annotate",
            "highlight",
            "reader",
            "批注",
            "标注",
            "阅读",
            "高亮",
            "笔记",
        ),
    ),
    (
        "同步/数据管理",
        (
            "sync",
            "synced",
            "backup",
            "cloud",
            "storage",
            "import",
            "export",
            "library",
            "organize",
            "tag",
            "folder",
            "同步",
            "备份",
            "导入",
            "导出",
            "标签",
            "管理",
        ),
    ),
    (
        "插件/生态集成",
        (
            "plugin",
            "plugins",
            "extension",
            "integrat",
            "ecosystem",
            "api",
            "插件",
            "生态",
            "集成",
        ),
    ),
    (
        "搜索/结果覆盖",
        (
            "search",
            "find",
            "discover",
            "coverage",
            "niche topics",
            "database",
            "搜索",
            "检索",
            "覆盖",
            "结果",
        ),
    ),
    (
        "协作",
        (
            "collaboration",
            "collaborate",
            "shared",
            "sharing",
            "team",
            "协作",
            "共享",
            "团队",
        ),
    ),
    (
        "稳定性/可靠性",
        (
            "stable",
            "reliable",
            "bug",
            "buggy",
            "crash",
            "inconsistent",
            "issue",
            "problem",
            "稳定",
            "不稳定",
            "崩溃",
            "问题",
        ),
    ),
    (
        "性能/速度",
        (
            "fast",
            "faster",
            "speed",
            "slow",
            "latency",
            "time",
            "性能",
            "速度",
            "很快",
            "很慢",
            "卡",
            "省时间",
        ),
    ),
    (
        "价格/价值",
        (
            "worth it",
            "expensive",
            "pricing",
            "price",
            "cost",
            "value",
            "价格",
            "付费",
            "成本",
            "值得",
        ),
    ),
    (
        "易用性/界面",
        (
            "easy to use",
            "user-friendly",
            "simple",
            "smooth",
            "intuitive",
            "ui",
            "ux",
            "界面",
            "易用",
            "顺手",
            "上手",
            "方便",
        ),
    ),
)

REQUIREMENT_CATEGORY_RULES: tuple[tuple[str, tuple[str, ...]], ...] = (
    (
        "双屏编辑/同步",
        (
            "markdown",
            "editor",
            "monaco",
            "pdf",
            "scroll sync",
            "sync scroll",
            "outline",
            "preview",
            "双屏",
            "同步滚动",
            "联动",
            "目录跳转",
        ),
    ),
    (
        "格式导出/模板",
        (
            "docx",
            "word",
            "pandoc",
            "template",
            "format",
            "style",
            "openxml",
            "export",
            "heading",
            "margin",
            "font",
            "line spacing",
            "导出",
            "模板",
            "格式",
            "样式",
            "页边距",
            "字体",
            "行距",
        ),
    ),
    (
        "文献管理/BibTeX",
        (
            "bibtex",
            "citation",
            "reference",
            "references",
            "library",
            "tag",
            "folder",
            "import",
            "export",
            "zotero",
            "mendeley",
            "paperpile",
            "paperlib",
            "引用",
            "参考文献",
            "文献库",
            "标签",
            "导入",
        ),
    ),
    (
        "PDF阅读/批注",
        (
            "pdf",
            "annotation",
            "annotate",
            "highlight",
            "reader",
            "阅读",
            "批注",
            "高亮",
            "笔记",
        ),
    ),
    (
        "AI阅读/问答",
        (
            "ai",
            "rag",
            "summary",
            "summar",
            "chat",
            "ask",
            "answer",
            "citation grounded",
            "llm",
            "embedding",
            "向量",
            "摘要",
            "问答",
            "检索增强",
            "语义",
        ),
    ),
    (
        "本地优先/隐私",
        (
            "offline",
            "local",
            "privacy",
            "private",
            "cloud",
            "on-device",
            "without internet",
            "本地",
            "离线",
            "隐私",
            "云端",
            "联网",
        ),
    ),
    (
        "协作/版本管理",
        (
            "git",
            "version",
            "snapshot",
            "history",
            "collaboration",
            "share",
            "team",
            "协作",
            "共享",
            "版本",
            "快照",
        ),
    ),
    (
        "性能/稳定性",
        (
            "fast",
            "slow",
            "startup",
            "latency",
            "performance",
            "stable",
            "reliable",
            "bug",
            "crash",
            "卡",
            "慢",
            "启动",
            "性能",
            "稳定",
            "崩溃",
        ),
    ),
    (
        "平台兼容性",
        (
            "linux",
            "windows",
            "ubuntu",
            "mac",
            "cross-platform",
            "compatibility",
            "兼容",
            "跨平台",
        ),
    ),
)

DOC_PRODUCT_CAPABILITY_TERMS: tuple[str, ...] = (
    "markdown",
    "pdf",
    "双屏",
    "联动",
    "同步滚动",
    "docx",
    "word",
    "openxml",
    "模板",
    "高保真导出",
    "pandoc",
    "afd",
    "bibtex",
    "citation",
    "引用",
    "参考文献",
    "annotation",
    "批注",
    "rag",
    "llm",
    "llamasharp",
    "本地优先",
    "offline",
    "local",
    "privacy",
    "隐私",
    "离线",
    "linux",
    "windows",
    "avx2",
    "16gb",
    "8gb",
    "5 秒",
    "30 毫秒",
    "95%",
)

DOC_NOISE_MARKERS: tuple[str, ...] = (
    "wbs",
    "甘特图",
    "里程碑",
    "项目进度",
    "阶段划分",
    "职责分配",
    "组织架构",
    "团队协作",
    "资源与成本",
    "预算",
    "风险分析",
    "质量保证",
    "配置管理",
    "监控与报告",
    "软件工程课程",
    "指导教师",
    "项目组长",
    "成员",
    "任务分解",
    "验收准备",
    "参考标准与资料",
    "文档编写",
    "gb/t",
    "github codeql",
    "trello",
    "飞书",
    "腾讯文档",
    "二〇二六年",
    "项目管理与技术文档",
)

DOC_REQUIREMENT_HEADINGS: tuple[str, ...] = (
    "1.2 项目背景",
    "1.3 任务来源",
    "2.1 项目范围",
    "2.2 主要功能",
    "2.3 性能要求",
    "2.5 实施环境",
)


@dataclass
class SourceConfig:
    name: str
    source_type: str = "generic_css"
    search_site: str = ""
    search_domains: list[str] = field(default_factory=list)
    search_tags: list[str] = field(default_factory=list)
    github_repos: list[str] = field(default_factory=list)
    github_search_qualifiers: list[str] = field(default_factory=list)
    start_urls: list[str] = field(default_factory=list)
    item_selector: str = ""
    title_selector: str = ""
    link_selector: str | None = None
    snippet_selector: str | None = None
    author_selector: str | None = None
    published_at_selector: str | None = None
    detail_content_selector: str | None = None
    next_page_selector: str | None = None
    subreddits: list[str] = field(default_factory=list)
    search_terms: list[str] = field(default_factory=list)
    search_sort: str = "relevance"
    search_limit: int = 25
    include_comments: bool = True
    max_comments: int = 5
    max_pages: int = 3
    crawl_delay_seconds: float = 1.0
    max_workers: int | None = None
    headers: dict[str, str] = field(default_factory=dict)

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> "SourceConfig":
        return cls(
            name=payload["name"],
            source_type=payload.get("source_type", "generic_css"),
            search_site=payload.get("search_site", ""),
            search_domains=payload.get("search_domains", []),
            search_tags=payload.get("search_tags", []),
            github_repos=payload.get("github_repos", []),
            github_search_qualifiers=payload.get("github_search_qualifiers", []),
            start_urls=payload.get("start_urls", []),
            item_selector=payload.get("item_selector", ""),
            title_selector=payload.get("title_selector", ""),
            link_selector=payload.get("link_selector"),
            snippet_selector=payload.get("snippet_selector"),
            author_selector=payload.get("author_selector"),
            published_at_selector=payload.get("published_at_selector"),
            detail_content_selector=payload.get("detail_content_selector"),
            next_page_selector=payload.get("next_page_selector"),
            subreddits=payload.get("subreddits", []),
            search_terms=payload.get("search_terms", []),
            search_sort=payload.get("search_sort", "relevance"),
            search_limit=payload.get("search_limit", 25),
            include_comments=payload.get("include_comments", True),
            max_comments=payload.get("max_comments", 5),
            max_pages=payload.get("max_pages", 3),
            crawl_delay_seconds=payload.get("crawl_delay_seconds", 1.0),
            max_workers=payload.get("max_workers"),
            headers=payload.get("headers", {}),
        )


@dataclass
class CrawlConfig:
    keywords: list[str]
    review_terms: list[str]
    requirement_terms: list[str]
    sources: list[SourceConfig]
    request_timeout_seconds: int = 20
    max_workers: int = 1
    keyword_aliases: dict[str, list[str]] = field(default_factory=dict)
    doc_requirements_path: str = ""

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> "CrawlConfig":
        return cls(
            keywords=payload["keywords"],
            review_terms=payload.get("review_terms", []),
            requirement_terms=payload.get("requirement_terms", []),
            sources=[SourceConfig.from_dict(item) for item in payload["sources"]],
            request_timeout_seconds=payload.get("request_timeout_seconds", 20),
            max_workers=max(1, int(payload.get("max_workers", 1))),
            keyword_aliases=payload.get("keyword_aliases", {}),
            doc_requirements_path=payload.get("doc_requirements_path", ""),
        )


@dataclass
class ToolMention:
    tool: str
    sentiment_label: str
    pros: list[str] = field(default_factory=list)
    cons: list[str] = field(default_factory=list)
    recommendations: list[str] = field(default_factory=list)
    experience_reports: list[str] = field(default_factory=list)
    evidence_count: int = 0

    def as_dict(self) -> dict[str, Any]:
        return {
            "tool": self.tool,
            "sentiment_label": self.sentiment_label,
            "pros": self.pros,
            "cons": self.cons,
            "recommendations": self.recommendations,
            "experience_reports": self.experience_reports,
            "evidence_count": self.evidence_count,
        }


@dataclass
class RequirementSignal:
    category: str
    signal_type: str
    summary: str
    evidence: str
    tools: list[str] = field(default_factory=list)
    source_kind: str = "crawl"
    alignment_score: float = 0.0

    def as_dict(self) -> dict[str, Any]:
        return {
            "category": self.category,
            "signal_type": self.signal_type,
            "summary": self.summary,
            "evidence": self.evidence,
            "tools": self.tools,
            "source_kind": self.source_kind,
            "alignment_score": self.alignment_score,
        }


@dataclass
class FeedbackRecord:
    source: str
    url: str
    title: str
    snippet: str
    content: str
    author: str
    published_at: str
    matched_keywords: list[str]
    matched_review_terms: list[str]
    matched_requirement_terms: list[str]
    overall_sentiment: str
    comment_count: int
    is_question: bool
    tool_mentions: list[ToolMention] = field(default_factory=list)
    requirement_signals: list[RequirementSignal] = field(default_factory=list)

    def as_dict(self) -> dict[str, Any]:
        return {
            "source": self.source,
            "url": self.url,
            "title": self.title,
            "snippet": self.snippet,
            "content": self.content,
            "author": self.author,
            "published_at": self.published_at,
            "matched_keywords": self.matched_keywords,
            "matched_review_terms": self.matched_review_terms,
            "matched_requirement_terms": self.matched_requirement_terms,
            "overall_sentiment": self.overall_sentiment,
            "comment_count": self.comment_count,
            "is_question": self.is_question,
            "tool_mentions": [item.as_dict() for item in self.tool_mentions],
            "requirement_signals": [item.as_dict() for item in self.requirement_signals],
        }


def load_config(path: str | Path) -> CrawlConfig:
    payload = json.loads(Path(path).read_text(encoding="utf-8"))
    return CrawlConfig.from_dict(payload)


def extract_docx_text_lines(path: str | Path) -> list[str]:
    docx_path = Path(path)
    if not docx_path.exists():
        return []
    try:
        with ZipFile(docx_path) as archive:
            xml = archive.read("word/document.xml").decode("utf-8", "ignore")
    except Exception:
        return []

    text = re.sub(r"<w:tab[^>]*/>", "\t", xml)
    text = re.sub(r"</w:p>", "\n", text)
    text = re.sub(r"<[^>]+>", "", text)
    text = (
        text.replace("&amp;", "&")
        .replace("&lt;", "<")
        .replace("&gt;", ">")
        .replace(" ", " ")
    )
    return [normalize_text(line) for line in text.splitlines() if normalize_text(line)]


def _extract_doc_requirement_lines(path: str | Path) -> list[str]:
    lines = extract_docx_text_lines(path)
    if not lines:
        return []

    relevant_lines: list[str] = []
    capture = False
    for line in lines:
        if line in DOC_REQUIREMENT_HEADINGS:
            capture = True
            continue
        if capture and re.match(r"^\d+\.\d+\s+", line) and line not in DOC_REQUIREMENT_HEADINGS:
            capture = line in DOC_REQUIREMENT_HEADINGS
            if not capture:
                continue
        if not capture:
            continue
        if _looks_like_doc_management_noise(line):
            continue
        if _looks_like_doc_requirement_line(line):
            relevant_lines.append(line)

    return _sorted_unique(relevant_lines)


def _looks_like_doc_management_noise(text: str) -> bool:
    lower = normalize_text(text).lower()
    if not lower:
        return True
    if any(marker in lower for marker in DOC_NOISE_MARKERS):
        return True
    if re.match(r"^(?:第?\d+[–-]?\d*周|20\d{2}\s*年\s*\d+\s*月\s*\d+\s*日)", lower):
        return True
    return False


def _looks_like_doc_requirement_line(text: str) -> bool:
    normalized = normalize_text(text)
    if not normalized:
        return False
    if len(normalized) < 10:
        return False
    lower = normalized.lower()
    explicit_requirement_markers = (
        "支持",
        "提供",
        "实现",
        "具备",
        "集成",
        "要求",
        "响应速度",
        "数据一致性",
        "跨平台兼容性",
        "安全性",
        "最低配置",
        "推荐配置",
        "网络要求",
    )
    capability_hits = sum(1 for term in DOC_PRODUCT_CAPABILITY_TERMS if term.lower() in lower)
    if any(marker in normalized for marker in ("痛点", "必要性", "应用领域", "服务对象")) and not any(
        marker in normalized for marker in explicit_requirement_markers
    ):
        return False
    if len(normalized) > 160 and not any(marker in normalized for marker in explicit_requirement_markers):
        return False
    if capability_hits >= 1:
        return True
    return any(
        marker in normalized
        for marker in (
            "功能范围",
            "核心目标导向",
            "性能要求",
            "响应速度",
            "数据一致性",
            "跨平台兼容性",
            "安全性",
            "最低配置",
            "推荐配置",
            "网络要求",
        )
    )


def _build_doc_requirement_signals(path: str | Path) -> list[RequirementSignal]:
    signals: list[RequirementSignal] = []
    seen_keys: set[tuple[str, str, str]] = set()
    for line in _extract_doc_requirement_lines(path):
        category = _infer_requirement_category(line)
        signal_type = _classify_requirement_signal(line)
        lower = normalize_text(line).lower()
        if signal_type == "usage_scenario" and any(
            marker in lower for marker in ("支持", "提供", "实现", "集成", "具备")
        ):
            signal_type = "desired_feature"
        if signal_type == "usage_scenario" and _contains_any_marker(line, CONSTRAINT_MARKERS):
            signal_type = "constraint"
        if any(marker in lower for marker in ("响应速度", "数据一致性", "跨平台兼容性", "安全性")):
            signal_type = "constraint"
        summary = _extract_requirement_summary(line, "")
        alignment_score = _score_doc_alignment(summary)
        key = (category, signal_type, _normalize_evidence_text(summary))
        if key in seen_keys:
            continue
        seen_keys.add(key)
        signals.append(
            RequirementSignal(
                category=category,
                signal_type=signal_type,
                summary=summary,
                evidence=line,
                tools=[],
                source_kind="doc_requirement",
                alignment_score=alignment_score,
            )
        )
    return signals


def normalize_text(value: str | None) -> str:
    if not value:
        return ""
    return " ".join(value.split())


def extract_text(node: Any, selector: str | None) -> str:
    if not selector:
        return ""
    target = node.select_one(selector)
    if target is None:
        return ""
    return normalize_text(target.get_text(" ", strip=True))


def extract_link(node: Any, selector: str | None, base_url: str) -> str:
    if not selector:
        return ""
    target = node.select_one(selector)
    if target is None:
        return ""
    href = target.get("href", "").strip()
    return urljoin(base_url, href)


def same_host(url_a: str, url_b: str) -> bool:
    return urlparse(url_a).netloc == urlparse(url_b).netloc


def _sorted_unique(items: list[str]) -> list[str]:
    deduped = list(dict.fromkeys(item for item in items if item))
    return deduped


@dataclass
class CrawlProgress:
    enabled: bool = True
    total_steps: int = 0
    completed_steps: int = 0
    record_count: int = 0
    started_at: float = field(default_factory=time.monotonic)
    _lock: threading.Lock = field(default_factory=threading.Lock, init=False, repr=False)

    def reset(self, total_steps: int) -> None:
        with self._lock:
            self.total_steps = max(total_steps, 0)
            self.completed_steps = 0
            self.record_count = 0
            self.started_at = time.monotonic()
            self._emit_unlocked(f"start | planned_steps={self.total_steps}")

    def status(
        self,
        source_name: str,
        stage: str,
        detail: str,
    ) -> None:
        with self._lock:
            self._emit_unlocked(
                "working",
                source_name=source_name,
                stage=stage,
                detail=detail,
            )

    def add_total(
        self,
        steps: int,
        source_name: str,
        stage: str,
        detail: str,
    ) -> None:
        if steps <= 0:
            return
        with self._lock:
            self.total_steps += steps
            remaining = max(self.total_steps - self.completed_steps, 0)
            self._emit_unlocked(
                f"discovered | added={steps} total={self.total_steps} remaining={remaining}",
                source_name=source_name,
                stage=stage,
                detail=detail,
            )

    def advance(
        self,
        source_name: str,
        stage: str,
        detail: str,
        records_added: int = 0,
    ) -> None:
        with self._lock:
            self.completed_steps += 1
            self.record_count += max(records_added, 0)
            remaining = max(self.total_steps - self.completed_steps, 0)
            self._emit_unlocked(
                (
                    f"progress {self.completed_steps}/{self.total_steps} "
                    f"| remaining={remaining} records={self.record_count}"
                ),
                source_name=source_name,
                stage=stage,
                detail=detail,
            )

    def finish(self) -> None:
        with self._lock:
            elapsed_seconds = time.monotonic() - self.started_at
            remaining = max(self.total_steps - self.completed_steps, 0)
            self._emit_unlocked(
                (
                    f"done | completed={self.completed_steps}/{self.total_steps} "
                    f"remaining={remaining} records={self.record_count} "
                    f"elapsed={elapsed_seconds:.1f}s"
                )
            )

    def _emit_unlocked(
        self,
        message: str,
        source_name: str = "",
        stage: str = "",
        detail: str = "",
    ) -> None:
        if not self.enabled:
            return
        parts = [f"[crawl] {message}"]
        if source_name:
            parts.append(f"source={source_name}")
        if stage:
            parts.append(f"stage={stage}")
        if detail:
            parts.append(f"detail={normalize_text(detail)}")
        print(" | ".join(parts), flush=True)


class FeedbackCrawler:
    def __init__(self, config: CrawlConfig) -> None:
        self.config = config
        self.progress = CrawlProgress()
        self._thread_local = threading.local()
        self._seen_urls_lock = threading.Lock()
        self.keyword_variants = self._build_keyword_variants()
        self.keyword_patterns = {
            keyword: [self._compile_term_pattern(variant) for variant in variants]
            for keyword, variants in self.keyword_variants.items()
        }
        self.review_term_patterns = {
            term: self._compile_term_pattern(term) for term in self.config.review_terms
        }
        self.requirement_term_patterns = {
            term: self._compile_term_pattern(term) for term in self.config.requirement_terms
        }

    def crawl(self) -> list[FeedbackRecord]:
        records: list[FeedbackRecord] = []
        seen_urls: set[str] = set()
        self.progress.reset(self._estimate_initial_total_steps())
        for source in self.config.sources:
            self._prepare_headers(source)
            self.progress.status(
                source_name=source.name,
                stage="source",
                detail=f"type={source.source_type}",
            )
            if source.source_type == "reddit_search":
                records.extend(self._crawl_reddit_source(source, seen_urls))
                continue
            if source.source_type == "v2ex_topics":
                records.extend(self._crawl_v2ex_source(source, seen_urls))
                continue
            if source.source_type == "producthunt_reviews":
                records.extend(self._crawl_producthunt_source(source, seen_urls))
                continue
            if source.source_type == "capterra_reviews":
                records.extend(self._crawl_capterra_source(source, seen_urls))
                continue
            if source.source_type == "hn_algolia_search":
                records.extend(self._crawl_hn_algolia_source(source, seen_urls))
                continue
            if source.source_type == "stackoverflow_search":
                records.extend(self._crawl_stackoverflow_source(source, seen_urls))
                continue
            if source.source_type == "github_issues_search":
                records.extend(self._crawl_github_issues_source(source, seen_urls))
                continue
            if source.source_type == "duckduckgo_html_search":
                records.extend(self._crawl_duckduckgo_html_source(source, seen_urls))
                continue
            task_results = self._run_tasks_in_pool(
                self._resolve_max_workers(source),
                [(source, start_url, seen_urls) for start_url in source.start_urls],
                self._crawl_generic_source,
            )
            for batch in task_results:
                records.extend(batch)
        self.progress.finish()
        return records

    def _estimate_initial_total_steps(self) -> int:
        total_steps = 0
        for source in self.config.sources:
            if source.source_type == "reddit_search":
                total_steps += len(source.subreddits or [""]) * len(
                    self._build_reddit_queries(source)
                )
                continue
            if source.source_type == "v2ex_topics":
                for start_url in source.start_urls:
                    if self._looks_like_v2ex_topic_url(start_url):
                        total_steps += 1
                    else:
                        total_steps += 1
                continue
            if source.source_type in {"producthunt_reviews", "capterra_reviews"}:
                total_steps += len(source.start_urls)
                continue
            if source.source_type in {"hn_algolia_search", "stackoverflow_search"}:
                total_steps += len(self._build_plain_search_queries(source))
                continue
            if source.source_type == "github_issues_search":
                total_steps += len(self._build_plain_search_queries(source))
                continue
            if source.source_type == "duckduckgo_html_search":
                total_steps += len(self._build_plain_search_queries(source)) * max(
                    len(source.search_domains or []), 1
                )
                continue
            total_steps += len(source.start_urls)
        return total_steps

    def _get_session(self) -> requests.Session:
        session = getattr(self._thread_local, "session", None)
        if session is None:
            session = requests.Session()
            self._thread_local.session = session
        session.headers.clear()
        session.headers.update(DEFAULT_HEADERS)
        session.headers.update(getattr(self._thread_local, "headers", {}))
        return session

    def _prepare_headers(self, source: SourceConfig) -> None:
        self._thread_local.headers = dict(source.headers)

    def _resolve_max_workers(self, source: SourceConfig) -> int:
        configured = source.max_workers or self.config.max_workers
        return max(1, int(configured))

    def _run_tasks_in_pool(
        self,
        max_workers: int,
        tasks: list[tuple[Any, ...]],
        worker: Any,
    ) -> list[Any]:
        if not tasks:
            return []
        if max_workers <= 1 or len(tasks) <= 1:
            return [worker(*task) for task in tasks]

        results: list[Any] = []
        with ThreadPoolExecutor(max_workers=max_workers) as executor:
            futures: list[Future[Any]] = [executor.submit(worker, *task) for task in tasks]
            for future in as_completed(futures):
                results.append(future.result())
        return results

    def _build_keyword_variants(self) -> dict[str, list[str]]:
        keyword_variants: dict[str, list[str]] = {}
        for keyword in self.config.keywords:
            variants = {normalize_text(keyword)}
            compact = keyword.replace(" ", "")
            hyphenated = keyword.replace(" ", "-")
            underscored = keyword.replace(" ", "_")
            variants.update(item for item in (compact, hyphenated, underscored) if item)
            variants.update(self.config.keyword_aliases.get(keyword, []))
            keyword_variants[keyword] = sorted(variants)
        return keyword_variants

    def _compile_term_pattern(self, term: str) -> re.Pattern[str]:
        escaped = re.escape(term.lower())
        return re.compile(rf"(?<![a-z0-9]){escaped}(?![a-z0-9])")

    def _crawl_generic_source(
        self,
        source: SourceConfig,
        start_url: str,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        self._prepare_headers(source)
        page_url = start_url
        page_count = 0
        records: list[FeedbackRecord] = []
        while page_url and page_count < source.max_pages:
            self.progress.status(
                source_name=source.name,
                stage="list_page",
                detail=f"url={page_url}",
            )
            soup = self._fetch_soup(page_url)
            if soup is None:
                self.progress.advance(
                    source_name=source.name,
                    stage="list_page",
                    detail=f"url={page_url} fetch_failed",
                )
                break
            page_count += 1
            detail_fetches = 0
            records_before = len(records)
            for item in soup.select(source.item_selector):
                url = extract_link(item, source.link_selector, page_url)
                if url and source.detail_content_selector:
                    detail_fetches += 1
                record = self._extract_generic_record(source, item, page_url)
                if record is None or not self._remember_url(record.url, seen_urls):
                    continue
                records.append(record)
            self.progress.add_total(
                steps=detail_fetches,
                source_name=source.name,
                stage="detail_page",
                detail=f"from={page_url}",
            )
            self.progress.advance(
                source_name=source.name,
                stage="list_page",
                detail=f"url={page_url}",
                records_added=len(records) - records_before,
            )
            next_page = extract_link(soup, source.next_page_selector, page_url)
            if not next_page or next_page == page_url or not same_host(page_url, next_page):
                break
            self.progress.add_total(
                steps=1,
                source_name=source.name,
                stage="list_page",
                detail=f"next={next_page}",
            )
            page_url = next_page
            time.sleep(source.crawl_delay_seconds)
        return records

    def _crawl_reddit_source(
        self,
        source: SourceConfig,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        tasks = [
            (source, subreddit, query, seen_urls)
            for subreddit in (source.subreddits or [""])
            for query in self._build_reddit_queries(source)
        ]
        results = self._run_tasks_in_pool(
            self._resolve_max_workers(source),
            tasks,
            self._crawl_reddit_query,
        )
        records: list[FeedbackRecord] = []
        for batch in results:
            records.extend(batch)
        return records

    def _crawl_reddit_query(
        self,
        source: SourceConfig,
        subreddit: str,
        query: str,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        self._prepare_headers(source)
        records: list[FeedbackRecord] = []
        label = f"subreddit={subreddit or 'all'} query={query}"
        self.progress.status(
            source_name=source.name,
            stage="search",
            detail=label,
        )
        payload = self._fetch_json(
            self._reddit_search_url(subreddit),
            params={
                "q": query,
                "limit": source.search_limit,
                "sort": source.search_sort,
                "t": "all",
                "restrict_sr": "on" if subreddit else "off",
                "raw_json": 1,
            },
        )
        if payload is None:
            self.progress.advance(
                source_name=source.name,
                stage="search",
                detail=f"{label} fetch_failed",
            )
            return records
        children = payload.get("data", {}).get("children", [])
        comment_fetches = 0
        if source.include_comments:
            for child in children:
                permalink = child.get("data", {}).get("permalink", "")
                url = (
                    urljoin("https://www.reddit.com", permalink)
                    if permalink
                    else ""
                )
                if url:
                    comment_fetches += 1
        self.progress.add_total(
            steps=comment_fetches,
            source_name=source.name,
            stage="comments",
            detail=label,
        )
        for child in children:
            record = self._extract_reddit_record(source, child.get("data", {}))
            if record is None or not self._remember_url(record.url, seen_urls):
                continue
            records.append(record)
        self.progress.advance(
            source_name=source.name,
            stage="search",
            detail=f"{label} hits={len(children)}",
            records_added=len(records),
        )
        time.sleep(source.crawl_delay_seconds)
        return records

    def _crawl_v2ex_source(
        self,
        source: SourceConfig,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        records: list[FeedbackRecord] = []
        for start_url in source.start_urls:
            if self._looks_like_v2ex_topic_url(start_url):
                self.progress.status(
                    source_name=source.name,
                    stage="topic",
                    detail=f"url={start_url}",
                )
                record = self._extract_v2ex_topic(source, start_url)
                if record is None or not self._remember_url(record.url, seen_urls):
                    self.progress.advance(
                        source_name=source.name,
                        stage="topic",
                        detail=f"url={start_url} skipped",
                    )
                    continue
                records.append(record)
                self.progress.advance(
                    source_name=source.name,
                    stage="topic",
                    detail=f"url={start_url}",
                    records_added=1,
                )
                continue

            topic_urls = self._collect_v2ex_topic_urls(source, start_url)
            self.progress.add_total(
                steps=sum(1 for topic_url in topic_urls if topic_url not in seen_urls),
                source_name=source.name,
                stage="topic",
                detail=f"from={start_url}",
            )
            task_results = self._run_tasks_in_pool(
                self._resolve_max_workers(source),
                [(source, topic_url, seen_urls) for topic_url in topic_urls],
                self._crawl_v2ex_topic_task,
            )
            for record in task_results:
                if record is not None:
                    records.append(record)
        return records

    def _crawl_v2ex_topic_task(
        self,
        source: SourceConfig,
        topic_url: str,
        seen_urls: set[str],
    ) -> FeedbackRecord | None:
        self._prepare_headers(source)
        if topic_url in seen_urls:
            return None
        self.progress.status(
            source_name=source.name,
            stage="topic",
            detail=f"url={topic_url}",
        )
        record = self._extract_v2ex_topic(source, topic_url)
        if record is None or not self._remember_url(record.url, seen_urls):
            self.progress.advance(
                source_name=source.name,
                stage="topic",
                detail=f"url={topic_url} skipped",
            )
            return None
        self.progress.advance(
            source_name=source.name,
            stage="topic",
            detail=f"url={topic_url}",
            records_added=1,
        )
        time.sleep(source.crawl_delay_seconds)
        return record

    def _crawl_producthunt_source(
        self,
        source: SourceConfig,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        task_results = self._run_tasks_in_pool(
            self._resolve_max_workers(source),
            [(source, start_url, seen_urls) for start_url in source.start_urls],
            self._crawl_producthunt_task,
        )
        records = [record for record in task_results if record is not None]
        return records

    def _crawl_producthunt_task(
        self,
        source: SourceConfig,
        start_url: str,
        seen_urls: set[str],
    ) -> FeedbackRecord | None:
        self._prepare_headers(source)
        self.progress.status(
            source_name=source.name,
            stage="product_page",
            detail=f"url={start_url}",
        )
        record = self._extract_producthunt_record(source, start_url)
        if record is None or not self._remember_url(record.url, seen_urls):
            self.progress.advance(
                source_name=source.name,
                stage="product_page",
                detail=f"url={start_url} skipped",
            )
            return None
        self.progress.advance(
            source_name=source.name,
            stage="product_page",
            detail=f"url={start_url}",
            records_added=1,
        )
        time.sleep(source.crawl_delay_seconds)
        return record

    def _crawl_capterra_source(
        self,
        source: SourceConfig,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        task_results = self._run_tasks_in_pool(
            self._resolve_max_workers(source),
            [(source, start_url, seen_urls) for start_url in source.start_urls],
            self._crawl_capterra_task,
        )
        records = [record for record in task_results if record is not None]
        return records

    def _crawl_capterra_task(
        self,
        source: SourceConfig,
        start_url: str,
        seen_urls: set[str],
    ) -> FeedbackRecord | None:
        self._prepare_headers(source)
        self.progress.status(
            source_name=source.name,
            stage="review_page",
            detail=f"url={start_url}",
        )
        record = self._extract_capterra_record(source, start_url)
        if record is None or not self._remember_url(record.url, seen_urls):
            self.progress.advance(
                source_name=source.name,
                stage="review_page",
                detail=f"url={start_url} skipped",
            )
            return None
        self.progress.advance(
            source_name=source.name,
            stage="review_page",
            detail=f"url={start_url}",
            records_added=1,
        )
        time.sleep(source.crawl_delay_seconds)
        return record

    def _crawl_hn_algolia_source(
        self,
        source: SourceConfig,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        tasks = [
            (source, query, seen_urls)
            for query in self._build_plain_search_queries(source)
        ]
        results = self._run_tasks_in_pool(
            self._resolve_max_workers(source),
            tasks,
            self._crawl_hn_algolia_query,
        )
        records: list[FeedbackRecord] = []
        for batch in results:
            records.extend(batch)
        return records

    def _crawl_hn_algolia_query(
        self,
        source: SourceConfig,
        query: str,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        self._prepare_headers(source)
        records: list[FeedbackRecord] = []
        label = f"query={query}"
        self.progress.status(
            source_name=source.name,
            stage="search",
            detail=label,
        )
        tags = ",".join(source.search_tags or ["story", "comment"])
        payload = self._fetch_json(
            "https://hn.algolia.com/api/v1/search",
            params={
                "query": query,
                "tags": tags,
                "hitsPerPage": source.search_limit,
            },
        )
        if payload is None:
            self.progress.advance(
                source_name=source.name,
                stage="search",
                detail=f"{label} fetch_failed",
            )
            return records
        hits = payload.get("hits", [])
        query_keywords = self._match_keywords(query)
        for hit in hits:
            record = self._extract_hn_algolia_record(
                source,
                hit,
                query=query,
                seeded_keywords=query_keywords,
            )
            if record is None or not self._remember_url(record.url, seen_urls):
                continue
            records.append(record)
        self.progress.advance(
            source_name=source.name,
            stage="search",
            detail=f"{label} hits={len(hits)}",
            records_added=len(records),
        )
        time.sleep(source.crawl_delay_seconds)
        return records

    def _crawl_stackoverflow_source(
        self,
        source: SourceConfig,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        tasks = [
            (source, query, seen_urls)
            for query in self._build_plain_search_queries(source)
        ]
        results = self._run_tasks_in_pool(
            self._resolve_max_workers(source),
            tasks,
            self._crawl_stackoverflow_query,
        )
        records: list[FeedbackRecord] = []
        for batch in results:
            records.extend(batch)
        return records

    def _crawl_stackoverflow_query(
        self,
        source: SourceConfig,
        query: str,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        self._prepare_headers(source)
        records: list[FeedbackRecord] = []
        label = f"query={query}"
        self.progress.status(
            source_name=source.name,
            stage="search",
            detail=label,
        )
        params: dict[str, Any] = {
            "order": "desc",
            "sort": "relevance",
            "q": query,
            "site": source.search_site or "stackoverflow",
            "pagesize": min(max(source.search_limit, 1), 100),
        }
        if source.search_tags:
            params["tagged"] = ";".join(source.search_tags)
        payload = self._fetch_json(
            "https://api.stackexchange.com/2.3/search/excerpts",
            params=params,
        )
        if payload is None:
            self.progress.advance(
                source_name=source.name,
                stage="search",
                detail=f"{label} fetch_failed",
            )
            return records
        items = payload.get("items", [])
        query_keywords = self._match_keywords(query)
        for item in items:
            record = self._extract_stackoverflow_record(
                source,
                item,
                query=query,
                seeded_keywords=query_keywords,
            )
            if record is None or not self._remember_url(record.url, seen_urls):
                continue
            records.append(record)
        self.progress.advance(
            source_name=source.name,
            stage="search",
            detail=f"{label} hits={len(items)}",
            records_added=len(records),
        )
        time.sleep(source.crawl_delay_seconds)
        return records

    def _crawl_github_issues_source(
        self,
        source: SourceConfig,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        tasks = [
            (source, query, seen_urls)
            for query in self._build_plain_search_queries(source)
        ]
        results = self._run_tasks_in_pool(
            self._resolve_max_workers(source),
            tasks,
            self._crawl_github_issues_query,
        )
        records: list[FeedbackRecord] = []
        for batch in results:
            records.extend(batch)
        return records

    def _crawl_github_issues_query(
        self,
        source: SourceConfig,
        query: str,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        self._prepare_headers(source)
        records: list[FeedbackRecord] = []
        label = f"query={query}"
        self.progress.status(
            source_name=source.name,
            stage="search",
            detail=label,
        )
        qualifiers = source.github_search_qualifiers or ["is:issue"]
        q = f"{query} {' '.join(qualifiers)}".strip()
        payload = self._fetch_json(
            "https://api.github.com/search/issues",
            params={
                "q": q,
                "per_page": min(max(source.search_limit, 1), 100),
                "sort": "comments",
                "order": "desc",
            },
        )
        if payload is None:
            self.progress.advance(
                source_name=source.name,
                stage="search",
                detail=f"{label} fetch_failed",
            )
            return records

        items = payload.get("items", []) if isinstance(payload, dict) else []
        query_keywords = self._match_keywords(query)
        repos_filter = set(source.github_repos or [])
        for item in items:
            repo_full_name = normalize_text(item.get("repository_url", "").split("repos/")[-1])
            if repos_filter and repo_full_name and repo_full_name not in repos_filter:
                continue
            record = self._extract_github_issue_record(
                source,
                item,
                query=query,
                seeded_keywords=query_keywords,
            )
            if record is None or not self._remember_url(record.url, seen_urls):
                continue
            records.append(record)

        self.progress.advance(
            source_name=source.name,
            stage="search",
            detail=f"{label} hits={len(items)}",
            records_added=len(records),
        )
        time.sleep(source.crawl_delay_seconds)
        return records

    def _crawl_duckduckgo_html_source(
        self,
        source: SourceConfig,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        domains = source.search_domains or [""]
        tasks = [
            (source, domain, query, seen_urls)
            for domain in domains
            for query in self._build_plain_search_queries(source)
        ]
        results = self._run_tasks_in_pool(
            self._resolve_max_workers(source),
            tasks,
            self._crawl_duckduckgo_html_query,
        )
        records: list[FeedbackRecord] = []
        for batch in results:
            records.extend(batch)
        return records

    def _crawl_duckduckgo_html_query(
        self,
        source: SourceConfig,
        domain: str,
        query: str,
        seen_urls: set[str],
    ) -> list[FeedbackRecord]:
        self._prepare_headers(source)
        records: list[FeedbackRecord] = []
        scoped_query = f"site:{domain} {query}".strip() if domain else query
        label = f"domain={domain or 'all'} query={query}"
        self.progress.status(
            source_name=source.name,
            stage="search",
            detail=label,
        )
        soup = self._fetch_soup(
            "https://html.duckduckgo.com/html/",
            params={"q": scoped_query},
        )
        if soup is None:
            self.progress.advance(
                source_name=source.name,
                stage="search",
                detail=f"{label} fetch_failed",
            )
            return records

        query_keywords = self._match_keywords(query)
        result_nodes = soup.select("div.result")
        if result_nodes:
            for node in result_nodes[: max(source.search_limit, 1)]:
                record = self._extract_duckduckgo_html_record(
                    source,
                    node,
                    query=query,
                    seeded_keywords=query_keywords,
                )
                if record is None or not self._remember_url(record.url, seen_urls):
                    continue
                records.append(record)
        else:
            anchors = soup.select("a.result__a")
            for anchor in anchors[: max(source.search_limit, 1)]:
                record = self._extract_duckduckgo_html_anchor_record(
                    source,
                    anchor,
                    query=query,
                    seeded_keywords=query_keywords,
                )
                if record is None or not self._remember_url(record.url, seen_urls):
                    continue
                records.append(record)

        self.progress.advance(
            source_name=source.name,
            stage="search",
            detail=f"{label} hits={len(records)}",
            records_added=len(records),
        )
        time.sleep(source.crawl_delay_seconds)
        return records

    def _build_reddit_queries(self, source: SourceConfig) -> list[str]:
        queries: list[str] = []
        default_terms = list(
            dict.fromkeys(self.config.review_terms + self.config.requirement_terms)
        )
        search_terms = source.search_terms or default_terms
        for keyword in self.config.keywords:
            queries.append(f'"{keyword}"')
            for search_term in search_terms:
                queries.append(f'"{keyword}" "{search_term}"')
        deduped_queries: list[str] = []
        seen_queries: set[str] = set()
        for query in queries:
            normalized = query.lower()
            if normalized in seen_queries:
                continue
            seen_queries.add(normalized)
            deduped_queries.append(query)
        return deduped_queries

    def _build_plain_search_queries(self, source: SourceConfig) -> list[str]:
        queries: list[str] = []
        default_terms = list(
            dict.fromkeys(self.config.review_terms + self.config.requirement_terms)
        )
        search_terms = source.search_terms or default_terms
        for keyword in self.config.keywords:
            queries.append(keyword)
            for search_term in search_terms:
                queries.append(f"{keyword} {search_term}")
        deduped_queries: list[str] = []
        seen_queries: set[str] = set()
        for query in queries:
            normalized = normalize_text(query).lower()
            if normalized in seen_queries:
                continue
            seen_queries.add(normalized)
            deduped_queries.append(normalize_text(query))
        return deduped_queries

    def _collect_v2ex_topic_urls(self, source: SourceConfig, start_url: str) -> list[str]:
        topic_urls: list[str] = []
        seen_topic_urls: set[str] = set()

        for page_number in range(1, max(source.max_pages, 1) + 1):
            page_url = self._build_v2ex_page_url(start_url, page_number)
            self.progress.status(
                source_name=source.name,
                stage="topic_list",
                detail=f"url={page_url}",
            )
            soup = self._fetch_soup(page_url)
            if soup is None:
                self.progress.advance(
                    source_name=source.name,
                    stage="topic_list",
                    detail=f"url={page_url} fetch_failed",
                )
                break

            for link in soup.select("span.item_title a[href], a.topic-link[href], a[href^='/t/']"):
                href = link.get("href", "").strip()
                if not href:
                    continue
                topic_url = urljoin(page_url, href)
                if not self._looks_like_v2ex_topic_url(topic_url):
                    continue
                if topic_url in seen_topic_urls:
                    continue
                seen_topic_urls.add(topic_url)
                topic_urls.append(topic_url)
                if len(topic_urls) >= source.search_limit:
                    self.progress.advance(
                        source_name=source.name,
                        stage="topic_list",
                        detail=f"url={page_url} topics={len(topic_urls)}",
                    )
                    return topic_urls

            self.progress.advance(
                source_name=source.name,
                stage="topic_list",
                detail=f"url={page_url} topics={len(topic_urls)}",
            )
            if page_number < max(source.max_pages, 1) and len(topic_urls) < source.search_limit:
                next_page_url = self._build_v2ex_page_url(start_url, page_number + 1)
                self.progress.add_total(
                    steps=1,
                    source_name=source.name,
                    stage="topic_list",
                    detail=f"next={next_page_url}",
                )
            time.sleep(source.crawl_delay_seconds)
        return topic_urls

    def _build_v2ex_page_url(self, start_url: str, page_number: int) -> str:
        if page_number <= 1:
            return start_url
        parsed = urlparse(start_url)
        query = dict(parse_qsl(parsed.query, keep_blank_values=True))
        query["p"] = str(page_number)
        return urlunparse(
            (
                parsed.scheme,
                parsed.netloc,
                parsed.path,
                parsed.params,
                urlencode(query),
                parsed.fragment,
            )
        )

    def _looks_like_v2ex_topic_url(self, url: str) -> bool:
        parsed = urlparse(url)
        return parsed.netloc.endswith("v2ex.com") and bool(re.search(r"/t/\d+", parsed.path))

    def _extract_generic_record(
        self,
        source: SourceConfig,
        item: Any,
        page_url: str,
    ) -> FeedbackRecord | None:
        title = extract_text(item, source.title_selector)
        url = extract_link(item, source.link_selector, page_url)
        snippet = extract_text(item, source.snippet_selector)
        author = extract_text(item, source.author_selector)
        published_at = extract_text(item, source.published_at_selector)
        content = snippet

        if url and source.detail_content_selector:
            self.progress.status(
                source_name=source.name,
                stage="detail_page",
                detail=f"url={url}",
            )
            detail_soup = self._fetch_soup(url)
            if detail_soup is not None:
                content = extract_text(detail_soup, source.detail_content_selector) or snippet
                time.sleep(source.crawl_delay_seconds)
            self.progress.advance(
                source_name=source.name,
                stage="detail_page",
                detail=f"url={url}",
            )

        return self._build_feedback_record(
            source_name=source.name,
            url=url or page_url,
            title=title,
            snippet=snippet,
            content=content,
            author=author,
            published_at=published_at,
            comments=[],
        )

    def _extract_reddit_record(
        self,
        source: SourceConfig,
        post: dict[str, Any],
    ) -> FeedbackRecord | None:
        title = normalize_text(post.get("title"))
        snippet = normalize_text(post.get("selftext"))
        permalink = post.get("permalink", "")
        url = urljoin("https://www.reddit.com", permalink) if permalink else ""
        author = normalize_text(post.get("author"))
        published_at = self._format_timestamp(post.get("created_utc"))
        comments: list[str] = []

        if source.include_comments and url:
            comments = self._fetch_reddit_comments(url, source.max_comments)
            if comments:
                time.sleep(source.crawl_delay_seconds)
            self.progress.advance(
                source_name=source.name,
                stage="comments",
                detail=f"url={url} comments={len(comments)}",
            )

        content_parts = [snippet] if snippet else []
        if comments:
            content_parts.extend(f"[comment] {item}" for item in comments)
        content = normalize_text(" ".join(content_parts))
        return self._build_feedback_record(
            source_name=source.name,
            url=url,
            title=title,
            snippet=snippet,
            content=content,
            author=author,
            published_at=published_at,
            comments=comments,
        )

    def _extract_v2ex_topic(
        self,
        source: SourceConfig,
        topic_url: str,
    ) -> FeedbackRecord | None:
        soup = self._fetch_soup(topic_url)
        if soup is None:
            return None

        title = normalize_text(
            _first_text(
                soup,
                [
                    "h1",
                    ".header h1",
                    "#Main .box .header h1",
                ],
            )
        )
        snippet = normalize_text(
            _first_text(
                soup,
                [
                    ".topic_content",
                    ".topic_content > div",
                    ".markdown_body",
                ],
            )
        )
        author = normalize_text(
            _first_text(
                soup,
                [
                    "small.gray a[href^='/member/']",
                    ".header small.gray a",
                    ".topic_buttons + small.gray a",
                ],
            )
        )
        published_at = normalize_text(
            _extract_v2ex_published_at(
                soup.select_one("small.gray")
                or soup.select_one(".header small.gray")
                or soup.select_one(".topic_buttons + small.gray")
            )
        )
        comments = _extract_v2ex_comments(soup, source.max_comments)
        content_parts = [snippet] if snippet else []
        content_parts.extend(f"[comment] {item}" for item in comments)
        content = normalize_text(" ".join(content_parts))

        return self._build_feedback_record(
            source_name=source.name,
            url=topic_url,
            title=title,
            snippet=snippet,
            content=content,
            author=author,
            published_at=published_at,
            comments=comments,
        )

    def _extract_producthunt_record(
        self,
        source: SourceConfig,
        product_url: str,
    ) -> FeedbackRecord | None:
        soup = self._fetch_soup(product_url)
        if soup is None:
            return None

        title = normalize_text(_first_text(soup, ["h1"]))
        snippet = normalize_text(
            " ".join(
                item
                for item in (
                    _first_text(soup, ["h2"]),
                    _extract_producthunt_description(soup),
                )
                if item
            )
        )
        published_at = normalize_text(_extract_producthunt_launched_at(soup))
        summary_text = normalize_text(_extract_producthunt_summary_text(soup))
        reviews = _extract_producthunt_review_bodies(soup, source.max_comments)
        content_parts = [summary_text or snippet] if (summary_text or snippet) else []
        content_parts.extend(f"[comment] {item}" for item in reviews)
        content = normalize_text(" ".join(content_parts))

        return self._build_feedback_record(
            source_name=source.name,
            url=product_url,
            title=title,
            snippet=snippet,
            content=content,
            author="Product Hunt reviewers",
            published_at=published_at,
            comments=reviews,
        )

    def _extract_capterra_record(
        self,
        source: SourceConfig,
        review_url: str,
    ) -> FeedbackRecord | None:
        soup = self._fetch_soup(review_url)
        if soup is None:
            return None

        title = normalize_text(
            _first_text(
                soup,
                [
                    "h1",
                    "[data-testid='product-name']",
                ],
            )
        )
        snippet = normalize_text(
            " ".join(
                item
                for item in (
                    _extract_capterra_description(soup),
                    _extract_capterra_sentiment_summary(soup),
                )
                if item
            )
        )
        published_at = normalize_text(_extract_capterra_last_updated(soup))
        reviews = _extract_capterra_review_bodies(soup, source.max_comments)
        content_parts = [snippet] if snippet else []
        content_parts.extend(f"[comment] {item}" for item in reviews)
        content = normalize_text(" ".join(content_parts))

        return self._build_feedback_record(
            source_name=source.name,
            url=review_url,
            title=title,
            snippet=snippet,
            content=content,
            author="Capterra reviewers",
            published_at=published_at,
            comments=reviews,
        )

    def _extract_hn_algolia_record(
        self,
        source: SourceConfig,
        hit: dict[str, Any],
        query: str = "",
        seeded_keywords: list[str] | None = None,
    ) -> FeedbackRecord | None:
        title = normalize_text(
            hit.get("title")
            or hit.get("story_title")
            or hit.get("comment_text")
            or ""
        )
        snippet = normalize_text(
            " ".join(
                item
                for item in (
                    _html_to_text(hit.get("story_text", "")),
                    _html_to_text(hit.get("comment_text", "")),
                )
                if item
            )
        )
        url = normalize_text(
            hit.get("url")
            or hit.get("story_url")
            or (
                f"https://news.ycombinator.com/item?id={hit.get('objectID')}"
                if hit.get("objectID")
                else ""
            )
        )
        author = normalize_text(hit.get("author"))
        published_at = normalize_text(hit.get("created_at"))
        return self._build_feedback_record(
            source_name=source.name,
            url=url,
            title=title,
            snippet=snippet,
            content=normalize_text(" ".join(item for item in (title, snippet) if item)),
            author=author or "Hacker News user",
            published_at=published_at,
            comments=[],
            seeded_keywords=seeded_keywords,
        )

    def _extract_stackoverflow_record(
        self,
        source: SourceConfig,
        item: dict[str, Any],
        query: str = "",
        seeded_keywords: list[str] | None = None,
    ) -> FeedbackRecord | None:
        title = normalize_text(_html_to_text(item.get("title", "")))
        excerpt = normalize_text(_html_to_text(item.get("excerpt", "")))
        tags = [normalize_text(tag) for tag in item.get("tags", []) if normalize_text(tag)]
        snippet = normalize_text(" ".join(part for part in (excerpt, " ".join(tags)) if part))
        link = normalize_text(item.get("link")) or _build_stackexchange_link(
            source.search_site or "stackoverflow",
            item,
        )
        author = normalize_text(item.get("owner", {}).get("display_name")) or "Stack Overflow user"
        published_at = self._format_timestamp(item.get("creation_date"))
        content = normalize_text(" ".join(part for part in (title, snippet, query) if part))
        return self._build_feedback_record(
            source_name=source.name,
            url=link,
            title=title,
            snippet=snippet,
            content=content,
            author=author,
            published_at=published_at,
            comments=[],
            seeded_keywords=seeded_keywords,
        )

    def _extract_github_issue_record(
        self,
        source: SourceConfig,
        item: dict[str, Any],
        query: str = "",
        seeded_keywords: list[str] | None = None,
    ) -> FeedbackRecord | None:
        title = normalize_text(item.get("title", ""))
        body = item.get("body") or ""
        snippet = normalize_text(body[:1200])
        url = normalize_text(item.get("html_url", ""))
        author = normalize_text(item.get("user", {}).get("login")) or "GitHub user"
        published_at = normalize_text(item.get("created_at", ""))
        content = normalize_text(" ".join(part for part in (title, snippet, query) if part))
        return self._build_feedback_record(
            source_name=source.name,
            url=url,
            title=title,
            snippet=snippet,
            content=content,
            author=author,
            published_at=published_at,
            comments=[],
            seeded_keywords=seeded_keywords,
        )

    def _extract_duckduckgo_html_record(
        self,
        source: SourceConfig,
        node: Any,
        query: str = "",
        seeded_keywords: list[str] | None = None,
    ) -> FeedbackRecord | None:
        title_node = node.select_one("a.result__a")
        title = normalize_text(title_node.get_text(" ", strip=True) if title_node else "")
        url = _extract_duckduckgo_result_url(title_node.get("href", "") if title_node else "")
        snippet = normalize_text(
            _first_text(
                node,
                [
                    ".result__snippet",
                    ".result__extras__url",
                ],
            )
        )
        if not self._is_relevant_duckduckgo_result(title, snippet, query):
            return None
        return self._build_feedback_record(
            source_name=source.name,
            url=url,
            title=title,
            snippet=snippet,
            content=normalize_text(" ".join(part for part in (title, snippet, query) if part)),
            author="DuckDuckGo result",
            published_at="",
            comments=[],
            seeded_keywords=seeded_keywords,
        )

    def _extract_duckduckgo_html_anchor_record(
        self,
        source: SourceConfig,
        anchor: Any,
        query: str = "",
        seeded_keywords: list[str] | None = None,
    ) -> FeedbackRecord | None:
        title = normalize_text(anchor.get_text(" ", strip=True))
        url = _extract_duckduckgo_result_url(anchor.get("href", ""))
        parent = anchor.parent
        snippet = normalize_text(
            _first_text(
                parent if parent is not None else anchor,
                [
                    ".result__snippet",
                ],
            )
        )
        if not self._is_relevant_duckduckgo_result(title, snippet, query):
            return None
        return self._build_feedback_record(
            source_name=source.name,
            url=url,
            title=title,
            snippet=snippet,
            content=normalize_text(" ".join(part for part in (title, snippet, query) if part)),
            author="DuckDuckGo result",
            published_at="",
            comments=[],
            seeded_keywords=seeded_keywords,
        )

    def _is_relevant_duckduckgo_result(
        self,
        title: str,
        snippet: str,
        query: str,
    ) -> bool:
        combined = normalize_text(" ".join(part for part in (title, snippet) if part)).lower()
        if not combined:
            return False

        query_parts = normalize_text(query).split()
        query_keyword_matches = self._match_keywords(query)
        content_keyword_matches = self._match_keywords(title, snippet)
        requirement_matches = self._match_requirement_terms(title, snippet, query)

        noise_markers = (
            "minecraft",
            "libpng",
            "lodop",
            "打印机",
            "回单机",
            "r包",
            "deepseek",
            "gemini",
            "豆包",
            "免费ai",
            "降ai",
            "课程资源",
            "github课程",
            "高校github",
        )
        if any(marker in combined for marker in noise_markers):
            return False

        if not content_keyword_matches:
            return False
        if not requirement_matches:
            return False

        non_keyword_parts = [
            part
            for part in query_parts
            if part and part not in {keyword.lower() for keyword in query_keyword_matches}
        ]
        if non_keyword_parts and not any(part.lower() in combined for part in non_keyword_parts):
            return False

        return True

    def _build_feedback_record(
        self,
        source_name: str,
        url: str,
        title: str,
        snippet: str,
        content: str,
        author: str,
        published_at: str,
        comments: list[str],
        seeded_keywords: list[str] | None = None,
    ) -> FeedbackRecord | None:
        matched_keywords = _sorted_unique((seeded_keywords or []) + self._match_keywords(title, snippet, content))
        if not matched_keywords:
            return None

        tool_mentions = self._extract_tool_mentions(
            matched_keywords=matched_keywords,
            title=title,
            snippet=snippet,
            content=content,
            comments=comments,
        )
        matched_review_terms = self._match_review_terms(title, snippet, content)
        matched_requirement_terms = self._match_requirement_terms(title, snippet, content)
        requirement_signals = self._extract_requirement_signals(
            matched_keywords=matched_keywords,
            title=title,
            snippet=snippet,
            content=content,
            comments=comments,
        )
        if (
            (self.config.review_terms or self.config.requirement_terms)
            and not matched_review_terms
            and not matched_requirement_terms
            and not tool_mentions
            and not requirement_signals
        ):
            return None

        return FeedbackRecord(
            source=source_name,
            url=url,
            title=title,
            snippet=snippet,
            content=content,
            author=author,
            published_at=published_at,
            matched_keywords=matched_keywords,
            matched_review_terms=matched_review_terms,
            matched_requirement_terms=matched_requirement_terms,
            overall_sentiment=self._summarize_overall_sentiment(tool_mentions),
            comment_count=len(comments),
            is_question=self._looks_like_question(" ".join([title, snippet])),
            tool_mentions=tool_mentions,
            requirement_signals=requirement_signals,
        )

    def _remember_url(self, url: str, seen_urls: set[str]) -> bool:
        if not url:
            return False
        with self._seen_urls_lock:
            if url in seen_urls:
                return False
            seen_urls.add(url)
            return True

    def _match_keywords(self, *values: str) -> list[str]:
        text = normalize_text(" ".join(values)).lower()
        matched_keywords: list[str] = []
        for keyword, patterns in self.keyword_patterns.items():
            if any(pattern.search(text) for pattern in patterns):
                matched_keywords.append(keyword)
        return matched_keywords

    def _match_review_terms(self, *values: str) -> list[str]:
        text = normalize_text(" ".join(values)).lower()
        matched_terms: list[str] = []
        for term, pattern in self.review_term_patterns.items():
            if pattern.search(text):
                matched_terms.append(term)
        return matched_terms

    def _match_requirement_terms(self, *values: str) -> list[str]:
        text = normalize_text(" ".join(values)).lower()
        matched_terms: list[str] = []
        for term, pattern in self.requirement_term_patterns.items():
            if pattern.search(text):
                matched_terms.append(term)
        return matched_terms

    def _extract_tool_mentions(
        self,
        matched_keywords: list[str],
        title: str,
        snippet: str,
        content: str,
        comments: list[str],
    ) -> list[ToolMention]:
        units = self._build_evidence_units(title, snippet, content, comments)
        single_tool = len(matched_keywords) == 1
        mentions: list[ToolMention] = []

        for keyword in matched_keywords:
            pros = self._collect_bucket(
                units,
                POSITIVE_MARKERS,
                keyword,
                single_tool,
                bucket_kind="sentiment",
            )
            cons = self._collect_bucket(
                units,
                NEGATIVE_MARKERS,
                keyword,
                single_tool,
                bucket_kind="sentiment",
            )
            recommendations = self._collect_bucket(
                units,
                RECOMMENDATION_MARKERS,
                keyword,
                single_tool,
                bucket_kind="recommendation",
            )
            experience_reports = self._collect_bucket(
                units,
                EXPERIENCE_MARKERS,
                keyword,
                single_tool,
                bucket_kind="experience",
            )

            evidence_count = len({*pros, *cons, *recommendations, *experience_reports})
            if evidence_count == 0:
                continue

            mentions.append(
                ToolMention(
                    tool=keyword,
                    sentiment_label=self._summarize_sentiment(pros, cons),
                    pros=pros,
                    cons=cons,
                    recommendations=recommendations,
                    experience_reports=experience_reports,
                    evidence_count=evidence_count,
                )
            )
        return mentions

    def _extract_requirement_signals(
        self,
        matched_keywords: list[str],
        title: str,
        snippet: str,
        content: str,
        comments: list[str],
    ) -> list[RequirementSignal]:
        units = self._build_evidence_units(title, snippet, content, comments)
        single_tool = len(matched_keywords) == 1
        signals: list[RequirementSignal] = []
        seen_keys: set[tuple[str, str, str]] = set()

        for unit in units:
            lower = unit.lower()
            mentioned_tools = [
                keyword for keyword in matched_keywords if self._unit_mentions_keyword(unit, keyword)
            ]
            if not mentioned_tools and not single_tool:
                continue
            if not self._is_relevant_requirement_unit(
                unit=unit,
                lower=lower,
                mentioned_tools=mentioned_tools,
                single_tool=single_tool,
            ):
                continue

            category = _infer_requirement_category(unit)
            signal_type = _classify_requirement_signal(unit)
            tools = mentioned_tools or (matched_keywords[:1] if single_tool else [])
            summary = _extract_requirement_summary(unit, tools[0] if tools else "")
            key = (category, signal_type, _normalize_evidence_text(summary))
            if key in seen_keys:
                continue
            seen_keys.add(key)
            signals.append(
                RequirementSignal(
                    category=category,
                    signal_type=signal_type,
                    summary=summary,
                    evidence=unit,
                    tools=tools,
                )
            )
        return signals[:8]

    def _is_relevant_requirement_unit(
        self,
        unit: str,
        lower: str,
        mentioned_tools: list[str],
        single_tool: bool,
    ) -> bool:
        has_core_marker = (
            _contains_any_marker(lower, DESIRED_FEATURE_MARKERS)
            or _contains_any_marker(lower, PAIN_POINT_MARKERS)
            or _contains_any_marker(lower, CONSTRAINT_MARKERS)
            or _contains_any_marker(lower, USAGE_SCENARIO_MARKERS)
        )
        has_context = _contains_requirement_context_signal(unit)
        if mentioned_tools:
            return has_core_marker or has_context or self._looks_like_question(unit)
        if not single_tool:
            return False
        return has_core_marker or has_context

    def _build_evidence_units(
        self,
        title: str,
        snippet: str,
        content: str,
        comments: list[str],
    ) -> list[str]:
        units: list[str] = []
        for value in (title, snippet):
            if value:
                units.extend(self._split_sentences(value))

        if comments:
            for comment in comments:
                units.extend(self._split_sentences(comment))
        elif content:
            units.extend(self._split_sentences(content))

        cleaned_units = [
            unit
            for unit in (_clean_text_fragment(item) for item in units)
            if unit and unit.lower() not in {"[deleted]", "[removed]"}
        ]
        return _sorted_unique(cleaned_units)

    def _split_sentences(self, value: str) -> list[str]:
        normalized = normalize_text(value)
        if not normalized:
            return []
        parts = re.split(r"(?<=[.!?])\s+|\s+\[comment\]\s+", normalized)
        clauses: list[str] = []
        for item in parts:
            clauses.extend(_split_feedback_clauses(item))
        return [normalize_text(item) for item in clauses if normalize_text(item)]

    def _collect_bucket(
        self,
        units: list[str],
        markers: tuple[str, ...],
        keyword: str,
        single_tool: bool,
        bucket_kind: str,
    ) -> list[str]:
        matched: list[str] = []
        for unit in units:
            lower = unit.lower()
            if self._looks_like_question(unit):
                continue
            mentions_keyword = self._unit_mentions_keyword(unit, keyword)
            if not single_tool and not mentions_keyword:
                continue
            if not _contains_any_marker(lower, markers):
                continue
            if not self._is_relevant_feedback_unit(
                unit=unit,
                keyword=keyword,
                single_tool=single_tool,
                mentions_keyword=mentions_keyword,
                bucket_kind=bucket_kind,
            ):
                continue
            matched.append(unit)
        return _sorted_unique(matched)[:3]

    def _is_relevant_feedback_unit(
        self,
        unit: str,
        keyword: str,
        single_tool: bool,
        mentions_keyword: bool,
        bucket_kind: str,
    ) -> bool:
        if mentions_keyword:
            return True
        if not single_tool:
            return False
        if bucket_kind == "recommendation":
            return True
        if bucket_kind == "experience":
            return _contains_any_marker(unit, EXPERIENCE_MARKERS)
        return _contains_feedback_context_signal(unit)

    def _unit_mentions_keyword(self, unit: str, keyword: str) -> bool:
        lower = unit.lower()
        return any(pattern.search(lower) for pattern in self.keyword_patterns[keyword])

    def _summarize_sentiment(self, pros: list[str], cons: list[str]) -> str:
        if pros and cons:
            return "mixed"
        if pros:
            return "positive"
        if cons:
            return "negative"
        return "neutral"

    def _summarize_overall_sentiment(self, tool_mentions: list[ToolMention]) -> str:
        sentiments = {item.sentiment_label for item in tool_mentions if item.sentiment_label}
        if not sentiments:
            return "neutral"
        if len(sentiments) == 1:
            return sentiments.pop()
        if {"positive", "negative"} & sentiments:
            return "mixed"
        if "mixed" in sentiments:
            return "mixed"
        return "neutral"

    def _looks_like_question(self, text: str) -> bool:
        normalized = normalize_text(text).lower()
        if not normalized:
            return False
        if "?" in normalized:
            return True
        return any(marker in normalized for marker in QUESTION_MARKERS)

    def _reddit_search_url(self, subreddit: str) -> str:
        if subreddit:
            return f"https://www.reddit.com/r/{subreddit}/search.json"
        return "https://www.reddit.com/search.json"

    def _fetch_reddit_comments(self, post_url: str, max_comments: int) -> list[str]:
        payload = self._fetch_json(
            f"{post_url}.json",
            params={"limit": max_comments, "sort": "top", "raw_json": 1},
        )
        if not isinstance(payload, list) or len(payload) < 2:
            return []
        comments_listing = payload[1]
        children = comments_listing.get("data", {}).get("children", [])
        comments: list[str] = []
        for child in children:
            if child.get("kind") != "t1":
                continue
            body = normalize_text(child.get("data", {}).get("body"))
            if not body or body.lower() in {"[deleted]", "[removed]"}:
                continue
            comments.append(body)
            if len(comments) >= max_comments:
                break
        return comments

    def _format_timestamp(self, raw_timestamp: Any) -> str:
        if raw_timestamp in (None, ""):
            return ""
        try:
            timestamp = float(raw_timestamp)
        except (TypeError, ValueError):
            return ""
        return datetime.fromtimestamp(timestamp, tz=timezone.utc).isoformat()

    def _fetch_soup(
        self,
        url: str,
        params: dict[str, Any] | None = None,
    ) -> BeautifulSoup | None:
        try:
            response = self._get_session().get(
                url,
                params=params,
                timeout=self.config.request_timeout_seconds,
            )
            response.raise_for_status()
        except requests.RequestException:
            return None
        return BeautifulSoup(response.text, "html.parser")

    def _fetch_json(
        self,
        url: str,
        params: dict[str, Any] | None = None,
    ) -> dict[str, Any] | list[Any] | None:
        try:
            response = self._get_session().get(
                url,
                params=params,
                timeout=self.config.request_timeout_seconds,
            )
            response.raise_for_status()
        except requests.RequestException:
            return None
        try:
            return response.json()
        except ValueError:
            return None


def _first_text(soup: BeautifulSoup, selectors: list[str]) -> str:
    for selector in selectors:
        node = soup.select_one(selector)
        if node is None:
            continue
        text = normalize_text(node.get_text(" ", strip=True))
        if text:
            return text
    return ""


def _extract_v2ex_published_at(node: Any) -> str:
    if node is None:
        return ""
    text = normalize_text(node.get_text(" ", strip=True))
    if not text:
        return ""
    fragments = [fragment.strip() for fragment in text.split("·")]
    if len(fragments) >= 2:
        return fragments[1]
    return text


def _extract_v2ex_comments(soup: BeautifulSoup, max_comments: int) -> list[str]:
    comments: list[str] = []
    for node in soup.select(
        ".reply_content",
    ):
        text = normalize_text(node.get_text(" ", strip=True))
        if not text or text.lower() in {"[deleted]", "[removed]"}:
            continue
        comments.append(text)
        if len(comments) >= max_comments:
            break
    return comments


def _build_stackexchange_link(site: str, item: dict[str, Any]) -> str:
    link = normalize_text(item.get("link"))
    if link:
        return link

    question_id = item.get("question_id")
    if question_id:
        return f"https://{site}.stackexchange.com/questions/{question_id}"

    answer_id = item.get("answer_id")
    if answer_id:
        return f"https://{site}.stackexchange.com/a/{answer_id}"

    return ""


def _extract_duckduckgo_result_url(href: str) -> str:
    normalized = normalize_text(href)
    if not normalized:
        return ""
    if normalized.startswith("//"):
        normalized = f"https:{normalized}"
    parsed = urlparse(normalized)
    if "duckduckgo.com" not in parsed.netloc:
        return normalized
    params = dict(parse_qsl(parsed.query, keep_blank_values=True))
    target = normalize_text(params.get("uddg"))
    return target or normalized


def _normalized_text_lines(soup: BeautifulSoup) -> list[str]:
    lines = [
        normalize_text(line)
        for line in soup.get_text("\n", strip=True).splitlines()
    ]
    return [line for line in lines if line]


def _extract_producthunt_description(soup: BeautifulSoup) -> str:
    for selector in ("meta[name='description']", "meta[property='og:description']"):
        node = soup.select_one(selector)
        if node is None:
            continue
        content = normalize_text(node.get("content"))
        if content:
            return content
    return ""


def _extract_producthunt_launched_at(soup: BeautifulSoup) -> str:
    lines = _normalized_text_lines(soup)
    for line in lines:
        if "Launched in " in line:
            return line
    return ""


def _extract_producthunt_summary_text(soup: BeautifulSoup) -> str:
    lines = _normalized_text_lines(soup)
    capture = False
    collected: list[str] = []
    stop_markers = {
        "Pros",
        "Cons",
        "Reviews",
        "All Reviews",
        "Most Informative",
        "Most Recent",
        "Highest Rated",
        "Lowest Rated",
        "View all",
    }

    for line in lines:
        if line == "Leave a review":
            capture = True
            continue
        if not capture:
            continue
        if line in stop_markers:
            break
        if line == "Summarized with AI":
            continue
        if re.fullmatch(r"Based on \d+ reviews?", line):
            continue
        if len(line) < 30:
            continue
        collected.append(line)
        if len(collected) >= 3:
            break
    return " ".join(collected)


def _extract_producthunt_review_bodies(soup: BeautifulSoup, max_comments: int) -> list[str]:
    selector_candidates = (
        "[data-test*='review']",
        "[data-sentry-component*='Review']",
        "article",
    )
    reviews: list[str] = []

    for selector in selector_candidates:
        for node in soup.select(selector):
            text = _extract_producthunt_review_text_from_node(node)
            if not text or text in reviews:
                continue
            reviews.append(text)
            if len(reviews) >= max_comments:
                return reviews
        if reviews:
            return reviews

    lines = _normalized_text_lines(soup)
    in_reviews = False
    boilerplate = {
        "Helpful",
        "Share",
        "Report",
        "Most Informative",
        "Most Recent",
        "Highest Rated",
        "Lowest Rated",
        "View all",
        "What's great",
        "What needs improvement",
    }
    for line in lines:
        if line == "Reviews":
            in_reviews = True
            continue
        if not in_reviews:
            continue
        if line in boilerplate:
            continue
        if re.fullmatch(r".*•\d+ reviews?", line):
            continue
        if re.search(r"\b\d+\s+views\b", line) or re.search(r"\b(?:hr|day|wk|mo|yr)s?\s+ago\b", line):
            continue
        if len(line) < 40:
            continue
        if _looks_like_ui_noise(line):
            continue
        reviews.append(line)
        if len(reviews) >= max_comments:
            break
    return _sorted_unique(reviews)


def _extract_producthunt_review_text_from_node(node: Any) -> str:
    lines = [
        normalize_text(line)
        for line in node.get_text("\n", strip=True).splitlines()
        if normalize_text(line)
    ]
    if not lines:
        return ""

    boilerplate = {
        "Helpful",
        "Share",
        "Report",
        "What's great",
        "What needs improvement",
        "Most Informative",
        "Most Recent",
        "Highest Rated",
        "Lowest Rated",
        "View all",
    }
    filtered: list[str] = []
    for line in lines:
        if line in boilerplate:
            continue
        if re.fullmatch(r".*•\d+ reviews?", line):
            continue
        if re.search(r"\b\d+\s+views\b", line) or re.search(r"\b(?:hr|day|wk|mo|yr)s?\s+ago\b", line):
            continue
        if len(line) < 20:
            continue
        if _looks_like_ui_noise(line):
            continue
        filtered.append(line)
    if not filtered:
        return ""
    return filtered[0]


def _looks_like_ui_noise(line: str) -> bool:
    lower = line.lower()
    return any(
        marker in lower
        for marker in (
            "join to rate",
            "save",
            "visit",
            "upvote",
            "followers",
            "collections",
            "hunt",
        )
    )


def _extract_capterra_description(soup: BeautifulSoup) -> str:
    for selector in ("meta[name='description']", "meta[property='og:description']"):
        node = soup.select_one(selector)
        if node is None:
            continue
        content = normalize_text(node.get("content"))
        if content:
            return content
    return ""


def _extract_capterra_last_updated(soup: BeautifulSoup) -> str:
    lines = _normalized_text_lines(soup)
    for line in lines:
        if "Updated on " in line:
            return line
    return ""


def _extract_capterra_sentiment_summary(soup: BeautifulSoup) -> str:
    lines = _normalized_text_lines(soup)
    collected: list[str] = []
    capture = False
    stop_markers = {
        "Pros and cons",
        "Top features",
        "Reviewer source",
        "Reviewer details",
        "Company size",
        "Industry",
        "Time used",
        "Overall rating",
    }
    for line in lines:
        if line == "Pros and Cons":
            capture = True
            continue
        if not capture:
            continue
        if line in stop_markers:
            break
        if len(line) < 30:
            continue
        if _looks_like_capterra_ui_noise(line):
            continue
        collected.append(line)
        if len(collected) >= 2:
            break
    return " ".join(collected)


def _extract_capterra_review_bodies(soup: BeautifulSoup, max_comments: int) -> list[str]:
    selector_candidates = (
        "[data-testid*='review']",
        "article",
        ".review",
    )
    reviews: list[str] = []
    for selector in selector_candidates:
        for node in soup.select(selector):
            text = _extract_capterra_review_text_from_node(node)
            if not text or text in reviews:
                continue
            reviews.append(text)
            if len(reviews) >= max_comments:
                return reviews
        if reviews:
            return reviews

    lines = _normalized_text_lines(soup)
    in_reviews = False
    for line in lines:
        if line == "Pros and Cons":
            in_reviews = True
            continue
        if not in_reviews:
            continue
        if _looks_like_capterra_ui_noise(line):
            continue
        if len(line) < 40:
            continue
        reviews.append(line)
        if len(reviews) >= max_comments:
            break
    return _sorted_unique(reviews)


def _extract_capterra_review_text_from_node(node: Any) -> str:
    lines = [
        normalize_text(line)
        for line in node.get_text("\n", strip=True).splitlines()
        if normalize_text(line)
    ]
    if not lines:
        return ""

    filtered: list[str] = []
    for line in lines:
        if _looks_like_capterra_ui_noise(line):
            continue
        if re.fullmatch(r"\d+(?:\.\d+)?", line):
            continue
        if len(line) < 20:
            continue
        filtered.append(line)
    if not filtered:
        return ""
    return " ".join(filtered[:3])


def _looks_like_capterra_ui_noise(line: str) -> bool:
    lower = line.lower()
    return any(
        marker in lower
        for marker in (
            "reviewer source",
            "company size",
            "industry",
            "time used",
            "overall rating",
            "ease of use",
            "customer service",
            "features",
            "value for money",
            "likelihood to recommend",
            "show more",
            "show less",
            "visit website",
            "view profile",
            "learn more",
            "updated on",
        )
    )


def _clean_text_fragment(value: str) -> str:
    cleaned = normalize_text(value.replace("[comment]", " "))
    return cleaned.strip(" -|")


def _html_to_text(value: str | None) -> str:
    if not value:
        return ""
    return normalize_text(BeautifulSoup(value, "html.parser").get_text(" ", strip=True))


def _contains_any_marker(text: str, markers: tuple[str, ...]) -> bool:
    lower = normalize_text(text).lower()
    for marker in markers:
        if re.search(r"[\u4e00-\u9fff]", marker):
            if marker in lower:
                return True
            continue
        escaped = re.escape(marker.lower())
        if re.search(rf"(?<![a-z0-9]){escaped}(?![a-z0-9])", lower):
            return True
    return False


def _normalize_evidence_text(text: str) -> str:
    normalized = normalize_text(text).lower()
    normalized = re.sub(r"https?://\S+", "", normalized)
    normalized = normalized.replace("’", "'")
    normalized = re.sub(r"\b(i use|i'm using|i am using|i've used|i have used|i used)\b", "", normalized)
    normalized = re.sub(r"[^a-z0-9\u4e00-\u9fff\s]", " ", normalized)
    normalized = re.sub(r"\s+", " ", normalized).strip()
    return normalized


def _tokenize_evidence_text(text: str) -> set[str]:
    tokens = re.findall(r"[a-z0-9]+|[\u4e00-\u9fff]+", _normalize_evidence_text(text))
    return {
        token
        for token in tokens
        if token not in EVIDENCE_STOPWORDS and len(token) > 1
    }


def _evidence_similarity(text_a: str, text_b: str) -> float:
    tokens_a = _tokenize_evidence_text(text_a)
    tokens_b = _tokenize_evidence_text(text_b)
    if not tokens_a or not tokens_b:
        return 0.0
    overlap = len(tokens_a & tokens_b)
    union = len(tokens_a | tokens_b)
    if union == 0:
        return 0.0
    return overlap / union


def _split_feedback_clauses(text: str) -> list[str]:
    parts = [normalize_text(text)]
    patterns = (
        r"\s+(?:but|however|though|yet)\s+",
        r"\s*(?:;|；)\s*",
        r"\s*(?:但是|不过|然而)\s*",
    )
    for pattern in patterns:
        next_parts: list[str] = []
        for part in parts:
            next_parts.extend(re.split(pattern, part))
        parts = next_parts
    return [part.strip(" ,:：-") for part in parts if part.strip(" ,:：-")]


def _counter_to_ranked_items(counter: Counter[str], limit: int = 5) -> list[dict[str, Any]]:
    clusters: list[dict[str, Any]] = []
    for text, count in counter.most_common():
        matched_cluster: dict[str, Any] | None = None
        normalized_text = _normalize_evidence_text(text)
        for cluster in clusters:
            if (
                normalized_text == cluster["normalized_text"]
                or _evidence_similarity(text, cluster["text"]) >= 0.72
            ):
                matched_cluster = cluster
                break

        if matched_cluster is None:
            clusters.append(
                {
                    "text": text,
                    "normalized_text": normalized_text,
                    "count": count,
                    "variants": 1,
                }
            )
            continue

        matched_cluster["count"] += count
        matched_cluster["variants"] += 1
        if len(text) < len(matched_cluster["text"]):
            matched_cluster["text"] = text
            matched_cluster["normalized_text"] = normalized_text

    ranked = sorted(clusters, key=lambda item: (-item["count"], len(item["text"])))
    return [
        {
            "text": item["text"],
            "count": item["count"],
            "variants": item["variants"],
        }
        for item in ranked[:limit]
    ]


def _build_highlights(items: list[dict[str, Any]], limit: int = 3) -> list[str]:
    return [item["text"] for item in items[:limit]]


def _contains_cjk(text: str) -> bool:
    return bool(re.search(r"[\u4e00-\u9fff]", text))


def _translate_focus_phrase_zh(text: str) -> str:
    translated = normalize_text(text).strip(" .。!！?？'\"")
    replacements = (
        ("word plugin", "Word 插件"),
        ("plugin ecosystem", "插件生态"),
        ("plugin", "插件"),
        ("sync setup", "同步设置"),
        ("sync", "同步"),
        ("collaboration options", "协作功能"),
        ("collaboration", "协作"),
        ("annotation workflow", "批注流程"),
        ("annotation", "批注"),
        ("pdf annotation workflow", "PDF 批注流程"),
        ("pdf annotation", "PDF 批注"),
        ("pdf reader", "PDF 阅读器"),
        ("pdf", "PDF"),
        ("citation workflow", "引用流程"),
        ("citation", "引用"),
        ("google docs", "Google Docs"),
        ("offline support", "离线支持"),
        ("coverage", "结果覆盖"),
        ("niche topics", "细分主题"),
        ("clickable links", "可点击链接"),
        ("links", "链接"),
        ("workflow", "流程"),
        ("merge duplicates", "重复条目合并"),
        ("duplicate merge flow", "重复条目合并流程"),
        ("duplicates flow", "重复条目处理流程"),
        ("duplicate handling", "重复条目处理"),
        ("duplicate", "重复条目"),
        ("library", "文献库"),
        ("import", "导入"),
        ("export", "导出"),
        ("search", "搜索"),
        ("results", "结果"),
        ("ui", "界面"),
        ("ux", "交互"),
        ("interface", "界面"),
        ("performance", "性能"),
        ("speed", "速度"),
    )
    lower = translated.lower()
    for source, target in replacements:
        lower = lower.replace(source, target)
    lower = re.sub(r"\bthe\b", "", lower)
    lower = re.sub(r"\s+", " ", lower).strip(" -:：")
    return lower or translated


def _extract_focus_phrase(reason: str, tool: str) -> str:
    normalized = normalize_text(reason).strip(" .。!！?？")
    if not normalized:
        return ""
    if _contains_cjk(normalized):
        fragments = re.split(r"[，,；;。.!?？]", normalized)
        return normalize_text(fragments[0]) if fragments else normalized

    patterns = (
        r"^(?:the\s+)?(?P<subject>.+?)\s+(?:can\s+be|could\s+be|is|are|feels|feel|seems|seem|remains|remain|stays|stay)\s+.+$",
        r"^(?:the\s+)?(?P<subject>.+?)\s+(?:still\s+)?(?:limited|buggy|slow|confusing|clunky|annoying|useful|helpful|stable|reliable|smooth)\b.*$",
    )
    for pattern in patterns:
        match = re.match(pattern, normalized, flags=re.IGNORECASE)
        if not match:
            continue
        subject = normalize_text(match.group("subject"))
        if not subject:
            continue
        subject = re.sub(rf"^{re.escape(tool)}(?:'s)?\s+", "", subject, flags=re.IGNORECASE)
        subject = re.sub(r"^(?:it|this|that)\s+", "", subject, flags=re.IGNORECASE)
        subject = re.sub(r"^(?:the|a|an)\s+", "", subject, flags=re.IGNORECASE)
        subject = subject.strip(" -:：")
        if subject:
            return subject
    return ""


def _contains_feedback_context_signal(text: str) -> bool:
    normalized = _normalize_evidence_text(text)
    if not normalized:
        return False
    for _, markers in ASPECT_RULES:
        if any(marker in normalized for marker in markers):
            return True
    return any(
        phrase in normalized
        for phrase in (
            "duplicate merge flow",
            "merge duplicates",
            "duplicate handling",
            "duplicate",
            "literature review",
            "research workflow",
            "daily workflow",
            "google docs",
            "word",
            "subscription",
            "price",
            "pricing",
            "cost",
            "student",
            "grad students",
            "research heavy",
            "systematic review",
            "meta analysis",
            "写论文",
            "做文献综述",
            "日常使用",
        )
    )


def _contains_requirement_context_signal(text: str) -> bool:
    normalized = _normalize_evidence_text(text)
    if not normalized:
        return False
    for _, markers in REQUIREMENT_CATEGORY_RULES:
        if any(marker in normalized for marker in markers):
            return True
    return any(
        phrase in normalized
        for phrase in (
            "switch between",
            "same folder",
            "real time",
            "real-time",
            "mvp",
            "local first",
            "format as code",
            "last mile",
            "offline model",
            "research workflow",
            "academic workflow",
            "paper writing",
            "citation workflow",
            "export docx",
            "模板导出",
            "本地优先",
            "格式即代码",
            "最后1公里",
            "学术工作流",
            "论文写作",
        )
    )


def _infer_requirement_category(text: str) -> str:
    normalized = _normalize_evidence_text(text)
    best_label = "通用工作流"
    best_score = 0
    for label, markers in REQUIREMENT_CATEGORY_RULES:
        score = sum(1 for marker in markers if marker in normalized)
        if score > best_score:
            best_label = label
            best_score = score
    return best_label


def _classify_requirement_signal(text: str) -> str:
    normalized = normalize_text(text)
    lower = normalized.lower()
    if _contains_any_marker(lower, DESIRED_FEATURE_MARKERS) and not any(
        marker in lower
        for marker in (
            "do not want",
            "don't want",
            "cannot",
            "can't",
            "不想",
            "不能",
            "不希望",
        )
    ):
        return "desired_feature"
    if _contains_any_marker(lower, CONSTRAINT_MARKERS) and any(
        marker in lower
        for marker in (
            "privacy",
            "private",
            "cloud",
            "without internet",
            "linux",
            "windows",
            "cross-platform",
            "do not want",
            "don't want",
            "cannot",
            "can't",
            "不想",
            "不能",
            "不希望",
            "隐私",
        )
    ):
        return "constraint"
    if _contains_any_marker(lower, PAIN_POINT_MARKERS):
        return "pain_point"
    if _contains_any_marker(lower, CONSTRAINT_MARKERS):
        return "constraint"
    if _contains_any_marker(lower, USAGE_SCENARIO_MARKERS) or "?" in normalized:
        return "usage_scenario"
    return "usage_scenario"


def _extract_requirement_summary(text: str, tool: str) -> str:
    summary = normalize_text(text)
    patterns = [
        r"^(?:i\s+)?(?:need|needs|want|wanted|wish)\s+",
        r"^(?:i\s+am\s+)?looking for\s+",
        r"^(?:i\s+)?would\s+(?:like|love)\s+",
        r"^(?:it|this|that)\s+(?:should|must|needs to)\s+",
        r"^(?:can|could)\s+(?:it|this|that)\s+",
        r"^(?:feature request\s*[:：-]\s*)",
        r"^(?:希望|需要|想要|最好能|最好|希望有|希望支持|应该支持|能不能|能否)",
    ]
    for pattern in patterns:
        summary = re.sub(pattern, "", summary, flags=re.IGNORECASE)
    if tool:
        summary = re.sub(rf"^{re.escape(tool)}(?:'s)?\s+", "", summary, flags=re.IGNORECASE)
    summary = re.sub(r"```.*?```", " ", summary, flags=re.DOTALL)
    summary = re.sub(r"`[^`]+`", " ", summary)
    summary = re.sub(r"\[[^\]]+\]\((https?://[^)]+)\)", " ", summary)
    summary = re.sub(r"https?://\S+", " ", summary)
    summary = re.sub(r"#{1,6}\s*", " ", summary)
    summary = re.sub(r"[*_~]+", " ", summary)
    summary = re.sub(r"\s*\|\s*", " ", summary)
    summary = normalize_text(summary).strip(" -:：")
    if not summary:
        summary = normalize_text(text)
    return _truncate_requirement_summary(summary, limit=200)


def _truncate_requirement_summary(text: str, limit: int = 200) -> str:
    normalized = normalize_text(text)
    if len(normalized) <= limit:
        return normalized
    if _contains_cjk(normalized):
        return normalized[:limit].rstrip(" ,;:：-。.!?！？")

    truncated = normalized[: limit + 1]
    candidate = truncated.rsplit(" ", 1)[0]
    if not candidate or len(candidate) < (limit // 2):
        candidate = normalized[:limit]
    return candidate.rstrip(" ,;:：-。.!?！？")


def _looks_like_generic_feedback_reason(
    reason: str,
    aspect: str,
    tool: str,
) -> bool:
    normalized = normalize_text(reason).strip(" .。!！?？").lower()
    if not normalized:
        return True

    tool_lower = re.escape(tool.lower())
    generic_patterns = (
        rf"^(?:i\s+)?(?:like|love|hate)\s+(?:it|this|that|{tool_lower})$",
        r"^(?:it|this|that)\s+(?:is|was|feels?|seems?)\s+(?:really\s+)?(?:good|great|bad|awful|annoying|helpful|useful|nice|okay|fine|terrible|amazing)$",
        r"^(?:really\s+)?(?:good|great|bad|awful|annoying|helpful|useful|nice|okay|fine|terrible|amazing|useless)$",
        r"^(?:it|this|that)\s+(?:works?|worked)\s+(?:well|great|fine)\s+(?:for me)?$",
        r"^i would like to\b.+$",
        r"^(?:it|this|that)\s+has\s+the\s+same\s+problem$",
        r"^(?:it|this|that)\s+could(?:n't| not)\s+solve\s+the\s+problem$",
        r"^protect your peace$",
    )
    if any(re.fullmatch(pattern, normalized) for pattern in generic_patterns):
        return True

    if "http://" in reason.lower() or "https://" in reason.lower():
        return True

    if aspect != "整体体验":
        return False

    tokens = _tokenize_evidence_text(reason)
    focus = _extract_focus_phrase(reason, tool)
    return len(tokens) < 5 and not focus and not _contains_feedback_context_signal(reason)


def _feedback_specificity_score(
    reason: str,
    aspect: str,
    tool: str,
) -> int:
    score = 0
    if aspect != "整体体验":
        score += 2
    if _extract_focus_phrase(reason, tool):
        score += 2
    if _contains_feedback_context_signal(reason):
        score += 1
    if len(_tokenize_evidence_text(reason)) >= 5:
        score += 1
    if _looks_like_generic_feedback_reason(reason, aspect, tool):
        score -= 4
    return score


def _text_mentions_tool_name(text: str, tool: str) -> bool:
    variants = {
        normalize_text(tool).lower(),
        tool.replace(" ", "").lower(),
        tool.replace(" ", "-").lower(),
        tool.replace(" ", "_").lower(),
    }
    lower = normalize_text(text).lower()
    return any(
        re.search(rf"(?<![a-z0-9]){re.escape(variant)}(?![a-z0-9])", lower)
        for variant in variants
        if variant
    )


def _looks_like_advice_statement(text: str) -> bool:
    lower = normalize_text(text).lower()
    return bool(
        re.match(
            r"^(?:just\s+)?(?:make sure|look for|try|consider|remember|be sure|use|check|start with)\b",
            lower,
        )
    )


def _should_keep_summary_feedback(text: str, tool: str, sentiment: str) -> bool:
    markers = POSITIVE_MARKERS if sentiment == "positive" else NEGATIVE_MARKERS
    if not _contains_any_marker(text, markers):
        return False

    mentions_tool = _text_mentions_tool_name(text, tool)
    if not mentions_tool and not _contains_feedback_context_signal(text):
        return False
    if not mentions_tool and _looks_like_advice_statement(text):
        return False

    aspect = _infer_feedback_aspect(text)
    reason = _extract_feedback_reason(text, tool)
    if _looks_like_generic_feedback_reason(reason, aspect, tool) and not mentions_tool:
        return False
    return True


def _resolve_feedback_aspect_label(aspect: str, reason: str, tool: str) -> str:
    focus = _extract_focus_phrase(reason, tool)
    if focus:
        translated_focus = _translate_focus_phrase_zh(focus)
        if translated_focus:
            return translated_focus
    if aspect != "整体体验":
        return aspect
    return "具体问题"


def _infer_feedback_aspect(text: str) -> str:
    normalized = _normalize_evidence_text(text)
    best_label = "整体体验"
    best_score = 0
    for label, markers in ASPECT_RULES:
        score = sum(1 for marker in markers if marker in normalized)
        if score > best_score:
            best_label = label
            best_score = score
    return best_label


def _extract_feedback_reason(text: str, tool: str) -> str:
    reason = normalize_text(text)
    patterns = (
        rf"^{re.escape(tool)}\s+(?:is|has|helps|makes|feels|works)\s+",
        rf"^{re.escape(tool)}\s*[:：-]\s*",
        r"^(?:it|this)\s+(?:is|has|helps|makes|works)\s+",
        r"^(?:biggest\s+pro\s+is|biggest\s+con\s+is)\s+",
        r"^(?:pros?\s*[:：]|cons?\s*[:：])\s*",
        r"^(?:the\s+best\s+part\s+is|what\s+i\s+like\s+is)\s+",
    )
    for pattern in patterns:
        reason = re.sub(pattern, "", reason, flags=re.IGNORECASE)
    return reason.strip(" -:：") or normalize_text(text)


def _build_specific_feedback(
    counter: Counter[str],
    tool: str,
    sentiment: str,
    limit: int = 5,
) -> list[dict[str, Any]]:
    grouped: dict[tuple[str, str], dict[str, Any]] = {}
    for item in _counter_to_ranked_items(counter, limit=max(limit * 3, 10)):
        aspect = _infer_feedback_aspect(item["text"])
        reason = _extract_feedback_reason(item["text"], tool)
        key = (aspect, _normalize_evidence_text(reason))
        bucket = grouped.setdefault(
            key,
            {
                "aspect": aspect,
                "reason": reason,
                "count": 0,
                "examples": [],
            },
        )
        bucket["count"] += item["count"]
        if item["text"] not in bucket["examples"]:
            bucket["examples"].append(item["text"])

    candidates = list(grouped.values())
    filtered = [
        item
        for item in candidates
        if _feedback_specificity_score(item["reason"], item["aspect"], tool) > 0
    ]
    if filtered:
        candidates = filtered

    ranked = sorted(
        candidates,
        key=lambda item: (
            -item["count"],
            -_feedback_specificity_score(item["reason"], item["aspect"], tool),
            len(item["reason"]),
        ),
    )
    return [
        {
            "aspect": item["aspect"],
            "reason": item["reason"],
            "count": item["count"],
            "examples": item["examples"][:2],
            "summary_zh": _build_feedback_summary_zh(
                tool=tool,
                aspect=item["aspect"],
                reason=item["reason"],
                sentiment=sentiment,
            ),
        }
        for item in ranked[:limit]
    ]


def _build_specific_highlights(items: list[dict[str, Any]], limit: int = 3) -> list[str]:
    return [f"{item['aspect']}: {item['reason']}" for item in items[:limit]]


def _build_feedback_highlights_zh(items: list[dict[str, Any]], limit: int = 3) -> list[str]:
    return [item["summary_zh"] for item in items[:limit] if item.get("summary_zh")]


def _translate_reason_fragment_zh(reason: str, aspect: str, sentiment: str) -> str:
    normalized = normalize_text(reason).strip(" .。!！?？")
    lower = normalized.lower()

    if _contains_cjk(normalized):
        return normalized

    if "easy to use" in lower and "google docs" in lower and "time" in lower:
        return "在 Google Docs 里插入引用时很容易上手，而且更省时间"
    if "easy to use" in lower and "google docs" in lower:
        return "在 Google Docs 里插入引用时很容易上手"
    if "easy to use" in lower and "time" in lower:
        return "上手简单，而且能节省时间"
    if "collaboration" in lower and "limited" in lower:
        return "协作能力比较有限"
    if "sync setup" in lower and "annoying" in lower:
        return "同步设置比较麻烦"
    if "sync" in lower and ("great" in lower or "well" in lower or "stable" in lower):
        return "同步表现比较稳定"
    if ("annotation" in lower or "pdf" in lower) and ("useful" in lower or "helpful" in lower):
        return "PDF 阅读和批注功能比较实用"
    if "offline support" in lower and "limited" in lower:
        return "离线支持比较有限"
    if "coverage" in lower and "limited" in lower:
        return "在细分主题上的覆盖仍然有限"
    if "clickable links" in lower and ("useful" in lower or "helpful" in lower):
        return "可点击链接很实用"
    if "plugin" in lower and ("bug" in lower or "buggy" in lower):
        return "插件稳定性一般，偶尔会出问题"
    if "merge duplicates" in lower and ("clunky" in lower or "confusing" in lower):
        return "重复条目合并流程不够顺手"
    if "duplicate merge flow" in lower and ("clunky" in lower or "confusing" in lower):
        return "重复条目合并流程不够顺手"
    if "citation" in lower and "smooth" in lower:
        return "引用流程比较顺畅"
    if "stable" in lower or "reliable" in lower:
        return "这一块表现比较稳定"
    if "useful" in lower or "helpful" in lower:
        return "这部分功能比较实用"
    if "limited" in lower:
        return "这一块能力还有些受限"
    if "bug" in lower or "buggy" in lower:
        return "这部分偶尔会出问题"
    if "slow" in lower:
        return "速度偏慢"
    if "annoying" in lower:
        return "用起来比较麻烦"
    if "clunky" in lower:
        return "操作不够顺手"
    if "confusing" in lower:
        return "容易让人困惑"
    if "expensive" in lower or "price" in lower or "pricing" in lower or "cost" in lower:
        return "价格偏高，性价比一般"
    if "recommend" in lower and sentiment == "positive":
        return "很多用户愿意推荐给同类人群"
    if "worth it" in lower and sentiment == "positive":
        return "整体来看值得投入"
    if "smooth" in lower:
        return "流程比较顺畅"

    if _contains_cjk(normalized):
        return normalized
    return f"原评论提到“{normalized}”"


def _build_feedback_summary_zh(tool: str, aspect: str, reason: str, sentiment: str) -> str:
    aspect_label = _resolve_feedback_aspect_label(aspect, reason, tool)
    fragment = _translate_reason_fragment_zh(reason, aspect, sentiment).strip("。")
    if sentiment == "positive":
        return f"用户主要喜欢 {tool} 的{aspect_label}，因为{fragment}。"
    return f"用户主要不满意 {tool} 的{aspect_label}，因为{fragment}。"


def _build_requirement_summary_zh(category: str, signal_type: str, summary: str) -> str:
    summary_text = normalize_text(summary).strip("。")
    if signal_type == "desired_feature":
        return f"需求倾向集中在“{category}”：用户希望支持 {summary_text}。"
    if signal_type == "pain_point":
        return f"痛点主要落在“{category}”：用户提到 {summary_text}。"
    if signal_type == "constraint":
        return f"约束条件集中在“{category}”：用户强调 {summary_text}。"
    return f"使用场景集中在“{category}”：用户描述了 {summary_text}。"


def summarize_records(records: list[FeedbackRecord]) -> dict[str, Any]:
    tool_buckets: dict[str, dict[str, Any]] = {}
    requirement_buckets: dict[str, dict[str, Any]] = {}

    for record in records:
        for mention in record.tool_mentions:
            bucket = tool_buckets.setdefault(
                mention.tool,
                {
                    "tool": mention.tool,
                    "record_count": 0,
                    "sentiment_counts": Counter(),
                    "review_terms": Counter(),
                    "pros": Counter(),
                    "cons": Counter(),
                    "recommendations": Counter(),
                    "experience_reports": Counter(),
                    "sample_urls": [],
                },
            )
            bucket["record_count"] += 1
            bucket["sentiment_counts"][mention.sentiment_label] += 1
            bucket["review_terms"].update(record.matched_review_terms)
            bucket["pros"].update(
                item
                for item in mention.pros
                if _should_keep_summary_feedback(item, mention.tool, "positive")
            )
            bucket["cons"].update(
                item
                for item in mention.cons
                if _should_keep_summary_feedback(item, mention.tool, "negative")
            )
            bucket["recommendations"].update(mention.recommendations)
            bucket["experience_reports"].update(mention.experience_reports)
            if record.url and record.url not in bucket["sample_urls"]:
                bucket["sample_urls"].append(record.url)

        for signal in record.requirement_signals:
            bucket = requirement_buckets.setdefault(
                signal.category,
                {
                    "category": signal.category,
                    "record_count": 0,
                    "signal_types": Counter(),
                    "summaries": Counter(),
                    "evidence": [],
                    "sample_urls": [],
                    "tools": Counter(),
                },
            )
            bucket["record_count"] += 1
            bucket["signal_types"][signal.signal_type] += 1
            bucket["summaries"][signal.summary] += 1
            bucket["tools"].update(signal.tools)
            evidence_item = {
                "signal_type": signal.signal_type,
                "summary": signal.summary,
                "evidence": signal.evidence,
                "url": record.url,
                "tools": signal.tools,
                "summary_zh": _build_requirement_summary_zh(
                    signal.category,
                    signal.signal_type,
                    signal.summary,
                ),
            }
            if evidence_item not in bucket["evidence"]:
                bucket["evidence"].append(evidence_item)
            if record.url and record.url not in bucket["sample_urls"]:
                bucket["sample_urls"].append(record.url)

    summaries: list[dict[str, Any]] = []
    for tool in sorted(tool_buckets):
        bucket = tool_buckets[tool]
        top_pros = _counter_to_ranked_items(bucket["pros"])
        top_cons = _counter_to_ranked_items(bucket["cons"])
        top_recommendations = _counter_to_ranked_items(bucket["recommendations"])
        top_experience_reports = _counter_to_ranked_items(bucket["experience_reports"])
        specific_positive_feedback = _build_specific_feedback(bucket["pros"], tool, "positive")
        specific_negative_feedback = _build_specific_feedback(bucket["cons"], tool, "negative")
        summaries.append(
            {
                "tool": tool,
                "record_count": bucket["record_count"],
                "sentiment_counts": dict(bucket["sentiment_counts"]),
                "review_terms": dict(bucket["review_terms"]),
                "top_pros": top_pros,
                "top_cons": top_cons,
                "top_recommendations": top_recommendations,
                "top_experience_reports": top_experience_reports,
                "specific_positive_feedback": specific_positive_feedback,
                "specific_negative_feedback": specific_negative_feedback,
                "positive_highlights": _build_highlights(top_pros),
                "negative_highlights": _build_highlights(top_cons),
                "specific_positive_highlights": _build_specific_highlights(
                    specific_positive_feedback
                ),
                "specific_negative_highlights": _build_specific_highlights(
                    specific_negative_feedback
                ),
                "specific_positive_summaries": _build_feedback_highlights_zh(
                    specific_positive_feedback
                ),
                "specific_negative_summaries": _build_feedback_highlights_zh(
                    specific_negative_feedback
                ),
                "recommendation_highlights": _build_highlights(top_recommendations),
                "experience_highlights": _build_highlights(top_experience_reports),
                "sample_urls": bucket["sample_urls"][:5],
            }
        )

    requirement_summaries: list[dict[str, Any]] = []
    for category in sorted(requirement_buckets):
        bucket = requirement_buckets[category]
        top_signals = _counter_to_ranked_items(bucket["summaries"])
        requirement_summaries.append(
            {
                "category": category,
                "record_count": bucket["record_count"],
                "signal_type_counts": dict(bucket["signal_types"]),
                "top_signals": top_signals,
                "top_tools": dict(bucket["tools"].most_common(5)),
                "signal_highlights": [
                    item["summary_zh"] for item in bucket["evidence"][:5] if item.get("summary_zh")
                ],
                "examples": bucket["evidence"][:5],
                "sample_urls": bucket["sample_urls"][:5],
            }
        )

    return {
        "generated_at": datetime.now(tz=timezone.utc).isoformat(),
        "record_count": len(records),
        "tool_summaries": summaries,
        "requirement_summaries": requirement_summaries,
    }


def build_ml_requirement_analysis(
    records: list[FeedbackRecord],
    cluster_limit: int = 30,
    doc_requirements_path: str | Path | None = None,
) -> dict[str, Any]:
    doc_signals = _build_doc_requirement_signals(doc_requirements_path) if doc_requirements_path else []
    dataset: list[dict[str, Any]] = []
    for record in records:
        for signal in record.requirement_signals:
            evidence = normalize_text(signal.evidence)
            if not _should_include_ml_requirement_signal(signal, evidence):
                continue
            text = normalize_text(
                f"{signal.category} {signal.signal_type} {signal.summary} {evidence}"
            )
            dataset.append(
                {
                    "category": signal.category,
                    "signal_type": signal.signal_type,
                    "summary": signal.summary,
                    "evidence": evidence,
                    "tools": signal.tools,
                    "url": record.url,
                    "text": text,
                }
            )


    if not dataset:
        return {
            "record_count": len(records),
            "signal_count": 0,
            "clusters": [],
            "lean_requirements": [],
            "doc_requirement_signals": [item.as_dict() for item in doc_signals],
        }

    token_docs = [_tokenize_ml_text(item["text"]) for item in dataset]
    doc_freq = Counter()
    for tokens in token_docs:
        doc_freq.update(set(tokens))

    doc_count = len(token_docs)
    vectors = [_build_tfidf_vector(tokens, doc_freq, doc_count) for tokens in token_docs]
    indexed = list(enumerate(vectors))
    indexed.sort(key=lambda item: _vector_norm(item[1]), reverse=True)

    threshold = 0.34
    clusters: list[dict[str, Any]] = []
    for index, vector in indexed:
        matched_cluster: dict[str, Any] | None = None
        best_similarity = 0.0
        for cluster in clusters:
            if dataset[index]["category"] != cluster["category"]:
                continue
            if dataset[index]["signal_type"] != cluster["signal_type"]:
                continue
            similarity = _cosine_similarity(vector, cluster["centroid"])
            if similarity > threshold and similarity > best_similarity:
                best_similarity = similarity
                matched_cluster = cluster
        if matched_cluster is None:
            clusters.append(
                {
                    "indices": [index],
                    "centroid": dict(vector),
                    "category": dataset[index]["category"],
                    "signal_type": dataset[index]["signal_type"],
                }
            )
            continue
        matched_cluster["indices"].append(index)
        matched_cluster["centroid"] = _average_vectors(
            [vectors[item_index] for item_index in matched_cluster["indices"]]
        )

    def process_one_cluster(cluster):
        items = [dataset[index] for index in cluster["indices"]]
        categories = Counter(item["category"] for item in items)
        signal_types = Counter(item["signal_type"] for item in items)
        tools = Counter(tool for item in items for tool in item["tools"])
        keywords = _top_vector_terms(cluster["centroid"], limit=6)
        representative = _pick_representative_item(items, cluster["centroid"], doc_freq, doc_count)
        summary = _build_cluster_requirement_summary(
            category=categories.most_common(1)[0][0],
            signal_type=signal_types.most_common(1)[0][0],
            representative=representative["summary"],
            keywords=keywords,
        )
        alignment_score, matched_doc_signal = _score_requirement_cluster_alignment(
            {
                "category": categories.most_common(1)[0][0],
                "signal_type": signal_types.most_common(1)[0][0],
                "summary": summary,
                "keywords": keywords,
            },
            doc_signals,
        )
        return {
            "category": categories.most_common(1)[0][0],
            "signal_type": signal_types.most_common(1)[0][0],
            "count": len(items),
            "score": _score_requirement_cluster(
                signal_types.most_common(1)[0][0],
                len(items),
                len(tools),
            ),
            "keywords": keywords,
            "summary": summary,
            "tools": dict(tools.most_common(5)),
            "alignment_score": alignment_score,
            "matched_doc_requirement": matched_doc_signal.summary if matched_doc_signal else None,
            "examples": [
                {
                    "summary": item["summary"],
                    "evidence": item["evidence"],
                    "url": item["url"],
                }
                for item in items[:3]
            ],
        }

    with ThreadPoolExecutor(max_workers=10) as executor:
        cluster_summaries = list(executor.map(process_one_cluster, clusters))

    cluster_summaries.sort(
        key=lambda item: (
            -item.get("alignment_score", 0.0),
            -_signal_priority(item["signal_type"]),
            -item["score"],
            -item["count"],
            item["category"],
            item["summary"],
        )
    )

    actionable_clusters = [
        item
        for item in cluster_summaries
        if item["signal_type"] in {"pain_point", "desired_feature", "constraint"}
    ]
    selected_clusters = actionable_clusters[:cluster_limit]
    if not selected_clusters:
        selected_clusters = cluster_summaries[:cluster_limit]

    lean_requirements = [
        {
            "priority": rank + 1,
            "category": item["category"],
            "signal_type": item["signal_type"],
            "summary": item["summary"],
            "keywords": item["keywords"],
            "count": item["count"],
            "alignment_score": round(item.get("alignment_score", 0.0), 4),
            "matched_doc_requirement": item.get("matched_doc_requirement"),
        }
        for rank, item in enumerate(selected_clusters[:cluster_limit])
    ]

    return {
        "record_count": len(records),
        "signal_count": len(dataset),
        "clusters": cluster_summaries,
        "lean_requirements": lean_requirements,
        "doc_requirement_signals": [item.as_dict() for item in doc_signals],
    }


def write_jsonl(records: list[FeedbackRecord], output_path: str | Path) -> None:
    path = Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as handle:
        for record in records:
            handle.write(json.dumps(record.as_dict(), ensure_ascii=False) + "\n")


def load_jsonl_records(input_path: str | Path) -> list[FeedbackRecord]:
    records: list[FeedbackRecord] = []
    path = Path(input_path)
    if not path.exists():
        return records
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        payload = json.loads(line)
        records.append(
            FeedbackRecord(
                source=payload.get("source", ""),
                url=payload.get("url", ""),
                title=payload.get("title", ""),
                snippet=payload.get("snippet", ""),
                content=payload.get("content", ""),
                author=payload.get("author", ""),
                published_at=payload.get("published_at", ""),
                matched_keywords=payload.get("matched_keywords", []),
                matched_review_terms=payload.get("matched_review_terms", []),
                matched_requirement_terms=payload.get("matched_requirement_terms", []),
                overall_sentiment=payload.get("overall_sentiment", "neutral"),
                comment_count=payload.get("comment_count", 0),
                is_question=payload.get("is_question", False),
                tool_mentions=[
                    ToolMention(
                        tool=item.get("tool", ""),
                        sentiment_label=item.get("sentiment_label", "neutral"),
                        pros=item.get("pros", []),
                        cons=item.get("cons", []),
                        recommendations=item.get("recommendations", []),
                        experience_reports=item.get("experience_reports", []),
                        evidence_count=item.get("evidence_count", 0),
                    )
                    for item in payload.get("tool_mentions", [])
                ],
                requirement_signals=[
                    RequirementSignal(
                        category=item.get("category", ""),
                        signal_type=item.get("signal_type", ""),
                        summary=item.get("summary", ""),
                        evidence=item.get("evidence", ""),
                        tools=item.get("tools", []),
                        source_kind=item.get("source_kind", "crawl"),
                        alignment_score=float(item.get("alignment_score", 0.0)),
                    )
                    for item in payload.get("requirement_signals", [])
                ],
            )
        )
    return records


def write_csv(records: list[FeedbackRecord], output_path: str | Path) -> None:
    path = Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = list(
        FeedbackRecord(
            source="",
            url="",
            title="",
            snippet="",
            content="",
            author="",
            published_at="",
            matched_keywords=[],
            matched_review_terms=[],
            matched_requirement_terms=[],
            overall_sentiment="neutral",
            comment_count=0,
            is_question=False,
            tool_mentions=[],
            requirement_signals=[],
        ).as_dict().keys()
    )
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for record in records:
            serialized: dict[str, Any] = {}
            for key, value in record.as_dict().items():
                if isinstance(value, (list, dict)):
                    serialized[key] = json.dumps(value, ensure_ascii=False)
                else:
                    serialized[key] = value
            writer.writerow(serialized)


def _tokenize_ml_text(text: str) -> list[str]:
    normalized = _normalize_evidence_text(text)
    raw_tokens = re.findall(r"[a-z0-9]+|[\u4e00-\u9fff]+", normalized)
    tokens: list[str] = []
    for token in raw_tokens:
        if not token:
            continue
        if re.fullmatch(r"[\u4e00-\u9fff]+", token):
            if len(token) <= 2:
                tokens.append(token)
                continue
            tokens.extend(token[i : i + 2] for i in range(len(token) - 1))
            continue
        if token in EVIDENCE_STOPWORDS or len(token) <= 1:
            continue
        tokens.append(token)
    return tokens


def _is_low_value_requirement_text(text: str) -> bool:
    raw_normalized = normalize_text(text).lower()
    normalized = _normalize_evidence_text(text)
    if not raw_normalized or not normalized:
        return True
    if len(raw_normalized) > 420:
        return True
    if _looks_like_structured_requirement_noise(raw_normalized):
        return True

    generic_patterns = (
        r"^(?:has|does|is|can|could|would|should|what|why|when|where|which|anyone)\b.+\?$",
        r"^(?:good question|following|same here|me too|thank you|thanks)\b.*$",
        r"^(?:i(?:'ve| have)? been using|i use|i'm using|i am using)\b.+$",
        r"^(?:alternatives? to|looking for alternatives? to)\b.+$",
    )
    if any(re.match(pattern, raw_normalized) for pattern in generic_patterns):
        return True

    return len(_tokenize_ml_text(text)) < 3


def _looks_like_structured_requirement_noise(raw_text: str) -> bool:
    noise_markers = (
        "issue cache note",
        "parent issue",
        "每日更新报告",
        "提交时间",
        "hackathon",
        "目录 1",
        "关键词：",
        "本报告",
        "验收标准",
        "scope",
        "acceptance criteria",
        "author",
        "date",
        "## ",
        "### ",
        "```",
    )
    hit_count = sum(1 for marker in noise_markers if marker in raw_text)
    if hit_count >= 3:
        return True
    if raw_text.count("http://") + raw_text.count("https://") >= 2:
        return True
    return False


def _should_include_ml_requirement_signal(
    signal: RequirementSignal,
    evidence: str,
) -> bool:
    if signal.source_kind == "doc_requirement":
        return signal.alignment_score > 0
    if not evidence or _is_low_value_requirement_text(evidence):
        return False

    has_requirement_context = _contains_requirement_context_signal(
        f"{signal.summary} {evidence}"
    )

    if signal.category == "通用工作流" and not has_requirement_context:
        return False

    if signal.signal_type == "usage_scenario":
        if "?" in evidence:
            return False
        if not has_requirement_context:
            return False
    elif not has_requirement_context and signal.category == "通用工作流":
        return False

    return True


def _score_doc_alignment(text: str) -> float:
    normalized = _normalize_evidence_text(text)
    if not normalized:
        return 0.0
    score = 0.0
    score += sum(1.0 for term in DOC_PRODUCT_CAPABILITY_TERMS if term.lower() in normalized)
    score -= sum(2.0 for marker in DOC_NOISE_MARKERS if marker.lower() in normalized)
    score -= sum(
        1.5
        for marker in ("痛点", "必要性", "应用领域", "服务对象", "覆盖场景", "核心目标导向")
        if marker in text
    )
    if any(marker in normalized for marker in ("本地优先", "离线", "隐私", "markdown", "pdf", "docx")):
        score += 1.5
    if any(
        marker in text
        for marker in (
            "支持",
            "提供",
            "实现",
            "具备",
            "响应速度",
            "数据一致性",
            "跨平台兼容性",
            "安全性",
        )
    ):
        score += 1.0
    if len(normalized) > 180:
        score -= 1.0
    return score


def _score_requirement_cluster_alignment(
    item: dict[str, Any],
    doc_signals: list[RequirementSignal],
) -> tuple[float, RequirementSignal | None]:
    if not doc_signals:
        return 0.0, None

    base_text = normalize_text(
        " ".join(
            [
                item.get("category", ""),
                item.get("summary", ""),
                " ".join(item.get("keywords", [])),
            ]
        )
    )
    if not base_text:
        return 0.0, None

    cluster_tokens = _tokenize_ml_text(base_text)
    if not cluster_tokens:
        return 0.0, None
    cluster_set = set(cluster_tokens)
    best_score = 0.0
    best_signal = None
    for signal in doc_signals:
        signal_text = normalize_text(f"{signal.category} {signal.summary} {signal.evidence}")
        signal_tokens = set(_tokenize_ml_text(signal_text))
        if not signal_tokens:
            continue
        overlap = len(cluster_set & signal_tokens)
        if overlap == 0:
            continue
        score = overlap / len(cluster_set | signal_tokens)
        if item.get("category") == signal.category:
            score += 0.25
        if item.get("signal_type") == signal.signal_type:
            score += 0.15
        score += min(signal.alignment_score, 6.0) * 0.03
        if score > best_score:
            best_score = score
            best_signal = signal
    return best_score, best_signal


def _signal_priority(signal_type: str) -> float:
    priorities = {
        "pain_point": 4.0,
        "desired_feature": 3.5,
        "constraint": 3.0,
        "usage_scenario": 1.0,
    }
    return priorities.get(signal_type, 1.0)


def _score_requirement_cluster(
    signal_type: str,
    count: int,
    tool_count: int,
) -> float:
    return (_signal_priority(signal_type) * count) + min(tool_count, 3) * 0.2


def _build_tfidf_vector(
    tokens: list[str],
    document_frequency: Counter[str],
    document_count: int,
) -> dict[str, float]:
    term_frequency = Counter(tokens)
    vector: dict[str, float] = {}
    for token, count in term_frequency.items():
        idf = math.log((1 + document_count) / (1 + document_frequency[token])) + 1.0
        vector[token] = (1.0 + math.log(count)) * idf
    return vector


def _vector_norm(vector: dict[str, float]) -> float:
    return math.sqrt(sum(value * value for value in vector.values()))


def _cosine_similarity(vector_a: dict[str, float], vector_b: dict[str, float]) -> float:
    if not vector_a or not vector_b:
        return 0.0
    dot_product = sum(value * vector_b.get(token, 0.0) for token, value in vector_a.items())
    norm = _vector_norm(vector_a) * _vector_norm(vector_b)
    if norm == 0:
        return 0.0
    return dot_product / norm


def _average_vectors(vectors: list[dict[str, float]]) -> dict[str, float]:
    if not vectors:
        return {}
    accumulator: dict[str, float] = {}
    for vector in vectors:
        for token, value in vector.items():
            accumulator[token] = accumulator.get(token, 0.0) + value
    scale = float(len(vectors))
    return {token: value / scale for token, value in accumulator.items()}


def _top_vector_terms(vector: dict[str, float], limit: int = 6) -> list[str]:
    return [
        token
        for token, _ in sorted(vector.items(), key=lambda item: (-item[1], item[0]))[:limit]
    ]


def _pick_representative_item(
    items: list[dict[str, Any]],
    centroid: dict[str, float],
    document_frequency: Counter[str],
    document_count: int,
) -> dict[str, Any]:
    if len(items) == 1:
        return items[0]
    scored_items = []
    for item in items:
        vector = _build_tfidf_vector(_tokenize_ml_text(item["text"]), document_frequency, document_count)
        scored_items.append((_cosine_similarity(vector, centroid), item))
    scored_items.sort(key=lambda pair: pair[0], reverse=True)
    return scored_items[0][1]


def _build_cluster_requirement_summary(
    category: str,
    signal_type: str,
    representative: str,
    keywords: list[str],
) -> str:
    snippet = _compact_requirement_fragment(representative)
    snippet_zh = _translate_requirement_fragment_zh(snippet)

    # Use synthesized title if snippet is too noisy, too short, or mostly English
    noisy_markers = {"however", "actually", "basically", "literally", "process", "thing", "issue", "problem", "i", "me", "my", "this", "is", "it", "that", "questions", "answer", "solve"}
    tokens = [t.strip(" ,.()!?") for t in snippet.lower().split()]
    is_noisy = len(tokens) <= 4 and any(t in noisy_markers for t in tokens)
    
    # Check if snippet_zh still contains significant English or is just very short
    english_chars = len(re.findall(r"[a-zA-Z]", snippet_zh))
    cjk_chars = len(re.findall(r"[\u4e00-\u9fff]", snippet_zh))
    is_mostly_english = cjk_chars == 0 or (english_chars > cjk_chars * 1.2)

    if is_mostly_english:
        clean_snippet = representative.replace('\n', ' ').strip()
        if len(clean_snippet) > 150:
            clean_snippet = clean_snippet[:147] + "..."
            
        if snippet_zh != "核心需求" and snippet_zh.lower() != snippet.lower() and _contains_cjk(snippet_zh):
            return f"{snippet_zh} (例如: {clean_snippet})"
            
        synthesized = _synthesize_requirement_title(category, signal_type, keywords)
        if synthesized:
            return f"{synthesized} (例如: {clean_snippet})"
        return clean_snippet

    if is_noisy or len(snippet_zh) < 4:
        synthesized = _synthesize_requirement_title(category, signal_type, keywords)
        if synthesized:
            return synthesized

    if signal_type == "desired_feature":
        if snippet_zh.startswith(("支持", "提供", "实现", "具备", "完善")):
            return snippet_zh
        return f"支持{snippet_zh}"
    if signal_type == "pain_point":
        if snippet_zh.startswith(("减少", "避免", "解决", "修复", "提升", "优化", "降低", "改善", "处理", "适配")):
            return snippet_zh
        return f"解决{snippet_zh}问题"
    if signal_type == "constraint":
        if snippet_zh.startswith(("支持", "兼容", "不要", "可在", "确保", "满足", "必须")):
            return snippet_zh
        return f"满足{snippet_zh}要求"
    if snippet_zh and len(snippet_zh) <= 40 and not snippet_zh.startswith("原评论提到"):
        return f"适配{snippet_zh}场景"
    if keywords:
        return f"适配 {' / '.join(keywords[:3])} 场景"
    return f"适配{snippet_zh}"


def _synthesize_requirement_title(category: str, signal_type: str, keywords: list[str]) -> str | None:
    term_map = {
        "slow": "响应速度",
        "speed": "运行速度",
        "performance": "性能表现",
        "fast": "性能表现",
        "sync": "同步功能",
        "metadata": "元数据提取",
        "citation": "引文生成",
        "cite": "引用功能",
        "citing": "引用功能",
        "bibtex": "BibTeX管理",
        "pdf": "PDF处理",
        "annotation": "批注功能",
        "annotate": "批注功能",
        "highlight": "高亮标注",
        "export": "格式导出",
        "import": "条目导入",
        "plugin": "插件系统",
        "extension": "扩展插件",
        "ecosystem": "生态集成",
        "ui": "界面交互",
        "ux": "用户体验",
        "mobile": "移动端适配",
        "ipad": "iPad端适配",
        "offline": "离线模式",
        "privacy": "隐私保护",
        "cloud": "云端同步",
        "security": "数据安全",
        "duplicate": "重复条目处理",
        "search": "搜索检索",
        "collaboration": "协作功能",
        "share": "分享功能",
        "team": "团队协作",
        "word": "Word插件",
        "google": "Google生态",
        "docs": "文档集成",
        "latex": "LaTeX支持",
        "format": "排版格式",
        "template": "样式模板",
        "style": "引文样式",
        "library": "文献库管理",
        "folder": "分类目录",
        "tag": "标签管理",
        "mendeley": "Mendeley兼容",
        "zotero": "Zotero联动",
        "paperpile": "Paperpile兼容",
        "switch": "数据迁移",
        "switching": "数据迁移",
        "wrong": "准确性",
        "bug": "稳定性",
        "crash": "稳定性",
        "broken": "可用性",
        "版本": "版本兼容性",
        "报错": "报错处理",
        "安装": "安装流程",
        "下载": "下载功能",
        "编译": "编译支持",
        "公式": "公式渲染",
        "排版": "排版质量",
        "文献": "文献管理",
        "引用": "引文功能",
        "PDF": "PDF处理",
        "paperlib": "文献库管理",
        "chatgpt": "AI辅助写作",
    }
    
    # Meaningless CJK bigrams to filter out
    meaningless_bigrams = {"及解", "会编", "于解", "们开", "决扫", "换的", "到需", "把数", "据同", "接到", "了很", "件路", "会这", "上有", "些别", "从核", "本问", "程的", "装过", "中公", "写对", "库文", "写发", "件中", "的错", "个引", "后导", "现不", "了但", "味指", "测问", "来的", "找到", "出来", "核心", "名字", "之后", "这个", "那个", "可以", "支持", "问题", "解决", "优化", "功能", "场景", "以上", "产生", "不少", "关键"}

    mapped_terms = []
    unmapped_keywords = []
    for kw in keywords:
        kw_lower = kw.lower()
        if kw_lower in term_map:
            mapped_terms.append(term_map[kw_lower])
        elif _contains_cjk(kw) and len(kw) >= 2 and kw not in meaningless_bigrams:
            found = False
            for v in term_map.values():
                if kw in v or v in kw:
                    mapped_terms.append(v)
                    found = True
                    break
            if not found and len(kw) > 2:
                mapped_terms.append(kw)
        elif not _contains_cjk(kw) and len(kw) >= 3:
            unmapped_keywords.append(kw)

    if not mapped_terms:
        cat_root = category.split("/")[0]
        mapped_terms = [cat_root]

    unique_terms = _sorted_unique(mapped_terms)
    filtered_terms = [t for t in unique_terms if t not in ("问题", "报错", "功能", "场景")]
    final_terms = filtered_terms if filtered_terms else unique_terms
    term_str = "/".join(final_terms[:2])

    if signal_type == "pain_point":
        base = f"优化 {term_str} 相关体验/修复已知缺陷"
    elif signal_type == "desired_feature":
        base = f"支持 {term_str} 核心能力"
    elif signal_type == "constraint":
        base = f"确保 {term_str} 相关约束"
    else:
        base = f"改善 {term_str} 体验"

    return base


def _compact_requirement_fragment(text: str) -> str:
    fragment = _html_to_text(text)
    fragment = _extract_requirement_summary(fragment, "")
    fragment = re.sub(r"\[[^\]]+\]\((https?://[^)]+)\)", "", fragment)
    fragment = re.sub(r"https?://\S+", "", fragment)
    
    # Strip site names and common blog artifacts
    noise_patterns = [
        r"[-|]\s*(?:博客园|掘金|知乎|CSDN|Bilibili|GitHub|博客|花谢悦神|噜噗|掘力).*",
        r"posted\s*@.*",
        r"阅读\s*\(\d+\).*",
        r"评论\s*\(\d+\).*",
        r"收藏\s*举报.*",
        r"升级成为会员.*",
        r"上一篇：.*",
        r"下一篇：.*",
        r"一、前言.*",
    ]
    for pattern in noise_patterns:
        fragment = re.sub(pattern, "", fragment, flags=re.IGNORECASE)

    fragment = normalize_text(fragment).strip(" -:：.。!！?？,，")

    split_patterns = r"\s+(?:because|but|however|though|so that|and then)\s+|[;；]"
    parts = re.split(split_patterns, fragment, flags=re.IGNORECASE)
    first_part = parts[0].strip()

    if (len(first_part.split()) >= 3 or _contains_cjk(first_part)) and len(first_part) < 60:
        return first_part
    
    if _contains_cjk(first_part) and len(first_part) >= 60:
        # Split by comma or other CJK delimiters for long sentences
        subparts = re.split(r"[，,。！!？?]", first_part)
        for sp in subparts:
            if 4 <= len(sp.strip()) <= 45:
                return sp.strip()

    return fragment[:80] or normalize_text(text)[:80].strip(" -:：.。!！?？,，")


def _extract_title_from_reasoning(reasoning: str) -> str | None:
    """Extract a Chinese PRD title from Qwen3.5's reasoning_content chain."""
    import re as _re
    candidates: list[tuple[int, str]] = []

    # Pattern 1: Quoted Chinese strings
    for m in _re.finditer(r'["\u201c\u300c*]([^"\u201d\u300d*]{4,25})["\u201d\u300d*]', reasoning):
        text = m.group(1).strip()
        cjk_count = len(_re.findall(r'[\u4e00-\u9fff]', text))
        if cjk_count >= 3 and len(text) >= 4 and text[0] in '\u4fee\u4f18\u652f\u89e3\u786e\u6539\u589e\u5b8c\u9002\u964d\u63d0':
            candidates.append((m.start(), text))

    # Pattern 2: Lines starting with action verbs
    for m in _re.finditer(
        r'(?:^|\n)\s*(?:Draft \d+:\s*|Final Choice:\s*|Best:\s*|Answer:\s*)?'
        r'([\u4fee\u4f18\u652f\u89e3\u786e\u6539\u589e\u5b8c\u9002\u964d\u63d0][^\n]{3,22})',
        reasoning,
    ):
        text = m.group(1).strip().rstrip('\u3002."\' )')
        cjk_count = len(_re.findall(r'[\u4e00-\u9fff]', text))
        if cjk_count >= 3:
            candidates.append((m.start(), text))

    if not candidates:
        return None

    best = candidates[-1][1]
    best = _re.sub(r'\s*[\(\uff08]\d+\s*(?:\u5b57|chars?|characters?|\u4e2a\u5b57)[\)\uff09]', '', best)
    best = best.strip('\u3002."\' )')
    # Reject prompt template echoes
    if 'XXX' in best or len(best) < 4:
        return None
    return best


def _call_local_llm(fragment: str) -> str | None:
    import re as _re
    import requests as _requests
    url = "http://127.0.0.1:8080/v1/chat/completions"
    prompt = (
        "你是一个资深产品经理。你的任务是将用户反馈（通常是英文吐槽或抱怨）"
        "提炼成一句【精准、落地、专业的中文 PRD 需求大标题】。\n"
        "规则：\n"
        "1. 输出必须是一行纯中文，不需要任何解释、标点符号或前缀。\n"
        "2. 字数限制在 20 个字以内。\n"
        '3. 格式通常为"修复 XXX 问题"或"支持 XXX 功能"或"优化 XXX 体验"。\n'
        '4. 直接直击痛点，保留原句中提到的具体软件名或格式（如 Word、Zotero、PDF、Pandoc 等）。\n'
        f"用户反馈原声：\n{fragment}\n\n需求标题："
    )
    payload = {
        "messages": [
            {"role": "system", "content": "你是一个资深产品经理，负责提炼核心产品需求。"},
            {"role": "user", "content": prompt}
        ],
        "temperature": 0.3,
        "max_tokens": 2048
    }
    try:
        response = _requests.post(url, json=payload, timeout=60)
        if response.status_code == 200:
            data = response.json()
            if "choices" in data and len(data["choices"]) > 0:
                msg = data["choices"][0]["message"]
                # Try content first (normal models)
                content = (msg.get("content") or "").strip()
                if content:
                    content = _re.sub(
                        r"^(?:需求标题[：:]|标题[：:]|需求[：:]|建议[：:]|\*|\-)\s*",
                        "", content,
                    ).strip()
                    content = content.split("\n")[0].strip("。.\"'）")
                    if len(content) >= 4:
                        return content

                # Fallback: extract from reasoning_content (Qwen3.5 thinking mode)
                reasoning = (msg.get("reasoning_content") or "").strip()
                if reasoning:
                    extracted = _extract_title_from_reasoning(reasoning)
                    if extracted:
                        return extracted
    except Exception:
        pass
    return None



@functools.lru_cache(maxsize=1024)
def _translate_requirement_fragment_zh(fragment: str) -> str:
    normalized = normalize_text(fragment).strip(" .。!！?？")
    if not normalized:
        return "核心需求"
    if _contains_cjk(normalized):
        return normalized

    lower = normalized.lower()
    lower = re.sub(r"^\[bug\]\s*", "", lower)
    exact_map = {
        "do not upload unpublished papers to cloud tools": "不要把未发表论文上传到云端工具",
        "need docx export on linux": "支持 Linux 下的 Docx 导出",
        "having to manually input and tag things is annoying": "减少手工录入和打标签",
        "not getting a real citation list at the end": "正确生成参考文献列表",
        "missing articles in mendeley": "避免文献条目丢失",
        "the sync setup is annoying": "减少同步配置成本",
        "merge duplicates": "优化重复条目合并",
        "now i suspect that this perhaps is bug": "修复已知缺陷并补充可复现说明",
        "metadata was just wrong": "提升元数据提取准确性",
        "this is slow process": "优化性能与响应速度",
        "i'm trying to switch from mendeley to zotero and one thing is bugging me": "解决 Mendeley 向 Zotero 迁移时的数据兼容与卡点问题",
        "cannot figure out how to download our zotero library to mendeley": "支持 Zotero 库向 Mendeley 的逆向下载与同步",
        "to the missing pdf problem when importing research from bibtex": "修复 BibTeX 导入文献时 PDF 附件丢失的问题",
        "issue with zotero annotations and notes": "修复 Zotero 批注与笔记在第三方工具中的同步错乱问题",
        "better-bibtex assigning wrong citation keys on export": "修复 Better-BibTeX 导出时生成错误引用键（Citation Keys）的问题",
        "i’m now in the process of switching to zotero": "优化跨平台软件生态切换时的基础配置与文献迁移体验",
        "zotfile doesn't work anymore with bibtex": "修复 Zotfile 插件与 BibTeX 的兼容性失效问题",
        "i also find it is not as slow as mendeley when working with large documents": "优化大文档（Large Documents）加载与响应速度，提升编辑流畅度",
        "hi everyone, i am switching from mendeley to endnote, since this is required by university unfortunately": "提供满足高校特定格式要求的 Endnote 数据导出或迁移支持",
        "i switched to paperpile recently because me mendeley and zotero were just too buggy": "解决基础文献管理 Bug，提高整体稳定性以防止用户流失",
        "if i really have to, overleaf and as a last resort, word even if i hate it": "改善 Overleaf 与 Word 插件的稳定性与易用性",
        "having a problem with line break when exporting by pandoc": "修复 Pandoc 导出 Markdown 时的换行解析与错位错误",
        "i found the export in markdown really awful when i did the switch)": "大幅改善 Markdown 格式导出的排版质量与表格兼容性"
    }
    if lower in exact_map:
        return exact_map[lower]

    llm_translation = _call_local_llm(fragment)
    if llm_translation and len(llm_translation) >= 4 and _contains_cjk(llm_translation):
        return llm_translation

    return fragment[:100]


def write_summary_json(summary: dict[str, Any], output_path: str | Path) -> None:
    path = Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")


def write_requirement_summary_json(summary: dict[str, Any], output_path: str | Path) -> None:
    payload = {
        "generated_at": summary["generated_at"],
        "record_count": summary["record_count"],
        "requirement_summaries": summary.get("requirement_summaries", []),
    }
    write_summary_json(payload, output_path)


def write_ml_requirement_analysis_json(
    analysis: dict[str, Any],
    output_path: str | Path,
) -> None:
    path = Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(analysis, ensure_ascii=False, indent=2), encoding="utf-8")


def write_ml_requirement_analysis_markdown(
    analysis: dict[str, Any],
    output_path: str | Path,
) -> None:
    lines = [
        "# ML Requirement Analysis",
        "",
        f"- Records: {analysis.get('record_count', 0)}",
        f"- Signals: {analysis.get('signal_count', 0)}",
        "",
        "## Lean Requirements",
        "",
    ]
    for item in analysis.get("lean_requirements", []):
        lines.append(
            f"- P{item['priority']} | {item['category']} | {item['signal_type']} | {item['summary']}"
        )
    lines.append("")
    lines.append("## Clusters")
    lines.append("")
    for item in analysis.get("clusters", []):
        lines.append(f"### {item['category']}")
        lines.append("")
        lines.append(f"- Count: {item['count']}")
        lines.append(f"- Signal type: {item['signal_type']}")
        if item.get("keywords"):
            lines.append(f"- Keywords: {json.dumps(item['keywords'], ensure_ascii=False)}")
        lines.append(f"- Summary: {item['summary']}")
        if item.get("tools"):
            lines.append(f"- Tools: {json.dumps(item['tools'], ensure_ascii=False)}")
        if item.get("examples"):
            lines.append("- Examples:")
            for example in item["examples"]:
                lines.append(f"  - {example['evidence']}")
                if example.get("url"):
                    lines.append(f"    URL: {example['url']}")
        lines.append("")

    path = Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines).strip() + "\n", encoding="utf-8")


def write_lean_requirements_json(
    analysis: dict[str, Any],
    output_path: str | Path,
) -> None:
    payload = {
        "record_count": analysis.get("record_count", 0),
        "signal_count": analysis.get("signal_count", 0),
        "lean_requirements": analysis.get("lean_requirements", []),
    }
    write_ml_requirement_analysis_json(payload, output_path)


def write_lean_requirements_markdown(
    analysis: dict[str, Any],
    output_path: str | Path,
) -> None:
    lines = [
        "# Lean Requirement List",
        "",
    ]
    for item in analysis.get("lean_requirements", []):
        category = item["category"]
        summary = item["summary"]
        # Avoid redundant category in the line
        clean_summary = re.sub(rf"^\[?{re.escape(category)}\]?[：:]\s*", "", summary)
        lines.append(
            f"- P{item['priority']}. [{category}] {clean_summary}"
        )
    path = Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines).strip() + "\n", encoding="utf-8")


def write_prd_requirements_markdown(
    analysis: dict[str, Any],
    output_path: str | Path,
) -> None:
    grouped: dict[str, list[dict[str, Any]]] = {}
    for item in analysis.get("lean_requirements", []):
        grouped.setdefault(item["category"], []).append(item)

    lines = [
        "# 精简需求清单",
        "",
        "## 核心结论",
        "",
        "基于评论级机器学习聚类结果整理，优先保留高频痛点、明确需求和约束条件。",
        "",
    ]

    priority_index = 1
    for category, items in sorted(
        grouped.items(),
        key=lambda pair: min(entry.get("priority", 999) for entry in pair[1]),
    ):
        lines.append(f"## {category}")
        lines.append("")
        seen_titles = set()
        for item in sorted(items, key=lambda entry: entry.get("priority", 999)):
            summary = normalize_text(item.get("summary", ""))
            title = summary
            match = re.search(r"^(.*?)\s*\(例如:\s*(.*?)\)$", summary)
            if match:
                title = match.group(1).strip()
            
            if title in seen_titles:
                continue
            seen_titles.add(title)
            
            prd_line = _build_prd_requirement_line(item)
            lines.append(f"{priority_index}. {prd_line}")
            priority_index += 1
            if len(seen_titles) >= 10:
                break
        lines.append("")

    path = Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines).strip() + "\n", encoding="utf-8")


def _build_prd_requirement_line(item: dict[str, Any]) -> str:
    category = item.get("category", "通用")
    signal_type = item.get("signal_type", "usage_scenario")
    summary = normalize_text(item.get("summary", ""))

    title = summary
    evidence = ""
    match = re.search(r"^(.*?)\s*\(例如:\s*(.*?)\)$", summary)
    if match:
        title = match.group(1).strip()
        evidence = match.group(2).strip()

    title = re.sub(rf"^\[?{re.escape(category)}\]?[：:]\s*", "", title)
    title = title.strip("。")

    if evidence:
         return f"**{title}**<br>   - *用户原声印证*：{evidence}"
    
    return f"**{title}**"


def write_summary_markdown(summary: dict[str, Any], output_path: str | Path) -> None:
    lines = [
        "# Paper Tool Feedback Summary",
        "",
        f"- Generated at: {summary['generated_at']}",
        f"- Records: {summary['record_count']}",
        "",
    ]

    if summary.get("requirement_summaries"):
        lines.append("## Requirement Insights")
        lines.append("")
        for item in summary["requirement_summaries"]:
            lines.append(f"### {item['category']}")
            lines.append("")
            lines.append(f"- Records: {item['record_count']}")
            lines.append(
                f"- Signal type counts: {json.dumps(item['signal_type_counts'], ensure_ascii=False)}"
            )
            if item["top_tools"]:
                lines.append(f"- Related tools: {json.dumps(item['top_tools'], ensure_ascii=False)}")
            if item["signal_highlights"]:
                lines.append(
                    f"- Requirement highlights: {json.dumps(item['signal_highlights'], ensure_ascii=False)}"
                )
            if item["examples"]:
                lines.append("- Requirement examples:")
                for example in item["examples"]:
                    lines.append(f"  - [{example['signal_type']}] {example['summary_zh']}")
                    lines.append(
                        f"    Evidence: {json.dumps(example['evidence'], ensure_ascii=False)}"
                    )
            if item["sample_urls"]:
                lines.append("- Sample threads:")
                for url in item["sample_urls"]:
                    lines.append(f"  - {url}")
            lines.append("")

    for item in summary["tool_summaries"]:
        lines.append(f"## {item['tool']}")
        lines.append("")
        lines.append(f"- Records: {item['record_count']}")
        lines.append(f"- Sentiment counts: {json.dumps(item['sentiment_counts'], ensure_ascii=False)}")
        if item["review_terms"]:
            lines.append(f"- Review terms: {json.dumps(item['review_terms'], ensure_ascii=False)}")
        if item["positive_highlights"]:
            lines.append(
                f"- Positive highlights: {json.dumps(item['positive_highlights'], ensure_ascii=False)}"
            )
        if item["negative_highlights"]:
            lines.append(
                f"- Negative highlights: {json.dumps(item['negative_highlights'], ensure_ascii=False)}"
            )
        if item["specific_positive_highlights"]:
            lines.append(
                "- Specific positive highlights: "
                f"{json.dumps(item['specific_positive_highlights'], ensure_ascii=False)}"
            )
        if item["specific_positive_summaries"]:
            lines.append(
                "- Specific positive summaries: "
                f"{json.dumps(item['specific_positive_summaries'], ensure_ascii=False)}"
            )
        if item["specific_negative_highlights"]:
            lines.append(
                "- Specific negative highlights: "
                f"{json.dumps(item['specific_negative_highlights'], ensure_ascii=False)}"
            )
        if item["specific_negative_summaries"]:
            lines.append(
                "- Specific negative summaries: "
                f"{json.dumps(item['specific_negative_summaries'], ensure_ascii=False)}"
            )
        if item["recommendation_highlights"]:
            lines.append(
                "- Recommendation highlights: "
                f"{json.dumps(item['recommendation_highlights'], ensure_ascii=False)}"
            )
        if item["specific_positive_feedback"]:
            lines.append("- Specific positive feedback:")
            for feedback in item["specific_positive_feedback"]:
                lines.append(f"  - ({feedback['count']}) {feedback['summary_zh']}")
                if feedback.get("examples"):
                    lines.append(
                        "    Examples: "
                        f"{json.dumps(feedback['examples'], ensure_ascii=False)}"
                    )
        if item["specific_negative_feedback"]:
            lines.append("- Specific negative feedback:")
            for feedback in item["specific_negative_feedback"]:
                lines.append(f"  - ({feedback['count']}) {feedback['summary_zh']}")
                if feedback.get("examples"):
                    lines.append(
                        "    Examples: "
                        f"{json.dumps(feedback['examples'], ensure_ascii=False)}"
                    )
        if item["top_pros"]:
            lines.append("- Top pros:")
            for pro in item["top_pros"]:
                suffix = f", {pro['variants']} variants" if pro.get("variants", 1) > 1 else ""
                lines.append(f"  - ({pro['count']}{suffix}) {pro['text']}")
        if item["top_cons"]:
            lines.append("- Top cons:")
            for con in item["top_cons"]:
                suffix = f", {con['variants']} variants" if con.get("variants", 1) > 1 else ""
                lines.append(f"  - ({con['count']}{suffix}) {con['text']}")
        if item["top_recommendations"]:
            lines.append("- Recommendation signals:")
            for recommendation in item["top_recommendations"]:
                suffix = (
                    f", {recommendation['variants']} variants"
                    if recommendation.get("variants", 1) > 1
                    else ""
                )
                lines.append(f"  - ({recommendation['count']}{suffix}) {recommendation['text']}")
        if item["top_experience_reports"]:
            lines.append("- Experience reports:")
            for report in item["top_experience_reports"]:
                suffix = f", {report['variants']} variants" if report.get("variants", 1) > 1 else ""
                lines.append(f"  - ({report['count']}{suffix}) {report['text']}")
        if item["sample_urls"]:
            lines.append("- Sample threads:")
            for url in item["sample_urls"]:
                lines.append(f"  - {url}")
        lines.append("")

    path = Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines).strip() + "\n", encoding="utf-8")


def write_requirement_summary_markdown(summary: dict[str, Any], output_path: str | Path) -> None:
    payload = {
        "generated_at": summary["generated_at"],
        "record_count": summary["record_count"],
        "requirement_summaries": summary.get("requirement_summaries", []),
        "tool_summaries": [],
    }
    write_summary_markdown(payload, output_path)
