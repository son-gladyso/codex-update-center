# Codex Update Center

### 更新 Codex，少一点折腾。

**一个窗口，找到安装、准备官方更新、确认安装结果。** 为 Windows 上不想反复查安装目录、重下大包和排查“更新了却不能用”的用户而做。

[![Windows build](https://github.com/son-gladyso/codex-update-center/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/son-gladyso/codex-update-center/actions/workflows/build.yml)

[下载 Windows 预览版](https://github.com/son-gladyso/codex-update-center/releases) · [30 秒了解使用方式](#开始使用) · [English](docs/README.en.md) · [报告问题](https://github.com/son-gladyso/codex-update-center/issues/new/choose)

Windows x64 · 解压运行，自带 .NET · MIT 开源 · 独立社区项目，与 OpenAI 无隶属关系

![Codex 更新中心：安装列表、候选验证、网络费用与确认安装入口](docs/images/update-center.png)

*实际程序界面，使用人工示例数据。当前界面为中文；截图不代表已对真实账号执行升级。*

## 这些麻烦，交给一个窗口

| 你遇到的问题 | 工具如何处理 |
| --- | --- |
| “装的是桌面版还是 CLI？应该更新哪一个？” | 识别当前用户的官方桌面安装和标准 npm CLI；多个安装分别列出，只更新你选中的一个。 |
| “网络断了，又要从头下载？” | 保留下载断点；CLI 大型原生组件按 1 MiB 分段下载，已取得的官方缓存验证后可复用。 |
| “包下好了，但我还在工作。” | 准备与安装分开；安装前需要你确认，并检查所选 Codex 是否仍在运行，不强行结束工作进程。 |
| “命令说成功了，实际还是打不开。” | 安装后重新识别目标版本；CLI 还要实际运行版本查询，匹配才报告成功。 |
| “手机热点不想被后台下载吃光。” | 计费或费用未知网络默认阻止大包下载，npm 安装前再次检查；可以明确放行本次下载。 |
| “C 盘快满了，缓存会放哪里？” | 先检查预算和安全余量；C 盘足够时优先使用，不足时考虑符合条件的内部固定盘，并显示缓存位置。 |

**安装包来自官方来源，安装前验证签名和内容。** 工具不需要 API key，不读取账号令牌或聊天数据库，也不上传遥测。

## 开始使用

1. 在 [Releases](https://github.com/son-gladyso/codex-update-center/releases) 下载 `CodexUpdateCenter-*-win-x64.zip`，按同页 `SHA256SUMS.txt` 核对文件。
2. 解压到可写目录，打开 `CodexUpdateCenter.exe`，不需要另外安装 .NET。
3. 选择要更新的安装，点击 **检查并准备更新**。只有一个支持安装时，默认自动选中并准备；可取消“打开后自动检查并准备”。
4. 等候选验证完成，保存工作并正常退出所选 Codex，再点击 **确认安装**。完成后查看重新检测的版本。

找不到自定义 npm 目录时，使用 **添加 npm 安装目录**。首次还没装 Codex 时，窗口提供官方安装说明。

> 当前是 **社区预览版**，适合愿意反馈问题的 Windows 用户。工具本体尚无商业 Authenticode 签名；不要关闭系统安全功能来运行。Windows 部署兼容性矩阵尚未完成，具体范围见[验证记录](docs/VALIDATION.md)。

## 哪些安装可以更新？

| 安装方式 | 支持情况 |
| --- | --- |
| 当前用户的官方 Store 签名 `OpenAI.Codex` x64 桌面安装 | 识别、验签官方 MSIX，通过 Windows 部署服务更新。实际 Windows 部署仍待完整验收。 |
| 标准 Node.js/npm 的稳定版 `@openai/codex` 全局安装 | 识别、验证主包及 Windows 原生组件、确认后更新；已做隔离目录真实升级验证。 |
| 多个 npm 全局目录、桌面版与 CLI 并存 | 分别显示，一次更新一项；可手动添加 npm 前缀。 |
| 便携复制版、自定义多实例启动器、WSL、ARM64、链接式 Node 版本管理器 | 不自动接管；使用原安装工具。 |

桌面版和 CLI 分别维护，工具不会把独立 CLI 塞进桌面应用。下载缓存放在其他盘，也不会改变 Windows 管理的桌面应用安装位置。

## 出问题时，结果说清楚

- 下载暂停或窗口关闭后保留断点；用户主动暂停后不自行恢复下载。
- 安装失败、中断或无法确认版本时提示重新检查，**不会把“安装器返回了”当作成功**。
- npm 安装不是原子事务，失败后可能需要 npm 修复；当前不承诺自动回滚。
- 不强制结束 Codex、不降级到较旧候选、不迁移账号和聊天，不启动后台常驻服务。
- 网络采用系统有效代理，保留 TLS 验证；不控制 VPN 节点或承诺所有网络都能高速下载。npm 索引请求在部分代理下仍可能失败，缓存和失败记录会保留。

更多情况见[支持与故障处理](docs/SUPPORT.md)。状态位于 `%LOCALAPPDATA%\CodexUpdateCenter\Public`，缓存位置可在任务界面查看。卸载本工具不卸载 Codex；清理前先核对状态和缓存文件。

## 我们如何确认它能用

- **81 项程序测试**，覆盖更新目标变化、无效签名、下载续传、并发锁、暂停与安装费用门禁。
- **10 项发布工具检查**，覆盖包篡改、隐藏文件遗漏、越界路径和源码隐私边界。
- 在新隔离目录实际完成 **CLI 0.161.0 → 0.162.1**，执行新 CLI 验证版本，原有安装清单保持一致。
- 官方桌面包已通过 Windows 验签及全部块哈希检查；源码包独立构建、运行包解压启动均已验证。

真实升级测试复用了官方包与 npm 索引缓存，**不等同冷缓存联网通过**；包验签不等同真实 Windows 部署通过。最新自动化运行见 [GitHub Actions](https://github.com/son-gladyso/codex-update-center/actions)，详细范围及待验收项见 [VALIDATION](docs/VALIDATION.md)。

## 开发与参与

需要 Windows x64、PowerShell 7 和 `global.json` 指定的 SDK：

```powershell
pwsh -File scripts/build.ps1 -Output artifacts/release-local
```

发布门禁会审计源码、锁定恢复依赖、运行测试、生成自带运行时的包并校验清单。输出目录必须是新目录；普通测试不会安装或修改 Codex。

[架构说明](docs/ARCHITECTURE.md) · [贡献指南](CONTRIBUTING.md) · [发布流程](docs/RELEASING.md) · [更新记录](CHANGELOG.md) · [安全政策](SECURITY.md)

**最有帮助的反馈**：你的安装方式、Windows 版本、卡在哪一步，以及期望看到什么结果。请用[问题或建议模板](https://github.com/son-gladyso/codex-update-center/issues/new/choose)，不要贴令牌、聊天内容或原始账号日志。欢迎提交能复现问题的测试，以及让失败提示更清楚的小改进。

MIT 许可证适用于本工具代码。发行包不包含 Codex 软件；运行时从官方来源下载，相关软件的许可证与条款独立适用。依赖声明见 [NOTICE](NOTICE.md)。
