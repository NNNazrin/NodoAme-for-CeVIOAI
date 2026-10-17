# NOTICE — 第三方组件

本仓库的 `bin\` 目录随包分发以下第三方组件（均为公开发布的二进制），它们各自的许可证与著作权归原作者所有，本仓库仅做原样分发以便开箱即用。

| 组件 | 用途 | 许可 |
|---|---|---|
| **NodoAme.Core.dll** / `NodoAmeApi.exe` | 文本 + 音高 → CeVIO `.ccst` 曲谱（本仓库对其的封装程序由本项目编写） | MIT（NodoAme, © its authors） |
| **LibSasara**（随 NodoAme 使用） | `.ccst` 读写 | MIT |
| **WORLD** (`world.dll`) | 基频分析（Harvest）与频谱包络 | Modified BSD |
| **Open JTalk** (`openjtalk.dll`) + **SharpOpenJTalk** | 日语文本 → 音素 | Modified BSD / MIT |
| **NAudio**（Core/WinMM/Wasapi/Asio/Midi/WinForms） | 音频设备与 WAV 处理 | MIT |
| **MathNet.Numerics** | 数值计算 | MIT |
| **NLog** | 日志 | BSD-3-Clause |
| **WanaKanaNet** | 假名/罗马字转换 | MIT |
| **DotnetWorld** | WORLD 的 .NET 封装 | MIT |
| **Microsoft.Bcl.AsyncInterfaces / System.Buffers / System.IO.Pipelines / System.Memory / System.Numerics.Vectors / System.Resources.Extensions / System.Runtime.CompilerServices.Unsafe / System.Security.AccessControl / System.Security.Principal.Windows / System.Text.Encodings.Web / System.Text.Json / System.Threading.Tasks.Extensions / Microsoft.Win32.Registry** | .NET 兼容包 | MIT |

`bin\cevio-render2.exe`、`bin\NodoAmeApi.exe` 与本仓库根目录的 `cevio-NodoAme-talk.exe` 是本项目自行编译的程序（MIT）。

## 不包含的内容

- 不含 CeVIO AI 的任何可执行文件、DLL、界面资源；
- 不含任何声库文件（`.tsnvoice`）或经声库渲染出的音频样本；
- 不含任何针对 CeVIO 的反编译产物。

如需使用，请自行安装并授权 CeVIO AI 与其歌曲声库；本工具在运行时从**你本机**的安装目录加载这些组件。
