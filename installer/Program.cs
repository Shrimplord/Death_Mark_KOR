// 사인 한국어 패치 인스톨러 (C# 5 / .NET Framework 4.5 이상)
//
// 배포본을 게임 설치 폴더에 풀고 '패치 적용.exe' 를 실행한다.
//
//   <게임 설치 폴더>\
//     Death Mark.exe
//     resource\              ← 압축 해제 때 통파일이 여기로 병합된다
//     패치 적용.exe          ← 이 파일
//     읽어주세요.txt
//     적용 예시.png
//     Death_Mark_KOR\
//       manifest.txt  patch\  ui\
//
// 구조는 위 하나뿐이다. 탐색도 대체 경로도 없다 — 맞으면 진행하고, 아니면
// 읽어주세요.txt 를 보라고 안내하고 중단한다. 스팀 라이브러리는 뒤지지 않는다.
//
// **통파일은 복사하지 않는다.** 압축 해제가 곧 설치이고, 인스톨러는 제대로
// 덮였는지 확인만 한다(manifest [verify]). 탐색기에서 '건너뛰기'를 누른 경우가
// 여기서 걸린다. 덕분에 인스톨러가 깨져도 통파일은 이미 적용된 상태로 남는다.
//
// 하는 일은 셋뿐이다.
//   1) 폴더 내 복사     게임폴더의 파일 A → B
//   2) exe 차분 적용    DM-PATCH
//   3) ui.dat 주입      .hed에서 오프셋을 읽어 해당 구간만 덮어쓰기
//
// 검사를 전부 통과한 뒤에야 게임 폴더를 건드린다.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

internal static class Program
{
    private const string DataFolder = "Death_Mark_KOR";

    // 반복되는 안내 문구는 여기 한 곳에서만 고친다.
    private const string VerifyHint =
        "스팀에서 '게임 파일 무결성 확인'을 실행한 뒤 다시 시도해 주세요.\r\n" +
        "(라이브러리에서 게임 우클릭 → 속성 → 설치된 파일)";
    private const string CorruptHint =
        "패치 파일이 손상됐습니다. 다시 내려받아 주세요.";
    private const string NotCopiedHint =
        "패치 파일이 제대로 복사되지 않았습니다.\r\n" +
        "압축을 풀 때 '모두 덮어쓰기'를 선택해 주세요.";
    private const string MissingGameFile =
        "패치할 파일을 찾을 수 없습니다.\r\n" + VerifyHint;
    private const string BusyHint =
        "게임 파일을 열 수 없습니다.\r\n" +
        "게임이 실행 중이면 종료한 뒤 다시 실행해 주세요.";
    private const string VersionFile = "패치_버전.txt";
    private const string LogFile = "패치_로그.txt";
    private const string TmpSuffix = ".patchtmp";

