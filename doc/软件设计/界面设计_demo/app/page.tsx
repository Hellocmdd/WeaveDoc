"use client"

import { useState } from "react"
import {
  ArrowUpDown,
  AlertTriangle,
  Bold,
  BookOpen,
  Bot,
  Check,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Clock,
  Code,
  Copy,
  Database,
  Download,
  ExternalLink,
  FileDown,
  FileText,
  Filter,
  FolderOpen,
  HardDrive,
  Heading1,
  Heading2,
  Keyboard,
  Image,
  Info,
  Italic,
  Link,
  List,
  ListOrdered,
  MessageSquare,
  Microscope,
  Minus,
  Moon,
  MoreHorizontal,
  Plus,
  Quote,
  Redo2,
  RefreshCw,
  Save,
  Search,
  Send,
  Settings as SettingsIcon,
  ShieldCheck,
  Square,
  Sun,
  Upload,
  Undo2,
  Wrench,
  X,
  XCircle,
  Zap,
  ZoomIn,
  ZoomOut,
} from "lucide-react"

const palette = {
  neutral950: "#0D1117",
  neutral900: "#161B22",
  neutral800: "#21262D",
  neutral700: "#30363D",
  neutral500: "#6E7681",
  neutral400: "#8B949E",
  neutral300: "#C9D1D9",
  neutral100: "#E6EDF3",
  neutral50: "#F0F6FC",
  neutral0: "#FFFFFF",
  neutralL10: "#F8F9FA",
  neutralL20: "#EAEEF2",
  neutralL30: "#D8DEE4",
  neutralL60: "#57606A",
  neutralL80: "#1C2128",
  blue700: "#0550AE",
  blue600: "#0969DA",
  blue500: "#1F6FEB",
  blue400: "#388BFD",
  blue300: "#58A6FF",
  blue200: "#A5D6FF",
  blue100: "#CAE8FF",
  blue50: "#DDF4FF",
  green700: "#1A7F37",
  green400: "#3FB950",
  green100: "#DCFFE4",
  yellow700: "#9A6700",
  yellow400: "#D29922",
  yellow100: "#FFF8C5",
  red700: "#CF222E",
  red400: "#F85149",
  red100: "#FFEBE9",
  purpleCode: "#C586C0",
  orangeCode: "#CE9178",
  closeHover: "#E81123",
  aiUnconfigured: "#888888",
  aiUnloaded: "#F85149",
  aiLoading: "#D29922",
  aiIdle: "#3FB950",
}

const editorZone = {
  background: palette.neutral950,
  foreground: palette.neutral100,
  panel: palette.neutral900,
  raised: palette.neutral800,
  border: palette.neutral700,
  muted: palette.neutral400,
  body: palette.neutral300,
  accent: palette.blue300,
  accentStrong: palette.blue500,
  quote: palette.green400,
  heading: palette.purpleCode,
  string: palette.orangeCode,
  hover: palette.neutral800,
}

const themes = {
  light: {
    name: "Light",
    background: palette.neutral0,
    foreground: palette.neutralL80,
    panel: palette.neutralL10,
    card: palette.neutral0,
    popover: palette.neutral0,
    primary: palette.blue600,
    primaryHover: palette.blue500,
    primaryForeground: palette.neutral0,
    secondary: palette.neutralL20,
    secondaryForeground: palette.neutralL80,
    muted: palette.neutral50,
    mutedForeground: palette.neutralL60,
    accent: palette.blue50,
    accentForeground: palette.blue600,
    border: palette.neutralL30,
    input: palette.neutralL20,
    ring: palette.blue700,
    titlebar: palette.neutral900,
    titlebarForeground: palette.neutral300,
    titlebarMuted: palette.neutral400,
    chromeHover: palette.neutralL20,
    controlHover: palette.neutralL30,
    split: palette.neutralL30,
    paper: palette.neutral0,
    paperWorkspace: palette.neutralL20,
    paperText: palette.neutralL80,
    success: palette.green700,
    successSurface: palette.green100,
    warning: palette.yellow700,
    warningSurface: palette.yellow100,
    destructive: palette.red700,
    aiBubbleBorder: palette.blue600,
    aiStatus: palette.aiIdle,
    editor: editorZone,
  },
  dark: {
    name: "Dark",
    background: palette.neutral950,
    foreground: palette.neutral100,
    panel: palette.neutral900,
    card: palette.neutral900,
    popover: palette.neutral800,
    primary: palette.blue300,
    primaryHover: palette.blue400,
    primaryForeground: palette.neutral950,
    secondary: palette.neutral800,
    secondaryForeground: palette.neutral100,
    muted: palette.neutral800,
    mutedForeground: palette.neutral400,
    accent: palette.neutral800,
    accentForeground: palette.blue300,
    border: palette.neutral700,
    input: palette.neutral800,
    ring: palette.blue300,
    titlebar: palette.neutral950,
    titlebarForeground: palette.neutral100,
    titlebarMuted: palette.neutral400,
    chromeHover: palette.neutral800,
    controlHover: palette.neutral700,
    split: palette.neutral700,
    paper: palette.neutral0,
    paperWorkspace: palette.neutral800,
    paperText: palette.neutralL80,
    success: palette.green400,
    successSurface: "rgba(63, 185, 80, 0.14)",
    warning: palette.yellow400,
    warningSurface: "rgba(210, 153, 34, 0.18)",
    destructive: palette.red400,
    aiBubbleBorder: palette.blue300,
    aiStatus: palette.aiIdle,
    editor: editorZone,
  },
} as const

type AppTheme = (typeof themes)[keyof typeof themes]
type ModalPage = "export" | "settings" | "setup" | "about" | null

