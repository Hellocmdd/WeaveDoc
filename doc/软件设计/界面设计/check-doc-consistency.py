#!/usr/bin/env python3
"""Consistency checks for WeaveDoc UI design documents.

This script intentionally checks only rules that are already agreed as hard
constraints in the design docs. It is not a general prose linter.
"""

from __future__ import annotations

import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


ROOT = Path(__file__).resolve().parent

FILES = {
    "assets": ROOT / "visual-assets.json",
    "design": ROOT / "Design System - 布局规则与设计系统.md",
    "components": ROOT / "UI组件定义.md",
    "pages": ROOT / "页面定义.md",
    "ia": ROOT / "信息架构设计.md",
    "flows": ROOT / "用户任务流分析.md",
}


@dataclass
class Issue:
    code: str
    path: Path
    line: int
    message: str
    snippet: str = ""

    def format(self) -> str:
        rel = self.path.relative_to(ROOT.parent)
        suffix = f"\n    {self.snippet.strip()}" if self.snippet.strip() else ""
        return f"[{self.code}] {rel}:{self.line}: {self.message}{suffix}"


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def read_lines(path: Path) -> list[str]:
    return read_text(path).splitlines()


def line_issues(
    code: str,
    path: Path,
    lines: Iterable[tuple[int, str]],
    pattern: str,
    message: str,
    *,
    flags: int = 0,
    unless: Iterable[str] = (),
) -> list[Issue]:
    compiled = re.compile(pattern, flags)
    skip = [re.compile(p, flags) for p in unless]
    issues: list[Issue] = []
    for lineno, line in lines:
        if compiled.search(line) and not any(p.search(line) for p in skip):
            issues.append(Issue(code, path, lineno, message, line))
    return issues


def all_markdown_lines() -> Iterable[tuple[Path, int, str]]:
    for key in ("design", "components", "pages", "ia", "flows"):
        path = FILES[key]
        for lineno, line in enumerate(read_lines(path), start=1):
            yield path, lineno, line


def section_lines(path: Path, heading: str) -> list[tuple[int, str]]:
    lines = read_lines(path)
    start = None
    for idx, line in enumerate(lines):
        if line.strip() == heading:
            start = idx
            break
    if start is None:
        return []

    result: list[tuple[int, str]] = []
    for idx in range(start, len(lines)):
        if idx > start and lines[idx].startswith("## 组件："):
            break
        result.append((idx + 1, lines[idx]))
    return result


def collect_color_tokens(data: dict) -> dict[str, str]:
    tokens: dict[str, str] = {}
    for item in data["colors"]["neutralScale"]["tokens"]:
        tokens[item["name"]] = item["hex"]
    for item in data["colors"]["primaryAccent"]["tokens"]:
        tokens[item["name"]] = item["hex"]
    for group in data["colors"]["semantic"].values():
        if isinstance(group, dict):
            for item in group.get("tokens", []):
                tokens[item["name"]] = item["hex"]
    return tokens


def check_json_and_color_sources() -> list[Issue]:
    path = FILES["assets"]
    try:
        data = json.loads(read_text(path))
    except json.JSONDecodeError as exc:
        return [Issue("JSON_PARSE", path, exc.lineno, f"invalid JSON: {exc.msg}")]

    tokens = collect_color_tokens(data)
    issues: list[Issue] = []

    def walk(obj: object, json_path: str) -> None:
        if isinstance(obj, dict):
            if "source" in obj and "value" in obj:
                source = obj["source"]
                value = obj["value"]
                if isinstance(source, str) and isinstance(value, str) and value.startswith("#"):
                    expected = tokens.get(source)
                    if expected is None:
                        issues.append(
                            Issue(
                                "TOKEN_SOURCE_UNKNOWN",
                                path,
                                1,
                                f"{json_path}: source '{source}' is not a known color token",
                            )
                        )
                    elif expected.lower() != value.lower():
                        issues.append(
                            Issue(
                                "TOKEN_SOURCE_MISMATCH",
                                path,
                                1,
                                f"{json_path}: value {value} does not match source {source} ({expected})",
                            )
                        )
            for key, value in obj.items():
                walk(value, f"{json_path}.{key}" if json_path else key)
        elif isinstance(obj, list):
            for idx, value in enumerate(obj):
                walk(value, f"{json_path}[{idx}]")

    walk(data, "")

    light = data.get("themeMapping", {}).get("light", {})
    expected_sources = {
        "Primary": "blue-600",
        "AccentForeground": "blue-600",
        "Ring": "blue-700",
        "SidebarPrimary": "blue-600",
    }
    for key, expected_source in expected_sources.items():
        actual = light.get(key, {}).get("source")
        if actual != expected_source:
            issues.append(
                Issue(
                    "LIGHT_THEME_SOURCE",
                    path,
                    1,
                    f"themeMapping.light.{key}.source should be {expected_source}, got {actual!r}",
                )
            )

    return issues


