// CeVioTalk.cs - cevio-NodoAme-talk 1.1.1
// 文本 -> WAV：Talk 引擎（VOICEVOX，经 NodoAme）-> 输出引擎（CeVIO AI）
// 无命令时直接给文字即执行 talk：  cevio-NodoAme-talk "おはよう、世界"
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Win32 版本信息（csc 会据此生成 VS_VERSION_INFO，缓解 Defender/SmartScreen 误报）
[assembly: System.Reflection.AssemblyTitle("cevio-NodoAme-talk")]
[assembly: System.Reflection.AssemblyDescription("Text to singing WAV: VOICEVOX talk via NodoAme, rendered by CeVIO AI")]
[assembly: System.Reflection.AssemblyProduct("cevio-NodoAme-talk")]
[assembly: System.Reflection.AssemblyCompany("NodoAme / CeVIO local tooling")]
[assembly: System.Reflection.AssemblyCopyright("Copyright (C) 2026")]
[assembly: System.Reflection.AssemblyVersion("1.3.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.3.1.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.1.1")]
[assembly: System.Reflection.AssemblyTrademark("")]
[assembly: System.Runtime.InteropServices.ComVisible(false)]

public static class CeVioTalk
{
    public const string APP = "cevio-NodoAme-talk";
    public const string VERSION = "1.3.1";

    static string gRoot;
    static Config gCfg;
    static string gCfgPath;
    static bool gJson;
    static bool gInUi;
    static bool gChanged;
    static string gLastInput = "";   // 最近一次交互输入的原始诊断信息

    // ================= config =================
    public class Config
    {
        public TalkSection talk = new TalkSection();
        public OutSection output = new OutSection();
        public PathSection paths = new PathSection();
        public SwitchSection @switch = new SwitchSection();
    }

    public class TalkSection
    {
        public string engine = "VOICEVOX";
        public string host = "http://127.0.0.1:50021";
        public string cast = "";             // 留空=运行时按 speaker 从 VOICEVOX 解析
        public int speaker = 8;
        public double tempo = 150.0;
        public string splitMode = "SYLLABLE";
        public string lyricsMode = "KANA";
        public int timeoutSeconds = 600;
        public bool autoPlay = false;
    }

    public class OutSection
    {
        public string engine = "CeVIO AI";
        public string cast = "";
        public string castId = "";
        public string castVersion = "1.0.2";
        public double alpha = 0.55;
        public string voiceModel = "";
        public string dictionary = "";
        // 声库来源：registry（扫描 CeVIO 安装注册表，默认）| json（手动维护名单，可自定义名字）
        public string voiceSource = "registry";
        public string voiceList = "voices.json";
    }

    public class PathSection
    {
        public string cevioDir = "";
        public string outputDir = "";
        public string runDir = "";
        public string tempDir = "";
        public string nodoAmeApi = "";
        public string renderExe = "";
        public string template = "";
    }

    public class SwitchSection
    {
        public bool trimStartSilence = true;
        public bool trimEndSilence = true;
        public double trimThresholdDb = -50.0;
        public int keepRuns = 5;
        public bool keepTempFiles = false;
        public bool showTiming = true;
        public bool autoPlayOnRender = false;   // 渲染完成后自动播放刚生成的 wav（默认关闭）
    }

    static void Normalize(Config c)
    {
        if (c.talk == null) c.talk = new TalkSection();
        if (c.output == null) c.output = new OutSection();
        if (c.paths == null) c.paths = new PathSection();
        if (c.@switch == null) c.@switch = new SwitchSection();
        if (string.IsNullOrEmpty(c.talk.engine)) c.talk.engine = "VOICEVOX";
        if (string.IsNullOrEmpty(c.talk.host)) c.talk.host = "http://127.0.0.1:50021";
        if (c.talk.speaker <= 0) c.talk.speaker = 8;
        if (string.IsNullOrEmpty(c.output.engine)) c.output.engine = "CeVIO AI";
        if (string.IsNullOrEmpty(c.output.voiceSource)) c.output.voiceSource = "registry";
        if (c.output.voiceSource != "json") c.output.voiceSource = "registry";
        if (string.IsNullOrEmpty(c.output.voiceList)) c.output.voiceList = "voices.json";
        if (c.output.alpha < 0.5 || c.output.alpha > 0.6) c.output.alpha = 0.55;
        if (c.@switch.keepRuns < 1) c.@switch.keepRuns = 5;
        if (c.@switch.trimThresholdDb > 0) c.@switch.trimThresholdDb = -50.0;
        if (c.talk.timeoutSeconds < 30) c.talk.timeoutSeconds = 600;
    }

