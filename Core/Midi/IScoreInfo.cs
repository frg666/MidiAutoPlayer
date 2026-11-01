using Melanchall.DryWetMidi.Core;
using MidiAutoPlayer.Core.MusicGame;

namespace MidiAutoPlayer.Core.Midi
{
    public interface IScoreInfo
    {
        string Name { get; }
        string FilePath { get; }
        MidiFile MidiFile { get; }
        int NoteNumber { get; set; }
        int CanPlayNoteNumber { get; set; }
        double CanPlayNoteRadio { get; }
        int BestNoteLevel { get; set; }
        double BestNoteRadio { get; set; }
        int FrenchHornCanPlayNoteNumber { get; set; }
        double FrenchHornCanPlayNoteRadio { get; }
        int FrenchHornBestNoteLevel { get; set; }
        double FrenchHornBestNoteRadio { get; set; }
        void CalculateBestNoteLevel(InstrumentType instrumentType = InstrumentType.Piano);
        void RefreshTracksByNoteLevel(int noteLevel, InstrumentType instrumentType = InstrumentType.Piano);
    }
}
