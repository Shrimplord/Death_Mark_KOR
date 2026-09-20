// 사인 한국어 패치 manifest 1 파서 — C# 5
//
// 형식은 "manifest 포맷.md" 참조. 평문, UTF-8, 섹션 + 공백 구분 필드.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public class ManifestException : Exception
{
    public ManifestException(string message) : base(message) { }
}

public sealed class Manifest
{
    /// <summary>압축 해제로 게임폴더\&lt;Path&gt; 에 들어갔어야 하는 파일</summary>
    public sealed class VerifyItem
    {
        public string Path;      // resource\event_script_en.dat
        public long Size;
        public string Sha256;
    }

    /// <summary>게임폴더 안에서 From → To 복사</summary>
    public sealed class CloneItem
    {
        public string From;
        public string To;
    }

    /// <summary>ui\&lt;File&gt; 을 &lt;Container&gt; 의 같은 이름 엔트리에 주입</summary>
    public sealed class InjectItem
    {
        public string File;      // noname_parts_en.dds
        public string Container; // resource\ui.dat
        public string Index;     // resource\ui.hed
        public long Size;
        public string Sha256;
    }

    /// <summary>patch\&lt;File&gt; 을 적용, 원본은 Backup 으로</summary>
    public sealed class PatchItem
    {
        public string File;      // Death Mark.dmpatch.txt
        public string Backup;    // bak\Death Mark.exe
        public long Size;
        public string Sha256;
    }

    public string Version = "";
    public string Date = "";
    public string Exe = "";
    public List<VerifyItem> Verifies = new List<VerifyItem>();
    public List<CloneItem> Clones = new List<CloneItem>();
    public List<InjectItem> Injects = new List<InjectItem>();
    public List<PatchItem> Patches = new List<PatchItem>();

    public static Manifest Parse(string path)
    {
        return ParseLines(File.ReadAllLines(path, Encoding.UTF8), Path.GetFileName(path));
    }

    public static Manifest ParseLines(IList<string> lines, string origin)
    {
        if (origin == null) origin = "manifest";
        Manifest m = new Manifest();
        string section = "";

        for (int i = 0; i < lines.Count; i++)
        {
            int no = i + 1;
            string line = lines[i].TrimEnd('\r');
            int semi = line.IndexOf(';');
            if (semi >= 0) line = line.Substring(0, semi);
            if (line.StartsWith("#")) continue;
            string t = line.Trim();
            if (t.Length == 0) continue;

            if (t[0] == '[') { section = t.Trim('[', ']').ToLowerInvariant(); continue; }

            string[] f = SplitFields(t);

            if (section.Length == 0)
            {
                if (f.Length >= 2)
                {
                    if (f[0] == "version") m.Version = f[1];
                    else if (f[0] == "date") m.Date = f[1];
                    else if (f[0] == "exe") m.Exe = Join(f, 1);
                }
                continue;
            }

            if (section == "verify")
            {
                if (f.Length < 3) throw Err(origin, no, "verify 항목은 경로·크기·해시 3개가 필요합니다.");
                VerifyItem v = new VerifyItem();
                v.Sha256 = f[f.Length - 1].ToLowerInvariant();
                v.Size = long.Parse(f[f.Length - 2], CultureInfo.InvariantCulture);
                v.Path = Join(f, 0, f.Length - 2);
                m.Verifies.Add(v);
            }
            else if (section == "clone")
            {
                if (f.Length < 2) throw Err(origin, no, "clone 항목은 원본·사본 2개가 필요합니다.");
                CloneItem c = new CloneItem();
                c.From = f[0];
                c.To = Join(f, 1);
                m.Clones.Add(c);
            }
            else if (section == "inject")
            {
                if (f.Length < 5) throw Err(origin, no, "inject 항목은 파일·컨테이너·인덱스·크기·해시 5개가 필요합니다.");
                InjectItem x = new InjectItem();
                x.File = f[0];
                x.Container = f[1];
                x.Index = f[2];
                x.Size = long.Parse(f[3], CultureInfo.InvariantCulture);
                x.Sha256 = f[4].ToLowerInvariant();
                m.Injects.Add(x);
            }
            else if (section == "patch")
            {
                if (f.Length < 4) throw Err(origin, no, "patch 항목은 패치파일·백업경로·크기·해시 4개가 필요합니다.");
                PatchItem p = new PatchItem();
                p.Sha256 = f[f.Length - 1].ToLowerInvariant();
                p.Size = long.Parse(f[f.Length - 2], CultureInfo.InvariantCulture);
                p.Backup = f[f.Length - 3];
                p.File = Join(f, 0, f.Length - 3);
                m.Patches.Add(p);
            }
            else
            {
                throw Err(origin, no, "알 수 없는 섹션 [" + section + "] 입니다.");
            }
        }

        if (m.Version.Length == 0) throw new ManifestException(origin + ": version 이 없습니다.");
        if (m.Exe.Length == 0) throw new ManifestException(origin + ": exe 가 없습니다.");
        return m;
    }

    // 필드는 공백 2칸 이상으로 구분한다(파일명에 한 칸 공백이 있어도 안전).
    private static string[] SplitFields(string s)
    {
        List<string> outp = new List<string>();
        int i = 0;
        while (i < s.Length)
        {
            while (i < s.Length && s[i] == ' ') i++;
            if (i >= s.Length) break;
            int start = i;
            while (i < s.Length)
            {
                if (s[i] == ' ' && i + 1 < s.Length && s[i + 1] == ' ') break;
                if (s[i] == '\t') break;
                i++;
            }
            outp.Add(s.Substring(start, i - start).TrimEnd());
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
        }
        return outp.ToArray();
    }

    private static string Join(string[] f, int from) { return Join(f, from, f.Length); }

    private static string Join(string[] f, int from, int toExclusive)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = from; i < toExclusive; i++)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(f[i]);
        }
        return sb.ToString();
    }

    private static ManifestException Err(string origin, int line, string msg)
    {
        return new ManifestException(origin + " " + line + "행: " + msg);
    }
}
