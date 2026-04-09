# AFD 模板扩展实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增 2 个 AFD 模板 JSON 文件（课程报告、实验报告），满足验收标准 F-04 模板切换需求。

**Architecture:** 纯数据文件创建任务。两个新 JSON 模板遵循与 `default-thesis.json` 相同的 AFD schema，包含 meta / defaults / styles / headerFooter 四个顶层节点。不修改任何现有代码。

**Tech Stack:** JSON, AFD Template Schema v1

---

## File Structure

| 操作 | 文件路径 | 职责 |
|------|----------|------|
| Create | `src/WeaveDoc.Converter/Config/TemplateSchemas/course-report.json` | 课程报告模板 |
| Create | `src/WeaveDoc.Converter/Config/TemplateSchemas/lab-report.json` | 实验报告模板 |

不修改任何现有文件。

---

### Task 1: 创建 course-report.json

**Files:**
- Create: `src/WeaveDoc.Converter/Config/TemplateSchemas/course-report.json`

- [ ] **Step 1: 创建课程报告模板文件**

```json
{
  "$schema": "https://weavedoc.dev/schemas/afd-template-v1.json",
  "meta": {
    "templateName": "课程报告",
    "version": "1.0.0",
    "author": "WeaveDoc",
    "description": "高校课程报告通用模板"
  },
  "defaults": {
    "fontFamily": "宋体",
    "fontSize": 12,
    "lineSpacing": 1.5,
    "pageSize": { "width": 210, "height": 297 },
    "margins": { "top": 25, "bottom": 25, "left": 25, "right": 25 }
  },
  "styles": {
    "heading1": {
      "displayName": "标题 1",
      "fontFamily": "黑体",
      "fontSize": 16,
      "bold": true,
      "alignment": "center",
      "spaceBefore": 24,
      "spaceAfter": 18,
      "lineSpacing": 1.5
    },
    "heading2": {
      "displayName": "标题 2",
      "fontFamily": "黑体",
      "fontSize": 15,
      "bold": true,
      "alignment": "left",
      "spaceBefore": 18,
      "spaceAfter": 12,
      "lineSpacing": 1.5
    },
    "heading3": {
      "displayName": "标题 3",
      "fontFamily": "黑体",
      "fontSize": 14,
      "bold": true,
      "alignment": "left",
      "spaceBefore": 12,
      "spaceAfter": 6,
      "lineSpacing": 1.5
    },
    "body": {
      "displayName": "正文",
      "fontFamily": "宋体",
      "fontSize": 12,
      "firstLineIndent": 24,
      "lineSpacing": 1.5
    }
  },
  "headerFooter": {
    "header": {
      "text": "课程报告",
      "fontFamily": "宋体",
      "fontSize": 10.5,
      "alignment": "center"
    },
    "footer": {
      "pageNumbering": true,
      "format": "arabic",
      "alignment": "center",
      "startPage": 1
    }
  }
}
```

- [ ] **Step 2: 验证 JSON 可被 AfdParser 解析**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "ReferenceDocBuilder_Build_CreatesValidDocx" -v normal`

> 此命令验证现有测试通过，确保新文件未破坏已有功能。由于新文件是独立的 JSON，不影响现有测试。

实际上，新模板不参与现有测试（现有测试硬编码使用 `default-thesis.json` 或 `CreateTestTemplate()`），所以只需确认文件存在且 JSON 格式合法。

手动验证：在项目根目录运行以下命令确认文件存在：

```bash
ls src/WeaveDoc.Converter/Config/TemplateSchemas/course-report.json
```

Expected: 文件存在。

---

### Task 2: 创建 lab-report.json

**Files:**
- Create: `src/WeaveDoc.Converter/Config/TemplateSchemas/lab-report.json`

- [ ] **Step 1: 创建实验报告模板文件**

```json
{
  "$schema": "https://weavedoc.dev/schemas/afd-template-v1.json",
  "meta": {
    "templateName": "实验报告",
    "version": "1.0.0",
    "author": "WeaveDoc",
    "description": "理工科实验报告通用模板"
  },
  "defaults": {
    "fontFamily": "宋体",
    "fontSize": 12,
    "lineSpacing": 1.5,
    "pageSize": { "width": 210, "height": 297 },
    "margins": { "top": 25.4, "bottom": 25.4, "left": 31.7, "right": 31.7 }
  },
  "styles": {
    "heading1": {
      "displayName": "标题 1",
      "fontFamily": "黑体",
      "fontSize": 18,
      "bold": true,
      "alignment": "center",
      "spaceBefore": 24,
      "spaceAfter": 18,
      "lineSpacing": 1.5
    },
    "heading2": {
      "displayName": "标题 2",
      "fontFamily": "黑体",
      "fontSize": 15,
      "bold": true,
      "alignment": "left",
      "spaceBefore": 18,
      "spaceAfter": 12,
      "lineSpacing": 1.5
    },
    "heading3": {
      "displayName": "标题 3",
      "fontFamily": "黑体",
      "fontSize": 12,
      "bold": true,
      "alignment": "left",
      "spaceBefore": 12,
      "spaceAfter": 6,
      "lineSpacing": 1.5
    },
    "body": {
      "displayName": "正文",
      "fontFamily": "宋体",
      "fontSize": 12,
      "firstLineIndent": 24,
      "lineSpacing": 1.5
    }
  },
  "headerFooter": {
    "header": {
      "text": "实验报告",
      "fontFamily": "宋体",
      "fontSize": 9,
      "alignment": "center"
    },
    "footer": {
      "pageNumbering": true,
      "format": "arabic",
      "alignment": "center",
      "startPage": 1
    }
  }
}
```

- [ ] **Step 2: 验证文件存在**

```bash
ls src/WeaveDoc.Converter/Config/TemplateSchemas/lab-report.json
```

Expected: 文件存在。

---

### Task 3: 运行全量测试确认无回归

- [ ] **Step 1: 运行全部 Converter 测试**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -v normal`

Expected: 所有测试 PASS。新模板是纯数据文件，不影响现有测试。

---

### Task 4: 提交

- [ ] **Step 1: 提交新增模板文件和设计文档**

```bash
git add src/WeaveDoc.Converter/Config/TemplateSchemas/course-report.json
git add src/WeaveDoc.Converter/Config/TemplateSchemas/lab-report.json
git add docs/superpowers/specs/2026-04-09-afd-template-expansion-design.md
git add docs/superpowers/plans/2026-04-09-afd-template-expansion.md
git commit -m "feat(converter): add course-report and lab-report AFD templates"
```

---

## Self-Review

**1. Spec coverage:**
- ✅ course-report.json: meta / defaults / styles (heading1-3, body) / headerFooter — 全部覆盖
- ✅ lab-report.json: meta / defaults / styles (heading1-3, body) / headerFooter — 全部覆盖
- ✅ 三模板差异化：页边距 / 字号 / 页眉页脚各有不同
- ✅ 不修改现有代码 / 测试

**2. Placeholder scan:** 无 TBD/TODO/模糊描述。所有 JSON 内容完整给出。

**3. Type consistency:** JSON 属性名与 AfdTemplate C# 模型的 camelCase 约定一致（propertyNameCaseInsensitive = true），fontSize 值均为正数，满足 AfdParser.Validate() 要求。
