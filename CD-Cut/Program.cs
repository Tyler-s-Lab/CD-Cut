using System.Diagnostics;

namespace CD_Cut {

	internal class Program {
		static void Main(string[] args) {
			Console.WriteLine("Hello, World!");

			string? cuePath = null;
			if (args.Length == 0) {
				Console.WriteLine("Path to the .cue file:");
				var s = Console.ReadLine();
				if (s != null) {
					cuePath = s;
				}
			}
			else if (File.Exists(args[0])) {
				cuePath = args[0];
			}
			if (cuePath == null) {
				Console.WriteLine("You can also drag the .cue file into this app.");
				Console.WriteLine("Press any key to continue . . .");
				Console.ReadKey();
				return;
			}
			cuePath = cuePath.Trim('"');
			ProcessOnCue(cuePath);

			Console.WriteLine("Press any key to continue . . .");
			Console.ReadKey();
		}

		private static void ProcessOnCue(string cuePath) {
			CueFile cue = new();
			cue.ReadFromFile(cuePath);

			string dir = Path.GetDirectoryName(cuePath) ?? "";
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			string cur_source = "";

			for (int i = 0, n = cue.Tracks.Count; i < n; ++i) {
				Track t = cue.Tracks[i];

				var source = CheckAudioFile(dir, t.filepath, Path.GetFileNameWithoutExtension(cuePath));
				if (string.IsNullOrEmpty(source) || !File.Exists(source)) {
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

				if (t.filepath != cur_source) {
					Console.WriteLine($"From file: {t.filepath} .");
					cur_source = t.filepath;
				}
				Console.WriteLine($"{t.index}: {t.metadata.Title} ({ss.Hours}:{ss.Minutes}:{ss.Seconds}.{ss.Milliseconds} - {ss.Hours}:{ss.Minutes}:{ss.Seconds}.{ss.Milliseconds})");

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
			if (!string.IsNullOrEmpty(t.metadata.Album))
				arglist.AddRange(["-metadata", $"ALBUM={t.metadata.Album}"]);
			if (!string.IsNullOrEmpty(t.metadata.Songwriter))
				arglist.AddRange(["-metadata", $"ARTIST={t.metadata.Songwriter}"]);
			if (!string.IsNullOrEmpty(t.metadata.Performer))
				arglist.AddRange(["-metadata", $"ALBUM_ARTIST={t.metadata.Performer}"]);
			if (!string.IsNullOrEmpty(t.metadata.Year))
				arglist.AddRange(["-metadata", $"YEAR={t.metadata.Year}"]);
			if (!string.IsNullOrEmpty(t.metadata.DiscID))
				arglist.AddRange(["-metadata", $"DISCID={t.metadata.DiscID}"]);
			if (!string.IsNullOrEmpty(t.metadata.Composer))
				arglist.AddRange(["-metadata", $"COMPOSER={t.metadata.Composer}"]);
			if (!string.IsNullOrEmpty(t.metadata.Genre))
				arglist.AddRange(["-metadata", $"GENRE={t.metadata.Genre}"]);
			if (!string.IsNullOrEmpty(t.metadata.Comment))
				arglist.AddRange(["-metadata", $"COMMENT={t.metadata.Comment}"]);
			if (!string.IsNullOrEmpty(t.metadata.Catalog))
				arglist.AddRange(["-metadata", $"CATALOG={t.metadata.Catalog}"]);
			if (!string.IsNullOrEmpty(t.metadata.ISRC))
				arglist.AddRange(["-metadata", $"ISRC={t.metadata.ISRC}"]);

			arglist.AddRange([
				"-c:a", "flac",
				targetPath
			]);

			ProcessStartInfo processStartInfo = new("ffmpeg.exe", arglist);

			using var process = Process.Start(processStartInfo);
			process?.WaitForExit();
		}

		private static string? CheckAudioFile(string dir, string filepath, string cuename) {
			string res;

			res = Path.Combine(dir, filepath);
			if (File.Exists(res))
				return res;

			filepath = Path.GetFileNameWithoutExtension(filepath);

			res = Path.Combine(dir, filepath + ".flac");
			if (File.Exists(res))
				return res;
			res = Path.Combine(dir, filepath + ".wav");
			if (File.Exists(res))
				return res;
			res = Path.Combine(dir, filepath + ".ape");
			if (File.Exists(res))
				return res;
			res = Path.Combine(dir, filepath + ".mp3");
			if (File.Exists(res))
				return res;
			res = Path.Combine(dir, filepath + ".aiff");
			if (File.Exists(res))
				return res;

			filepath = cuename;

			res = Path.Combine(dir, filepath + ".flac");
			if (File.Exists(res))
				return res;
			res = Path.Combine(dir, filepath + ".wav");
			if (File.Exists(res))
				return res;
			res = Path.Combine(dir, filepath + ".ape");
			if (File.Exists(res))
				return res;
			res = Path.Combine(dir, filepath + ".mp3");
			if (File.Exists(res))
				return res;
			res = Path.Combine(dir, filepath + ".aiff");
			if (File.Exists(res))
				return res;

			return null;
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