// cevio-render2 - render a CeVIO .ccst score to WAV with the locally installed
// CeVIO AI song editor API (SongEditorControl) on a headless STA thread:
//   ChangeSong -> SetSongXml(<Song>) -> ChangeVoice
//   -> GetTimingParameter/GetParameter -> GetSongSequence -> 16-bit WAV
// Requires a legally installed CeVIO AI on the same machine. Ships no CeVIO code.
// usage: cevio-render2.exe <in.ccst> <out.wav> --voice <p.tsnvoice> [--cevio <dir>] [--dic <dir>] [--alpha 0.55] [--version 1.0.2]
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml.Linq;

public class CeVioRender2
{
    static string CEVIO = "";
    const string DEF_ALPHA = "0.55";
    const string DEF_VERSION = "1.0.2";

    static List<double> gSamples = new List<double>();
    static List<double> gPcm = new List<double>();
    static volatile bool gEnded = false;
    static volatile bool gOk = false;
    static int gRate = 48000;
    static string gIn, gOut, gVoice, gDic, gAlpha, gVersion;

    static void Log(string s) { Console.WriteLine(s); Console.Out.Flush(); }

    static string ResolveCevio(string cli)
    {
        if (!string.IsNullOrEmpty(cli)) return cli.TrimEnd('\\');
        string[] keys = new string[] { @"SOFTWARE\CeVIO_NV\Subject\Editor\x64", @"SOFTWARE\CeVIO_NV\Subject\Editor" };
        foreach (string k in keys)
        {
            try
            {
                Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(k);
                if (rk == null) rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(k);
                if (rk == null) continue;
                string dir = Convert.ToString(rk.GetValue("InstallFolder", ""));
                if (dir.Length > 0 && Directory.Exists(dir)) return dir.TrimEnd('\\');
            }
            catch (Exception) { }
        }
        return "";
    }

    static object ScoreOf(object ctl, Guid track)
    {
        System.Collections.IDictionary dict = (System.Collections.IDictionary)ctl.GetType().GetProperty("ScoreDictionary").GetValue(ctl, null);
        foreach (System.Collections.DictionaryEntry kv in dict) return kv.Value;
        return null;
    }

