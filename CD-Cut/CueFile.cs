using System.Runtime.InteropServices;

namespace CD_Cut {
	public partial class CueFile {
		bool _loaded = false;

		Metadata metadata;

		List<Track> tracks = [];

		public CueFile() { }

		public void ReadFromFile(string filepath) {
			using FileStream fileStream = new(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using StreamReader reader = new(fileStream, true);

			string? cur_file = null;
			Metadata cur_metadata = new();

			while (true) {
				var line = reader.ReadLine();
				if (line == null)
					break;
				var cmds = CommandLineToArgs(line);
				ParseLine(cmds, ref cur_file, ref cur_metadata);
			}

			_loaded = true;
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
					cur_metadata.Year ??= _3rd;
					break;
				case "DISCID":
					cur_metadata.DiscID ??= _3rd;
					break;
				case "COMPOSER":
					cur_metadata.Composer ??= _3rd;
					break;
				case "GENRE":
					cur_metadata.Genre ??= _3rd;
					break;
				case "COMMENT":
					cur_metadata.Comment ??= _3rd;
					break;
				default:
					break;
				}
				break;
			case "CATALOG":
				cur_metadata.Catalog ??= _2nd;
				break;
			case "PERFORMER":
				cur_metadata.Performer ??= _2nd;
				break;
			case "TITLE":
				cur_metadata.Title ??= _2nd;
				break;
			case "ISRC":
				cur_metadata.ISRC ??= _2nd;
				break;
			case "FILE":
				metadata = cur_metadata;
				cur_file = _2nd;
				break;
			case "TRACK":
				if (tracks.Count > 0) {
					var tmp = tracks[^1];
					tmp.metadata = cur_metadata;
					tracks[^1] = tmp;
					cur_metadata = metadata;
					cur_metadata.Album = cur_metadata.Title;
				}
				else {
					metadata = cur_metadata;
					cur_metadata.Album = cur_metadata.Title;
				}
				if (cur_file is null)
					break;
				if (_3rd?.Equals("AUDIO", StringComparison.OrdinalIgnoreCase) != true)
					break;
				tracks.Add(new Track() {
					index = int.TryParse(_2nd, out var res1) ? res1 : -1,
					filepath = cur_file
				});
				break;
			case "INDEX":
				if (tracks.Count > 0 && _3rd is not null) {
					var tmp = tracks[^1];
					tmp.offsets.Add(new OffsetData() {
						Index = int.TryParse(_2nd, out var res2) ? res2 : -1,
						Time = ParseTimeSpan(_3rd),
					});
					tracks[^1] = tmp;
				}
				break;
			default:
				break;
			}
		}

		static TimeSpan ParseTimeSpan(string str) {
			var nums = str.Split(':');
			if (nums.Length != 3) {
				throw new ArgumentException($"Time component count: {nums.Length}: {nums}");
			}
			int milliScale = nums[2].Length switch {
				1 => 100,
				2 => 10,
				_ => 1
			};
			return new TimeSpan(0, 0, int.Parse(nums[0]), int.Parse(nums[1]), int.Parse(nums[2]) * milliScale);
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
		static partial IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

	}
}
