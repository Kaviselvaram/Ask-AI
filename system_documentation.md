# AskAI: Advanced Knowledge Intelligence OS

**AskAI** is a state-of-the-art document intelligence platform designed to transform how users interact with their knowledge bases. Powered by Azure OpenAI and the Semantic Kernel, it provides highly isolated, multi-document Retrieval-Augmented Generation (RAG) capabilities coupled with an enterprise-grade deduplication engine.

---

## 🌟 Core Features

### 1. Multi-Document Chat & Isolation
- **Context Grounding:** Users can seamlessly chat with multiple documents (PDF, TXT, MD, CSV, DOCX). The system intelligently pulls context exclusively from the "Active Document" in the current session.
- **Thread Safety & Multi-Document RAG:** Completely stateless backend generation ensures that chat sessions are never polluted with cross-document data unless explicitly requested.

### 2. Intelligent Deduplication Engine
- **Instant Exact Duplicate Rejection:** Computes a strict SHA-256 cryptographic hash of the binary file during upload. If the exact file is already present, the API instantly rejects it to save processing time and LLM costs.
- **Smart Near-Duplicate Detection:** For documents with different binary hashes (e.g. minor edits), the system extracts the text and generates a semantic "Summary Embedding." If it detects >95% similarity with an existing document, the system triggers a conflict resolution modal offering to:
  - Create a new version
  - Replace the existing version
  - Store as a separate document
- **Chunk-Level Deduplication:** Instead of duplicating context for repeating paragraphs or disclaimers, individual text chunks are hashed and stored in a global, deduplicated vector pool, significantly minimizing storage footprint.

### 3. High-Performance Retrieval & Caching
- **Semantic Caching:** Frequently asked questions are semantically cached in Azure SQL. If a user asks a conceptually identical question within the same document context, the system serves the cached response instantly.
- **Vector Search:** Highly optimized Cosine Similarity SQL functions return the top-k most relevant chunks in milliseconds.

### 4. Modern Glassmorphic UI
- **Reactive Workflow:** Built with React & Vite, featuring smooth step-by-step upload animations, dynamic sidebar indexing, and real-time LLM streaming.
- **Premium Aesthetics:** Dark mode by default, utilizing curated minimalist typography (Outfit, Manrope, Space Grotesk, Fira Code), subtle micro-animations, and glassmorphic badges.

---

## 🏗️ Architecture Stack

### Backend
- **Framework:** C# ASP.NET Core Minimal API
- **AI Orchestration:** Microsoft Semantic Kernel
- **Models:** Azure OpenAI (`startup-gpt` for Chat Completions, `text-embedding-3-small` for Vector Embeddings)
- **Database:** Azure SQL (Relational structured data & Vector storage)
- **Document Parsing:** PdfPig (PDFs), OpenXml (DOCX), standard IO for plain text.

### Frontend
- **Framework:** React + Vite
- **Styling:** Vanilla CSS, heavily utilizing flexbox, CSS Grid, and custom variables for theming.
- **Networking:** Axios for HTTP requests, handling both multipart-form data (uploads) and standard JSON payloads.

---

## 🗄️ Database Schema Overview

The system utilizes an optimized relational architecture for deduplication:

1. **`Documents` Table:** Stores metadata, `FileHash` (for exact match), `SummaryEmbedding` (for near-match), `VersionGroupId`, `Version`, and `Status` ('Latest' vs 'Archived').
2. **`Chunks` Table:** A global pool of unique text blocks. Contains `ChunkHash`, `ChunkText`, and the 1536-dimensional `Embedding`.
3. **`DocumentChunkMapping` Table:** A many-to-many relationship linking `Documents` to their respective `Chunks`.
4. **`QuestionCache` Table:** Stores past queries and responses, strictly bound by `DocumentId` for semantic caching.
