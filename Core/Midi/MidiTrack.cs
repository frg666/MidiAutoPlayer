using System.Linq;
using Melanchall.DryWetMidi.Core;
using MidiAutoPlayer.Core.MusicGame;

namespace MidiAutoPlayer.Core.Midi
{
    public class MidiTrack
    {
        public TrackChunk Track { get; set; }

        public string Name { get; set; }

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


        public MidiTrack(TrackChunk track)
        {
            Track = track;
            Name = track.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault()?.Text;
            NoteNumber = track.Events.Count(x => x.EventType == MidiEventType.NoteOn);
            RefreshByNoteLevel(0, InstrumentType.Piano);
            RefreshFrenchHornNoteLevel(0);
        }

        public void RefreshByNoteLevel(int noteLevel, InstrumentType instrumentType = InstrumentType.Piano)
        {
            var dictionary = instrumentType == InstrumentType.FrenchHorn 
                ? Const.FrenchHornNoteToVisualKeyDictionary 
                : Const.NoteToVisualKeyDictionary;
            
            CanPlayNoteNumber = Track.Events.Where(x => x.EventType == MidiEventType.NoteOn).Count(x => dictionary.ContainsKey((x as NoteOnEvent).NoteNumber + noteLevel));
            
            if (CanBeChecked)
            {
                MaxNoteLevel = Track.Events.Where(x => x.EventType == MidiEventType.NoteOn).Max(x => (x as NoteOnEvent).NoteNumber + noteLevel);
                MinNoteLevel = Track.Events.Where(x => x.EventType == MidiEventType.NoteOn).Min(x => (x as NoteOnEvent).NoteNumber + noteLevel);
            }
        }

        public void RefreshFrenchHornNoteLevel(int noteLevel)
        {
            var frenchHornDictionary = Const.FrenchHornNoteToVisualKeyDictionary;
            
            FrenchHornCanPlayNoteNumber = Track.Events.Where(x => x.EventType == MidiEventType.NoteOn).Count(x => frenchHornDictionary.ContainsKey((x as NoteOnEvent).NoteNumber + noteLevel));
            
            if (CanBeChecked)
            {
                FrenchHornMaxNoteLevel = Track.Events.Where(x => x.EventType == MidiEventType.NoteOn).Max(x => (x as NoteOnEvent).NoteNumber + noteLevel);
                FrenchHornMinNoteLevel = Track.Events.Where(x => x.EventType == MidiEventType.NoteOn).Min(x => (x as NoteOnEvent).NoteNumber + noteLevel);
            }
        }
    }
}
