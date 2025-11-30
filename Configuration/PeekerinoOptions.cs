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
            public int ExcelMaxSheets { get; set; } = 3;
            public int ExcelMaxRows { get; set; } = 100;
            public int ExcelMaxColumns { get; set; } = 20;
            public int ExcelMaxRowsToScan { get; set; } = 2_000;
            public int ExcelMaxCellCharacters { get; set; } = 120;
            public int MarkdownMaxCharacters { get; set; } = 20_000;
            public int BinaryHeaderBytes { get; set; } = 256;
            public int BinaryEntropySampleBytes { get; set; } = 64 * 1024;
            public int BinaryStringScanBytes { get; set; } = 4 * 1024;
            public int BinaryStringSampleCount { get; set; } = 5;
            public int BinaryMinStringLength { get; set; } = 4;
            public int BinaryMaxStringLength { get; set; } = 40;
            public int YxdbMaxRows { get; set; } = 15;
            public int YxdbMaxColumns { get; set; } = 25;
            public int YxdbMaxValueLength { get; set; } = 200;
        }
    }
}

