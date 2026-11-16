using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

class Installer
{
    const string RawBase = "https://raw.githubusercontent.com/aaa111ws/majestic-fps/main/parts/laws.v2.rar.part";
    const int Parts = 12;
    const string RarSha256 = "e0af576a858bebc3b4a0f28a7c7783b94a7ceb337918edb58239fa54b38083d5";
    const long ReduxSize = 1462195712;
    const string RarPassword = "majestic-mods.ru";
    const string BackupName = "update.rpf.original";

    [STAThread]
    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "Majestic FPS: LAWS REDUX v2";
        try
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            Say("=== Majestic без фризов: LAWS REDUX v2 + лёгкая графика ===\n");
            Say("1 - установить редукс и урезать графику");
            Say("2 - только урезать графику (без редукса)");
            Say("3 - откат: вернуть оригинальные файлы");
            Console.Write("\nВыбери 1, 2 или 3 и нажми Enter: ");
            string c = (Console.ReadLine() ?? "").Trim();
            if (c != "1" && c != "2" && c != "3") { Say("Ничего не выбрано."); Pause(); return 1; }

            WaitGameClosed();
            if (c == "2") { PatchSettings(); Done(); return 0; }

            string gta = FindGta();
            if (gta == null) { Say("Папка GTA V не выбрана, выход."); Pause(); return 1; }
            Say("Игра: " + gta);
            string upd = Path.Combine(gta, "update");
            string rpf = Path.Combine(upd, "update.rpf");
            string bak = Path.Combine(upd, BackupName);

            if (c == "3") { Rollback(rpf, bak); Done(); return 0; }

