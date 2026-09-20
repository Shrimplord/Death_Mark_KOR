// DM-PATCH 1 적용기 — .NET Framework 4.5 이상 / C# 5 문법
//
//     DmPatch patch = DmPatch.Parse(@"patch\Death Mark.dmpatch.txt");
//     byte[] src = File.ReadAllBytes(exePath);
//     byte[] dst = patch.Apply(src);
//     File.WriteAllBytes(exePath, dst);
//
// 실패는 전부 DmPatchException. 메시지를 그대로 사용자에게 보여줘도 된다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public class DmPatchException : Exception
{
    public DmPatchException(string message) : base(message) { }
}

public sealed class DmPatch
{
    public sealed class Record
    {
        public int Offset;
        public byte[] Data;
        public int LineNumber;
    }

    public sealed class Group
    {
        public string Name;        // "4_입력기_한글_그리드"
        public string Number;      // "4"
        public List<Record> Records = new List<Record>();
    }

    private string _srcFile, _dstFile, _srcSha, _dstSha;
    private int _size = -1;
    private List<Group> _groups = new List<Group>();
    private List<string> _knownDst = new List<string>();

    public string SrcFile { get { return _srcFile; } }
    public string DstFile { get { return _dstFile; } }
    public string SrcSha256 { get { return _srcSha; } }
    public string DstSha256 { get { return _dstSha; } }
    public int Size { get { return _size; } }
    public List<Group> Groups { get { return _groups; } }

    /// <summary>이전 판본이 만들어낸 결과 해시들. 헤더 known-dst-sha256 (여러 줄 가능).
    /// 패치를 갱신해 내보낼 때 구버전 적용본을 "우리가 건드린 파일"로 인식하기 위한 것.</summary>
    public List<string> KnownDstSha256 { get { return _knownDst; } }

    // ── 파싱 ──────────────────────────────────────────────────────────
    public static DmPatch Parse(string path)
    {
        return ParseLines(File.ReadAllLines(path, Encoding.UTF8), Path.GetFileName(path));
    }

