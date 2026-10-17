# cevio-NodoAme-talk

把一段文字变成 **CeVIO AI 歌声库唱出来的 WAV**，一条命令完成：

```
文本 ──VOICEVOX(朗读)──▶ 音高/音素 ──NodoAme──▶ .ccst 曲谱 ──CeVIO AI 歌声库──▶ WAV
```

- 前半段用 **VOICEVOX** 把文字读成带语调的语音，再由 **NodoAme** 把音高与音素写成 CeVIO 的 `.ccst` 曲谱；
- 后半段用**本机已安装的 CeVIO AI**（歌声库）把曲谱渲染成音频。
- 工具本身**不包含** CeVIO、VOICEVOX、NodoAme 的任何程序与声库数据，只负责调用你本机已有的安装。

> 非官方工具，与 CeVIO / VOICEVOX / NodoAme 的作者无关联。使用者需自行拥有相应软件的合法授权。

## 运行要求

| 项目 | 说明 |
|---|---|
| 系统 | Windows 10 / 11 x64 |
| 运行时 | .NET Framework 4.8（系统自带） |
| 朗读引擎 | VOICEVOX ENGINE 本机运行（默认 `http://127.0.0.1:50021`） |
| 歌声引擎 | 本机安装的 CeVIO AI + 至少一个**歌曲（Song）声库** |

工具只与本机 `127.0.0.1` 通信，不联网、不上传任何文本或音频。

## 快速开始

1. 解压，双击 `cevio-NodoAme-talk.exe`（或命令行运行同名程序）。
2. 首次运行会出现向导：确认 CeVIO AI 安装目录、输出声库、显示名、输出目录。
   - 目录与声库会**自动从 CeVIO 的安装信息里探测**；探测不到才需要手填。
3. 直接输入一句文字，回车即可生成 WAV：

```
> こんにちは、私はせかいです
ccst  ok  (13.8 KB)  3.3s
wav   ok  (450 KB)   3.9s
out   : output\<声库名>-こんにちは-1.65s.wav
```

交互模式下：

- **以 `/` 开头的行**是命令（`/help` 查看全部）；
- **不以 `/` 开头的行**就是待合成的文本；
- `Tab` 补全、`↑`/`↓` 翻历史、`Esc` 清行。

## 常用命令

| 命令 | 作用 |
|---|---|
| `/say <文本>` | 合成（等价于直接输入文本） |
| `/voices` | 列出可用歌声库（注册表来源） |
| `/use <编号\|名字>` | 切换歌声库，`--as` 可改显示名 |
| `/play` `/playlast` | 播放最新输出的 WAV |
| `/config set k=v` | 改配置（如 `talk.speaker=8`） |
| `/doctor` | 依赖自检 |
| `/runs` `/clean` | 查看 / 清理中间产物 |

命令行（Agent / 脚本）用法：

```bat
cevio-NodoAme-talk.exe "こんにちは" --json
cevio-NodoAme-talk.exe say "おはよう" --out out.wav --play
```

## 输出

- 默认写到 `output\<声库名>-<文本前5字>-<时长>s.wav`（48 kHz / 16-bit / 单声道）
- 中间产物在 `runs\<时间戳>\`（`.ccst`、`render.wav`、`run.log`），默认只保留最近 5 次
- 配置在 `config.json`，声库名单（可选手工模式）在 `voices.json`

## 目录结构

```
cevio-NodoAme-talk.exe     主程序
CeVioTalk.cs               主程序源码（单文件，C# 5）
bin\                       两个阶段的可执行程序及其依赖
  NodoAmeApi.exe           文本 → .ccst（NodoAme）
  cevio-render2.exe        .ccst → WAV（调用本机 CeVIO AI）
  CeVioRender2.cs          上面这个的源码
template\temp.track.xml    .ccst 母版
docs\                      手册与使用指南
build.ps1                  重新编译两个 exe
```

## 重新编译

```powershell
pwsh -File build.ps1
```

需要 .NET Framework 4.8 的 `csc.exe`（Windows 自带）。`cevio-render2.exe` 编译时需要引用本机 CeVIO AI 的 DLL（脚本会从注册表/参数里找），**编译产物不含 CeVIO 代码**，运行时才从你的安装目录加载。

## 隐私

- 只读取本机注册表 `HKLM\SOFTWARE\CeVIO_NV` 下的安装信息（安装路径、已装声库版本与 GUID），用于定位 CeVIO。
- 不收集、不上传任何数据；`docs` 与 `config.json` 中不含任何个人路径（首次运行由向导生成）。

## 许可

本工具代码以 MIT 许可发布，详见 `LICENSE`。随附的第三方组件许可见 `NOTICE.md`。
