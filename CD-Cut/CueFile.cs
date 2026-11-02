using System.Runtime.InteropServices;
using UtfUnknown;

namespace CD_Cut {
	public partial class CueFile {

		Metadata albumMetadata;
		bool albumCaptured;
		int pendingTrackIndex = -1;

		readonly List<Track> tracks = [];
		public List<Track> Tracks {
			get { return tracks; }
		}

		public Metadata AlbumMetadata {
			get { return albumMetadata; }
		}

		public CueFile() { }

		public void ReadFromFile(string filepath) {
			using var memoryStream = new MemoryStream();
			using (var fileStream = File.OpenRead(filepath)) {
				fileStream.CopyTo(memoryStream);
				fileStream.Close();
			}
			memoryStream.Position = 0;
			var encoding = CharsetDetector.DetectFromStream(memoryStream).Detected?.Encoding;
			memoryStream.Position = 0;

			using var reader = encoding is null ?
				new StreamReader(memoryStream, true) :
				new StreamReader(memoryStream, encoding);

			string? cur_file = null;
			Metadata cur_metadata = new();

			while (true) {
				var line = reader.ReadLine();
				if (line == null)
					break;
				var cmds = CommandLineToArgs(line);
				ParseLine(cmds, ref cur_file, ref cur_metadata);
			}

			CommitPendingTrack(cur_metadata);
		}

		void CommitPendingTrack(Metadata value) {
			if (pendingTrackIndex < 0)
				return;
			var track = tracks[pendingTrackIndex];
			track.metadata = value;
			tracks[pendingTrackIndex] = track;
			pendingTrackIndex = -1;
		}

		void ParseLine(IEnumerable<string> cmds, ref string? cur_file, ref Metadata cur_metadata) {
			var it = cmds.GetEnumerator();

			string? _1st = null, _2nd = null, _3rd = null;
			if (it.MoveNext())
				_1st = it.Current;
			if (it.MoveNext())
				_2nd = it.Current;
			if (it.MoveNext())
				_3rd = it.Current;

			switch (_1st?.ToUpper()) {
			case null:
				break;
			case "REM":
				switch (_2nd?.ToUpper()) {
				case "DATE":
					cur_metadata.Year = _3rd;
					break;
				case "DISCID":
					cur_metadata.DiscID = _3rd;
					break;
				case "COMPOSER":
					cur_metadata.Composer = _3rd;
					break;
				case "GENRE":
					cur_metadata.Genre = _3rd;
					break;
				case "COMMENT":
					cur_metadata.Comment = _3rd;
					break;
				default:
					break;
				}
				break;
			case "CATALOG":
				cur_metadata.Catalog = _2nd;
				break;
			case "PERFORMER":
				cur_metadata.Performer = _2nd;
				break;
			case "SONGWRITER":
				cur_metadata.Songwriter = _2nd;
				break;
			case "TITLE":
				cur_metadata.Title = _2nd;
				break;
			case "ISRC":
				cur_metadata.ISRC = _2nd;
				break;
			case "FILE":
				cur_file = _2nd;
				break;
			case "TRACK":
				if (cur_file is null)
					break;

				// Album metadata is everything before the first TRACK line, once.
				// FILE must not overwrite it: in a multi-FILE cue the second FILE
				// line appears after the previous track's metadata.
				if (!albumCaptured) {
					albumMetadata = cur_metadata;
					albumCaptured = true;
				}
				CommitPendingTrack(cur_metadata);

				cur_metadata = albumMetadata;
				cur_metadata.Album = albumMetadata.Title;

				if (_3rd?.Equals("AUDIO", StringComparison.OrdinalIgnoreCase) != true)
					break;
				tracks.Add(new Track() {
					index = int.TryParse(_2nd, out var res1) ? res1 : -1,
					filepath = cur_file
				});
				pendingTrackIndex = tracks.Count - 1;
				break;
			case "INDEX":
				if (tracks.Count > 0 && _3rd is not null) {
					if (int.TryParse(_2nd, out var index)) {
						var tmp = tracks[^1];
						switch (index) {
						case 0:
							tmp.offset00 = ParseTimeSpan(_3rd);
							break;
						case 1:
							tmp.offset01 = ParseTimeSpan(_3rd);
							break;
						}
						tracks[^1] = tmp;
					}
				}
				break;
			default:
				break;
			}
		}

		static double ParseTimeSpan(string str) {
			var nums = str.Split(':');
			if (nums.Length != 3)
				throw new ArgumentException($"Time component count: {nums.Length}: {nums}");
			return (int.Parse(nums[0]) * 60.0 + int.Parse(nums[1]) + int.Parse(nums[2]) / 75.0) * 1000.0;
		}

		static IEnumerable<string> CommandLineToArgs(string commandLine) {
			var argv = CommandLineToArgvW(commandLine, out int argc);
			if (argv == IntPtr.Zero)
				throw new System.ComponentModel.Win32Exception();
			try {
				var args = new string[argc];
				for (int i = 0; i < args.Length; i++) {
					var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
					args[i] = Marshal.PtrToStringUni(p) ?? "";
				}
				return args.Where(x => !string.IsNullOrEmpty(x));
			}
			finally {
				Marshal.FreeHGlobal(argv);
			}
		}

		[LibraryImport("shell32.dll", SetLastError = true)]
		private static partial IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

	}
}
