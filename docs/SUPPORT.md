# 支持与故障处理

## 支持条件

发行包面向 Windows 10 2004+ / Windows 11 x64。ARM64 和 32 位系统明确停止，不使用 x64 模拟运行结果冒充原生支持。Windows PowerShell 5.1、Appx 部署组件和正常的系统信任服务需要可用。

官方桌面安装必须匹配 `OpenAI.Codex`、官方发布者、x64 架构、Store 签名类型，并且不是开发模式。更新调用 Windows 部署服务，不改写 WindowsApps 权限，不修改应用内核，不处理其他 Windows 用户的安装。

npm 支持标准 Node.js 安装中可定位的 `node.exe` 与 `npm-cli.js`，以及稳定版本的 `@openai/codex`。便携 Node、链接目录、版本管理器、多套 Node 的自定义映射尚未自动适配。检测到不受支持的安装会显示原因，不猜测可执行脚本。

## 常见情况

| 情况 | 处理 |
| --- | --- |
| 没找到 Codex | 打开官方安装说明；npm 自定义前缀可以手动添加 |
| 多个安装 | 分别选择；更新一个不会自动修改其他项 |
| 网络费用未知 | 检查网络状态；愿意使用当前流量时勾选“仅本次允许” |
| 官方包在续传期间变化 | 保留旧文件，重新准备；不拼接不同版本的字节 |
| 缓存磁盘离线或盘符复用 | 接回原磁盘，或保留旧文件后重新准备 |
| 安装失败/进程中断 | 重新扫描版本；查看 Windows AppXDeployment 日志或独立 npm 缓存下的 `_logs` |
| 当前版本已达到或超过候选 | 不降级；需要重新查询时选择“保留旧文件，重新准备” |
| Codex 仍在运行 | 保存工作并正常退出所选安装；无法确认进程归属时停止安装 |
| 企业策略阻止部署 | 联系管理员使用组织批准的部署流程；本工具不修改策略 |

npm 安装失败可能留下不完整的包目录。工具记录“需要复核”，不会承诺已经回滚；请使用 npm 的正常修复流程。Windows 更新是否成功以重新查询到的版本为准。没有虚拟机验收前，不宣称覆盖所有依赖、企业策略、标准用户或 Store 场景。

原生组件虽已支持分段续传，npm 自身仍可能请求较大的完整版本索引。部分代理环境下该请求会连接重置；工具会停止并保留缓存，不会跳过 TLS 或伪报成功。保存日志后检查网络，再重新扫描和准备。最终真实升级验收复用了成功取得的官方索引缓存，未把失败的冷缓存联网测试算作通过。

## 数据边界

任务文件只记录安装路径、版本、公开包元数据、哈希和状态。不会读取认证令牌或聊天内容。发送问题前请删除本机路径中的用户名；不要直接附上整个状态目录。

## 官方依据

- [Windows 安装说明](https://learn.chatgpt.com/docs/windows/windows-app)
- [官方 Windows 部署与 MSIX 来源](https://learn.chatgpt.com/docs/enterprise/windows-deployment)
- [官方更新策略](https://learn.chatgpt.com/docs/enterprise/manage-app-updates)
- [Codex CLI](https://learn.chatgpt.com/docs/codex/cli)
- [Microsoft Add-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/add-appxpackage)

来源于 2026-10-15 核对。官方地址或包身份变更时，应更新适配器并重新验收，而不是放宽来源校验。
