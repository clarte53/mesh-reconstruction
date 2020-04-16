#if USE_PLY
// Adapted from
// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Pcx
{
	class Ply_PointCloudProvider : PointCloudProvider
	{
		[SerializeField] int _pointCount;

		[SerializeField] string filename = "toto.ply";

		#region MonoBevaviour callbacks
		private void Start()
		{
			Import(filename);
		}
		#endregion

		#region Internal methods

		public void Import(string filename)
		{
			PlyFile ply = LoadPly(filename);

			CreateTextures(ply.body.vertices, ply.body.colors);
		}

		#endregion

		#region Internal data structure

		enum DataProperty
		{
			Invalid,
			R8, G8, B8, A8,
			R16, G16, B16, A16,
			SingleX, SingleY, SingleZ,
			DoubleX, DoubleY, DoubleZ,
			Data8, Data16, Data32, Data64
		}

		static int GetPropertySize(DataProperty p)
		{
			switch(p)
			{
				case DataProperty.R8: return 1;
				case DataProperty.G8: return 1;
				case DataProperty.B8: return 1;
				case DataProperty.A8: return 1;
				case DataProperty.R16: return 2;
				case DataProperty.G16: return 2;
				case DataProperty.B16: return 2;
				case DataProperty.A16: return 2;
				case DataProperty.SingleX: return 4;
				case DataProperty.SingleY: return 4;
				case DataProperty.SingleZ: return 4;
				case DataProperty.DoubleX: return 8;
				case DataProperty.DoubleY: return 8;
				case DataProperty.DoubleZ: return 8;
				case DataProperty.Data8: return 1;
				case DataProperty.Data16: return 2;
				case DataProperty.Data32: return 4;
				case DataProperty.Data64: return 8;
			}
			return 0;
		}

		class DataHeader
		{
			public List<DataProperty> properties = new List<DataProperty>();
			public int vertexCount = -1;
		}

		class DataBody
		{
			public List<Vector3> vertices;
			public List<Color32> colors;

			public DataBody(int vertexCount)
			{
				vertices = new List<Vector3>(vertexCount);
				colors = new List<Color32>(vertexCount);
			}

			public void AddPoint(
				float x, float y, float z,
				byte r, byte g, byte b, byte a
			)
			{
				vertices.Add(new Vector3(x, y, z));
				colors.Add(new Color32(r, g, b, a));
			}
		}

		class PlyFile
		{
			public DataHeader header;
			public DataBody body;

			public PlyFile(DataHeader _header, DataBody _body)
			{
				header = _header;
				body = _body;
			}
		}

		#endregion

		#region Reader implementation
		PlyFile LoadPly(string path)
		{
			try
			{
				var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
				var header = ReadDataHeader(new StreamReader(stream));
				var body = ReadDataBody(header, new BinaryReader(stream));

				return new PlyFile(header, body);
			}
			catch(Exception e)
			{
				Debug.LogError("Failed importing " + path + ". " + e.Message);

				return null;
			}
		}

		DataHeader ReadDataHeader(StreamReader reader)
		{
			var data = new DataHeader();
			var readCount = 0;

			// Magic number line ("ply")
			var line = reader.ReadLine();
			readCount += line.Length + 1;
			if(line != "ply")
				throw new ArgumentException("Magic number ('ply') mismatch.");

			// Data format: check if it's binary/little endian.
			line = reader.ReadLine();
			readCount += line.Length + 1;
			if(line != "format binary_little_endian 1.0")
				throw new ArgumentException(
					"Invalid data format ('" + line + "'). " +
					"Should be binary/little endian.");

			// Read header contents.
			for(var skip = false; ;)
			{
				// Read a line and split it with white space.
				line = reader.ReadLine();
				readCount += line.Length + 1;
				if(line == "end_header") break;
				var col = line.Split();

				// Element declaration (unskippable)
				if(col[0] == "element")
				{
					if(col[1] == "vertex")
					{
						data.vertexCount = Convert.ToInt32(col[2]);
						skip = false;
					}
					else
					{
						// Don't read elements other than vertices.
						skip = true;
					}
				}

				if(skip) continue;

				// Property declaration line
				if(col[0] == "property")
				{
					var prop = DataProperty.Invalid;

					// Parse the property name entry.
					switch(col[2])
					{
						case "red": prop = DataProperty.R8; break;
						case "green": prop = DataProperty.G8; break;
						case "blue": prop = DataProperty.B8; break;
						case "alpha": prop = DataProperty.A8; break;
						case "x": prop = DataProperty.SingleX; break;
						case "y": prop = DataProperty.SingleY; break;
						case "z": prop = DataProperty.SingleZ; break;
					}

					// Check the property type.
					if(col[1] == "char" || col[1] == "uchar" ||
						col[1] == "int8" || col[1] == "uint8")
					{
						if(prop == DataProperty.Invalid)
							prop = DataProperty.Data8;
						else if(GetPropertySize(prop) != 1)
							throw new ArgumentException("Invalid property type ('" + line + "').");
					}
					else if(col[1] == "short" || col[1] == "ushort" ||
							 col[1] == "int16" || col[1] == "uint16")
					{
						switch(prop)
						{
							case DataProperty.Invalid: prop = DataProperty.Data16; break;
							case DataProperty.R8: prop = DataProperty.R16; break;
							case DataProperty.G8: prop = DataProperty.G16; break;
							case DataProperty.B8: prop = DataProperty.B16; break;
							case DataProperty.A8: prop = DataProperty.A16; break;
						}
						if(GetPropertySize(prop) != 2)
							throw new ArgumentException("Invalid property type ('" + line + "').");
					}
					else if(col[1] == "int" || col[1] == "uint" || col[1] == "float" ||
							 col[1] == "int32" || col[1] == "uint32" || col[1] == "float32")
					{
						if(prop == DataProperty.Invalid)
							prop = DataProperty.Data32;
						else if(GetPropertySize(prop) != 4)
							throw new ArgumentException("Invalid property type ('" + line + "').");
					}
					else if(col[1] == "int64" || col[1] == "uint64" ||
							 col[1] == "double" || col[1] == "float64")
					{
						switch(prop)
						{
							case DataProperty.Invalid: prop = DataProperty.Data64; break;
							case DataProperty.SingleX: prop = DataProperty.DoubleX; break;
							case DataProperty.SingleY: prop = DataProperty.DoubleY; break;
							case DataProperty.SingleZ: prop = DataProperty.DoubleZ; break;
						}
						if(GetPropertySize(prop) != 8)
							throw new ArgumentException("Invalid property type ('" + line + "').");
					}
					else
					{
						throw new ArgumentException("Unsupported property type ('" + line + "').");
					}

					data.properties.Add(prop);
				}
			}

			// Rewind the stream back to the exact position of the reader.
			reader.BaseStream.Position = readCount;

			return data;
		}

		DataBody ReadDataBody(DataHeader header, BinaryReader reader)
		{
			var data = new DataBody(header.vertexCount);

			float x = 0, y = 0, z = 0;
			Byte r = 255, g = 255, b = 255, a = 255;

			for(var i = 0; i < header.vertexCount; i++)
			{
				foreach(var prop in header.properties)
				{
					switch(prop)
					{
						case DataProperty.R8: r = reader.ReadByte(); break;
						case DataProperty.G8: g = reader.ReadByte(); break;
						case DataProperty.B8: b = reader.ReadByte(); break;
						case DataProperty.A8: a = reader.ReadByte(); break;

						case DataProperty.R16: r = (byte)(reader.ReadUInt16() >> 8); break;
						case DataProperty.G16: g = (byte)(reader.ReadUInt16() >> 8); break;
						case DataProperty.B16: b = (byte)(reader.ReadUInt16() >> 8); break;
						case DataProperty.A16: a = (byte)(reader.ReadUInt16() >> 8); break;

						case DataProperty.SingleX: x = reader.ReadSingle(); break;
						case DataProperty.SingleY: y = reader.ReadSingle(); break;
						case DataProperty.SingleZ: z = reader.ReadSingle(); break;

						case DataProperty.DoubleX: x = (float)reader.ReadDouble(); break;
						case DataProperty.DoubleY: y = (float)reader.ReadDouble(); break;
						case DataProperty.DoubleZ: z = (float)reader.ReadDouble(); break;

						case DataProperty.Data8: reader.ReadByte(); break;
						case DataProperty.Data16: reader.BaseStream.Position += 2; break;
						case DataProperty.Data32: reader.BaseStream.Position += 4; break;
						case DataProperty.Data64: reader.BaseStream.Position += 8; break;
					}
				}

				data.AddPoint(x, y, z, r, g, b, a);
			}

			return data;
		}
		#endregion

		#region Texture packing
		public void CreateTextures(List<Vector3> positions, List<Color32> colors)
		{
			_pointCount = positions.Count;

			var width = Mathf.CeilToInt(Mathf.Sqrt(_pointCount));

			Texture2D vertex_texture = new Texture2D(width, width, TextureFormat.RGBAFloat, false);
			vertex_texture.name = "Position Map";
			vertex_texture.filterMode = FilterMode.Point;

			Texture2D color_texture = new Texture2D(width, width, TextureFormat.BGRA32, false);
			color_texture.name = "Color Map";
			color_texture.filterMode = FilterMode.Point;

			var i1 = 0;
			var i2 = 0U;

			for(var y = 0; y < width; y++)
			{
				for(var x = 0; x < width; x++)
				{
					var i = i1 < _pointCount ? i1 : (int)(i2 % _pointCount);
					var p = positions[i];

					vertex_texture.SetPixel(x, y, new Color(p.x, p.y, p.z));
					color_texture.SetPixel(x, y, colors[i]);

					i1++;
					i2 += 132049U; // prime
				}
			}

			vertex_texture.Apply(false, true);
			color_texture.Apply(false, true);

			vertexTexture = vertex_texture;
			colorTexture = color_texture;
		}
		#endregion
	}
}
#endif