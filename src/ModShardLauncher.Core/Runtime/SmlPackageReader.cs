using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ModShardLauncher.Mods;
using Serilog;

namespace ModShardLauncher;

/// <summary>
/// Reads the packed <c>.sml</c> container produced by ModShardPacker:
/// magic "MSLM", a version string, four chunk tables and the compiled mod assembly.
/// The on-disk layout is a compatibility contract and is intentionally unchanged.
/// </summary>
public static class SmlPackageReader
{
    private const string Magic = "MSLM";

    public static ModFile? Read(string path)
    {
        FileStream stream = new(path, FileMode.Open);
        string modName = Path.GetFileNameWithoutExtension(path);

        ModFile file = new()
        {
            Stream = stream,
            Name = modName,
        };

        if (Encoding.UTF8.GetString(ReadBytes(stream, 4)) != Magic)
        {
            stream.Close();
            return null;
        }

        Regex versionDigits = new("0([0-9])");
        const byte dot = 0x2E;
        const byte zero = 0x30;
        const byte nine = 0x39;

        // The version string is at most 24 bytes, terminated by the first byte that is
        // neither a dot nor a digit.
        byte[] versionPeek = ReadBytes(stream, 24);
        int versionLength = 24;
        for (int i = 1; i < 24; i++)
        {
            if (versionPeek[i] != dot && (versionPeek[i] > nine || versionPeek[i] < zero))
            {
                versionLength = i;
                break;
            }
        }

        if (versionLength == 24)
        {
            Log.Warning("Version number seems ill formed");
        }

        stream.Seek(-24, SeekOrigin.Current);
        byte[] versionBytes = ReadBytes(stream, versionLength);

        file.Version = versionDigits.Replace(Encoding.UTF8.GetString(versionBytes), "$1");
        Log.Information("Reading {{{0}}} built with version {{{1}}}", modName, file.Version);

        ReadChunkTable(stream, file);
        ReadChunkTable(stream, file);
        ReadChunkTable(stream, file);
        ReadChunkTable(stream, file);

        file.FileOffset = (int)stream.Position;

        if (file.Files.Count > 0)
        {
            FileChunk last = file.Files[^1];
            ReadBytes(stream, last.offset + last.length);
        }

        int assemblyLength = BitConverter.ToInt32(ReadBytes(stream, 4), 0);
        file.Assembly = ModAssemblyLoadContext.Instance.LoadMod(ReadBytes(stream, assemblyLength));
        file.Path = path;

        try
        {
            file.Icon = file.GetFile(file.Name + "\\icon.png");
        }
        catch
        {
            Log.Information("Cannot find the icon.png associated to {0}", Path.GetFileName(path));
        }

        stream.Close();
        file.Stream = null;
        return file;
    }

    private static void ReadChunkTable(FileStream stream, ModFile file)
    {
        int count = BitConverter.ToInt32(ReadBytes(stream, 4), 0);
        for (int i = 0; i < count; i++)
        {
            int nameLength = BitConverter.ToInt32(ReadBytes(stream, 4), 0);
            FileChunk chunk = new()
            {
                name = Encoding.UTF8.GetString(ReadBytes(stream, nameLength)),
                offset = BitConverter.ToInt32(ReadBytes(stream, 4), 0),
                length = BitConverter.ToInt32(ReadBytes(stream, 4), 0),
            };
            file.Files.Add(chunk);
        }
    }

    internal static byte[] ReadBytes(FileStream stream, int length)
    {
        byte[] bytes = new byte[length];
        if (stream.Length - stream.Position < length)
        {
            stream.Close();
            throw new IOException(
                $"In SmlPackageReader.ReadBytes cannot read {length} bytes in the mod {Path.GetFileName(stream.Name)}");
        }

        stream.Read(bytes, 0, length);
        return bytes;
    }
}
