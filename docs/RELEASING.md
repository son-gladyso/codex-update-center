# 构建与发布

在 Windows x64 安装 PowerShell 7 和 global.json 指定的 SDK，然后运行：

```powershell
pwsh -File scripts/build.ps1 -Output artifacts/release-local
```

输出目录必须不存在。脚本执行锁定依赖恢复、测试、独立运行时发布、运行时许可证收集、发行内容检查、文件清单及 ZIP 的 SHA-256 清单。任何一步失败都不得发布输出。源码包只包含此公开项目的源文件，不包含 artifacts、bin、obj、账号状态或本机版。

使用 `pwsh -File scripts/package-source.ps1` 生成可单独上传到 GitHub 的源码 ZIP。`scripts/verify-release.ps1 -Directory <解压目录>` 会校验内容边界及 manifest.json 的逐文件哈希。审核记录见 [REVIEW.md](REVIEW.md)。

公开仓库以此目录作为根目录；不要上传其父工作区。main 和 PR 会构建并上传 Actions 工件。推送版本标签时只生成 draft prerelease，维护者审核 ZIP、清单、说明和测试记录后再公开。首次运行 GitHub 工作流本身仍需在目标仓库验证。

当前为 0.1.1-preview 预览版。未完成 Windows 部署虚拟机矩阵前不要宣称稳定版。工具本体当前没有商业 Authenticode 签名；系统可能显示未知发布者。不要把本机私人证书或私钥放入公开仓库，不要指导用户关闭系统安全功能。下载包的 SHA-256 用于完整性核对，不能替代可信发布渠道。

维护者获得代码签名证书后，可在隔离 CI 环境中添加签名步骤；签名必须先于清单和 ZIP 生成。本工具验证官方 Codex 包的流程独立于自身发行签名。

官方 npm 更新方式参考 [OpenAI 官方说明](https://developers.openai.com/cookbook/examples/codex/using_goals_in_codex)。本工具固定到已查明的稳定版本，用 npm 的全局安装机制处理所选目录；CLI 原生依赖必须成功安装，且实际运行 --version 必须匹配。

## 维护者发布顺序

1. 完成源码审计、回归和审核记录，更新 CHANGELOG；Directory.Build.props 是版本唯一来源。
2. 用全新输出目录执行 build.ps1。该门禁运行发布工具负向检查、锁定恢复和全量程序测试，随后生成运行包、源码包及摘要。失败产物保留诊断，不发布。
3. 将运行 ZIP 解压到新目录，执行 verify-release.ps1；用隔离数据执行 --preview，使用 --discover 只读核对安装发现。将源码 ZIP 解压到另一个新目录，执行 locked restore 和构建，确保无需父工作区。
4. 只提交经过 audit-source.ps1 审计的公开项目文件；不从父工作区初始化仓库，不加入 artifacts。启用私密漏洞报告，核对 Actions 权限及分支规则。
5. 推送 main，等待实际 Windows CI 成功后再创建与版本完全匹配的 v<Version> 标签。标签工作流创建草稿预发行；审核生成资产和 SHA256SUMS 后才公开发布。
6. 失败时保留日志并提交修复；不要挪动已发布标签或覆盖已有发行。未运行的 CI、未完成的 Windows 部署矩阵不能写成通过。

发布脚本可单独验证：pwsh -File scripts/test-release-tools.ps1。它仅创建 artifacts 下的人工文件和源码副本，不安装 Codex。依赖更新由 Dependabot 提议，审核锁文件与固定 Actions 提交后再合入。