export default function WeaveDocApp() {
  const [activeTab, setActiveTab] = useState<"ai" | "literature" | "snapshot">("ai")
  const [pdfZoom, setPdfZoom] = useState(100)
  const [isDarkTheme, setIsDarkTheme] = useState(false)
  const [modalPage, setModalPage] = useState<ModalPage>(null)
  const [showOcrToolbar, setShowOcrToolbar] = useState(true)
  const theme = isDarkTheme ? themes.dark : themes.light
  const totalPages = 24

  return (
    <div
      className="flex flex-col h-screen w-[1440px] mx-auto font-['Segoe_UI',_'Noto_Sans',_system-ui,_sans-serif] text-[13px] overflow-hidden select-none"
      style={{ backgroundColor: theme.background, color: theme.foreground }}
    >
      <div
        className="flex items-center justify-between h-8 px-2 shrink-0"
        style={{ backgroundColor: theme.titlebar }}
      >
        <div className="flex items-center gap-2">
          <svg width="14" height="14" viewBox="0 0 14 14" fill="none" className="shrink-0">
            <rect x="0" y="0" width="6" height="6" rx="1" fill={palette.blue600} />
            <rect x="8" y="0" width="6" height="6" rx="1" fill={palette.blue300} opacity="0.75" />
            <rect x="0" y="8" width="6" height="6" rx="1" fill={palette.blue400} opacity="0.65" />
            <rect x="8" y="8" width="6" height="6" rx="1" fill={palette.blue500} opacity="0.45" />
          </svg>
          <span className="text-[11px]" style={{ color: theme.titlebarForeground }}>
            WeaveDoc - 论文草稿.md
          </span>
        </div>
        <div className="flex items-center">
          <ChromeButton icon={<Minus className="w-4 h-4" strokeWidth={1.5} />} theme={theme} />
          <ChromeButton icon={<Square className="w-3 h-3" strokeWidth={1.5} />} theme={theme} />
          <ChromeButton icon={<X className="w-4 h-4" strokeWidth={1.5} />} theme={theme} destructive />
        </div>
      </div>

      <div
        className="flex items-center h-7 px-2 shrink-0"
        style={{ backgroundColor: theme.panel, borderBottom: `1px solid ${theme.border}` }}
      >
        {["File", "Edit", "View", "AI", "Literature", "Export", "Help"].map((menu) => (
          <button
            key={menu}
            className="px-3 h-full text-[11px] rounded-sm"
            style={{ color: theme.foreground }}
            onClick={() => {
              if (menu === "File") setModalPage("settings")
              if (menu === "AI") setModalPage("settings")
              if (menu === "Export") setModalPage("export")
              if (menu === "Help") setModalPage("about")
            }}
          >
            {menu}
          </button>
        ))}
        <div className="flex-1" />
        <button
          className="p-1 rounded-sm"
          onClick={() => setIsDarkTheme(!isDarkTheme)}
          style={{ color: theme.mutedForeground, backgroundColor: "transparent" }}
          title={`Switch to ${isDarkTheme ? "light" : "dark"} theme`}
        >
          {isDarkTheme ? <Sun className="w-4 h-4" strokeWidth={1.5} /> : <Moon className="w-4 h-4" strokeWidth={1.5} />}
        </button>
      </div>

      <div
        className="flex items-center h-9 px-2 gap-1 shrink-0"
        style={{ backgroundColor: theme.card, borderBottom: `1px solid ${theme.border}` }}
      >
        <ToolbarButton icon={<FileText className="w-3.5 h-3.5" strokeWidth={1.5} />} tooltip="新建" theme={theme} />
        <ToolbarButton icon={<FolderOpen className="w-3.5 h-3.5" strokeWidth={1.5} />} tooltip="打开" theme={theme} />
        <ToolbarButton icon={<Save className="w-3.5 h-3.5" strokeWidth={1.5} />} tooltip="保存" theme={theme} />
        <ToolbarDivider theme={theme} />
        <ToolbarButton
          icon={<Download className="w-3.5 h-3.5" strokeWidth={1.5} />}
          tooltip="导出"
          theme={theme}
          primary
          onClick={() => setModalPage("export")}
        />
        <ToolbarDivider theme={theme} />
        <ToolbarButton
          icon={<Bot className="w-3.5 h-3.5" strokeWidth={1.5} />}
          tooltip="AI 助手"
          active
          theme={theme}
        />
        <ToolbarDivider theme={theme} />
        <ToolbarButton icon={<Undo2 className="w-3.5 h-3.5" strokeWidth={1.5} />} tooltip="撤销" theme={theme} />
        <ToolbarButton icon={<Redo2 className="w-3.5 h-3.5" strokeWidth={1.5} />} tooltip="重做" theme={theme} />
        <ToolbarDivider theme={theme} />
        <ToolbarButton
          icon={<Wrench className="w-3.5 h-3.5" strokeWidth={1.5} />}
          tooltip="首次配置向导"
          theme={theme}
          onClick={() => setModalPage("setup")}
        />
        <ToolbarButton
          icon={<SettingsIcon className="w-3.5 h-3.5" strokeWidth={1.5} />}
          tooltip="设置"
          theme={theme}
          onClick={() => setModalPage("settings")}
        />
        <div className="flex-1" />
        <div
          className="flex items-center gap-1 px-2 py-1 text-[10px]"
          style={{ backgroundColor: theme.input, border: `1px solid ${theme.border}`, color: theme.mutedForeground }}
        >
          <Search className="w-3 h-3" strokeWidth={1.5} />
          <span>搜索 (Ctrl+P)</span>
        </div>
      </div>

      <div className="flex flex-1 overflow-hidden">
        <div
          className="flex flex-col shrink-0"
          style={{ width: "35%", minWidth: "300px", maxWidth: "50%", backgroundColor: theme.panel }}
        >
          <div
            className="flex items-center justify-between h-7 px-2"
            style={{ backgroundColor: theme.secondary, borderBottom: `1px solid ${theme.border}` }}
          >
            <div className="flex items-center gap-0.5">
              <IconButton icon={<ChevronLeft className="w-3.5 h-3.5" strokeWidth={1.5} />} theme={theme} />
              <span className="text-[10px] px-1.5 font-mono" style={{ color: theme.foreground }}>
                1 / {totalPages}
              </span>
              <IconButton icon={<ChevronRight className="w-3.5 h-3.5" strokeWidth={1.5} />} theme={theme} />
            </div>
            <div className="flex items-center gap-0.5">
              <IconButton
                icon={<ZoomOut className="w-3.5 h-3.5" strokeWidth={1.5} />}
                theme={theme}
                onClick={() => setPdfZoom(Math.max(50, pdfZoom - 10))}
              />
              <span className="text-[10px] w-8 text-center font-mono" style={{ color: theme.foreground }}>
                {pdfZoom}%
              </span>
              <IconButton
                icon={<ZoomIn className="w-3.5 h-3.5" strokeWidth={1.5} />}
                theme={theme}
                onClick={() => setPdfZoom(Math.min(200, pdfZoom + 10))}
              />
            </div>
          </div>

          <div className="flex items-center h-6 px-1" style={{ backgroundColor: theme.secondary }}>
            <div
              className="flex items-center gap-1 px-2 py-0.5 text-[10px]"
              style={{ backgroundColor: theme.card, color: theme.foreground, borderBottom: `2px solid ${theme.primary}` }}
            >
              <FileText className="w-3 h-3" strokeWidth={1.5} />
              <span className="max-w-[120px] truncate">Attention Is All You Need.pdf</span>
              <IconButton icon={<X className="w-2.5 h-2.5" strokeWidth={1.5} />} theme={theme} tight />
            </div>
            <IconButton icon={<Plus className="w-2.5 h-2.5" strokeWidth={1.5} />} theme={theme} tight />
          </div>

          <div className="relative flex-1 overflow-auto p-2" style={{ backgroundColor: theme.paperWorkspace }}>
            <div
              className="mx-auto shadow-sm"
              style={{
                width: "92%",
                minHeight: "calc(100% - 2px)",
                backgroundColor: theme.paper,
                border: `1px solid ${theme.border}`,
              }}
            >
              <div className="p-6 text-[11px] leading-relaxed" style={{ color: theme.paperText }}>
                <h1 className="text-[16px] font-bold text-center mb-4">Attention Is All You Need</h1>
                <p className="text-[10px] text-center mb-4" style={{ color: palette.neutralL60 }}>
                  Ashish Vaswani, Noam Shazeer, Niki Parmar, Jakob Uszkoreit,<br />
                  Llion Jones, Aidan N. Gomez, Lukasz Kaiser, Illia Polosukhin
                </p>

                <h2 className="font-semibold mb-2 text-[12px]">Abstract</h2>
                <p className="mb-4 text-justify">
                  The dominant sequence transduction models are based on complex recurrent or
                  convolutional neural networks that include an encoder and a decoder. The best
                  performing models also connect the encoder and decoder through an attention
                  mechanism. We propose a new simple network architecture, the Transformer,
                  based solely on attention mechanisms, dispensing with recurrence and convolutions
                  entirely.
                </p>

                <p className="mb-4 text-justify">
                  <span style={{ backgroundColor: palette.yellow100 }}>
                    Experiments on two machine translation tasks show these models to be superior in
                    quality while being more parallelizable and requiring significantly less time to
                    train.
                  </span>{" "}
                  Our model achieves 28.4 BLEU on the WMT 2014 English-to-German translation task,
                  improving over the existing best results, including ensembles, by over 2 BLEU.
                </p>

                <h2 className="font-semibold mb-2 text-[12px]">1 Introduction</h2>
                <p className="text-justify">
                  Recurrent neural networks, long short-term memory and gated recurrent neural
                  networks in particular, have been firmly established as state of the art approaches
                  in sequence modeling and transduction problems such as language modeling and
                  machine translation.
                </p>

                <button
                  className="mt-5 flex items-center justify-center mx-auto px-3 py-2 border"
                  style={{ borderColor: theme.primary, color: theme.accentForeground, backgroundColor: theme.accent }}
                  onClick={() => setShowOcrToolbar(true)}
                >
                  <Microscope className="w-3.5 h-3.5 mr-1" strokeWidth={1.5} />
                  模拟框选公式
                </button>
              </div>
            </div>
            {showOcrToolbar && <OcrFloatingToolbar theme={theme} onClose={() => setShowOcrToolbar(false)} />}
          </div>
        </div>

        <div className="w-0.5 cursor-col-resize" style={{ backgroundColor: theme.split }} />

        <div className="flex-1 flex flex-col min-w-[400px]" style={{ backgroundColor: theme.editor.background }}>
          <div
            className="flex items-center h-8"
            style={{ backgroundColor: theme.editor.panel, borderBottom: `1px solid ${theme.editor.background}` }}
          >
            <div
              className="flex items-center gap-1 px-3 py-1 text-[10px]"
              style={{
                backgroundColor: theme.editor.background,
                color: theme.editor.foreground,
                borderBottom: `2px solid ${theme.editor.accentStrong}`,
              }}
            >
              <FileText className="w-3 h-3" strokeWidth={1.5} />
              <span>论文草稿.md</span>
              <span className="w-1.5 h-1.5 rounded-full ml-1" style={{ backgroundColor: theme.editor.foreground }} title="已修改" />
            </div>
            <div
              className="flex items-center gap-1 px-3 py-1 text-[10px] cursor-pointer"
              style={{ backgroundColor: theme.editor.raised, color: theme.editor.muted }}
            >
              <FileText className="w-3 h-3" strokeWidth={1.5} />
              <span>notes.md</span>
            </div>
          </div>

          <div
            className="flex items-center h-7 px-2 gap-0.5"
            style={{ backgroundColor: theme.editor.panel, borderBottom: `1px solid ${theme.editor.border}` }}
          >
            <EditorToolbarButton icon={<Heading1 className="w-3.5 h-3.5" strokeWidth={1.5} />} theme={theme} />
            <EditorToolbarButton icon={<Heading2 className="w-3.5 h-3.5" strokeWidth={1.5} />} theme={theme} />
            <EditorDivider theme={theme} />
            <EditorToolbarButton icon={<Bold className="w-3.5 h-3.5" strokeWidth={1.5} />} theme={theme} />
            <EditorToolbarButton icon={<Italic className="w-3.5 h-3.5" strokeWidth={1.5} />} theme={theme} />
            <EditorDivider theme={theme} />
            <EditorToolbarButton icon={<List className="w-3.5 h-3.5" strokeWidth={1.5} />} theme={theme} />
            <EditorToolbarButton icon={<ListOrdered className="w-3.5 h-3.5" strokeWidth={1.5} />} theme={theme} />
            <EditorToolbarButton icon={<Quote className="w-3.5 h-3.5" strokeWidth={1.5} />} theme={theme} />
            <EditorDivider theme={theme} />
            <EditorToolbarButton icon={<Link className="w-3.5 h-3.5" strokeWidth={1.5} />} theme={theme} />
            <EditorToolbarButton icon={<Image className="w-3.5 h-3.5" strokeWidth={1.5} />} theme={theme} />
            <EditorToolbarButton icon={<Code className="w-3.5 h-3.5" strokeWidth={1.5} />} theme={theme} />
            <div className="flex-1" />
            <button className="flex items-center gap-1 px-2 py-1 text-[10px]" style={{ color: theme.editor.accent }}>
              <Bot className="w-3 h-3" strokeWidth={1.5} />
              AI 润色
            </button>
          </div>

          <div
            className="flex-1 overflow-auto p-3 font-['JetBrains_Mono',_'Cascadia_Code',_'Fira_Code',_monospace] text-[13px] leading-5"
            style={{ backgroundColor: theme.editor.background, color: theme.editor.foreground }}
          >
            <div className="max-w-[680px] mx-auto">
              <div style={{ color: theme.editor.accent }}># 基于 Transformer 架构的学术写作辅助系统研究</div>
              <br />
              <div style={{ color: theme.editor.quote }}>{">"} 摘要: 本文探讨了如何利用大语言模型技术辅助学术论文写作...</div>
              <br />
              <div style={{ color: theme.editor.heading }}>## 1. 引言</div>
              <br />
              <div style={{ color: theme.editor.body }}>
                近年来，随着深度学习技术的快速发展，自然语言处理领域取得了显著突破。
                特别是 <span style={{ color: theme.editor.string }}>Transformer 架构</span> 的提出
                <span style={{ color: theme.editor.accent }}> [@vaswani2017attention]</span>，
                彻底改变了序列建模的范式。
              </div>
              <br />
              <div style={{ color: theme.editor.body }}>Vaswani 等人在其开创性工作中指出：</div>
              <br />
              <div className="pl-3" style={{ borderLeft: `2px solid ${theme.editor.accentStrong}`, color: theme.editor.quote }}>
                {'"'}The Transformer allows for significantly more parallelization and can reach a new
                state of the art in translation quality after being trained for as little as twelve
                hours on eight P100 GPUs.{'"'}
              </div>
              <br />
              <div style={{ color: theme.editor.heading }}>## 2. 相关工作</div>
              <br />
              <div style={{ color: theme.editor.body }}>
                在本节中，我们将回顾与学术写作辅助相关的研究工作。主要包括：
              </div>
              <br />
              <div style={{ color: theme.editor.body }}>
                <span style={{ color: theme.editor.accent }}>-</span> 语言模型的发展历程<br />
                <span style={{ color: theme.editor.accent }}>-</span> 文本生成技术<br />
                <span style={{ color: theme.editor.accent }}>-</span> 引用推荐系统<br />
                <span style={{ color: theme.editor.accent }}>-</span> 写作辅助工具
              </div>
              <br />
              <div style={{ color: theme.editor.heading }}>### 2.1 预训练语言模型</div>
              <br />
              <div style={{ color: theme.editor.body }}>
                BERT <span style={{ color: theme.editor.accent }}>[@devlin2019bert]</span> 和 GPT 系列模型
                <span style={{ color: theme.editor.accent }}> [@brown2020language]</span> 的出现，
                标志着自然语言处理进入了预训练时代。
              </div>
              <br />
              <div style={{ color: palette.neutral500 }}>
                <span className="animate-pulse">|</span>
              </div>
            </div>
          </div>

          <div
            className="flex items-center h-6 px-2 text-[10px] font-mono"
            style={{ backgroundColor: theme.editor.panel, borderTop: `1px solid ${theme.editor.border}`, color: theme.editor.muted }}
          >
            <span>论文草稿.md</span>
            <span className="mx-1.5">&gt;</span>
            <span>2. 相关工作</span>
            <span className="mx-1.5">&gt;</span>
            <span style={{ color: theme.editor.foreground }}>2.1 预训练语言模型</span>
          </div>
        </div>

        <div className="w-0.5 cursor-col-resize" style={{ backgroundColor: theme.split }} />

        <div
          className="flex flex-col shrink-0"
          style={{ width: "280px", backgroundColor: theme.panel, borderLeft: `1px solid ${theme.border}` }}
        >
          <div
            className="flex items-center h-8"
            style={{ backgroundColor: theme.secondary, borderBottom: `1px solid ${theme.border}` }}
          >
            <TabButton
              icon={<MessageSquare className="w-3.5 h-3.5" strokeWidth={1.5} />}
              label="AI 问答"
              active={activeTab === "ai"}
              onClick={() => setActiveTab("ai")}
              theme={theme}
            />
            <TabButton
              icon={<BookOpen className="w-3.5 h-3.5" strokeWidth={1.5} />}
              label="文献"
              active={activeTab === "literature"}
              onClick={() => setActiveTab("literature")}
              theme={theme}
            />
            <TabButton
              icon={<Clock className="w-3.5 h-3.5" strokeWidth={1.5} />}
              label="快照"
              active={activeTab === "snapshot"}
              onClick={() => setActiveTab("snapshot")}
              theme={theme}
            />
          </div>

          <div className="flex-1 overflow-hidden flex flex-col">
            {activeTab === "ai" && <AIPanel theme={theme} />}
            {activeTab === "literature" && <LiteraturePanel theme={theme} />}
            {activeTab === "snapshot" && <SnapshotPanel theme={theme} />}
          </div>
        </div>
      </div>

      <div
        className="flex items-center justify-between h-[22px] px-2 text-[9px] font-mono shrink-0"
        style={{ backgroundColor: theme.secondary, color: theme.mutedForeground, borderTop: `1px solid ${theme.border}` }}
      >
        <div className="flex items-center gap-2">
          <span>Ln 42, Col 18</span>
          <StatusSeparator theme={theme} />
          <span>字数: 1,847</span>
          <StatusSeparator theme={theme} />
          <span>引用: 12</span>
          <StatusSeparator theme={theme} />
          <span className="flex items-center gap-0.5">
            <CheckCircle2 className="w-2.5 h-2.5" style={{ color: theme.success }} strokeWidth={1.5} />
            同步滚动
          </span>
        </div>
        <div className="flex items-center gap-2">
          <span className="flex items-center gap-1">
            <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: theme.aiStatus }} />
            <Bot className="w-2.5 h-2.5" strokeWidth={1.5} />
            Qwen2.5-7B
          </span>
          <StatusSeparator theme={theme} />
          <span className="flex items-center gap-0.5">
            <span style={{ color: theme.warning }}>内存: 72%</span>
            <span style={{ color: theme.mutedForeground }}>(5.8GB)</span>
          </span>
        </div>
      </div>

      {modalPage === "export" && <ExportDialog theme={theme} onClose={() => setModalPage(null)} />}
      {modalPage === "settings" && <SettingsDialog theme={theme} onClose={() => setModalPage(null)} />}
      {modalPage === "setup" && <SetupWizardDialog theme={theme} onClose={() => setModalPage(null)} />}
      {modalPage === "about" && <AboutDialog theme={theme} onClose={() => setModalPage(null)} />}
    </div>
  )
}

