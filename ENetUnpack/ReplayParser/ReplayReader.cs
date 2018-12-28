﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetUnpack.ReplayParser
{
    public class DataOffset
    {
        [JsonProperty("offset")]
        public int Offset { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }
    }
    public class Replay
    {
        [JsonProperty("replayVersion")]
        public string ReplayVersion { get; set; }

        [JsonProperty("clientVersion")]
        public string ClientVersion { get; set; }

        [JsonProperty("clientHash")]
        public string ClientHash { get; set; }

        [JsonProperty("encryptionKey")]
        public byte[] EncryptionKey { get; set; }

        [JsonProperty("spectatorMode")]
        public bool SpectatorMode { get; set; }

        [JsonProperty("dataIndex")]
        public List<KeyValuePair<string, DataOffset>> DataIndex { get; set; }

        public static List<ENetPacket> ReadPackets(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
            {
                // Basic header
                var _unused = reader.ReadByte();
                var _version = reader.ReadByte();
                var _compressed = reader.ReadByte();
                var _reserved = reader.ReadByte();

                // JSON data
                var _jsonLength = reader.ReadInt32();
                var _json = reader.ReadBytes(_jsonLength);
                var _replay = JsonConvert.DeserializeObject<Replay>(Encoding.UTF8.GetString(_json));

                // Binary data offset start position
                var _offsetStart = stream.Position;

                // Stream data
                var _stream = _replay.DataIndex.First(kvp => kvp.Key == "stream").Value;
                var _data = reader.ReadBytes(_stream.Size);

                if((_data[0] & 0x4C) != 0)
                {
                    _data = BDODecompress.Decompress(_data);
                }
                
                // FIXME: detect correct league version
                // TODO: determing where exact breaking changes in league ENet are??
                var _enetLeageuVersion = ENetLeagueVersion.Patch_1_0_0_106;

                // Type of parser spectator or ingame/ENet
                IChunkParser _chunkParser = null;
                if(_replay.SpectatorMode)
                {
                    throw new NotImplementedException("Spectator replays are fucky wucky!");
                }
                else
                {                    
                    _chunkParser = new ChunkParserENet(_enetLeageuVersion, _replay.EncryptionKey);
                }

                // Read "chunks" from stream and hand them over to parser
                using (var chunksReader = new BinaryReader(new MemoryStream(_data)))
                {
                    while (chunksReader.BaseStream.Position < chunksReader.BaseStream.Length)
                    {
                        var _chunkTime = chunksReader.ReadSingle();
                        var _chunkLength = chunksReader.ReadInt32();
                        var _chunkData = chunksReader.ReadBytes(_chunkLength);
                        _chunkParser.Read(_chunkData, _chunkTime);
                        var _chunkUnk = chunksReader.ReadByte();
                    }
                }

                return _chunkParser.Packets;
            }
        }
    }
}