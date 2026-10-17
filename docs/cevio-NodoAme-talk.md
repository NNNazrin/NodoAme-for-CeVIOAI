# cevio-NodoAme-talk 操作手册（v1.3.0）

文本 → WAV 的一步工具：**VOICEVOX 负责“怎么说”（talk）**，**CeVIO AI 负责“唱出来/说出来”的音色**。
整条链路全自动、无 GUI：`文本 → NodoAme(ccst) → CeVIO SongEditorControl(GetSongSequence) → WAV`。

---

## 1. 界面与身份行

启动（`ui` 或双击 exe）后只打印一行身份行：

```
cevio-NodoAme-talk <版本>  |  Talk.Engine: VOICEVOX  |  Talk: <Talk角色>  |  Output.Engine: CeVIO AI  |  Output: <声库名>
```

- `Talk.Engine` / `Talk`：VOICEVOX 引擎与角色（角色名实时从 `/speakers` 解析，失败回落 `talk.cast`）
- `Output.Engine` / `Output`：CeVIO AI 与输出声库显示名（`output.cast`）

## 2. 输入规则（重要）

| 你输入 | 结果 |
|---|---|
| `こんにちは、せかい` | **直接当 talk 文本合成**（没有 `/` 前缀一律视为文本） |
| `/voices`、`/use 1`、`/config show` … | 执行命令 |
| 直接回车 | 忽略 |

输入编码自动判定：按字节读 stdin，先严格 UTF-8，失败再依次尝试 GBK(936) / Shift-JIS(932) / UTF-16，最后清洗控制字符——控制台是 936 还是 65001 都能正确键入日文。

## 3. 首次运行（生成配置时手动输入）

删除 `config.json` 后再运行会进入向导，逐项询问（回车 = 建议值）：

1. CeVIO AI 安装目录（从 CeVIO 注册表自动探测；探测不到会给出示例路径，如 `C:\Program Files\CeVIO\CeVIO AI`）
2. 输出声库 `.tsnvoice`（自动取注册表中已安装的第一个声库；之后可用 `/voices` 换）
3. 输出声库显示名（用于文件名与身份行，可自命名）
4. CastId（由声库目录名自动推断）
5. Song 词典目录（`<CeVIO>\Configuration\Dictionary\Song`）
6. 输出 / 中间产物 / 临时目录

非交互环境（Agent、管道）自动采纳建议值并打印，不会卡住。之后可随时 `config wizard` 重跑。

首次运行还会做一次依赖自检（`doctor`）：缺项 > 0 返回退出码 9 并列出缺什么。

## 4. 命令

交互里命令一律 `/` 开头；命令行里也可以写 `cevio-NodoAme-talk voices`、`cevio-NodoAme-talk use 1`、`cevio-NodoAme-talk "/use 1"`。

| 命令 | 作用 |
|---|---|
| `/voices [--dump]` | 列出可用声库；`--dump` 按注册表生成 `voices.json` 名单模板 |
| `/use <编号\|名字\|CastId> [--as 显示名]` | 切换输出声库并立即写入 config |
| `/say <文本> [参数]` | 显式 talk（等价于直接输入文本） |
| `/play [wav]`（别名 `/playlast`、`/replay`） | 播放指定文件或**刚刚生成**的最新输出 |
| `/runs` | 最近运行（默认 10 条） |
| `/clean [--keep N]` | 按次数清理中间产物 |
| `/config show\|set k=v\|reset\|wizard\|path` | 配置 |
| `/doctor` | 依赖自检 |
| `/version` `/help` `/exit` | 版本 / 帮助 / 退出 |

### 4.1 声库来源：registry（默认）/ json

| 模式 | 配置 | 说明 |
|---|---|---|
| 注册表 | `output.voiceSource = "registry"` | 扫 `HKLM\SOFTWARE\CeVIO_NV\Subject\VocalSource\Song\<CastId> (x64)` 取 `InstallFolder` / `Version` / `PackageCode`；`PackageCode` 是 GUID 原始字节，转成 `{GUID}` 后到 `HKLM\SOFTWARE\CeVIO_NV\Product\{GUID} (x64)` 取 `ProductName` 作显示名（去掉“ ソングボイス”后缀）。装了哪些就列哪些 |
| 名单 | `output.voiceSource = "json"` | 读 `output.voiceList`（默认 `voices.json`）：`{"voices":[{"name","castId","version","path","model"}]}`。`name` 可任意自命名；`path` 是声库目录（自动递归找 `*.tsnvoice`），也可直接写 `model` |

```powershell
/voices --dump                     # 用注册表结果生成 voices.json
config set output.voiceSource=json  # 切到名单模式
/voices                             # 列出名单
/use 1                              # 按编号切换
/use 1 --as 我的声库                 # 切换 + 改显示名（用于文件名）
```

声库列表以 `/voices` 实际输出为准（每行：编号、显示名、CastId、版本、模型文件名）。