            Install(upd, rpf, bak);
            PatchSettings();
            Done();
            return 0;
        }
        catch (Exception e)
        {
            Say("\nОШИБКА: " + e.Message);
            Say("Скинь скрин этого окна тому, кто дал программу.");
            Pause();
            return 1;
        }
    }

    static void Install(string upd, string rpf, string bak)
    {
        if (!File.Exists(rpf)) throw new Exception("Нет файла " + rpf);
        if (new FileInfo(rpf).Length == ReduxSize) { Say("Редукс уже стоит, пропускаю."); return; }

        string tmp = Path.Combine(upd, "_redux_tmp");
        Directory.CreateDirectory(tmp);
        string rar = Path.Combine(tmp, "laws.v2.rar");

        Say("\nСкачиваю редукс (~1,2 ГБ, " + Parts + " частей)...");
        using (var outF = new FileStream(rar, FileMode.Create, FileAccess.Write))
        {
            for (int i = 0; i < Parts; i++)
            {
                string url = RawBase + i.ToString("00");
                Console.Write("  часть " + (i + 1) + "/" + Parts + " ... ");
                DownloadAppend(url, outF);
                Say("ок");
            }
        }
        Console.Write("Проверяю архив... ");
        if (Sha256(rar) != RarSha256) throw new Exception("Архив скачался битым. Запусти ещё раз.");
        Say("ок");

        Console.Write("Распаковываю... ");
        string sevenDir = Path.Combine(tmp, "7z");
        Directory.CreateDirectory(sevenDir);
        Extract("7z.exe", Path.Combine(sevenDir, "7z.exe"));
        Extract("7z.dll", Path.Combine(sevenDir, "7z.dll"));
        var psi = new ProcessStartInfo(Path.Combine(sevenDir, "7z.exe"),
            "e -y -p" + RarPassword + " \"" + rar + "\" -o\"" + tmp + "\" update.rpf");
        psi.UseShellExecute = false; psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
        var p = Process.Start(psi);
        p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit();
        string newRpf = Path.Combine(tmp, "update.rpf");
        if (p.ExitCode != 0 || !File.Exists(newRpf) || new FileInfo(newRpf).Length != ReduxSize)
            throw new Exception("Не распаковалось (код 7z " + p.ExitCode + ").");
        Say("ок");

        if (!File.Exists(bak))
        {
            File.Move(rpf, bak);
            Say("Оригинал сохранён: " + bak);
        }
        else
        {
            File.Delete(rpf);
            Say("Оригинал уже был сохранён раньше: " + bak);
        }
        File.Move(newRpf, rpf);
        try { Directory.Delete(tmp, true); } catch { }
        Say("Редукс установлен.");
    }

    static void Rollback(string rpf, string bak)
    {
        if (!File.Exists(bak)) { Say("Бэкапа " + bak + " нет - откатывать редукс нечего."); }
        else
        {
            if (File.Exists(rpf)) File.Delete(rpf);
            File.Move(bak, rpf);
            Say("Оригинальный update.rpf возвращён.");
        }
        string s = SettingsPath();
        string sb = s + ".original";
        if (File.Exists(sb)) { File.Copy(sb, s, true); File.Delete(sb); Say("Оригинальные настройки графики возвращены."); }
    }

    static string SettingsPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Rockstar Games\GTA V\settings.xml");
    }

    static void PatchSettings()
    {
        string f = SettingsPath();
        if (!File.Exists(f)) { Say("settings.xml не найден (" + f + ") - зайди в игру один раз и запусти снова пункт 2."); return; }
        string sb = f + ".original";
        if (!File.Exists(sb)) File.Copy(f, sb);
        var set = new Dictionary<string, string> {
            {"LodScale","0.000000"}, {"PedLodBias","0.000000"}, {"SSAO","0"},
            {"Shadow_SoftShadows","0"}, {"Shadow_ParticleShadows","false"}, {"Reflection_MipBlur","false"},
            {"Lighting_FogVolumes","false"}, {"Shader_SSA","false"},
            {"CityDensity","0.000000"}, {"PedVarietyMultiplier","0.000000"}, {"VehicleVarietyMultiplier","0.000000"}
        };
        string t = File.ReadAllText(f);
        foreach (var kv in set)
            t = Regex.Replace(t, "(<" + kv.Key + " value=\")[^\"]*(\")", "${1}" + kv.Value + "${2}");
        File.WriteAllText(f, t);
        Say("Графика урезана (разрешение и монитор не тронуты). Бэкап: " + sb);
    }

    static string FindGta()
    {
        var cands = new List<string>();
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (var key in new[] { @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", @"SOFTWARE\Rockstar Games\Grand Theft Auto V" })
            {
                try { using (var k = hive.OpenSubKey(key)) { var v = k == null ? null : k.GetValue("InstallFolder") as string; if (v != null) cands.Add(v); } } catch { }
            }
        try
        {
            string steam = null;
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) { if (k != null) steam = k.GetValue("SteamPath") as string; }
            if (steam != null)
            {
                steam = steam.Replace('/', '\\');
                cands.Add(Path.Combine(steam, @"steamapps\common\Grand Theft Auto V"));
                string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        cands.Add(Path.Combine(m.Groups[1].Value.Replace(@"\\", @"\"), @"steamapps\common\Grand Theft Auto V"));
            }
        }
        catch { }
        foreach (var d in DriveInfo.GetDrives())
        {
            if (d.DriveType != DriveType.Fixed) continue;
            foreach (var rel in new[] { @"SteamLibrary\steamapps\common\Grand Theft Auto V", @"Program Files\Rockstar Games\Grand Theft Auto V", @"Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V", @"Games\Grand Theft Auto V", @"Grand Theft Auto V" })
                cands.Add(Path.Combine(d.RootDirectory.FullName, rel));
        }
        foreach (var c in cands)
            if (File.Exists(Path.Combine(c, "GTA5.exe")) && File.Exists(Path.Combine(c, @"update\update.rpf"))) return c;

        Say("Не нашёл GTA V сам - выбери папку игры (где лежит GTA5.exe).");
        using (var dlg = new FolderBrowserDialog())
        {
            dlg.Description = "Папка GTA V (где GTA5.exe)";
            while (dlg.ShowDialog() == DialogResult.OK)
            {
                if (File.Exists(Path.Combine(dlg.SelectedPath, "GTA5.exe"))) return dlg.SelectedPath;
                MessageBox.Show("В этой папке нет GTA5.exe, выбери другую.");
            }
        }
        return null;
    }

    static void WaitGameClosed()
    {
        string[] names = { "GTA5", "PlayGTAV", "GTAVLauncher", "RAGEMP", "majestic", "MajesticLauncher" };
        while (true)
        {
            var running = new List<string>();
            foreach (var n in names) if (Process.GetProcessesByName(n).Length > 0) running.Add(n);
            if (running.Count == 0) return;
            Say("Закрой игру/лаунчер (" + string.Join(", ", running.ToArray()) + ") и нажми Enter.");
            Console.ReadLine();
        }
    }

    static void DownloadAppend(string url, Stream outF)
    {
        for (int attempt = 1; ; attempt++)
        {
            long start = outF.Position;
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "majestic-fps-installer";
                req.Timeout = 60000; req.ReadWriteTimeout = 60000;
                using (var resp = req.GetResponse())
                using (var s = resp.GetResponseStream()) s.CopyTo(outF);
                return;
            }
            catch (Exception)
            {
                outF.SetLength(start); outF.Position = start;
                if (attempt >= 5) throw;
                Console.Write("повтор... ");
                System.Threading.Thread.Sleep(3000);
            }
        }
    }

    static string Sha256(string file)
    {
        using (var sha = SHA256.Create())
        using (var f = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
    }

    static void Extract(string res, string to)
    {
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(res))
        using (var f = File.Create(to)) s.CopyTo(f);
    }

    static void Done() { Say("\nГотово. Можно заходить в Majestic."); Pause(); }
    static void Say(string s) { Console.WriteLine(s); }
    static void Pause() { Console.WriteLine("Нажми Enter, чтобы закрыть."); Console.ReadLine(); }
}
