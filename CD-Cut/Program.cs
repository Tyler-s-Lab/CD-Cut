using System.Diagnostics;

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

			string dir = Path.GetDirectoryName(cuePath) ?? "";
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			for (int i = 0, n = cue.Tracks.Count; i < n; ++i) {
				Track t = cue.Tracks[i];

				string source = Path.Combine(dir, t.filepath);
				if (!File.Exists(source)) {
					source = Path.Combine(dir, Path.GetFileNameWithoutExtension(cuePath) + ".flac");
				}
				if (!File.Exists(source)) {
					source = Path.Combine(dir, Path.GetFileNameWithoutExtension(cuePath) + ".wav");
				}
				if (!File.Exists(source)) {
					source = Path.Combine(dir, Path.GetFileNameWithoutExtension(cuePath) + ".mp3");
				}
				if (!File.Exists(source)) {
					continue;
				}

				TimeSpan ss = TimeSpan.Zero, to = TimeSpan.MaxValue;
				if (t.offset01 is double start)
					ss = TimeSpan.FromMilliseconds(start);
				if (i + 1 < n)
					if (cue.Tracks[i + 1].offset00 is double end)
						to = TimeSpan.FromMilliseconds(end);
					else if (cue.Tracks[i + 1].offset01 is double end2)
						to = TimeSpan.FromMilliseconds(end2);

				t.filepath = source;
				Console.WriteLine($"{t.index}: {t.offset01} ({t.filepath})");
				Cut(t, ss, to);
			}
		}

		public static void Cut(Track t, TimeSpan ss, TimeSpan to) {
			string filename;
			filename = $"{t.index}-{t.metadata.Title}.flac";
			filename = CleanNameForPath(filename);
			string targetPath = filename;

			List<string> arglist = [];
			arglist.AddRange([
				"-loglevel", "warning",
				"-y",
				"-i", t.filepath,
				"-ss", $"{ss.Hours}:{ss.Minutes}:{ss.Seconds}.{ss.Milliseconds}"
			]);
			if (to < TimeSpan.MaxValue)
				arglist.AddRange([
					"-to", $"{to.Hours}:{to.Minutes}:{to.Seconds}.{to.Milliseconds}"
				]);
			arglist.AddRange([
				"-metadata", $"TRACK={t.index}"
			]);

			if (!string.IsNullOrEmpty(t.metadata.Title))
				arglist.AddRange(["-metadata", $"TITLE={t.metadata.Title}"]);
			if (!string.IsNullOrEmpty(t.metadata.Performer))
				arglist.AddRange(["-metadata", $"AUTHOR={t.metadata.Performer}"]);
			if (!string.IsNullOrEmpty(t.metadata.Performer))
				arglist.AddRange(["-metadata", $"ALBUM_ARTIST={t.metadata.Performer}"]);
			if (!string.IsNullOrEmpty(t.metadata.Title))
				arglist.AddRange(["-metadata", $"ALBUM={t.metadata.Album}"]);
			if (!string.IsNullOrEmpty(t.metadata.Composer))
				arglist.AddRange(["-metadata", $"COMPOSER={t.metadata.Composer}"]);
			if (!string.IsNullOrEmpty(t.metadata.Year))
				arglist.AddRange(["-metadata", $"YEAR={t.metadata.Year}"]);
			if (!string.IsNullOrEmpty(t.metadata.Genre))
				arglist.AddRange(["-metadata", $"GENRE={t.metadata.Genre}"]);

			arglist.AddRange([
				"-c:a", "flac",
				targetPath
			]);

			ProcessStartInfo processStartInfo = new("ffmpeg.exe", arglist);

			using var process = Process.Start(processStartInfo);
			process?.WaitForExit();
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