function ChromeButton({ icon, theme, destructive }: { icon: React.ReactNode; theme: AppTheme; destructive?: boolean }) {
  return (
    <button
      className="w-11 h-8 flex items-center justify-center"
      style={{ color: theme.titlebarMuted }}
      onMouseEnter={(event) => {
        event.currentTarget.style.backgroundColor = destructive ? palette.closeHover : theme.editor.hover
      }}
      onMouseLeave={(event) => {
        event.currentTarget.style.backgroundColor = "transparent"
      }}
    >
      {icon}
    </button>
  )
}

function ToolbarButton({
  icon,
  tooltip,
  active,
  primary,
  theme,
  onClick,
}: {
  icon: React.ReactNode
  tooltip: string
  active?: boolean
  primary?: boolean
  theme: AppTheme
  onClick?: () => void
}) {
  return (
    <button
      className="p-1.5"
      style={{
        backgroundColor: primary ? theme.primary : active ? theme.accent : "transparent",
        color: primary ? theme.primaryForeground : active ? theme.accentForeground : theme.mutedForeground,
      }}
      title={tooltip}
      onClick={onClick}
    >
      {icon}
    </button>
  )
}

function ToolbarDivider({ theme }: { theme: AppTheme }) {
  return <div className="w-px h-4 mx-1" style={{ backgroundColor: theme.border }} />
}

