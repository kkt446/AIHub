import { useRef, useState } from "react";
import { FileJson, Upload } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/textarea";
import { useHub } from "@/components/hub-provider";

export function ImportDialog() {
  const { importJson } = useHub();
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const fileRef = useRef<HTMLInputElement>(null);

  const submit = (raw: string) => {
    const result = importJson(raw);
    if (result.ok) {
      toast.success(result.message);
      setOpen(false);
      setText("");
    } else {
      toast.error(result.message);
    }
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="outline" size="sm" className="gap-1.5">
          <Upload className="size-3.5" />
          导入数据
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>导入工作区数据</DialogTitle>
          <DialogDescription>
            选择 aihub 导出的 JSON，或在下方粘贴内容。数据仅保存在本机浏览器。
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <button
            type="button"
            onClick={() => fileRef.current?.click()}
            className="flex w-full items-center justify-center gap-2 rounded-lg border border-dashed border-border bg-surface px-4 py-6 text-sm text-muted-foreground transition-colors hover:border-brand/40 hover:text-foreground"
          >
            <FileJson className="size-4" />
            选择 JSON 文件
          </button>
          <input
            ref={fileRef}
            type="file"
            accept="application/json,.json"
            className="hidden"
            onChange={(event) => {
              const file = event.target.files?.[0];
              if (!file) return;
              const reader = new FileReader();
              reader.onload = () => submit(String(reader.result ?? ""));
              reader.readAsText(file);
              event.target.value = "";
            }}
          />
          <Textarea
            value={text}
            onChange={(event) => setText(event.target.value)}
            placeholder='{"workspace":"C:\\\\ws","sessions":[...]}'
            className="h-32 font-mono text-xs"
          />
          <Button className="w-full" onClick={() => submit(text)} disabled={!text.trim()}>
            载入数据
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
