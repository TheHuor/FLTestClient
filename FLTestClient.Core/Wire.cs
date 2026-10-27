using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;

namespace FLTestClient;

/// <summary>
/// Builds a single FL RPC packet. The header matches <c>FRpcWireStream::PutHeader</c>:
/// low byte = opcode, high byte = channel.
/// </summary>
/// <remarks>Channel 1 = COMMON, 2 = SERVER (server to client), 3 = CLIENT (client to server).</remarks>
internal sealed class PacketWriter
{
    private readonly MemoryStream stream = new();
    private readonly BinaryWriter writer;

    /// <summary>Starts a packet with channel and opcode.</summary>
    /// <param name="channel">Channel, see <see cref="Ch"/>.</param>
    /// <param name="opcode">Opcode within the channel.</param>
    public PacketWriter(int channel, int opcode)
    {
        writer = new BinaryWriter(stream, Encoding.Unicode);
        writer.Write((byte)opcode);
        writer.Write((byte)channel);
    }

    /// <summary>Appends a byte.</summary>
    /// <param name="v">Value.</param>
    /// <returns>This writer.</returns>
    public PacketWriter U8(byte v) { writer.Write(v); return this; }

    /// <summary>Appends a 16-bit word (little endian).</summary>
    /// <param name="v">Value.</param>
    /// <returns>This writer.</returns>
    public PacketWriter U16(ushort v) { writer.Write(v); return this; }

    /// <summary>Appends a 32-bit dword (little endian).</summary>
    /// <param name="v">Value.</param>
    /// <returns>This writer.</returns>
    public PacketWriter U32(uint v) { writer.Write(v); return this; }

    /// <summary>Appends a 32-bit float.</summary>
    /// <param name="v">Value.</param>
    /// <returns>This writer.</returns>
    public PacketWriter F32(float v) { writer.Write(v); return this; }

    /// <summary>Appends raw bytes.</summary>
    /// <param name="v">Data.</param>
    /// <returns>This writer.</returns>
    public PacketWriter Bytes(ReadOnlySpan<byte> v) { writer.Write(v); return this; }

    /// <summary>Appends a vector as three floats.</summary>
    /// <param name="v">Value.</param>
    /// <returns>This writer.</returns>
    public PacketWriter Vec(Vector3 v) => F32(v.X).F32(v.Y).F32(v.Z);

    /// <summary>Appends a string like <c>RpcEncodeWideString</c>: u16 length in characters, then UTF-16 without NUL.</summary>
    /// <param name="s">Text.</param>
    /// <returns>This writer.</returns>
    public PacketWriter WideString(string s)
    {
        U16((ushort)s.Length);
        writer.Write(Encoding.Unicode.GetBytes(s));
        return this;
    }

    /// <summary>Returns the finished packet.</summary>
    /// <returns>Header and payload.</returns>
    public byte[] ToArray() => stream.ToArray();
}

/// <summary>Reads fields from a received message.</summary>
internal sealed class PacketReader
{
    private readonly byte[] data;

    /// <summary>Current read position.</summary>
    public int Pos;

    /// <summary>Creates a reader starting at a position.</summary>
    /// <param name="data">Complete message.</param>
    /// <param name="pos">Start position.</param>
    public PacketReader(byte[] data, int pos = 0)
    {
        this.data = data;
        Pos = pos;
    }

    /// <summary>Number of bytes not read yet.</summary>
    public int Remaining => data.Length - Pos;

    private ReadOnlySpan<byte> Take(int n)
    {
        if (n < 0 || Pos + n > data.Length)
            throw new EndOfStreamException($"packet too short: need {n} bytes at {Pos}, have {data.Length}");
        var span = new ReadOnlySpan<byte>(data, Pos, n);
        Pos += n;
        return span;
    }

    /// <summary>Reads a byte.</summary>
    /// <returns>Value.</returns>
    public byte U8() => Take(1)[0];

