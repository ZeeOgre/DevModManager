using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DMM.AssetManagers.GameStores.BattleNet;

/// <summary>
/// Minimal proto3 wire decoder for Battle.net Agent "product.db".
/// Schema reference: ProductDb / ProductInstall / UserSettings / CachedProductState / BaseProductState
/// from galaxy_blizzard_plugin/src/product_db.proto (MIT). :contentReference[oaicite:2]{index=2}
///
/// We intentionally decode only the fields DMM needs:
/// - ProductDb.product_installs (field 1)
/// - ProductInstall.uid (1), product_code (2), settings (3), cached_product_state (4)
/// - UserSettings.install_path (1), play_region (2), versionbranch (10)
/// - CachedProductState.base_product_state (1)
/// - BaseProductState.installed (1), playable (2), current_version (6), current_version_str (7)
/// </summary>
internal static class BattleNetProductDb
{
    internal sealed record ProductInstallInfo(
        string? Uid,
        string? ProductCode,
        string? InstallPath,
        string? PlayRegion,
        string? VersionBranch,
        bool? Installed,
        bool? Playable,
        string? CurrentVersion,
        string? CurrentVersionStr
    );

    public static IReadOnlyList<ProductInstallInfo> TryReadProductDb(string productDbPath)
    {
        var bytes = File.ReadAllBytes(productDbPath);
        return ParseProductDb(bytes);
    }

    public static IReadOnlyList<ProductInstallInfo> ParseProductDb(ReadOnlySpan<byte> data)
    {
        var installs = new List<ProductInstallInfo>();

        // ProductDb:
        // repeated ProductInstall product_installs = 1;  (wire type 2 / len-delimited message)
        var r = new Reader(data);

        while (!r.Eof)
        {
            var (field, wire) = r.ReadTag();
            if (field == 1 && wire == WireType.LengthDelimited)
            {
                var msg = r.ReadBytes();
                installs.Add(ParseProductInstall(msg));
            }
            else
            {
                r.Skip(wire);
            }
        }

        return installs;
    }

    private static ProductInstallInfo ParseProductInstall(ReadOnlySpan<byte> msg)
    {
        string? uid = null;
        string? productCode = null;

        string? installPath = null;
        string? playRegion = null;
        string? versionBranch = null;

        bool? installed = null;
        bool? playable = null;
        string? currentVersion = null;
        string? currentVersionStr = null;

        var r = new Reader(msg);
        while (!r.Eof)
        {
            var (field, wire) = r.ReadTag();
            switch (field)
            {
                case 1 when wire == WireType.LengthDelimited:
                    uid = r.ReadString();
                    break;

                case 2 when wire == WireType.LengthDelimited:
                    productCode = r.ReadString();
                    break;

                case 3 when wire == WireType.LengthDelimited:
                    {
                        var settingsMsg = r.ReadBytes();
                        ParseUserSettings(settingsMsg, ref installPath, ref playRegion, ref versionBranch);
                        break;
                    }

                case 4 when wire == WireType.LengthDelimited:
                    {
                        var cpsMsg = r.ReadBytes();
                        ParseCachedProductState(cpsMsg, ref installed, ref playable, ref currentVersion, ref currentVersionStr);
                        break;
                    }

                default:
                    r.Skip(wire);
                    break;
            }
        }

        return new ProductInstallInfo(
            uid, productCode,
            installPath, playRegion, versionBranch,
            installed, playable,
            currentVersion, currentVersionStr
        );
    }

    private static void ParseUserSettings(
        ReadOnlySpan<byte> msg,
        ref string? installPath,
        ref string? playRegion,
        ref string? versionBranch)
    {
        // UserSettings:
        // string install_path = 1;
        // string play_region  = 2;
        // string versionbranch = 10;
        var r = new Reader(msg);

        while (!r.Eof)
        {
            var (field, wire) = r.ReadTag();
            switch (field)
            {
                case 1 when wire == WireType.LengthDelimited:
                    installPath ??= r.ReadString();
                    break;

                case 2 when wire == WireType.LengthDelimited:
                    playRegion ??= r.ReadString();
                    break;

                case 10 when wire == WireType.LengthDelimited:
                    versionBranch ??= r.ReadString();
                    break;

                default:
                    r.Skip(wire);
                    break;
            }
        }
    }

