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
    return best if len(best) >= 4 else None

import functools

@functools.lru_cache(maxsize=1024)
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
        "3. 格式通常为\"修复 XXX 问题\"或\"支持 XXX 功能\"或\"优化 XXX 体验\"。\n"
        "4. 直接直击痛点，保留原句中提到的具体软件名或格式（如 Word、Zotero、PDF、Pandoc 等）。\n"
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
        response = _requests.post(url, json=payload, timeout=10)
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
