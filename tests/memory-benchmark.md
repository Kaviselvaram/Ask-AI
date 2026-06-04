# Memory Benchmark Tests

This benchmark verifies that the Phase 3 Conversation Memory Engine resolves pronouns correctly without breaking vector search.

| User Query | Memory Context | Rewritten Query (Expected) | Intent | Strategy | Expected Entity Extracted |
|------------|----------------|-----------------------------|--------|----------|---------------------------|
| What is AIOS? | None | What is AIOS? | QuickFact | Fact | AIOS |
| Who uses it? | Previous: What is AIOS? | Who uses AIOS? | QuickFact | Fact | AIOS |
| Summarize Bookreview report.docx | None | Summarize Bookreview report.docx | Summary | Summary | Bookreview report.docx |
| What were its weaknesses? | Previous: Summarize Bookreview... | What were the weaknesses of Bookreview report.docx? | QuickFact | Fact | Bookreview report.docx |
| Compare AIOS and Moodle | None | Compare AIOS and Moodle | Comparison | Comparison | AIOS, Moodle |
| Which one is better for schools? | Previous: Compare AIOS and Moodle | Is AIOS or Moodle better for schools? | Comparison | Comparison | AIOS, Moodle |
| Tell me about Deep Work | None | Tell me about Deep Work | QuickFact | Fact | Deep Work |
| Who wrote it? | Previous: Tell me about Deep Work | Who wrote Deep Work? | QuickFact | Fact | Deep Work |
