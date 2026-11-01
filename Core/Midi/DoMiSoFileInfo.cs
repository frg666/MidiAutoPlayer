using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MidiAutoPlayer.Core.MusicGame;

namespace MidiAutoPlayer.Core.Midi
{
    public class DoMiSoFileInfo : IScoreInfo
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public MidiFile MidiFile { get; private set; }
        public int NoteNumber { get; set; }
        public int CanPlayNoteNumber { get; set; }
        public double CanPlayNoteRadio => NoteNumber > 0 ? (double)CanPlayNoteNumber / NoteNumber : 0;
        public int BestNoteLevel { get; set; }
        public double BestNoteRadio { get; set; }
        public int FrenchHornCanPlayNoteNumber { get; set; }
        public double FrenchHornCanPlayNoteRadio => NoteNumber > 0 ? (double)FrenchHornCanPlayNoteNumber / NoteNumber : 0;
        public int FrenchHornBestNoteLevel { get; set; }
        public double FrenchHornBestNoteRadio { get; set; }

        public int GuitarCanPlayNoteNumber { get; set; }
        public double GuitarCanPlayNoteRadio => NoteNumber > 0 ? (double)GuitarCanPlayNoteNumber / NoteNumber : 0;
        public int GuitarBestNoteLevel { get; set; }
        public double GuitarBestNoteRadio { get; set; }

        private int _bpm = 80;

        private static readonly int[] BaseOffset = { 0, 0, 2, 4, 5, 7, 9, 11 };

        private static readonly Dictionary<string, int> KeySignatureOffset = new Dictionary<string, int>
        {
            { "C", 0 }, { "D", 2 }, { "E", 4 }, { "F", 5 }, { "G", 7 }, { "A", 9 }, { "B", 11 },
            { "Cb", -1 }, { "Db", 1 }, { "Eb", 3 }, { "Fb", 4 }, { "Gb", 6 }, { "Ab", 8 }, { "Bb", 10 },
            { "C#", 1 }, { "D#", 3 }, { "E#", 5 }, { "F#", 6 }, { "G#", 8 }, { "A#", 10 }, { "B#", 12 }
        };

        public DoMiSoFileInfo(string path)
        {
            FilePath = path;
            Name = Path.GetFileNameWithoutExtension(path);

            // Check if file is encrypted
            var content = IsEncryptedDmsFile(path) ? DecryptDmsFile(path) : ReadPlainText(path);

            MidiFile = ParseDmsFormat(content);
            CalculateNoteStats();
        }

        // 读取未加密的 .txt 谱子，自动检测编码。
        // 用户谱子多为 GBK/ANSI，DoMiSo 官方谱子多为 UTF-8，音符本身是 ASCII，
        // 编码只影响中文注释/标题，这里做尽量正确的自动识别。
        private static string ReadPlainText(string path)
        {
            var bytes = File.ReadAllBytes(path);

            // UTF-8 BOM
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

            // UTF-16LE BOM
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

            // 尝试严格 UTF-8 解码，成功则按 UTF-8；失败回退 GBK
            try
            {
                var utf8 = new System.Text.UTF8Encoding(false, true);
                return utf8.GetString(bytes);
            }
            catch
            {
                var gbk = System.Text.Encoding.GetEncoding(936);
                return gbk.GetString(bytes);
            }
        }

