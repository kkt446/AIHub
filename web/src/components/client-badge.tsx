import type { ClientId } from "@/lib/hub";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-[11px] font-medium leading-none",
  {
    variants: {
      client: {
        qoder: "border-qoder/30 bg-qoder/10 text-qoder",
        qodercn: "border-qodercn/30 bg-qodercn/10 text-qodercn",
        dsh: "border-dsh/30 bg-dsh/10 text-dsh",
      },
    },
    defaultVariants: { client: "qoder" },
  },
);

interface ClientBadgeProps extends VariantProps<typeof badgeVariants> {
  client: ClientId;
  label?: string;
  className?: string;
}

export function ClientBadge({ client, label, className }: ClientBadgeProps) {
  return (
    <span className={cn(badgeVariants({ client }), className)}>
      <span className="size-1.5 rounded-full bg-current" />
      {label ?? (client === "qoder" ? "Qoder" : client === "qodercn" ? "Qoder CN" : "dsh")}
    </span>
  );
}
