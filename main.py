import argparse
import json
import os
from pathlib import Path
from paper_tool_feedback_crawler.crawler import (
    CrawlConfig,
    FeedbackCrawler,
    summarize_records,
    write_jsonl,
    write_csv,
    write_summary_json,
    write_summary_markdown,
    write_requirement_summary_json,
    write_requirement_summary_markdown,
    build_ml_requirement_analysis,
    write_ml_requirement_analysis_json,
    write_ml_requirement_analysis_markdown,
    write_lean_requirements_json,
    write_lean_requirements_markdown,
    write_prd_requirements_markdown
)

def main():
    parser = argparse.ArgumentParser(description="Paper Tool Feedback Crawler")
    parser.add_argument("--config", required=True, help="Path to the configuration JSON file")
    parser.add_argument("--output-dir", required=True, help="Directory to save the results")
    parser.add_argument("--max-workers", type=int, help="Maximum number of workers for crawling")
    
    args = parser.parse_args()
    
    config_path = Path(args.config)
    if not config_path.exists():
        print(f"Error: Config file {config_path} not found.")
        return
    
    with open(config_path, "r", encoding="utf-8") as f:
        config_dict = json.load(f)
    
    if args.max_workers:
        config_dict["max_workers"] = args.max_workers
        
    config = CrawlConfig.from_dict(config_dict)
    
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    
    print(f"Starting crawl using config: {args.config}")
    crawler = FeedbackCrawler(config)
    records = crawler.crawl()
    
    print(f"Crawl completed. Collected {len(records)} records.")
    
    # Save raw records
    write_jsonl(records, output_dir / "feedback.jsonl")
    write_csv(records, output_dir / "feedback.csv")
    
    # Generate summaries
    print("Generating summaries...")
    summary = summarize_records(records)
    write_summary_json(summary, output_dir / "tool_summary.json")
    write_summary_markdown(summary, output_dir / "tool_summary.md")
    write_requirement_summary_json(summary, output_dir / "requirement_summary.json")
    write_requirement_summary_markdown(summary, output_dir / "requirement_summary.md")
    
    # Generate ML analysis
    print("Generating ML analysis...")
    ml_analysis = build_ml_requirement_analysis(
        records,
        doc_requirements_path=config.doc_requirements_path or None,
    )
    write_ml_requirement_analysis_json(ml_analysis, output_dir / "ml_requirement_analysis.json")
    write_ml_requirement_analysis_markdown(ml_analysis, output_dir / "ml_requirement_analysis.md")
    write_lean_requirements_json(ml_analysis, output_dir / "lean_requirements.json")
    write_lean_requirements_markdown(ml_analysis, output_dir / "lean_requirements.md")
    write_prd_requirements_markdown(ml_analysis, output_dir / "prd_requirements.md")
    
    print(f"All results saved to {output_dir}")

if __name__ == "__main__":
    main()
