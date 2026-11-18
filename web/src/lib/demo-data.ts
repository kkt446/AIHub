import type { HubData } from "./hub";

const t = (iso: string) => iso;

export const DEMO_DATA: HubData = {
  workspace: "C:\\Users\\xzq\\Documents\\Qoder\\2026-11-17\\86854597",
  generatedAt: t("2026-11-18T10:48:00+08:00"),
  sessions: [
    {
      key: "qoder-86854597",
      client: "qoder",
      title: "AIHub 双向数据库同步收尾",
      updated: t("2026-11-18T10:42:00+08:00"),
      messages: [
        {
          id: "m1",
          role: "user",
          text: "你这就是没同步，我qoder国际发的1，qodercn没有，等会你coding完跟我说下你是怎么写的",
          attachments: [{ name: "sidebar-cn.png", kind: "image" }],
        },
        {
          id: "m2",
          role: "assistant",
          text: "定位到问题不在 ~/.qoder/projects 的 jsonl 缓存，而是任务列表来自 main.sqlite 的 chat_sessions 表。文件镜像不会改变侧边栏，只有写入数据库才会让另一端刷新出会话。",
        },
        { id: "m3", role: "tool_call", tool: "Read", text: "src/AIHub.Core/WorkspaceHub.cs" },
        {
          id: "m4",
          role: "assistant",
          text: "已实现 DbSync：ATTACH 两端 main.sqlite，按 cwd 匹配会话整行复制，并重映射 workspace_id 与模型键（dfmodel ↔ qfmodel）。同步前自动 BackupDatabase，保留 5 份备份。",
        },
        {
          id: "m5",
          role: "tool_call",
          tool: "Bash",
          text: "aihub.exe db-sync --workspace C:\\Users\\xzq\\Documents\\aihub-test\\cn",
        },
        { id: "m6", role: "tool_result", text: "cn->qoder=1 qoder->cn=3 workspace=...\\cn" },
        {
          id: "m7",
          role: "user",
          text: "怎么只读呢，肯定要能改写的，两个agent通用一个工作区",
        },
        {
          id: "m8",
          role: "assistant",
          text: "明白，同步已按可写设计：两端互为源和目标，insert or ignore 保证幂等，并用 .aihub-dbsync.json 记录插入项，db-sync-undo 可精确撤销。真库双向各 4 个会话，integrity_check ok。",
        },
      ],
    },
    {
      key: "qoder-credential",
      client: "qoder",
      title: "CredentialHelperSelector 弹窗排查",
      updated: t("2026-11-18T09:12:00+08:00"),
      messages: [
        { id: "c1", role: "user", text: "这是啥（截图：CredentialHelperSelector）", attachments: [{ name: "dialog.png", kind: "image" }] },
        {
          id: "c2",
          role: "assistant",
          text: "这是 Git for Windows 的凭据选择器：Qoder 内置 git 配置了 credential.helper = helper-selector，匿名推送时被触发。推送改用 http.extraHeader 的 Basic 认证即可绕过，token 不会进入 URL。",
        },
        { id: "c3", role: "user", text: "那这个弹窗会留设置吗？" },
        { id: "c4", role: "assistant", text: "不会。窗口已自行关闭，没有写入凭据，也没有残留进程。后续自动化推送都走 extraHeader。" },
      ],
    },
    {
      key: "cn-number-one",
      client: "qodercn",
      title: "Number one",
      updated: t("2026-11-18T09:40:00+08:00"),
      messages: [
        { id: "n1", role: "user", text: "Number one" },
        { id: "n2", role: "assistant", text: "收到，这个会话已经从国际版同步过来了。任务列表刷新后可直接继续。" },
        { id: "n3", role: "user", text: "继续，把交接摘要也带过来" },
        { id: "n4", role: "assistant", text: "已读取工作区交接：最近进展、上一条用户输入和上一条助手回复都已注入上下文。" },
      ],
    },
    {
      key: "cn-ui-review",
      client: "qodercn",
      title: "秒悟 UI 翻新 · 设计评审",
      updated: t("2026-11-18T10:36:00+08:00"),
      messages: [
        { id: "u1", role: "user", text: "通过 meoo 来把 UI 翻新一下，用最好的模型" },
        { id: "u2", role: "assistant", text: "方案确定：用 Meoo react-design 模板重做 AIHub 控制台，深色玻璃质感 + 三端配色编码，保留会话浏览、消息时间线、交接面板。" },
        { id: "u3", role: "tool_call", tool: "meoo init", text: "react-design" },
        { id: "u4", role: "tool_result", text: "React Design System 模板初始化完成" },
        { id: "u5", role: "assistant", text: "页面结构：会话总览 /、交接内容 /handoff、工作区设置 /settings。全部使用文件路由与 shadcn 组件。" },
      ],
    },
    {
      key: "dsh-probe",
      client: "dsh",
      title: "三端共享工作区探针测试",
      updated: t("2026-11-18T08:20:00+08:00"),
      messages: [
        { id: "p1", role: "user", text: "跑一遍三端共享探针，确认附件也能带过去" },
        { id: "p2", role: "tool_call", tool: "bash", text: "aihub import --workspace ./ws" },
        { id: "p3", role: "tool_result", text: "qoder=4 dsh=3 workspace=./ws" },
        { id: "p4", role: "assistant", text: "三个客户端会话均已进入 hub，附件按会话分目录归档，图片可在详情里直接预览。" },
      ],
    },
    {
      key: "dsh-release",
      client: "dsh",
      title: "GitHub Release v0.1.0 发布",
      updated: t("2026-11-18T07:55:00+08:00"),
      messages: [
        { id: "r1", role: "user", text: "把安装版发到 Release，token 用文件里的" },
        { id: "r2", role: "tool_call", tool: "gh", text: "release create v0.1.0 AIHub-Setup.exe" },
        { id: "r3", role: "tool_result", text: "release created: kkt446/AIHub v0.1.0" },
        { id: "r4", role: "assistant", text: "已发布 v0.1.0，安装包与校验信息都附在 Release 里。" },
      ],
    },
    {
      key: "dsh-attachments",
      client: "dsh",
      title: "附件管线回归测试",
      updated: t("2026-11-17T22:10:00+08:00"),
      messages: [
        { id: "a1", role: "user", text: "批量导入带图会话，检查重名附件" },
        { id: "a2", role: "assistant", text: "同会话内重名按内容哈希去重，跨会话隔离；缺失附件降级为文件名提示，不会中断渲染。" },
        { id: "a3", role: "user", text: "OK，这版可以" },
      ],
    },
  ],
};
