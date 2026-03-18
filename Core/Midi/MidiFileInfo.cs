using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Core;

namespace MidiAutoPlayer.Core.Midi
{
    public class MidiFileInfo
    {

        public string Name { get; set; }

        public string FilePath { get; set; }

        public FileInfo FileInfo { get; private set; }

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
            MaxNoteLevel = CanPlayTracks.Max(x => x.MaxNoteLevel);
            MinNoteLevel = CanPlayTracks.Min(x => x.MinNoteLevel);
        }

        public void CalculateBestNoteLevel()
        {
            var originalLevel = 0;
            var bestLevel = 0;
            var bestRadio = 0.0;

            for (int level = -24; level <= 24; level++)
            {
                RefreshTracksByNoteLevel(level);
                var radio = CanPlayNoteRadio;
                if (radio > bestRadio)
                {
                    bestRadio = radio;
                    bestLevel = level;
                }
            }

            RefreshTracksByNoteLevel(originalLevel);
            BestNoteLevel = bestLevel;
            BestNoteRadio = bestRadio;
        }

        public void RefreshTracksByNoteLevel(int noteLevel)
        {
            CanPlayTracks.ForEach(x => x.RefreshByNoteLevel(noteLevel));
            NoteNumber = CanPlayTracks.Sum(x => x.NoteNumber);
            CanPlayNoteNumber = CanPlayTracks.Sum(x => x.CanPlayNoteNumber);
            MaxNoteLevel = CanPlayTracks.Max(x => x.MaxNoteLevel);
            MinNoteLevel = CanPlayTracks.Min(x => x.MinNoteLevel);
        }

    }
}