def check_status_vocabulary() -> list[Issue]:
    issues: list[Issue] = []
    rows = list(all_markdown_lines())
    for path, lineno, line in rows:
        if re.search(r"\bdanger\b", line, re.IGNORECASE):
            issues.append(
                Issue(
                    "STATUS_DANGER",
                    path,
                    lineno,
                    "use 'error' for states and 'Destructive'/'destructive' for destructive controls, not 'danger'",
                    line,
                )
            )

        if re.search(r"`(completed|failed)`", line):
            issues.append(
                Issue(
                    "STEPPER_OLD_STATE",
                    path,
                    lineno,
                    "ProgressStepper states must use pending/active/success/error",
                    line,
                )
            )

    return issues


def check_schema_failure_semantics() -> list[Issue]:
    issues: list[Issue] = []
    bad = re.compile(r"(Schema 校验失败|Schema 错误).*(warning|`warning`|⚠️|灰色/禁用)", re.IGNORECASE)
    bad_reverse = re.compile(r"(warning|`warning`|⚠️|灰色/禁用).*(Schema 校验失败|Schema 错误)", re.IGNORECASE)
    for path, lineno, line in all_markdown_lines():
        if bad.search(line) or bad_reverse.search(line):
            issues.append(
                Issue(
                    "SCHEMA_FAILURE_SEVERITY",
                    path,
                    lineno,
                    "AFD template Schema failure must be error + red/disabled, not warning/gray",
                    line,
                )
            )
    return issues


def check_ocr_loading_feedback() -> list[Issue]:
    issues: list[Issue] = []
    for path, lineno, line in all_markdown_lines():
        lower = line.lower()
        relevant = any(term in line for term in ("OCR", "公式", "识别"))
        has_spinner = "spinner" in lower or "loadingspinner" in lower
        allowed = any(term in line for term in ("不可只", "不只", "避免", "非无限"))
        if relevant and has_spinner and not allowed:
            issues.append(
                Issue(
                    "OCR_SPINNER_ONLY",
                    path,
                    lineno,
                    "OCR loading must use progress/stage text/cancel affordance, not bare spinner",
                    line,
                )
            )
    return issues


def check_filterable_list_contract() -> list[Issue]:
    path = FILES["components"]
    section = section_lines(path, "## 组件：FilterableList")
    issues: list[Issue] = []
    if not section:
        return [Issue("MISSING_SECTION", path, 1, "missing FilterableList component section")]

    for lineno, line in section:
        if "`search-no-results`" in line and "文献" in line:
            issues.append(
                Issue(
                    "FILTERABLE_LIST_HARDCODED_COPY",
                    path,
                    lineno,
                    "FilterableList search empty text must be generic, not literature-specific",
                    line,
                )
            )
        if "双击行 / 拖拽" in line and "插入引用" in line and "P3" not in line:
            issues.append(
                Issue(
                    "FILTERABLE_LIST_HARDCODED_ACTION",
                    path,
                    lineno,
                    "FilterableList default row action must be scenario-defined; citation insertion is P3-only",
                    line,
                )
            )

    return issues


