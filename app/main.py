from fastapi import FastAPI, Request, Form, UploadFile, File
from fastapi.responses import HTMLResponse, RedirectResponse
from fastapi.templating import Jinja2Templates
import uvicorn
from .db import (
    init_db,
    insert_document,
    list_documents,
    get_document_by_id,
    delete_document,
    clear_documents,
)
from .embeddings import get_embedder, embed_texts
from .vectorstore import FaissStore
import os
from typing import List, Optional
import re
from pathlib import Path
import importlib
import importlib.util

HAS_PDF = importlib.util.find_spec("pypdf") is not None
HAS_DOCX = importlib.util.find_spec("docx") is not None

# optional model imports (will be lazy-loaded)
try:
    from transformers import AutoTokenizer, AutoModelForCausalLM
    MODEL_BACKEND = True
except Exception:
    MODEL_BACKEND = False

app = FastAPI()
templates = Jinja2Templates(directory=os.path.join(os.path.dirname(__file__), "templates"))

# Initialize persistence and services
init_db()
embedder = get_embedder()
store = FaissStore(dim=embedder.get_sentence_embedding_dimension())

# Generator model lazy placeholders
GEN_MODEL_ID = "google/gemma-4-E2B-it"
gen_tokenizer = None
gen_model = None


def _is_repetitive_text(text: str) -> bool:
    lines = [ln.strip() for ln in text.splitlines() if ln.strip()]
    if len(lines) < 4:
        return False
    uniq_ratio = len(set(lines)) / len(lines)
    if uniq_ratio < 0.35:
        return True
    words = text.split()
    if len(words) >= 20 and len(set(words)) / len(words) < 0.35:
        return True
    # Detect very long immediate pattern repetition
    if re.search(r"(.{6,40})\1{4,}", text.replace("\n", "")):
        return True
    return False


def _build_prompt(q: str, context_parts: List[dict]) -> str:
    prompt = (
        "你是一个基于检索上下文回答问题的助手。"
        "请严格遵守：\n"
        "1) 只能依据给定 Context 回答，不要编造事实。\n"
        "2) 如果信息不足，明确回答“我不知道”并说明缺失点。\n"
        "3) 优先用中文，表达清晰、简洁。\n"
        "4) 先给结论，再给 2-4 条依据。\n"
        "5) 每条依据末尾必须标注来源编号，格式如 [#123]。\n\n"
        "Context:\n"
    )
    prompt += "\n---\n".join([f"[# {c['id']}]\n{c['content']}".replace("# ", "#") for c in context_parts])
    prompt += f"\n\nQuestion: {q}\nAnswer:"
    return prompt


def _generate_answer(tok, mdl, q: str, context_parts: List[dict]) -> str:
    prompt = _build_prompt(q, context_parts)

    # Prefer chat template when tokenizer provides one (for instruction/chat models like Gemma-IT).
    if hasattr(tok, "apply_chat_template") and getattr(tok, "chat_template", None):
        messages = [
            {"role": "system", "content": "你是严谨的 RAG 助手，只能基于上下文回答。"},
            {"role": "user", "content": prompt},
        ]
        try:
            chat_inputs = tok.apply_chat_template(
                messages,
                return_tensors="pt",
                add_generation_prompt=True,
                return_dict=True,
            )
        except TypeError:
            chat_inputs = tok.apply_chat_template(
                messages,
                return_tensors="pt",
                add_generation_prompt=True,
            )

        # Compatibility: some versions return Tensor, some return BatchEncoding/dict.
        if hasattr(chat_inputs, "items"):
            inputs = {k: v.to(mdl.device) for k, v in chat_inputs.items() if hasattr(v, "to")}
        else:
            inputs = {"input_ids": chat_inputs.to(mdl.device)}
    else:
        inputs = tok(prompt, return_tensors="pt")
        inputs = {k: v.to(mdl.device) for k, v in inputs.items()}

    if "input_ids" not in inputs:
        raise RuntimeError("tokenizer did not produce input_ids")

    input_len = inputs["input_ids"].shape[-1]
    eos_id = tok.eos_token_id
    pad_id = tok.pad_token_id if tok.pad_token_id is not None else eos_id

    out = mdl.generate(
        **inputs,
        max_new_tokens=220,
        do_sample=True,
        temperature=0.25,
        top_p=0.85,
        repetition_penalty=1.12,
        no_repeat_ngram_size=4,
        eos_token_id=eos_id,
        pad_token_id=pad_id,
    )

    gen_tokens = out[0][input_len:]
    answer = tok.decode(gen_tokens, skip_special_tokens=True).strip()
    if not answer:
        return "我不知道。当前上下文不足以得出可靠结论。"
    if _is_repetitive_text(answer):
        return "我不知道。当前模型输出出现重复退化，请换一种提问方式，或补充更聚焦的文档片段。"
    return answer