    private static string _root;      // 게임 설치 폴더 (이 exe가 있는 곳)
    private static string _data;      // <_root>\Death_Mark_KOR
    private static Manifest _mf;
    private static bool _partial;  // 진행 표시(...)로 줄이 열려 있는지

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch (Exception) { }
        try
        {
            return Run();
        }
        catch (Exception ex)
        {
            WriteLog(ex);
            Line();
            Console.WriteLine("[실패] " + ex.Message);
            Line();
            Pause();
            return 1;
        }
    }

    private static int Run()
    {
        _root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        _data = Path.Combine(_root, DataFolder);

        if (!File.Exists(Path.Combine(_data, "manifest.txt")))
            throw LayoutError();
        try { _mf = Manifest.Parse(Path.Combine(_data, "manifest.txt")); }
        catch (Exception ex) { throw Corrupt("manifest.txt", ex.Message); }
        if (!File.Exists(Path.Combine(_root, _mf.Exe)))
            throw LayoutError();

        Console.WriteLine("사인(死印) 한국어 패치  v" + _mf.Version);
        Line();
        Console.Write("패치 적용 중...");
        _partial = true;

        // 검사를 모두 통과한 뒤에야 게임 폴더를 건드린다.
        VerifyPayload();
        DmPatch patch = PreparePatch(_root);
        PrepareInjects(_root);
        PrepareClones(_root);

        ApplyClones(_root);
        ApplyPatch(_root, patch);
        ApplyInjects(_root);

        File.WriteAllText(Path.Combine(_root, VersionFile),
            "사인 한국어 패치 v" + _mf.Version + " (" + _mf.Date + ") 적용됨\r\n",
            Encoding.UTF8);

        EndProgress();
        Console.WriteLine("패치가 완료되었습니다.");
        Line();
        Pause();
        return 0;
    }

    // ── 위치 확인 ────────────────────────────────────────────────────
    // 기대 구조는 하나뿐이다. 맞지 않으면 그림을 보여주고 중단한다.
    private static Exception LayoutError()
    {
        return new Exception(
            "게임 설치 폴더에서 실행해야 합니다.\r\n" +
            "'읽어주세요.txt' 의 적용법을 확인해 주세요.");
    }

    // ── 검사 ────────────────────────────────────────────────────────
    private static void VerifyPayload()
    {
        // 배포본에 들어 있는 것 — Death_Mark_KOR 안
        for (int i = 0; i < _mf.Injects.Count; i++)
        {
            Manifest.InjectItem x = _mf.Injects[i];
            CheckFile(Path.Combine(_data, Path.Combine("ui", x.File)), x.Size, x.Sha256, true);
            Console.Write(".");
        }
        for (int i = 0; i < _mf.Patches.Count; i++)
        {
            Manifest.PatchItem pi = _mf.Patches[i];
            CheckFile(Path.Combine(_data, Path.Combine("patch", pi.File)), pi.Size, pi.Sha256, true);
        }

        // 압축 해제로 게임 폴더에 들어갔어야 하는 것 — resource\
        // 탐색기에서 '건너뛰기'를 누른 경우를 여기서 잡는다.
        for (int i = 0; i < _mf.Verifies.Count; i++)
        {
            Manifest.VerifyItem v = _mf.Verifies[i];
            CheckFile(Path.Combine(_root, v.Path), v.Size, v.Sha256, false);
            if ((i % 8) == 0) Console.Write(".");
        }
    }

    // 없음·크기 불일치·해시 불일치는 유저에게 모두 같은 뜻이므로 한 문장으로 묶는다.
    /// <param name="inPackage">true면 배포본 파일(손상), false면 복사됐어야 할 게임 파일(덮어쓰기 실패)</param>
    private static void CheckFile(string path, long size, string sha, bool inPackage)
    {
        if (!File.Exists(path))
            throw Fail(inPackage, path, "파일 없음");

        FileInfo fi = new FileInfo(path);
        if (fi.Length != size)
            throw Fail(inPackage, path, "크기 " + fi.Length + " / 기대 " + size);

        string got = DmPatch.Sha256File(path);
        if (!string.Equals(got, sha, StringComparison.OrdinalIgnoreCase))
            throw Fail(inPackage, path, "sha256 " + got + " / 기대 " + sha);
    }

    // 화면에는 한 문장만 나가고, 어느 파일이 왜 걸렸는지는 로그에만 남긴다.
    private static Exception Fail(bool inPackage, string path, string detail)
    {
        return WithDetail(new Exception(inPackage ? CorruptHint : NotCopiedHint), path, detail);
    }

    /// <summary>배포본이 깨진 경우. 사용자에게는 한 문장.</summary>
    private static Exception Corrupt(string path, string detail)
    {
        return WithDetail(new Exception(CorruptHint), path, detail);
    }

    private static Exception WithDetail(Exception ex, string path, string detail)
    {
        ex.Data["path"] = path;
        ex.Data["detail"] = detail;
        return ex;
    }

    // 쓰기 직전에 실패하면 일부만 적용된 상태가 남으므로, 잠겨 있는지를 검사 단계에서 본다.
    // 실행 중인 exe 는 삭제 시 '액세스가 거부되었습니다'로 떨어져 원인을 알 수 없다.
    private static void CheckWritable(string path)
    {
        try
        {
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }
        catch (Exception ex)
        {
            throw WithDetail(new Exception(BusyHint), path, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static DmPatch PreparePatch(string game)
    {
        if (_mf.Patches.Count == 0) return null;
        Manifest.PatchItem pi = _mf.Patches[0];

        string pPath = Path.Combine(_data, Path.Combine("patch", pi.File));
        DmPatch patch;
        try { patch = DmPatch.Parse(pPath); }
        catch (Exception ex) { throw Corrupt(pi.File, ex.Message); }

        string exePath = Path.Combine(game, _mf.Exe);
        byte[] cur = File.ReadAllBytes(exePath);
        TryDelete(exePath + TmpSuffix);               // 이전 실행이 남긴 임시 파일

        if (patch.IsOriginal(cur)) { CheckWritable(exePath); return patch; }

        if (patch.IsOursPatched(cur))
        {
            string bak = Path.Combine(game, pi.Backup);
            if (File.Exists(bak) && patch.IsOriginal(File.ReadAllBytes(bak)))
            {
                CheckWritable(exePath);
                return patch;
            }
            if (patch.IsAlreadyPatched(cur))
            {
                return null;
            }
            // 구버전 적용본인데 되돌릴 백업이 없다. 그냥 건너뛰면 실행 파일만
            // 구버전으로 남아 다른 파일과 어긋나므로 중단한다.
        }

        throw WithDetail(new Exception(_mf.Exe + " 가 손상되었습니다.\r\n" + VerifyHint),
                         exePath, "sha256 " + DmPatch.Sha256(cur));
    }

    private static List<UiDatIndex.Entry> _injectEntries = new List<UiDatIndex.Entry>();

    private static void PrepareInjects(string game)
    {
        _injectEntries.Clear();
        for (int i = 0; i < _mf.Injects.Count; i++)
        {
            Manifest.InjectItem x = _mf.Injects[i];
            string container = Path.Combine(game, x.Container);
            string index = Path.Combine(game, x.Index);
            if (!File.Exists(container))
                throw new Exception(MissingGameFile);
            if (!File.Exists(index))
                throw new Exception(MissingGameFile);

            if (i == 0 || _mf.Injects[i - 1].Container != x.Container)
                CheckWritable(container);

            UiDatIndex idx = UiDatIndex.Load(index);
            long clen = new FileInfo(container).Length;
            _injectEntries.Add(idx.Check(x.File, x.Size, Path.GetFileName(x.Container), clen));
        }
    }

    private static void PrepareClones(string game)
    {
        for (int i = 0; i < _mf.Clones.Count; i++)
        {
            string from = Path.Combine(game, _mf.Clones[i].From);
            if (!File.Exists(from))
                throw new Exception(MissingGameFile);
        }
    }

    // ── 적용 ────────────────────────────────────────────────────────
    private static void ApplyClones(string game)
    {
        for (int i = 0; i < _mf.Clones.Count; i++)
        {
            string from = Path.Combine(game, _mf.Clones[i].From);
            string to = Path.Combine(game, _mf.Clones[i].To);
            string tmp = to + TmpSuffix;
            File.Copy(from, tmp, true);
            if (File.Exists(to)) File.Delete(to);
            File.Move(tmp, to);
            Console.Write(".");
        }
    }

    private static void ApplyPatch(string game, DmPatch patch)
    {
        if (patch == null) return;
        Manifest.PatchItem pi = _mf.Patches[0];
        string exe = Path.Combine(game, _mf.Exe);
        string bak = Path.Combine(game, pi.Backup);

        byte[] cur = File.ReadAllBytes(exe);
        byte[] original;

        if (patch.IsOriginal(cur))
        {
            original = cur;
            string bdir = Path.GetDirectoryName(bak);
            if (!Directory.Exists(bdir)) Directory.CreateDirectory(bdir);
            if (!File.Exists(bak)) File.Copy(exe, bak);   // 있으면 덮지 않는다
        }
        else
        {
            original = File.ReadAllBytes(bak);            // 재적용 경로 (검사에서 확인됨)
        }

        byte[] dst = patch.Apply(original);
        string tmp = exe + TmpSuffix;
        File.WriteAllBytes(tmp, dst);
        File.Delete(exe);
        File.Move(tmp, exe);
        Console.Write(".");
    }

    private static void ApplyInjects(string game)
    {
        for (int i = 0; i < _mf.Injects.Count; i++)
        {
            Manifest.InjectItem x = _mf.Injects[i];
            string container = Path.Combine(game, x.Container);
            string payload = Path.Combine(_data, Path.Combine("ui", x.File));
            UiDatIndex.Write(container, _injectEntries[i].Offset, payload);
            Console.Write(".");
        }
    }

    // ── 잡다 ────────────────────────────────────────────────────────
    // 제보 대응용. 화면 메시지는 그대로 두고 상세만 파일로 남긴다. 실패해도 조용히 넘어간다.
    private static void WriteLog(Exception ex)
    {
        try
        {
            if (_root == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] 패치 v" +
                          (_mf != null ? _mf.Version : "?"));
            sb.AppendLine("메시지: " + ex.Message);
            if (ex.Data.Contains("path")) sb.AppendLine("파일: " + ex.Data["path"]);
            if (ex.Data.Contains("detail")) sb.AppendLine("상세: " + ex.Data["detail"]);
            sb.AppendLine(ex.ToString());
            sb.AppendLine();
            File.AppendAllText(Path.Combine(_root, LogFile), sb.ToString(), Encoding.UTF8);
        }
        catch (Exception) { }
    }

    private static void TryDelete(string p)
    {
        try { if (File.Exists(p)) File.Delete(p); }
        catch (Exception) { }
    }

    // 진행 표시(...)로 열려 있는 줄을 닫는다. 열려 있지 않으면 아무것도 하지 않는다.
    private static void EndProgress()
    {
        if (!_partial) return;
        _partial = false;
        Console.WriteLine();
    }

    private static void Line()
    {
        EndProgress();
        Console.WriteLine("--------------------------------------------------");
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("아무 키나 누르면 닫힙니다...");
        try { Console.ReadKey(true); } catch (Exception) { Console.ReadLine(); }
    }
}
