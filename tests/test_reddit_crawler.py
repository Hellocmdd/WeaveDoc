from __future__ import annotations

import unittest
from collections import Counter
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch
from zipfile import ZipFile

from bs4 import BeautifulSoup

from paper_tool_feedback_crawler.crawler import (
    CrawlConfig,
    FeedbackCrawler,
    SourceConfig,
    _counter_to_ranked_items,
    build_ml_requirement_analysis,
    extract_docx_text_lines,
    summarize_records,
    write_requirement_summary_markdown,
    write_lean_requirements_markdown,
    write_prd_requirements_markdown,
    write_summary_markdown,
)


class RedditCrawlerTests(unittest.TestCase):
    def _write_test_docx(self, lines: list[str]) -> Path:
        temp_dir = TemporaryDirectory()
        self.addCleanup(temp_dir.cleanup)
        path = Path(temp_dir.name) / "requirements.docx"
        xml_body = "".join(
            f"<w:p><w:r><w:t>{line}</w:t></w:r></w:p>"
            for line in lines
        )
        document_xml = (
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">'
            f"<w:body>{xml_body}</w:body></w:document>"
        )
        with ZipFile(path, "w") as archive:
            archive.writestr("word/document.xml", document_xml)
        return path

    def test_reddit_source_collects_post_and_comments(self) -> None:
        config = CrawlConfig(
            keywords=["Zotero"],
            review_terms=["review", "pros"],
            requirement_terms=[],
            sources=[
                SourceConfig(
                    name="reddit-paper-tools",
                    source_type="reddit_search",
                    subreddits=["academia"],
                    search_terms=["review"],
                    include_comments=True,
                    max_comments=2,
                    crawl_delay_seconds=0,
                )
            ],
            request_timeout_seconds=5,
        )
        crawler = FeedbackCrawler(config)

        search_payload = {
            "data": {
                "children": [
                    {
                        "data": {
                            "title": "Zotero review from a PhD student",
                            "selftext": "My experience is mostly positive.",
                            "author": "alice",
                            "created_utc": 1710000000,
                            "permalink": "/r/academia/comments/abc123/zotero_review/",
                        }
                    }
                ]
            }
        }
        comments_payload = [
            {},
            {
                "data": {
                    "children": [
                        {"kind": "t1", "data": {"body": "Biggest pro is PDF annotation."}},
                        {"kind": "t1", "data": {"body": "The sync setup is annoying."}},
                    ]
                }
            },
        ]

        with (
            patch.object(crawler, "_build_reddit_queries", return_value=['"Zotero" "review"']),
            patch.object(
                FeedbackCrawler,
                "_fetch_json",
                side_effect=[search_payload, comments_payload],
            ),
        ):
            records = crawler.crawl()

        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].author, "alice")
        self.assertEqual(records[0].comment_count, 2)
        self.assertIn("Zotero", records[0].matched_keywords)
        self.assertIn("review", records[0].matched_review_terms)
        self.assertEqual(records[0].overall_sentiment, "mixed")
        self.assertEqual(records[0].tool_mentions[0].tool, "Zotero")
        self.assertIn("Biggest pro is PDF annotation.", records[0].tool_mentions[0].pros)
        self.assertIn("The sync setup is annoying.", records[0].tool_mentions[0].cons)
        self.assertIn("reddit.com", records[0].url)

    def test_evidence_signals_keep_real_feedback_without_literal_review_terms(self) -> None:
        config = CrawlConfig(
            keywords=["Zotero"],
            review_terms=["review"],
            requirement_terms=[],
            sources=[
                SourceConfig(
                    name="reddit-paper-tools",
                    source_type="reddit_search",
                    subreddits=["academia"],
                    include_comments=True,
                    max_comments=3,
                    crawl_delay_seconds=0,
                )
            ],
            request_timeout_seconds=5,
        )
        crawler = FeedbackCrawler(config)

        search_payload = {
            "data": {
                "children": [
                    {
                        "data": {
                            "title": "Should I use Zotero for my thesis?",
                            "selftext": "I am deciding between tools.",
                            "author": "bob",
                            "created_utc": 1710000000,
                            "permalink": "/r/academia/comments/def456/zotero_thesis/",
                        }
                    }
                ]
            }
        }
        comments_payload = [
            {},
            {
                "data": {
                    "children": [
                        {
                            "kind": "t1",
                            "data": {
                                "body": (
                                    "I've used Zotero for 5 years and recommend it. "
                                    "Sync works great."
                                )
                            },
                        },
                        {
                            "kind": "t1",
                            "data": {"body": "The Word plugin can be buggy sometimes."},
                        },
                    ]
                }
            },
        ]

        with (
            patch.object(crawler, "_build_reddit_queries", return_value=['"Zotero"']),
            patch.object(
                FeedbackCrawler,
                "_fetch_json",
                side_effect=[search_payload, comments_payload],
            ),
        ):
            records = crawler.crawl()

        self.assertEqual(len(records), 1)
        mention = records[0].tool_mentions[0]
        self.assertEqual(mention.sentiment_label, "mixed")
        self.assertIn("I've used Zotero for 5 years and recommend it.", mention.recommendations)
        self.assertIn("I've used Zotero for 5 years and recommend it.", mention.experience_reports)
        self.assertIn("Sync works great.", mention.pros)
        self.assertIn("The Word plugin can be buggy sometimes.", mention.cons)

    def test_summary_groups_feedback_by_tool(self) -> None:
        config = CrawlConfig(
            keywords=["Zotero"],
            review_terms=[],
            requirement_terms=[],
            sources=[],
        )
        crawler = FeedbackCrawler(config)

        record = crawler._build_feedback_record(
            source_name="reddit-paper-tools",
            url="https://www.reddit.com/r/academia/comments/ghi789/zotero/",
            title="Zotero for daily research",
            snippet="Zotero has been good for me.",
            content=(
                "Zotero has been good for me. "
                "[comment] I recommend Zotero to students. "
                "[comment] The plugin is buggy."
            ),
            author="carol",
            published_at="2024-01-01T00:00:00+00:00",
            comments=["I recommend Zotero to students.", "The plugin is buggy."],
        )

        self.assertIsNotNone(record)
        summary = summarize_records([record])

        self.assertEqual(summary["record_count"], 1)
        self.assertEqual(summary["tool_summaries"][0]["tool"], "Zotero")
        self.assertEqual(summary["tool_summaries"][0]["record_count"], 1)
        self.assertIn("mixed", summary["tool_summaries"][0]["sentiment_counts"])
        self.assertEqual(
            summary["tool_summaries"][0]["sample_urls"][0],
            "https://www.reddit.com/r/academia/comments/ghi789/zotero/",
        )

    def test_v2ex_source_collects_topic_and_replies(self) -> None:
        config = CrawlConfig(
            keywords=["Paperlib", "Zotero"],
            review_terms=["推荐", "review", "recommend"],
            requirement_terms=[],
            sources=[
                SourceConfig(
                    name="v2ex-paper-tools",
                    source_type="v2ex_topics",
                    start_urls=["https://www.v2ex.com/go/create"],
                    max_pages=1,
                    search_limit=10,
                    max_comments=2,
                    crawl_delay_seconds=0,
                )
            ],
            request_timeout_seconds=5,
            keyword_aliases={"Paperlib": ["paperlib"]},
        )
        crawler = FeedbackCrawler(config)

        list_html = """
        <html>
          <body>
            <div class="cell item">
              <span class="item_title">
                <a href="/t/123456">用了 Paperlib 两周，体验不错</a>
              </span>
            </div>
          </body>
        </html>
        """
        topic_html = """
        <html>
          <body>
            <div id="Main">
              <div class="box">
                <div class="header">
                  <h1>用了 Paperlib 两周，体验不错</h1>
                  <small class="gray">
                    <a href="/member/alice">alice</a> · 2026-04-20 10:00:00 +08:00 · 123 次点击
                  </small>
                </div>
                <div class="topic_content">
                  我最近一直在用 Paperlib 管理论文，界面很好，推荐给做文献管理的人。
                </div>
              </div>
              <div class="box">
                <div class="reply_content">我也在用 Paperlib，同步很稳定。</div>
                <div class="reply_content">不过 Zotero 插件生态更强，Paperlib 的批注还差一点。</div>
              </div>
            </div>
          </body>
        </html>
        """

        soups = {
            "https://www.v2ex.com/go/create": BeautifulSoup(list_html, "html.parser"),
            "https://www.v2ex.com/t/123456": BeautifulSoup(topic_html, "html.parser"),
        }

        with patch.object(
            FeedbackCrawler,
            "_fetch_soup",
            side_effect=lambda url: soups.get(url),
        ):
            records = crawler.crawl()

        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].source, "v2ex-paper-tools")
        self.assertEqual(records[0].author, "alice")
        self.assertEqual(records[0].comment_count, 2)
        self.assertIn("Paperlib", records[0].matched_keywords)
        self.assertTrue(records[0].tool_mentions)
        mentions = {item.tool: item for item in records[0].tool_mentions}
        self.assertIn("Paperlib", mentions)
        self.assertIn("推荐给做文献管理的人。", "".join(mentions["Paperlib"].recommendations))
        self.assertIn("同步很稳定。", "".join(mentions["Paperlib"].pros))

    def test_producthunt_source_collects_summary_and_reviews(self) -> None:
        config = CrawlConfig(
            keywords=["Consensus"],
            review_terms=["recommend", "worth it"],
            requirement_terms=[],
            sources=[
                SourceConfig(
                    name="producthunt-paper-tools",
                    source_type="producthunt_reviews",
                    start_urls=["https://www.producthunt.com/products/consensus-2"],
                    max_comments=3,
                    crawl_delay_seconds=0,
                )
            ],
            request_timeout_seconds=5,
        )
        crawler = FeedbackCrawler(config)

        html = """
        <html>
          <head>
            <meta name="description" content="Consensus is an AI search engine for research papers." />
          </head>
          <body>
            <h1>Consensus</h1>
            <h2>Evidence-based answers from scientific research</h2>
            <div>Launched in 2023</div>
            <div>Leave a review</div>
            <div>Based on 11 reviews</div>
            <div>Summarized with AI</div>
            <div>
              Consensus helps me get trustworthy answers fast and is worth it for research-heavy work.
            </div>
            <div>Pros</div>
            <article>
              <div>Chris Guest</div>
              <div>Topology •1 review</div>
              <div>This works like a dream for literature reviews. I recommend it to my students.</div>
              <div>Helpful</div>
              <div>Share</div>
              <div>Report</div>
            </article>
            <article>
              <div>Maksim Mamchur</div>
              <div>SubSchool •69 reviews</div>
              <div>Clickable links are useful, but coverage is still limited in niche topics.</div>
              <div>Helpful</div>
              <div>Share</div>
              <div>Report</div>
            </article>
          </body>
        </html>
        """

        with patch.object(
            FeedbackCrawler,
            "_fetch_soup",
            return_value=BeautifulSoup(html, "html.parser"),
        ):
            records = crawler.crawl()

        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].source, "producthunt-paper-tools")
        self.assertEqual(records[0].author, "Product Hunt reviewers")
        self.assertEqual(records[0].published_at, "Launched in 2023")
        self.assertEqual(records[0].comment_count, 2)
        self.assertIn("Consensus", records[0].matched_keywords)
        self.assertEqual(records[0].overall_sentiment, "mixed")
        mention = records[0].tool_mentions[0]
        self.assertIn("recommend it to my students.", " ".join(mention.recommendations))
        self.assertIn("This works like a dream for literature reviews.", " ".join(mention.pros))
        self.assertIn("coverage is still limited in niche topics.", " ".join(mention.cons))

    def test_capterra_source_collects_review_page(self) -> None:
        config = CrawlConfig(
            keywords=["Paperpile"],
            review_terms=["recommend", "review"],
            requirement_terms=[],
            sources=[
                SourceConfig(
                    name="capterra-paper-tools",
                    source_type="capterra_reviews",
                    start_urls=["https://www.capterra.com/p/101431/Paperpile/"],
                    max_comments=3,
                    crawl_delay_seconds=0,
                )
            ],
            request_timeout_seconds=5,
        )
        crawler = FeedbackCrawler(config)

        html = """
        <html>
          <head>
            <meta name="description" content="Paperpile helps researchers collect, organize and cite papers." />
          </head>
          <body>
            <h1>Paperpile</h1>
            <div>Updated on April 20, 2026</div>
            <div>Pros and Cons</div>
            <div>Paperpile is easy to use and saves me time when citing in Google Docs.</div>
            <div>The PDF annotation workflow is useful, but offline support is limited.</div>
            <article>
              <div>Overall rating</div>
              <div>4.7</div>
              <div>Paperpile is easy to use and I recommend it to grad students.</div>
              <div>Reviewer source</div>
            </article>
            <article>
              <div>Time used</div>
              <div>The citation workflow is smooth, but collaboration options are limited.</div>
              <div>Company size</div>
            </article>
          </body>
        </html>
        """

        with patch.object(
            FeedbackCrawler,
            "_fetch_soup",
            return_value=BeautifulSoup(html, "html.parser"),
        ):
            records = crawler.crawl()

        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].source, "capterra-paper-tools")
        self.assertEqual(records[0].author, "Capterra reviewers")
        self.assertEqual(records[0].published_at, "Updated on April 20, 2026")
        self.assertEqual(records[0].comment_count, 2)
        self.assertIn("Paperpile", records[0].matched_keywords)
        mention = records[0].tool_mentions[0]
        self.assertEqual(mention.sentiment_label, "mixed")
        self.assertIn("recommend it to grad students.", " ".join(mention.recommendations))
        self.assertIn("Paperpile is easy to use", " ".join(mention.pros))
        self.assertIn("limited", " ".join(mention.cons))

    def test_duckduckgo_html_source_collects_search_results(self) -> None:
        config = CrawlConfig(
            keywords=["Zotero"],
            review_terms=["experience", "bug"],
            requirement_terms=["markdown", "pdf"],
            sources=[
                SourceConfig(
                    name="duckduckgo-cnblogs",
                    source_type="duckduckgo_html_search",
                    search_domains=["cnblogs.com"],
                    search_terms=["markdown pdf"],
                    search_limit=5,
                    crawl_delay_seconds=0,
                )
            ],
            request_timeout_seconds=5,
        )
        crawler = FeedbackCrawler(config)

        html = """
        <html>
          <body>
            <div class="result">
              <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.cnblogs.com%2Fdemo%2Fp%2F1.html">
                Zotero + Markdown + PDF workflow
              </a>
              <a class="result__snippet">
                My experience with Zotero in a markdown pdf workflow, including export bugs.
              </a>
            </div>
          </body>
        </html>
        """

        with patch.object(
            FeedbackCrawler,
            "_fetch_soup",
            return_value=BeautifulSoup(html, "html.parser"),
        ):
            records = crawler.crawl()

        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].source, "duckduckgo-cnblogs")
        self.assertEqual(records[0].url, "https://www.cnblogs.com/demo/p/1.html")
        self.assertIn("Zotero", records[0].matched_keywords)
        self.assertIn("markdown", records[0].matched_requirement_terms)

    def test_summary_clusters_near_duplicate_evidence(self) -> None:
        ranked = _counter_to_ranked_items(
            Counter(
                {
                    "Paperpile is easy to use and saves me time.": 1,
                    "Paperpile is easy to use and saves me a lot of time.": 1,
                    "The collaboration options are limited.": 1,
                }
            )
        )

        self.assertEqual(ranked[0]["count"], 2)
        self.assertEqual(ranked[0]["variants"], 2)
        self.assertIn("Paperpile is easy to use", ranked[0]["text"])

    def test_summary_exposes_highlight_fields(self) -> None:
        config = CrawlConfig(
            keywords=["Paperpile"],
            review_terms=[],
            requirement_terms=[],
            sources=[],
        )
        crawler = FeedbackCrawler(config)

        record = crawler._build_feedback_record(
            source_name="capterra-paper-tools",
            url="https://www.capterra.com/p/101431/Paperpile/",
            title="Paperpile",
            snippet="Paperpile is easy to use when citing in Google Docs.",
            content=(
                "Paperpile is easy to use when citing in Google Docs. "
                "[comment] Paperpile is easy to use and saves me time when citing in Google Docs. "
                "[comment] The collaboration options are limited. "
                "[comment] I recommend Paperpile to graduate students."
            ),
            author="reviewers",
            published_at="2026-04-20",
            comments=[
                "Paperpile is easy to use and saves me time when citing in Google Docs.",
                "The collaboration options are limited.",
                "I recommend Paperpile to graduate students.",
            ],
        )

        self.assertIsNotNone(record)
        summary = summarize_records([record])
        tool_summary = summary["tool_summaries"][0]

        self.assertIn("positive_highlights", tool_summary)
        self.assertIn("negative_highlights", tool_summary)
        self.assertIn("recommendation_highlights", tool_summary)
        self.assertIn("specific_positive_feedback", tool_summary)
        self.assertIn("specific_negative_feedback", tool_summary)
        self.assertIn("specific_positive_summaries", tool_summary)
        self.assertIn("specific_negative_summaries", tool_summary)
        self.assertTrue(tool_summary["positive_highlights"])
        self.assertTrue(tool_summary["negative_highlights"])
        self.assertTrue(tool_summary["recommendation_highlights"])
        self.assertTrue(tool_summary["specific_positive_feedback"])
        self.assertTrue(tool_summary["specific_negative_feedback"])
        self.assertTrue(tool_summary["specific_positive_summaries"])
        self.assertTrue(tool_summary["specific_negative_summaries"])

        positive_feedback = tool_summary["specific_positive_feedback"][0]
        negative_feedback = tool_summary["specific_negative_feedback"][0]
        self.assertIn("aspect", positive_feedback)
        self.assertIn("reason", positive_feedback)
        self.assertIn("summary_zh", positive_feedback)
        self.assertIn("aspect", negative_feedback)
        self.assertIn("reason", negative_feedback)
        self.assertIn("summary_zh", negative_feedback)
        self.assertIn(
            "引用/写作流程",
            " ".join(item["aspect"] for item in tool_summary["specific_positive_feedback"]),
        )
        self.assertIn(
            "collaboration",
            " ".join(item["reason"].lower() for item in tool_summary["specific_negative_feedback"]),
        )
        self.assertIn("用户主要喜欢", tool_summary["specific_positive_summaries"][0])
        self.assertIn("Google Docs", tool_summary["specific_positive_summaries"][0])
        self.assertIn("用户主要不满意", tool_summary["specific_negative_summaries"][0])
        self.assertIn("协作", tool_summary["specific_negative_summaries"][0])

    def test_negative_summary_stays_specific_when_aspect_rules_do_not_match(self) -> None:
        config = CrawlConfig(
            keywords=["Zotero"],
            review_terms=[],
            requirement_terms=[],
            sources=[],
        )
        crawler = FeedbackCrawler(config)

        record = crawler._build_feedback_record(
            source_name="reddit-paper-tools",
            url="https://example.com/zotero",
            title="Zotero feedback",
            snippet="Zotero review",
            content=(
                "Zotero review. "
                "[comment] The duplicate merge flow is clunky and confusing."
            ),
            author="tester",
            published_at="2026-04-20",
            comments=["The duplicate merge flow is clunky and confusing."],
        )

        self.assertIsNotNone(record)
        summary = summarize_records([record])
        tool_summary = summary["tool_summaries"][0]
        negative_summary = tool_summary["specific_negative_summaries"][0]

        self.assertIn("重复条目", negative_summary)
        self.assertNotIn("整体体验", negative_summary)
        self.assertNotIn("明显短板", negative_summary)

    def test_single_tool_post_ignores_generic_non_tool_sentences(self) -> None:
        config = CrawlConfig(
            keywords=["Consensus"],
            review_terms=[],
            requirement_terms=[],
            sources=[],
        )
        crawler = FeedbackCrawler(config)

        record = crawler._build_feedback_record(
            source_name="reddit-paper-tools",
            url="https://example.com/consensus",
            title="Trying to verify something college professor said",
            snippet="I'm having trouble finding any studies to back up what my professor said.",
            content=(
                "Trying to verify something college professor said. "
                "[comment] I'm having trouble finding any studies to back up what my professor said. "
                "[comment] Consensus is useful when I need to check whether the first 100 results answer a question I have."
            ),
            author="tester",
            published_at="2026-04-20",
            comments=[
                "I'm having trouble finding any studies to back up what my professor said.",
                "Consensus is useful when I need to check whether the first 100 results answer a question I have.",
            ],
        )

        self.assertIsNotNone(record)
        mention = record.tool_mentions[0]
        self.assertEqual(
            mention.pros,
            ["Consensus is useful when I need to check whether the first 100 results answer a question I have."],
        )
        self.assertFalse(mention.cons)

    def test_specific_feedback_filters_generic_phrases(self) -> None:
        config = CrawlConfig(
            keywords=["EndNote"],
            review_terms=[],
            requirement_terms=[],
            sources=[],
        )
        crawler = FeedbackCrawler(config)

        record = crawler._build_feedback_record(
            source_name="reddit-paper-tools",
            url="https://example.com/endnote",
            title="EndNote feedback",
            snippet="I like EndNote.",
            content=(
                "I like EndNote. "
                "[comment] The Word plugin can be buggy sometimes. "
                "[comment] I like to mark up the pdfs a lot."
            ),
            author="tester",
            published_at="2026-04-20",
            comments=[
                "The Word plugin can be buggy sometimes.",
                "I like to mark up the pdfs a lot.",
            ],
        )

        self.assertIsNotNone(record)
        summary = summarize_records([record])
        tool_summary = summary["tool_summaries"][0]

        self.assertTrue(tool_summary["specific_positive_feedback"])
        self.assertTrue(tool_summary["specific_negative_feedback"])
        self.assertNotIn(
            "I like EndNote",
            [item["reason"] for item in tool_summary["specific_positive_feedback"]],
        )
        self.assertIn(
            "The Word plugin can be buggy sometimes.",
            [item["examples"][0] for item in tool_summary["specific_negative_feedback"]],
        )

    def test_summary_markdown_includes_examples_for_specific_feedback(self) -> None:
        summary = {
            "generated_at": "2026-04-20T00:00:00+00:00",
            "record_count": 1,
            "tool_summaries": [
                {
                    "tool": "Paperpile",
                    "record_count": 1,
                    "sentiment_counts": {"mixed": 1},
                    "review_terms": {},
                    "top_pros": [],
                    "top_cons": [],
                    "top_recommendations": [],
                    "top_experience_reports": [],
                    "specific_positive_feedback": [
                        {
                            "count": 1,
                            "summary_zh": "用户主要喜欢 Paperpile 的引用流程，因为在 Google Docs 里插入引用更省时间。",
                            "examples": ["Paperpile is easy to use and saves me time when citing in Google Docs."],
                        }
                    ],
                    "specific_negative_feedback": [],
                    "positive_highlights": [],
                    "negative_highlights": [],
                    "specific_positive_highlights": [],
                    "specific_negative_highlights": [],
                    "specific_positive_summaries": [],
                    "specific_negative_summaries": [],
                    "recommendation_highlights": [],
                    "experience_highlights": [],
                    "sample_urls": [],
                }
            ],
        }

        output_path = Path("output/test_tool_summary.md")
        write_summary_markdown(summary, output_path)
        rendered = output_path.read_text(encoding="utf-8")

        self.assertIn("Examples:", rendered)
        self.assertIn("citing in Google Docs", rendered)

    def test_requirement_signals_are_extracted_for_product_needs(self) -> None:
        config = CrawlConfig(
            keywords=["Zotero"],
            review_terms=[],
            requirement_terms=["workflow", "offline", "docx", "privacy", "linux"],
            sources=[],
        )
        crawler = FeedbackCrawler(config)

        record = crawler._build_feedback_record(
            source_name="reddit-weavedoc-requirements",
            url="https://example.com/requirements",
            title="Need an offline Zotero workflow on Linux",
            snippet="I want a local-first workflow that can export docx.",
            content=(
                "Need an offline Zotero workflow on Linux. "
                "[comment] I want a local-first workflow that can export docx without switching between apps. "
                "[comment] Privacy matters, so I do not want to upload unpublished papers to cloud AI tools."
            ),
            author="tester",
            published_at="2026-04-20",
            comments=[
                "I want a local-first workflow that can export docx without switching between apps.",
                "Privacy matters, so I do not want to upload unpublished papers to cloud AI tools.",
            ],
        )

        self.assertIsNotNone(record)
        self.assertIn("offline", record.matched_requirement_terms)
        self.assertTrue(record.requirement_signals)
        categories = {item.category for item in record.requirement_signals}
        signal_types = {item.signal_type for item in record.requirement_signals}
        self.assertIn("本地优先/隐私", categories)
        self.assertIn("格式导出/模板", categories)
        self.assertIn("desired_feature", signal_types)
        self.assertIn("constraint", signal_types)

    def test_summary_contains_requirement_summaries(self) -> None:
        config = CrawlConfig(
            keywords=["Zotero"],
            review_terms=[],
            requirement_terms=["workflow", "offline", "docx"],
            sources=[],
        )
        crawler = FeedbackCrawler(config)

        record = crawler._build_feedback_record(
            source_name="reddit-weavedoc-requirements",
            url="https://example.com/requirements-summary",
            title="Offline Zotero writing workflow",
            snippet="Need markdown to docx export.",
            content=(
                "Offline Zotero writing workflow. "
                "[comment] Need markdown to docx export with stable citation support."
            ),
            author="tester",
            published_at="2026-04-20",
            comments=["Need markdown to docx export with stable citation support."],
        )

        self.assertIsNotNone(record)
        summary = summarize_records([record])
        self.assertTrue(summary["requirement_summaries"])
        requirement_summary = summary["requirement_summaries"][0]
        self.assertIn("category", requirement_summary)
        self.assertIn("signal_type_counts", requirement_summary)
        self.assertIn("examples", requirement_summary)

    def test_requirement_summary_markdown_renders_requirement_section(self) -> None:
        summary = {
            "generated_at": "2026-04-20T00:00:00+00:00",
            "record_count": 1,
            "requirement_summaries": [
                {
                    "category": "本地优先/隐私",
                    "record_count": 1,
                    "signal_type_counts": {"constraint": 1},
                    "top_signals": [],
                    "top_tools": {"Zotero": 1},
                    "signal_highlights": ["约束条件集中在“本地优先/隐私”：用户强调 不想把未发表论文传到云端。"],
                    "examples": [
                        {
                            "signal_type": "constraint",
                            "summary_zh": "约束条件集中在“本地优先/隐私”：用户强调 不想把未发表论文传到云端。",
                            "evidence": "Privacy matters, so I do not want to upload unpublished papers to cloud AI tools."
                        }
                    ],
                    "sample_urls": ["https://example.com/requirements"]
                }
            ],
            "tool_summaries": []
        }

        output_path = Path("output/test_requirement_summary.md")
        write_requirement_summary_markdown(summary, output_path)
        rendered = output_path.read_text(encoding="utf-8")

        self.assertIn("Requirement Insights", rendered)
        self.assertIn("本地优先/隐私", rendered)
        self.assertIn("未发表论文", rendered)

    def test_hn_source_collects_requirement_records(self) -> None:
        config = CrawlConfig(
            keywords=["Zotero"],
            review_terms=[],
            requirement_terms=["offline", "workflow"],
            sources=[
                SourceConfig(
                    name="hn-weavedoc-requirements",
                    source_type="hn_algolia_search",
                    search_limit=5,
                    crawl_delay_seconds=0,
                )
            ],
        )
        crawler = FeedbackCrawler(config)
        payload = {
            "hits": [
                {
                    "title": "Need an offline Zotero workflow",
                    "story_text": "Looking for a local-first paper writing workflow on Linux.",
                    "url": "https://example.com/hn-1",
                    "author": "pg",
                    "created_at": "2026-04-20T00:00:00Z",
                    "objectID": "1",
                }
            ]
        }

        with patch.object(FeedbackCrawler, "_fetch_json", return_value=payload):
            records = crawler.crawl()

        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].source, "hn-weavedoc-requirements")
        self.assertTrue(records[0].requirement_signals)

    def test_stackoverflow_source_collects_requirement_records(self) -> None:
        config = CrawlConfig(
            keywords=["Markdown"],
            review_terms=[],
            requirement_terms=["pdf", "export"],
            sources=[
                SourceConfig(
                    name="stackoverflow-weavedoc-requirements",
                    source_type="stackoverflow_search",
                    search_site="stackoverflow",
                    search_tags=["markdown"],
                    search_limit=5,
                    crawl_delay_seconds=0,
                )
            ],
        )
        crawler = FeedbackCrawler(config)
        payload = {
            "items": [
                {
                    "title": "Markdown to PDF export with citations",
                    "excerpt": "<p>Need a reliable workflow to export Markdown to PDF with citation support.</p>",
                    "tags": ["markdown", "pdf"],
                    "link": "https://stackoverflow.com/questions/1",
                    "creation_date": 1710000000,
                    "owner": {"display_name": "alice"},
                }
            ]
        }

        with patch.object(FeedbackCrawler, "_fetch_json", return_value=payload):
            records = crawler.crawl()

        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].source, "stackoverflow-weavedoc-requirements")
        self.assertIn("export", records[0].matched_requirement_terms)

    def test_ml_requirement_analysis_returns_lean_requirements(self) -> None:
        config = CrawlConfig(
            keywords=["Zotero", "Paperpile"],
            review_terms=[],
            requirement_terms=["offline", "docx", "workflow", "privacy"],
            sources=[],
        )
        crawler = FeedbackCrawler(config)
        record_a = crawler._build_feedback_record(
            source_name="reddit",
            url="https://example.com/a",
            title="Need an offline Zotero workflow",
            snippet="Need docx export on Linux.",
            content="Need an offline Zotero workflow. [comment] Need docx export on Linux.",
            author="u1",
            published_at="2026-04-20",
            comments=["Need docx export on Linux."],
        )
        record_b = crawler._build_feedback_record(
            source_name="hn",
            url="https://example.com/b",
            title="Zotero privacy-first paper workflow",
            snippet="Do not upload unpublished Zotero papers to cloud tools.",
            content=(
                "Zotero privacy-first paper workflow. "
                "[comment] Do not upload unpublished Zotero papers to cloud tools."
            ),
            author="u2",
            published_at="2026-04-20",
            comments=["Do not upload unpublished Zotero papers to cloud tools."],
        )

        self.assertIsNotNone(record_a)
        self.assertIsNotNone(record_b)
        analysis = build_ml_requirement_analysis([record_a, record_b])

        self.assertTrue(analysis["clusters"])
        self.assertTrue(analysis["lean_requirements"])
        self.assertIn("summary", analysis["lean_requirements"][0])
        self.assertIn(
            analysis["lean_requirements"][0]["signal_type"],
            {"desired_feature", "pain_point", "constraint"},
        )

    def test_github_issues_source_collects_issue(self) -> None:
        config = CrawlConfig(
            keywords=["Zotero"],
            review_terms=["review", "experience"],
            requirement_terms=["citation", "export", "bibtex"],
            sources=[
                SourceConfig(
                    name="github-issues-paper-tools",
                    source_type="github_issues_search",
                    search_terms=["review", "citation"],
                    search_limit=5,
                    crawl_delay_seconds=0,
                )
            ],
        )
        crawler = FeedbackCrawler(config)
        payload = {
            "items": [
                {
                    "title": "Zotero citation export workflow review",
                    "body": "Need better BibTeX export for Zotero in our writing workflow.",
                    "html_url": "https://github.com/example/repo/issues/1",
                    "created_at": "2026-04-20T10:00:00Z",
                    "user": {"login": "alice"},
                    "repository_url": "https://api.github.com/repos/example/repo",
                }
            ]
        }

        with patch.object(FeedbackCrawler, "_fetch_json", return_value=payload):
            records = crawler.crawl()

        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].source, "github-issues-paper-tools")
        self.assertIn("Zotero", records[0].matched_keywords)
        self.assertIn("citation", records[0].matched_requirement_terms)

    def test_ml_requirement_analysis_prioritizes_actionable_comment_clusters(self) -> None:
        config = CrawlConfig(
            keywords=["Zotero"],
            review_terms=[],
            requirement_terms=["docx", "linux", "privacy", "cloud"],
            sources=[],
        )
        crawler = FeedbackCrawler(config)
        record = crawler._build_feedback_record(
            source_name="reddit",
            url="https://example.com/c",
            title="I use Zotero every day for my literature review",
            snippet="Need docx export on Linux.",
            content=(
                "I use Zotero every day for my literature review. "
                "[comment] Need docx export on Linux. "
                "[comment] Do not upload unpublished papers to cloud tools."
            ),
            author="u3",
            published_at="2026-04-20",
            comments=[
                "Need docx export on Linux.",
                "Do not upload unpublished papers to cloud tools.",
            ],
        )

        self.assertIsNotNone(record)
        analysis = build_ml_requirement_analysis([record])

        self.assertGreaterEqual(len(analysis["lean_requirements"]), 2)
        self.assertEqual(analysis["lean_requirements"][0]["signal_type"], "desired_feature")
        self.assertEqual(analysis["lean_requirements"][1]["signal_type"], "constraint")
        self.assertIn("Docx", analysis["lean_requirements"][0]["summary"])
        self.assertIn("云端工具", analysis["lean_requirements"][1]["summary"])

    def test_extract_docx_text_lines_and_focus_sections_ignore_management_noise(self) -> None:
        doc_path = self._write_test_docx(
            [
                "1.3 任务来源",
                "任务要求：完成一个具备 Markdown 双屏联动编辑、PDF 实时解析、模板化格式导出及本地离线 AI 辅助阅读功能的桌面端软件产品。",
                "2.3 性能要求",
                "数据一致性：导出docx需满足模板定义的关键样式项一致率不低于95%。",
                "3.2 关键角色及职责分配",
                "项目组长：负责统筹项目进度与站会。",
            ]
        )

        lines = extract_docx_text_lines(doc_path)

        self.assertIn("1.3 任务来源", lines)
        analysis = build_ml_requirement_analysis([], doc_requirements_path=doc_path)
        doc_summaries = [item["summary"] for item in analysis["doc_requirement_signals"]]
        joined = " ".join(doc_summaries)
        self.assertIn("Markdown 双屏联动编辑", joined)
        self.assertIn("导出docx", joined)
        self.assertNotIn("项目进度", joined)

    def test_ml_requirement_analysis_uses_doc_alignment_to_demote_project_management_noise(self) -> None:
        doc_path = self._write_test_docx(
            [
                "1.3 任务来源",
                "任务要求：完成一个具备 Markdown 双屏联动编辑、PDF 实时解析、模板化格式导出及本地离线 AI 辅助阅读功能的桌面端软件产品。",
                "2.3 性能要求",
                "跨平台兼容性：优先适配 Windows 10/11 (x64)，兼容主流 Linux 发行版。",
                "安全性：所有文献数据及 AI 处理过程均在本地完成，不产生任何非用户授权的外部网络通信。",
            ]
        )
        config = CrawlConfig(
            keywords=["WeaveDoc"],
            review_terms=[],
            requirement_terms=["workflow", "export", "offline", "linux"],
            sources=[],
        )
        crawler = FeedbackCrawler(config)
        relevant = crawler._build_feedback_record(
            source_name="hn",
            url="https://example.com/relevant",
            title="Need WeaveDoc markdown and PDF dual-pane workflow",
            snippet="Need WeaveDoc docx export on Linux with offline support.",
            content=(
                "Need WeaveDoc markdown and PDF dual-pane workflow. "
                "[comment] Need WeaveDoc docx export on Linux with offline support."
            ),
            author="u1",
            published_at="2026-04-20",
            comments=["Need WeaveDoc docx export on Linux with offline support."],
        )
        noisy = crawler._build_feedback_record(
            source_name="github",
            url="https://example.com/noisy",
            title="Need WeaveDoc gantt chart export for weekly standup",
            snippet="Need WeaveDoc WBS board export for project tracking.",
            content=(
                "Need WeaveDoc gantt chart export for weekly standup. "
                "[comment] Need WeaveDoc WBS board export for project tracking."
            ),
            author="u2",
            published_at="2026-04-20",
            comments=["Need WeaveDoc WBS board export for project tracking."],
        )

        self.assertIsNotNone(relevant)
        self.assertIsNotNone(noisy)
        analysis = build_ml_requirement_analysis(
            [relevant, noisy],
            doc_requirements_path=doc_path,
        )

        summaries = [item["summary"] for item in analysis["lean_requirements"]]
        relevant_index = next(
            index for index, summary in enumerate(summaries) if "Linux" in summary or "dual-pane" in summary
        )
        noisy_index = next(index for index, summary in enumerate(summaries) if "gantt" in summary.lower())
        self.assertLess(relevant_index, noisy_index)

    def test_lean_requirements_markdown_renders(self) -> None:
        analysis = {
            "lean_requirements": [
                {
                    "priority": 1,
                    "category": "本地优先/隐私",
                    "signal_type": "constraint",
                    "summary": "本地优先/隐私: 满足 不想把未发表论文传到云端 约束",
                    "keywords": ["隐私", "云端"],
                }
            ]
        }
        output_path = Path("output/test_lean_requirements.md")
        write_lean_requirements_markdown(analysis, output_path)
        rendered = output_path.read_text(encoding="utf-8")

        self.assertIn("Lean Requirement List", rendered)
        self.assertIn("本地优先/隐私", rendered)

    def test_prd_requirements_markdown_renders(self) -> None:
        analysis = {
            "lean_requirements": [
                {
                    "priority": 1,
                    "category": "本地优先/隐私",
                    "signal_type": "constraint",
                    "summary": "本地优先/隐私: 不要把未发表论文上传到云端工具",
                },
                {
                    "priority": 2,
                    "category": "格式导出/模板",
                    "signal_type": "desired_feature",
                    "summary": "格式导出/模板: 支持 Linux 下的 Docx 导出",
                }
            ]
        }
        output_path = Path("output/test_prd_requirements.md")
        write_prd_requirements_markdown(analysis, output_path)
        rendered = output_path.read_text(encoding="utf-8")

        self.assertIn("精简需求清单", rendered)
        self.assertIn("必须满足", rendered)
        self.assertIn("应支持", rendered)


if __name__ == "__main__":
    unittest.main()
