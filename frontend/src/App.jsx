import { useState, useEffect, useRef } from "react";
import axios from "axios";
import "./App.css";
import ReactMarkdown from "react-markdown";
import { FaPaperPlane } from "react-icons/fa";

function App() {
  //
  // CORE STATES
  //
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  // Chat Sessions State
  const [chats, setChats] = useState([
    {
      id: 1,
      title: "Knowledge Session",
      messages: []
    }
  ]);
  const [activeChatId, setActiveChatId] = useState(1);

  // Active Chat Session helper
  const activeChat = chats.find(chat => chat.id === activeChatId) || chats[0];

  // Document Vault Ingestion states
  const [documents, setDocuments] = useState([]);
  const [uploadingState, setUploadingState] = useState(null); // 'uploading' | 'extracting' | 'chunking' | 'embeddings' | 'storing' | 'ready' | null
  const [uploadedFileDetails, setUploadedFileDetails] = useState(null);
  const [isDragging, setIsDragging] = useState(false);

  // Retrieval Pipeline animation states
  const [retrievalState, setRetrievalState] = useState(null); // 'received' | 'embedding' | 'searching' | 'scoring' | 'chunks' | 'context' | 'generating' | 'ready' | null

  // Duplicate Resolution State
  const [pendingSimilarity, setPendingSimilarity] = useState(null);

  // Scroll target
  const messagesEndRef = useRef(null);

  //
  // INITIALIZERS & FETCHERS
  //
  useEffect(() => {
    fetchDocuments();
  }, []);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [activeChat?.messages, retrievalState]);

  const fetchDocuments = async () => {
    try {
      const res = await axios.get(`${import.meta.env.VITE_API_BASE_URL}/documents`);
      setDocuments(res.data);
    } catch (err) {
      console.error("Error fetching documents:", err);
    }
  };

  //
  // SESSION CREATION & NAVIGATION
  //
  const createNewChat = () => {
    const newChat = {
      id: Date.now(),
      title: "New Session",
      messages: []
    };
    setChats(prev => [newChat, ...prev]);
    setActiveChatId(newChat.id);
  };



  //
  // FILE UPLOAD AND INGESTION PIPELINE
  //
  const triggerIngestionPipeline = async (file, action = null, targetGroup = null) => {
    if (!file) return;

    const formData = new FormData();
    formData.append("file", file);
    if (action) formData.append("versionAction", action);
    if (targetGroup) formData.append("targetGroupId", targetGroup);

    setUploadedFileDetails({ name: file.name, size: (file.size / 1024).toFixed(1) + " KB" });

    // Stepwise animated transitions for text, chunk, and embedding pipelines
    setUploadingState("uploading");

    try {
      // API request is launched immediately
      const uploadPromise = axios.post(`${import.meta.env.VITE_API_BASE_URL}/upload`, formData, {
        headers: { "Content-Type": "multipart/form-data" }
      });

      // Animated steps to display ingestion mechanics
      await new Promise(r => setTimeout(r, 600));
      setUploadingState("extracting");
      await new Promise(r => setTimeout(r, 600));
      setUploadingState("chunking");
      await new Promise(r => setTimeout(r, 600));
      setUploadingState("embeddings");
      await new Promise(r => setTimeout(r, 500));
      setUploadingState("storing");

      const response = await uploadPromise;

      if (response.data.status === "exact_duplicate") {
        setUploadingState(null);
        alert(`Document "${response.data.fileName}" (Version ${response.data.version}) already exists in the knowledge base!`);
        return;
      } else if (response.data.status === "similar_found") {
        setUploadingState(null);
        setPendingSimilarity({
          file: file,
          similarDoc: response.data.similarDocument
        });
        return;
      }

      setUploadingState("ready");
      await new Promise(r => setTimeout(r, 800));
      setUploadingState(null);

      // Refresh dynamic sidebar list of indexed documents
      fetchDocuments();
    } catch (error) {
      console.error(error);
      setUploadingState(null);
      alert("Document ingestion failed. Please try again.");
    }
  };

  const resolveSimilarity = (action) => {
    if (pendingSimilarity) {
      triggerIngestionPipeline(pendingSimilarity.file, action, pendingSimilarity.similarDoc.versionGroupId);
      setPendingSimilarity(null);
    }
  };

  const handleFileInputChange = (e) => {
    const file = e.target.files[0];
    triggerIngestionPipeline(file);
  };

  const handleDragOver = (e) => {
    e.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = () => {
    setIsDragging(false);
  };

  const handleDrop = (e) => {
    e.preventDefault();
    setIsDragging(false);
    const file = e.dataTransfer.files[0];
    if (file && (file.type === "application/pdf" || file.name.match(/\.(txt|md|csv|docx)$/i))) {
      triggerIngestionPipeline(file);
    } else {
      alert("Only PDF, TXT, MD, CSV, and DOCX documents are supported at this stage.");
    }
  };

  //
  // SEND MESSAGE WITH RETRIEVAL PIPELINE
  //
  const sendMessage = async () => {
    if (!message.trim()) return;

    const userMessage = {
      role: "user",
      content: message
    };

    const chatTitle = message.length > 24 ? message.substring(0, 24) + "..." : message;

    setChats(prev =>
      prev.map(chat => {
        if (chat.id !== activeChatId) return chat;
        return {
          ...chat,
          title: chat.messages.length === 0 ? chatTitle : chat.title,
          messages: [...chat.messages, userMessage]
        };
      })
    );

    const currentMessage = message;
    setMessage("");
    setLoading(true);

    // Stepwise animated transitions representing command retrieval stages
    setRetrievalState("received");
    await new Promise(r => setTimeout(r, 300));
    setRetrievalState("embedding");
    await new Promise(r => setTimeout(r, 350));
    setRetrievalState("searching");
    await new Promise(r => setTimeout(r, 350));
    setRetrievalState("scoring");
    await new Promise(r => setTimeout(r, 300));
    setRetrievalState("chunks");
    await new Promise(r => setTimeout(r, 300));
    setRetrievalState("context");
    await new Promise(r => setTimeout(r, 300));
    setRetrievalState("generating");

    try {
      const res = await axios.post(`${import.meta.env.VITE_API_BASE_URL}/chat`, {
        message: currentMessage
      });

      setRetrievalState("ready");
      await new Promise(r => setTimeout(r, 200));
      setRetrievalState(null);

      const finalText = res.data.result;
      const sources = res.data.sources || [];
      let currentText = "";

      const aiMessage = {
        role: "assistant",
        content: "",
        sources: res.data.sources || [],
        chunksRetrieved: res.data.chunksRetrieved || 0,
        similarityScore: res.data.similarityScore
          ? res.data.similarityScore.toFixed(2)
          : "0.00"
      };

      setChats(prev =>
        prev.map(chat =>
          chat.id === activeChatId
            ? { ...chat, messages: [...chat.messages, aiMessage] }
            : chat
        )
      );

      // Typing Streaming Effect
      for (let i = 0; i < finalText.length; i++) {
        currentText += finalText[i];
        await new Promise(resolve => setTimeout(resolve, 4));

        setChats(prev =>
          prev.map(chat => {
            if (chat.id !== activeChatId) return chat;
            const updatedMessages = [...chat.messages];
            updatedMessages[updatedMessages.length - 1] = {
              ...aiMessage,
              content: currentText
            };
            return { ...chat, messages: updatedMessages };
          })
        );
      }
    } catch (err) {
      console.error(err);
      setRetrievalState(null);

      const errorMessage = {
        role: "assistant",
        content: "Operational connection to the intelligence endpoint has failed. Please verify if the backend is running."
      };

      setChats(prev =>
        prev.map(chat =>
          chat.id === activeChatId
            ? { ...chat, messages: [...chat.messages, errorMessage] }
            : chat
        )
      );
    }

    setLoading(false);
  };

  return (
    <div className="app">
      {pendingSimilarity && (
        <div style={{
          position: "fixed", top: 0, left: 0, width: "100%", height: "100%",
          backgroundColor: "rgba(0,0,0,0.7)", zIndex: 9999, display: "flex",
          alignItems: "center", justifyContent: "center"
        }}>
          <div style={{
            backgroundColor: "#222", padding: "30px", borderRadius: "10px",
            maxWidth: "500px", color: "white", boxShadow: "0 10px 30px rgba(0,0,0,0.5)"
          }}>
            <h2 style={{ marginTop: 0, color: "#10a37f" }}>Similar Document Detected</h2>
            <p style={{ lineHeight: 1.5 }}>The uploaded file is highly similar ({(pendingSimilarity.similarDoc.similarity * 100).toFixed(1)}%) to an existing document in the vault:</p>
            <div style={{
              backgroundColor: "#111", padding: "15px", borderRadius: "8px",
              marginBottom: "20px", border: "1px solid #333"
            }}>
              <strong style={{ color: "#fff" }}>{pendingSimilarity.similarDoc.fileName}</strong>
              <span style={{ marginLeft: "10px", color: "#888" }}>v{pendingSimilarity.similarDoc.version}</span>
            </div>
            <p style={{ marginBottom: "20px" }}>How would you like to proceed?</p>
            <div style={{ display: "flex", flexDirection: "column", gap: "10px" }}>
              <button style={{ padding: "12px", background: "#10a37f", color: "white", border: "none", borderRadius: "5px", cursor: "pointer" }} onClick={() => resolveSimilarity("create_version")}>Create New Version</button>
              <button style={{ padding: "12px", background: "#333", color: "white", border: "1px solid #444", borderRadius: "5px", cursor: "pointer" }} onClick={() => resolveSimilarity("replace")}>Replace Existing Version</button>
              <button style={{ padding: "12px", background: "#333", color: "white", border: "1px solid #444", borderRadius: "5px", cursor: "pointer" }} onClick={() => resolveSimilarity("store_separate")}>Store as Separate Document</button>
              <button style={{ padding: "12px", background: "transparent", color: "#888", border: "none", cursor: "pointer" }} onClick={() => setPendingSimilarity(null)}>Cancel</button>
            </div>
          </div>
        </div>
      )}

      {/* TOP COMMAND NAVIGATION */}
      <header className="top-bar">
        <div className="brand-container">
          <h1 className="brand-logo">
            AskAI // <span>Console</span>
          </h1>
        </div>

        <div className="platform-status-center">
          <div className="status-badge">
            Model: <strong>startup-gpt (OpenAI)</strong>
          </div>
          <div className="status-badge">
            Vault: <strong>{documents.length} Docs</strong>
          </div>
        </div>
      </header>

      {/* CORE WORKSPACE VIEW */}
      <div className="main-layout" style={{ position: 'relative' }}>
        {/* COLLAPSIBLE SIDEBAR */}
        <aside className={`sidebar ${sidebarCollapsed ? "collapsed" : ""}`}>
          <div className="sidebar-header">
            <button className="new-chat-btn" onClick={createNewChat}>
              + New Session
            </button>
          </div>

          <div className="sidebar-sections">
            {/* SESSION MEMORY */}
            <div className="sidebar-section">
              <h2 className="sidebar-section-title">
                <span>Sessions</span>
              </h2>
              <div className="chat-list">
                {chats.map(chat => (
                  <div
                    key={chat.id}
                    className={`chat-item ${chat.id === activeChatId ? "active-chat" : ""}`}
                    onClick={() => setActiveChatId(chat.id)}
                  >
                    {chat.title}
                  </div>
                ))}
              </div>
            </div>

            {/* KNOWLEDGE VAULT */}
            <div className="sidebar-section">
              <h2 className="sidebar-section-title">
                <span>Vault</span>
              </h2>
              <div className="document-list">
                {documents.length === 0 ? (
                  <div className="empty-vault-text">
                    No documents uploaded.
                  </div>
                ) : (
                  documents.map(doc => (
                    <div
                      key={doc.id}
                      className="document-item"
                    >
                      <div className="doc-header">
                        <span className="doc-name">{doc.fileName} <span style={{ fontSize: '0.8em', color: '#888' }}>v{doc.version}</span></span>
                      </div>
                      <div className="doc-telemetry">
                        <span>{doc.chunks} Chunks</span>
                        <span style={{ marginLeft: '8px', color: '#10a37f', fontSize: '0.9em' }}>{doc.status}</span>
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>
        </aside>

        <button
          className="sidebar-toggle-tab"
          onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
          title={sidebarCollapsed ? "Expand Sidebar" : "Collapse Sidebar"}
          style={{
            position: 'absolute',
            left: sidebarCollapsed ? '0px' : '272px',
            top: '50%',
            transform: 'translateY(-50%)',
            zIndex: 60,
            transition: 'left 0.35s cubic-bezier(0.16, 1, 0.3, 1)'
          }}
        >
          {sidebarCollapsed ? "❯" : "❮"}
        </button>

        {/* MAIN CENTRAL WORKSPACE */}
        <main className="chat-container">
          {activeChat?.messages.length === 0 && !uploadingState && !retrievalState ? (
            /* DYNAMIC INTRO workspace */
            <div className="intro-workspace">
              <div className="intro-header-block">
                <h1>Ask AI Console</h1>
                <p className="intro-desc">
                  Minimalist, high-performance document intelligence interface. Upload PDF, TXT, MD, CSV, or DOCX documents to start a context-grounded session.
                </p>
              </div>

              {/* INTEGRATED FILE INGESTION FIELD */}
              <div
                className={`upload-workspace-area ${isDragging ? "dragging" : ""}`}
                onDragOver={handleDragOver}
                onDragLeave={handleDragLeave}
                onDrop={handleDrop}
                onClick={() => document.getElementById("file-upload-input").click()}
              >
                <div className="upload-text">Drag & Drop Documents Here</div>
                <div className="upload-subtext">or click to browse local files</div>
                <input
                  type="file"
                  id="file-upload-input"
                  accept=".pdf,.txt,.md,.csv,.docx"
                  style={{ display: "none" }}
                  onChange={handleFileInputChange}
                />
              </div>
            </div>
          ) : (
            /* MESSAGES AND PIPELINE RETRIEVAL ANIMATION */
            <div className="messages">
              {activeChat?.messages.map((msg, index) => (
                <div
                  key={index}
                  className={msg.role === "user" ? "user-message-container" : "ai-message-container"}
                >
                  <div className={msg.role === "user" ? "user-message" : "ai-message"}>
                    {msg.role === "assistant" ? (
                      <>
                        <ReactMarkdown>{msg.content}</ReactMarkdown>

                        {msg.sources?.length > 0 && (
                          <div className="sources-section">
                            <div className="sources-title">
                              📚 Sources
                            </div>

                            {msg.sources.map((source, index) => (
                              <div
                                key={index}
                                className="source-item"
                              >
                                <a
                                  href={`${import.meta.env.VITE_API_BASE_URL}${source.downloadUrl}`}
                                  target="_blank"
                                  rel="noopener noreferrer"
                                  style={{ color: "inherit", textDecoration: "none" }}
                                >
                                  [{source.referenceId}] {source.fileName} ({source.formattedPages})
                                </a>
                              </div>
                            ))}
                          </div>
                        )}

                        {(msg.chunksRetrieved > 0) && (
                          <div className="retrieval-metadata">
                            <span>📄 Semantic Vault</span>
                            <span>🔢 {msg.chunksRetrieved} Chunks</span>
                            <span>🎯 Similarity: {msg.similarityScore}</span>
                          </div>
                        )}
                      </>
                    ) : (
                      msg.content
                    )}
                  </div>
                </div>
              ))}

              {/* REAL-TIME UPLOADING PIPELINE ANIMATION */}
              {uploadingState && (
                <div className="pipeline-progress-container">
                  <div className="pipeline-progress-title">
                    <span>Ingesting // {uploadedFileDetails?.name}</span>
                    <span className="pipeline-status-text">Processing...</span>
                  </div>
                  <div className="pipeline-steps">
                    <div className={`pipeline-step ${uploadingState === "uploading" ? "active" : "completed"}`}>
                      <span className="pipeline-step-label">[1] Upload</span>
                    </div>
                    <div className={`pipeline-step ${uploadingState === "extracting" ? "active" : (uploadingState === "uploading" ? "" : "completed")}`}>
                      <span className="pipeline-step-label">[2] Extract</span>
                    </div>
                    <div className={`pipeline-step ${uploadingState === "chunking" ? "active" : (["uploading", "extracting"].includes(uploadingState) ? "" : "completed")}`}>
                      <span className="pipeline-step-label">[3] Chunk</span>
                    </div>
                    <div className={`pipeline-step ${uploadingState === "embeddings" ? "active" : (["uploading", "extracting", "chunking"].includes(uploadingState) ? "" : "completed")}`}>
                      <span className="pipeline-step-label">[4] Embed</span>
                    </div>
                    <div className={`pipeline-step ${uploadingState === "storing" ? "active" : (["uploading", "extracting", "chunking", "embeddings"].includes(uploadingState) ? "" : "completed")}`}>
                      <span className="pipeline-step-label">[5] Store</span>
                    </div>
                    <div className={`pipeline-step ${uploadingState === "ready" ? "active" : ""}`}>
                      <span className="pipeline-step-label">[✓] Ready</span>
                    </div>
                  </div>
                </div>
              )}

              {/* REAL-TIME RETRIEVAL PIPELINE ANIMATION */}
              {retrievalState && (
                <div className="retrieval-pipeline-animation">
                  <div className="retrieval-pipeline-title">
                    <span>Semantic Search Pipeline // Active</span>
                  </div>
                  <div className="retrieval-list">
                    <div className={`retrieval-node ${retrievalState === "received" ? "active" : "completed"}`}>
                      [1] Query Received
                    </div>
                    <div className={`retrieval-node ${retrievalState === "embedding" ? "active" : (["received"].includes(retrievalState) ? "" : "completed")}`}>
                      [2] Embed Query
                    </div>
                    <div className={`retrieval-node ${retrievalState === "searching" ? "active" : (["received", "embedding"].includes(retrievalState) ? "" : "completed")}`}>
                      [3] Vector Search
                    </div>
                    <div className={`retrieval-node ${retrievalState === "scoring" ? "active" : (["received", "embedding", "searching"].includes(retrievalState) ? "" : "completed")}`}>
                      [4] Similarity Match
                    </div>
                    <div className={`retrieval-node ${retrievalState === "chunks" ? "active" : (["received", "embedding", "searching", "scoring"].includes(retrievalState) ? "" : "completed")}`}>
                      [5] Retrieve Chunks
                    </div>
                    <div className={`retrieval-node ${retrievalState === "context" ? "active" : (["received", "embedding", "searching", "scoring", "chunks"].includes(retrievalState) ? "" : "completed")}`}>
                      [6] Build Context
                    </div>
                    <div className={`retrieval-node ${retrievalState === "generating" ? "active" : (["received", "embedding", "searching", "scoring", "chunks", "context"].includes(retrievalState) ? "" : "completed")}`}>
                      [7] LLM Generating
                    </div>
                    <div className={`retrieval-node ${retrievalState === "ready" ? "active" : ""}`}>
                      [✓] Dispatching Answer
                    </div>
                  </div>
                </div>
              )}

              <div ref={messagesEndRef} />
            </div>
          )}

          {/* COMMAND BOTTOM INPUT */}
          <div className="input-area-container">
            <div className="input-area">
              <textarea
                placeholder={"Ask a question from the unified knowledge base, or drag a document here to add..."}
                value={message}
                onChange={(e) => setMessage(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && !e.shiftKey) {
                    e.preventDefault();
                    sendMessage();
                  }
                }}
                disabled={loading || uploadingState !== null}
              />
              <button
                className="send-btn"
                onClick={sendMessage}
                disabled={!message.trim() || loading || uploadingState !== null}
                title="Send Command"
              >
                <FaPaperPlane />
              </button>
            </div>
          </div>
        </main>
      </div>
    </div>
  );
}

export default App;
