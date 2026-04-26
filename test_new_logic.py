
from paper_tool_feedback_crawler.crawler import _build_cluster_requirement_summary

def test_summary_generation():
    test_cases = [
        {
            "category": "性能/稳定性",
            "signal_type": "pain_point",
            "representative": "however, this is slow process",
            "keywords": ["slow", "process", "performance"]
        },
        {
            "category": "文献管理/BibTeX",
            "signal_type": "pain_point",
            "representative": "(and no, i can't switch mendeley so don't recommend that :( )",
            "keywords": ["switch", "mendeley", "pain"]
        },
        {
            "category": "文献管理/BibTeX",
            "signal_type": "pain_point",
            "representative": "metadata zotero pulled was just wrong",
            "keywords": ["metadata", "zotero", "wrong"]
        },
        {
            "category": "格式导出/模板",
            "signal_type": "pain_point",
            "representative": "i am having issue that mendeley Word-addin disappears",
            "keywords": ["word", "addin", "disappears"]
        },
        {
            "category": "本地优先/隐私",
            "signal_type": "constraint",
            "representative": "i need it to work offline without any cloud account",
            "keywords": ["offline", "cloud", "privacy"]
        },
        {
            "category": "AI阅读/问答",
            "signal_type": "usage_scenario",
            "representative": "i use it to summarize long pdf papers and ask questions",
            "keywords": ["pdf", "summarize", "ask"]
        }
    ]

    for tc in test_cases:
        summary = _build_cluster_requirement_summary(
            tc["category"], tc["signal_type"], tc["representative"], tc["keywords"]
        )
        print(f"Original: {tc['representative']}")
        print(f"Category: {tc['category']}")
        print(f"Signal Type: {tc['signal_type']}")
        print(f"New Summary: {summary}")
        print("-" * 20)

if __name__ == "__main__":
    test_summary_generation()
