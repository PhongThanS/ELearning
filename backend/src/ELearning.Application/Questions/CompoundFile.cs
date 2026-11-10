using System.Buffers.Binary;
using System.Text;

namespace ELearning.Application.Questions;

/// <summary>
/// Đọc một stream trong file "Compound File Binary" (OLE, ví dụ <c>word/embeddings/oleObject1.bin</c> chứa công thức
/// MathType). Chỉ đọc, đủ cho file nhỏ: FAT, DIFAT, mini stream. Dữ liệu hỏng → null, không ném lỗi.
/// </summary>
public static class CompoundFile
{
    private const int EndOfChain = -2;
    private const int MaxChain = 1 << 20;

    private static readonly byte[] Signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public static byte[]? ReadStream(byte[] file, string name)
    {
        try
        {
            return Read(file, name);
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or InvalidDataException)
        {
            return null;
        }
    }

    private static byte[]? Read(byte[] file, string name)
    {
        if (file.Length < 512 || !file.AsSpan(0, 8).SequenceEqual(Signature))
        {
            return null;
        }

        var sectorShift = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(30));
        var miniShift = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(32));
        if (sectorShift is not (9 or 12) || miniShift != 6)
        {
            return null;
        }

        var sectorSize = 1 << sectorShift;
        const int miniSize = 64;
        var fatSectors = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(44));
        var directoryStart = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(48));
        var miniCutoff = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(56));
        var miniFatStart = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(60));
        var difatStart = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(68));
        var difatSectors = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(72));

        int Offset(int sector) => checked((sector + 1) * sectorSize);

        // DIFAT: 109 mục trong header, phần còn lại nối thành chuỗi sector
        var difat = new List<int>();
        for (var i = 0; i < 109; i++)
        {
            difat.Add(BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(76 + (i * 4))));
        }

        var next = difatStart;
        for (var k = 0; k < difatSectors && next >= 0; k++)
        {
            var at = Offset(next);
            for (var i = 0; i < (sectorSize / 4) - 1; i++)
            {
                difat.Add(BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(at + (i * 4))));
            }

            next = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(at + sectorSize - 4));
        }

        var fat = new List<int>();
        foreach (var sector in difat.Where(s => s >= 0).Take(fatSectors))
        {
            var at = Offset(sector);
            for (var i = 0; i < sectorSize / 4; i++)
            {
                fat.Add(BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(at + (i * 4))));
            }
        }

        byte[] ReadChain(int start)
        {
            using var buffer = new MemoryStream();
            var visited = 0;
            for (var s = start; s >= 0; s = s < fat.Count ? fat[s] : EndOfChain)
            {
                if (++visited > MaxChain || Offset(s) + sectorSize > file.Length)
                {
                    throw new InvalidDataException("Chuỗi sector hỏng.");
                }

                buffer.Write(file, Offset(s), sectorSize);
            }

            return buffer.ToArray();
        }

        var directory = ReadChain(directoryStart);
        byte[]? miniStream = null;
        for (var i = 0; (i + 1) * 128 <= directory.Length; i++)
        {
            var entry = directory.AsSpan(i * 128, 128);
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(entry[64..]);
            var type = entry[66];
            var start = BinaryPrimitives.ReadInt32LittleEndian(entry[116..]);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(entry[120..]);
            if (type == 5)
            {
                miniStream = ReadChain(start);
                continue;
            }

            if (type != 2 || nameLength < 2 || nameLength > 64)
            {
                continue;
            }

            var entryName = Encoding.Unicode.GetString(entry[..(nameLength - 2)]);
            if (!string.Equals(entryName, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (size >= miniCutoff)
            {
                var data = ReadChain(start);
                return size <= data.Length ? data[..(int)size] : null;
            }

            if (miniStream is null)
            {
                return null;
            }

            var miniFat = new List<int>();
            var raw = ReadChain(miniFatStart);
            for (var j = 0; j + 4 <= raw.Length; j += 4)
            {
                miniFat.Add(BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(j)));
            }

            using var result = new MemoryStream();
            var count = 0;
            for (var s = start; s >= 0 && result.Length < size; s = s < miniFat.Count ? miniFat[s] : EndOfChain)
            {
                if (++count > MaxChain || (s + 1) * miniSize > miniStream.Length)
                {
                    return null;
                }

                result.Write(miniStream, s * miniSize, miniSize);
            }

            return result.Length >= size ? result.ToArray()[..(int)size] : null;
        }

        return null;
    }
}