def load_generator_model():
    global gen_tokenizer, gen_model
    if gen_model is not None and gen_tokenizer is not None:
        return gen_tokenizer, gen_model
    if not MODEL_BACKEND:
        return None, None
    try:
        gen_tokenizer = AutoTokenizer.from_pretrained(GEN_MODEL_ID)
        gen_model = AutoModelForCausalLM.from_pretrained(GEN_MODEL_ID, dtype="auto", device_map="auto")
        return gen_tokenizer, gen_model
    except Exception:
        gen_model = None
        gen_tokenizer = None
        return None, None


def chunk_text(text: str, chunk_size: int = 1000, overlap: int = 200) -> List[str]:
    if len(text) <= chunk_size:
        return [text]
    chunks = []
    start = 0
    L = len(text)
    while start < L:
        end = min(start + chunk_size, L)
        chunks.append(text[start:end])
        start = max(end - overlap, end)
    return chunks


def _extract_upload_text(filename: str, data: bytes) -> str:
    suffix = Path(filename or "").suffix.lower()

    if suffix in {".txt", ".md", ".log", ".csv", ".json"}:
        try:
            return data.decode("utf-8")
        except Exception:
            return data.decode("latin-1", errors="ignore")

    if suffix == ".pdf":
        if not HAS_PDF:
            raise ValueError("缺少 PDF 解析依赖，请安装 pypdf。")
        try:
            from io import BytesIO

            PdfReader = importlib.import_module("pypdf").PdfReader
            reader = PdfReader(BytesIO(data))
            pages = []
            for p in reader.pages:
                pages.append((p.extract_text() or "").strip())
            text = "\n\n".join([p for p in pages if p])
            if not text.strip():
                raise ValueError("PDF 未提取到可用文本，可能是扫描件或图片型 PDF。")
            return text
        except ValueError:
            raise
        except Exception as e:
            raise ValueError(f"PDF 解析失败: {e}")

    if suffix == ".docx":
        if not HAS_DOCX:
            raise ValueError("缺少 Word 解析依赖，请安装 python-docx。")
        try:
            from io import BytesIO

            Document = importlib.import_module("docx").Document
            doc = Document(BytesIO(data))
            paras = [(p.text or "").strip() for p in doc.paragraphs]
            text = "\n".join([p for p in paras if p])
            if not text.strip():
                raise ValueError("Word 文档为空或未提取到可用文本。")
            return text
        except ValueError:
            raise
        except Exception as e:
            raise ValueError(f"Word 解析失败: {e}")

    if suffix == ".doc":
        raise ValueError("暂不支持 .doc（老版 Word），请先转换为 .docx 后再上传。")

    raise ValueError("不支持的文件类型，请上传 txt/md/log/pdf/docx。")


def rebuild_index_from_db():
    docs = list_documents()
    store.reset()
    if not docs:
        return 0
    ids = [int(d["id"]) for d in docs]
    texts = [d.get("content") or "" for d in docs]
    embs = embed_texts(texts, model=embedder)
    store.add(embs, ids)
    return len(ids)


