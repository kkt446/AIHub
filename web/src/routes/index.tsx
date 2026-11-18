import { useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { cva } from "class-variance-authority";
import { Inbox, MessagesSquare, Paperclip, Search, Users } from "lucide-react";
import { SessionListItem } from "@/components/session-list-item";
import { SessionDetail } from "@/components/session-detail";
import { StatCard } from "@/components/stat-card";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { useHub } from "@/components/hub-provider";
import { countAttachments, relTime } from "@/lib/format";
import type { ClientId } from "@/lib/hub";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/")({
  component: OverviewPage,
});

type Filter = "all" | ClientId;

const filterVariants = cva(
  "rounded-full border px-3 py-1 text-xs transition-colors",
  {
    variants: {
      active: {
        true: "border-brand/40 bg-brand-soft text-brand",
        false: "border-border/70 text-muted-foreground hover:border-border hover:text-foreground",
      },
    },
    defaultVariants: { active: false },
  },
);

const FILTERS: { id: Filter; label: string }[] = [
  { id: "all", label: "全部" },
  { id: "qoder", label: "Qoder" },
  { id: "qodercn", label: "Qoder CN" },
  { id: "dsh", label: "dsh" },
];

function OverviewPage() {
  const { state } = useHub();
  const [filter, setFilter] = useState<Filter>("all");
  const [query, setQuery] = useState("");
  const [selectedKey, setSelectedKey] = useState<string | null>(null);

  const sessions = state.data.sessions;
  const messages = sessions.reduce((sum, s) => sum + s.messages.length, 0);
  const attachments = sessions.reduce((sum, s) => sum + countAttachments(s.messages), 0);
  const clients = new Set(sessions.map((s) => s.client)).size;

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return sessions.filter((s) => {
      if (filter !== "all" && s.client !== filter) return false;
      if (!q) return true;
      return (
        s.title.toLowerCase().includes(q) ||
        s.messages.some((m) => m.text.toLowerCase().includes(q))
      );
    });
  }, [sessions, filter, query]);

  const selected = filtered.find((s) => s.key === selectedKey) ?? filtered[0] ?? null;
  const latest = sessions[0];

  return (
    <div className="space-y-5 px-5 py-5 sm:px-7">
      <section className="grid grid-cols-2 gap-3 xl:grid-cols-4">
        <StatCard icon={Inbox} label="会话总数" value={String(sessions.length)} hint="来自三端自动归档" accent="brand" />
        <StatCard icon={MessagesSquare} label="消息总数" value={String(messages)} hint="含工具调用与结果" accent="qoder" />
        <StatCard icon={Paperclip} label="附件" value={String(attachments)} hint="图片与文件按会话隔离" accent="qodercn" />
        <StatCard
          icon={Users}
          label="活跃客户端"
          value={`${clients} 端`}
          hint={latest ? `最近更新 ${relTime(latest.updated)}` : "暂无会话"}
          accent="dsh"
        />
      </section>

      <section className="glass animate-rise overflow-hidden rounded-2xl">
        <div className="flex flex-wrap items-center gap-2 border-b border-border/60 px-4 py-3">
          <div className="relative min-w-[180px] flex-1">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="搜索会话标题或消息内容"
              className="h-8 border-border/70 bg-surface pl-8 text-xs placeholder:text-muted-foreground/70"
            />
          </div>
          <div className="flex flex-wrap items-center gap-1.5">
            {FILTERS.map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() => setFilter(item.id)}
                className={cn(filterVariants({ active: filter === item.id }))}
              >
                {item.label}
              </button>
            ))}
          </div>
        </div>

        <div className="grid min-h-[460px] grid-cols-1 lg:h-[calc(100dvh-330px)] lg:grid-cols-[360px_1fr]">
          <div className="flex min-h-0 flex-col border-b border-border/60 lg:border-b-0 lg:border-r">
            <div className="flex items-center justify-between px-4 py-2.5 text-[11px] text-muted-foreground">
              <span>筛选结果</span>
              <span>{filtered.length} 个会话</span>
            </div>
            <ScrollArea className="h-[38vh] min-h-0 lg:h-auto lg:flex-1">
              <div className="space-y-1 px-2 pb-3">
                {filtered.map((session) => (
                  <SessionListItem
                    key={session.key}
                    session={session}
                    active={selected?.key === session.key}
                    onSelect={() => setSelectedKey(session.key)}
                  />
                ))}
                {filtered.length === 0 && (
                  <div className="px-4 py-14 text-center text-xs text-muted-foreground">
                    没有匹配的会话，换个关键词或筛选条件试试。
                  </div>
                )}
              </div>
            </ScrollArea>
          </div>

          <div className="flex min-h-[380px] min-w-0 flex-col lg:min-h-0">
            <SessionDetail session={selected} />
          </div>
        </div>
      </section>
    </div>
  );
}
