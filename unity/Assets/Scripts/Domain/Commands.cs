using System;
using System.Collections.Generic;

namespace CatMetro.Domain
{
    // ADR-0002 §6: 8-byte record. Tick = the tick during which the command was enqueued;
    // it applies at step 1 of tick+1 (CM-R07.3).
    public readonly struct ToggleSwitchCommand
    {
        public readonly ushort SwitchId;
        public readonly int Tick;

        public ToggleSwitchCommand(ushort switchId, int tick)
        {
            SwitchId = switchId;
            Tick = tick;
        }
    }

    // Versioned envelope (contract criterion 9, A-C1-4). FormatVersion is NOT part of the
    // replay hash (criterion 10): the hash is defined over per-tick state digests only, so a
    // NEW-Q35-driven format bump is additive, never golden-breaking.
    public sealed class CommandLog
    {
        public const int CurrentFormatVersion = 1;

        public int FormatVersion { get; }
        private readonly List<ToggleSwitchCommand> _entries = new List<ToggleSwitchCommand>();

        public CommandLog(int formatVersion = CurrentFormatVersion)
        {
            FormatVersion = formatVersion;
        }

        public IReadOnlyList<ToggleSwitchCommand> Entries => _entries;

        // Append-only, receipt order preserved (CM-R07.3).
        public void Append(ToggleSwitchCommand command) => _entries.Add(command);
    }
}
