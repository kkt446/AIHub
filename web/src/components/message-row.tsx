import { Bot, FileImage, Paperclip, Terminal, User, Wrench } from "lucide-react";
import { cva } from "class-variance-authority";
import type { HubMessage } from "@/lib/hub";
import { cn } from "@/lib/utils";

const bubbleVariants = cva("max-w-[86%] rounded-2xl px-4 py-3 text-[13.5px] leading-relaxed", {
  variants: {
    side: {
      user: "rounded-tr-md bg-gradient-to-br from-brand/85 to-qoder/70 text-primary-foreground shadow-lg shadow-brand/15",
      assistant: "rounded-tl-md border border-border/60 bg-card/70 text-card-foreground",
    },
  },
  defaultVariants: { side: "assistant" },
});

const avatarVariants = cva("mt-0.5 grid size-7 shrink-0 place-items-center rounded-full border", {
  variants: {
    side: {
      user: "border-brand/30 bg-brand-soft text-brand",
      assistant: "border-border/80 bg-surface-strong text-foreground/80",
    },
  },
  defaultVariants: { side: "assistant" },
});

export function MessageRow({
  message,
  imageUrl,
}: {
  message: HubMessage;
  imageUrl?: (name: string) => string | undefined;
}) {
  if (message.role === "tool_call" || message.role === "tool_result") {
    const isCall = message.role === "tool_call";
    return (
      <div className="flex items-start gap-2.5 pl-1 text-xs text-muted-foreground">
        <div className="mt-0.5 grid size-6 shrink-0 place-items-center rounded-md border border-border/70 bg-surface text-muted-foreground">
          {isCall ? <Wrench className="size-3" /> : <Terminal className="size-3" />}
        </div>
        <div className="min-w-0 flex-1 rounded-lg border border-border/50 bg-surface px-3 py-2">
          <span className="mr-2 font-medium text-foreground/70">{isCall ? message.tool ?? "tool" : "结果"}</span>
          <span className="font-mono text-[11px] text-muted-foreground">{message.text}</span>
        </div>
      </div>
    );
  }

  const isUser = message.role === "user";
  const side = isUser ? "user" : "assistant";
  return (
    <div className={cn("flex items-start gap-2.5", isUser ? "flex-row-reverse" : "flex-row")}>
      <div className={avatarVariants({ side })}>
        {isUser ? <User className="size-3.5" /> : <Bot className="size-3.5" />}
      </div>
      <div className={bubbleVariants({ side })}>
        <p className="whitespace-pre-wrap break-words">{message.text}</p>
        {message.attachments && message.attachments.length > 0 && (
          <div className="mt-2.5 flex flex-wrap gap-2">
            {message.attachments.map((att) => {
              const src = att.kind === "image" ? imageUrl?.(att.name) : undefined;
              if (src) {
                return (
                  <img
                    key={att.name}
                    src={src}
                    alt={att.name}
                    loading="lazy"
                    className="max-h-40 max-w-full rounded-lg border border-white/15 object-contain"
                  />
                );
              }
              return (
                <div
                  key={att.name}
                  className="flex items-center gap-2 rounded-lg border border-white/15 bg-black/15 px-2.5 py-1.5 text-[11px]"
                >
                  {att.kind === "image" ? <FileImage className="size-3.5" /> : <Paperclip className="size-3.5" />}
                  <span className="max-w-[180px] truncate font-mono">{att.name}</span>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