## 5. 输出文件名

默认（未给 `--out`）：

```
<声库名>-<前5个语音字符>-<时长>s.wav
例：output\<声库名>-こんにちは-2.01s.wav
```

- 声库名 = `output.cast`（`/use ... --as` 可改）
- 前 5 个“语音字符”：从文本里按顺序取前 5 个字母/数字/假名/汉字，跳过标点
- 时长 = **裁剪后**的实际时长（两位小数）

需要固定名就用 `--out x.wav`。

## 6. talk 的可选参数

| 参数 | 说明 |
|---|---|
| `--out <路径>` | 指定输出文件（默认见 §5） |
| `--play` / `--no-play` | 渲染后播放 / 不播放 |
| `--trim` / `--no-trim` | 本次裁 / 不裁首尾静音（默认裁） |
| `--ccst-only` | 只生成 `.ccst`，不渲染 |
| `--voice <x.tsnvoice>` / `--cast <名>` / `--cast-id <ID>` | 临时换输出声库/角色 |
| `--dic <目录>` / `--alpha <0.50-0.60>` | 词典 / 表现力 |
| `--tempo <BPM>` / `--speaker <N>` | 速度 / Talk 角色 id |
| `--keep <N>` | 本次保留几次运行 |
| `--json` | 单行 JSON 结果（Agent 用） |
| `--config <文件>` | 使用另一份配置 |

## 7. 配置文件（四段，`config.json`）

| 段 | 关键字段 |
|---|---|
| `talk` | `engine` / `host` / `cast` / `speaker` / `tempo` / `splitMode` / `lyricsMode` / `timeoutSeconds` / `autoPlay` |
| `output` | `engine` / `cast` / `castId` / `castVersion` / `alpha` / `voiceModel` / **`voiceSource`** / **`voiceList`** / `dictionary` |
| `paths` | `cevioDir` / `outputDir` / `runDir` / `tempDir` / `nodoAmeApi` / `renderExe` / `template`（默认留空，首运行输入） |
| `switch` | `trimStartSilence` / `trimEndSilence` / `trimThresholdDb` / `keepRuns` / `keepTempFiles` / `showTiming` / **`autoPlayOnRender`**（默认 `false`，渲染完自动播放刚生成的 wav） |

改配置三种方式：编辑 `config.json`、`config set k=v`、交互里 `/config set k=v`。
旧版扁平配置会在加载时自动迁移（`MigrateLegacy`）。

## 8. 目录结构

```
cevio-NodoAme-talk\
  cevio-NodoAme-talk.exe   主程序（标识：NodoAme / 1.2.0.0）
  config.json              配置（首次运行生成）
  voices.json              名单模式用（/voices --dump 生成）
  bin\                     NodoAmeApi.exe + 依赖 DLL + cevio-render2.exe
  template\temp.track.xml  ccst 母版
  docs\cevio-NodoAme-talk.md
  output\                  最终 WAV（不清理）
  runs\<时间戳>\            speech.ccst / render.wav / final.wav / run.log（按次数清理）
  tmp\                     引擎临时文件（每轮清空）
```

## 9. 产物、日志与清理

- `runs\` 与 `output\` 里各有一份 WAV：`runs\...\final.wav` 是裁剪后原件，`output\*.wav` 是最终交付。
- `runs\` 保留 `switch.keepRuns` 次（默认 5），更早的整目录删除；`output\` 永不自动删除。
- `tmp\` 每轮结束清空（`switch.keepTempFiles=true` 时保留）。
- 清理只认 `^\d{8}_\d{6}_\d{3}$` 形式的运行目录，手放的文件不会被误删。

## 10. JSON 字段与退出码

```json
{"ok":true,"out":"…wav","ccst":"…ccst","log":"…run.log","run":"…","bytes":157970,
 "durationSec":1.645,"sampleRate":48000,"peak":14401,"trimmedSamples":151437,
 "elapsedSec":12.72,"cleanedRuns":0,"text":"おはよ、せかい"}
