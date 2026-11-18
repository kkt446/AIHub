import type { HubData } from "@/lib/hub";

interface WebViewHost {
  postMessage(message: unknown): void;
  addEventListener(type: "message", listener: (event: { data: unknown }) => void): void;
  removeEventListener(type: "message", listener: (event: { data: unknown }) => void): void;
}

declare global {
  interface Window {
    chrome?: { webview?: WebViewHost };
  }
}

export type DesktopEvent =
  | { type: "data"; payload: HubData | string }
  | { type: "status"; message: string };

export function isDesktop(): boolean {
  return Boolean(window.chrome?.webview);
}

export function sendDesktop(type: string): boolean {
  const webview = window.chrome?.webview;
  if (!webview) return false;
  webview.postMessage({ type });
  return true;
}

export function onDesktop(handler: (event: DesktopEvent) => void): () => void {
  const webview = window.chrome?.webview;
  if (!webview) return () => undefined;
  const listener = (event: { data: unknown }) => {
    const data = event.data as { type?: unknown } | null;
    if (!data || typeof data !== "object" || typeof data.type !== "string") return;
    if (data.type === "data" || data.type === "status") handler(data as DesktopEvent);
  };
  webview.addEventListener("message", listener);
  return () => webview.removeEventListener("message", listener);
}
