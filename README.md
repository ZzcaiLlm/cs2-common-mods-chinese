# 常用模组汉化：加载器源码

Cities: Skylines II 简体中文汉化插件的独立加载器，对应私人 Paradox Mods 条目 **164827「常用模组汉化」**。

本仓库公开实际运行的 C# 加载器及检查代码，便于审查。原始译文的公开再许可尚未确认，因此词库和编译后的私人包不在本仓库分发。此仓库的 MIT 许可仅适用于加载器代码，不代表对原模组、资产或词库重新授权。

## 加载器做什么

- 使用游戏标准 `Game.Modding.IMod` 和 `Colossal.Localization.MemorySource`。
- 从内嵌语言数据读取简体中文译文。
- 仅向 `zh-HANS` 注册译文，支持语言切换及卸载。
- 支持 Placemaker 设置的运行时本地化别名。
- 对额外设置和资产使用精确键清单，避免覆盖其他模组。
- 不修改模拟系统、道路、建筑、存档或原模组文件。
- 不访问网络，不收集信息，不保存账号或密码。

`Mod.cs` 与社区包编译使用的加载器源文件相同。检查代码覆盖加载顺序、英文回退、语言切换、延迟事件、卸载和设置词条。

## 构建

需要 Windows 自带 .NET Framework 4.x 编译器、已安装的游戏，以及有权使用的本地私人词库项目。无需下载或上传游戏程序集。

在 Windows PowerShell 5.1 中执行：

```powershell
.\Build-Loader.ps1 -GamePath '你的游戏目录' -TranslationProject '你的私人词库项目目录' -Version 1.1.3
.\Test.ps1
```

私人词库项目目录结构：

```text
Lang/zh-HANS*.json                    本地化键到中文的 JSON 对象
Assets/civicbeaver-manifest.json       Packages[].Assets[].Name 精确资产清单
Editor/zh-HANS.overrides.json          可选，本地新增或修订词条
```

脚本只读取该目录，生成 `obj/` 中的数据和 `dist/PlacemakerChinese.dll`，不会发布、安装或更改私人词库。`Lang/`、`Editor/`、`Assets/`、编译产物和日志已加入忽略清单。

社区更新仍由本机原有汉化管理器完成，管理器的账号信息和发布状态不属于此源码仓库。

## 版本与校验

目前设置补译词库为 8356 条，其中包含六个常用模组的175条设置及说明补译。源码本身可公开检查；完整词库及原游戏程序集需由使用者自行提供。

本仓库与原模组作者无关联。感谢 Jack Richards、MiniJ、civicbeaver 及各模组作者制作原作。
