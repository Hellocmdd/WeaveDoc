import numpy as np

# Try to use sentence-transformers if available; otherwise fall back to transformers + mean pooling
MODEL_NAME = "all-MiniLM-L6-v2"

def _use_sentence_transformers():
    try:
        from sentence_transformers import SentenceTransformer  # type: ignore
        return SentenceTransformer
    except Exception:
        return None

def _use_transformers():
    try:
        from transformers import AutoModel, AutoTokenizer
        import torch
        return AutoModel, AutoTokenizer, torch
    except Exception:
        return None, None, None

def get_embedder(model_name: str = MODEL_NAME):
    ST = _use_sentence_transformers()
    if ST is not None:
        return ST(model_name)
    # fallback: return transformers-based tuple (model, tokenizer, torch)
    AutoModel, AutoTokenizer, torch = _use_transformers()
    if AutoModel is None:
        raise RuntimeError("No embedding backend available: install sentence-transformers or transformers")
    tokenizer = AutoTokenizer.from_pretrained(model_name)
    model = AutoModel.from_pretrained(model_name)
    model.eval()
    return (model, tokenizer, torch)

def embed_texts(texts, model=None):
    if model is None:
        model = get_embedder()

    # sentence-transformers case
    from types import SimpleNamespace
    if hasattr(model, "encode"):
        embs = model.encode(texts, show_progress_bar=False, convert_to_numpy=True)
        return np.array(embs, dtype="float32")

    # transformers fallback: model is (pt_model, tokenizer, torch)
    pt_model, tokenizer, torch = model
    inputs = tokenizer(texts, padding=True, truncation=True, return_tensors="pt")
    with torch.no_grad():
        outputs = pt_model(**inputs)
        if hasattr(outputs, "last_hidden_state"):
            hidden = outputs.last_hidden_state  # (batch, seq, dim)
        else:
            # try first element
            hidden = outputs[0]
        # mean pooling (attention mask aware)
        if "attention_mask" in inputs:
            mask = inputs["attention_mask"].unsqueeze(-1)
            masked = hidden * mask
            summed = masked.sum(1)
            counts = mask.sum(1).clamp(min=1)
            pooled = summed / counts
        else:
            pooled = hidden.mean(dim=1)
    return pooled.cpu().numpy().astype("float32")
