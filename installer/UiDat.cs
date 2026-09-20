// ui.hed / ui.dat — YPAC 컨테이너 인덱스 읽기 + 제자리 주입 (C# 5)
//
//   헤더 16B : 'YPAC' | u32 버전(=2) | u32 엔트리 수 | u32 엔트리 크기(=72)
//   엔트리   : char name[64] (NUL 패딩, cp932) | u32 size | u32 offset
//
// 항목이 틈 없이 연속 배치되므로 크기가 바뀌면 안 된다.
// 오프셋은 하드코딩하지 않고 항상 설치본의 .hed에서 읽는다.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class UiDatException : Exception
{
    public UiDatException(string message) : base(message) { }
}

public sealed class UiDatIndex
{
    public sealed class Entry
    {
        public string Name;
        public long Size;
        public long Offset;
    }

    private List<Entry> _entries = new List<Entry>();
    public List<Entry> Entries { get { return _entries; } }

    public static UiDatIndex Load(string hedPath)
    {
        byte[] d = File.ReadAllBytes(hedPath);
        string name = Path.GetFileName(hedPath);

        // 매직·엔트리 크기·잘림 검사를 하나로 묶는다. 무엇이 틀렸는지는 사용자에게
        // 의미가 없다. 진단이 필요하면 이 자리에서 조건을 나눠 보면 된다.
        bool ok = d.Length >= 16
               && d[0] == (byte)'Y' && d[1] == (byte)'P' && d[2] == (byte)'A' && d[3] == (byte)'C';
        int count = 0, entSize = 0;
        if (ok)
        {
            count = BitConverter.ToInt32(d, 8);
            entSize = BitConverter.ToInt32(d, 12);
            ok = entSize == 72 && count > 0 && 16 + (long)count * entSize <= d.Length;
        }
        if (!ok)
            throw new UiDatException("올바른 " + name + " 파일이 아닙니다.");

        Encoding cp932;
        try { cp932 = Encoding.GetEncoding(932); }
        catch (Exception) { cp932 = Encoding.ASCII; }

        UiDatIndex idx = new UiDatIndex();
        for (int i = 0; i < count; i++)
        {
            int o = 16 + i * entSize;
            int len = 0;
            while (len < 64 && d[o + len] != 0) len++;
            Entry e = new Entry();
            e.Name = cp932.GetString(d, o, len);
            e.Size = (uint)BitConverter.ToInt32(d, o + 64);
            e.Offset = (uint)BitConverter.ToInt32(d, o + 68);
            idx._entries.Add(e);
        }
        return idx;
    }

    public Entry Find(string name)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (string.Equals(_entries[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return _entries[i];
        return null;
    }

    /// <summary>주입 전 검사만 수행. 문제가 있으면 예외.</summary>
    public Entry Check(string entryName, long payloadSize, string containerName, long containerSize)
    {
        Entry e = Find(entryName);
        if (e == null)
            throw new UiDatException(containerName + " 에 \"" + entryName + "\" 파일이 없습니다.");
        if (e.Size != payloadSize)
            throw new UiDatException(containerName + " 의 \"" + entryName + "\" 크기가 달라 넣을 수 없습니다.");
        if (e.Offset + e.Size > containerSize)
            throw new UiDatException("올바른 " + containerName + " 파일이 아닙니다.");
        return e;
    }

    /// <summary>해당 구간만 seek-write. 컨테이너 전체를 다시 쓰지 않는다.</summary>
    public static void Write(string containerPath, long offset, string payloadPath)
    {
        using (FileStream dst = new FileStream(containerPath, FileMode.Open, FileAccess.Write, FileShare.None))
        using (FileStream src = File.OpenRead(payloadPath))
        {
            dst.Seek(offset, SeekOrigin.Begin);
            byte[] buf = new byte[1024 * 1024];
            int n;
            while ((n = src.Read(buf, 0, buf.Length)) > 0)
                dst.Write(buf, 0, n);
            dst.Flush();
        }
    }
}
