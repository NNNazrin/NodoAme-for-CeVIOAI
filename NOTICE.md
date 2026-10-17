# NOTICE — 第三方组件

本仓库的 `bin\` 目录随包分发以下第三方组件（均为公开发布的二进制）。
各组件著作权归原作者所有，本仓库仅做原样分发以便开箱即用。
每个组件的**完整版权声明与许可证原文**见 `LICENSES\` 目录下对应文件。

## 随包分发的第三方组件

| 组件 | 用途 | 许可 | 许可全文 |
|---|---|---|---|
| **NodoAme.Core.dll**（NodoAme） | 文本 + 音高 → CeVIO `.ccst` 曲谱 | MIT | `LICENSES\MIT-NodoAme.txt` |
| **LibSasara**（随 NodoAme 使用） | `.ccst` 读写 | MIT | `LICENSES\MIT-LibSasara.txt` |
| **WORLD**（`world.dll`） | 基频分析（Harvest）与频谱包络 | Modified BSD | `LICENSES\BSD-WORLD.txt` |
| **Open JTalk**（`openjtalk.dll`） | 日语文本 → 音素 | Modified BSD | `LICENSES\BSD-OpenJTalk.txt` |
| **Open JTalk 词典**（`dic\`） | 日语词典数据 | 见词典内 `COPYING` | `LICENSES\OpenJTalk-dic-COPYING.txt` |
| **SharpOpenJTalk** | Open JTalk 的 .NET 封装 | MIT | `LICENSES\MIT-SharpOpenJTalk.txt` |
| **NAudio**（Core/WinMM/Wasapi/Asio/Midi/WinForms） | 音频设备与 WAV 处理 | MIT | `LICENSES\MIT-NAudio.txt` |
| **MathNet.Numerics** | 数值计算 | MIT | `LICENSES\MIT-MathNet.txt` |
| **NLog** | 日志 | BSD-3-Clause | `LICENSES\BSD-NLog.txt` |
| **WanaKanaNet** | 假名/罗马字转换 | MIT | `LICENSES\MIT-WanaKanaNet.txt` |
| **DotnetWorld** | WORLD 的 .NET 封装 | MIT | `LICENSES\MIT-DotnetWorld.txt` |
| **Microsoft.*** / **System.*** 系列（Bcl.AsyncInterfaces、Buffers、IO.Pipelines、Memory、Numerics.Vectors、Resources.Extensions、Runtime.CompilerServices.Unsafe、Security.AccessControl、Security.Principal.Windows、Text.Encodings.Web、Text.Json、Threading.Tasks.Extensions、Win32.Registry） | .NET 兼容包 | MIT | `LICENSES\MIT-DotNet.txt` |

> `LICENSES\` 目录下每个文件均包含该组件**完整的版权行（Copyright (c) …）与许可证原文**。若某组件在你下载的版本中版权年份/作者与本仓库记录不一致，以组件原始发布页为准。

## 本项目自行编译的程序

以下程序由本项目源码编译，以 MIT 许可发布（见根目录 `LICENSE`）：

- `cevio-NodoAme-talk.exe`
- `bin\NodoAmeApi.exe`
- `bin\cevio-render2.exe`

`cevio-render2.exe` 编译时会引用**用户本机** CeVIO AI 安装目录中的 DLL 进行类型绑定；**编译产物不包含任何 CeVIO 代码或数据**，运行时才从用户本机安装目录加载。

## 不包含的内容

- 不含 CeVIO AI 的任何可执行文件、DLL、界面资源。
- 不含任何声库文件（`.tsnvoice`）或经声库渲染出的音频样本。

如需使用，请自行安装并授权 CeVIO AI 与其歌曲声库；本工具在运行时从**你本机**的安装目录加载这些组件。
