import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { DEMO_DATA } from "@/lib/demo-data";
import { isHubData, normalizeSession, type HubData, type HubState } from "@/lib/hub";

const STORAGE_KEY = "aihub.hubData";

declare global {
  interface Window {
    __AIHUB_DATA__?: HubData;
    aihubReceive?: (payload: string | HubData) => string;
  }
}

interface HubContextValue {
  state: HubState;
  importJson: (text: string) => { ok: boolean; message: string };
  resetToDemo: () => void;
}

const HubContext = createContext<HubContextValue | null>(null);

function withNormalized(data: HubData): HubData {
  return {
    workspace: data.workspace,
    generatedAt: data.generatedAt || new Date().toISOString(),
    sessions: data.sessions
      .map(normalizeSession)
      .sort((a, b) => new Date(b.updated).getTime() - new Date(a.updated).getTime()),
  };
}

export function HubProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<HubState>({ data: DEMO_DATA, source: "demo" });

  useEffect(() => {
    if (window.__AIHUB_DATA__ && isHubData(window.__AIHUB_DATA__)) {
      setState({ data: withNormalized(window.__AIHUB_DATA__), source: "live" });
      return;
    }
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored);
        if (isHubData(parsed)) {
          setState({ data: withNormalized(parsed), source: "imported" });
          return;
        }
      }
    } catch {
      localStorage.removeItem(STORAGE_KEY);
    }
    fetch("./data.json")
      .then((res) => (res.ok ? res.json() : null))
      .then((json) => {
        if (isHubData(json)) setState({ data: withNormalized(json), source: "live" });
      })
      .catch(() => undefined);
  }, []);

  const importJson = useCallback((text: string) => {
    try {
      const parsed = JSON.parse(text);
      if (!isHubData(parsed)) {
        return { ok: false, message: "JSON 缺少 workspace 或 sessions 字段" };
      }
      const next = withNormalized(parsed);
      setState({ data: next, source: "imported" });
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
      return { ok: true, message: `已载入 ${next.sessions.length} 个会话` };
    } catch {
      return { ok: false, message: "不是合法的 JSON 文件" };
    }
  }, []);

  const resetToDemo = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY);
    setState({ data: DEMO_DATA, source: "demo" });
  }, []);

  useEffect(() => {
    window.aihubReceive = (payload) => {
      if (typeof payload === "string") return importJson(payload).message;
      if (isHubData(payload)) {
        setState({ data: withNormalized(payload), source: "live" });
        return `已接收 ${payload.sessions.length} 个会话`;
      }
      return "数据格式不正确";
    };
    return () => {
      delete window.aihubReceive;
    };
  }, [importJson]);

  const value = useMemo(() => ({ state, importJson, resetToDemo }), [state, importJson, resetToDemo]);
  return <HubContext.Provider value={value}>{children}</HubContext.Provider>;
}

export function useHub(): HubContextValue {
  const ctx = useContext(HubContext);
  if (!ctx) throw new Error("useHub must be used inside HubProvider");
  return ctx;
}
