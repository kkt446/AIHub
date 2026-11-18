import { useMemo } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { ClipboardCopy, Download, FileText, Sparkles, Terminal } from "lucide-react";
import { toast } from "sonner";
import { ClientBadge } from "@/components/client-badge";
import { Button } from "@/components/ui/button";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useHub } from "@/components/hub-provider";
import { buildHandoff } from "@/lib/handoff";
import { oneLine, relTime, lastMessage } from "@/lib/format";

export const Route = createFileRoute("/handoff")({
  component: HandoffPage,
});

function HandoffPage() {
  const { state } = useHub();
  const markdown = useMemo(() => buildHandoff(state.data), [state.data]);
  const latest = state.data.sessions[0];

  const copy = () => {
    navigator.clipboard
      .writeText(markdown)
      .then(() => toast.success("交接内容已复制"))
      .catch(() => toast.error("复制失败，浏览器未授权剪贴板"));
  };

  const download = () => {
    const blob = new Blob([markdown], { type: "text/markdown;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "handoff.md";
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="grid gap-5 px-5 py-5 sm:px-7 xl:grid-cols-[360px_1fr]">
      <div className="space-y-4">
        <div className="glass animate-rise rounded-2xl p-4">
          <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
            <Sparkles className="size-3.5 text-brand" /> 最近一次进展
          </div>
          {latest ? (
            <div className="mt-3 space-y-3">
              <ClientBadge client={latest.client} />
              <div className="text-sm font-medium leading-snug">{latest.title}</div>
              <div className="rounded-xl border border-border/60 bg-surface px-3 py-2.5 text-xs leading-relaxed text-muted-foreground">
                <div className="mb-1 text-[10px] uppercase tracking-wider text-muted-foreground/70">用户</div>
                {oneLine(lastMessage(latest.messages, "user"), 140) || "—"}
                <div className="mb-1 mt-2.5 text-[10px] uppercase tracking-wider text-muted-foreground/70">助手</div>
                {oneLine(lastMessage(latest.messages, "assistant"), 200) || "—"}
              </div>
              <div className="text-[11px] text-muted-foreground">更新于 {relTime(latest.updated)}</div>
            </div>
          ) : (
            <div className="mt-3 text-xs text-muted-foreground">还没有任何客户端写入会话。</div>
          )}
        </div>

        <div className="glass animate-rise rounded-2xl p-4">
          <div className="text-xs font-medium text-muted-foreground">注入方式</div>
          <ul className="mt-3 space-y-3 text-xs text-muted-foreground">
            <li className="flex gap-2.5">
              <Terminal className="mt-0.5 size-3.5 shrink-0 text-qoder" />
              <span>Qoder 新会话：SessionStart 钩子把摘要注入额外上下文。</span>
            </li>
            <li className="flex gap-2.5">
              <FileText className="mt-0.5 size-3.5 shrink-0 text-qodercn" />
              <span>dsh：自动写入工作区的 AGENTS.local.md，启动时读取。</span>
            </li>
            <li className="flex gap-2.5">
              <ClipboardCopy className="mt-0.5 size-3.5 shrink-0 text-dsh" />
              <span>手动：复制下方 Markdown，粘贴到任意客户端即可接续。</span>
            </li>
          </ul>
        </div>
      </div>

      <div className="glass animate-rise flex min-h-[520px] flex-col overflow-hidden rounded-2xl">
        <Tabs defaultValue="preview" className="flex min-h-0 flex-1 flex-col">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 px-4 py-3">
            <TabsList className="h-8 bg-surface">
              <TabsTrigger value="preview" className="text-xs">摘要预览</TabsTrigger>
              <TabsTrigger value="raw" className="text-xs">Markdown</TabsTrigger>
            </TabsList>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" onClick={copy} className="gap-1.5">
                <ClipboardCopy className="size-3.5" /> 复制
              </Button>
              <Button variant="outline" size="sm" onClick={download} className="gap-1.5">
                <Download className="size-3.5" /> 下载
              </Button>
            </div>
          </div>

          <TabsContent value="preview" className="m-0 min-h-0 flex-1">
            <ScrollArea className="h-[calc(100dvh-330px)] min-h-[420px]">
              <div className="space-y-5 px-5 py-5">
                <div>
                  <div className="text-[11px] uppercase tracking-wider text-muted-foreground">工作区</div>
                  <div className="mt-1 break-all font-mono text-xs text-foreground/80">{state.data.workspace}</div>
                </div>
                <div>
                  <div className="mb-2 text-[11px] uppercase tracking-wider text-muted-foreground">最近会话</div>
                  <div className="space-y-2">
                    {state.data.sessions.slice(0, 8).map((s) => (
                      <div
                        key={s.key}
                        className="flex items-center justify-between gap-3 rounded-xl border border-border/60 bg-surface px-3 py-2.5"
                      >
                        <div className="flex min-w-0 items-center gap-2.5">
                          <ClientBadge client={s.client} />
                          <span className="truncate text-xs">{s.title}</span>
                        </div>
                        <span className="shrink-0 text-[11px] text-muted-foreground">{relTime(s.updated)}</span>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </ScrollArea>
          </TabsContent>

          <TabsContent value="raw" className="m-0 min-h-0 flex-1">
            <ScrollArea className="h-[calc(100dvh-330px)] min-h-[420px]">
              <pre className="whitespace-pre-wrap break-words px-5 py-5 font-mono text-xs leading-relaxed text-foreground/80">
                {markdown}
              </pre>
            </ScrollArea>
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
}
