import { CLIENT_META, type HubData } from "./hub";
import { lastMessage, oneLine } from "./format";

export function buildHandoff(data: HubData, maxChars = 3500): string {
  const lines: string[] = [];
  lines.push("# 共享工作区交接（AIHub 自动生成）", "");
  lines.push(`工作区：${data.workspace}`);
  const generated = new Date(data.generatedAt);
  const pad = (n: number) => String(n).padStart(2, "0");
  lines.push(
    `生成时间：${generated.getFullYear()}-${pad(generated.getMonth() + 1)}-${pad(generated.getDate())} ${pad(generated.getHours())}:${pad(generated.getMinutes())}`,
    "",
  );
  if (data.sessions.length === 0) {
    lines.push("还没有任何客户端写入会话。");
    return lines.join("\n");
  }

  lines.push("## 最近会话");
  data.sessions.slice(0, 12).forEach((s) => {
    const when = new Date(s.updated);
    lines.push(
      `- [${CLIENT_META[s.client].name}] ${s.title}（更新 ${pad(when.getMonth() + 1)}-${pad(when.getDate())} ${pad(when.getHours())}:${pad(when.getMinutes())}，${s.messages.length} 条消息）`,
    );
  });
  lines.push("");

  const latest = data.sessions[0];
  lines.push(`## 最近一次进展：${CLIENT_META[latest.client].name} · ${latest.title}`);
  const userText = lastMessage(latest.messages, "user");
  const assistantText = lastMessage(latest.messages, "assistant");
  if (userText) lines.push(`用户上一条：${oneLine(userText, 600)}`, "");
  if (assistantText) lines.push(`助手上一条：${oneLine(assistantText, 900)}`, "");

  const text = lines.join("\n");
  return text.length > maxChars ? text.slice(0, maxChars) + "\n（已截断）" : text;
}
