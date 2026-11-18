import { cva } from "class-variance-authority";
import { Paperclip } from "lucide-react";
import { ClientBadge } from "@/components/client-badge";
import { countAttachments, oneLine, relTime } from "@/lib/format";
import type { HubSession } from "@/lib/hub";
import { cn } from "@/lib/utils";

const itemVariants = cva(
  "w-full rounded-xl border px-3.5 py-3 text-left transition-all duration-200",
  {
    variants: {
      active: {
        true: "border-brand/40 bg-brand-soft/60 shadow-[0_10px_40px_-24px_oklch(0.72_0.17_288/0.8)]",
        false: "border-transparent bg-transparent hover:border-border/70 hover:bg-surface",
      },
    },
    defaultVariants: { active: false },
  },
);

export function SessionListItem({
  session,
  active,
  onSelect,
}: {
  session: HubSession;
  active: boolean;
  onSelect: () => void;
}) {
  const last = [...session.messages].reverse().find((m) => m.role === "user")?.text ?? "";
  const atts = countAttachments(session.messages);

  return (
    <button type="button" onClick={onSelect} className={cn(itemVariants({ active }))}>
      <div className="flex items-center justify-between gap-2">
        <ClientBadge client={session.client} />
        <span className="shrink-0 text-[11px] text-muted-foreground">{relTime(session.updated)}</span>
      </div>
      <div className="mt-2 truncate text-[13.5px] font-medium leading-snug">{session.title}</div>
      <div className="mt-1 line-clamp-2 text-xs leading-relaxed text-muted-foreground">
        {oneLine(last, 90) || "暂无消息"}
      </div>
      <div className="mt-2 flex items-center gap-3 text-[11px] text-muted-foreground">
        <span>{session.messages.length} 条消息</span>
        {atts > 0 && (
          <span className="inline-flex items-center gap-1">
            <Paperclip className="size-3" />
            {atts}
          </span>
        )}
      </div>
    </button>
  );
}
