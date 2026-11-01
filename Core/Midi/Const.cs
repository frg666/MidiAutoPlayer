using System.Collections.Generic;
using MidiAutoPlayer.Core.Native;
using MidiAutoPlayer.Core.MusicGame;

namespace MidiAutoPlayer.Core.Midi
{
    static class Const
    {
        internal static Dictionary<int, VirtualKey> NoteToVisualKeyDictionary = new Dictionary<int, VirtualKey>
        {
            { 48,VirtualKey.VK_Z},
            { 50,VirtualKey.VK_X},
            { 52,VirtualKey.VK_C},
            { 53,VirtualKey.VK_V},
            { 55,VirtualKey.VK_B},
            { 57,VirtualKey.VK_N},
            { 59,VirtualKey.VK_M},

            { 60,VirtualKey.VK_A},
            { 62,VirtualKey.VK_S},
            { 64,VirtualKey.VK_D},
            { 65,VirtualKey.VK_F},
            { 67,VirtualKey.VK_G},
            { 69,VirtualKey.VK_H},
            { 71,VirtualKey.VK_J},

            { 72,VirtualKey.VK_Q},
            { 74,VirtualKey.VK_W},
            { 76,VirtualKey.VK_E},
            { 77,VirtualKey.VK_R},
            { 79,VirtualKey.VK_T},
            { 81,VirtualKey.VK_Y},
            { 83,VirtualKey.VK_U},

        };

        // 晚风圆号：两行 14 键（无 Z-M 那排）。
        //   高音谱号行 Q W E R T Y U = 60 62 64 65 67 69 71
        //   低音谱号行 A S D F G H J = 48 50 52 53 55 57 59
        // 风物之诗琴（钢琴）是三行 21 键，见 NoteToVisualKeyDictionary。
        internal static Dictionary<int, VirtualKey> FrenchHornNoteToVisualKeyDictionary = new Dictionary<int, VirtualKey>
        {
            { 48, VirtualKey.VK_A },
            { 50, VirtualKey.VK_S },
            { 52, VirtualKey.VK_D },
            { 53, VirtualKey.VK_F },
            { 55, VirtualKey.VK_G },
            { 57, VirtualKey.VK_H },
            { 59, VirtualKey.VK_J },

            { 60, VirtualKey.VK_Q },
            { 62, VirtualKey.VK_W },
            { 64, VirtualKey.VK_E },
            { 65, VirtualKey.VK_R },
            { 67, VirtualKey.VK_T },
            { 69, VirtualKey.VK_Y },
            { 71, VirtualKey.VK_U },
        };

        internal static Dictionary<int, string> NoteToCharDictionary = new Dictionary<int, string>
        {
            { 48,"z"},
            { 50,"x"},
            { 52,"c"},
            { 53,"v"},
            { 55,"b"},
            { 57,"n"},
            { 59,"m"},

            { 60,"a"},
            { 62,"s"},
            { 64,"d"},
            { 65,"f"},
            { 67,"g"},
            { 69,"h"},
            { 71,"j"},

            { 72,"q"},
            { 74,"w"},
            { 76,"e"},
            { 77,"r"},
            { 79,"t"},
            { 81,"y"},
            { 83,"u"},

        };

        // ============ 吉他（Guitar）============
        // 下两排 14 个单音键，与晚风圆号映射一致：
        //   高音谱号行 Q W E R T Y U = 60 62 64 65 67 69 71
        //   低音谱号行 A S D F G H J = 48 50 52 53 55 57 59
        internal static Dictionary<int, VirtualKey> GuitarNoteToVisualKeyDictionary = new Dictionary<int, VirtualKey>
        {
            { 48, VirtualKey.VK_A },
            { 50, VirtualKey.VK_S },
            { 52, VirtualKey.VK_D },
            { 53, VirtualKey.VK_F },
            { 55, VirtualKey.VK_G },
            { 57, VirtualKey.VK_H },
            { 59, VirtualKey.VK_J },

            { 60, VirtualKey.VK_Q },
            { 62, VirtualKey.VK_W },
            { 64, VirtualKey.VK_E },
            { 65, VirtualKey.VK_R },
            { 67, VirtualKey.VK_T },
            { 69, VirtualKey.VK_Y },
            { 71, VirtualKey.VK_U },
        };

        // 吉他上排 7 个和弦键（Z X C V B N M）。
        // 每个和弦由「组成音（以 12 为模的音级集合）」定义，便于在任意移调后仍能匹配。
        // 音级：0=C 1=C# 2=D 3=D# 4=E 5=F 6=F# 7=G 8=G# 9=A 10=A# 11=B
        internal class GuitarChord
        {
            public string Name { get; }
            public VirtualKey Key { get; }
            // 组成音的音级集合（模 12），匹配时忽略八度。
            public int[] PitchClasses { get; }

            public GuitarChord(string name, VirtualKey key, params int[] pitchClasses)
            {
                Name = name;
                Key = key;
                PitchClasses = pitchClasses;
            }
        }

        // 7 个标准和弦，顺序对应 Z X C V B N M。
        //   C  = 1-3-5   -> 0 4 7
        //   Dm = 2-4-6   -> 2 5 9
        //   Em = 3-5-7   -> 4 7 11
        //   F  = 4-6-1   -> 5 9 0
        //   G  = 5-7-2   -> 7 11 2
        //   Am = 6-1-3   -> 9 0 4
        //   G7 = 5-7-2-4 -> 7 11 2 5
        internal static List<GuitarChord> GuitarChords = new List<GuitarChord>
        {
            new GuitarChord("C",  VirtualKey.VK_Z, 0, 4, 7),
            new GuitarChord("Dm", VirtualKey.VK_X, 2, 5, 9),
            new GuitarChord("Em", VirtualKey.VK_C, 4, 7, 11),
            new GuitarChord("F",  VirtualKey.VK_V, 5, 9, 0),
            new GuitarChord("G",  VirtualKey.VK_B, 7, 11, 2),
            new GuitarChord("Am", VirtualKey.VK_N, 9, 0, 4),
            new GuitarChord("G7", VirtualKey.VK_M, 7, 11, 2, 5),
        };

        // 吉他单音键对应的字符（与 NoteToCharDictionary 的下两排一致），供调试/日志使用。
        internal static Dictionary<int, string> GuitarNoteToCharDictionary = new Dictionary<int, string>
        {
            { 48,"a"},
            { 50,"s"},
            { 52,"d"},
            { 53,"f"},
            { 55,"g"},
            { 57,"h"},
            { 59,"j"},

            { 60,"q"},
            { 62,"w"},
            { 64,"e"},
            { 65,"r"},
            { 67,"t"},
            { 69,"y"},
            { 71,"u"},
        };
    }
}
