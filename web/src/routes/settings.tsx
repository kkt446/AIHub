import { useEffect, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import {
  ArrowLeftRight,
  Check,
  Database,
  FolderOpen,
  Github,
  RefreshCcw,
  RotateCcw,
  ShieldCheck,
} from "lucide-react";
import { toast } from "sonner";
import { ClientBadge } from "@/components/client-badge";
import { ImportDialog } from "@/components/import-dialog";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { Separator } from "@/components/ui/separator";
import { Switch } from "@/components/ui/switch";
import { useHub } from "@/components/hub-provider";
import { isDesktop, onDesktop, sendDesktop, copyText } from "@/lib/desktop";
import { CLIENT_META, type ClientId } from "@/lib/hub";

export const Route = createFileRoute("/settings")({
  component: SettingsPage,
});

const CLIENTS: ClientId[] = ["qoder", "qodercn", "dsh"];

function SettingsPage() {
  const { state, resetToDemo } = useHub();
  const [enabled, setEnabled] = useState<Record<ClientId, boolean>>({
    qoder: true,
    qodercn: true,
    dsh: true,
  });
  const [pending, setPending] = useState("");

  useEffect(
    () =>
      onDesktop((event) => {
        if (event.type !== "status") return;
        setPending("");
        toast.info(event.message);
      }),
    [],
  );

  const total = state.data.sessions.length;
  const byClient = (client: ClientId) => state.data.sessions.filter((s) => s.client === client).length;

  const run = (type: string, label: string, fallback: string) => {
    if (!isDesktop()) {
      toast.info(fallback);
      return;
    }
    if (!sendDesktop(type)) {
      toast.error("桌面桥未连接");
      return;
    }
    setPending(label);
  };

  const copyPath = () => {
    copyText(state.data.workspace)
      .then((ok) => (ok ? toast.success("工作区路径已复制") : toast.error("复制失败")))
      .catch(() => toast.error("复制失败"));
  };

  return (
    <div className="mx-auto max-w-5xl space-y-4 px-5 py-5 sm:px-7">
      <section className="glass animate-rise rounded-2xl p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
              <FolderOpen className="size-3.5 text-brand" /> 当前工作区
            </div>
            <div className="mt-2 break-all font-mono text-[13px] text-foreground/85">{state.data.workspace}</div>
            <div className="mt-2 text-[11px] text-muted-foreground">
              {total} 个会话 · 数据源：
              {state.source === "live" ? "实时" : state.source === "imported" ? "本地导入" : "演示数据"}
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" size="sm" onClick={copyPath}>复制路径</Button>
            <Button
              variant="outline"
              size="sm"
              disabled={pending.length > 0}
              onClick={() => run("openFolder", "", "桌面端可直接打开工作区文件夹")}
            >
              打开文件夹
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={pending.length > 0}
              onClick={() => run("chooseWorkspace", "切换工作区", "切换工作区仅在桌面端可用")}
            >
              切换工作区
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={pending.length > 0}
              onClick={() => run("refresh", "重新导入", "重新导入仅在桌面端可用")}
            >
              {pending === "重新导入" ? "导入中…" : "重新导入"}
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={pending.length > 0}
              onClick={() => run("hooks", "启用自动交接", "启用自动交接仅在桌面端可用")}
            >
              启用自动交接
            </Button>
            {!isDesktop() && <ImportDialog />}
          </div>
        </div>
      </section>

      <section className="glass animate-rise rounded-2xl p-5">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-sm font-medium">客户端</h2>
            <p className="mt-1 text-xs text-muted-foreground">启用的客户端会参与导入与交接</p>
          </div>
          <span className="inline-flex items-center gap-1.5 rounded-full border border-success/30 bg-success/10 px-2 py-0.5 text-[11px] text-success">
            <ShieldCheck className="size-3" /> 只写工作区，不改客户端配置
          </span>
        </div>
        <div className="mt-4 space-y-2">
          {CLIENTS.map((client) => (
            <div
              key={client}
              className="flex items-center gap-3 rounded-xl border border-border/60 bg-surface px-3.5 py-3"
            >
              <ClientBadge client={client} label={CLIENT_META[client].short} />
              <div className="min-w-0 flex-1">
                <div className="truncate text-[13px] font-medium">{CLIENT_META[client].name}</div>
                <div className="truncate text-[11px] text-muted-foreground">{CLIENT_META[client].description}</div>
              </div>
              <div className="shrink-0 text-right text-[11px] text-muted-foreground">
                {byClient(client)} 个会话
              </div>
              <Switch
                checked={enabled[client]}
                onCheckedChange={(value) => setEnabled((prev) => ({ ...prev, [client]: value }))}
              />
            </div>
          ))}
        </div>
      </section>

      <section className="glass animate-rise rounded-2xl p-5">
        <div className="flex items-center gap-2 text-sm font-medium">
          <Database className="size-4 text-brand" /> 数据库双向同步
        </div>
        <p className="mt-1 text-xs text-muted-foreground">
          两端 main.sqlite 按工作区 cwd 匹配，整行复制会话并重映射 workspace_id 与模型键。
        </p>
        <div className="mt-4 space-y-3">
          {[
            { from: "qoder" as ClientId, to: "qodercn" as ClientId },
            { from: "qodercn" as ClientId, to: "qoder" as ClientId },
          ].map((row) => (
            <div key={`${row.from}-${row.to}`} className="rounded-xl border border-border/60 bg-surface p-3.5">
              <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
                <div className="flex items-center gap-2">
                  <ClientBadge client={row.from} label={CLIENT_META[row.from].short} />
                  <ArrowLeftRight className="size-3.5 text-muted-foreground" />
                  <ClientBadge client={row.to} label={CLIENT_META[row.to].short} />
                </div>
                <span className="inline-flex items-center gap-1 text-[11px] text-success">
                  <Check className="size-3" /> 已同步
                </span>
              </div>
              <Progress
                value={total > 0 ? Math.min(100, (byClient(row.from) / total) * 100) : 0}
                className="mt-3 h-1.5 bg-surface-strong"
              />
              <div className="mt-2 text-[11px] text-muted-foreground">
                {byClient(row.from)} 个会话已写入，同步前自动备份并保留 5 份。
              </div>
            </div>
          ))}
        </div>
        <Separator className="my-4" />
        <div className="flex flex-wrap items-center justify-between gap-2 text-[11px] text-muted-foreground">
          <span>写入后另一端需重启或刷新任务列表可见。</span>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              className="gap-1.5"
              disabled={pending.length > 0}
              onClick={() => run("sync", "立即同步", "在桌面端运行：aihub.exe db-sync")}
            >
              <RefreshCcw className="size-3.5" /> {pending === "立即同步" ? "同步中…" : "立即同步"}
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="gap-1.5"
              disabled={pending.length > 0}
              onClick={() => run("syncUndo", "撤销镜像", "在桌面端运行：aihub.exe db-sync-undo")}
            >
              <RotateCcw className="size-3.5" /> 撤销镜像
            </Button>
          </div>
        </div>
      </section>

      <section className="glass animate-rise rounded-2xl p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="text-sm font-medium">数据源</h2>
            <p className="mt-1 text-xs text-muted-foreground">当前展示 {state.source === "demo" ? "演示数据" : "真实工作区数据"}</p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={resetToDemo}>恢复演示数据</Button>
            <Button asChild variant="outline" size="sm" className="gap-1.5">
              <a href="https://github.com/kkt446/AIHub" target="_blank" rel="noreferrer">
                <Github className="size-3.5" /> 项目仓库
              </a>
            </Button>
          </div>
        </div>
      </section>

      <p className="pb-4 text-center text-[11px] text-muted-foreground">
        AIHub 桌面版 · Qoder / Qoder CN / dsh 共享工作区
      </p>
    </div>
  );
}
