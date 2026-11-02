using System.Diagnostics;
using System.Globalization;

namespace CD_Cut {

	internal class Program {
		static int Main(string[] args) {
			string? cuePath = null;

			if (args.Length == 0) {
				Console.WriteLine("Path to the .cue file:");
				var s = Console.ReadLine();
				if (!string.IsNullOrWhiteSpace(s))
					cuePath = s;
			}
			else {
				// Explorer passes a quoted path when a file is dragged onto the exe.
				cuePath = args[0].Trim('"');
			}

			cuePath = cuePath?.Trim().Trim('"');

			if (string.IsNullOrEmpty(cuePath) || !File.Exists(cuePath)) {
				if (!string.IsNullOrEmpty(cuePath))
					Console.Error.WriteLine($"Cue file not found: {cuePath}");
				Console.WriteLine("You can also drag the .cue file into this app.");
				Pause();
				return 1;
			}

			int failures = ProcessOnCue(cuePath);
			Pause();
			return failures == 0 ? 0 : 1;
		}

		private static void Pause() {
			if (Console.IsInputRedirected)
				return;
			Console.WriteLine("Press any key to continue . . .");
			Console.ReadKey();
		}

		private static int ProcessOnCue(string cuePath) {
			cuePath = Path.GetFullPath(cuePath);
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
				double startMs = 0;
				if (t.offset01 is double start) {
					startMs = start;
					ss = TimeSpan.FromMilliseconds(start);
				}

				// INDEX times restart at zero for every FILE block, so the next
				// track only delimits this one when both belong to the same file.
				if (i + 1 < n &&
					string.Equals(cue.Tracks[i].filepath, cue.Tracks[i + 1].filepath, StringComparison.OrdinalIgnoreCase)) {
					Track next = cue.Tracks[i + 1];
					double? endMs = next.offset00 ?? next.offset01;
					if (endMs is double end) {
						if (end <= startMs) {
							Console.Error.WriteLine(
								$"Track {t.index}: end {FormatTime(TimeSpan.FromMilliseconds(end))} is not after start {FormatTime(ss)}; skipping.");
							continue;
						}
						to = TimeSpan.FromMilliseconds(end);
					}
				}

				t.filepath = source;

				if (t.filepath != cur_source) {
					Console.WriteLine($"From file: {t.filepath} .");
					cur_source = t.filepath;
				}
				string endText = to < TimeSpan.MaxValue ? FormatTime(to) : "end of file";
				Console.WriteLine($"{t.index}: {t.metadata.Title} ({FormatTime(ss)} - {endText})");

				Cut(t, ss, to);
			}

			return 0;
		}

		public static void Cut(Track t, TimeSpan ss, TimeSpan to) {
			string filename;
			filename = $"{t.index}-{t.metadata.Title}.flac";
			filename = CleanNameForPath(filename);
			string targetPath = filename;

			List<string> arglist = [];
			// -ss and -to must both be input options: -to is measured against the input
			// timeline, while an output -to would be relative to the seek point. Seeking
			// before -i also avoids decoding the whole image for every track.
			arglist.AddRange([
				"-loglevel", "warning",
				"-y",
				"-ss", FormatTime(ss)
			]);
			if (to < TimeSpan.MaxValue)
				arglist.AddRange([
					"-to", FormatTime(to)
				]);
			arglist.AddRange([
				"-i", t.filepath
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

		private static string FormatTime(TimeSpan value) {
			long totalMilliseconds = (long)Math.Round(value.TotalMilliseconds, MidpointRounding.AwayFromZero);
			TimeSpan rounded = TimeSpan.FromMilliseconds(totalMilliseconds);
			return string.Create(CultureInfo.InvariantCulture,
				$"{(long)rounded.TotalHours:D2}:{rounded.Minutes:D2}:{rounded.Seconds:D2}.{rounded.Milliseconds:D3}");
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