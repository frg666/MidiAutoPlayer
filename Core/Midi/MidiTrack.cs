using System.Linq;
using Melanchall.DryWetMidi.Core;
using MidiAutoPlayer.Core.MusicGame;

namespace MidiAutoPlayer.Core.Midi
{
    public class MidiTrack
    {
        public TrackChunk Track { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsCheck { get; set; }

        public bool CanBeChecked => NoteNumber > 0;

        public int NoteNumber { get; set; }

        public int CanPlayNoteNumber { get; set; }

        public double CanPlayNoteRadio => NoteNumber > 0 ? (double)CanPlayNoteNumber / NoteNumber : 0;

        public int FrenchHornCanPlayNoteNumber { get; set; }

        public double FrenchHornCanPlayNoteRadio => NoteNumber > 0 ? (double)FrenchHornCanPlayNoteNumber / NoteNumber : 0;

        public int MaxNoteLevel { get; set; }

        public int MinNoteLevel { get; set; }

        public int FrenchHornMaxNoteLevel { get; set; }

        public int FrenchHornMinNoteLevel { get; set; }


        // 取所有真正发声的 NoteOn（velocity > 0）。
        // velocity == 0 的 NoteOn 在 MIDI 规范中等价于 NoteOff，不能计入音符数。
        private IEnumerable<NoteOnEvent> PlayableNoteOns =>
            Track.Events.Where(x => x.EventType == MidiEventType.NoteOn)
                        .Cast<NoteOnEvent>()
                        .Where(x => x.Velocity > 0);

        public MidiTrack(TrackChunk track)
        {
            Track = track;
            Name = track.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault()?.Text ?? string.Empty;
            NoteNumber = PlayableNoteOns.Count();
            RefreshByNoteLevel(0);
        }

        // 一次移调，同时刷新钢琴(21键)与圆号(14键)两套可弹数与命中率。
        public void RefreshByNoteLevel(int noteLevel, InstrumentType instrumentType = InstrumentType.Piano)
        {
            // 钢琴：NoteToVisualKeyDictionary（21 键）
            CanPlayNoteNumber = PlayableNoteOns.Count(x => Const.NoteToVisualKeyDictionary.ContainsKey(x.NoteNumber + noteLevel));
            // 圆号：FrenchHornNoteToVisualKeyDictionary（14 键）
            FrenchHornCanPlayNoteNumber = PlayableNoteOns.Count(x => Const.FrenchHornNoteToVisualKeyDictionary.ContainsKey(x.NoteNumber + noteLevel));

            if (CanBeChecked)
            {
                MaxNoteLevel = PlayableNoteOns.Max(x => x.NoteNumber + noteLevel);
                MinNoteLevel = PlayableNoteOns.Min(x => x.NoteNumber + noteLevel);
                FrenchHornMaxNoteLevel = MaxNoteLevel;
                FrenchHornMinNoteLevel = MinNoteLevel;
            }
        }
    }
}
