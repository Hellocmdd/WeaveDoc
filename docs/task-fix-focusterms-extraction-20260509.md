# 修复 FocusTerms 提取 — 任务清单

> 日期: 2026-05-09
> 状态: 待实施
> 前提: R0-5b (compare 修复) + 锚定词提纯 + Pass 1 拆分 已提交

## 问题

对于纯中文问题（如"这个智能浇花系统如何实现精准灌溉"），`FocusTerms` 始终为空。原因是 `ExtractQueryTokens` 的正则 `[一-鿿]{2,}` 贪婪匹配整句中文（14字黏成一个 token），然后 `IsMeaningfulFocusTerm` 检测到 token 包含 "如何"（在 FocusStopWords 中）→ 整句丢弃。后续 Reranker query 增强、Pass 1a context assembly、Intent boost、Fallback 代表 chunk 选择均在无 FocusTerms 的状态下运行，procedure 类问题稳定性不足。

## 改法

不在 `IsMeaningfulFocusTerm` 过滤器上打补丁，不在 `ExtractQueryTokens` 上改正则。在 token 生成之后、过滤之前，加一步切分——用已有的 FocusStopWords 作为分词边界。

### 核心思路

```
问题: "这个智能浇花系统如何实现精准灌溉"
  → ExtractQueryTokens → ["这个智能浇花系统如何实现精准灌溉"]  (14字，全黏)
  → 沿 FocusStopWords 切开 → ["智能浇花系统", "精准灌溉"]
  → IsMeaningfulFocusTerm(精确匹配) → 两个都通过
  → 排序(短词+ASCII优先) → ["精准灌溉", "智能浇花系统", ...]
  → FocusTerms 不再为空
```

## 任务

### 1. `BuildRetrievalQueryTokens` — 长中文 token 切分（核心）

- **文件**: `csharp-rag-avalonia/Services/LocalAiService.cs`
- **位置**: L1868，`return tokens.ToArray()` 之前
- **改动**: 插入切分逻辑

对每个已收集的 token，若长度 >6 且纯中文（无 ASCII 字符），沿 FocusStopWords 中长度 ≥2 的词（按长度降序，避免"一下"先于"一下子"错误匹配）替换为 `\n` 然后 split，将 ≥2字的片段加入 tokens。

```
示例: "这个智能浇花系统如何实现精准灌溉"
  → 替换 "这个" → "\n智能浇花系统如何实现精准灌溉"
  → 替换 "如何" → "\n智能浇花系统\n实现精准灌溉"
  → 替换 "实现" → "\n智能浇花系统\n\n精准灌溉"
  → split → ["智能浇花系统", "精准灌溉"]
```

### 2. `IsMeaningfulFocusTerm` — 删除子串包含检查（配套）

- **文件**: `csharp-rag-avalonia/Services/LocalAiService.cs`
- **位置**: L1793-1808
- **改动**: 删除纯中文路径下的两段子串检查

删除：
- `FocusStopWords.Any(stopWord => token.Contains(stopWord))` — 子串匹配
- `SupplementalRequestSignals.Any(signal => token.Contains(signal) || signal.Contains(token))` — 子串匹配

保留：
- `FocusStopWords.Contains(token)` — 精确匹配（早已存在）
- `IsSupplementalRequestToken(token)` — 精确匹配（早已存在）
- `token.Length <= 2` — 纯中文短词过滤

切分后的 token 不再包含 "如何""实现" 等 stop word，子串检查冗余。

### 3. `QueryUnderstandingService.BuildProfile` — 排序修正（配套）

- **文件**: `csharp-rag-avalonia/Services/Rag/QueryUnderstandingService.cs`
- **位置**: L10
- **改动**:

```
OrderByDescending(token => token.Length).Take(8)
→ OrderBy(token => token.Length)
   .ThenByDescending(token => token.Any(char.IsAsciiLetterOrDigit))
   .Take(8)
```

与 `ExtractAnchorTerms` 修改模式一致：短词 + ASCII 缩写优先，长碎片排后。

### 4. `BuildLocalProcedureFallbackAnswer` — 回退阈值（兜底）

- **文件**: `csharp-rag-avalonia/Services/LocalAiService.cs`
- **位置**: L3760
- **改动**:

```
ordered.Length < 4 → ordered.Length < 3
```

3/4 部件即可触发 procedure fallback，避免 LLM 随机输出差答案时 fallback 无法救援而返回"未找到"。FocusTerms 修好后 `FindRepresentativeChunk` 信号更强，找到 4/4 的概率也会提升，此条是安全线。

### 5. 编译 + 全量回归 eval

- **目标**: ≥19/20 PASS，stm32-control 恢复稳定 PASS，其余 case 不退化

## 变更范围

| 文件 | 行数 | 改动量 | 风险 |
|------|------|--------|------|
| `LocalAiService.cs` | L1868, L1793, L3760 | ~25行 | 低 — 只改 token 切分和过滤，不动数据结构 |
| `QueryUnderstandingService.cs` | L10 | ~3行 | 低 — 排序方向修正，与 ExtractAnchorTerms 对称 |

## 不做的事

- 不改 `ExtractQueryTokens` 正则 — 被几十处调用，影响面太大
- 不引入中文分词库 — FocusStopWords 本身标注了问题句的边界词（"如何""是什么""怎么"），天然可用
- 不改 `ExpandRetrievalTerms` — 对 procedure 类问题无扩展是已知行为，不在此次修
