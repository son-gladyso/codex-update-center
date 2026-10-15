# Codex Update Center

适用于 Windows 的社区更新工具。识别电脑上的 Codex 安装，准备官方更新，并在确认后更新所选安装。

**独立社区项目，与 OpenAI 无隶属关系。当前为 0.1.1-preview 预览版，Windows 部署兼容性矩阵仍待虚拟机验收。**

[English](docs/README.en.md) · [支持范围](docs/SUPPORT.md) · [验证记录](docs/VALIDATION.md) · [构建与发布](docs/RELEASING.md)

## 使用

1. 从本仓库的 GitHub Releases 下载 `CodexUpdateCenter-*-win-x64.zip`，并核对同一发行的 SHA-256 清单。
2. 解压到可写目录，运行 `CodexUpdateCenter.exe`。发行包已包含 .NET 运行时，无需单独安装 .NET。
3. 一个安装会自动选中；多个安装逐项显示。选择要更新的安装，检查并准备更新。
4. 候选通过验签后，保存工作并退出该安装的 Codex，再点击“确认安装”。工具会重新检测结果。

未找到安装时可打开官方安装说明。自定义 npm 全局目录可用“添加 npm 安装目录”补充；不会递归扫描整个硬盘或读取账号文件。

## 能做什么

| 安装方式 | 识别 | 更新行为 |
| --- | --- | --- |
| 当前 Windows 用户的官方 `OpenAI.Codex` x64 Store 签名安装 | 自动 | 验证官方 MSIX 后，通过 Windows `Add-AppxPackage` 更新 |
| 标准 Node.js/npm 的稳定版 `@openai/codex` 全局安装 | 自动发现 PATH 和常见目录；可补充目录 | 主包和 Windows 原生组件均验证 npm 发布签名与 SHA-512；npm 使用已校验缓存安装，实际启动确认版本 |
| 多个 npm 全局目录、桌面版和 CLI 并存 | 分别显示 | 一次仅更新选中项 |
| 独立二进制、便携版、开发版、WSL、非 x64 | 部分路径可发现 | 显示未支持，使用原安装工具 |

桌面版和 CLI 是独立安装。工具不会把独立 CLI 塞入桌面应用，也不会接管自定义启动脚本。安装发现基于当前用户，不需要电脑上预先存在多个 Codex。

## 默认行为

- 打开窗口后读取本地状态、识别安装。只有单个支持安装会自动选择并准备；安装始终需要确认。
- 计费网络或无法确定网络费用时暂停大包下载，可仅本次放行。HTTP 使用系统代理配置，TLS 验证保持开启。
- CLI 原生组件采用 1 MiB 分段下载；npm 沿用系统有效代理并复用固定版本缓存，减少重复下载。
- C 盘空间足够时优先缓存到 C 盘，并至少保留 20 GiB；空间不足时考虑可识别的内部固定 NTFS 磁盘。原断点绑定磁盘身份。
- 关闭窗口会停止当前下载，保留断点。主动暂停保持暂停；安装过程中等待结果后关闭。
- 不强制结束 Codex，不更改账号配置，不启动后台常驻服务，不自动删除旧缓存。版本不满足升级条件时不降级。
- 下载到其他磁盘并不等于更改 Windows 应用安装位置。Windows 管理桌面应用的部署位置；安装前也检查目标盘空间。

## 构建与验证

需要 Windows x64、PowerShell 7 和 `global.json` 指定的 .NET SDK。

```powershell
dotnet restore CodexUpdateCenter.slnx --locked-mode
dotnet test CodexUpdateCenter.slnx -c Release --no-restore
pwsh -File scripts/build.ps1 -Output artifacts/release-local
```

`build.ps1` 强制执行测试、独立运行时发布和内容清单验证，不覆盖旧发行目录。普通测试不更改本机 Codex 安装。真实 CLI 升级验收必须显式运行，写入全新隔离目录；详见[验证说明](docs/VALIDATION.md)。

## 数据和卸载

状态保存在 `%LOCALAPPDATA%\CodexUpdateCenter\Public`；缓存路径显示在任务页面。程序不读取 `.codex`、认证令牌或聊天数据库，也不上传遥测。npm 和 Windows 自身可能产生安装日志。关闭程序后可删除解压目录；需要清理状态与缓存时先检查对应路径和文件清单。卸载本工具不卸载 Codex。

## 参与项目

欢迎复现明确的问题和提交小范围改进，参见 [CONTRIBUTING.md](CONTRIBUTING.md)。漏洞请按 [SECURITY.md](SECURITY.md) 私密报告，不要在公开 issue 中粘贴令牌、完整本机路径或原始账号日志。

MIT 许可证适用于本工具代码。OpenAI 软件由运行时从官方来源下载，不包含在本发行包中；其许可证和服务条款独立适用。依赖与运行时声明见 [NOTICE.md](NOTICE.md)。
