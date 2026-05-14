export type LocalServiceStatus = "starting" | "connected" | "reused" | "failed" | "exited";

export type LocalServiceState = {
  status: LocalServiceStatus;
  apiBaseUrl: string | null;
  port: number | null;
  pid: number | null;
  dataRoot: string | null;
  logDirectory: string | null;
  reason: string | null;
};

export function getLocalServiceStatusLabel(status: LocalServiceStatus) {
  switch (status) {
    case "starting":
      return "启动中";
    case "connected":
      return "已连接";
    case "reused":
      return "复用上次残留进程";
    case "failed":
      return "连接失败";
    case "exited":
      return "后端异常退出";
  }
}