function IconButton({
  icon,
  theme,
  onClick,
  tight,
}: {
  icon: React.ReactNode
  theme: AppTheme
  onClick?: () => void
  tight?: boolean
}) {
  return (
    <button className={tight ? "p-0.5" : "p-1"} style={{ color: theme.mutedForeground }} onClick={onClick}>
      {icon}
    </button>
  )
}

function EditorToolbarButton({ icon, theme }: { icon: React.ReactNode; theme: AppTheme }) {
  return (
    <button className="p-1" style={{ color: theme.editor.muted }}>
      {icon}
    </button>
  )
}

function EditorDivider({ theme }: { theme: AppTheme }) {
  return <span className="w-px h-3.5 mx-1" style={{ backgroundColor: theme.editor.border }} />
}

function TabButton({
  icon,
  label,
  active,
  onClick,
  theme,
}: {
  icon: React.ReactNode
  label: string
  active: boolean
  onClick: () => void
  theme: AppTheme
}) {
  return (
    <button
      className="flex items-center gap-1 px-2.5 h-full text-[10px]"
      style={{
        borderBottom: active ? `2px solid ${theme.primary}` : "2px solid transparent",
        color: active ? theme.foreground : theme.mutedForeground,
        backgroundColor: active ? theme.card : "transparent",
      }}
      onClick={onClick}
    >
      {icon}
      {label}
    </button>
  )
}

function StatusSeparator({ theme }: { theme: AppTheme }) {
  return <span style={{ color: theme.border }}>|</span>
}

function AIPanel({ theme }: { theme: AppTheme }) {
  return (
    <>
      <div className="flex items-center justify-between px-2 py-1.5 h-7" style={{ borderBottom: `1px solid ${theme.border}` }}>
        <span className="text-[10px] font-medium" style={{ color: theme.foreground }}>Qwen2.5-7B · 3.2 t/s</span>
        <div className="flex items-center gap-1 text-[9px]" style={{ color: theme.mutedForeground }}>
          <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: theme.aiStatus }} />
          <span>就绪 · RAM 42%</span>
        </div>
      </div>

      <div className="flex-1 overflow-auto p-2 space-y-2">
        <div className="flex items-center justify-between text-[10px]" style={{ color: theme.mutedForeground }}>
          <span>基于已加载的 PDF 文献提问</span>
          <button style={{ color: theme.accentForeground }}>Clear</button>
        </div>
        <div className="p-2" style={{ backgroundColor: theme.card, borderLeft: `2px solid ${theme.aiBubbleBorder}` }}>
          <p className="text-[11px] leading-4" style={{ color: theme.foreground }}>
            根据您引用的 Transformer 论文，我建议在引言部分补充以下内容：
          </p>
          <ol className="mt-1.5 text-[11px] leading-4 space-y-0.5 list-decimal list-inside" style={{ color: theme.foreground }}>
            <li>注意力机制的核心创新点</li>
            <li>与 RNN/LSTM 的对比优势</li>
            <li>在 NLP 领域的深远影响</li>
          </ol>
          <div className="flex items-center gap-2 mt-2 pt-1.5" style={{ borderTop: `1px solid ${theme.border}` }}>
            <PanelLink icon={<Copy className="w-2.5 h-2.5" strokeWidth={1.5} />} label="复制" theme={theme} />
            <PanelLink icon={<FileDown className="w-2.5 h-2.5" strokeWidth={1.5} />} label="插入" theme={theme} />
            <PanelLink icon={<RefreshCw className="w-2.5 h-2.5" strokeWidth={1.5} />} label="重新生成" theme={theme} />
          </div>
        </div>

        <div className="flex justify-end">
          <div className="p-2 max-w-[85%]" style={{ backgroundColor: theme.input }}>
            <p className="text-[11px] leading-4" style={{ color: theme.foreground }}>
              请帮我扩展第二点，关于与 RNN/LSTM 的对比优势
            </p>
          </div>
        </div>

        <div className="p-2" style={{ backgroundColor: theme.card, borderLeft: `2px solid ${theme.aiBubbleBorder}` }}>
          <p className="text-[11px] leading-4 font-medium" style={{ color: theme.foreground }}>
            Transformer 相比 RNN/LSTM 的主要优势：
          </p>
          <ul className="mt-1.5 text-[11px] leading-4 space-y-1" style={{ color: theme.foreground }}>
            {["并行计算：无需按时序逐步处理", "长距离依赖：直接建模任意距离依赖", "梯度传播：避免梯度消失/爆炸"].map((text, index) => (
              <li key={text} className="flex gap-1.5">
                <span style={{ color: theme.accentForeground }}>{index + 1}.</span>
                <span>{text}</span>
              </li>
            ))}
          </ul>
          <div className="mt-1.5 flex items-center gap-1">
            <span
              className="inline-flex items-center gap-0.5 px-1 py-0.5 text-[9px] cursor-pointer hover:underline"
              style={{ backgroundColor: theme.accent, color: theme.accentForeground }}
            >
              [1] p.3
              <ExternalLink className="w-2.5 h-2.5" strokeWidth={1.5} />
            </span>
          </div>
        </div>
      </div>

      <div className="p-2" style={{ borderTop: `1px solid ${theme.border}`, backgroundColor: theme.card }}>
        <div className="text-[10px] mb-1" style={{ color: theme.mutedForeground }}>基于已加载的 PDF 文献提问</div>
        <div style={{ backgroundColor: theme.input, border: `1px solid ${theme.border}` }}>
          <textarea
            className="w-full px-2 py-1.5 text-[11px] bg-transparent resize-none outline-none"
            style={{ color: theme.foreground }}
            placeholder="输入问题，或选中文本后提问..."
            rows={2}
          />
          <div className="flex items-center justify-between px-1.5 py-1" style={{ borderTop: `1px solid ${theme.border}` }}>
            <div className="flex items-center gap-0.5">
              <IconButton icon={<BookOpen className="w-3 h-3" strokeWidth={1.5} />} theme={theme} />
              <IconButton icon={<FileText className="w-3 h-3" strokeWidth={1.5} />} theme={theme} />
            </div>
            <button className="p-1" style={{ backgroundColor: theme.primary, color: theme.primaryForeground }}>
              <Send className="w-3 h-3" strokeWidth={1.5} />
            </button>
          </div>
        </div>
      </div>
    </>
  )
}

