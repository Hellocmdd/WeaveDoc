import sqlite3
from typing import Optional, List, Dict, Any
import json

DB_PATH = "rag_store.db"

def init_db(path: str = DB_PATH):
    conn = sqlite3.connect(path)
    c = conn.cursor()
    c.execute(
        """
        CREATE TABLE IF NOT EXISTS documents (
            id INTEGER PRIMARY KEY,
            content TEXT NOT NULL,
            metadata TEXT
        )
        """
    )
    conn.commit()
    conn.close()

def insert_document(content: str, metadata: Optional[dict] = None, path: str = DB_PATH) -> int:
    conn = sqlite3.connect(path)
    c = conn.cursor()
    meta_text = json.dumps(metadata) if metadata is not None else None
    c.execute("INSERT INTO documents (content, metadata) VALUES (?,?)", (content, meta_text))
    doc_id = c.lastrowid
    conn.commit()
    conn.close()
    return doc_id

def list_documents(path: str = DB_PATH) -> List[Dict[str, Any]]:
    conn = sqlite3.connect(path)
    c = conn.cursor()
    c.execute("SELECT id, content, metadata FROM documents ORDER BY id DESC")
    rows = c.fetchall()
    conn.close()
    out = []
    for r in rows:
        meta = None
        if r[2]:
            try:
                meta = json.loads(r[2])
            except Exception:
                meta = r[2]
        out.append({"id": r[0], "content": r[1], "metadata": meta})
    return out

def get_document_by_id(doc_id: int, path: str = DB_PATH) -> Optional[Dict[str, Any]]:
    conn = sqlite3.connect(path)
    c = conn.cursor()
    c.execute("SELECT id, content, metadata FROM documents WHERE id=?", (doc_id,))
    row = c.fetchone()
    conn.close()
    if not row:
        return None
    meta = None
    if row[2]:
        try:
            meta = json.loads(row[2])
        except Exception:
            meta = row[2]
    return {"id": row[0], "content": row[1], "metadata": meta}


def delete_document(doc_id: int, path: str = DB_PATH) -> bool:
    conn = sqlite3.connect(path)
    c = conn.cursor()
    c.execute("DELETE FROM documents WHERE id=?", (doc_id,))
    affected = c.rowcount
    conn.commit()
    conn.close()
    return affected > 0


def clear_documents(path: str = DB_PATH) -> int:
    conn = sqlite3.connect(path)
    c = conn.cursor()
    c.execute("DELETE FROM documents")
    affected = c.rowcount
    conn.commit()
    conn.close()
    return affected if affected is not None else 0