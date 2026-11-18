import { ClipboardCopy, MessagesSquare, Paperclip } from "lucide-react";
import { toast } from "sonner";
import { ClientBadge } from "@/components/client-badge";
import { MessageRow } from "@/components/message-row";
import { Button } from "@/components/ui/button";
import { ScrollArea } from "@/components/ui/scroll-area";
import { countAttachments, fmtClock, relTime } from "@/lib/format";
import { CLIENT_META, ROLE_LABEL, type HubSession } from "@/lib/hub";

function copyTranscript(session: HubSession) {
  const body = session.messages
    .map((m) => (m.role === "tool_call" || m.role === "tool_result"
      ? `[${ROLE_LABEL[m.role]}${m.tool ? ` · ${m.tool}` : ""}] ${m.text}`
      : `[${ROLE_LABEL[m.role]}] ${m.text}`))
    .join("\n\n");
  navigator.clipboard
    .writeText(`# ${session.title}\n\n${body}`)
    .then(() => toast.success("会话内容已复制"))
    .catch(() => toast.error("复制失败，浏览器未授权剪贴板"));
}

export function SessionDetail({ session }: { session: HubSession | null }) {
  if (!session) {
    return (
      <div className="flex h-full flex-col items-center justify-center gap-3 text-center text-muted-foreground">
        <div className="grid size-12 place-items-center rounded-2xl border border-border/70 bg-surface">
          <MessagesSquare className="size-5" />
        </div>
        <div className="text-sm">选择左侧会话查看完整对话</div>
        <div className="max-w-[260px] text-xs">消息与附件都来自共享工作区，三端内容保持一致。</div>
      </div>
    );
  }

  const atts = countAttachments(session.messages);

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex flex-wrap items-start justify-between gap-3 border-b border-border/60 px-5 py-4">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <ClientBadge client={session.client} />
            <span className="text-[11px] text-muted-foreground">{CLIENT_META[session.client].description}</span>
          </div>
          <h2 className="mt-2 truncate text-[17px] font-semibold tracking-tight">{session.title}</h2>
          <div className="mt-1 flex flex-wrap items-center gap-3 text-[11px] text-muted-foreground">
            <span>更新于 {relTime(session.updated)}</span>
            <span>{session.messages.length} 条消息</span>
            {atts > 0 && (
              <span className="inline-flex items-center gap-1">
                <Paperclip className="size-3" />
                {atts} 个附件
              </span>
            )}
          </div>
        </div>
        <Button variant="outline" size="sm" onClick={() => copyTranscript(session)} className="gap-1.5">
          <ClipboardCopy className="size-3.5" />
          复制全文
        </Button>
      </div>

      <ScrollArea className="min-h-0 flex-1">
        <div className="space-y-4 px-5 py-5">
          {session.messages.map((message, index) => (
            <div key={message.id} className="animate-fade" style={{ animationDelay: `${Math.min(index, 8) * 30}ms` }}>
              <MessageRow message={message} />
              <div className="mt-1 px-10 text-[10px] text-muted-foreground/70">
                {index === 0 ? fmtClock(session.updated) : ""}
              </div>
            </div>
          ))}
          {session.messages.length === 0 && (
            <div className="py-16 text-center text-sm text-muted-foreground">没有消息记录</div>
          )}
        </div>
      </ScrollArea>
    </div>
  );
}
