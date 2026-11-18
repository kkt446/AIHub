export function relTime(iso: string, now = new Date()): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "--";
  const diff = now.getTime() - date.getTime();
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return "刚刚";
  if (minutes < 60) return `${minutes} 分钟前`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24 && date.getDate() === now.getDate()) return `${hours} 小时前`;
  const pad = (n: number) => String(n).padStart(2, "0");
  if (date.getFullYear() === now.getFullYear()) {
    return `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export function fmtClock(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "--:--";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function oneLine(text: string, max = 120): string {
  const flat = text.replace(/\s+/g, " ").trim();
  return flat.length > max ? flat.slice(0, max) + "…" : flat;
}

export function countAttachments(messages: { attachments?: unknown[] }[]): number {
  return messages.reduce((sum, m) => sum + (m.attachments?.length ?? 0), 0);
}

export function lastMessage(
  messages: { role: string; text: string }[],
  role: "user" | "assistant",
): string {
  for (let i = messages.length - 1; i >= 0; i--) {
    if (messages[i].role === role) return messages[i].text;
  }
  return "";
}