def check_toast_boundaries() -> list[Issue]:
    issues: list[Issue] = []
    design = read_text(FILES["design"])
    components = read_text(FILES["components"])
    if "页面内已有明确结果区时不强制重复弹出" not in design:
        issues.append(
            Issue(
                "TOAST_BOUNDARY",
                FILES["design"],
                1,
                "Design System must state Toast is not forced when a page-local result area exists",
            )
        )
    if "PAGE 5: 导出成功默认在导出对话框进度区内展示" not in components:
        issues.append(
            Issue(
                "TOAST_P5_BOUNDARY",
                FILES["components"],
                1,
                "Toast contract must keep P5 export success local by default",
            )
        )
    if "PAGE 8: LaTeX 复制成功默认使用浮层内按钮反馈" not in components:
        issues.append(
            Issue(
                "TOAST_P8_BOUNDARY",
                FILES["components"],
                1,
                "Toast contract must keep P8 copy success local by default",
            )
        )
    return issues


def check_citation_warning_color() -> list[Issue]:
    issues: list[Issue] = []
    for path, lineno, line in all_markdown_lines():
        if "未匹配引用" in line and "红色" in line:
            issues.append(
                Issue(
                    "CITATION_WARNING_COLOR",
                    path,
                    lineno,
                    "unmatched citation warnings must use warning/orange, not red",
                    line,
                )
            )
    return issues


def check_timeline_semantic_scope() -> list[Issue]:
    issues: list[Issue] = []
    component_section = section_lines(FILES["components"], "## 组件：Timeline")
    for lineno, line in component_section:
        if "每个节点包含" in line and "语义描述" in line and "未来" not in line:
            issues.append(
                Issue(
                    "TIMELINE_SEMANTIC_SCOPE",
                    FILES["components"],
                    lineno,
                    "Timeline semantic labels are future AI extensions; MVP nodes must only require timestamp/change summary/status",
                    line,
                )
            )

    page_lines = read_lines(FILES["pages"])
    in_page_4 = False
    before_ai_section = False
    for lineno, line in enumerate(page_lines, start=1):
        if line.startswith("## PAGE 4:"):
            in_page_4 = True
            before_ai_section = True
            continue
        if in_page_4 and line.startswith("## PAGE 5:"):
            in_page_4 = False
            before_ai_section = False
        if in_page_4 and line.startswith("### AI 功能区域"):
            before_ai_section = False
        if not in_page_4 or not before_ai_section:
            continue
        if "语义标注" in line:
            issues.append(
                Issue(
                    "TIMELINE_SEMANTIC_SCOPE",
                    FILES["pages"],
                    lineno,
                    "PAGE 4 must not present semantic snapshot labels as MVP UI",
                    line,
                )
            )
        if "每个节点包含" in line and "语义描述" in line and "未来" not in line:
            issues.append(
                Issue(
                    "TIMELINE_SEMANTIC_SCOPE",
                    FILES["pages"],
                    lineno,
                    "PAGE 4 semantic snapshot labels must be marked as future AI extension",
                    line,
                )
            )
    return issues


def check_component_mapping_contracts() -> list[Issue]:
    path = FILES["assets"]
    try:
        data = json.loads(read_text(path))
    except json.JSONDecodeError:
        return []

    notes = {
        item.get("component"): item.get("note", "")
        for item in data.get("componentMapping", {}).get("coreComponents", [])
    }
    issues: list[Issue] = []

    model_note = notes.get("ModelStatusIndicator", "")
    if "Row" not in model_note or "Settings" not in model_note:
        issues.append(
            Issue(
                "MODEL_STATUS_MAPPING",
                path,
                1,
                "ModelStatusIndicator token mapping must include the Settings row variant",
                model_note,
            )
        )

    diff_note = notes.get("DiffViewer", "")
    if "inline-compact" not in diff_note:
        issues.append(
            Issue(
                "DIFF_VIEWER_MAPPING",
                path,
                1,
                "DiffViewer token mapping must mention inline-compact layout for 280px panels",
                diff_note,
            )
        )

    required_components = {"StreamingTextBlock", "CitationChip", "FieldControls"}
    missing = sorted(required_components - set(notes))
    if missing:
        issues.append(
            Issue(
                "COMPONENT_MAPPING_COVERAGE",
                path,
                1,
                f"componentMapping.coreComponents missing token mapping for {', '.join(missing)}",
            )
        )

    return issues


