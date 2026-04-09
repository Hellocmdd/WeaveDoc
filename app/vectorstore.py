import faiss
import numpy as np
import os
from typing import List

INDEX_FILE = "faiss_index.idx"
EMB_DIMS = 384

class FaissStore:
    def __init__(self, dim: int = EMB_DIMS, index_file: str = INDEX_FILE):
        self.dim = dim
        self.index_file = index_file
        if os.path.exists(self.index_file):
            self.index = faiss.read_index(self.index_file)
        else:
            self.index = self._new_index()

    def add(self, embeddings: np.ndarray, ids: List[int]):
        if embeddings is None or len(embeddings) == 0 or len(ids) == 0:
            return
        if embeddings.dtype != np.float32:
            embeddings = embeddings.astype('float32')
        self.index.add_with_ids(embeddings, np.array(ids, dtype='int64'))
        self._save()

    def search(self, query_emb: np.ndarray, k: int = 5):
        if query_emb.dtype != np.float32:
            query_emb = query_emb.astype('float32')
        D, I = self.index.search(query_emb.reshape(1, -1), k)
        return D[0].tolist(), I[0].tolist()

    def _save(self):
        faiss.write_index(self.index, self.index_file)

    def reset(self):
        self.index = self._new_index()
        self._save()

    def _new_index(self):
        base = faiss.IndexFlatL2(self.dim)
        return faiss.IndexIDMap(base)
