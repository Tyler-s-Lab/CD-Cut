using CueSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
				return;
			}

			cuePath = cuePath.Trim('"');
			ProcessOnCue(cuePath);

			Console.WriteLine("Press any key to continue . . .");
			Console.ReadKey();
		}

		private static void ProcessOnCue(string cuePath) {
			var cueSheet = new CueSheet(cuePath);

			foreach (Track track in cueSheet.Tracks) {
				string sourceFilePath = track.DataFile.Filename;
				Console.WriteLine($"原始文件: {sourceFilePath}");
				

				Console.WriteLine($"{track.PreGap.Number} - {track.Offset} - {track.PostGap.Number}");
			}

			Console.WriteLine();
			Console.WriteLine();
			//Console.WriteLine($"总计 {albumData.AudioTracks.Count} 个 track。");
			Console.WriteLine();
			Console.WriteLine();
		}

	}
}