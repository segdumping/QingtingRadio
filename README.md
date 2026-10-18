# QingtingRadio · 蜻蜓电台

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-UWP%20%C2%B7%20Windows%2010-blueviolet)](https://learn.microsoft.com/windows/uwp/)
[![IDE](https://img.shields.io/badge/IDE-Visual%20Studio%202022-5C2D91)](https://visualstudio.microsoft.com/)

基于 UWP（Universal Windows Platform）的蜻蜓 FM 第三方电台客户端，支持电台直播、节目回听、专辑点播、下载与自定义电台。

## ✨ 功能特性

- 📻 **电台直播**：分类浏览电台，收听实时直播
- ⏪ **节目回听**：前天 / 昨天 / 今天三页签的电台节目单回听
- 💿 **专辑点播**：专辑详情与节目列表，逐集播放
- 🔍 **搜索**：支持电台 / 节目 / 专辑三种类型，搜索历史记录
- ⭐ **收藏与历史**：电台收藏、回听节目收藏、收听历史（含进度记忆）
- ⬇️ **下载**：节目离线下载（后台传输，支持暂停 / 取消 / 删除）
- 🎛️ **自定义电台**：添加任意 `http(s)://` 音频流地址作为个人电台
- 🌗 **主题自动切换**：按设定时段在深浅主题间定时切换
- 🔈 **系统集成**：SMTC 系统媒体控制（锁屏 / 媒体浮窗）与后台播放

## 🛠️ 环境要求

| 项目 | 要求 |
| --- | --- |
| IDE | Visual Studio 2022（勾选 **通用 Windows 平台开发** 工作负载） |
| Windows SDK | 10.0.22621 |
| 目标系统 | Windows 10（最低版本 10.0.10586，即 1511+） |

## 🚀 构建与运行

```powershell
git clone https://github.com/segdumping/QingtingRadio.git
```

1. 用 Visual Studio 2022 打开根目录下的 `QingtingRadio.sln`
2. 将解决方案平台设置为 **x86** 或 **x64**（ARM 用于打包）
3. 还原 NuGet 包后生成解决方案
4. 按 **F5** 部署并运行

### 首次部署：签名证书

仓库**不包含**任何签名证书。本地部署时二选一：

- 在 Visual Studio 中：*项目属性 › 打包 › 选择证书 › 创建测试证书*；
- 或在本机的 `QingtingRadio.csproj.user` 文件（已被 git 忽略）中自行配置 `PackageCertificateKeyFile`。

## 📦 私有 API 程序集说明

蜻蜓 FM 的接口地址与请求签名算法（含密钥）**未随源码公开**，统一封装在预编译程序集中：

```
libs/QingtingRadio.Core.dll
```

- 命名空间：`QingtingRadio.Https`
- 对外类型：`ServiceURLs`（接口地址模板）、`ApiSigner`（请求签名）
- 主项目 `QingtingRadio/QingtingRadio.csproj` 已引用它，克隆后开箱即可编译
- 如需新增或修改接口：用相同命名空间重新编译一份 `QingtingRadio.Core.dll`，覆盖 `libs/` 下的同名文件即可，无需改动主工程

## 📁 项目结构

```
QingtingRadio/
├── DataModels/          # 数据模型：电台 / 节目 / 专辑 / 搜索结果 / 下载条目等
├── Https/               # HTTP 请求基类与业务接口封装
├── Services/            # 全局播放服务（MediaPlayer 单例、SMTC 按钮接管）
├── Tools/               # 工具类：下载管理、文件缓存、日志、IP 定位、版本更新等
├── ViewModels/          # 各页面的视图模型（含增量加载列表）
├── Styles/              # 通用样式资源
├── Assets/              # 应用图标、启动画面与本地资源文件
├── Properties/          # 程序集信息与 .NET Native 描述文件
├── *.xaml / *.cs        # 页面与用户控件
├── App.xaml(.cs)        # 应用入口：异常兜底、启动恢复、挂起处理
└── Package.appxmanifest # 应用清单
libs/
└── QingtingRadio.Core.dll   # 私有 API 程序集（见上节）
```

## ⚠️ 免责声明

- 本项目为蜻蜓 FM 的**第三方客户端**，仅供学习与技术交流，与蜻蜓 FM 官方无任何关联；
- 应用内所有电台与音频内容均来自蜻蜓 FM 平台，版权归原平台及内容方所有；
- 请勿将本项目或其衍生作品用于任何商业用途；如有侵权，请联系删除。

## 🤝 参与贡献

欢迎提交 Issue 与 Pull Request：

1. Fork 本仓库并新建分支
2. 修改代码并确保本地编译通过（x86 / x64 均可）
3. 提交 PR，说明改动内容与动机

## 📄 许可证

本项目基于 [MIT License](LICENSE) 开源。
