using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using MidiAutoPlayer.Core.MusicGame;

namespace MidiAutoPlayer.Core.Midi
{
    public class MidiFileInfo : IScoreInfo
    {

        public string Name { get; set; }

        public string FilePath { get; set; }

        public MidiFile MidiFile { get; private set; }

        public List<MidiTrack> MidiTracks { get; set; }

        public List<MidiTrack> CanPlayTracks { get; set; }

        public int NoteNumber { get; set; }

        public int CanPlayNoteNumber { get; set; }

        public double CanPlayNoteRadio => NoteNumber > 0 ? (double)CanPlayNoteNumber / NoteNumber : 0;

        public int MaxNoteLevel { get; set; }

        public int MinNoteLevel { get; set; }

        public int BestNoteLevel { get; set; }

        public double BestNoteRadio { get; set; }

        public int FrenchHornNoteNumber { get; set; }

        public int FrenchHornCanPlayNoteNumber { get; set; }

        public double FrenchHornCanPlayNoteRadio => FrenchHornNoteNumber > 0 ? (double)FrenchHornCanPlayNoteNumber / FrenchHornNoteNumber : 0;

        public int FrenchHornBestNoteLevel { get; set; }

        public double FrenchHornBestNoteRadio { get; set; }

        public int GuitarNoteNumber { get; set; }

        public int GuitarCanPlayNoteNumber { get; set; }

        public double GuitarCanPlayNoteRadio => GuitarNoteNumber > 0 ? (double)GuitarCanPlayNoteNumber / GuitarNoteNumber : 0;

        public int GuitarBestNoteLevel { get; set; }

        public double GuitarBestNoteRadio { get; set; }

        public MidiFileInfo(string path)
        {
            FilePath = path;
            MidiFile = MidiFile.Read(Path.GetFullPath(path), new ReadingSettings
            {
                InvalidChannelEventParameterValuePolicy = InvalidChannelEventParameterValuePolicy.ReadValid,
                InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore,
                InvalidMetaEventParameterValuePolicy = InvalidMetaEventParameterValuePolicy.SnapToLimits,
                MissedEndOfTrackPolicy = MissedEndOfTrackPolicy.Ignore,
                NoHeaderChunkPolicy = NoHeaderChunkPolicy.Ignore,
                NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore,
                UnexpectedTrackChunksCountPolicy = UnexpectedTrackChunksCountPolicy.Ignore,
                UnknownChannelEventPolicy = UnknownChannelEventPolicy.SkipStatusByteAndOneDataByte,
                UnknownChunkIdPolicy = UnknownChunkIdPolicy.ReadAsUnknownChunk,
                UnknownFileFormatPolicy = UnknownFileFormatPolicy.Ignore,
            });
            Name = Path.GetFileNameWithoutExtension(path);
            MidiTracks = MidiFile.GetTrackChunks().Select(x => new MidiTrack(x)).ToList();
            CanPlayTracks = MidiTracks.Where(x => x.CanBeChecked).ToList();
            CanPlayTracks.ForEach(x => x.IsCheck = true);
            NoteNumber = CanPlayTracks.Sum(x => x.NoteNumber);
            CanPlayNoteNumber = CanPlayTracks.Sum(x => x.CanPlayNoteNumber);
            FrenchHornNoteNumber = NoteNumber;
            FrenchHornCanPlayNoteNumber = CanPlayTracks.Sum(x => x.FrenchHornCanPlayNoteNumber);
            GuitarNoteNumber = NoteNumber;
            GuitarCanPlayNoteNumber = CanPlayTracks.Sum(x => x.GuitarCanPlayNoteNumber);
            if (CanPlayTracks.Count > 0)
            {
                MaxNoteLevel = CanPlayTracks.Max(x => x.MaxNoteLevel);
                MinNoteLevel = CanPlayTracks.Min(x => x.MinNoteLevel);
            }
            else
            {
                MaxNoteLevel = 0;
                MinNoteLevel = 0;
            }
        }

        // 同时计算钢琴(21键)、圆号(14键)与吉他(14单音)三套最佳移调及命中率。
        public void CalculateBestNoteLevel(InstrumentType instrumentType = InstrumentType.Piano)
        {
            var bestPianoLevel = 0;
            var bestPianoRadio = 0.0;
            var bestHornLevel = 0;
            var bestHornRadio = 0.0;
            var bestGuitarLevel = 0;
            var bestGuitarRadio = 0.0;

            for (int level = -24; level <= 24; level++)
            {
                RefreshTracksByNoteLevel(level);
                if (CanPlayNoteRadio > bestPianoRadio)
                {
                    bestPianoRadio = CanPlayNoteRadio;
                    bestPianoLevel = level;
                }
                if (FrenchHornCanPlayNoteRadio > bestHornRadio)
                {
                    bestHornRadio = FrenchHornCanPlayNoteRadio;
                    bestHornLevel = level;
                }
                if (GuitarCanPlayNoteRadio > bestGuitarRadio)
                {
                    bestGuitarRadio = GuitarCanPlayNoteRadio;
                    bestGuitarLevel = level;
                }
            }

            BestNoteLevel = bestPianoLevel;
            BestNoteRadio = bestPianoRadio;
            FrenchHornBestNoteLevel = bestHornLevel;
            FrenchHornBestNoteRadio = bestHornRadio;
            GuitarBestNoteLevel = bestGuitarLevel;
            GuitarBestNoteRadio = bestGuitarRadio;
        }

        public void RefreshTracksByNoteLevel(int noteLevel, InstrumentType instrumentType = InstrumentType.Piano)
        {
            CanPlayTracks.ForEach(x => x.RefreshByNoteLevel(noteLevel, instrumentType));
            NoteNumber = CanPlayTracks.Sum(x => x.NoteNumber);
            CanPlayNoteNumber = CanPlayTracks.Sum(x => x.CanPlayNoteNumber);
            FrenchHornNoteNumber = NoteNumber;
            FrenchHornCanPlayNoteNumber = CanPlayTracks.Sum(x => x.FrenchHornCanPlayNoteNumber);
            GuitarNoteNumber = NoteNumber;
            GuitarCanPlayNoteNumber = CanPlayTracks.Sum(x => x.GuitarCanPlayNoteNumber);
            if (CanPlayTracks.Count > 0)
            {
                MaxNoteLevel = CanPlayTracks.Max(x => x.MaxNoteLevel);
                MinNoteLevel = CanPlayTracks.Min(x => x.MinNoteLevel);
            }
            else
            {
                MaxNoteLevel = 0;
                MinNoteLevel = 0;
            }
        }

    }
}
