using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CD_Cut {
	public struct OffsetData() {
		public int Index = 0;
		public TimeSpan Time = new();
	}

	public struct TrackData() {
		public int Index = 0;

		public string Title = "";
		public string Performer = "";

		public string Composer = "";

		public string ISRC = "";

		public List<OffsetData> Offsets = [];
	}

	public struct AlbumData() {
		public string Title = "";
		public string Performer = "";

		public string Year = "";
		public string DiscID = "";
		public string Composer = "";
		public string Genre = "";
		public string Comment = "";

		public string File = "";

		public List<TrackData> AudioTracks = [];
	}

	internal class Program {
		static void Main(string[] args) {
			Console.WriteLine("Hello, World!");

			string? cuePath = null;
			if (args.Length == 0) {
				Console.WriteLine("输入 .cue 文件路径。");
				var s = Console.ReadLine();
				if (s != null) {
					cuePath = s;
				}
			}
			else if (File.Exists(args[0])) {
				cuePath = args[0];
			}
			if (cuePath == null) {
				Console.WriteLine("拖入 CD 的 .cue 文件即可。");
				return;
			}
			cuePath = cuePath.Trim('"');
			ProcessOnCue(cuePath);

			Console.WriteLine("请按任意键继续。");
			Console.ReadKey();
		}

		private static void ProcessOnCue(string cuePath) {
			using FileStream fileStream = new(cuePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using StreamReader reader = new(fileStream, true);

			AlbumData albumData = new();
			int currentTrackIndex = -1;
			string message = "";

			while (true) {
				var line = reader.ReadLine();
				if (line == null) {
					break;
				}
				var cmds = CommandLineToArgs(line);
				int index = 0;
				while (index < cmds.Length && (string.IsNullOrEmpty(cmds[index]) || string.IsNullOrWhiteSpace(cmds[index]))) {
					index++;
				}
				if (index >= cmds.Length) {
					message += $"Empty Line." + Environment.NewLine;
					continue;
				}
				if (currentTrackIndex < 0) { // Album
					string mainCmd = cmds[index++];
					switch (mainCmd.ToUpper()) {
					case "TITLE":
						albumData.Title = cmds[index++];
						break;
					case "PERFORMER":
						albumData.Performer = cmds[index++];
						break;
					case "FILE":
						albumData.File = cmds[index++];
						index++;
						break;
					case "REM":
						string secCmd = cmds[index++];
						switch (secCmd.ToUpper()) {
						case "DATE":
							albumData.Year = cmds[index++];
							break;
						case "DISCID":
							albumData.DiscID = cmds[index++];
							break;
						case "COMPOSER":
							albumData.Composer = cmds[index++];
							break;
						case "GENRE":
							albumData.Genre = cmds[index++];
							break;
						case "COMMENT":
							albumData.Comment = cmds[index++];
							break;
						default:
							message += $"Unknown 2nd command \"{secCmd}\"" + Environment.NewLine;
							break;
						}
						break;
					case "TRACK":
						TrackData td = new(){
							Index = int.Parse(cmds[index++])
						};
						if (cmds[index++].Equals("AUDIO", StringComparison.OrdinalIgnoreCase)) {
							albumData.AudioTracks.Add(td);
							currentTrackIndex = albumData.AudioTracks.IndexOf(td);
						}
						break;
					default:
						message += $"Unknown command \"{mainCmd}\"" + Environment.NewLine;
						break;
					}
					if (index != cmds.Length) {
						message += $"Command \'{line}\' 未处理完毕。";
					}
				}
				else { // Track
					TrackData trackData = albumData.AudioTracks[currentTrackIndex];
					int newTrackInbdex = currentTrackIndex;
					string mainCmd = cmds[index++];
					switch (mainCmd.ToUpper()) {
					case "TITLE":
						trackData.Title = cmds[index++];
						break;
					case "PERFORMER":
						trackData.Performer = cmds[index++];
						break;
					case "ISRC":
						trackData.ISRC = cmds[index++];
						break;
					case "REM":
						string secCmd = cmds[index++];
						switch (secCmd.ToUpper()) {
						case "COMPOSER":
							trackData.Composer = cmds[index++];
							break;
						default:
							message += $"Unknown 2nd command for track \"{mainCmd}\"" + Environment.NewLine;
							break;
						}
						break;
					case "INDEX":
						OffsetData od = new(){
							Index = int.Parse(cmds[index++]),
							Time = ParseTimeSpan(cmds[index++]),
						};
						trackData.Offsets.Add(od);
						break;
					case "TRACK":
						TrackData td = new(){
							Index = int.Parse(cmds[index++])
						};
						albumData.AudioTracks.Add(td);
						newTrackInbdex = albumData.AudioTracks.IndexOf(td);
						break;
					default:
						message += $"Unknown command for track \"{mainCmd}\"" + Environment.NewLine;
						break;
					}
					albumData.AudioTracks[currentTrackIndex] = trackData;
					currentTrackIndex = newTrackInbdex;
				}
			}
			if (!string.IsNullOrEmpty(message)) {
				Console.WriteLine(message);
			}

			Cuts(Path.GetDirectoryName(cuePath) ?? "", albumData);

			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine($"总计 {albumData.AudioTracks.Count} 个 track。");
			Console.WriteLine();
			Console.WriteLine();
		}

		public static void Cuts(string dir, AlbumData data) {
			string source = Path.IsPathFullyQualified(data.File) ? data.File : Path.Combine(dir, data.File);

			TimeSpan nextOffset = TimeSpan.Zero;
			if (data.AudioTracks.Count > 0) {
				data.AudioTracks[0].Offsets.Sort((x, y) => x.Index - y.Index);
				nextOffset = ComputeOffset(data.AudioTracks[0].Offsets.Select(x => x.Time).ToArray());
			}

			data.AudioTracks.Sort((x, y) => x.Index - y.Index);
			for (int i = 0; i < data.AudioTracks.Count; i++) {
				TimeSpan start = nextOffset;
				TimeSpan end = TimeSpan.MaxValue;
				if (i + 1 < data.AudioTracks.Count) {
					data.AudioTracks[i + 1].Offsets.Sort((x, y) => x.Index - y.Index);
					nextOffset = ComputeOffset(data.AudioTracks[i + 1].Offsets.Select(x => x.Time).ToArray());
					end = nextOffset;
				}
				Cut(source, Path.Combine(dir, $"Cuts"), start, end, data.AudioTracks[i], data);
			}
		}

		public static void Cut(string srcPath, string dstDir, TimeSpan start, TimeSpan end, TrackData data, AlbumData albumData) {
			if (!Directory.Exists(dstDir))
				Directory.CreateDirectory(dstDir);

			string filename;
			filename = $"{data.Title}.flac";
			filename = CleanNameForPath(filename);
			string targetPath = Path.Combine(dstDir, filename);

			string cmdStart = $"-loglevel warning -y -i \"{srcPath}\"";
			string cmdCut = $"-ss {start.Hours}:{start.Minutes}:{start.Seconds}.{start.Milliseconds}";
			if (end < TimeSpan.MaxValue) {
				cmdCut += $" -to {end.Hours}:{end.Minutes}:{end.Seconds}.{end.Milliseconds}";
			}
			string cmdOut = $"-c:a flac";
			string cmdMeta = "";
			if (!(string.IsNullOrEmpty(data.Title) || string.IsNullOrWhiteSpace(data.Title)))
				cmdMeta += $" -metadata TITLE=\"{data.Title}\"";
			if (!(string.IsNullOrEmpty(data.Performer) || string.IsNullOrWhiteSpace(data.Performer)))
				cmdMeta += $" -metadata AUTHOR=\"{data.Performer}\"";
			if (!(string.IsNullOrEmpty(albumData.Performer) || string.IsNullOrWhiteSpace(albumData.Performer)))
				cmdMeta += $" -metadata ALBUM_ARTIST=\"{albumData.Performer}\"";
			if (!(string.IsNullOrEmpty(albumData.Title) || string.IsNullOrWhiteSpace(albumData.Title)))
				cmdMeta += $" -metadata ALBUM=\"{albumData.Title}\"";
			if (!(string.IsNullOrEmpty(data.Composer) || string.IsNullOrWhiteSpace(data.Composer)))
				cmdMeta += $" -metadata COMPOSER=\"{data.Composer}\"";
			if (!(string.IsNullOrEmpty(albumData.Year) || string.IsNullOrWhiteSpace(albumData.Year)))
				cmdMeta += $" -metadata YEAR=\"{albumData.Year}\"";
			cmdMeta += $" -metadata TRACK=\"{data.Index}\"";
			if (!(string.IsNullOrEmpty(albumData.Genre) || string.IsNullOrWhiteSpace(albumData.Genre)))
				cmdMeta += $" -metadata GENRE=\"{albumData.Genre}\"";

			ProcessStartInfo processStartInfo = new() {
				FileName = "ffmpeg",
				Arguments = $"{cmdStart} {cmdCut} {cmdOut} {cmdMeta} \"{targetPath}\""
			};

			Console.WriteLine();
			Console.WriteLine(processStartInfo.Arguments);
			Console.WriteLine();

			using Process process = Process.Start(processStartInfo);
			process.WaitForExit();
		}

		public static TimeSpan ComputeOffset(TimeSpan[] timeSpans) {
			if (timeSpans.Length == 0) {
				throw new ArgumentException("TimeSpans too short.");
			}
			if (timeSpans.Length == 1) {
				return timeSpans[0];
			}
			return (timeSpans[0] + timeSpans[1]) / 2;
		}

		public static TimeSpan ParseTimeSpan(string str) {
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

		private static string CleanNameForPath(string name) {
			name = name.Replace(':', ';');
			foreach (char badChar in Path.GetInvalidFileNameChars()) {
				name = name.Replace(badChar, '_');
			}
			foreach (char badChar in Path.GetInvalidPathChars()) {
				name = name.Replace(badChar, '_');
			}
			return name;
		}

		public static string[] CommandLineToArgs(string commandLine) {
			var argv = CommandLineToArgvW(commandLine, out int argc);
			if (argv == IntPtr.Zero)
				throw new System.ComponentModel.Win32Exception();
			try {
				var args = new string[argc];
				for (int i = 0; i < args.Length; i++) {
					var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
					args[i] = Marshal.PtrToStringUni(p) ?? "";
				}
				return args;
			}
			finally {
				Marshal.FreeHGlobal(argv);
			}
		}
		[DllImport("shell32.dll", SetLastError = true)]
		private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

	}
}