    public static DmPatch ParseLines(IList<string> lines, string origin)
    {
        if (origin == null) origin = "패치 파일";
        DmPatch p = new DmPatch();
        Group current = null;
        Record pending = null;
        int declaredLen = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            int lineNo = i + 1;
            string line = lines[i].TrimEnd('\r');

            if (line.StartsWith("#"))
            {
                string[] h = line.Substring(1).Split(new char[] { ' ', '\t' },
                                                     StringSplitOptions.RemoveEmptyEntries);
                if (h.Length >= 2)
                {
                    if (h[0] == "src-sha256") p._srcSha = h[1].ToLowerInvariant();
                    else if (h[0] == "dst-sha256") p._dstSha = h[1].ToLowerInvariant();
                    else if (h[0] == "size") p._size = int.Parse(h[1], CultureInfo.InvariantCulture);
                    else if (h[0] == "src-file") p._srcFile = string.Join(" ", h, 1, h.Length - 1);
                    else if (h[0] == "dst-file") p._dstFile = string.Join(" ", h, 1, h.Length - 1);
                    else if (h[0] == "known-dst-sha256") p._knownDst.Add(h[1].ToLowerInvariant());
                }
                continue;
            }

            int semi = line.IndexOf(';');
            string body = semi >= 0 ? line.Substring(0, semi) : line;
            if (body.Trim().Length == 0) continue;
            string trimmed = body.Trim();

            if (trimmed[0] == '[')
            {
                Finish(pending, declaredLen, origin);
                pending = null;
                current = new Group();
                current.Name = trimmed.Trim('[', ']');
                int us = current.Name.IndexOf('_');
                current.Number = us > 0 ? current.Name.Substring(0, us) : current.Name;
                p._groups.Add(current);
                continue;
            }

            bool cont = body[0] == ' ' || body[0] == '\t';

            if (cont)
            {
                if (pending == null)
                    throw Err(origin, lineNo, "이어지는 바이트인데 앞에 레코드가 없습니다.");
                byte[] extra = Hex(trimmed, origin, lineNo);
                byte[] merged = new byte[pending.Data.Length + extra.Length];
                Buffer.BlockCopy(pending.Data, 0, merged, 0, pending.Data.Length);
                Buffer.BlockCopy(extra, 0, merged, pending.Data.Length, extra.Length);
                pending.Data = merged;
            }
            else
            {
                Finish(pending, declaredLen, origin);

                if (current == null)
                    throw Err(origin, lineNo, "그룹 머리말 [...] 없이 레코드가 나왔습니다.");

                string[] t = trimmed.Split(new char[] { ' ', '\t' },
                                           StringSplitOptions.RemoveEmptyEntries);
                if (t.Length < 2) throw Err(origin, lineNo, "형식 오류 — \"" + trimmed + "\"");

                int off;
                if (!int.TryParse(t[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out off) || off < 0)
                    throw Err(origin, lineNo, "오프셋이 잘못됐습니다 — \"" + t[0] + "\"");
                if (!int.TryParse(t[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out declaredLen) || declaredLen <= 0)
                    throw Err(origin, lineNo, "길이가 잘못됐습니다 — \"" + t[1] + "\"");

                pending = new Record();
                pending.Offset = off;
                pending.LineNumber = lineNo;
                pending.Data = t.Length > 2 ? Hex(t[2], origin, lineNo) : new byte[0];
                current.Records.Add(pending);
            }

            if (pending != null && pending.Data.Length > declaredLen)
                throw Err(origin, lineNo, "오프셋 " + pending.Offset.ToString("X8") +
                          " — 선언 길이 " + declaredLen + "보다 바이트가 많습니다(" + pending.Data.Length + ").");
        }

        Finish(pending, declaredLen, origin);

        if (p._groups.Count == 0) throw new DmPatchException(origin + ": 그룹이 하나도 없습니다.");
        if (string.IsNullOrEmpty(p._srcSha)) throw new DmPatchException(origin + ": src-sha256 머리말이 없습니다.");
        if (string.IsNullOrEmpty(p._dstFile)) p._dstFile = p._srcFile;
        return p;
    }

    private static DmPatchException Err(string origin, int line, string msg)
    {
        return new DmPatchException(origin + " " + line + "행: " + msg);
    }

    private static void Finish(Record r, int declaredLen, string origin)
    {
        if (r == null) return;
        if (r.Data.Length != declaredLen)
            throw Err(origin, r.LineNumber, "오프셋 " + r.Offset.ToString("X8") +
                      " — 길이 " + declaredLen + "인데 바이트는 " + r.Data.Length + "개입니다.");
    }

    private static byte[] Hex(string s, string origin, int lineNo)
    {
        if ((s.Length & 1) != 0) throw Err(origin, lineNo, "hex 자릿수가 홀수입니다.");
        byte[] b = new byte[s.Length / 2];
        for (int i = 0; i < b.Length; i++)
            if (!byte.TryParse(s.Substring(i * 2, 2), NumberStyles.HexNumber,
                               CultureInfo.InvariantCulture, out b[i]))
                throw Err(origin, lineNo, "hex가 아닌 문자가 있습니다.");
        return b;
    }

    // ── 적용 ──────────────────────────────────────────────────────────
    public byte[] Apply(byte[] src) { return Apply(src, null); }

    /// <param name="onlyGroupNumbers">null이면 전체. 예: new string[]{"4","6"}</param>
    public byte[] Apply(byte[] src, IEnumerable<string> onlyGroupNumbers)
    {
        // 아래 두 검사는 이 클래스를 단독으로 쓸 때를 위한 것이다.
        // 인스톨러는 호출 전에 IsOriginal / IsAlreadyPatched 로 이미 판정하므로 걸리지 않는다.
        if (_size >= 0 && src.Length != _size)
            throw new DmPatchException("대상 파일 크기가 맞지 않습니다. 기대 " +
                _size.ToString("N0") + " 바이트, 실제 " + src.Length.ToString("N0") + " 바이트.");

        string got = Sha256(src);
        if (!string.Equals(got, _srcSha, StringComparison.OrdinalIgnoreCase))
            throw new DmPatchException("대상 파일이 이 패치의 원본이 아닙니다. (" +
                Short(got) + " / 기대 " + Short(_srcSha) + ")");

        HashSet<string> only = null;
        if (onlyGroupNumbers != null) only = new HashSet<string>(onlyGroupNumbers, StringComparer.Ordinal);

        byte[] buf = (byte[])src.Clone();
        for (int g = 0; g < _groups.Count; g++)
        {
            Group grp = _groups[g];
            if (only != null && !only.Contains(grp.Number)) continue;
            for (int r = 0; r < grp.Records.Count; r++)
            {
                Record rec = grp.Records[r];
                if (rec.Offset + rec.Data.Length > buf.Length)
                    throw new DmPatchException("[" + grp.Name + "] 오프셋 " +
                        rec.Offset.ToString("X8") + "이 파일 범위를 벗어납니다.");
                Buffer.BlockCopy(rec.Data, 0, buf, rec.Offset, rec.Data.Length);
            }
        }

        if (only == null && !string.IsNullOrEmpty(_dstSha))
        {
            string res = Sha256(buf);
            if (!string.Equals(res, _dstSha, StringComparison.OrdinalIgnoreCase))
                throw new DmPatchException("패치 결과가 기대값과 다릅니다. (" +
                    Short(res) + " / 기대 " + Short(_dstSha) + ")\r\n" +
                    "패치 파일이 손상됐을 수 있습니다.");
        }
        return buf;
    }

    public bool IsAlreadyPatched(byte[] data)
    {
        return !string.IsNullOrEmpty(_dstSha)
            && string.Equals(Sha256(data), _dstSha, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>이 패치의 결과물이거나, 이전 판본의 결과물인지.</summary>
    public bool IsOursPatched(byte[] data)
    {
        if (IsAlreadyPatched(data)) return true;
        string h = Sha256(data);
        for (int i = 0; i < _knownDst.Count; i++)
            if (string.Equals(h, _knownDst[i], StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public bool IsOriginal(byte[] data)
    {
        return string.Equals(Sha256(data), _srcSha, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>메시지용 해시 축약. 전문 64자는 사용자에게 의미가 없다.</summary>
    public static string Short(string sha)
    {
        if (string.IsNullOrEmpty(sha)) return "(없음)";
        return sha.Length <= 12 ? sha : sha.Substring(0, 12) + "...";
    }

    public static string Sha256(byte[] data)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
    }

    public static string Sha256File(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream fs = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
    }
}
