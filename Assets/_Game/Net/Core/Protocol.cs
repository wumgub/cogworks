using System;
using System.Collections.Generic;
using System.IO;
using Cogworks.Sim;

namespace Cogworks.Net
{
    /// <summary>
    /// Wire format. Every message is one byte of <see cref="MessageType"/> followed by its body.
    /// Transports must deliver messages reliably and in order.
    /// </summary>
    public enum MessageType : byte
    {
        /// <summary>Client → host: a command the client wants run.</summary>
        Command = 1,
        /// <summary>Host → client: the commands for one or more consecutive ticks (empty ticks included).</summary>
        TickBatch = 2,
        /// <summary>Host → client: one piece of a world snapshot.</summary>
        SnapshotPart = 3,
        /// <summary>Client → host: world hash after a tick, for desync detection.</summary>
        HashReport = 4,
    }

    public interface IHostTransport
    {
        void SendToClient(ulong clientId, byte[] message);
    }

    public interface IClientTransport
    {
        void SendToHost(byte[] message);
    }

    public static class Protocol
    {
        /// <summary>Ticks between hash comparisons (once a second).</summary>
        public const int HashInterval = Simulation.TicksPerSecond;

        /// <summary>
        /// Max bytes of snapshot per message. Kept under Unity Transport's default fragmented payload
        /// limit (6 KB) with room for headers.
        /// </summary>
        public const int SnapshotPartSize = 4096;

        /// <summary>Soft cap on a tick batch; batches are split before exceeding it.</summary>
        public const int MaxBatchBytes = 4096;

        public static byte[] EncodeCommand(Command command)
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms))
            {
                w.Write((byte)MessageType.Command);
                CommandCodec.Write(w, command);
            }
            return ms.ToArray();
        }

        public static byte[] EncodeHash(long tick, ulong hash)
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms))
            {
                w.Write((byte)MessageType.HashReport);
                w.Write(tick);
                w.Write(hash);
            }
            return ms.ToArray();
        }

        public static List<byte[]> EncodeSnapshot(int snapshotId, byte localPlayerId, byte[] snapshot)
        {
            int partCount = Math.Max(1, (snapshot.Length + SnapshotPartSize - 1) / SnapshotPartSize);
            var parts = new List<byte[]>(partCount);
            for (int i = 0; i < partCount; i++)
            {
                int offset = i * SnapshotPartSize;
                int length = Math.Min(SnapshotPartSize, snapshot.Length - offset);
                using var ms = new MemoryStream();
                using (var w = new BinaryWriter(ms))
                {
                    w.Write((byte)MessageType.SnapshotPart);
                    w.Write(snapshotId);
                    w.Write(localPlayerId);
                    w.Write((ushort)i);
                    w.Write((ushort)partCount);
                    w.Write(length);
                    w.Write(snapshot, offset, length);
                }
                parts.Add(ms.ToArray());
            }
            return parts;
        }

        /// <summary>Encodes consecutive tick bundles, splitting into several messages if large.</summary>
        public static List<byte[]> EncodeTickBatches(IReadOnlyList<TickBundle> bundles)
        {
            var messages = new List<byte[]>();
            int start = 0;
            while (start < bundles.Count)
            {
                using var ms = new MemoryStream();
                using var w = new BinaryWriter(ms);
                w.Write((byte)MessageType.TickBatch);
                long countPos = ms.Position;
                w.Write((ushort)0);
                int count = 0;
                // Always include at least one bundle so an oversized tick still goes out.
                while (start + count < bundles.Count && (count == 0 || ms.Length < MaxBatchBytes) && count < ushort.MaxValue)
                {
                    var b = bundles[start + count];
                    w.Write(b.Tick);
                    w.Write((ushort)b.Commands.Count);
                    foreach (var c in b.Commands) CommandCodec.Write(w, c);
                    count++;
                }
                w.Flush();
                ms.Position = countPos;
                w.Write((ushort)count);
                w.Flush();
                messages.Add(ms.ToArray());
                start += count;
            }
            return messages;
        }

        public static List<TickBundle> DecodeTickBatch(BinaryReader r)
        {
            int count = r.ReadUInt16();
            var list = new List<TickBundle>(count);
            for (int i = 0; i < count; i++)
            {
                long tick = r.ReadInt64();
                int n = r.ReadUInt16();
                var commands = new List<Command>(n);
                for (int c = 0; c < n; c++) commands.Add(CommandCodec.Read(r));
                list.Add(new TickBundle(tick, commands));
            }
            return list;
        }
    }

    /// <summary>The ordered commands the host ran on one tick.</summary>
    public readonly struct TickBundle
    {
        public readonly long Tick;
        public readonly List<Command> Commands;

        public TickBundle(long tick, List<Command> commands)
        {
            Tick = tick;
            Commands = commands;
        }
    }
}
