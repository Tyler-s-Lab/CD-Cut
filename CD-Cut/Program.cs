using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CD_Cut {

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
			CueFile cue = new();
			cue.ReadFromFile(cuePath);

			Cuts(Path.GetDirectoryName(cuePath) ?? "", cue);

		}

		public static void Cuts(string dir, CueFile cue) {
			string source = Path.IsPathFullyQualified(data.File) ? data.File : Path.Combine(dir, data.File);

			TimeSpan nextOffset = TimeSpan.Zero;
			if (data.AudioTracks.Count > 0) {
				data.AudioTracks[0].Offsets.Sort((x, y) => x.Index - y.Index);
				nextOffset = ;
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

	}
}