function PanelLink({ icon, label, theme }: { icon: React.ReactNode; label: string; theme: AppTheme }) {
  return (
    <button className="text-[10px] flex items-center gap-0.5 hover:underline" style={{ color: theme.accentForeground }}>
      {icon}
      {label}
    </button>
  )
}

function LiteraturePanel({ theme }: { theme: AppTheme }) {
  const references = [
    { id: 1, key: "vaswani2017attention", title: "Attention Is All You Need", authors: "Vaswani et al.", year: 2017, cited: true },
    { id: 2, key: "devlin2019bert", title: "BERT: Pre-training of Deep Bidirectional...", authors: "Devlin et al.", year: 2019, cited: true },
    { id: 3, key: "brown2020language", title: "Language Models are Few-Shot Learners", authors: "Brown et al.", year: 2020, cited: true },
    { id: 4, key: "radford2019language", title: "Language Models are Unsupervised...", authors: "Radford et al.", year: 2019, cited: false },
  ]

  return (
    <>
      <div className="flex items-center justify-between px-2 py-1.5" style={{ borderBottom: `1px solid ${theme.border}` }}>
        <span className="text-[10px] font-medium uppercase tracking-wide" style={{ color: theme.mutedForeground }}>
          文献库
        </span>
        <div className="flex items-center gap-0.5">
          <IconButton icon={<Filter className="w-3 h-3" strokeWidth={1.5} />} theme={theme} />
          <IconButton icon={<ArrowUpDown className="w-3 h-3" strokeWidth={1.5} />} theme={theme} />
        </div>
      </div>

      <div className="flex items-center justify-between px-2 py-1 text-[10px]" style={{ borderBottom: `1px solid ${theme.border}`, color: theme.mutedForeground }}>
        <span>{references.length} 条文献</span>
        <span>3 已关联 PDF · 1 待匹配</span>
      </div>

      <div className="p-1.5" style={{ borderBottom: `1px solid ${theme.border}` }}>
        <div
          className="flex items-center gap-1.5 px-1.5 py-1"
          style={{ backgroundColor: theme.input, border: `1px solid ${theme.border}` }}
        >
          <Search className="w-3 h-3" style={{ color: theme.mutedForeground }} strokeWidth={1.5} />
          <input
            type="text"
            placeholder="搜索文献库..."
            className="flex-1 text-[10px] outline-none bg-transparent"
            style={{ color: theme.foreground }}
          />
        </div>
      </div>

      <div className="flex-1 overflow-auto">
        {references.map((ref) => (
          <div key={ref.id} className="px-2 py-1.5 cursor-pointer" style={{ borderBottom: `1px solid ${theme.border}` }}>
            <div className="flex items-start justify-between gap-1.5">
              <div className="flex-1 min-w-0">
                <p className="text-[11px] font-medium truncate leading-4" style={{ color: theme.foreground }}>{ref.title}</p>
                <p className="text-[10px] mt-0.5" style={{ color: theme.mutedForeground }}>{ref.authors}, {ref.year}</p>
                <p className="text-[9px] mt-0.5 font-mono" style={{ color: theme.mutedForeground }}>@{ref.key}</p>
              </div>
              <div className="flex items-center gap-0.5 shrink-0">
                {ref.cited && (
                  <span className="px-1 py-0.5 text-[9px]" style={{ backgroundColor: theme.successSurface, color: theme.success }}>已引用</span>
                )}
                <IconButton icon={<MoreHorizontal className="w-3 h-3" strokeWidth={1.5} />} theme={theme} tight />
              </div>
            </div>
          </div>
        ))}
      </div>

      <PanelAction theme={theme} icon={<Plus className="w-3 h-3" strokeWidth={1.5} />} label="添加文献" />
    </>
  )
}

function SnapshotPanel({ theme }: { theme: AppTheme }) {
  const snapshots = [
    { id: 1, name: "初稿完成", time: "今天 14:32", words: 1523 },
    { id: 2, name: "添加引言", time: "今天 11:20", words: 987 },
    { id: 3, name: "开始写作", time: "昨天 20:15", words: 234 },
  ]

  return (
    <>
      <div className="flex items-center justify-between px-2 py-1.5" style={{ borderBottom: `1px solid ${theme.border}` }}>
        <span className="text-[10px] font-medium uppercase tracking-wide" style={{ color: theme.mutedForeground }}>
          快照时间线
        </span>
      </div>

      <div className="px-2 py-1 text-[10px]" style={{ borderBottom: `1px solid ${theme.border}`, color: theme.mutedForeground }}>
        共 47 个快照 · 占用 12.4 MB · 每 5 分钟保存
      </div>

      <div className="flex-1 overflow-auto">
        {snapshots.map((snapshot, index) => (
          <div key={snapshot.id} className="relative px-2 py-2 cursor-pointer">
            <div
              className="absolute left-[18px] top-0 bottom-0 w-0.5"
              style={{ backgroundColor: index < snapshots.length - 1 ? theme.border : "transparent" }}
            />

            <div className="flex items-start gap-2">
              <div
                className="w-2.5 h-2.5 rounded-full shrink-0 mt-0.5 z-10"
                style={{ backgroundColor: index === 0 ? theme.primaryHover : theme.mutedForeground }}
              />

              <div className="flex-1 min-w-0">
                <div className="flex items-center justify-between">
                  <p className="text-[11px] font-medium" style={{ color: theme.foreground }}>{snapshot.name}</p>
                  <div className="flex items-center gap-0.5">
                    <IconButton icon={<RefreshCw className="w-3 h-3" strokeWidth={1.5} />} theme={theme} tight />
                    <IconButton icon={<Copy className="w-3 h-3" strokeWidth={1.5} />} theme={theme} tight />
                  </div>
                </div>
                <p className="text-[9px] font-mono mt-0.5" style={{ color: theme.mutedForeground }}>
                  {snapshot.time} · {snapshot.words} 字
                </p>
              </div>
            </div>
          </div>
        ))}
      </div>

      <div className="p-2" style={{ borderTop: `1px solid ${theme.border}`, backgroundColor: theme.card }}>
        <div className="text-[10px] mb-1" style={{ color: theme.mutedForeground }}>预览: 14:32 快照</div>
        <div className="p-2 font-mono text-[10px] leading-4" style={{ backgroundColor: theme.input, color: theme.foreground, border: `1px solid ${theme.border}` }}>
          ## 相关工作<br />
          Transformer 相比 RNN/LSTM 可并行建模，并显著降低长距离依赖的训练成本...
        </div>
      </div>

      <PanelAction theme={theme} icon={<Clock className="w-3 h-3" strokeWidth={1.5} />} label="创建快照" />
    </>
  )
}

