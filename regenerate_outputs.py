
import json
from pathlib import Path
from paper_tool_feedback_crawler.crawler import (
    load_jsonl_records,
    load_config,
    summarize_records,
    build_ml_requirement_analysis,
    write_summary_json,
    write_summary_markdown,
    write_requirement_summary_json,
    write_requirement_summary_markdown,
    write_ml_requirement_analysis_json,
    write_ml_requirement_analysis_markdown,
    write_lean_requirements_json,
    write_lean_requirements_markdown,
    write_prd_requirements_markdown
)

def process_directory(output_dir_name, config_path):
    output_dir = Path(output_dir_name)
    feedback_jsonl = output_dir / "feedback.jsonl"
    
    if not feedback_jsonl.exists():
        print(f"Skipping {output_dir_name}: feedback.jsonl not found.")
        return
    
    print(f"\nProcessing {output_dir_name} using {config_path}...")
    records = load_jsonl_records(feedback_jsonl)
    print(f"Loaded {len(records)} records.")
    
    config = load_config(config_path)
    
    print("Regenerating summaries...")
    summary = summarize_records(records)
    write_summary_json(summary, output_dir / "tool_summary.json")
    write_summary_markdown(summary, output_dir / "tool_summary.md")
    write_requirement_summary_json(summary, output_dir / "requirement_summary.json")
    write_requirement_summary_markdown(summary, output_dir / "requirement_summary.md")
    
    print("Regenerating ML analysis...")
    ml_analysis = build_ml_requirement_analysis(
        records,
        doc_requirements_path=config.doc_requirements_path or None,
    )
    write_ml_requirement_analysis_json(ml_analysis, output_dir / "ml_requirement_analysis.json")
    write_ml_requirement_analysis_markdown(ml_analysis, output_dir / "ml_requirement_analysis.md")
    write_lean_requirements_json(ml_analysis, output_dir / "lean_requirements.json")
    write_lean_requirements_markdown(ml_analysis, output_dir / "lean_requirements.md")
    write_prd_requirements_markdown(ml_analysis, output_dir / "prd_requirements.md")

def main():
    mappings = [
        ("output", "config.example.json"),
        ("output_chinese_expanded", "config.chinese_expanded.json"),
        ("output_chinese_expanded_v2", "config.chinese_expanded.json"),
        ("output_debug_stack", "config.debug_stack.json"),
        ("output_expanded", "config.expanded.json"),
        ("output_github", "config.github_expanded.json"),
        ("output_reachable", "config.reachable.json"),
        ("output_reachable_fast", "config.reachable.fast.json"),
        ("output_weavedoc_doc", "config.weavedoc_doc.json"),
        ("output_weavedoc_doc_clean", "config.weavedoc_doc.json"),
        ("output_weavedoc_doc_fix", "config.weavedoc_doc.json"),
        ("output_weavedoc_doc_fix2", "config.weavedoc_doc.json"),
        ("output_weavedoc_doc_v2", "config.weavedoc_doc.json"),
        ("output_weavedoc_doc_v3", "config.weavedoc_doc.json"),
    ]
    
    for dir_name, config_file in mappings:
        if Path(dir_name).exists():
            process_directory(dir_name, config_file)
    
    print("\nAll directories processed.")

if __name__ == "__main__":
    main()
