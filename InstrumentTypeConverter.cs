using System;
using System.Globalization;
using System.Windows.Data;
using MidiAutoPlayer.Core.MusicGame;

namespace MidiAutoPlayer
{
    public class InstrumentTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is InstrumentType instrumentType)
            {
                return (int)instrumentType;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return (InstrumentType)index;
            }
            return InstrumentType.Piano;
        }
    }
}
