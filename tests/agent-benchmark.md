# Lightweight Agents Benchmark Tests

This document outlines the benchmark test cases for the Phase 5 Lightweight Agents.

## Test Case 1: Fact/Research Routing

**Question:** What is AIOS?
**Expected Result:**
- **Selected Agents:** Research Agent only.
- **Explanation:** The query intent maps to factual/research. The Orchestrator routes the request only to the Research agent.
- **Verification Layer:** The Research agent's output is passed to the Verification layer successfully.

---

## Test Case 2: Explicit Research Routing

**Question:** Research AIOS in detail
**Expected Result:**
- **Selected Agents:** Research Agent only.
- **Explanation:** The query intent explicitly involves research. The Orchestrator routes the request only to the Research agent.
- **Verification Layer:** The output is passed to the Verification layer successfully.

---

## Test Case 3: Comparison Routing

**Question:** Compare AIOS and Moodle
**Expected Result:**
- **Selected Agents:** Research Agent + Comparison Agent.
- **Explanation:** The query intent involves a comparison. The Orchestrator triggers both agents sequentially. First, the Research agent extracts facts, then the Comparison agent uses those facts to construct a comparison.
- **Verification Layer:** The aggregated findings of both agents are passed to the Verification layer successfully.

---

## Test Case 4: Document Comparison Routing

**Question:** Compare uploaded documents
**Expected Result:**
- **Selected Agents:** Research Agent + Comparison Agent.
- **Explanation:** The query intent involves comparing content from the retrieved context. The Orchestrator correctly selects both agents and aggregates their outputs.
- **Verification Layer:** The final output is checked by the Verification layer.