@app.get("/", response_class=HTMLResponse)
async def index(request: Request, msg: Optional[str] = None):
    docs = list_documents()
    return templates.TemplateResponse(
        "index.html",
        {"request": request, "docs": docs, "results": None, "answer": None, "msg": msg, "query": None},
    )


@app.post("/add")
async def add_doc(content: str = Form(...)):
    content = content.strip()
    if not content:
        return RedirectResponse(url="/?msg=内容为空，未添加文档。", status_code=303)

    # chunk and store each chunk as a separate document
    chunks = chunk_text(content)
    ids = []
    for i, c in enumerate(chunks):
        meta = {"source": "paste", "chunk_index": i}
        doc_id = insert_document(c, metadata=meta)
        ids.append(doc_id)
    embs = embed_texts(chunks, model=embedder)
    store.add(embs, ids)
    return RedirectResponse(url=f"/?msg=添加成功：新增 {len(chunks)} 个分块。", status_code=303)


@app.post("/upload")
async def upload_file(file: UploadFile = File(...)):
    data = await file.read()
    try:
        text = _extract_upload_text(file.filename or "", data)
    except ValueError as e:
        return RedirectResponse(url=f"/?msg=上传失败：{str(e)}", status_code=303)

    if not text.strip():
        return RedirectResponse(url="/?msg=上传失败：文件内容为空。", status_code=303)

    chunks = chunk_text(text)
    ids = []
    for i, c in enumerate(chunks):
        meta = {"source": file.filename, "chunk_index": i}
        doc_id = insert_document(c, metadata=meta)
        ids.append(doc_id)
    embs = embed_texts(chunks, model=embedder)
    store.add(embs, ids)
    return RedirectResponse(
        url=f"/?msg=上传成功：{file.filename} 已入库，共 {len(chunks)} 个分块。", status_code=303
    )


@app.post("/delete/{doc_id}")
async def delete_doc(doc_id: int):
    ok = delete_document(doc_id)
    if not ok:
        return RedirectResponse(url=f"/?msg=删除失败：文档 #{doc_id} 不存在。", status_code=303)
    rebuild_index_from_db()
    return RedirectResponse(url=f"/?msg=已删除文档 #{doc_id}，索引已重建。", status_code=303)


@app.post("/clear")
async def clear_all_docs():
    removed = clear_documents()
    store.reset()
    return RedirectResponse(url=f"/?msg=知识库已清空，删除 {removed} 条记录。", status_code=303)


@app.post("/query", response_class=HTMLResponse)
async def query(request: Request, q: str = Form(...)):
    q_emb = embed_texts([q], model=embedder)[0]
    D, I = store.search(q_emb, k=5)
    docs = list_documents()
    id_to_doc = {d["id"]: d for d in docs}
    results = []
    context_parts = []
    for score, doc_id in zip(D, I):
        if doc_id == -1:
            continue
        did = int(doc_id)
        doc = id_to_doc.get(did)
        if not doc:
            continue
        results.append({"id": did, "score": float(score), "content": doc.get("content"), "metadata": doc.get("metadata")})
        context_parts.append({"id": did, "content": doc.get("content")})

    answer = None
    tok, mdl = load_generator_model()
    if tok is not None and mdl is not None and len(context_parts) > 0:
        try:
            answer = _generate_answer(tok, mdl, q, context_parts)
        except Exception:
            answer = "生成模型暂时不可用，请稍后重试；已返回召回片段供参考。"
    elif len(results) > 0:
        answer = "未加载生成模型，已返回最相关片段，请结合片段编号自行判断。"

    return templates.TemplateResponse(
        "index.html",
        {"request": request, "docs": docs, "results": results, "answer": answer, "query": q, "msg": None},
    )


if __name__ == "__main__":
    uvicorn.run("app.main:app", host="127.0.0.1", port=8000, reload=False)