```

失败时：`{"ok":false,"error":"…","log":"…","run":"…","exit":N}`

| 退出码 | 含义 |
|---|---|
| 0 | 成功 |
| 1 | 参数错误 / 文本为空 |
| 4 | 阶段①（文本→ccst）失败 |
| 5 / 6 | 缺少渲染器 / 阶段②（ccst→WAV）失败 |
| 7 | 音频为空（peak=0），不生成空白 WAV |
| 8 | `notes=0`：文本没有被 Talk 引擎转成音节 |
| 9 | `doctor` 有缺项 |
| -99 | 子进程超时被杀 |

## 11. 环境要求

- CeVIO AI（x64）+ 至少一个 Song 声库（`.tsnvoice`）
- VOICEVOX 引擎，默认 `http://127.0.0.1:50021`（`doctor` 会做一次 HTTP 探测）
- Windows 10/11 x64；首次运行需可写的 TEMP（程序会把它指向 `tmp\`）

### 11.1 exe 的发布者信息

exe 带 Win32 版本资源：产品名 `cevio-NodoAme-talk`、公司 `NodoAme local tooling`、文件版本 `1.2.0.0`。
这能减少杀软/Defender 的“未知程序”弹窗；要彻底消除 SmartScreen 提示仍需代码签名证书（未包含）。

## 12. 常见问题

| 现象 | 处理 |
|---|---|
| 输出的 WAV 是空白（peak=0） | 先看 `runs\…\run.log` 的 `text :` 是否乱码；用 `--file` 传 UTF-8 文本可绕过输入编码 |
| `stage1 failed, exit=2` | 文本里混进控制字符或非文字符号；`♪♪♪` 之类无法合成，会返回 8 |
| `/voices` 为空 | CeVIO 未安装或当前用户读不到注册表 → 改用 `output.voiceSource=json` 手工维护 |
| 切换声库后报 8 | 该声库没有日语 Song 词典，检查 `output.dictionary` |
| 渲染很慢（10–30 s） | 每次都会新起 CeVIO 引擎进程（约 8–20 s），属正常 |

## 13. 重建

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /langversion:5 /codepage:65001 ^
  /target:exe /platform:anycpu /optimize+ /out:cevio-NodoAme-talk.exe ^
  /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll CeVioTalk.cs
```

`/codepage:65001` 不能省（源码含中日文）。改源码请用 UTF-8 读写，**不要**用 `Set-Content` 直接把中日文 C# 源码读回再写（会双编码损坏）。

## 14. 版本历史

- **v1.3.0**（2026-10-17）
  - 交互模式新增 **Tab 补全**：命令名、`/use` 的声库名、`/config set` 的键名、`/play` 的 wav 文件名
  - 空行（或只输入 `/`）按 **Tab 即列出全部指令与一句话说明**（指令预览）
  - **↑/↓ 翻历史**、**Esc 清空当前行**、**Ctrl+C 放弃当前行**
  - 命令补全成功后自动打印该指令的用途
  - `/use` 兼容忘记空格/等号的写法：`--asかふ`、`--as=IAさん` 均可
  - 重绘使用 ANSI 转义（`SetConsoleMode` 打开 `ENABLE_VIRTUAL_TERMINAL_PROCESSING`），中文/日文宽度不再错位
- **v1.2.2**（2026-10-17）
  - `/config set`、`/voice`、`/use` 之后**身份行即时刷新**（改 `talk.speaker` 也立刻显示新角色）
  - 命令拼错时给建议：`/ues` → “是不是想输入 /use？”
  - `/use N --as` 漏写显示名时给出明确提示，不再把 `--as` 当成名字去匹配
  - 新增 **[人话版使用指南](人话版使用指南.md)**（面向普通使用者）
- **v1.2.1**（2026-10-17）
  - 新增开关 `switch.autoPlayOnRender`（默认 `false`）：渲染完成后自动播放刚生成的 wav
  - 新增命令 `/playlast`（别名 `/replay`，等价 `/play`）：播放刚刚生成的最新输出
  - 实测：VOICEVOX Talk 角色 × CeVIO 歌声库（含 `--as` 别名与自动播放）
- **v1.2.0**（2026-10-17）
  - 声库双来源：`output.voiceSource = registry（默认）/ json`；`/voices`、`/use`、`/voices --dump`
  - 注册表扫描 `CeVIO_NV` 并解析 `ProductName`（`PackageCode` GUID → Product 键）
  - 命令统一 `/` 前缀；无 `/` 的整行永远是 talk 文本
  - 输出文件名改为 `<声库名>-<前5个语音字符>-<时长>s.wav`
  - 输入编码：严格 UTF-8 优先 + 多编码回退 + 控制字符清洗
- **v1.1.1**：更名 `cevio-NodoAme-talk`、身份行、无命令即 talk、首尾裁剪、四段配置+向导、内置 NodoAme、纯净 zip
- **v1.1.0**：命令词 CLI、四段配置、首运行自检、裁剪开关、按次数清理
- **v1.0.0**：合并「文本→ccst」与「ccst→WAV」

## 15. 更新记录（最近三版）

- **v1.3.1** — 默认配置去个人化：首次向导不再写死任何本机路径；CeVIO 安装目录、Song 词典、首选声库全部从 CeVIO 安装注册表自动探测，探测不到才给示例值。
- **v1.3.0** — 交互支持 Tab 补全与指令预览（空行 Tab 列出全部指令与说明），↑↓ 翻历史，Esc 清行；`/use 2 --as名前` 支持无空格粘连写法。
- **v1.2.2** — `/config set`、`/voice`、`/use` 之后身份行立即刷新；命令拼错给出“是不是想输入 /use”提示；新增《使用指南》面向人工阅读。