    private static void ParseCachedProductState(
        ReadOnlySpan<byte> msg,
        ref bool? installed,
        ref bool? playable,
        ref string? currentVersion,
        ref string? currentVersionStr)
    {
        // CachedProductState:
        // BaseProductState base_product_state = 1;
        var r = new Reader(msg);

        while (!r.Eof)
        {
            var (field, wire) = r.ReadTag();
            if (field == 1 && wire == WireType.LengthDelimited)
            {
                var bpsMsg = r.ReadBytes();
                ParseBaseProductState(bpsMsg, ref installed, ref playable, ref currentVersion, ref currentVersionStr);
            }
            else
            {
                r.Skip(wire);
            }
        }
    }

    private static void ParseBaseProductState(
        ReadOnlySpan<byte> msg,
        ref bool? installed,
        ref bool? playable,
        ref string? currentVersion,
        ref string? currentVersionStr)
    {
        // BaseProductState:
        // bool installed = 1;
        // bool playable  = 2;
        // string current_version = 6;
        // string current_version_str = 7;
        var r = new Reader(msg);

        while (!r.Eof)
        {
            var (field, wire) = r.ReadTag();
            switch (field)
            {
                case 1 when wire == WireType.Varint:
                    installed ??= r.ReadBool();
                    break;

                case 2 when wire == WireType.Varint:
                    playable ??= r.ReadBool();
                    break;

                case 6 when wire == WireType.LengthDelimited:
                    currentVersion ??= r.ReadString();
                    break;

                case 7 when wire == WireType.LengthDelimited:
                    currentVersionStr ??= r.ReadString();
                    break;

                default:
                    r.Skip(wire);
                    break;
            }
        }
    }

    // ---- Proto3 wire reader (minimal) ----

    private enum WireType : int
    {
        Varint = 0,
        Fixed64 = 1,
        LengthDelimited = 2,
        Fixed32 = 5,
    }

    private ref struct Reader
    {
        private ReadOnlySpan<byte> _data;
        private int _pos;

        public Reader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _pos = 0;
        }

        public bool Eof => _pos >= _data.Length;

        public (int Field, WireType Wire) ReadTag()
        {
            var key = ReadVarint32();
            var field = key >> 3;
            var wire = (WireType)(key & 0x7);
            return (field, wire);
        }

        public bool ReadBool() => ReadVarint32() != 0;

        public string ReadString()
        {
            var bytes = ReadBytes();
            return Encoding.UTF8.GetString(bytes);
        }

        public ReadOnlySpan<byte> ReadBytes()
        {
            var len = ReadVarint32();
            if (len < 0 || _pos + len > _data.Length)
                throw new InvalidDataException("Invalid length-delimited field.");
            var slice = _data.Slice(_pos, len);
            _pos += len;
            return slice;
        }

        public void Skip(WireType wire)
        {
            switch (wire)
            {
                case WireType.Varint:
                    _ = ReadVarint64();
                    return;

                case WireType.Fixed64:
                    _pos += 8;
                    if (_pos > _data.Length) throw new InvalidDataException("Skip past EOF (fixed64).");
                    return;

                case WireType.Fixed32:
                    _pos += 4;
                    if (_pos > _data.Length) throw new InvalidDataException("Skip past EOF (fixed32).");
                    return;

                case WireType.LengthDelimited:
                    {
                        var len = ReadVarint32();
                        _pos += len;
                        if (_pos > _data.Length) throw new InvalidDataException("Skip past EOF (len-delimited).");
                        return;
                    }

                default:
                    throw new InvalidDataException($"Unknown wire type: {(int)wire}");
            }
        }

        private int ReadVarint32()
        {
            ulong v = ReadVarint64();
            if (v > int.MaxValue) return unchecked((int)v);
            return (int)v;
        }

        private ulong ReadVarint64()
        {
            ulong result = 0;
            int shift = 0;

            while (true)
            {
                if (_pos >= _data.Length) throw new InvalidDataException("Unexpected EOF in varint.");
                byte b = _data[_pos++];
                result |= (ulong)(b & 0x7F) << shift;

                if ((b & 0x80) == 0) return result;

                shift += 7;
                if (shift > 63) throw new InvalidDataException("Varint too long.");
            }
        }
    }
}