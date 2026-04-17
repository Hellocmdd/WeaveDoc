# WeaveDoc

WeaveDoc is a local RAG desktop application built around an Avalonia front end, local embedding + retrieval, and `llama.cpp` for chat generation.

## Repository Layout

- `csharp-rag-avalonia/`: Avalonia desktop app, RAG pipeline, prompt assembly, and local document management
- `llama.cpp/`: upstream `ggml-org/llama.cpp` linked as a Git submodule
- `scripts/`: build, run, debug, and update helper scripts
- `docs/`: project documentation such as the RAG architecture summary
- `doc/`: local source documents indexed by the app
- `models/`: local GGUF model files used by the app and `llama-server`

## First Clone

Clone the repository together with submodules:

```bash
git clone --recurse-submodules https://github.com/Hellocmdd/WeaveDoc.git
cd WeaveDoc
```

If you already cloned the repository without submodules:

```bash
git submodule update --init --recursive
```

## One-Click Update

The repository includes `llama.cpp` as a submodule and provides a helper script to refresh both the main repo and the submodule, then rebuild the local runtime pieces:

```bash
./scripts/update_all.sh
```

By default, the script:

- pulls the current branch of this repository
- syncs and initializes submodules
- updates `llama.cpp` to the latest commit on the tracked branch from `.gitmodules`
- rebuilds `llama.cpp`
- rebuilds the Avalonia app

If you only want the submodule version pinned by the current main repo commit, use:

```bash
./scripts/update_all.sh --pinned
```

## Run

After models are prepared in `models/`, start the full local stack with:

```bash
./scripts/run_weavedoc.sh
```

This launcher will build and start `llama-server` when needed, then launch the Avalonia desktop app.

## Documentation

- RAG architecture summary: [docs/rag-architecture.md](docs/rag-architecture.md)
- App-specific usage notes: [csharp-rag-avalonia/README.md](csharp-rag-avalonia/README.md)