    static string JsonOf(Config c)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"talk\": {");
        AStr(sb, 4, "engine", c.talk.engine, true);
        AStr(sb, 4, "host", c.talk.host, true);
        AStr(sb, 4, "cast", c.talk.cast, true);
        ANum(sb, 4, "speaker", c.talk.speaker.ToString(CultureInfo.InvariantCulture), true);
        ANum(sb, 4, "tempo", c.talk.tempo.ToString("F1", CultureInfo.InvariantCulture), true);
        AStr(sb, 4, "splitMode", c.talk.splitMode, true);
        AStr(sb, 4, "lyricsMode", c.talk.lyricsMode, true);
        ANum(sb, 4, "timeoutSeconds", c.talk.timeoutSeconds.ToString(CultureInfo.InvariantCulture), true);
        ABol(sb, 4, "autoPlay", c.talk.autoPlay, false);
        sb.AppendLine("  },");
        sb.AppendLine("  \"output\": {");
        AStr(sb, 4, "engine", c.output.engine, true);
        AStr(sb, 4, "cast", c.output.cast, true);
        AStr(sb, 4, "castId", c.output.castId, true);
        AStr(sb, 4, "castVersion", c.output.castVersion, true);
        ANum(sb, 4, "alpha", c.output.alpha.ToString("F2", CultureInfo.InvariantCulture), true);
        AStr(sb, 4, "voiceModel", c.output.voiceModel, true);
        AStr(sb, 4, "voiceSource", c.output.voiceSource, true);
        AStr(sb, 4, "voiceList", c.output.voiceList, true);
        AStr(sb, 4, "dictionary", c.output.dictionary, false);
        sb.AppendLine("  },");
        sb.AppendLine("  \"paths\": {");
        AStr(sb, 4, "cevioDir", c.paths.cevioDir, true);
        AStr(sb, 4, "outputDir", c.paths.outputDir, true);
        AStr(sb, 4, "runDir", c.paths.runDir, true);
        AStr(sb, 4, "tempDir", c.paths.tempDir, true);
        AStr(sb, 4, "nodoAmeApi", c.paths.nodoAmeApi, true);
        AStr(sb, 4, "renderExe", c.paths.renderExe, true);
        AStr(sb, 4, "template", c.paths.template, false);
        sb.AppendLine("  },");
        sb.AppendLine("  \"switch\": {");
        ABol(sb, 4, "trimStartSilence", c.@switch.trimStartSilence, true);
        ABol(sb, 4, "trimEndSilence", c.@switch.trimEndSilence, true);
        ANum(sb, 4, "trimThresholdDb", c.@switch.trimThresholdDb.ToString("F1", CultureInfo.InvariantCulture), true);
        ANum(sb, 4, "keepRuns", c.@switch.keepRuns.ToString(CultureInfo.InvariantCulture), true);
        ABol(sb, 4, "keepTempFiles", c.@switch.keepTempFiles, true);
        ABol(sb, 4, "showTiming", c.@switch.showTiming, true);
        ABol(sb, 4, "autoPlayOnRender", c.@switch.autoPlayOnRender, false);
        sb.AppendLine("  }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    static void AStr(StringBuilder sb, int ind, string k, string v, bool comma)
    {
        sb.Append(' ', ind).Append('"').Append(k).Append("\": \"").Append(Esc(v)).Append('"').AppendLine(comma ? "," : "");
    }
    static void ANum(StringBuilder sb, int ind, string k, string v, bool comma)
    {
        sb.Append(' ', ind).Append('"').Append(k).Append("\": ").Append(v).AppendLine(comma ? "," : "");
    }
    static void ABol(StringBuilder sb, int ind, string k, bool v, bool comma)
    {
        sb.Append(' ', ind).Append('"').Append(k).Append("\": ").Append(v ? "true" : "false").AppendLine(comma ? "," : "");
    }

    static string Esc(string s)
    {
        if (s == null) return "";
        StringBuilder sb = new StringBuilder();
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 32) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                    else sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }

    static Config LoadConfig(string path)
    {
        Config c = null;
        if (File.Exists(path))
        {
            try { c = new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(path, Encoding.UTF8)); }
            catch (Exception ex)
            {
                Console.Error.WriteLine("WARN: config 解析失败（" + ex.Message + "），将重新生成");
                try { File.Copy(path, path + ".bak", true); } catch (Exception) { }
            }
        }
        if (c == null) c = new Config();
        Normalize(c);
        return c;
    }

    static void SaveConfig() { File.WriteAllText(gCfgPath, JsonOf(gCfg), new UTF8Encoding(false)); }

    // ================= first-run wizard =================
    static void Wizard()
    {
        Console.WriteLine("== " + APP + " " + VERSION + " 首次运行 ==");
        Console.WriteLine("路径默认留空，下面逐项确认（直接回车 = 使用 [ ] 中的建议值）。");
        bool tty = !Console.IsInputRedirected;

        // 1. CeVIO 目录：优先从 CeVIO_NV 注册表读安装位置，读不到就留空让用户自己填
        if (string.IsNullOrEmpty(gCfg.paths.cevioDir))
        {
            string sug = CeVioDirFromRegistry();
            gCfg.paths.cevioDir = Ask("CeVIO AI 安装目录（例：C:\\Program Files\\CeVIO\\CeVIO AI）", sug, tty);
        }
        // 2. 输出角色（声库）：从注册表列出的声库中挑第一个，选不到就留空
        if (string.IsNullOrEmpty(gCfg.output.voiceModel))
        {
            string sug = FindFirstVoicebank();
            gCfg.output.voiceModel = Ask("输出声库 .tsnvoice 全路径（可先回车，之后用 /voices 选）", sug, tty);
        }
        if (string.IsNullOrEmpty(gCfg.output.castId) && !string.IsNullOrEmpty(gCfg.output.voiceModel))
            gCfg.output.castId = GuessCastId(gCfg.output.voiceModel);
        if (string.IsNullOrEmpty(gCfg.output.cast))
            gCfg.output.cast = Ask("输出角色显示名（用于文件名，可自取）", gCfg.output.castId, tty);
        // 3. 词典
        if (string.IsNullOrEmpty(gCfg.output.dictionary) && !string.IsNullOrEmpty(gCfg.paths.cevioDir))
            gCfg.output.dictionary = SongDictDir(gCfg.paths.cevioDir);
        if (string.IsNullOrEmpty(gCfg.output.dictionary))
            gCfg.output.dictionary = Ask("Song 词典目录", "", tty);
        // 4. 工作目录
        if (string.IsNullOrEmpty(gCfg.paths.outputDir)) gCfg.paths.outputDir = Ask("输出目录", "output", tty);
        if (string.IsNullOrEmpty(gCfg.paths.runDir)) gCfg.paths.runDir = Ask("中间产物目录", "runs", tty);
        if (string.IsNullOrEmpty(gCfg.paths.tempDir)) gCfg.paths.tempDir = Ask("临时目录", "tmp", tty);
        // 5. 引擎程序（工具自带，默认即可）
        if (string.IsNullOrEmpty(gCfg.paths.nodoAmeApi)) gCfg.paths.nodoAmeApi = @"bin\NodoAmeApi.exe";
        if (string.IsNullOrEmpty(gCfg.paths.renderExe)) gCfg.paths.renderExe = @"bin\cevio-render2.exe";
        if (string.IsNullOrEmpty(gCfg.paths.template)) gCfg.paths.template = @"template\temp.track.xml";

        SaveConfig();
        Console.WriteLine("已写入 " + gCfgPath);
        Console.WriteLine();
    }

    static string Ask(string label, string suggest, bool tty)
    {
        if (!tty || Console.IsInputRedirected)
        {
            Console.WriteLine("  " + label + " = " + (string.IsNullOrEmpty(suggest) ? "(空)" : suggest));
            return suggest;
        }
        Console.Write("  " + label + (string.IsNullOrEmpty(suggest) ? "" : " [" + suggest + "]") + ": ");
        string s = ReadLineRaw();
        if (string.IsNullOrWhiteSpace(s)) return suggest;
        return s.Trim().Trim('"');
    }

    // 从注册表读 CeVIO 安装目录；拿不到就返回空（交给用户填）
    static string CeVioDirFromRegistry()
    {
        string[] keys = new string[] { @"SOFTWARE\CeVIO_NV\Subject\Editor\x64", @"SOFTWARE\CeVIO_NV\Subject\Editor" };
        for (int i = 0; i < keys.Length; i++)
        {
            try
            {
                Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keys[i]);
                if (rk == null) rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keys[i]);
                if (rk == null) continue;
                string f = Convert.ToString(rk.GetValue("InstallFolder", ""));
                if (!string.IsNullOrEmpty(f) && Directory.Exists(f)) return f.TrimEnd('\\');
            }
            catch (Exception) { }
        }
        return "";
    }

    static string SongDictDir(string cevioDir)
    {
        string p = Path.Combine(cevioDir, "Configuration", "Dictionary", "Song");
        if (Directory.Exists(p)) return p;
        string q = Path.Combine(cevioDir, "Dictionary", "Song");
        if (Directory.Exists(q)) return q;
        return p;
    }

    // 首个可用声库：优先注册表里列出的，取不到就留空
    static string FindFirstVoicebank()
    {
        try
        {
            List<VoiceEntry> vs = RegistryVoices();
            if (vs.Count > 0) return vs[0].model;
        }
        catch (Exception) { }
        return "";
    }

    static string GuessCastId(string voiceModel)
    {
        try
        {
            string d = Path.GetFileName(Path.GetDirectoryName(voiceModel));
            int p = d.IndexOf(" (");
            if (p > 0) d = d.Substring(0, p);
            return d.Trim();
        }
        catch (Exception) { return ""; }
    }

    // ================= header =================
    static string TalkCastName()
    {
        string code = ResolveSpeakerName(gCfg.talk.speaker);
        if (!string.IsNullOrEmpty(code)) return code;
        return string.IsNullOrEmpty(gCfg.talk.cast) ? "(未连接)" : gCfg.talk.cast;
    }

    static string ResolveSpeakerName(int id)
    {
        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(gCfg.talk.host.TrimEnd('/') + "/speakers");
            req.Timeout = 1500;
            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            using (StreamReader r = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
            {
                object[] arr = new JavaScriptSerializer().Deserialize<object[]>(r.ReadToEnd());
                if (arr == null) return null;
                foreach (object o in arr)
                {
                    Dictionary<string, object> ch = o as Dictionary<string, object>;
                    if (ch == null) continue;
                    object stylesObj;
                    if (!ch.TryGetValue("styles", out stylesObj)) continue;
                    object[] styles = stylesObj as object[];
                    if (styles == null) continue;
                    foreach (object so in styles)
                    {
                        Dictionary<string, object> st = so as Dictionary<string, object>;
                        if (st == null) continue;
                        object sid;
                        if (!st.TryGetValue("id", out sid) || sid == null) continue;
                        if (Convert.ToInt32(sid) == id)
                        {
                            object nm;
                            return ch.TryGetValue("name", out nm) && nm != null ? Convert.ToString(nm) : null;
                        }
                    }
                }
            }
        }
        catch (Exception) { }
        return null;
    }

    static void Header()
    {
        string talk = TalkCastName();
        string outCast = string.IsNullOrEmpty(gCfg.output.cast) ? "(未设定)" : gCfg.output.cast;
        Console.WriteLine(APP + " " + VERSION
            + "  |  Talk.Engine: " + gCfg.talk.engine
            + "  |  Talk: " + talk
            + "  |  Output.Engine: " + gCfg.output.engine
            + "  |  Output: " + outCast);
    }

    // ================= main =================
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern bool SetConsoleOutputCP(uint codePage);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern bool SetConsoleCP(uint codePage);

    public static int Main(string[] args)
    {
        try
        {
            // 真实控制台：先把控制台代码页切成 UTF-8，否则键入的日文会被按 936/932 解码成乱码。
            if (!Console.IsInputRedirected) { SetConsoleCP(65001); SetConsoleOutputCP(65001); }
            Console.InputEncoding = new UTF8Encoding(false);
            Console.OutputEncoding = new UTF8Encoding(false);
            gRoot = AppDomain.CurrentDomain.BaseDirectory;
            gCfgPath = Path.Combine(gRoot, "config.json");

            List<string> argv = new List<string>(args);
            for (int i = 0; i < argv.Count; i++)
            {
                if (argv[i] == "--json") { gJson = true; argv.RemoveAt(i); i--; }
                else if (argv[i] == "--config" && i + 1 < argv.Count) { gCfgPath = argv[i + 1]; argv.RemoveRange(i, 2); i--; }
            }

            bool firstRun = !File.Exists(gCfgPath);
            gCfg = LoadConfig(gCfgPath);
            bool meta = argv.Count > 0 && (argv[0] == "version" || argv[0] == "ver" || argv[0] == "help" || argv[0] == "h" || argv[0] == "--help");
            if ((firstRun || gChanged) && !meta) Wizard();
            else if (!firstRun) SaveConfig();   // 已存在则补齐/规范化；首运行不写空配置

            return Dispatch(argv);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FATAL: " + ex.GetType().Name + ": " + ex.Message);
            return 2;
        }
    }

    static int Dispatch(List<string> a)
    {
        if (a.Count == 0)
        {
            return Ui();   // 无参数一律进交互；stdin 结束（ReadLine 返回 null）自动退出
        }
        string cmd = a[0].StartsWith("-", StringComparison.Ordinal) ? a[0].TrimStart('-') : a[0].TrimStart('/');
        List<string> rest = a.GetRange(1, a.Count - 1);
        switch (cmd.ToLowerInvariant())
        {
            case "voices": case "voicebanks": case "vb": return VoicesCmd(rest);
            case "use": case "cast": return UseCmd(rest);
            case "say": case "talk": case "text": return Say(rest);
            case "play": case "playlast": case "replay": return Play(rest);
            case "runs": case "list": return Runs(rest);
            case "clean": return Clean(rest);
            case "config": case "cfg": return ConfigCmd(rest);
            case "doctor": case "check": return Doctor(true) > 0 ? 9 : 0;
            case "ui": return Ui();
            case "help": case "h": Help(); return 0;
            case "version": case "ver": Console.WriteLine(APP + " " + VERSION); return 0;
            default: return Say(a);   // 无命令：整行当文本
        }
    }

    // ================= say =================
    static int Say(List<string> a)
    {
        string text = null, outPath = null;
        bool ccstOnly = false, play = false, noPlay = false;
        bool? trimStart = null, trimEnd = null;
        List<string> words = new List<string>();

        for (int i = 0; i < a.Count; i++)
        {
            string t = a[i];
            string v = (i + 1 < a.Count) ? a[i + 1] : null;
            switch (t)
            {
                case "--text": text = v; i++; break;
                case "--text-file": case "--file": text = ReadTextFile(v); i++; break;
                case "--out": case "-o": outPath = v; i++; break;
                case "--voice": gCfg.output.voiceModel = v; gCfg.output.castId = GuessCastId(v); i++; break;
                case "--dic": gCfg.output.dictionary = v; i++; break;
                case "--cast": gCfg.output.cast = v; i++; break;
                case "--cast-id": gCfg.output.castId = v; i++; break;
                case "--alpha": gCfg.output.alpha = ParseD(v); i++; break;
                case "--tempo": gCfg.talk.tempo = ParseD(v); i++; break;
                case "--speaker": gCfg.talk.speaker = (int)ParseD(v); i++; break;
                case "--keep": gCfg.@switch.keepRuns = (int)ParseD(v); i++; break;
                case "--ccst-only": ccstOnly = true; break;
                case "--play": play = true; break;
                case "--no-play": noPlay = true; break;
                case "--trim": trimStart = true; trimEnd = true; break;
                case "--no-trim": trimStart = false; trimEnd = false; break;
                default:
                    if (!t.StartsWith("-", StringComparison.Ordinal)) words.Add(t);
                    break;
            }
        }
        if (text == null && words.Count > 0) text = string.Join(" ", words.ToArray());
        if (string.IsNullOrEmpty(text)) { Console.Error.WriteLine("缺少文本。例：" + APP + " \"おはよう、世界\""); return 1; }
        if (play) gCfg.talk.autoPlay = true;
        if (noPlay) gCfg.talk.autoPlay = false;
        if (trimStart.HasValue) gCfg.@switch.trimStartSilence = trimStart.Value;
        if (trimEnd.HasValue) gCfg.@switch.trimEndSilence = trimEnd.Value;
        return Render(text, outPath, ccstOnly);
    }

    static int Render(string text, string outPath, bool ccstOnly)
    {
        Stopwatch total = Stopwatch.StartNew();
        text = Sanitize(text);
        if (text.Length == 0)
        {
            Console.Error.WriteLine("文本为空或无法解码。请改用 --file <utf-8.txt> 传入，或设置 CEVIO_TALK_DEBUG_INPUT=1 查看原始字节。");
            return 1;
        }
        if (!gJson && !gInUi) Header();
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string run = Path.Combine(Abs(Resolve(gCfg.paths.runDir, "runs")), stamp);
        EnsureDir(run);
        string log = Path.Combine(run, "run.log");
        StringBuilder logBuf = new StringBuilder();
        string ccst = Path.Combine(run, "speech.ccst");
        string wav = Path.Combine(run, "render.wav");
        string tmp = Abs(Resolve(gCfg.paths.tempDir, "tmp"));
        EnsureDir(tmp);

        bool autoName = string.IsNullOrEmpty(outPath);
        if (autoName) outPath = Path.Combine(run, "out_auto.wav");   // 真正文件名在拿到时长后生成
        outPath = Path.GetFullPath(outPath);
        EnsureDir(Path.GetDirectoryName(outPath));

        LogLine(logBuf, "== " + APP + " " + VERSION + " ==");
        LogLine(logBuf, "text : " + text);
        LogLine(logBuf, "run  : " + run);

        string apiExe = Abs(Resolve(gCfg.paths.nodoAmeApi, @"bin\NodoAmeApi.exe"));
        if (!File.Exists(apiExe)) return Fail(run, logBuf, log, "缺少 " + apiExe + "（运行 doctor）", 3);
        List<string> a1 = new List<string>();
        a1.Add("--text"); a1.Add(text);
        a1.Add("--out"); a1.Add(ccst);
        a1.Add("--cast"); a1.Add(gCfg.output.castId);
        a1.Add("--speaker"); a1.Add(gCfg.talk.speaker.ToString(CultureInfo.InvariantCulture));
        a1.Add("--tempo"); a1.Add(gCfg.talk.tempo.ToString("F1", CultureInfo.InvariantCulture));
        a1.Add("--split"); a1.Add(gCfg.talk.splitMode);
        a1.Add("--lyrics"); a1.Add(gCfg.talk.lyricsMode);
        a1.Add("--host"); a1.Add(gCfg.talk.host);
        string tpl = Abs(Resolve(gCfg.paths.template, @"template\temp.track.xml"));
        if (File.Exists(tpl)) { a1.Add("--template"); a1.Add(tpl); }

        Dictionary<string, string> env = new Dictionary<string, string>();
        env["NODOAME_API_TMP"] = tmp; env["TEMP"] = tmp; env["TMP"] = tmp;
        Stopwatch sw = Stopwatch.StartNew();
        int rc1 = Exec(apiExe, Path.GetDirectoryName(apiExe), a1, env, gCfg.talk.timeoutSeconds, logBuf, "stage1");
        sw.Stop();
        if (rc1 != 0 || !File.Exists(ccst)) return Fail(run, logBuf, log, "stage1 (text->ccst) failed, exit=" + rc1, 4);
        Match mn = Regex.Match(logBuf.ToString(), @"notes=(\d+)");
        if (mn.Success && mn.Groups[1].Value == "0")
        {
            if (gLastInput.Length > 0) LogLine(logBuf, "input  : " + gLastInput);
            return Fail(run, logBuf, log, "文本没有可唱音节 (notes=0)：Talk 引擎没把它转成音素，请换文本或改用 --file 传入 UTF-8 文件", 8);
        }
        if (!gJson && !ccstOnly) Console.WriteLine("ccst  ok  (" + new FileInfo(ccst).Length + " B)  " + sw.Elapsed.TotalSeconds.ToString("F1") + "s");

        if (ccstOnly)
        {
            total.Stop();
            LogLine(logBuf, "elapsed: " + total.Elapsed.TotalSeconds.ToString("F2") + "s");
            WriteLog(log, logBuf);
            int rm0 = CleanRuns(gCfg.@switch.keepRuns);
            if (gJson) Console.WriteLine("{\"ok\":true,\"ccst\":" + Q(ccst) + ",\"log\":" + Q(log) + ",\"run\":" + Q(run) + ",\"cleanedRuns\":" + rm0 + "}");
            return 0;
        }

        string renderExe = Abs(Resolve(gCfg.paths.renderExe, @"bin\cevio-render2.exe"));
        if (!File.Exists(renderExe)) return Fail(run, logBuf, log, "缺少 " + renderExe + "（运行 doctor）", 5);
        List<string> a2 = new List<string>();
        a2.Add(ccst); a2.Add(wav);
        a2.Add("--voice"); a2.Add(gCfg.output.voiceModel);
        a2.Add("--dic"); a2.Add(gCfg.output.dictionary);
        a2.Add("--alpha"); a2.Add(gCfg.output.alpha.ToString("F2", CultureInfo.InvariantCulture));
        a2.Add("--version"); a2.Add(gCfg.output.castVersion);
        sw = Stopwatch.StartNew();
        int rc2 = Exec(renderExe, Path.GetDirectoryName(renderExe), a2, env, gCfg.talk.timeoutSeconds, logBuf, "stage2");
        sw.Stop();
        if (rc2 != 0 || !File.Exists(wav)) return Fail(run, logBuf, log, "stage2 (ccst->wav) failed, exit=" + rc2, 6);
        if (!gJson) Console.WriteLine("wav   ok  (" + new FileInfo(wav).Length + " B)  " + sw.Elapsed.TotalSeconds.ToString("F1") + "s");

        long trimmed = 0;
        string stage = Path.Combine(run, "final.wav");
        if (gCfg.@switch.trimStartSilence || gCfg.@switch.trimEndSilence)
            trimmed = TrimWav(wav, stage, gCfg.@switch.trimStartSilence, gCfg.@switch.trimEndSilence, gCfg.@switch.trimThresholdDb);
        else File.Copy(wav, stage, true);
        if (!gJson && !gInUi) Console.WriteLine("trim  ok  (" + (trimmed > 0 ? trimmed + " samples cut" : "kept full length") + ")");

        total.Stop();
        double peak, dur; int rate;
        ReadWavInfo(stage, out peak, out dur, out rate);
        if (autoName)
        {
            string outDir = Abs(Resolve(gCfg.paths.outputDir, "output"));
            EnsureDir(outDir);
            outPath = Path.Combine(outDir, AutoFileName(text, dur) + ".wav");
        }
        File.Copy(stage, outPath, true);
        if (peak <= 0.5)
        {
            LogLine(logBuf, "out    : " + outPath + " (" + new FileInfo(outPath).Length + " B)");
            LogLine(logBuf, "audio  : EMPTY (peak=" + peak.ToString("F0") + ")");
            if (gLastInput.Length > 0) LogLine(logBuf, "input  : " + gLastInput);
            LogLine(logBuf, "hint   : 文本未转成音符或声库未发声，请用 doctor 检查 talk 引擎与输出声库");
            WriteLog(log, logBuf);
            try { File.Delete(outPath); } catch (Exception) { }      // 不留空白产物
            return Fail(run, logBuf, log, "empty audio (peak=0)", 7);
        }
        LogLine(logBuf, "out    : " + outPath + " (" + new FileInfo(outPath).Length + " B)");
        LogLine(logBuf, "audio  : " + dur.ToString("F3") + "s @" + rate + "Hz peak=" + peak.ToString("F0") + (trimmed > 0 ? " trimmed=" + trimmed : ""));
        LogLine(logBuf, "elapsed: " + total.Elapsed.TotalSeconds.ToString("F2") + "s");
        WriteLog(log, logBuf);
        int removed = CleanRuns(gCfg.@switch.keepRuns);
        if (!gCfg.@switch.keepTempFiles) ClearDir(tmp);

        if (gJson)
        {
            StringBuilder j = new StringBuilder();
            j.Append("{\"ok\":true");
            j.Append(",\"out\":").Append(Q(outPath));
            j.Append(",\"ccst\":").Append(Q(ccst));
            j.Append(",\"log\":").Append(Q(log));
            j.Append(",\"run\":").Append(Q(run));
            j.Append(",\"bytes\":").Append(new FileInfo(outPath).Length);
            j.Append(",\"durationSec\":").Append(dur.ToString("F3", CultureInfo.InvariantCulture));
            j.Append(",\"sampleRate\":").Append(rate);
            j.Append(",\"peak\":").Append(peak.ToString("F0", CultureInfo.InvariantCulture));
            j.Append(",\"trimmedSamples\":").Append(trimmed);
            j.Append(",\"elapsedSec\":").Append(total.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture));
            j.Append(",\"cleanedRuns\":").Append(removed);
            j.Append(",\"text\":").Append(Q(text));
            j.Append("}");
            Console.WriteLine(j.ToString());
        }
        else
        {
            Console.WriteLine("out   : " + outPath);
            Console.WriteLine("time  : " + total.Elapsed.TotalSeconds.ToString("F2") + "s" + (trimmed > 0 ? ", trimmed " + trimmed + " samples" : "") + ", cleaned runs: " + removed);
        }
        if (gCfg.talk.autoPlay || gCfg.@switch.autoPlayOnRender) PlayWav(outPath);
        return 0;
    }

    // ================= other commands =================
    static int Play(List<string> a)
    {
        string p = (a.Count > 0 && !a[0].StartsWith("-", StringComparison.Ordinal)) ? a[0] : null;
        if (p == null)
        {
            string dir = Abs(Resolve(gCfg.paths.outputDir, "output"));
            if (!Directory.Exists(dir)) { Console.Error.WriteLine("输出目录为空"); return 1; }
            FileInfo newest = null;
            foreach (FileInfo f in new DirectoryInfo(dir).GetFiles("*.wav"))
                if (newest == null || f.LastWriteTimeUtc > newest.LastWriteTimeUtc) newest = f;
            if (newest == null) { Console.Error.WriteLine("没有可播放的 wav"); return 1; }
            p = newest.FullName;
        }
        if (!File.Exists(p)) { Console.Error.WriteLine("文件不存在: " + p); return 1; }
        Console.WriteLine("playing: " + p);
        PlayWav(p); return 0;
    }

    static int Runs(List<string> a)
    {
        int limit = 10;
        for (int i = 0; i + 1 < a.Count; i++) if (a[i] == "--limit") limit = (int)ParseD(a[i + 1]);
        string dir = Abs(Resolve(gCfg.paths.runDir, "runs"));
        if (!Directory.Exists(dir)) { Console.WriteLine("(还没有运行记录)"); return 0; }
        List<DirectoryInfo> ds = new List<DirectoryInfo>(new DirectoryInfo(dir).GetDirectories());
        ds.Sort(delegate(DirectoryInfo x, DirectoryInfo y) { return string.CompareOrdinal(y.Name, x.Name); });
        int n = Math.Min(limit, ds.Count);
        for (int i = 0; i < n; i++)
        {
            string wav = Path.Combine(ds[i].FullName, "render.wav");
            long sz = File.Exists(wav) ? new FileInfo(wav).Length : 0;
            Console.WriteLine(ds[i].Name + "  " + (sz > 0 ? sz + " B" : "无 wav"));
        }
        if (ds.Count > n) Console.WriteLine("… 共 " + ds.Count + " 次");
        return 0;
    }

    static int Clean(List<string> a)
    {
        int? keep = null;
        for (int i = 0; i + 1 < a.Count; i++) if (a[i] == "--keep") keep = (int)ParseD(a[i + 1]);
        if (keep.HasValue) gCfg.@switch.keepRuns = keep.Value;
        int removed = CleanRuns(gCfg.@switch.keepRuns);
        if (!gCfg.@switch.keepTempFiles) ClearDir(Abs(Resolve(gCfg.paths.tempDir, "tmp")));
        Console.WriteLine("cleaned " + removed + " run(s), keeping " + gCfg.@switch.keepRuns);
        return 0;
    }

    static int ConfigCmd(List<string> a)
    {
        if (a.Count == 0 || a[0] == "show")
        {
            if (gJson) { Console.WriteLine(JsonOf(gCfg)); return 0; }
            Console.WriteLine("config: " + gCfgPath);
            Console.WriteLine();
            Console.Write(JsonOf(gCfg));
            return 0;
        }
        if (a[0] == "path") { Console.WriteLine(gCfgPath); return 0; }
        if (a[0] == "set")
        {
            int n = 0;
            for (int i = 1; i < a.Count; i++) { if (SetKey(a[i])) n++; else Console.Error.WriteLine("未知键: " + a[i]); }
            SaveConfig();
            Console.WriteLine("已更新 " + n + " 项 -> " + gCfgPath);
            return 0;
        }
        if (a[0] == "reset")
        {
            gCfg = new Config(); Normalize(gCfg); SaveConfig();
            Console.WriteLine("已恢复默认配置（" + gCfgPath + "）");
            return 0;
        }
        if (a[0] == "wizard")
        {
            gCfg = new Config(); Normalize(gCfg);
            gChanged = true; Wizard(); return 0;
        }
        Console.Error.WriteLine("用法: config show | config set k=v | config reset | config wizard | config path");
        return 1;
    }

    static bool SetKey(string kv)
    {
        int eq = kv.IndexOf('=');
        if (eq <= 0) return false;
        string k = kv.Substring(0, eq).Trim();
        string v = kv.Substring(eq + 1).Trim();
        switch (k)
        {
            case "talk.engine": gCfg.talk.engine = v; break;
            case "talk.host": gCfg.talk.host = v; break;
            case "talk.cast": gCfg.talk.cast = v; break;
            case "talk.speaker": gCfg.talk.speaker = int.Parse(v); break;
            case "talk.tempo": gCfg.talk.tempo = ParseD(v); break;
            case "talk.splitMode": gCfg.talk.splitMode = v; break;
            case "talk.lyricsMode": gCfg.talk.lyricsMode = v; break;
            case "talk.timeoutSeconds": gCfg.talk.timeoutSeconds = int.Parse(v); break;
            case "talk.autoPlay": gCfg.talk.autoPlay = v == "true" || v == "1"; break;
            case "output.engine": gCfg.output.engine = v; break;
            case "output.cast": gCfg.output.cast = v; break;
            case "output.castId": gCfg.output.castId = v; break;
            case "output.castVersion": gCfg.output.castVersion = v; break;
            case "output.alpha": gCfg.output.alpha = ParseD(v); break;
            case "output.voiceModel": gCfg.output.voiceModel = v; break;
            case "output.voiceSource": gCfg.output.voiceSource = (v == "json" ? "json" : "registry"); break;
            case "output.voiceList": gCfg.output.voiceList = v; break;
            case "output.dictionary": gCfg.output.dictionary = v; break;
            case "paths.cevioDir": gCfg.paths.cevioDir = v; break;
            case "paths.outputDir": gCfg.paths.outputDir = v; break;
            case "paths.runDir": gCfg.paths.runDir = v; break;
            case "paths.tempDir": gCfg.paths.tempDir = v; break;
            case "paths.nodoAmeApi": gCfg.paths.nodoAmeApi = v; break;
            case "paths.renderExe": gCfg.paths.renderExe = v; break;
            case "paths.template": gCfg.paths.template = v; break;
            case "switch.trimStartSilence": gCfg.@switch.trimStartSilence = v == "true" || v == "1"; break;
            case "switch.trimEndSilence": gCfg.@switch.trimEndSilence = v == "true" || v == "1"; break;
            case "switch.trimThresholdDb": gCfg.@switch.trimThresholdDb = ParseD(v); break;
            case "switch.keepRuns": gCfg.@switch.keepRuns = int.Parse(v); break;
            case "switch.keepTempFiles": gCfg.@switch.keepTempFiles = v == "true" || v == "1"; break;
            case "switch.showTiming": gCfg.@switch.showTiming = v == "true" || v == "1"; break;
            case "switch.autoPlayOnRender": gCfg.@switch.autoPlayOnRender = v == "true" || v == "1"; break;
            default: return false;
        }
        return true;
    }

    // ================= 声库（registry / json 两种来源） =================
    public class VoiceEntry
    {
        public string name = "";
        public string castId = "";
        public string version = "";
        public string folder = "";
        public string path = "";      // voices.json 里的键名
        public string model = "";
        public string from = "";
    }

    const string REG_PATH = @"SOFTWARE\CeVIO_NV\Subject\VocalSource\Song";
    const string REG_PRODUCT = @"SOFTWARE\CeVIO_NV\Product";

    static List<VoiceEntry> AllVoices()
    {
        return gCfg.output.voiceSource == "json" ? JsonVoices() : RegistryVoices();
    }

    static List<VoiceEntry> RegistryVoices()
    {
        List<VoiceEntry> list = new List<VoiceEntry>();
        try
        {
            Microsoft.Win32.RegistryKey baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(REG_PATH);
            if (baseKey == null) baseKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(REG_PATH);
            if (baseKey == null) return list;
            foreach (string k in baseKey.GetSubKeyNames())
            {
                Microsoft.Win32.RegistryKey vk = baseKey.OpenSubKey(k);
                if (vk == null) continue;
                VoiceEntry e = new VoiceEntry();
                e.from = "registry";
                e.castId = Regex.Replace(k, @"\s*\(x64\)\s*$", "", RegexOptions.IgnoreCase).Trim();
                e.folder = Convert.ToString(vk.GetValue("InstallFolder", ""));
                e.version = Convert.ToString(vk.GetValue("Version", ""));
                e.model = FindVoiceModel(e.folder);
                e.name = ProductNameOf(vk.GetValue("PackageCode"));
                if (string.IsNullOrEmpty(e.name)) e.name = e.castId;
                if (!string.IsNullOrEmpty(e.model)) list.Add(e);
            }
        }
        catch (Exception ex) { Console.Error.WriteLine("读取注册表失败: " + ex.Message); }
        list.Sort(delegate(VoiceEntry x, VoiceEntry y) { return string.CompareOrdinal(x.name, y.name); });
        return list;
    }

    static string FindVoiceModel(string folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return "";
        try
        {
            string[] f = Directory.GetFiles(folder, "*.tsnvoice", SearchOption.AllDirectories);
            if (f.Length > 0) return f[0];
        }
        catch (Exception) { }
        return "";
    }

    // PackageCode 是 GUID 的原始 16 字节（小端），转成 {XXXXXXXX-....} 后去 Product 键下取 ProductName
    static string ProductNameOf(object packageCode)
    {
        byte[] b = packageCode as byte[];
        if (b == null || b.Length != 16) return null;
        string guid = "{" + new Guid(b).ToString() + "}";
        try
        {
            foreach (Microsoft.Win32.RegistryKey root in new Microsoft.Win32.RegistryKey[] {
                Microsoft.Win32.Registry.LocalMachine.OpenSubKey(REG_PRODUCT),
                Microsoft.Win32.Registry.CurrentUser.OpenSubKey(REG_PRODUCT) })
            {
                if (root == null) continue;
                foreach (string k in root.GetSubKeyNames())
                {
                    if (!k.StartsWith(guid, StringComparison.OrdinalIgnoreCase)) continue;
                    Microsoft.Win32.RegistryKey pk = root.OpenSubKey(k);
                    if (pk == null) continue;
                    string nm = Convert.ToString(pk.GetValue("ProductName", ""));
                    if (nm.Length > 0) return CleanProductName(nm);
                }
            }
        }
        catch (Exception) { }
        return null;
    }

    static string CleanProductName(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = Regex.Replace(s, @"\s*(ソングボイス|Song\s*Voice|ボイスバンク)\s*$", "", RegexOptions.IgnoreCase).Trim();
        return s;
    }

    static List<VoiceEntry> JsonVoices()
    {
        List<VoiceEntry> list = new List<VoiceEntry>();
        string p = Abs(gCfg.output.voiceList);
        if (!File.Exists(p)) { Console.Error.WriteLine("声库名单不存在: " + p + "（可以先运行 /voices --dump 生成模板）"); return list; }
        try
        {
            string json = File.ReadAllText(p, Encoding.UTF8);
            JavaScriptSerializer js = new JavaScriptSerializer();
            VoiceFile vf = js.Deserialize<VoiceFile>(json);
            if (vf == null || vf.voices == null) return list;
            foreach (VoiceEntry e0 in vf.voices)
            {
                if (e0 == null) continue;
                VoiceEntry e = new VoiceEntry();
                e.from = "json";
                e.name = e0.name;
                e.castId = e0.castId;
                e.version = e0.version;
                e.folder = string.IsNullOrEmpty(e0.path) ? e0.folder : e0.path;
                e.model = e0.model;
                if (string.IsNullOrEmpty(e.version)) e.version = "1.0.2";
                if (string.IsNullOrEmpty(e.model) && !string.IsNullOrEmpty(e.folder)) e.model = FindVoiceModel(e.folder);
                if (string.IsNullOrEmpty(e.name)) e.name = string.IsNullOrEmpty(e.castId) ? Path.GetFileNameWithoutExtension(e.model) : e.castId;
                if (!string.IsNullOrEmpty(e.model)) list.Add(e);
            }
        }
        catch (Exception ex) { Console.Error.WriteLine("解析声库名单失败: " + ex.Message); }
        return list;
    }

    public class VoiceFile
    {
        public List<VoiceEntry> voices = new List<VoiceEntry>();
    }

    static string GetStr(Dictionary<string, object> d, string k)
    {
        object v;
        return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : "";
    }

    static void DumpVoiceTemplate()
    {
        List<VoiceEntry> list = RegistryVoices();
        string p = Abs(gCfg.output.voiceList);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"voices\": [");
        for (int i = 0; i < list.Count; i++)
        {
            sb.AppendLine("    {");
            sb.AppendLine("      \"name\": \"" + Esc(list[i].name) + "\",");
            sb.AppendLine("      \"castId\": \"" + Esc(list[i].castId) + "\",");
            sb.AppendLine("      \"version\": \"" + Esc(list[i].version) + "\",");
            sb.AppendLine("      \"path\": \"" + Esc(list[i].folder) + "\",");
            sb.AppendLine("      \"model\": \"" + Esc(list[i].model) + "\"");
            sb.Append("    }").AppendLine(i + 1 < list.Count ? "," : "");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        File.WriteAllText(p, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("已按注册表生成名单模板: " + p + "（" + list.Count + " 个，可自行改 name）");
    }

    static int VoicesCmd(List<string> a)
    {
        if (a.Count > 0 && (a[0] == "--dump" || a[0] == "dump")) { DumpVoiceTemplate(); return 0; }
        List<VoiceEntry> list = AllVoices();
        Console.WriteLine("声库来源: " + gCfg.output.voiceSource + (gCfg.output.voiceSource == "json" ? "  (" + Abs(gCfg.output.voiceList) + ")" : "  (CeVIO 安装注册表)"));
        if (list.Count == 0)
        {
            Console.WriteLine("未找到可用声库。检查 CeVIO AI 是否安装、或用 config set output.voiceSource=json 手工维护名单。");
            return 1;
        }
        for (int i = 0; i < list.Count; i++)
        {
            bool cur = !string.IsNullOrEmpty(gCfg.output.voiceModel) && string.Equals(list[i].model, gCfg.output.voiceModel, StringComparison.OrdinalIgnoreCase);
            Console.WriteLine(string.Format("  {0,2}. {1}{2}  [{3}]  {4}  {5}",
                i + 1, cur ? "* " : "  ", list[i].name, list[i].castId, list[i].version, Path.GetFileName(list[i].model)));
        }
        return 0;
    }

    static int UseCmd(List<string> a)
    {
        string asName = null;
        List<string> keep = new List<string>();
        for (int i = 0; i < a.Count; i++)
        {
            // 兼容 --asかふ / --as=かふ 这种忘记空格的写法
            if (a[i].StartsWith("--as", StringComparison.Ordinal) && a[i].Length > 4)
            {
                asName = a[i].Substring(4).TrimStart('=', ':', ' ');
                if (asName.Length == 0) asName = null;
                if (asName != null) continue;
            }
            if (a[i].StartsWith("-as", StringComparison.Ordinal) && !a[i].StartsWith("--", StringComparison.Ordinal) && a[i].Length > 3)
            {
                asName = a[i].Substring(3).TrimStart('=', ':', ' ');
                if (asName.Length == 0) asName = null;
                if (asName != null) continue;
            }
            if ((a[i] == "--as" || a[i] == "-as") && i + 1 >= a.Count)
            {
                Console.Error.WriteLine("--as 后面要跟显示名，例如: /use 1 --as 我的声库");
                return 1;
            }
            if ((a[i] == "--as" || a[i] == "-as") && i + 1 < a.Count) { asName = a[i + 1]; i++; continue; }
            keep.Add(a[i]);
        }
        if (keep.Count == 0) { Console.Error.WriteLine("用法: /use <编号|名字|CastId> [--as 显示名]"); return 1; }
        string want = string.Join(" ", keep.ToArray()).Trim();
        List<VoiceEntry> list = AllVoices();
        if (list.Count == 0) { Console.Error.WriteLine("没有可用声库（先 /doctor）"); return 1; }
        VoiceEntry hit = null;
        int idx;
        if (want.StartsWith("#")) want = want.Substring(1);
        if (int.TryParse(want, out idx) && idx >= 1 && idx <= list.Count) hit = list[idx - 1];
        if (hit == null) foreach (VoiceEntry e in list) if (string.Equals(e.name, want, StringComparison.OrdinalIgnoreCase) || string.Equals(e.castId, want, StringComparison.OrdinalIgnoreCase)) { hit = e; break; }
        if (hit == null) foreach (VoiceEntry e in list) if (e.name.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0) { hit = e; break; }
        if (hit == null) { Console.Error.WriteLine("没有匹配的声库: " + want + "（用 /voices 查看）"); return 1; }
        bool same = string.Equals(gCfg.output.castId, hit.castId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(gCfg.output.cast);
        string newCast = asName != null ? asName : (same ? gCfg.output.cast : hit.name);
        gCfg.output.cast = newCast;
        gCfg.output.castId = string.IsNullOrEmpty(hit.castId) ? GuessCastId(hit.model) : hit.castId;
        gCfg.output.castVersion = string.IsNullOrEmpty(hit.version) ? gCfg.output.castVersion : hit.version;
        gCfg.output.voiceModel = hit.model;
        SaveConfig();
        Console.WriteLine("已切换输出声库: " + gCfg.output.cast + "  [" + gCfg.output.castId + "]  " + Path.GetFileName(hit.model));
        if (gInUi) Header();
        return 0;
    }

    // ================= doctor =================
    static int Doctor(bool print)
    {
        int bad = 0;
        if (print) Console.WriteLine("依赖检查");
        bad += Check("Talk 引擎 " + gCfg.talk.engine, VoicevoxVersion(gCfg.talk.host) != null, VoicevoxVersion(gCfg.talk.host));
        bad += Check("NodoAmeApi.exe", File.Exists(Abs(Resolve(gCfg.paths.nodoAmeApi, @"bin\NodoAmeApi.exe"))));
        string apiDir = Path.GetDirectoryName(Abs(Resolve(gCfg.paths.nodoAmeApi, @"bin\NodoAmeApi.exe")));
        bad += Check("NodoAme.Core.dll", apiDir != null && File.Exists(Path.Combine(apiDir, "NodoAme.Core.dll")));
        bad += Check("cevio-render2.exe", File.Exists(Abs(Resolve(gCfg.paths.renderExe, @"bin\cevio-render2.exe"))));
        bad += Check("ccst 母版", File.Exists(Abs(Resolve(gCfg.paths.template, @"template\temp.track.xml"))));
        bad += Check("CeVIO SongEditorControl", !string.IsNullOrEmpty(gCfg.paths.cevioDir) && File.Exists(Path.Combine(gCfg.paths.cevioDir, "CeVIO.SongEditorControl.dll")));
        bad += Check("输出声库 .tsnvoice", !string.IsNullOrEmpty(gCfg.output.voiceModel) && File.Exists(gCfg.output.voiceModel));
        int nvb = 0;
        try { nvb = AllVoices().Count; } catch (Exception) { }
        bad += Check("声库来源 " + gCfg.output.voiceSource + " 可用", nvb > 0, nvb + " 个（/voices 查看）");
        bad += Check("Song 词典目录", !string.IsNullOrEmpty(gCfg.output.dictionary) && Directory.Exists(gCfg.output.dictionary));
        foreach (string d in new string[] { gCfg.paths.outputDir, gCfg.paths.runDir, gCfg.paths.tempDir })
        {
            string full = Abs(Resolve(d, null));
            try { EnsureDir(full); } catch (Exception) { bad += Check("可写目录 " + full, false, null); }
        }
        if (print && bad == 0) Console.WriteLine("全部通过。");
        return bad;
    }

    static int Check(string name, bool ok, string extra)
    {
        Console.WriteLine((ok ? "  [ok]   " : "  [缺失] ") + name + (string.IsNullOrEmpty(extra) ? "" : "  (" + extra + ")"));
        return ok ? 0 : 1;
    }

    static int Check(string name, bool ok) { return Check(name, ok, null); }

    static string VoicevoxVersion(string host)
    {
        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(host.TrimEnd('/') + "/version");
            req.Timeout = 1500;
            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            using (StreamReader r = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                return r.ReadToEnd().Trim().Replace("\"", "");
        }
        catch (Exception) { return null; }
    }

    // 读一行输入：
    //  ① 真控制台 → 直接用 ReadConsoleW 拿 UTF-16 字符，绕开任何代码页/字节解码问题；
    //  ② stdin 被重定向（Agent/管道）→ 按字节读，再用多候选解码。
    static string ReadLineRaw()
    {
        if (!Console.IsInputRedirected)
        {
            try
            {
                string s = ReadLineConsole();
                if (s != null)
                {
                    string clean = Sanitize(s);
                    StringBuilder cc = new StringBuilder();
                    for (int i = 0; i < clean.Length && i < 64; i++) cc.Append(((int)clean[i]).ToString("X4")).Append(' ');
                    gLastInput = "console(ReadConsoleW) chars: " + cc;
                    return clean;
                }
            }
            catch (Exception) { }
        }
        Stream s2 = Console.OpenStandardInput();
        List<byte> buf = new List<byte>(256);
        while (true)
        {
            int b;
            try { b = s2.ReadByte(); } catch (Exception) { return null; }
            if (b < 0) break;
            if (b == '\n') break;
            buf.Add((byte)b);
        }
        if (buf.Count == 0) return null;
        byte[] raw = buf.ToArray();
        if (Environment.GetEnvironmentVariable("CEVIO_TALK_DEBUG_INPUT") == "1")
        {
            StringBuilder hx = new StringBuilder();
            for (int i = 0; i < raw.Length && i < 64; i++) hx.Append(raw[i].ToString("X2")).Append(' ');
            Console.Error.WriteLine("[in] " + raw.Length + "B " + hx);
        }
        string dec = DecodeText(raw);
        StringBuilder rb = new StringBuilder();
        for (int i = 0; i < raw.Length && i < 64; i++) rb.Append(raw[i].ToString("X2")).Append(' ');
        gLastInput = "stdin bytes(" + raw.Length + "): " + rb + " -> decoded: " + dec;
        return dec;
    }

    // ================= 交互式行编辑：Tab 补全 + 指令预览 + 历史 =================
    [DllImport("kernel32.dll")]
    static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
    [DllImport("kernel32.dll")]
    static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    static bool gVtTried, gVtOk;
    static List<string> gHistory = new List<string>();
    static int gHistPos = -1;

    // 打开 ANSI 转义（用于整行重绘），失败则退化成普通输出
    static void EnsureVt()
    {
        if (gVtTried) return;
        gVtTried = true;
        try
        {
            IntPtr h = GetStdHandle(-11);
            uint mode;
            if (GetConsoleMode(h, out mode) && SetConsoleMode(h, mode | 0x0004)) gVtOk = true;
        }
        catch (Exception) { gVtOk = false; }
    }

    static void Draw(string prompt, StringBuilder buf)
    {
        if (gVtOk) Console.Write("\x1b[2K\r" + prompt + buf.ToString());
        else Console.Write("\r" + prompt + buf.ToString() + new string(' ', 8) + "\r" + prompt + buf.ToString());
    }

    // 带补全/预览的读行；控制台不可交互时由调用方退回 ReadLineRaw
    static string ReadLineEdit(string prompt)
    {
        EnsureVt();
        StringBuilder buf = new StringBuilder();
        Console.Write(prompt);
        while (true)
        {
            ConsoleKeyInfo k;
            try { k = Console.ReadKey(true); } catch (Exception) { Console.WriteLine(); return null; }
            if (k.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                string s = buf.ToString();
                if (s.Trim().Length > 0) { gHistory.Add(s); gHistPos = gHistory.Count; }
                return s;
            }
            if (k.Key == ConsoleKey.Escape || (k.Key == ConsoleKey.C && (k.Modifiers & ConsoleModifiers.Control) != 0))
            {
                buf.Length = 0; Draw(prompt, buf); continue;
            }
            if (k.Key == ConsoleKey.Backspace)
            {
                if (buf.Length > 0) buf.Length--;
                Draw(prompt, buf); continue;
            }
            if (k.Key == ConsoleKey.UpArrow)
            {
                if (gHistory.Count > 0)
                {
                    if (gHistPos > 0) gHistPos--;
                    buf.Length = 0; buf.Append(gHistory[gHistPos]);
                }
                Draw(prompt, buf); continue;
            }
            if (k.Key == ConsoleKey.DownArrow)
            {
                if (gHistory.Count > 0)
                {
                    if (gHistPos < gHistory.Count - 1) { gHistPos++; buf.Length = 0; buf.Append(gHistory[gHistPos]); }
                    else { gHistPos = gHistory.Count; buf.Length = 0; }
                }
                Draw(prompt, buf); continue;
            }
            if (k.Key == ConsoleKey.Tab) { DoComplete(prompt, buf); continue; }
            if (k.KeyChar >= ' ') { buf.Append(k.KeyChar); Draw(prompt, buf); continue; }
        }
    }

    static void DoComplete(string prompt, StringBuilder buf)
    {
        string text = buf.ToString();
        // 空行或只有 “/” → 列出全部命令与一句话说明（指令预览）
        if (text.Trim().Length == 0 || text.Trim() == "/")
        {
            Console.WriteLine();
            Console.WriteLine("  可用指令（Tab 补全，↑↓ 历史，Esc 清行）：");
            foreach (string c in CMDS) Console.WriteLine("    /" + c.PadRight(9) + " " + CmdDesc(c));
            Console.WriteLine();
            Draw(prompt, buf);
            return;
        }
        int sp = text.LastIndexOf(' ');
        string head = sp < 0 ? "" : text.Substring(0, sp + 1);
        string token = sp < 0 ? text : text.Substring(sp + 1);
        List<string> cand = Candidates(head, token);
        if (cand.Count == 0) { Draw(prompt, buf); return; }
        if (cand.Count == 1)
        {
            buf.Length = 0;
            buf.Append(head).Append(cand[0]);
            string desc = CmdDesc(cand[0].TrimStart('/'));
            if (head.Length == 0) { Console.WriteLine(); Console.WriteLine("    ↳ " + desc); }
            Draw(prompt, buf);
            return;
        }
        string common = CommonPrefix(cand);
        if (common.Length > token.Length) { buf.Length = 0; buf.Append(head).Append(common); }
        Console.WriteLine();
        Console.WriteLine("  " + string.Join("  ", cand.ToArray()));
        Draw(prompt, buf);
    }

    static string CommonPrefix(List<string> a)
    {
        string p = a[0];
        for (int i = 1; i < a.Count; i++)
        {
            int n = 0;
            while (n < p.Length && n < a[i].Length && p[n] == a[i][n]) n++;
            p = p.Substring(0, n);
        }
        return p;
    }

    // 依据“已输入的命令 + 当前令牌”给出候选
    static List<string> Candidates(string head, string token)
    {
        List<string> r = new List<string>();
        string h = head.Trim();
        if (h.Length == 0)
        {
            foreach (string c in CMDS) if (("/" + c).StartsWith(token, StringComparison.OrdinalIgnoreCase)) r.Add("/" + c);
            return r;
        }
        if (h == "/use" || h == "/cast")
        {
            List<VoiceEntry> vs = AllVoices();
            for (int i = 0; i < vs.Count; i++)
            {
                string idx = (i + 1).ToString();
                if (idx.StartsWith(token) || vs[i].name.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                    r.Add(vs[i].name.Replace(' ', '_'));
            }
            return r;
        }
        if (h == "/config" || h == "/cfg")
        {
            foreach (string c in new string[] { "show", "set", "reset", "wizard", "path" })
                if (c.StartsWith(token, StringComparison.OrdinalIgnoreCase)) r.Add(c);
            return r;
        }
        if (h == "/config set" || h == "/cfg set")
        {
            foreach (string key in CONFIG_KEYS) if (key.StartsWith(token, StringComparison.OrdinalIgnoreCase)) r.Add(key + "=");
            return r;
        }
        if (h == "/switch")
        {
            foreach (string key in CONFIG_KEYS) if (key.StartsWith("switch.", StringComparison.Ordinal) && key.StartsWith(token, StringComparison.OrdinalIgnoreCase)) r.Add(key.Substring(7) + "=");
            return r;
        }
        if (h == "/voice")
        {
            foreach (string key in CONFIG_KEYS) if (key.StartsWith("output.", StringComparison.Ordinal) && key.StartsWith(token, StringComparison.OrdinalIgnoreCase)) r.Add(key.Substring(7) + "=");
            return r;
        }
        if (h == "/play" || h == "/playlast")
        {
            string dir = Abs(Resolve(gCfg.paths.outputDir, "output"));
            if (Directory.Exists(dir))
                foreach (string f in Directory.GetFiles(dir, "*.wav"))
                    if (Path.GetFileName(f).StartsWith(token, StringComparison.OrdinalIgnoreCase)) r.Add(Path.GetFileName(f));
            return r;
        }
        if (h == "/clean")
        {
            if ("--keep".StartsWith(token, StringComparison.OrdinalIgnoreCase)) r.Add("--keep");
            return r;
        }
        if (h == "/runs")
        {
            if ("--limit".StartsWith(token, StringComparison.OrdinalIgnoreCase)) r.Add("--limit");
            return r;
        }
        return r;
    }

    static readonly string[] CONFIG_KEYS = new string[] {
        "talk.engine","talk.host","talk.cast","talk.speaker","talk.tempo","talk.splitMode","talk.lyricsMode",
        "talk.timeoutSeconds","talk.autoPlay","output.engine","output.cast","output.castId","output.castVersion",
        "output.alpha","output.voiceModel","output.voiceSource","output.voiceList","output.dictionary",
        "paths.cevioDir","paths.outputDir","paths.runDir","paths.tempDir","paths.nodoAmeApi","paths.renderExe","paths.template",
        "switch.trimStartSilence","switch.trimEndSilence","switch.trimThresholdDb","switch.keepRuns",
        "switch.keepTempFiles","switch.showTiming","switch.autoPlayOnRender"
    };

    static string CmdDesc(string c)
    {
        switch (c)
        {
            case "say": case "talk": return "合成文字（带参数时用，如 /say 文本 --no-trim）";
            case "voices": case "vb": return "列出可用歌声库（--dump 导出 voices.json）";
            case "use": case "cast": return "切换输出声库：/use <编号|名字> [--as 显示名]";
            case "play": case "playlast": case "replay": return "播放刚生成的（或指定）wav";
            case "runs": return "查看最近运行记录";
            case "clean": return "清理旧的中间产物（默认保留最近 5 次）";
            case "config": case "cfg": return "查看/修改配置：show|set k=v|reset|wizard|path";
            case "doctor": return "依赖体检（声库、词典、VOICEVOX、目录）";
            case "voice": return "改输出角色设置：/voice cast=<显示名>";
            case "switch": return "改开关：/switch autoPlayOnRender=true";
            case "version": return "显示版本";
            case "help": return "显示命令帮助";
            case "exit": return "退出";
            default: return "";
        }
    }



    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool ReadConsoleW(IntPtr hConsoleInput, StringBuilder lpBuffer, int nNumberOfCharsToRead, out int lpNumberOfCharsRead, IntPtr pInputControl);

    static string ReadLineConsole()
    {
        const int STD_INPUT_HANDLE = -10;
        IntPtr h = GetStdHandle(STD_INPUT_HANDLE);
        if (h == IntPtr.Zero || h == new IntPtr(-1)) return null;
        StringBuilder acc = new StringBuilder();
        while (true)
        {
            StringBuilder sb = new StringBuilder(512);
            int n;
            if (!ReadConsoleW(h, sb, 512, out n, IntPtr.Zero)) return acc.Length > 0 ? acc.ToString() : null;
            if (n <= 0) return acc.Length > 0 ? acc.ToString() : null;
            string part = sb.ToString(0, n);
            int nl = part.IndexOf('\n');
            if (nl >= 0) { acc.Append(part.Substring(0, nl)); break; }
            acc.Append(part);
            if (part.Length < 512) break;
        }
        return acc.ToString();
    }

    // 多候选解码 + 打分。某些终端（ConPTY / 输入 CP=1200）送来的是 UTF-16LE，
    // 按 GBK 解会得到含控制字符的垃圾，直接写进 XML 会抛 “十六进制值 0x02 是无效字符”。
    static string DecodeText(byte[] raw)
    {
        // 1) 优先严格 UTF-8：GBK/Shift-JIS 的字节在严格模式下会抛异常，能解出来基本就是真的 UTF-8
        try
        {
            string u = new UTF8Encoding(false, true).GetString(raw).TrimStart('\uFEFF');
            if (!HasControl(u)) return Sanitize(u);
        }
        catch (Exception) { }
        string best = null;
        int bestScore = int.MinValue;
        List<KeyValuePair<string, string>> cand = new List<KeyValuePair<string, string>>();
        int[] cps = new int[] { 936, 932, 1200, 1201 };
        for (int i = 0; i < cps.Length; i++)
        {
            if ((cps[i] == 1200 || cps[i] == 1201) && (raw.Length % 2) != 0) continue;
            try { cand.Add(new KeyValuePair<string, string>("cp" + cps[i], Encoding.GetEncoding(cps[i]).GetString(raw))); }
            catch (Exception) { }
        }
        cand.Add(new KeyValuePair<string, string>("utf8loose", Encoding.UTF8.GetString(raw)));
        foreach (KeyValuePair<string, string> kv in cand)
        {
            string v = kv.Value.TrimStart('\uFEFF');
            if (HasControl(v)) continue;
            int sc = Score(v);
            if (sc > bestScore) { bestScore = sc; best = v; }
        }
        return Sanitize(best);
    }

    static bool HasControl(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c < 0x20 && c != '\t' && c != '\r' && c != '\n') return true;
        }
        return false;
    }

    static int Score(string s)
    {
        if (s == null) return int.MinValue;
        int sc = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c < 0x20) { sc -= 8; continue; }                 // 控制字符：解码错误的强信号
            if (c == '\uFFFD') { sc -= 10; continue; }
            if (c >= 0x3041 && c <= 0x30FF) { sc += 4; continue; }  // 平假名/片假名
            if (c >= 0x4E00 && c <= 0x9FFF) { sc += 3; continue; }  // 汉字
            if (c >= 0x3000 && c <= 0x303F) { sc += 3; continue; }  // 日文标点
            if (c >= 0x20 && c < 0x7F) { sc += 1; continue; }
            if (c >= 0xFF01 && c <= 0xFF60) { sc += 2; continue; }  // 全角
            sc -= 1;
        }
        return sc;
    }

    static string Sanitize(string s)
    {
        if (s == null) return "";
        StringBuilder b = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c < 0x20 && c != '\t') continue;
            if (c == '\uFFFD') continue;
            b.Append(c);
        }
        return b.ToString().Trim();
    }

    // ================= ui =================
    static int Ui()
    {
        gInUi = true;
        Header();
        while (true)
        {
            Console.Write("");
            string line;
            if (!Console.IsInputRedirected && Environment.GetEnvironmentVariable("CEVIO_TALK_NO_EDITOR") != "1")
                line = ReadLineEdit("> ");
            else
            {
                Console.Write("> ");
                line = ReadLineRaw();
            }
            if (line == null) break;
            line = line.Trim();
            if (line.Length == 0) continue;
            if (!line.StartsWith("/", StringComparison.Ordinal))
            {
                // 没有 / 前缀 → 整行都是 talk 文本
                Say(new List<string>(new string[] { line }));
                continue;
            }
            List<string> parts = Tokenize(line.Substring(1));
            if (parts.Count == 0) continue;
            string c = parts[0].ToLowerInvariant();
            List<string> rest = parts.GetRange(1, parts.Count - 1);
            if (c == "exit" || c == "quit" || c == "q") break;
            if (c == "help" || c == "h" || c == "?") { Help(); continue; }
            try
            {
                if (c == "say" || c == "talk" || c == "s") Say(rest);
                else if (c == "voices" || c == "voicebanks" || c == "vb") VoicesCmd(rest);
                else if (c == "use" || c == "cast") UseCmd(rest);
                else if (c == "play" || c == "p" || c == "playlast" || c == "replay") Play(rest);
                else if (c == "runs" || c == "ls") Runs(rest);
                else if (c == "clean") Clean(rest);
                else if (c == "config" || c == "cfg") { ConfigCmd(rest); Header(); }
                else if (c == "doctor") Doctor(true);
                else if (c == "voice")
                {
                    int n = 0;
                    foreach (string kv in rest) if (SetKey(kv.StartsWith("output.", StringComparison.Ordinal) ? kv : "output." + kv)) n++;
                    SaveConfig(); Console.WriteLine("输出角色设置已更新 " + n + " 项"); Header();
                }
                else if (c == "switch")
                {
                    int n = 0;
                    foreach (string kv in rest) if (SetKey(kv.StartsWith("switch.", StringComparison.Ordinal) ? kv : "switch." + kv)) n++;
                    SaveConfig(); Console.WriteLine("开关已更新 " + n + " 项");
                }
                else if (c == "version" || c == "ver") Console.WriteLine(APP + " " + VERSION);
                else
                {
                    string hint = Suggest(c);
                    Console.WriteLine("未知命令 /" + c + (hint != null ? "，是不是想输入 /" + hint + "？" : "") + "（/help 查看）");
                }
            }
            catch (Exception ex) { Console.WriteLine("错误: " + ex.Message); }
        }
        return 0;
    }

    static List<string> Tokenize(string line)
    {
        List<string> r = new List<string>();
        StringBuilder cur = new StringBuilder();
        bool q = false;
        foreach (char ch in line)
        {
            if (ch == '"') { q = !q; continue; }
            if (!q && (ch == ' ' || ch == '\t')) { if (cur.Length > 0) { r.Add(cur.ToString()); cur.Length = 0; } continue; }
            cur.Append(ch);
        }
        if (cur.Length > 0) r.Add(cur.ToString());
        return r;
    }

    static readonly string[] CMDS = new string[] { "say", "talk", "voices", "vb", "use", "cast", "play", "playlast",
        "replay", "runs", "clean", "config", "cfg", "doctor", "voice", "switch", "version", "help", "exit" };

    // 把写错的命令（例如 /ues）提示成最接近的正确命令
    static string Suggest(string c)
    {
        string best = null;
        int bestD = 3;   // 只提示差异 ≤2 的
        foreach (string cmd in CMDS)
        {
            int d = EditDistance(c, cmd);
            if (d < bestD) { bestD = d; best = cmd; }
        }
        return best;
    }

    static int EditDistance(string a, string b)
    {
        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        return d[a.Length, b.Length];
    }

    static void Help()
    {
        Console.WriteLine(APP + " " + VERSION + "  文字直接转 WAV（交互里命令以 / 开头，没有 / 就是 talk 文本）");
        Console.WriteLine("  直接输入文字                  合成 WAV，文件名 = 声库名-前5字-时长");
        Console.WriteLine("  /say <文本> [--out x.wav] [--play] [--ccst-only] [--trim|--no-trim]");
        Console.WriteLine("  /voices [--dump]               列出可用声库（--dump 按注册表导出 voices.json 名单）");
        Console.WriteLine("  /use <编号|名字|CastId>        切换输出声库");
        Console.WriteLine("  /play [wav]（别名 /playlast /replay）  /runs  /clean  /doctor  /config show|set k=v|reset|wizard|path");
        Console.WriteLine("  /version  /help  /exit");
        Console.WriteLine("  声库来源开关: config set output.voiceSource=registry|json（默认 registry）");
        Console.WriteLine("  命令行方式: " + APP + " \"/say 文本\"  或  " + APP + " \"文本\"   + --json 输出单行结果");
    }

    // ================= helpers =================
    static string ReadTextFile(string p)
    {
        try { return File.ReadAllText(p, Encoding.UTF8).Trim(); }
        catch (Exception ex) { Console.Error.WriteLine("读取文本失败: " + ex.Message); return null; }
    }

    static string Resolve(string configured, string dflt)
    {
        if (!string.IsNullOrEmpty(configured)) return configured;
        return dflt;
    }

    static string Abs(string p)
    {
        if (string.IsNullOrEmpty(p)) return gRoot;
        return Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(gRoot, p));
    }

    static void EnsureDir(string d)
    {
        if (!string.IsNullOrEmpty(d) && !Directory.Exists(d)) Directory.CreateDirectory(d);
    }

    static string SafeName(string s)
    {
        if (s == null) return "talk";
        StringBuilder sb = new StringBuilder();
        foreach (char ch in s)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length == 0 || sb[sb.Length - 1] != '_') sb.Append('_');
            if (sb.Length >= 24) break;
        }
        string r = sb.ToString().Trim('_');
        return r.Length == 0 ? "talk" : r;
    }

    // 自动文件名：声库名-前5个语音字符-时长
    static string AutoFileName(string text, double dur)
    {
        string cast = !string.IsNullOrEmpty(gCfg.output.cast) ? gCfg.output.cast
                    : (!string.IsNullOrEmpty(gCfg.output.castId) ? gCfg.output.castId : "CeVIO");
        StringBuilder w = new StringBuilder();
        foreach (char ch in text)
        {
            if (char.IsLetterOrDigit(ch)) { w.Append(ch); if (w.Length >= 5) break; }
        }
        if (w.Length == 0) w.Append("talk");
        return FileSafe(cast) + "-" + w + "-" + dur.ToString("F2", CultureInfo.InvariantCulture) + "s";
    }

    static string FileSafe(string s)
    {
        char[] bad = Path.GetInvalidFileNameChars();
        StringBuilder sb = new StringBuilder();
        foreach (char ch in s) sb.Append(Array.IndexOf(bad, ch) >= 0 ? '_' : ch);
        string r = sb.ToString().Trim();
        return r.Length == 0 ? "CeVIO" : r;
    }

    static double ParseD(string s)
    {
        double d;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 0;
    }

    static void LogLine(StringBuilder sb, string s) { sb.AppendLine(s); }

    static void WriteLog(string path, StringBuilder sb)
    {
        try { File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false)); } catch (Exception) { }
    }

    static string Q(string s) { return "\"" + Esc(s) + "\""; }

    static int Fail(string run, StringBuilder logBuf, string log, string msg, int code)
    {
        LogLine(logBuf, "ERROR: " + msg);
        WriteLog(log, logBuf);
        if (gJson) Console.WriteLine("{\"ok\":false,\"error\":" + Q(msg) + ",\"log\":" + Q(log) + ",\"run\":" + Q(run) + ",\"exit\":" + code + "}");
        else Console.Error.WriteLine("ERROR: " + msg + "\n（日志: " + log + "）");
        return code;
    }

    static int CleanRuns(int keep)
    {
        string dir = Abs(Resolve(gCfg.paths.runDir, "runs"));
        if (!Directory.Exists(dir)) return 0;
        if (keep < 1) keep = 1;
        List<DirectoryInfo> ds = new List<DirectoryInfo>();
        foreach (DirectoryInfo d in new DirectoryInfo(dir).GetDirectories())
            if (Regex.IsMatch(d.Name, @"^\d{8}_\d{6}_\d{3}$")) ds.Add(d);   // 只碰本工具产生的运行目录
        ds.Sort(delegate(DirectoryInfo x, DirectoryInfo y) { return string.CompareOrdinal(x.Name, y.Name); });
        int removed = 0;
        while (ds.Count > keep)
        {
            try { ds[0].Delete(true); removed++; } catch (Exception) { break; }
            ds.RemoveAt(0);
        }
        return removed;
    }

    static void ClearDir(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (FileInfo f in new DirectoryInfo(dir).GetFiles()) { try { f.Delete(); } catch (Exception) { } }
        foreach (DirectoryInfo d in new DirectoryInfo(dir).GetDirectories()) { try { d.Delete(true); } catch (Exception) { } }
    }

    static int Exec(string exe, string workDir, List<string> args, Dictionary<string, string> env,
                    int timeoutSec, StringBuilder logBuf, string tag)
    {
        ProcessStartInfo psi = new ProcessStartInfo(exe);
        psi.WorkingDirectory = workDir;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.StandardOutputEncoding = new UTF8Encoding(false);
        psi.StandardErrorEncoding = new UTF8Encoding(false);
        foreach (string a in args) psi.Arguments += " " + Quote(a);
        if (env != null) foreach (KeyValuePair<string, string> kv in env) psi.EnvironmentVariables[kv.Key] = kv.Value;
        using (Process p = new Process())
        {
            p.StartInfo = psi;
            StringBuilder o = new StringBuilder();
            StringBuilder e = new StringBuilder();
            p.OutputDataReceived += delegate(object s, DataReceivedEventArgs ev) { if (ev.Data != null) o.AppendLine(ev.Data); };
            p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs ev) { if (ev.Data != null) e.AppendLine(ev.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            bool done = p.WaitForExit(timeoutSec * 1000);
            if (!done) { try { p.Kill(); } catch (Exception) { } }
            try { p.WaitForExit(2000); } catch (Exception) { }
            string so = o.ToString(), se = e.ToString();
            if (so.Length > 0) logBuf.Append("[" + tag + "] ").AppendLine(so);
            if (se.Length > 0) logBuf.Append("[" + tag + "] ").AppendLine(se);
            logBuf.AppendLine("[" + tag + "] exit " + (done ? p.ExitCode : -99));
            return done ? p.ExitCode : -99;
        }
    }

    static string Quote(string s)
    {
        if (s == null) return "\"\"";
        StringBuilder b = new StringBuilder("\"");
        int bs = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\') { bs++; b.Append(c); continue; }
            if (c == '"') { b.Append('\\', bs + 1).Append('"'); bs = 0; continue; }
            bs = 0;
            b.Append(c);
        }
        if (bs > 0) b.Append('\\', bs);      // 结尾反斜杠加倍，避免吃掉右引号
        b.Append('"');
        return b.ToString();
    }

    static void PlayWav(string p)
    {
        try { new System.Media.SoundPlayer(p).PlaySync(); }
        catch (Exception ex) { Console.Error.WriteLine("播放失败: " + ex.Message); }
    }

    static void ReadWavInfo(string path, out double peak, out double dur, out int rate)
    {
        peak = 0; dur = 0; rate = 48000;
        byte[] b = File.ReadAllBytes(path);
        int i = 12, ch = 1, off = -1;
        while (i < b.Length - 8)
        {
            string id = Encoding.ASCII.GetString(b, i, 4);
            int sz = BitConverter.ToInt32(b, i + 4);
            if (id == "fmt ") { ch = BitConverter.ToInt16(b, i + 10); rate = BitConverter.ToInt32(b, i + 12); }
            else if (id == "data") { off = i + 8; break; }
            i += 8 + sz;
        }
        if (off < 0) return;
        int n = (b.Length - off) / (2 * ch);
        dur = n / (double)rate;
        for (int k = 0; k < n; k++)
        {
            short v = BitConverter.ToInt16(b, off + k * 2 * ch);
            if (Math.Abs((int)v) > peak) peak = Math.Abs((int)v);
        }
    }

    static long TrimWav(string src, string dst, bool head, bool tail, double threshDb)
    {
        byte[] b = File.ReadAllBytes(src);
        int i = 12, ch = 1, rate = 48000, bits = 16, off = -1;
        while (i < b.Length - 8)
        {
            string id = Encoding.ASCII.GetString(b, i, 4);
            int sz = BitConverter.ToInt32(b, i + 4);
            if (id == "fmt ")
            {
                ch = BitConverter.ToInt16(b, i + 10);
                rate = BitConverter.ToInt32(b, i + 12);
                bits = BitConverter.ToInt16(b, i + 22);
            }
            else if (id == "data") { off = i + 8; break; }
            i += 8 + sz;
        }
        if (off < 0 || bits != 16) { File.Copy(src, dst, true); return 0; }
        int n = (b.Length - off) / (2 * ch);
        short[] s = new short[n];
        int peak = 0;
        for (int k = 0; k < n; k++)
        {
            s[k] = BitConverter.ToInt16(b, off + k * 2 * ch);
            if (Math.Abs((int)s[k]) > peak) peak = Math.Abs((int)s[k]);
        }
        int gate = (int)(peak * Math.Pow(10.0, threshDb / 20.0));
        if (gate < 32) gate = 32;
        int pad = Math.Max(1, rate * 15 / 1000);   // 两侧各留 15 ms，避免啃掉辅音起音与尾音
        int a = 0, z = n - 1;
        if (head) { while (a < n && Math.Abs((int)s[a]) <= gate) a++; }
        if (tail) { while (z > a && Math.Abs((int)s[z]) <= gate) z--; }
        a -= pad; if (a < 0) a = 0;
        z += pad; if (z > n - 1) z = n - 1;        if (a > z) { a = 0; z = n - 1; }
        int len = z - a + 1;
        byte[] pcm = new byte[len * 2 * ch];
        Buffer.BlockCopy(b, off + a * 2 * ch, pcm, 0, pcm.Length);
        using (BinaryWriter w = new BinaryWriter(File.Create(dst)))
        {
            w.Write(new char[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + pcm.Length);
            w.Write(new char[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16); w.Write((short)1); w.Write((short)ch);
            w.Write(rate); w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
            w.Write(new char[] { 'd', 'a', 't', 'a' });
            w.Write(pcm.Length);
            w.Write(pcm);
        }
        return n - len;
    }
}
