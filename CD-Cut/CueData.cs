using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CD_Cut {
	public struct OffsetData() {
		public int Index = 0;
		public TimeSpan Time = new();
	}

	public struct Metadata() {
		public string? Title = null;
		public string? Album = null;
		public string? Performer = null;

		public string? Year = null;
		public string? DiscID = null;
		public string? Composer = null;
		public string? Genre = null;
		public string? Comment = null;

		public string? Catalog = null;
		public string? ISRC = null;
	}

	public struct Track() {
		public int index = -1;
		public Metadata metadata;
		public string filepath = "";
		public List<OffsetData> offsets = [];
	}
}
