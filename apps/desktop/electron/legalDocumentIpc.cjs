const fs = require("node:fs");
const path = require("node:path");

const legalDocuments = {
  statement: {
    fileName: "PERSONAL_STATEMENT.md",
    repoRelativePath: path.join("docs", "legal", "PERSONAL_STATEMENT.md")
  },
  license: {
    fileName: "LICENSE",
    repoRelativePath: "LICENSE"
  },
  privacy: {
    fileName: "PRIVACY.md",
    repoRelativePath: path.join("docs", "legal", "PRIVACY.md")
  },
  "data-safety": {
    fileName: "DATA_SAFETY.md",
    repoRelativePath: path.join("docs", "legal", "DATA_SAFETY.md")
  },
  "ai-notice": {
    fileName: "AI_NOTICE.md",
    repoRelativePath: path.join("docs", "legal", "AI_NOTICE.md")
  },
  disclaimer: {
    fileName: "DISCLAIMER.md",
    repoRelativePath: path.join("docs", "legal", "DISCLAIMER.md")
  }
};

function getLegalDocument(documentId) {
  if (typeof documentId !== "string") {
    return null;
  }

  return legalDocuments[documentId] ?? null;
}

function resolveLegalDocumentPath(documentId, options = {}) {
  const document = getLegalDocument(documentId);
  if (!document) {
    return null;
  }

  if (typeof options.legalRoot === "string" && options.legalRoot.trim()) {
    return path.join(options.legalRoot, document.fileName);
  }

  if (typeof options.repoRoot === "string" && options.repoRoot.trim()) {
    return path.join(options.repoRoot, document.repoRelativePath);
  }

  return null;
}

async function readLegalDocument(documentId, options) {
  const { legalRoot, repoRoot, exists = fs.existsSync, readFile = fs.promises.readFile } = options;
  const document = getLegalDocument(documentId);
  if (!document) {
    return null;
  }

  const documentPath = resolveLegalDocumentPath(documentId, { legalRoot, repoRoot });
  if (!documentPath || !exists(documentPath)) {
    return null;
  }

  const content = await readFile(documentPath, "utf8");
  return {
    fileName: document.fileName,
    content
  };
}

function createLegalDocumentIpcHandlers(options) {
  const { ipcMain, legalRoot, repoRoot, exists = fs.existsSync } = options;
  ipcMain.handle("journal:read-legal-document", (_event, documentId) =>
    readLegalDocument(documentId, { legalRoot, repoRoot, exists }));
}

module.exports = {
  createLegalDocumentIpcHandlers,
  readLegalDocument,
  resolveLegalDocumentPath
};
