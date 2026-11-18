import { Link, useRouterState } from "@tanstack/react-router";
import { cva } from "class-variance-authority";
import {
  Database,
  FolderOpen,
  Github,
  LayoutDashboard,
  ScrollText,
  Settings2,
  Sparkles,
} from "lucide-react";
import { Toaster } from "@/components/ui/sonner";
import { ImportDialog } from "@/components/import-dialog";
import { useHub } from "@/components/hub-provider";
import { cn } from "@/lib/utils";

const NAV = [
  { to: "/", label: "会话总览", icon: LayoutDashboard, title: "会话总览", subtitle: "三端对话统一归档，随时接续" },
  { to: "/handoff", label: "交接内容", icon: ScrollText, title: "交接内容", subtitle: "新会话自动注入的上下文摘要" },
  { to: "/settings", label: "工作区设置", icon: Settings2, title: "工作区设置", subtitle: "客户端、同步与数据源" },
] as const;

const navItemVariants = cva(
  "group flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors",
  {
    variants: {
      active: {
        true: "bg-sidebar-accent text-sidebar-accent-foreground shadow-[inset_0_0_0_1px_oklch(1_0_0/8%)]",
        false: "text-muted-foreground hover:bg-sidebar-accent/60 hover:text-foreground",
      },
    },
    defaultVariants: { active: false },
  },
);

const sourceVariants = cva("inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-[11px]", {
  variants: {
    source: {
      live: "border-success/30 bg-success/10 text-success",
      imported: "border-brand/30 bg-brand-soft text-brand",
      demo: "border-dsh/30 bg-dsh/10 text-dsh",
    },
  },
  defaultVariants: { source: "demo" },
});

const navIconVariants = cva("size-4", {
  variants: {
    active: {
      true: "text-brand",
      false: "text-muted-foreground",
    },
  },
  defaultVariants: { active: false },
});

const mobileNavVariants = cva(
  "flex items-center gap-1.5 whitespace-nowrap rounded-full px-3 py-1.5 text-xs",
  {
    variants: {
      active: {
        true: "bg-brand-soft text-brand",
        false: "text-muted-foreground hover:text-foreground",
      },
    },
    defaultVariants: { active: false },
  },
);

const SOURCE_LABEL = { live: "实时数据", imported: "已导入", demo: "演示数据" } as const;

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  const { state } = useHub();
  const page = NAV.find((n) => n.to === pathname) ?? NAV[0];
  const sessionCount = state.data.sessions.length;

  return (
    <div className="app-backdrop min-h-screen">
      <div className="pointer-events-none fixed inset-0 grid-veil" />
      <div className="relative mx-auto flex min-h-screen w-full max-w-[1680px]">
        <aside className="sticky top-0 hidden h-screen w-[248px] shrink-0 flex-col border-r border-border/70 bg-sidebar/60 backdrop-blur-xl lg:flex">
          <div className="flex items-center gap-3 px-5 pb-5 pt-6">
            <div className="grid size-10 place-items-center rounded-xl bg-gradient-to-br from-brand to-qoder text-background shadow-lg shadow-brand/25">
              <Sparkles className="size-5" />
            </div>
            <div className="leading-tight">
              <div className="text-[15px] font-semibold tracking-tight">AIHub</div>
              <div className="text-[11px] text-muted-foreground">共享工作区</div>
            </div>
          </div>

          <nav className="space-y-1 px-3">
            {NAV.map((item) => {
              const active = pathname === item.to;
              return (
                <Link key={item.to} to={item.to} className={navItemVariants({ active })}>
                  <item.icon className={navIconVariants({ active })} />
                  <span className="flex-1">{item.label}</span>
                  {active && <span className="size-1.5 rounded-full bg-brand" />}
                </Link>
              );
            })}
          </nav>

          <div className="mx-4 mt-6 rounded-xl border border-border/70 bg-surface p-3.5">
            <div className="mb-2 flex items-center gap-2 text-[11px] uppercase tracking-wider text-muted-foreground">
              <FolderOpen className="size-3.5" /> 工作区
            </div>
            <div className="break-all font-mono text-[11px] leading-relaxed text-foreground/80">
              {state.data.workspace}
            </div>
            <div className="mt-3 flex items-center justify-between border-t border-border/60 pt-2.5 text-[11px] text-muted-foreground">
              <span>{sessionCount} 个会话</span>
              <span className={sourceVariants({ source: state.source })}>
                <span className="size-1.5 rounded-full bg-current" />
                {SOURCE_LABEL[state.source]}
              </span>
            </div>
          </div>

          <div className="mt-auto space-y-3 px-5 py-5 text-[11px] text-muted-foreground">
            <div className="flex items-center gap-2">
              <Database className="size-3.5" />
              <span>SQLite 双向同步 · 附件归档</span>
            </div>
            <a
              href="https://github.com/kkt446/AIHub"
              target="_blank"
              rel="noreferrer"
              className="flex items-center gap-2 transition-colors hover:text-foreground"
            >
              <Github className="size-3.5" />
              <span>kkt446/AIHub · v0.2</span>
            </a>
          </div>
        </aside>

        <div className="flex min-w-0 flex-1 flex-col">
          <header className="sticky top-0 z-20 border-b border-border/60 bg-background/65 backdrop-blur-xl">
            <div className="flex flex-wrap items-center gap-3 px-5 py-3.5 sm:px-7">
              <div className="min-w-0 flex-1">
                <h1 className="truncate text-[17px] font-semibold tracking-tight">{page.title}</h1>
                <p className="truncate text-xs text-muted-foreground">{page.subtitle}</p>
              </div>
              <div className="flex items-center gap-2">
                <span className={cn(sourceVariants({ source: state.source }), "hidden sm:inline-flex")}>
                  <span className="size-1.5 rounded-full bg-current" />
                  {SOURCE_LABEL[state.source]}
                </span>
                <ImportDialog />
              </div>
            </div>
            <nav className="flex gap-1 overflow-x-auto px-4 pb-2 lg:hidden">
              {NAV.map((item) => (
                <Link
                  key={item.to}
                  to={item.to}
                  className={mobileNavVariants({ active: pathname === item.to })}
                >
                  <item.icon className="size-3.5" />
                  {item.label}
                </Link>
              ))}
            </nav>
          </header>

          <main className="flex-1">{children}</main>
        </div>
      </div>
      <Toaster position="bottom-right" theme="dark" />
    </div>
  );
}
