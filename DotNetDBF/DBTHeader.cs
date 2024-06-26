﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DotNetDBF
{
    public class DBTHeader
    {
        public const byte FieldTerminator = 0x1A;


        private int _nextBlock; /* 0-3*/
        private byte _version = 0x00;

        internal int NextBlock
        {
            get => _nextBlock;
            set => _nextBlock = value;
        }

        internal byte Version
        {
            get => _version;
            set => _version = value;
        }

        internal void Write(BinaryWriter dataOutput)
        {
            dataOutput.Write(new byte[3]);
            dataOutput.Write((int)0x00);
            dataOutput.Write((int)0x40);
            dataOutput.Write(new byte[8]);
            dataOutput.Write(_version);
            dataOutput.Write(new byte[484]);
        }
    }
}