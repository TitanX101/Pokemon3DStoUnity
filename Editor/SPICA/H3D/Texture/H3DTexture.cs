﻿using System;
using System.IO;
using P3DS2U.Editor.SPICA.Bitmap;
using P3DS2U.Editor.SPICA.Commands;
using P3DS2U.Editor.SPICA.Converters;
using P3DS2U.Editor.SPICA.PICA;
using P3DS2U.Editor.SPICA.Serialization;
using P3DS2U.Editor.SPICA.Serialization.Attributes;
using P3DS2U.Editor.SPICA.Serialization.Serializer; // using System.Drawing.Imaging;

namespace P3DS2U.Editor.SPICA.H3D.Texture
{
    public class H3DTexture : ICustomSerialization, ICustomSerializeCmd, INamed
    {
        private byte _Format;

        [Ignore] public int Height;

        [Padding (4)] public byte MipmapSize;
        [Ignore] public byte[] RawBufferXNeg;

        [Ignore] public byte[] RawBufferXPos;
        [Ignore] public byte[] RawBufferYNeg;
        [Ignore] public byte[] RawBufferYPos;
        [Ignore] public byte[] RawBufferZNeg;
        [Ignore] public byte[] RawBufferZPos;
        private uint[] Texture0Commands;
        private uint[] Texture1Commands;
        private uint[] Texture2Commands;

        [Ignore] public int Width;

        public H3DTexture ()
        {
        }

        public H3DTexture (string FileName)
        {
            var Img = new Bitmap.Bitmap (FileName);

            if (Img.PixelFormat != PixelFormat.Format32bppArgb) Img = new Bitmap.Bitmap (Img);

            using (Img) {
                Name = Path.GetFileNameWithoutExtension (FileName);

                Format = PICATextureFormat.RGBA8;

                H3DTextureImpl (Img);
            }
        }

        public H3DTexture (string Name, Bitmap.Bitmap Img, PICATextureFormat Format = 0)
        {
            this.Name = Name;
            this.Format = Format;

            H3DTextureImpl (Img);
        }

        public PICATextureFormat Format {
            get => (PICATextureFormat) _Format;
            set => _Format = (byte) value;
        }

        public bool IsCubeTexture => RawBufferZNeg != null;

        public byte[] RawBuffer {
            get => RawBufferXPos;
            set => RawBufferXPos = value;
        }

        void ICustomSerialization.Deserialize (BinaryDeserializer Deserializer)
        {
            var Reader = new PICACommandReader (Texture0Commands);

            var Address = new uint[6];

            while (Reader.HasCommand) {
                var Cmd = Reader.GetCommand ();

                var Param = Cmd.Parameters[0];

                switch (Cmd.Register) {
                    case PICARegister.GPUREG_TEXUNIT0_DIM:
                        Height = (int) (Param >> 0) & 0x7ff;
                        Width = (int) (Param >> 16) & 0x7ff;
                        break;
                    case PICARegister.GPUREG_TEXUNIT0_ADDR1:
                        Address[0] = Param;
                        break;
                    case PICARegister.GPUREG_TEXUNIT0_ADDR2:
                        Address[1] = Param;
                        break;
                    case PICARegister.GPUREG_TEXUNIT0_ADDR3:
                        Address[2] = Param;
                        break;
                    case PICARegister.GPUREG_TEXUNIT0_ADDR4:
                        Address[3] = Param;
                        break;
                    case PICARegister.GPUREG_TEXUNIT0_ADDR5:
                        Address[4] = Param;
                        break;
                    case PICARegister.GPUREG_TEXUNIT0_ADDR6:
                        Address[5] = Param;
                        break;
                }
            }

            var Length = TextureConverter.CalculateLength (Width, Height, Format);

            var Position = Deserializer.BaseStream.Position;

            for (var Face = 0; Face < 6; Face++) {
                if (Address[Face] == 0) break;

                Deserializer.BaseStream.Seek (Address[Face], SeekOrigin.Begin);

                switch (Face) {
                    case 0:
                        RawBufferXPos = Deserializer.Reader.ReadBytes (Length);
                        break;
                    case 1:
                        RawBufferXNeg = Deserializer.Reader.ReadBytes (Length);
                        break;
                    case 2:
                        RawBufferYPos = Deserializer.Reader.ReadBytes (Length);
                        break;
                    case 3:
                        RawBufferYNeg = Deserializer.Reader.ReadBytes (Length);
                        break;
                    case 4:
                        RawBufferZPos = Deserializer.Reader.ReadBytes (Length);
                        break;
                    case 5:
                        RawBufferZNeg = Deserializer.Reader.ReadBytes (Length);
                        break;
                }
            }

            Deserializer.BaseStream.Seek (Position, SeekOrigin.Begin);
        }

