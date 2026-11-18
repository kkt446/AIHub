import type { LucideIcon } from "lucide-react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const cardVariants = cva("glass animate-rise rounded-2xl p-4", {
  variants: {
    accent: {
      brand: "shadow-[0_18px_60px_-30px_oklch(0.72_0.17_288/0.5)]",
      qoder: "shadow-[0_18px_60px_-30px_oklch(0.72_0.14_255/0.45)]",
      qodercn: "shadow-[0_18px_60px_-30px_oklch(0.76_0.13_165/0.45)]",
      dsh: "shadow-[0_18px_60px_-30px_oklch(0.81_0.13_80/0.45)]",
    },
  },
  defaultVariants: { accent: "brand" },
});

const iconVariants = cva("grid size-9 place-items-center rounded-xl border", {
  variants: {
    accent: {
      brand: "border-brand/25 bg-brand-soft text-brand",
      qoder: "border-qoder/25 bg-qoder/10 text-qoder",
      qodercn: "border-qodercn/25 bg-qodercn/10 text-qodercn",
      dsh: "border-dsh/25 bg-dsh/10 text-dsh",
    },
  },
  defaultVariants: { accent: "brand" },
});

interface StatCardProps extends VariantProps<typeof cardVariants> {
  icon: LucideIcon;
  label: string;
  value: string;
  hint: string;
}

export function StatCard({ icon: Icon, label, value, hint, accent }: StatCardProps) {
  return (
    <div className={cn(cardVariants({ accent }))}>
      <div className="flex items-start justify-between">
        <div>
          <div className="text-xs text-muted-foreground">{label}</div>
          <div className="mt-1.5 text-2xl font-semibold tracking-tight">{value}</div>
        </div>
        <div className={iconVariants({ accent })}>
          <Icon className="size-4.5" />
        </div>
      </div>
      <div className="mt-2 text-[11px] text-muted-foreground">{hint}</div>
    </div>
  );
}
