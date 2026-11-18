export type ClientId = "qoder" | "qodercn" | "dsh";

export type MessageRole = "user" | "assistant" | "tool_call" | "tool_result";

export interface HubAttachment {
  name: string;
  kind: "image" | "file";
}

export interface HubMessage {
  id: string;
  role: MessageRole;
  text: string;
  tool?: string;
  attachments?: HubAttachment[];
}

export interface HubSession {
  key: string;
  client: ClientId;
  title: string;
  updated: string;
  messages: HubMessage[];
}

export interface HubData {
  workspace: string;
  generatedAt: string;
  sessions: HubSession[];
  assetBase?: string;
}

export type DataSource = "live" | "imported" | "demo";

export interface HubState {
  data: HubData;
  source: DataSource;
}

export const CLIENT_META: Record<
  ClientId,
  { name: string; short: string; description: string; accent: string }
> = {
  qoder: {
    name: "Qoder 国际版",
    short: "Qoder",
    description: "主开发端 · 模型 dfmodel",
    accent: "text-qoder",
  },
  qodercn: {
    name: "Qoder CN",
    short: "CN",
    description: "国内端 · 模型 qfmodel",
    accent: "text-qodercn",
  },
  dsh: {
    name: "DeepSeek Harness",
    short: "dsh",
    description: "命令行端 · 读取 AGENTS.local.md",
    accent: "text-dsh",
  },
};

export const ROLE_LABEL: Record<MessageRole, string> = {
  user: "用户",
  assistant: "助手",
  tool_call: "工具调用",
  tool_result: "工具结果",
};

export function isHubData(value: unknown): value is HubData {
  if (!value || typeof value !== "object") return false;
  const v = value as HubData;
  return typeof v.workspace === "string" && Array.isArray(v.sessions);
}

export function normalizeSession(raw: HubSession, index: number): HubSession {
  return {
    key: raw.key || `session-${index}`,
    client: raw.client in CLIENT_META ? raw.client : "qoder",
    title: raw.title || "未命名会话",
    updated: raw.updated || new Date().toISOString(),
    messages: Array.isArray(raw.messages) ? raw.messages : [],
  };
}
