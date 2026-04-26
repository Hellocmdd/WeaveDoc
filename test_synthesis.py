
import re
from paper_tool_feedback_crawler.crawler import _synthesize_requirement_title

def main():
    category = "协作/版本管理"
    signal_type = "pain_point"
    keywords = ["版本", "libpng", "及解", "本问", "程的", "装过"]
    
    title = _synthesize_requirement_title(category, signal_type, keywords)
    print(f"Synthesized Title: {title}")
    
    category2 = "双屏编辑/同步"
    keywords2 = ["mar", "中公", "会编", "找到", "来的", "出来"]
    title2 = _synthesize_requirement_title(category2, signal_type, keywords2)
    print(f"Synthesized Title 2: {title2}")

if __name__ == "__main__":
    main()