function PanelAction({ icon, label, theme }: { icon: React.ReactNode; label: string; theme: AppTheme }) {
  return (
    <div className="p-1.5" style={{ borderTop: `1px solid ${theme.border}`, backgroundColor: theme.card }}>
      <button
        className="w-full flex items-center justify-center gap-1 py-1.5 text-[10px]"
        style={{ color: theme.accentForeground, border: `1px dashed ${theme.primary}` }}
      >
        {icon}
        {label}
      </button>
    </div>
  )
}

function ModalShell({
  title,
  width,
  height,
  theme,
  onClose,
  children,
}: {
  title: string
  width: number
  height: number
  theme: AppTheme
  onClose: () => void
  children: React.ReactNode
}) {
  return (
    <div className="absolute inset-0 flex items-center justify-center" style={{ backgroundColor: "rgba(0,0,0,.4)", zIndex: 20 }}>
      <div
        className="flex flex-col shadow-2xl"
        style={{ width, height, backgroundColor: theme.card, color: theme.foreground, border: `1px solid ${theme.border}` }}
      >
        <div className="flex items-center justify-between h-9 px-3 shrink-0" style={{ borderBottom: `1px solid ${theme.border}`, backgroundColor: theme.secondary }}>
          <span className="text-[12px] font-medium">{title}</span>
          <button className="p-1" style={{ color: theme.mutedForeground }} onClick={onClose}>
            <X className="w-4 h-4" strokeWidth={1.5} />
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}

function AppButton({
  label,
  theme,
  variant = "secondary",
  icon,
  onClick,
}: {
  label: string
  theme: AppTheme
  variant?: "primary" | "secondary" | "danger" | "link" | "warning"
  icon?: React.ReactNode
  onClick?: () => void
}) {
  const style =
    variant === "primary"
      ? { backgroundColor: theme.primary, color: theme.primaryForeground, border: `1px solid ${theme.primary}` }
      : variant === "danger"
        ? { backgroundColor: "transparent", color: theme.destructive, border: `1px solid ${theme.destructive}` }
        : variant === "link"
          ? { backgroundColor: "transparent", color: theme.accentForeground, border: "1px solid transparent" }
          : variant === "warning"
            ? { backgroundColor: theme.warningSurface, color: theme.warning, border: `1px solid ${theme.warning}` }
            : { backgroundColor: theme.input, color: theme.foreground, border: `1px solid ${theme.border}` }

  return (
    <button className="h-8 px-3 text-[11px] inline-flex items-center justify-center gap-1" style={style} onClick={onClick}>
      {icon}
      {label}
    </button>
  )
}

function SectionTitle({ index, title, theme }: { index?: number; title: string; theme: AppTheme }) {
  return (
    <div className="flex items-center gap-1.5 text-[12px] font-medium" style={{ color: theme.foreground }}>
      {index && <span style={{ color: theme.mutedForeground }}>{index}.</span>}
      <span>{title}</span>
    </div>
  )
}

function FieldRow({ label, value, theme, action }: { label: string; value: string; theme: AppTheme; action?: string }) {
  return (
    <div className="grid grid-cols-[92px_1fr_auto] items-center gap-2 text-[11px]">
      <span style={{ color: theme.mutedForeground }}>{label}</span>
      <div className="h-7 flex items-center px-2 font-mono truncate" style={{ backgroundColor: theme.input, border: `1px solid ${theme.border}`, color: theme.foreground }}>
        {value}
      </div>
      {action && <AppButton label={action} theme={theme} />}
    </div>
  )
}

function StatusDot({ color }: { color: string }) {
  return <span className="w-2 h-2 rounded-full shrink-0" style={{ backgroundColor: color }} />
}

function ExportDialog({ theme, onClose }: { theme: AppTheme; onClose: () => void }) {
  const templates = [
    ["武汉大学硕士学位论文", "whu-thesis v2.1", "适用: 武汉大学理工科硕士论文", "success"],
    ["IEEE 会议论文", "ieee-conf v1.3", "适用: IEEE Conference Proceedings", "success"],
    ["自定义模板", "my-custom v0.2", "Schema 校验失败: 缺少 referenceStyle", "error"],
  ] as const

  return (
    <ModalShell title="导出文档" width={540} height={620} theme={theme} onClose={onClose}>
      <div className="flex-1 overflow-hidden p-4 space-y-3">
        <SectionTitle index={1} title="选择排版模板" theme={theme} />
        <div style={{ border: `1px solid ${theme.border}` }}>
          {templates.map((item, index) => (
            <div
              key={item[1]}
              className="grid grid-cols-[18px_1fr_auto] gap-2 p-2 text-[11px]"
              style={{ borderBottom: index < templates.length - 1 ? `1px solid ${theme.border}` : "none", backgroundColor: index === 0 ? theme.accent : theme.card, opacity: item[3] === "error" ? 0.62 : 1 }}
            >
              <span className="mt-0.5">{index === 0 ? "●" : "○"}</span>
              <div>
                <div className="font-medium">{item[0]} <span style={{ color: theme.mutedForeground }}>({item[1]})</span></div>
                <div className="mt-0.5" style={{ color: theme.mutedForeground }}>{item[2]}</div>
              </div>
              <div className="flex items-center gap-1" style={{ color: item[3] === "success" ? theme.success : theme.destructive }}>
                {item[3] === "success" ? <Check className="w-3 h-3" /> : <XCircle className="w-3 h-3" />}
                <span>{item[3] === "success" ? "可导出" : "失败"}</span>
              </div>
            </div>
          ))}
        </div>
        <div className="flex items-center justify-between text-[10px]" style={{ color: theme.mutedForeground }}>
          <span>模板存储路径: ~/WeaveDoc/templates/</span>
          <button style={{ color: theme.accentForeground }}>管理模板</button>
        </div>

        <SectionTitle index={2} title="导出配置" theme={theme} />
        <div className="grid grid-cols-[92px_1fr] gap-2 text-[11px]">
          <span style={{ color: theme.mutedForeground }}>输出格式</span>
          <div className="flex gap-4">
            <span>● Word (.docx)</span>
            <span style={{ color: theme.mutedForeground }}>○ PDF</span>
          </div>
        </div>
        <FieldRow label="输出文件" value="~/Papers/thesis.docx" action="浏览..." theme={theme} />

        <SectionTitle index={3} title="引文校验" theme={theme} />
        <div className="p-2 text-[11px]" style={{ backgroundColor: theme.warningSurface, border: `1px solid ${theme.warning}`, color: theme.foreground }}>
          <div className="flex items-center gap-1 font-medium" style={{ color: theme.warning }}>
            <AlertTriangle className="w-3.5 h-3.5" strokeWidth={1.5} />
            2 条引用无法匹配
          </div>
          <div className="mt-1 font-mono" style={{ color: theme.mutedForeground }}>@zhang2024unknown · Line 88<br />@li2023missing · Line 124</div>
          <div className="mt-2 flex gap-2">
            <AppButton label="仍要继续导出" variant="warning" theme={theme} />
            <AppButton label="返回修改" theme={theme} />
          </div>
        </div>

        <SectionTitle index={4} title="进度" theme={theme} />
        <div className="p-2 text-[11px]" style={{ backgroundColor: theme.input, border: `1px solid ${theme.border}` }}>
          {["解析模板", "生成 IR", "重排引文", "生成 Word 文档... 65%"].map((step, index) => (
            <div key={step} className="flex items-center gap-2 h-5">
              {index < 3 ? <CheckCircle2 className="w-3 h-3" style={{ color: theme.success }} /> : <Clock className="w-3 h-3" style={{ color: theme.warning }} />}
              <span>{step}</span>
            </div>
          ))}
          <div className="h-1.5 mt-2" style={{ backgroundColor: theme.secondary }}>
            <div className="h-full" style={{ width: "65%", backgroundColor: theme.primary }} />
          </div>
        </div>
      </div>
      <div className="h-11 px-4 flex justify-end items-center gap-2 shrink-0" style={{ borderTop: `1px solid ${theme.border}` }}>
        <AppButton label="取消" theme={theme} onClick={onClose} />
        <AppButton label="导出" theme={theme} variant="primary" />
      </div>
    </ModalShell>
  )
}

function SettingsDialog({ theme, onClose }: { theme: AppTheme; onClose: () => void }) {
  const [tab, setTab] = useState("模型管理")
  const tabs = ["通用", "模型管理", "Zotero", "模板库", "快照策略"]

  return (
    <ModalShell title="设置" width={640} height={520} theme={theme} onClose={onClose}>
      <div className="h-9 px-3 flex items-center gap-1 shrink-0" style={{ borderBottom: `1px solid ${theme.border}`, backgroundColor: theme.secondary }}>
        {tabs.map((item) => (
          <button
            key={item}
            className="h-8 px-2 text-[11px]"
            style={{ color: tab === item ? theme.foreground : theme.mutedForeground, borderBottom: tab === item ? `2px solid ${theme.primary}` : "2px solid transparent" }}
            onClick={() => setTab(item)}
          >
            {item}
          </button>
        ))}
      </div>
      <div className="flex-1 p-4 overflow-hidden">
        {tab === "通用" && (
          <div className="space-y-3">
            <FieldRow label="语言" value="中文简体" theme={theme} />
            <FieldRow label="默认工作区" value="/home/user/WeaveDoc" action="浏览" theme={theme} />
            <FieldRow label="编辑器字体" value="JetBrains Mono" theme={theme} />
            <FieldRow label="编辑器字号" value="14 px" theme={theme} />
            <SettingToggle label="启动时恢复上次工作区" checked theme={theme} />
          </div>
        )}
        {tab === "模型管理" && <ModelSettings theme={theme} />}
        {tab === "Zotero" && (
          <div className="space-y-3">
            <FieldRow label=".bib 文件" value="~/Zotero/library.bib" action="浏览" theme={theme} />
            <InfoLine icon={<CheckCircle2 className="w-3.5 h-3.5" />} title="已连接 · 最后同步: 2026-05-20 16:30" tone="success" theme={theme} />
            <SettingToggle label="自动监听 .bib 文件变更" checked theme={theme} />
            <div className="p-2 font-mono text-[10px] leading-4" style={{ backgroundColor: theme.input, border: `1px solid ${theme.border}`, color: theme.mutedForeground }}>
              16:30 增量同步完成 · 34 条文献<br />16:12 更新 citation index<br />15:58 检测到 Better BibTeX 导出
            </div>
            <AppButton label="立即同步" theme={theme} variant="primary" />
          </div>
        )}
        {tab === "模板库" && <TemplateSettings theme={theme} />}
        {tab === "快照策略" && (
          <div className="space-y-3">
            <SettingToggle label="启用自动快照" checked theme={theme} />
            <FieldRow label="历史间隔" value="5 分钟" theme={theme} />
            <FieldRow label="最大数量" value="200" theme={theme} />
            <FieldRow label="存储路径" value="~/WeaveDoc/snapshots/" action="浏览" theme={theme} />
            <InfoLine icon={<HardDrive className="w-3.5 h-3.5" />} title="快照占用: 12.4 MB · 47 个快照" theme={theme} />
            <AppButton label="删除所有快照" theme={theme} variant="danger" />
          </div>
        )}
      </div>
      <div className="h-10 px-4 flex justify-end items-center gap-2 shrink-0" style={{ borderTop: `1px solid ${theme.border}` }}>
        <AppButton label="恢复默认" theme={theme} />
        <AppButton label="应用" theme={theme} variant="primary" />
      </div>
    </ModalShell>
  )
}

function ModelSettings({ theme }: { theme: AppTheme }) {
  const models = [
    ["Phi-4 7B Q4", "已加载", "4.2 GB", "3.2 t/s", "success"],
    ["Qwen2.5 3B", "未加载", "1.8 GB", "-", "muted"],
    ["Phi-4 1.5B", "未加载", "0.9 GB", "-", "muted"],
  ] as const
  return (
    <div className="space-y-3">
      <div className="text-[11px] font-medium">LLM 模型</div>
      <div style={{ border: `1px solid ${theme.border}` }}>
        {models.map((model, index) => (
          <div key={model[0]} className="grid grid-cols-[1fr_auto_auto_auto] gap-2 px-2 py-2 text-[11px]" style={{ borderBottom: index < models.length - 1 ? `1px solid ${theme.border}` : "none" }}>
            <span>{index === 0 ? "●" : "○"} {model[0]}</span>
            <span style={{ color: model[4] === "success" ? theme.success : theme.mutedForeground }}>{model[1]}</span>
            <span style={{ color: theme.mutedForeground }}>{model[2]}</span>
            <span style={{ color: theme.mutedForeground }}>{model[3]}</span>
          </div>
        ))}
      </div>
      <div className="flex gap-2">
        <AppButton label="加载" theme={theme} variant="primary" />
        <AppButton label="卸载" theme={theme} />
        <AppButton label="删除" theme={theme} variant="danger" />
        <AppButton label="导入模型" icon={<Upload className="w-3 h-3" />} theme={theme} />
      </div>
      <FieldRow label="Embedding" value="bge-small-en · 已加载" theme={theme} />
      <FieldRow label="OCR 模型" value="pix2tex · 已加载" theme={theme} />
      <FieldRow label="模型路径" value="/home/user/WeaveDoc/models/" theme={theme} />
      <SettingToggle label="AI 面板自动加载" theme={theme} />
      <InfoLine icon={<HardDrive className="w-3.5 h-3.5" />} title="当前 RAM 72% · 模型权重 4.2 GB · 阈值 80%" tone="warning" theme={theme} />
    </div>
  )
}

function TemplateSettings({ theme }: { theme: AppTheme }) {
  return (
    <div className="space-y-3">
      {["武汉大学硕士学位论文 · 默认 · Schema 通过", "IEEE 会议论文 · 启用 · Schema 通过", "自定义模板 · 禁用 · 缺少 referenceStyle"].map((item, index) => (
        <div key={item} className="flex items-center justify-between px-2 py-2 text-[11px]" style={{ border: `1px solid ${index === 2 ? theme.destructive : theme.border}` }}>
          <span>{index === 0 ? "●" : "○"} {item}</span>
          <span style={{ color: index === 2 ? theme.destructive : theme.success }}>{index === 2 ? "失败" : "启用"}</span>
        </div>
      ))}
      <div className="flex gap-2">
        <AppButton label="导入模板" theme={theme} variant="primary" />
        <AppButton label="打开文件夹" theme={theme} />
      </div>
      <FieldRow label="模板目录" value="~/WeaveDoc/templates/" theme={theme} />
    </div>
  )
}

function SettingToggle({ label, checked, theme }: { label: string; checked?: boolean; theme: AppTheme }) {
  return (
    <div className="flex items-center justify-between h-8 text-[11px]">
      <span>{label}</span>
      <span className="w-9 h-5 flex items-center px-0.5" style={{ backgroundColor: checked ? theme.primary : theme.input, border: `1px solid ${theme.border}` }}>
        <span className="w-4 h-4" style={{ backgroundColor: checked ? theme.primaryForeground : theme.mutedForeground, marginLeft: checked ? 14 : 0 }} />
      </span>
    </div>
  )
}

function InfoLine({ icon, title, theme, tone }: { icon: React.ReactNode; title: string; theme: AppTheme; tone?: "success" | "warning" }) {
  const color = tone === "success" ? theme.success : tone === "warning" ? theme.warning : theme.mutedForeground
  return (
    <div className="flex items-center gap-2 px-2 py-2 text-[11px]" style={{ backgroundColor: theme.input, border: `1px solid ${theme.border}`, color }}>
      {icon}
      <span>{title}</span>
    </div>
  )
}

function SetupWizardDialog({ theme, onClose }: { theme: AppTheme; onClose: () => void }) {
  const steps = ["环境检测", "模型配置", "Zotero", "模板", "完成"]
  return (
    <ModalShell title="Welcome to WeaveDoc" width={600} height={500} theme={theme} onClose={onClose}>
      <div className="h-[72px] px-6 flex items-center justify-between shrink-0" style={{ borderBottom: `1px solid ${theme.border}` }}>
        {steps.map((step, index) => (
          <div key={step} className="flex flex-col items-center gap-1 text-[10px]" style={{ color: index <= 1 ? theme.foreground : theme.mutedForeground }}>
            <span className="w-4 h-4 rounded-full flex items-center justify-center text-[9px]" style={{ backgroundColor: index < 1 ? theme.success : index === 1 ? theme.primary : theme.input, color: index <= 1 ? theme.primaryForeground : theme.mutedForeground }}>
              {index < 1 ? "✓" : index + 1}
            </span>
            {step}
          </div>
        ))}
      </div>
      <div className="flex-1 p-6">
        <div className="h-full p-4" style={{ border: `1px solid ${theme.border}`, backgroundColor: theme.panel }}>
          <div className="text-[13px] font-medium mb-2">模型配置</div>
          <p className="text-[11px] mb-3" style={{ color: theme.mutedForeground }}>
            根据您的硬件配置 (14.2 GB 可用内存)，推荐 Phi-4 7B Q4。所有联网下载都需要用户显式确认。
          </p>
          <div className="space-y-2 text-[11px]">
            <OptionRow selected label="从本地导入模型文件" detail="选择 .gguf 文件并复制到模型目录" theme={theme} />
            <OptionRow label="下载推荐模型" detail="Phi-4 7B Q4 · 4.2 GB" theme={theme} />
            <OptionRow label="跳过，稍后配置" detail="AI 问答暂不可用，写作和导出仍可继续" theme={theme} />
          </div>
          <div className="mt-4 grid grid-cols-2 gap-2">
            <FieldRow label="Embedding" value="跳过" theme={theme} />
            <FieldRow label="OCR 模型" value="pix2tex · 已检测" theme={theme} />
          </div>
        </div>
      </div>
      <div className="h-11 px-4 flex justify-end items-center gap-2 shrink-0" style={{ borderTop: `1px solid ${theme.border}` }}>
        <AppButton label="上一步" theme={theme} />
        <AppButton label="跳过" theme={theme} variant="link" />
        <AppButton label="下一步" theme={theme} variant="primary" />
      </div>
    </ModalShell>
  )
}

function OptionRow({ label, detail, selected, theme }: { label: string; detail: string; selected?: boolean; theme: AppTheme }) {
  return (
    <div className="grid grid-cols-[18px_1fr] gap-2 p-2" style={{ backgroundColor: selected ? theme.accent : theme.card, border: `1px solid ${theme.border}` }}>
      <span>{selected ? "●" : "○"}</span>
      <div>
        <div>{label}</div>
        <div className="text-[10px]" style={{ color: theme.mutedForeground }}>{detail}</div>
      </div>
    </div>
  )
}

function OcrFloatingToolbar({ theme, onClose }: { theme: AppTheme; onClose: () => void }) {
  return (
    <div className="absolute left-[138px] top-[360px] w-[280px] shadow-xl" style={{ zIndex: 5, backgroundColor: theme.popover, border: `1px solid ${theme.border}`, color: theme.foreground }}>
      <div className="flex items-center justify-between px-2 h-8" style={{ borderBottom: `1px solid ${theme.border}` }}>
        <span className="flex items-center gap-1 text-[11px] font-medium"><Microscope className="w-3.5 h-3.5" />公式识别</span>
        <button onClick={onClose}><X className="w-3.5 h-3.5" /></button>
      </div>
      <div className="p-2">
        <div className="p-2 font-mono text-[11px] leading-4" style={{ backgroundColor: theme.editor.background, color: theme.editor.foreground, border: `1px solid ${theme.editor.border}` }}>
          \frac{"{\\partial^2 u}"}{"{\\partial x^2}"} = \frac{"{\\partial u}"}{"{\\partial t}"}
        </div>
        <div className="mt-2 text-[10px]" style={{ color: theme.mutedForeground }}>置信度: 92% · OCR 模型 pix2tex 已加载</div>
        <div className="mt-2 flex gap-2 justify-end">
          <AppButton label="重新框选" theme={theme} />
          <AppButton label="复制 LaTeX" theme={theme} variant="primary" icon={<Copy className="w-3 h-3" />} onClick={onClose} />
        </div>
      </div>
    </div>
  )
}

function AboutDialog({ theme, onClose }: { theme: AppTheme; onClose: () => void }) {
  const shortcuts = [
    ["Ctrl+S", "保存文档"],
    ["Ctrl+O", "打开工作区"],
    ["Ctrl+Shift+E", "导出文档"],
    ["Ctrl+Shift+A", "AI 问答面板"],
    ["Ctrl+Shift+L", "文献列表面板"],
    ["Ctrl+Shift+T", "快照时间轴面板"],
    ["Ctrl+,", "设置"],
    ["F1", "帮助"],
  ]
  return (
    <ModalShell title="关于 WeaveDoc" width={400} height={520} theme={theme} onClose={onClose}>
      <div className="flex-1 overflow-hidden p-4">
        <div className="text-center pb-4" style={{ borderBottom: `1px solid ${theme.border}` }}>
          <div className="mx-auto mb-2 w-12 h-12 grid grid-cols-2 gap-1 p-2" style={{ backgroundColor: theme.accent }}>
            {[palette.blue600, palette.blue300, palette.blue400, palette.blue500].map((color) => <span key={color} style={{ backgroundColor: color }} />)}
          </div>
          <div className="text-[18px] font-semibold">WeaveDoc</div>
          <div className="text-[11px]" style={{ color: theme.mutedForeground }}>v0.1.0 (MVP) · 本地优先智能学术工作台</div>
        </div>
        <div className="py-3" style={{ borderBottom: `1px solid ${theme.border}` }}>
          <div className="flex items-center gap-1 text-[12px] font-medium mb-2"><Database className="w-3.5 h-3.5" />技术栈</div>
          {["运行时: .NET 10.0", "UI: Avalonia UI 11.x", "AI 推理: LLamaSharp", "文档转换: Pandoc 3.x", "文献标准: GB/T 7714-2015"].map((item) => (
            <div key={item} className="text-[11px] leading-5" style={{ color: theme.mutedForeground }}>· {item}</div>
          ))}
        </div>
        <div className="py-3">
          <div className="flex items-center gap-1 text-[12px] font-medium mb-2"><Keyboard className="w-3.5 h-3.5" />快捷键速查表</div>
          <div className="grid grid-cols-[110px_1fr] gap-y-1 text-[11px]">
            {shortcuts.map(([key, value]) => (
              <div key={key} className="contents">
                <span className="font-mono" style={{ color: theme.accentForeground }}>{key}</span>
                <span>{value}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
      <div className="h-16 px-4 shrink-0" style={{ borderTop: `1px solid ${theme.border}` }}>
        <div className="h-8 flex justify-between items-center">
          <AppButton label="导出审计日志" theme={theme} variant="link" />
          <AppButton label="检查更新" theme={theme} variant="link" />
          <AppButton label="许可协议" theme={theme} variant="link" />
        </div>
        <div className="flex justify-end">
          <AppButton label="关闭" theme={theme} onClick={onClose} />
        </div>
      </div>
    </ModalShell>
  )
}
