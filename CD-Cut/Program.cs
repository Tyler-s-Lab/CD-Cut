using System.ComponentModel;
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

			if (cue.Tracks.Count == 0) {
				Console.Error.WriteLine($"No audio tracks found in {cuePath}.");
				return 1;
			}

			string? ffmpeg = FindFfmpeg();
			if (ffmpeg is null) {
				Console.Error.WriteLine("ffmpeg.exe was not found in PATH or next to this program.");
				return 1;
			}

			string dir = Path.GetDirectoryName(cuePath) ?? "";
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			// Falling back to an audio file named after the cue only makes sense
			// for a single-file image; with several FILE blocks it would silently
			// substitute the whole disc for a missing track file.
			bool allowCueNameFallback = cue.Tracks
				.Select(track => track.filepath)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Count() == 1;

			string cueName = Path.GetFileNameWithoutExtension(cuePath);
			string cur_source = "";
			int failures = 0;

			for (int i = 0, n = cue.Tracks.Count; i < n; ++i) {
				Track t = cue.Tracks[i];

				string? source = CheckAudioFile(dir, t.filepath, cueName, allowCueNameFallback);
				if (string.IsNullOrEmpty(source)) {
					Console.Error.WriteLine($"Track {t.index}: audio file '{t.filepath}' was not found; skipping.");
					++failures;
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
							++failures;
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

				if (!Cut(t, ss, to, cue.AlbumMetadata, n, dir, ffmpeg))
					++failures;
			}

			Console.WriteLine($"{cue.Tracks.Count - failures} of {cue.Tracks.Count} track(s) written.");
			return failures;
		}

		public static bool Cut(Track t, TimeSpan ss, TimeSpan to, Metadata album, int totalTracks, string outputDir, string ffmpeg) {
			string title = CleanNameForPath(t.metadata.Title ?? "").Trim().TrimEnd('.');
			if (title.Length == 0)
				title = "untitled";
			if (title.Length > 120)
				title = title[..120].TrimEnd().TrimEnd('.');

			string filename = string.Create(CultureInfo.InvariantCulture, $"{t.index:D2} {title}.flac");
			string targetPath = Path.Combine(outputDir, filename);

			List<string> arglist = [];
			// -ss and -to must both be input options: -to is measured against the input
			// timeline, while an output -to would be relative to the seek point. Seeking
			// before -i also avoids decoding the whole image for every track.
			arglist.AddRange([
				"-loglevel", "warning",
				"-y",
				"-nostdin",
				"-ss", FormatTime(ss)
			]);
			if (to < TimeSpan.MaxValue)
				arglist.AddRange([
					"-to", FormatTime(to)
				]);
			arglist.AddRange([
				"-i", t.filepath,
				// Do not inherit tags from the source image; only the tags below apply.
				"-map_metadata", "-1",
				"-metadata", $"TRACKNUMBER={t.index}",
				"-metadata", $"TRACKTOTAL={totalTracks}"
			]);

			if (!string.IsNullOrEmpty(t.metadata.Title))
				arglist.AddRange(["-metadata", $"TITLE={t.metadata.Title}"]);
			if (!string.IsNullOrEmpty(t.metadata.Album))
				arglist.AddRange(["-metadata", $"ALBUM={t.metadata.Album}"]);
			if (!string.IsNullOrEmpty(t.metadata.Performer))
				arglist.AddRange(["-metadata", $"ARTIST={t.metadata.Performer}"]);
			if (!string.IsNullOrEmpty(album.Performer))
				arglist.AddRange(["-metadata", $"ALBUMARTIST={album.Performer}"]);
			if (!string.IsNullOrEmpty(t.metadata.Year))
				arglist.AddRange(["-metadata", $"DATE={t.metadata.Year}"]);
			if (!string.IsNullOrEmpty(t.metadata.DiscID))
				arglist.AddRange(["-metadata", $"DISCID={t.metadata.DiscID}"]);
			string? composer = !string.IsNullOrEmpty(t.metadata.Composer) ? t.metadata.Composer : t.metadata.Songwriter;
			if (!string.IsNullOrEmpty(composer))
				arglist.AddRange(["-metadata", $"COMPOSER={composer}"]);
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

			ProcessStartInfo processStartInfo = new(ffmpeg, arglist);

			try {
				using var process = Process.Start(processStartInfo);
				if (process is null) {
					Console.Error.WriteLine($"Track {t.index}: could not start ffmpeg.");
					TryDelete(targetPath);
					return false;
				}
				process.WaitForExit();
				if (process.ExitCode != 0) {
					Console.Error.WriteLine($"Track {t.index}: ffmpeg exited with code {process.ExitCode}; discarding the partial file.");
					TryDelete(targetPath);
					return false;
				}
			}
			catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException) {
				Console.Error.WriteLine($"Track {t.index}: failed to run ffmpeg: {ex.Message}");
				TryDelete(targetPath);
				return false;
			}

			return true;
		}

		private static string? FindFfmpeg() {
			string exeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

			string local = Path.Combine(AppContext.BaseDirectory, exeName);
			if (File.Exists(local))
				return local;

			string? path = Environment.GetEnvironmentVariable("PATH");
			if (path is null)
				return null;

			foreach (string entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)) {
				string directory = entry.Trim().Trim('"');
				if (directory.Length == 0)
					continue;
				string candidate;
				try {
					candidate = Path.Combine(directory, exeName);
				}
				catch (ArgumentException) {
					continue;
				}
				if (File.Exists(candidate))
					return candidate;
			}

			return null;
		}

		private static void TryDelete(string path) {
			try {
				if (File.Exists(path))
					File.Delete(path);
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}

		private static string FormatTime(TimeSpan value) {
			long totalMilliseconds = (long)Math.Round(value.TotalMilliseconds, MidpointRounding.AwayFromZero);
			TimeSpan rounded = TimeSpan.FromMilliseconds(totalMilliseconds);
			return string.Create(CultureInfo.InvariantCulture,
				$"{(long)rounded.TotalHours:D2}:{rounded.Minutes:D2}:{rounded.Seconds:D2}.{rounded.Milliseconds:D3}");
		}

		private static readonly string[] AudioExtensions = [".flac", ".wav", ".wave", ".ape", ".mp3", ".aiff", ".aif"];

		private static string? CheckAudioFile(string dir, string filepath, string cuename, bool allowCueNameFallback) {
			string direct = Path.Combine(dir, filepath);
			if (File.Exists(direct))
				return direct;

			string? found = FindWithExtensions(dir, Path.GetDirectoryName(filepath), Path.GetFileNameWithoutExtension(filepath));
			if (found is not null)
				return found;

			return allowCueNameFallback ? FindWithExtensions(dir, null, cuename) : null;
		}

		private static string? FindWithExtensions(string dir, string? subdir, string stem) {
			string searchDir = string.IsNullOrEmpty(subdir) ? dir : Path.Combine(dir, subdir);
			foreach (string extension in AudioExtensions) {
				string candidate = Path.Combine(searchDir, stem + extension);
				if (File.Exists(candidate))
					return candidate;
			}
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