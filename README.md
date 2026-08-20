# Quick Notes — 快捷便签

一款面向 Windows 10/11 的极简桌面便签：只有一页连续内容，随手记录；不用时收缩成置顶小条，鼠标移入后展开。

> 项目正在开发中。目前尚未发布可安装版本；首个 MSIX 安装包完成验证后会在 [Releases](https://github.com/GrugHan/Quick-Notes/releases) 提供下载。

## 下载与使用

- [安装与使用指南](docs/USER_GUIDE.zh-CN.md)
- [版本下载页面](https://github.com/GrugHan/Quick-Notes/releases)
- [问题反馈](https://github.com/GrugHan/Quick-Notes/issues)

## 设计目标

- 只有一个连续便签页面，不做笔记本、标签、分类或多便签。
- 点击“新建当天便签”才创建日期区块，绝不在零点自动打断记录。
- 历史内容默认全部展开；当前日期区块可手动折叠。
- 待办方框勾选后保留在原处并显示删除线；悬停三秒显示创建和完成时间。
- 始终置顶；可收缩为细小横条，鼠标移入展开。
- 默认离线可用，后续可选择用 Microsoft 帐户同步到 OneDrive 应用专属目录。

## 当前进度

- 已完成：.NET 8/WPF 项目基础、便签/日期块/待办领域模型、SQLite 本地存储、连续编辑界面。
- 开发中：顶部收缩横条、设置与快捷键、OneDrive 同步、MSIX 安装包。
- 尚未提供：可下载安装文件。请不要将当前源码分支视为稳定发行版。

## 计划功能

- `Ctrl + Alt + N` 快速展开或收起便签。
- 底部极简工具栏：新建当天、加粗、字体增减、待办、撤销、重做。
- 可选的随 Windows 启动、浅/深色主题和字体大小。
- 同步状态提示：已保存、同步中、离线、需要处理。

## 开发环境

需要 Windows 10/11 和 .NET 8 SDK。克隆后运行：

```powershell
dotnet test BianQian.sln
dotnet build BianQian.sln -c Release
```

OneDrive 同步需要单独配置 Microsoft Entra 公共客户端；其客户端 ID 只保存在本地的 `appsettings.local.json`，不会提交到仓库。

## 参与方式

欢迎通过 [Issues](https://github.com/GrugHan/Quick-Notes/issues) 提交问题或建议。项目优先保持“少功能、低打扰、可立即记录”的原则。

## 发布与下载

正式版本会在 [Releases](https://github.com/GrugHan/Quick-Notes/releases) 发布，并附带：

- MSIX 安装文件
- 版本说明与已知问题
- SHA-256 校验值

当前没有发行版，是因为安装包尚未构建完成。