        bool ICustomSerialization.Serialize (BinarySerializer Serializer)
        {
            if (Width > 1024 || Height > 1024)
                //PICA max texture size is 1024x1024. Anything bigger than this makes it hang?
                throw new OverflowException ($"Texture \"{Name}\" with size {Width}x{Height} is too big!");

            for (var Unit = 0; Unit < 3; Unit++) {
                var Writer = new PICACommandWriter ();

                var Resolution = (uint) (Height | (Width << 16));

                switch (Unit) {
                    case 0:
                        Writer.SetCommand (PICARegister.GPUREG_TEXUNIT0_DIM, Resolution);
                        Writer.SetCommand (PICARegister.GPUREG_TEXUNIT0_LOD, MipmapSize);
                        Writer.SetCommand (PICARegister.GPUREG_TEXUNIT0_ADDR1, 0);
                        Writer.SetCommand (PICARegister.GPUREG_TEXUNIT0_TYPE, (uint) Format);
                        break;

                    case 1:
                        Writer.SetCommand (PICARegister.GPUREG_TEXUNIT1_DIM, Resolution);
                        Writer.SetCommand (PICARegister.GPUREG_TEXUNIT1_LOD, MipmapSize);
                        Writer.SetCommand (PICARegister.GPUREG_TEXUNIT1_ADDR, 0);
                        Writer.SetCommand (PICARegister.GPUREG_TEXUNIT1_TYPE, (uint) Format);
                        break;

                    case 2:
                        Writer.SetCommand (PICARegister.GPUREG_TEXUNIT2_DIM, Resolution);
                        Writer.SetCommand (PICARegister.GPUREG_TEXUNIT2_LOD, MipmapSize);
                        Writer.SetCommand (PICARegister.GPUREG_TEXUNIT2_ADDR, 0);
                        Writer.SetCommand (PICARegister.GPUREG_TEXUNIT2_TYPE, (uint) Format);
                        break;
                }

                Writer.WriteEnd ();

                switch (Unit) {
                    case 0:
                        Texture0Commands = Writer.GetBuffer ();
                        break;
                    case 1:
                        Texture1Commands = Writer.GetBuffer ();
                        break;
                    case 2:
                        Texture2Commands = Writer.GetBuffer ();
                        break;
                }
            }

            return false;
        }

        void ICustomSerializeCmd.SerializeCmd (BinarySerializer Serializer, object Value)
        {
            //TODO: Write all 6 faces of a Cube Map
            var Position = Serializer.BaseStream.Position + 0x10;

            H3DRelocator.AddCmdReloc (Serializer, H3DSection.RawDataTexture, Position);

            Serializer.Sections[(uint) H3DSectionId.RawData].Values.Add (new RefValue {
                Parent = this,
                Value = RawBufferXPos,
                Position = Position
            });
        }

        public string Name { get; set; }

        private void H3DTextureImpl (Bitmap.Bitmap Img)
        {
            MipmapSize = 1;

            Width = (int) BitUtils.Pow2RoundDown ((uint) Img.Width);
            Height = (int) BitUtils.Pow2RoundDown ((uint) Img.Height);

            if (Img.Width != Width ||
                Img.Height != Height)
                /*
                     * 3DS GPU only accepts textures with power of 2 sizes.
                     * If the texture doesn't have a power of 2 size, we need to resize it then.
                     */
                using (var NewImg = new Bitmap.Bitmap (Img, Width, Height)) {
                    RawBufferXPos = TextureConverter.Encode (NewImg, Format);
                }
            else
                RawBufferXPos = TextureConverter.Encode (Img, Format);
        }

        public byte[] ToRGBA (int Face = 0)
        {
            return TextureConverter.DecodeBuffer (BufferFromFace (Face), Width, Height, Format);
        }

        public Bitmap.Bitmap ToBitmap (int Face = 0)
        {
            return TextureConverter.DecodeBitmap (BufferFromFace (Face), Width, Height, Format);
        }

        private byte[] BufferFromFace (int Face)
        {
            switch (Face) {
                case 0: return RawBufferXPos;
                case 1: return RawBufferXNeg;
                case 2: return RawBufferYPos;
                case 3: return RawBufferYNeg;
                case 4: return RawBufferZPos;
                case 5: return RawBufferZNeg;

                default: throw new ArgumentOutOfRangeException ("Expected a value in 0-6 range!");
            }
        }

        public void ReplaceData (H3DTexture Texture)
        {
            Format = Texture.Format;

            MipmapSize = Texture.MipmapSize;

            RawBufferXPos = Texture.RawBufferXPos;

            Width = Texture.Width;
            Height = Texture.Height;
        }
    }
}