    /// <summary>Reads a 16-bit word.</summary>
    /// <returns>Value.</returns>
    public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));

    /// <summary>Reads a 32-bit dword.</summary>
    /// <returns>Value.</returns>
    public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

    /// <summary>Reads a 32-bit float.</summary>
    /// <returns>Value.</returns>
    public float F32() => BinaryPrimitives.ReadSingleLittleEndian(Take(4));

    /// <summary>Reads a 64-bit double.</summary>
    /// <returns>Value.</returns>
    public double F64() => BinaryPrimitives.ReadDoubleLittleEndian(Take(8));

    /// <summary>Reads a number of bytes.</summary>
    /// <param name="n">Count.</param>
    /// <returns>Copy of the bytes.</returns>
    public byte[] Bytes(int n) => Take(n).ToArray();

    /// <summary>Skips bytes.</summary>
    /// <param name="n">Count.</param>
    public void Skip(int n) => Take(n);

    /// <summary>Reads a vector (three floats).</summary>
    /// <returns>Value.</returns>
    public Vector3 Vec() => new(F32(), F32(), F32());

    /// <summary>Reads a quaternion in FL order w, x, y, z (<c>Common Quaternion</c>).</summary>
    /// <returns>Value.</returns>
    public Quaternion Quat() { float w = F32(), x = F32(), y = F32(), z = F32(); return new Quaternion(x, y, z, w); }

    /// <summary>Reads fixed-length ASCII text.</summary>
    /// <param name="n">Length in bytes.</param>
    /// <returns>Text.</returns>
    public string Ascii(int n) => Encoding.ASCII.GetString(Take(n));

    /// <summary>Reads fixed-length UTF-16 text.</summary>
    /// <param name="chars">Length in characters.</param>
    /// <returns>Text.</returns>
    public string Wide(int chars) => Encoding.Unicode.GetString(Take(chars * 2));
}

/// <summary>Message compression like <c>CDPNetCodec</c> (zlib).</summary>
internal static class Codec
{
    /// <summary>
    /// Tries to inflate a message as a zlib stream; if that fails, the message is raw.
    /// The server compresses batched messages of 32 bytes or more, the client always sends raw data.
    /// </summary>
    /// <param name="data">Received message.</param>
    /// <returns>Inflated or unchanged data.</returns>
    public static byte[] MaybeInflate(byte[] data)
    {
        if (data.Length < 2 || data[0] != 0x78)
            return data;
        try
        {
            using var input = new MemoryStream(data);
            using var z = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            z.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return data;
        }
    }
}

/// <summary>
/// Binary render display list format used for chat text (<c>BinaryRDLWriter</c>):
/// records of u32 type, u32 length and payload; type 2 = TEXT (UTF-16 with NUL).
/// </summary>
public static class Rdl
{
    /// <summary>Creates an RDL with a single TEXT record, the way the client sends chat.</summary>
    /// <param name="text">Chat text.</param>
    /// <returns>Binary RDL.</returns>
    public static byte[] Text(string text)
    {
        var body = Encoding.Unicode.GetBytes(text + "\0");
        var buf = new byte[8 + body.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, 2);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4), (uint)body.Length);
        body.CopyTo(buf, 8);
        return buf;
    }

    /// <summary>Extracts the plain text from a binary RDL (like <c>BinaryRDLReader::extract_text_from_buffer</c>).</summary>
    /// <param name="rdl">Binary RDL.</param>
    /// <returns>All TEXT records concatenated.</returns>
    public static string ExtractText(byte[] rdl)
    {
        var sb = new StringBuilder();
        int pos = 0;
        while (pos + 8 <= rdl.Length)
        {
            uint type = BinaryPrimitives.ReadUInt32LittleEndian(rdl.AsSpan(pos));
            int len = (int)BinaryPrimitives.ReadUInt32LittleEndian(rdl.AsSpan(pos + 4));
            if (len < 0 || pos + 8 + len > rdl.Length)
                break;
            if (type == 2)
                sb.Append(Encoding.Unicode.GetString(rdl, pos + 8, len).TrimEnd('\0'));
            pos += 8 + len;
        }
        return sb.ToString();
    }
}

/// <summary>
/// <c>CreateID</c> from Common.dll: nickname to FL hash. CRC with a reflected table built from 0xA001 &lt;&lt; 14,
/// lower case, byte swap, then <c>&gt;&gt; 2 | 0x80000000</c>.
/// </summary>
/// <example><c>FLHash.CreateID("li01") == 0x83A78E4F</c></example>
public static class FLHash
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        const uint poly = 0xA001u << (30 - 16);
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ poly : crc >> 1;
            table[i] = crc;
        }
        return table;
    }

    /// <summary>Computes the FL hash of a nickname (case-insensitive).</summary>
    /// <param name="nickname">For example <c>Li01_01_Base</c>.</param>
    /// <returns>Hash as used by server and client.</returns>
    public static uint CreateID(string nickname)
    {
        uint hash = 0;
        foreach (byte c in Encoding.ASCII.GetBytes(nickname.ToLowerInvariant()))
            hash = (hash >> 8) ^ Table[(byte)hash ^ c];
        hash = BinaryPrimitives.ReverseEndianness(hash);
        return (hash >> 2) | 0x80000000u;
    }
}