def check_setup_wizard_template_skip() -> list[Issue]:
    issues: list[Issue] = []
    pages = read_text(FILES["pages"])
    flows = read_text(FILES["flows"])

    if "自定义模板导入可跳过" not in pages or "没有可用模板" not in pages:
        issues.append(
            Issue(
                "SETUP_TEMPLATE_SKIP",
                FILES["pages"],
                1,
                "Setup Wizard must clarify that only custom template import is skippable and no-template completion makes export unavailable",
            )
        )

    for lineno, line in enumerate(read_lines(FILES["flows"]), start=1):
        if "基础编辑和导出可用" in line:
            issues.append(
                Issue(
                    "SETUP_TEMPLATE_SKIP",
                    FILES["flows"],
                    lineno,
                    "Task flow must not promise export is available when no valid AFD template exists",
                    line,
                )
            )
    return issues


def check_memory_threshold_semantics() -> list[Issue]:
    issues: list[Issue] = []
    expected = {
        FILES["components"]: ("`threshold - 10pp`", "critical 告警阈值"),
        FILES["pages"]: ("critical 阈值范围 50%–90%", "10 个百分点"),
        FILES["ia"]: ("warning 区间随 critical 阈值前移", "超过 critical 阈值"),
        FILES["assets"]: ("critical above configured threshold", "default 80%"),
    }
    for path, required_fragments in expected.items():
        text = read_text(path)
        missing = [fragment for fragment in required_fragments if fragment not in text]
        if missing:
            issues.append(
                Issue(
                    "MEMORY_THRESHOLD_SEMANTICS",
                    path,
                    1,
                    f"memory threshold semantics missing fragment(s): {', '.join(missing)}",
                )
            )
    return issues


def check_page_map_alignment() -> list[Issue]:
    pattern_ia = re.compile(r"^## (\d+)\. ([^（—\n]+)", re.MULTILINE)
    pattern_pages = re.compile(r"^## PAGE (\d+): ([^（—\n]+)", re.MULTILINE)
    ia_pages = [(number, name.strip()) for number, name in pattern_ia.findall(read_text(FILES["ia"]))]
    page_defs = [(number, name.strip()) for number, name in pattern_pages.findall(read_text(FILES["pages"]))]

    if ia_pages != page_defs:
        return [
            Issue(
                "PAGE_MAP_ALIGNMENT",
                FILES["pages"],
                1,
                f"IA page map and page definitions differ: IA={ia_pages!r}; pages={page_defs!r}",
            )
        ]
    return []


def run_checks() -> list[Issue]:
    issues: list[Issue] = []
    for path in FILES.values():
        if not path.exists():
            issues.append(Issue("MISSING_FILE", path, 1, "required design document is missing"))
    if issues:
        return issues

    checks = [
        check_json_and_color_sources,
        check_status_vocabulary,
        check_schema_failure_semantics,
        check_ocr_loading_feedback,
        check_filterable_list_contract,
        check_toast_boundaries,
        check_citation_warning_color,
        check_timeline_semantic_scope,
        check_component_mapping_contracts,
        check_setup_wizard_template_skip,
        check_memory_threshold_semantics,
        check_page_map_alignment,
    ]
    for check in checks:
        issues.extend(check())
    return issues


def main() -> int:
    issues = run_checks()
    if issues:
        print(f"FAIL: {len(issues)} consistency issue(s) found\n")
        for issue in issues:
            print(issue.format())
        return 1

    print("PASS: UI design documents are consistent for enforced checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
