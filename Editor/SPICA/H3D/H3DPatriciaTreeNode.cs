﻿namespace P3DS2U.Editor.SPICA.H3D
{
    public struct H3DPatriciaTreeNode : IPatriciaTreeNode
    {
        public uint ReferenceBit { get; set; }

        public ushort LeftNodeIndex { get; set; }

        public ushort RightNodeIndex { get; set; }

        public string Name { get; set; }
    }
}