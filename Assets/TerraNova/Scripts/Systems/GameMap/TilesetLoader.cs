using System.IO;
using UnityEngine;

namespace TerraNova.Systems.GameMap
{
	/// <summary>
	/// Parses OP2's custom "PBMP" tileset format used by the wellNNNN.bmp files inside maps.vol.
	/// These are NOT standard Windows BMPs — they're RIFF-style chunked palettes + indexed pixel data:
	///
	///   "PBMP" + uint32 contentSize
	///   "head" + uint32 size=20 + (uint32, int32 width, int32 height, uint32 bitDepth, uint32)
	///   "PPAL" + uint32 size
	///     "head" + uint32 size=4 + uint32 paletteCount
	///     "data" + uint32 size=1024 + 256 BGRA palette entries
	///   "data" + uint32 size + (width * height) bytes of 8-bit palette indices
	///
	/// Width is always 32 (one tile wide); height is 32 * numTiles for the vertical strip.
	/// </summary>
	public static class TilesetLoader
	{
		private const int PaletteEntries = 256;

		/// <summary>
		/// Decodes the given PBMP byte buffer into a Unity Texture2D. The texture preserves the
		/// vertical strip layout — tile 0 occupies the top 32 rows (visually), then tile 1, etc.
		/// In Unity texture coords (Y-up, origin bottom-left) tile 0 sits in the topmost 32 pixel rows.
		/// </summary>
		public static Texture2D Load(byte[] data, string debugName)
		{
			using (MemoryStream ms = new MemoryStream(data))
			using (BinaryReader br = new BinaryReader(ms))
			{
				ExpectTag(br, "PBMP", debugName);
				br.ReadUInt32(); // content size — not needed for parsing

				// "head" chunk: image dimensions
				ExpectTag(br, "head", debugName);
				uint headSize = br.ReadUInt32();
				if (headSize != 20)
					Debug.LogWarning("[TilesetLoader] " + debugName + ": unexpected head size " + headSize + " (expected 20)");
				br.ReadUInt32();                // f1 — typically 2
				int width  = br.ReadInt32();
				int height = br.ReadInt32();
				uint bitDepth = br.ReadUInt32();
				br.ReadUInt32();                // f5 — typically 8

				if (bitDepth != 8)
					throw new System.Exception("[TilesetLoader] " + debugName + ": unsupported bitDepth " + bitDepth + " (only 8 supported)");
				if (width <= 0 || height <= 0)
					throw new System.Exception("[TilesetLoader] " + debugName + ": invalid dimensions " + width + "x" + height);

				// "PPAL" container with nested "head" + "data"
				ExpectTag(br, "PPAL", debugName);
				br.ReadUInt32();                // total PPAL size

				ExpectTag(br, "head", debugName);
				uint palHeadSize = br.ReadUInt32();
				uint paletteCount = br.ReadUInt32();
				if (palHeadSize != 4)
					Debug.LogWarning("[TilesetLoader] " + debugName + ": unexpected palette-head size " + palHeadSize);
				if (paletteCount != 1)
					Debug.LogWarning("[TilesetLoader] " + debugName + ": unexpected palette count " + paletteCount + " — using first palette");

				ExpectTag(br, "data", debugName);
				uint palDataSize = br.ReadUInt32();
				if (palDataSize != PaletteEntries * 4)
					Debug.LogWarning("[TilesetLoader] " + debugName + ": palette data size " + palDataSize + " (expected " + (PaletteEntries * 4) + ")");

				// OP2 stores palette entries as R, G, B, pad (verified against the OP2Mapper reference tool).
				Color32[] palette = new Color32[PaletteEntries];
				for (int i = 0; i < PaletteEntries; i++)
				{
					byte r = br.ReadByte();
					byte g = br.ReadByte();
					byte b = br.ReadByte();
					br.ReadByte();              // padding / alpha — discarded; OP2 palettes are opaque
					palette[i] = new Color32(r, g, b, 255);
				}

				// "data" chunk: pixel data, 8-bit palette indices, width * height bytes
				ExpectTag(br, "data", debugName);
				uint pixelDataSize = br.ReadUInt32();
				int expectedPixelBytes = width * height;
				if (pixelDataSize < expectedPixelBytes)
					throw new System.Exception("[TilesetLoader] " + debugName + ": pixel data size " + pixelDataSize + " < expected " + expectedPixelBytes);

				byte[] indices = br.ReadBytes(expectedPixelBytes);

				// Decode into a Texture2D. In source-file order, row 0 is the TOP of the image (Y-down).
				// Unity Texture2D uses Y-up, so to keep "tile 0 at the top" we write source row r into texture
				// row (height - 1 - r).
				Color32[] pixels = new Color32[width * height];
				for (int srcY = 0; srcY < height; srcY++)
				{
					int srcRow = srcY * width;
					int dstRow = (height - 1 - srcY) * width;
					for (int x = 0; x < width; x++)
					{
						pixels[dstRow + x] = palette[indices[srcRow + x]];
					}
				}

				Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
				tex.name       = debugName;
				tex.filterMode = FilterMode.Point;
				tex.wrapMode   = TextureWrapMode.Clamp;
				tex.SetPixels32(pixels);
				tex.Apply(false, false);
				return tex;
			}
		}

		private static void ExpectTag(BinaryReader br, string expected, string debugName)
		{
			byte[] raw = br.ReadBytes(4);
			string actual = System.Text.Encoding.ASCII.GetString(raw);
			if (actual != expected)
				throw new System.Exception("[TilesetLoader] " + debugName + ": expected tag '" + expected + "' but found '" + actual + "' at offset " + (br.BaseStream.Position - 4));
		}
	}
}