    static void Pump(int ms)
    {
        DateTime end = DateTime.Now.AddMilliseconds(ms);
        while (DateTime.Now < end)
        {
            System.Windows.Threading.DispatcherFrame frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate { frame.Continue = false; }));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            Thread.Sleep(5);
        }
    }

    public static int Main(string[] a)
    {
        try
        {
            if (a.Length < 2) { Console.Error.WriteLine("usage: cevio-render2.exe <in.ccst> <out.wav> --voice <p.tsnvoice> [--cevio <dir>] [--dic <dir>] [--alpha 0.55] [--version 1.0.2]"); return 1; }
            gIn = a[0]; gOut = a[1];
            string cevioArg = null;
            gAlpha = DEF_ALPHA; gVersion = DEF_VERSION;
            for (int i = 2; i + 1 < a.Length; i += 2)
            {
                if (a[i] == "--voice") gVoice = a[i + 1];
                else if (a[i] == "--dic") gDic = a[i + 1];
                else if (a[i] == "--alpha") gAlpha = a[i + 1];
                else if (a[i] == "--version") gVersion = a[i + 1];
                else if (a[i] == "--cevio") cevioArg = a[i + 1];
            }
            CEVIO = ResolveCevio(cevioArg);
            if (string.IsNullOrEmpty(CEVIO) || !Directory.Exists(CEVIO)) { Console.Error.WriteLine("CeVIO AI not found: pass --cevio <dir> containing CeVIO.SongEditorControl.dll"); return 6; }
            if (string.IsNullOrEmpty(gVoice)) { Console.Error.WriteLine("missing --voice <*.tsnvoice>"); return 6; }
            if (string.IsNullOrEmpty(gDic)) gDic = Path.Combine(CEVIO, @"Configuration\Dictionary\Song");

            int rc = 1;
            Thread t = new Thread(delegate()
            {
                try { rc = Body(); }
                catch (Exception ex)
                {
                    Exception e2 = ex; int d = 0;
                    while (e2 != null && d < 6) { Log("EX[" + d + "]: " + e2.GetType().FullName + ": " + e2.Message); e2 = e2.InnerException; d++; }
                    rc = 2;
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            return rc;
        }
        catch (Exception ex) { Console.Error.WriteLine("FATAL: " + ex.Message); return 3; }
    }

    static int Body()
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name;
            string p = Path.Combine(CEVIO, name + ".dll");
            if (File.Exists(p)) return Assembly.LoadFrom(p);
            return null;
        };
        Directory.SetCurrentDirectory(CEVIO);

        XElement root = XElement.Load(gIn);
        XElement units = root.Element("Units");
        if (units == null) { Log("ccst: no <Units>"); return 4; }
        XElement unit = null, songEl = null;
        foreach (XElement u in units.Elements("Unit"))
        {
            XElement s = u.Element("Song");
            if (s != null) { unit = u; songEl = s; break; }
        }
        if (songEl == null) { Log("ccst: no <Song> in Units/Unit"); return 5; }
        Log("ccst=" + Path.GetFileName(gIn) + " castId=" + (string)unit.Attribute("CastId") + " duration=" + (string)unit.Attribute("Duration"));

        Assembly asm = Assembly.LoadFrom(Path.Combine(CEVIO, "CeVIO.SongEditorControl.dll"));
        Type ctlType = asm.GetType("CeVIO.SongEditorControl.SongEditorControl");
        if (ctlType == null) { Log("SongEditorControl type missing"); return 6; }

        if (System.Windows.Application.Current == null)
        {
            System.Windows.Application app = new System.Windows.Application();
            System.Windows.Window win = new System.Windows.Window();
            app.MainWindow = win;
        }

        object ctl = Activator.CreateInstance(ctlType);
        Guid track = Guid.NewGuid();
        ctlType.GetMethod("ChangeSong").Invoke(ctl, new object[] { track });
        int sr = (int)ctlType.GetMethod("SetSongXml").Invoke(ctl, new object[] { track, songEl, true });
        Log("SetSongXml=" + sr);
        if (sr < 0) { Log("ccst rejected by SetSongXml"); return 7; }

        double alpha = double.Parse(gAlpha, System.Globalization.CultureInfo.InvariantCulture);
        bool okv = (bool)ctlType.GetMethod("ChangeVoice").Invoke(ctl,
            new object[] { track, gVoice, gDic, gVersion, 0, alpha, alpha, 0.5, 0.6, alpha });
        Log("ChangeVoice=" + okv + " voice=" + Path.GetFileName(gVoice));
        if (!okv) { Log("voice load failed"); return 8; }

        // same as official editor: run parameter analysis before export
        object score = ScoreOf(ctl, track);
        if (score != null)
        {
            Type st = score.GetType();
            string[] analysis = new string[] { "GetTimingParameter", "GetParameter", "GetLogF0WithVibParameter" };
            foreach (string an in analysis)
            {
                System.Reflection.MethodInfo mi = st.GetMethod(an, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (mi == null) { Log("analysis " + an + " not found"); continue; }
                object rr = mi.Invoke(score, null);
                Log("analysis " + an + " -> " + (rr == null ? "void" : rr.ToString()));
                Pump(200);
            }
        }

        object freq = ctlType.GetMethod("GetSamplingFrequency").Invoke(ctl, new object[] { track });
        gRate = Convert.ToInt32(freq);
        Log("samplingFrequency=" + gRate);

        Action<double[]> onAudio = delegate(double[] data) { if (data != null) lock (gSamples) gSamples.AddRange(data); };
        Action<bool> onEnd = delegate(bool ok) { gEnded = true; gOk = ok; };
        object r = ctlType.GetMethod("GetSongSequence").Invoke(ctl, new object[] { track, onAudio, onEnd });
        Log("GetSongSequence=" + r);

        int waited = 0;
        while (!gEnded && waited < 600000) { Thread.Sleep(20); waited += 20; }
        Log("ended=" + gEnded + " ok=" + gOk + " waited=" + waited + "ms");
        if (!gEnded || !gOk) { Log("synthesis did not complete"); return 9; }

        double[] buf;
        lock (gSamples) buf = gSamples.ToArray();
        if (buf.Length == 0) { Log("no samples"); return 10; }
        double peak = 0;
        for (int i = 0; i < buf.Length; i++) if (Math.Abs(buf[i]) > peak) peak = Math.Abs(buf[i]);
        Log("samples=" + buf.Length + " (" + (buf.Length / (double)gRate).ToString("F3") + "s @ " + gRate + ") peak=" + peak.ToString("F1"));
        WriteWav(gOut, buf, gRate, 1);
        Log("WROTE " + gOut);
        return 0;
    }

    static void WriteWav(string path, double[] data, int rate, int channels)
    {
        int n = data.Length;
        int dataBytes = n * channels * 2;
        using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write(new char[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataBytes);
            w.Write(new char[] { 'W', 'A', 'V', 'E' });
            w.Write(new char[] { 'f', 'm', 't', ' ' }); w.Write(16);
            w.Write((short)1); w.Write((short)channels);
            w.Write(rate); w.Write(rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
            w.Write(new char[] { 'd', 'a', 't', 'a' }); w.Write(dataBytes);
            byte[] pcm = new byte[dataBytes];
            for (int i = 0; i < n; i++)
            {
                int v = (int)Math.Round(data[i]);
                if (v > 32767) v = 32767; if (v < -32768) v = -32768;
                byte[] bb = BitConverter.GetBytes((short)v);
                pcm[i * 2] = bb[0]; pcm[i * 2 + 1] = bb[1];
            }
            w.Write(pcm);
        }
    }
}
