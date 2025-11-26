using System;

namespace Peekerino.Configuration
{
    public class PeekerinoOptions
    {
        public HotkeyOptions Hotkey { get; set; } = new();
        public SummaryOptions Summary { get; set; } = new();

        public class HotkeyOptions
        {
            public uint Modifier { get; set; } = 0x0002 | 0x0001;
            public uint Key { get; set; } = 0x20;
        }

        public class SummaryOptions
        {
            public int TextPreviewBytes { get; set; } = 4 * 1024;
            public int JsonMaxCharacters { get; set; } = 40_000;
            public int ArchiveMaxEntries { get; set; } = 100;
            public int ArchivePreviewBytes { get; set; } = 512;
            public int BinaryHeaderBytes { get; set; } = 256;
            public int BinaryEntropySampleBytes { get; set; } = 64 * 1024;
            public int BinaryStringScanBytes { get; set; } = 4 * 1024;
            public int BinaryStringSampleCount { get; set; } = 5;
            public int BinaryMinStringLength { get; set; } = 4;
            public int BinaryMaxStringLength { get; set; } = 40;
        }
    }
}