        private bool IsEncryptedDmsFile(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                if (fs.Length < 8) return false;

                var header = new byte[8];
                fs.Read(header, 0, 8);

                // Check for encrypted DMS file header: [0x80, 0xFE, 0x64, 0x6F, 0x6D, 0x69, 0x73, 0x6F]
                return header[0] == 0x80 && header[1] == 0xFE && header[2] == 0x64 && header[3] == 0x6F &&
                       header[4] == 0x6D && header[5] == 0x69 && header[6] == 0x73 && header[7] == 0x6F;
            }
            catch
            {
                return false;
            }
        }

        // Public static method for external decryption
        public static string DecryptDmsFilePublic(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            if (fs.Length < 8) throw new Exception("文件太小，不是有效的DMS文件");

            var header = new byte[8];
            fs.Read(header, 0, 8);

            // Check if encrypted
            bool isEncrypted = header[0] == 0x80 && header[1] == 0xFE && header[2] == 0x64 && header[3] == 0x6F &&
                               header[4] == 0x6D && header[5] == 0x69 && header[6] == 0x73 && header[7] == 0x6F;

            if (!isEncrypted)
            {
                // Not encrypted, read as plain text（自动检测编码）
                fs.Close();
                return ReadPlainText(path);
            }

            fs.Close();
            // Decrypt encrypted file
            return DecryptDmsFileInternal(path);
        }

        private string DecryptDmsFile(string path)
        {
            return DecryptDmsFileInternal(path);
        }

        private static string DecryptDmsFileInternal(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);

            // Initial encryption key
            var encKey = new byte[] { 0xAA, 0x55, 0xDE, 0xAD };
            var cipherKey = new byte[4];

            // Skip file header (8 bytes)
            fs.Seek(8, SeekOrigin.Begin);

            // Read cipher key (4 bytes)
            fs.Read(cipherKey, 0, 4);

            // Read plaintext length (2 bytes, UInt16)
            var lengthBytes = new byte[2];
            fs.Read(lengthBytes, 0, 2);
            var plaintextLen = BitConverter.ToUInt16(lengthBytes, 0);

            // Read ciphertext length (2 bytes, UInt16)
            fs.Read(lengthBytes, 0, 2);
            var ciphertextLen = BitConverter.ToUInt16(lengthBytes, 0);

            System.Diagnostics.Debug.WriteLine($"[Decrypt] File: {System.IO.Path.GetFileName(path)}");
            System.Diagnostics.Debug.WriteLine($"[Decrypt] CipherKey: {BitConverter.ToString(cipherKey)}");
            System.Diagnostics.Debug.WriteLine($"[Decrypt] PlaintextLen: {plaintextLen}, CiphertextLen: {ciphertextLen}");

            // Read plaintext segment
            var plaintext = new byte[plaintextLen];
            fs.Read(plaintext, 0, plaintextLen);

            // Calculate modified encryption key using plaintext
            for (int i = 0; i < plaintextLen; i++)
            {
                int keyIndex = i & 0x3;
                encKey[keyIndex] ^= plaintext[i];
            }

            // Calculate decryption key
            for (int i = 0; i < 4; i++)
            {
                cipherKey[i] ^= encKey[i];
            }

            // Read and decrypt ciphertext segment
            var ciphertext = new byte[ciphertextLen];
            fs.Read(ciphertext, 0, ciphertextLen);

            for (int i = 0; i < ciphertextLen; i++)
            {
                int keyIndex = i & 0x3;
                ciphertext[i] ^= cipherKey[keyIndex];
            }

            // Combine plaintext and decrypted ciphertext
            // DoMiSo 是 AutoHotkey 程序，加密时直接写入了字符串的 UTF-16 字节，
            // 因此 .dms 文件内容（明文段与密文段）都是 UTF-16LE 编码。
            // 之前用 GBK/UTF-8 解码都会导致中文乱码，必须用 UTF-16LE。
            var utf16 = System.Text.Encoding.Unicode; // UTF-16LE
            var plaintextStr = utf16.GetString(plaintext);
            var ciphertextStr = utf16.GetString(ciphertext);

            // Debug: show first 100 chars of decrypted content
            var combined = plaintextStr + ciphertextStr;
            var preview = combined.Length > 100 ? combined.Substring(0, 100) : combined;
            System.Diagnostics.Debug.WriteLine($"[Decrypt] First 100 chars: {preview}");

            return combined;
        }

        private MidiFile ParseDmsFormat(string content)
        {
            // Clean up extra spaces ONLY in config lines (lines with =)
            // This fixes encrypted files like "b p m = 1 3 4" -> "bpm=134"
            // But preserves spaces in note lines like "1 2 3" (3 separate notes)
            var tempLines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tempLines.Length; i++)
            {
                if (tempLines[i].Contains("="))
                {
                    // Config line - remove all spaces
                    tempLines[i] = tempLines[i].Replace(" ", "").Replace("\t", "");
                }
            }
            content = string.Join("\n", tempLines);

            int ppq = 480;
            int bpm = 80;
            int baseNote = 60;
            int keyOffset = 0;

            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var noteLines = new List<string>();

            System.Diagnostics.Debug.WriteLine($"[DoMiSo] Total lines: {lines.Length}");

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (trimmed.StartsWith(";")) continue;
                if (trimmed.StartsWith("by ", StringComparison.OrdinalIgnoreCase)) continue;

                // "======" 分隔线：之前是元数据/注释，之后才是谱面
                if (trimmed.Contains("====")) continue;

                // 元数据行（Title: / Artist: / Notator: 等），不进谱面
                if (Regex.IsMatch(trimmed, @"^(Title|Artist|Notator|Author|Source|Composer|Arranger|By)\s*:", RegexOptions.IgnoreCase))
                    continue;

                var bpmMatch = Regex.Match(trimmed, @"(?i)bpm=(\d+)");
                if (bpmMatch.Success)
                {
                    if (int.TryParse(bpmMatch.Groups[1].Value, out int parsedBpm) && parsedBpm > 30 && parsedBpm < 600)
                        bpm = parsedBpm;
                    continue;
                }

                // DoMiSo 调号格式：1=C、1=C6、1=C#、1=Cb 等。
                // 记法基准：C0=0、C1=12 … C5=60、C6=72（即 base = 八度数字 × 12）。
                // "1=C"（无数字）默认 base=60。
                var keyMatch = Regex.Match(trimmed, @"1=([A-Ga-g][#b]?)([0-9])?");
                if (keyMatch.Success)
                {
                    var key = keyMatch.Groups[1].Value.ToUpperInvariant();
                    if (KeySignatureOffset.ContainsKey(key))
                        keyOffset = KeySignatureOffset[key];

                    // "1=C6" 里的数字表示基准八度（C6 → 72），据此调整基准音高。
                    // 默认（无数字）基准为 C5=60（与 DoMiSo 的 NoteData 中 "60 C" 一致）。
                    if (keyMatch.Groups[2].Success && int.TryParse(keyMatch.Groups[2].Value, out int octaveBase))
                    {
                        baseNote = octaveBase * 12;
                    }
                    continue;
                }

                // rollback=N 是回滚标记（回滚 N 拍，用于循环段落），
                // 需要影响 currentOffset，因此保留到 noteLines，在第二个阶段处理。
                noteLines.Add(trimmed);
            }

            _bpm = bpm;
            double beatTime = 60000.0 / bpm;
            double msToTicks = ppq * bpm / 60000.0;

            System.Diagnostics.Debug.WriteLine($"[DoMiSo] Note lines count: {noteLines.Count}, BPM: {bpm}");

            var tempoMap = TempoMap.Create(new TicksPerQuarterNoteTimeDivision((short)ppq), Tempo.FromBeatsPerMinute(bpm));

            // Use Timeline model like DoMiSo original: absolute time positions
            var timeline = new SortedDictionary<long, List<MidiEvent>>();
            long currentOffset = 0;  // Current position in ticks (like Notes.Offset)
            int noteCount = 0;
            int lineIndex = 0;

            foreach (var line in noteLines)
            {
                lineIndex++;

                // rollback=N：回滚 N 拍（用于循环段落）。
                // 若回滚量超过当前已推进的偏移，则回滚到开头（0），与 DoMiSo 的 rollback_parser 一致。
                var rollbackMatch = Regex.Match(line, @"(?i)rollback\s*=\s*(\d+\.?\d*)");
                if (rollbackMatch.Success)
                {
                    if (double.TryParse(rollbackMatch.Groups[1].Value, out double rollbackBeats))
                    {
                        long rollbackTicks = (long)(rollbackBeats * beatTime * msToTicks);
                        if (rollbackTicks <= currentOffset)
                            currentOffset -= rollbackTicks;
                        else
                            currentOffset = 0;
                    }
                    continue;
                }

                var tokens = TokenizeDmsLine(line);
                bool inChord = false;
                var chordNotes = new List<(int noteNum, double noteTimeMs)>();

                // Debug first 5 lines
                if (lineIndex <= 5)
                {
                    System.Diagnostics.Debug.WriteLine($"[DoMiSo] Line {lineIndex}: {line}");
                    System.Diagnostics.Debug.WriteLine($"[DoMiSo] Tokens: {string.Join(" | ", tokens)}");
                }

                foreach (var token in tokens)
                {
                    if (token == "|") continue;

                    if (token == "(")
                    {
                        inChord = true;
                        chordNotes.Clear();
                        continue;
                    }

                    if (token == ")")
                    {
                        inChord = false;
                        if (chordNotes.Count > 0)
                        {
                            // Chord: like DoMiSo - all notes start at currentOffset, end at their own duration
                            double chordTimeMs = chordNotes.Max(n => n.noteTimeMs);
                            long chordTicks = (long)(chordTimeMs * msToTicks);
                            if (chordTicks <= 0) chordTicks = ppq / 4;

                            // Add NoteOn for all chord notes at currentOffset
                            foreach (var note in chordNotes)
                            {
                                if (!timeline.ContainsKey(currentOffset))
                                    timeline[currentOffset] = new List<MidiEvent>();
                                timeline[currentOffset].Add(new NoteOnEvent((SevenBitNumber)note.noteNum, (SevenBitNumber)100));

                                // Add NoteOff for each note at its own duration
                                long noteDuration = (long)(note.noteTimeMs * msToTicks);
                                long noteOffOffset = currentOffset + noteDuration;
                                if (!timeline.ContainsKey(noteOffOffset))
                                    timeline[noteOffOffset] = new List<MidiEvent>();
                                timeline[noteOffOffset].Add(new NoteOffEvent((SevenBitNumber)note.noteNum, (SevenBitNumber)0));
                            }

                            // Delay by chord time (like Notes.Delay(chordTime))
                            currentOffset += chordTicks;
                        }
                        continue;
                    }

                    var (noteNum, noteTimeMs) = ParseDmsNote(token, baseNote, keyOffset, beatTime);

                    // 非音符字符（如 "@"）：零时值、零音符，完全忽略，不延迟。
                    if (noteTimeMs <= 0 && noteNum <= 0)
                        continue;

                    long noteTicks = (long)(noteTimeMs * msToTicks);
                    if (noteTicks <= 0) noteTicks = ppq / 4;

                    // Debug first 5 lines' token parsing
                    if (lineIndex <= 5 && token != "(" && token != ")" && token != "|")
                    {
                        System.Diagnostics.Debug.WriteLine($"[DoMiSo]   Token '{token}' -> noteNum={noteNum}, noteTimeMs={noteTimeMs}");
                    }

                    if (inChord)
                    {
                        if (noteNum > 0)
                            chordNotes.Add((noteNum, noteTimeMs));
                    }
                    else
                    {
                        if (noteNum > 0)
                        {
                            // Normal note: like Notes.Note(noteNum, noteTicks).Delay(noteTicks)
                            // NoteOn at currentOffset
                            if (!timeline.ContainsKey(currentOffset))
                                timeline[currentOffset] = new List<MidiEvent>();
                            timeline[currentOffset].Add(new NoteOnEvent((SevenBitNumber)noteNum, (SevenBitNumber)100));

                            // NoteOff at currentOffset + noteTicks
                            long noteOffOffset = currentOffset + noteTicks;
                            if (!timeline.ContainsKey(noteOffOffset))
                                timeline[noteOffOffset] = new List<MidiEvent>();
                            timeline[noteOffOffset].Add(new NoteOffEvent((SevenBitNumber)noteNum, (SevenBitNumber)0));

                            noteCount++;
                        }
                        // Rest or note: always delay (like Notes.Delay(noteTicks))
                        currentOffset += noteTicks;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[DoMiSo] Parsed {noteCount} notes, timeline entries: {timeline.Count}");

            // Convert timeline to delta-time events (like DoMiSo's Start() method)
            var events = new List<MidiEvent>();
            long previousOffset = 0;
            foreach (var kvp in timeline)
            {
                long offset = kvp.Key;
                long deltaTime = offset - previousOffset;

                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    kvp.Value[i].DeltaTime = (i == 0) ? deltaTime : 0;
                    events.Add(kvp.Value[i]);
                }

                previousOffset = offset;
            }

            System.Diagnostics.Debug.WriteLine($"[DoMiSo] Generated {events.Count} MIDI events");

            var trackChunk = new TrackChunk();
            trackChunk.Events.AddRange(events);
            var midiFile = new MidiFile(trackChunk)
            {
                TimeDivision = new TicksPerQuarterNoteTimeDivision((short)ppq)
            };
            midiFile.ReplaceTempoMap(tempoMap);
            return midiFile;
        }

        private List<string> TokenizeDmsLine(string line)
        {
            var tokens = new List<string>();
            var current = "";

            foreach (char c in line)
            {
                if (c == '(' || c == ')' || c == '{' || c == '}')
                {
                    if (current.Length > 0) { tokens.Add(current); current = ""; }
                    // "{" "}" 多连音标记：与 "(" ")" 一样作为分隔符处理，
                    // 连音内的音符按普通音符解析（保证不丢音、不错位）。
                    if (c == '(' || c == ')')
                        tokens.Add(c.ToString());
                }
                else if (c == '|')
                {
                    if (current.Length > 0) { tokens.Add(current); current = ""; }
                    tokens.Add("|");
                }
                else if (c == ' ' || c == '\t')
                {
                    if (current.Length > 0) { tokens.Add(current); current = ""; }
                }
                else
                {
                    current += c;
                }
            }

            if (current.Length > 0) tokens.Add(current);
            return tokens;
        }

        private (int noteNum, double noteTimeMs) ParseDmsNote(string token, int baseNote, int keyOffset, double beatTime)
        {
            if (string.IsNullOrEmpty(token)) return (0, beatTime);

            var trimmed = token.Trim();
            int idx = 0;
            int octave = 0;

            // "~" 为琶音标记（如 ~+5、~6），表示该音以琶音方式演奏。
            // 音高与普通音符一致，这里去掉前缀按普通音符解析（避免整音丢失）。
            while (idx < trimmed.Length && trimmed[idx] == '~')
                idx++;

            while (idx < trimmed.Length && (trimmed[idx] == '+' || trimmed[idx] == '-'))
            {
                if (trimmed[idx] == '+') octave++;
                else octave--;
                idx++;
            }

            if (idx >= trimmed.Length) return (0, beatTime);

            if (trimmed[idx] == '0')
            {
                idx++;
                double restTime = ExtraLengthParser(beatTime, trimmed, ref idx, beatTime);
                return (0, restTime);
            }

            if (trimmed[idx] >= '1' && trimmed[idx] <= '7')
            {
                int noteValue = trimmed[idx] - '0';
                idx++;

                // 音符级升降号：# 升半音、b 降半音（如 4#、7b）。
                // 注意要先于时值解析，因为 "7b-" 里的 b 是升降号、后面的 - 才是时值延长。
                int semitone = 0;
                if (idx < trimmed.Length && (trimmed[idx] == '#' || trimmed[idx] == 'b'))
                {
                    semitone = (trimmed[idx] == '#') ? 1 : -1;
                    idx++;
                }

                double noteTime = ExtraLengthParser(beatTime, trimmed, ref idx, beatTime);

                // 不做任何八度折返：保留简谱的真实音高，超出音域的音交给移调逻辑处理。
                int noteNum = baseNote + BaseOffset[noteValue] + keyOffset + (octave * 12) + semitone;

                return (noteNum, noteTime);
            }

            // 其它非音符字符（如 "@"、标点等）应被完全忽略：不发声、也不延迟。
            // 返回 (0, 0) 表示"无音符、零时值"，主循环据此不做任何处理。
            return (0, 0);
        }

        private double ExtraLengthParser(double basetime, string src, ref int idx, double beatTime)
        {
            double lastBase = beatTime;
            var regex = new Regex(@"(\/+|\-+|\.+)");

            while (idx < src.Length)
            {
                var match = regex.Match(src, idx);
                if (!match.Success || match.Index != idx) break;

                string matched = match.Value;
                int len = matched.Length;

                if (matched[0] == '/')
                {
                    double _t = lastBase / Math.Pow(2, len);
                    basetime = basetime - lastBase + _t;
                    lastBase = _t;
                }
                else if (matched[0] == '-')
                {
                    basetime += beatTime * len;
                    lastBase = beatTime;
                }
                else if (matched[0] == '.')
                {
                    double db = lastBase;
                    for (int i = 0; i < len; i++)
                    {
                        db *= 0.5;
                        basetime += db;
                    }
                }

                idx += len;
            }

            return basetime;
        }

        private void CalculateNoteStats()
        {
            if (MidiFile == null) return;
            int totalNotes = 0, playableNotes = 0, hornPlayableNotes = 0, guitarPlayableNotes = 0;
            foreach (var trackChunk in MidiFile.GetTrackChunks())
            {
                foreach (var midiEvent in trackChunk.Events)
                {
                    if (midiEvent is NoteOnEvent noteOn && noteOn.Velocity > 0)
                    {
                        totalNotes++;
                        if (Const.NoteToVisualKeyDictionary.ContainsKey(noteOn.NoteNumber)) playableNotes++;
                        if (Const.FrenchHornNoteToVisualKeyDictionary.ContainsKey(noteOn.NoteNumber)) hornPlayableNotes++;
                        if (Const.GuitarNoteToVisualKeyDictionary.ContainsKey(noteOn.NoteNumber)) guitarPlayableNotes++;
                    }
                }
            }
            NoteNumber = totalNotes;
            CanPlayNoteNumber = playableNotes;
            FrenchHornCanPlayNoteNumber = hornPlayableNotes;
            GuitarCanPlayNoteNumber = guitarPlayableNotes;
        }

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
                if (CanPlayNoteRadio > bestPianoRadio) { bestPianoRadio = CanPlayNoteRadio; bestPianoLevel = level; }
                if (FrenchHornCanPlayNoteRadio > bestHornRadio) { bestHornRadio = FrenchHornCanPlayNoteRadio; bestHornLevel = level; }
                if (GuitarCanPlayNoteRadio > bestGuitarRadio) { bestGuitarRadio = GuitarCanPlayNoteRadio; bestGuitarLevel = level; }
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
            if (MidiFile == null) return;
            int totalNotes = 0, playableNotes = 0, hornPlayableNotes = 0, guitarPlayableNotes = 0;
            foreach (var trackChunk in MidiFile.GetTrackChunks())
            {
                foreach (var midiEvent in trackChunk.Events)
                {
                    if (midiEvent is NoteOnEvent noteOn && noteOn.Velocity > 0)
                    {
                        totalNotes++;
                        int noteNum = noteOn.NoteNumber + noteLevel;
                        if (Const.NoteToVisualKeyDictionary.ContainsKey(noteNum)) playableNotes++;
                        if (Const.FrenchHornNoteToVisualKeyDictionary.ContainsKey(noteNum)) hornPlayableNotes++;
                        if (Const.GuitarNoteToVisualKeyDictionary.ContainsKey(noteNum)) guitarPlayableNotes++;
                    }
                }
            }
            NoteNumber = totalNotes;
            CanPlayNoteNumber = playableNotes;
            FrenchHornCanPlayNoteNumber = hornPlayableNotes;
            GuitarCanPlayNoteNumber = guitarPlayableNotes;
        }
    }
}
